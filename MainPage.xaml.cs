using MauiAppMain.Models;
using MauiAppMain.Resources.Localization;
using MauiAppMain.Services;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MauiAppMain
{
    public partial class MainPage : ContentPage
    {
        // SERVICES
        private readonly DatabaseService _database;
        private readonly DataFetch _dataFetch;
        private readonly LanguageService _languageService;

        // DATA
        private ObservableCollection<PointOfInterest> _pois = new();
        private ObservableCollection<PointOfInterest> _favorites = new();
        private ObservableCollection<PointOfInterest> _displayedPois = new();

        private Dictionary<Pin, PointOfInterest> _pinPoiMap = new();

        private CancellationTokenSource _ttsCts;
        private CancellationTokenSource _timerCts;
        private CancellationTokenSource _cts;

        // UI STATE
        private bool _sheetVisible = false;
        private bool _mapInitialized = false;
        private bool _isPlaying = false;
        private int _audioSessionId = 0;
        private int _currentTab = 0;

        // BOTTOM SHEET
        private double _sheetFullY = 10;
        private double _sheetHalfY;
        private double _sheetHiddenY;
        private double _startY;

        // SELECTION
        private PointOfInterest _selectedPoi;
        private PointOfInterest _lastSpokenPoi;

        public PointOfInterest SelectedPoi
        {
            get => _selectedPoi;
            set
            {
                _selectedPoi = value;
                OnPropertyChanged();
            }
        }

        // CONSTRUCTOR
        public MainPage(DatabaseService database, DataFetch dataFetch)
        {
            InitializeComponent();

            _database = database;
            _dataFetch = dataFetch;

            BindingContext = this;

            LanguageService.LoadSavedLanguage();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            SearchEntry.Text = AppResource.Search_placeholder;

            var status = await Permissions.RequestAsync<Permissions.LocationAlways>();

            if (status != PermissionStatus.Granted)
            {
                await DisplayAlert("Permission", "Location permission required", "OK");
                return;
            }
            // start tracking user in real-time
#if ANDROID
            AndroidTtsService.OnSpeechCompleted = () =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _isPlaying = false;
                    UpdateAudioUI(false);
                });
            };

            if (!LocationForegroundService.IsRunning)
            {
                var intent = new Android.Content.Intent(
                Android.App.Application.Context,
                typeof(LocationForegroundService));

                Android.App.Application.Context.StartForegroundService(intent);
            }
#endif

            _displayedPois = new ObservableCollection<PointOfInterest>(_pois);
            PoiListView.ItemsSource = _displayedPois;

            var list = await _database.GetPOIsAsync();  // list từ DB

            _pois.Clear();                               // xóa dữ liệu cũ
            foreach (var poi in list)
                _pois.Add(poi);                          // thêm từng item vào ObservableCollection

            _favorites.Clear();
            foreach (var poi in _pois)
                if (poi.IsFavorite)
                    _favorites.Add(poi);

            if (_pois.Count > 0)
            {
                LoadPoisOnMap();

                var firstPoi = _pois[0];
                MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                    new Location(firstPoi.Latitude, firstPoi.Longitude),
                    Distance.FromMeters(500)));
            }
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            _sheetHiddenY = height;
            _sheetHalfY = height - 420;

            if (!_sheetVisible)
                PoiSheet.TranslationY = _sheetHiddenY;
        }

        // ---------------- MAP ----------------
        void LoadPoisOnMap()
        {
            MyMap.Pins.Clear();
            _pinPoiMap.Clear();

            foreach (var poi in _pois)
            {
                var pin = new Pin
                {
                    Label = poi.Name,
                    Address = poi.Description,
                    Location = new Location(poi.Latitude, poi.Longitude)
                };

                pin.MarkerClicked += OnPinClicked;

                MyMap.Pins.Add(pin);
                _pinPoiMap[pin] = poi;
            }
        }

        async void OnPinClicked(object sender, PinClickedEventArgs e)
        {
            e.HideInfoWindow = true;

            if (sender is Pin pin && _pinPoiMap.TryGetValue(pin, out var poi))
                await ShowPoiWithTransition(poi);
        }

        // ---------------- BOTTOM SHEET ----------------
        async Task ShowPoiWithTransition(PointOfInterest poi)
        {
            SelectedPoi = poi;

            if (!_sheetVisible)
            {
                PoiSheet.TranslationY = _sheetHalfY;
                _sheetVisible = true;
            }

            SinglePoiView.IsVisible = true;
            PoiListContainer.IsVisible = false;

            await PoiSheet.TranslateTo(0, _sheetHalfY, 200);
        }

        async Task HideBottomSheet()
        {
            _sheetVisible = false;
            await PoiSheet.TranslateTo(0, _sheetHiddenY, 150);
        }

        private async void OnSheetPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _startY = PoiSheet.TranslationY;
                    break;

                case GestureStatus.Running:
                    double targetY = _startY + e.TotalY;
                    PoiSheet.TranslationY = Math.Clamp(targetY, _sheetFullY, _sheetHiddenY);
                    break;

                case GestureStatus.Completed:
                    if (PoiSheet.TranslationY > _sheetHalfY + 50)
                        await HideBottomSheet();
                    else
                        await PoiSheet.TranslateTo(0, _sheetHalfY, 200);
                    break;
            }
        }

        // ---------------- AUDIO ----------------
        private Task PlayAudio()
        {
            if (SelectedPoi == null) return Task.CompletedTask;

            _isPlaying = true;
            UpdateAudioUI(true);

            _audioSessionId++;

            //  USE ANDROID TTS (same as service)
#if ANDROID
            AndroidTtsService.Speak(SelectedPoi.Description);
#endif
            return Task.CompletedTask;
        }

        private void StopAudio()
        {
            _isPlaying = false;

            //  STOP GLOBAL TTS
#if ANDROID
            AndroidTtsService.Stop();
#endif
            UpdateAudioUI(false);
        }

        private async void OnAudioToggleClicked(object sender, EventArgs e)
        {
            if (_isPlaying)
                StopAudio();
            else
                await PlayAudio();
        }

        private void UpdateAudioUI(bool isPlaying)
        {
            if (isPlaying)
            {
                AudioStatusLabel.Text = "ĐANG PHÁT...";
                AudioIconBtn.Source = "pause_icon.png";
            }
            else
            {
                AudioStatusLabel.Text = "PHÁT AUDIO";
                AudioIconBtn.Source = "play_icon.png";
            }
        }

        // ---------------- FAVORITES ----------------
        private async Task ToggleFavorite(PointOfInterest poi)
        {
            if (poi == null) return;

            // 🔥 find real DB entity
            var realPoi = (await _database.GetAllPoisAsync())
                .FirstOrDefault(p => p.Id == poi.Id);

            if (realPoi == null) return;

            // update DB entity
            realPoi.IsFavorite = !realPoi.IsFavorite;

            await _database.UpdatePoiAsync(realPoi);

            // 🔥 update UI model too
            poi.IsFavorite = realPoi.IsFavorite;

            // update favorites list
            if (poi.IsFavorite)
            {
                if (!_favorites.Any(f => f.Id == poi.Id))
                    _favorites.Add(poi);
            }
            else
            {
                var item = _favorites.FirstOrDefault(f => f.Id == poi.Id);
                if (item != null)
                    _favorites.Remove(item);
            }
        }

        private async void OnFavoriteToggleClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is PointOfInterest poi)
                await ToggleFavorite(poi);
        }

        // ---------------- TABS ----------------
        private async void OnAllPoiTabClicked(object sender, EventArgs e)
        {
            _currentTab = 0;

            //  SWITCH TO LIST MODE
            SinglePoiView.IsVisible = false;
            PoiListContainer.IsVisible = true;

            _displayedPois.Clear();
            foreach (var poi in _pois)
            {
                Console.WriteLine("---- POI ----");

                foreach (var img in poi.ImageList)
                {
                    Console.WriteLine(img);
                }
                _displayedPois.Add(poi);
            }

            await PoiSheet.TranslateTo(0, _sheetFullY, 200);
        }

        private async void OnFavoriteTabClicked(object sender, EventArgs e)
        {
            _currentTab = 1;

            // SWITCH TO LIST MODE
            SinglePoiView.IsVisible = false;
            PoiListContainer.IsVisible = true;

            _displayedPois.Clear();
            foreach (var poi in _favorites)
                _displayedPois.Add(poi);

            await PoiSheet.TranslateTo(0, _sheetFullY, 200);
        }

        // ---------------- SEARCH ----------------
        private async void OnSearchTapped(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new SearchPage());
        }

        // ---------------- SEED ----------------
        async Task SeedData()
        {

        }

        // ---------------- MENU ----------------
        public async void OnMenuClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new SettingPage(_dataFetch, _database));
        }

        // ---------------- MAP TAP ----------------
        private async void OnMapClicked(object sender, MapClickedEventArgs e)
        {
            if (_sheetVisible)
                await HideBottomSheet();
        }
        private async void OnPoiItemSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is PointOfInterest poi)
            {
                // 🔥 Move map tới POI
                MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                    new Location(poi.Latitude, poi.Longitude),
                    Distance.FromMeters(300)));

                // 🔥 Mở detail luôn (tuỳ bạn)
                await ShowPoiWithTransition(poi);
            }

            // ❗ reset selection để không bị highlight
            ((CollectionView)sender).SelectedItem = null;
        }
    }
}