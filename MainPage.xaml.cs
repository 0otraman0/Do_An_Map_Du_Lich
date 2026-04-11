

using PointOfInterest = MauiAppMain.Models.PointOfInterest;
using MauiAppMain.Models;
using MauiAppMain.Resources.Localization;
using MauiAppMain.Services;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;

namespace MauiAppMain
{
    public partial class MainPage : ContentPage
    {
        // ALL AND FAVORITE


        // Hai biến để bật tắt nhanh Tab
        //private bool _isAllTabVisible = true;
        //public bool IsAllTabVisible { get => _isAllTabVisible; set { _isAllTabVisible = value; OnPropertyChanged(); } }
        //private bool _isFavoriteTabVisible = false;
        //public bool IsFavoriteTabVisible { get => _isFavoriteTabVisible; set { _isFavoriteTabVisible = value; OnPropertyChanged(); } }

        // SERVICES
        private readonly DatabaseService _database;
        private readonly DataFetch _dataFetch;
        private readonly LanguageService _languageService;

        // DATA
        private ObservableCollection<PointOfInterest> _pois = new();
        private ObservableCollection<PointOfInterest> _favorites = new();
        //private ObservableCollection<PointOfInterest> _displayedPois = new();

        private Dictionary<Pin, PointOfInterest> _pinPoiMap = new();


        private CancellationTokenSource? _ttsCts;
        private CancellationTokenSource? _timerCts;
        private CancellationTokenSource? _cts;
        private PointOfInterest? _selectedPoi;
        private PointOfInterest? _lastSpokenPoi;

        private bool _isDataLoaded = false; // Biến cờ

        // UI STATE
        private bool _sheetVisible = false;
        private bool _mapInitialized = false;
        private bool _isPlaying = false;
        private int _audioSessionId = 0;
        //private int _currentTab = 0;
        bool movedToUser = false;

        private double _lastLat = 0;
        private double _lastLng = 0;

        bool _isLoaded = false;

        private bool _isMapLoading = false; // Biến cờ ngăn chặn nạp chồng

        // BOTTOM SHEET
        private double _sheetFullY = 10;
        private double _sheetHalfY;
        private double _sheetHiddenY;
        private double _startY;

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

            // === PHẢI CÓ 2 DÒNG NÀY ===
            MyMap.PropertyChanged += OnMapPropertyChanged;
            MyMap.IsVisible = true;

            // Gán ItemsSource cho CollectionView mới
            //PoiListView.ItemsSource = _displayedPois;

            LanguageService.LoadSavedLanguage();
        }

        //private Task FilterDisplayedPois()
        //{
        //    var targetSource = _currentTab == 0 ? _pois : _favorites;

        //    _displayedPois.Clear();

        //    foreach (var poi in targetSource)
        //    {
        //        _displayedPois.Add(poi);
        //    }

        //    return Task.CompletedTask;
        //}

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (_isLoaded) return;

            try
            {
                // 1. Lấy dữ liệu ngầm (Luồng phụ)
                var list = await Task.Run(() => _database.GetPOIsAsync());
                var allList = list.ToList();
                var favList = list.Where(p => p.IsFavorite).ToList();

                // 2. Cập nhật dữ liệu lên UI (Luồng chính)
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    _pois.Clear();
                    _favorites.Clear();

                    foreach (var p in allList) _pois.Add(p);
                    foreach (var f in favList) _favorites.Add(f);

                    var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                    MoveMapToInitialLocation(status);

                    await Task.Delay(1000);
                    LoadPoisOnMap();
                    _isLoaded = true;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi: {ex.Message}");
            }
        }

        // Hàm phụ để tách code cho sạch sẽ, tránh rối mắt trong OnAppearing
        private async void MoveMapToInitialLocation(PermissionStatus status)
        {
            Location targetLocation = null;
            if (status == PermissionStatus.Granted)
            {
                try
                {
                    targetLocation = await Geolocation.GetLocationAsync(
                        new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(3)));
                }
                catch { }
            }

            if (targetLocation != null)
            {
                MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(targetLocation, Distance.FromMeters(500)));
            }
            else if (_pois.Count > 0)
            {
                var first = _pois[0];
                MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                    new Location(first.Latitude, first.Longitude), Distance.FromMeters(500)));
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

        private async void OnMapPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MyMap.VisibleRegion))
            {
                if (_isMapLoading) return;
                _isMapLoading = true;

                // Đợi 1 chút để người dùng ngừng lướt hẳn rồi mới vẽ Pin (tránh giật)
                await Task.Delay(300);
                LoadPoisOnMap();

                _isMapLoading = false;
            }
        }

        // ---------------- MAP ----------------
        private void LoadPoisOnMap()
        {
            if (_pois == null || _pois.Count == 0 || MyMap == null) return;

            var region = MyMap.VisibleRegion;
            if (region == null) return;

            var center = region.Center;

            if (_lastLat != 0 && Location.CalculateDistance(center, new Location(_lastLat, _lastLng), DistanceUnits.Kilometers) < 0.1)
                return;

            _lastLat = center.Latitude;
            _lastLng = center.Longitude;

            var visiblePois = _pois
                .Where(p => Location.CalculateDistance(center, new Location(p.Latitude, p.Longitude), DistanceUnits.Kilometers) < 2)
                .Take(30)
                .ToList();

            // Đã ở MainThread rồi, không cần BeginInvoke lồng nữa
            MyMap.Pins.Clear();
            _pinPoiMap.Clear();

            foreach (var poi in visiblePois)
            {
                var pin = new Pin
                {
                    Label = poi.Name,
                    Location = new Location(poi.Latitude, poi.Longitude),
                    Type = PinType.Place
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
            //UpdateTabTitle();

            SinglePoiView.IsVisible = true;
            //PoiListContainer.IsVisible = false;

            await PoiSheet.TranslateTo(0, _sheetHalfY, 200);
        }

        private async Task HandlePoiSelected(PointOfInterest poi)
        {
            if (poi == null) return;

            // Bước 1: Bay bản đồ đến vị trí POI
            MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                new Location(poi.Latitude, poi.Longitude),
                Distance.FromMeters(400)));

            // Bước 2: Gọi hàm UI bạn đã viết sẵn để hiện Bottom Sheet
            await ShowPoiWithTransition(poi);
        }

        async Task HideBottomSheet()
        {
            _sheetVisible = false;
            // Xóa đoạn lọc danh sách ở đây đi, vì mình đã xử lý nó ở ToggleFavorite rồi
            await PoiSheet.TranslateTo(0, _sheetHiddenY, 150);
        }

        private async void OnSheetPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            PoiSheet.AbortAnimation("TranslationY");

            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    // Hủy mọi animation đang chạy để không bị tranh chấp vị trí
                    PoiSheet.CancelAnimations();
                    _startY = PoiSheet.TranslationY;
                    break;

                case GestureStatus.Running:
                    // Cập nhật vị trí trực tiếp theo ngón tay
                    double targetY = _startY + e.TotalY;

                    // Giới hạn vùng vuốt để không vuốt quá cao hoặc quá thấp
                    PoiSheet.TranslationY = Math.Clamp(targetY, _sheetFullY, _sheetHiddenY);
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    // Trước khi Snap, phải Cancel để đảm bảo không còn lệnh nào can thiệp vào Y
                    PoiSheet.CancelAnimations();

                    double currentY = PoiSheet.TranslationY;

                    // 1. Nếu vuốt xuống quá thấp -> Ẩn luôn
                    double quarter = (_sheetFullY + _sheetHalfY) / 2;
                    double threeQuarter = (_sheetHalfY + _sheetHiddenY) / 2;

                    if (currentY < quarter)
                    {
                        await PoiSheet.TranslateTo(0, _sheetFullY, 200, Easing.CubicOut);
                    }
                    else if (currentY < threeQuarter)
                    {
                        await PoiSheet.TranslateTo(0, _sheetHalfY, 200, Easing.CubicOut);
                    }
                    else
                    {
                        await HideBottomSheet();
                    }
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
                AudioStatusLabel.Text = "PHÁT AUDIO     ";
                AudioIconBtn.Source = "play_icon.png";
            }
        }

        // ---------------- FAVORITES ----------------
        private async Task ToggleFavorite(PointOfInterest poi)
        {
            if (poi == null) return;

            poi.IsFavorite = !poi.IsFavorite;

            if (poi.IsFavorite)
            {
                if (!_favorites.Any(f => f.Id == poi.Id))
                    _favorites.Add(poi);
            }
            else
            {
                var toRemove = _favorites.FirstOrDefault(f => f.Id == poi.Id);
                if (toRemove != null)
                    _favorites.Remove(toRemove);
            }

            // DB chạy nền
            _ = Task.Run(() => _database.UpdateFavoriteStatusAsync(poi));

            UpdateSaveButtonUI(poi);
        }

        private async void OnFavoriteToggleClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is PointOfInterest poi)
                await ToggleFavorite(poi);
        }

        void UpdateSaveButtonUI(PointOfInterest poi)
        {
            if (poi == null) return;

            var icon = new FontImageSource
            {
                FontFamily = "MaterialIcons",
                Size = 20
            };

            if (poi.IsFavorite)
            {

                icon.Glyph = "❤️"; // bookmark filled
                icon.Color = Colors.DeepSkyBlue;
            }
            else
            {

                icon.Glyph = "🤍"; // bookmark outline   
                icon.Color = Colors.Gray;
            }

            SaveBtn.ImageSource = icon;
        }


        // ---------------- TABS MỚI ----------------

        private async void OnAllPoiTabClicked(object sender, EventArgs e)
        {
            var allPage = new AllPoiPage(_pois);
            // Sử dụng tên event mới đã đổi ở trên
            allPage.PoiSelectedEvent += async (poi) =>
            {
                await HandlePoiSelected(poi);
            };
            await Navigation.PushModalAsync(allPage);
        }

        private async void OnFavoriteTabClicked(object sender, EventArgs e)
        {
            var favPage = new FavoritePoiPage(_favorites);
            favPage.PoiSelectedEvent += async (poi) =>
            {
                await HandlePoiSelected(poi);
            };
            await Navigation.PushModalAsync(favPage);
        }

        //void UpdateTabTitle()
        //{
        //    //string title = _currentTab == 0 ? "Tất cả POI" : "Yêu thích";

        //    CurrentTabLabel.Text = title;          // cập nhật tên tab trên danh sách

        //}

        // ---------------- SEARCH ----------------
        private async void OnSearchTapped(object sender, EventArgs e)
        {
            // Truyền biến _database (đã có sẵn trong MainPage) vào SearchPage
            var searchPage = new SearchPage(_database);

            // Thiết lập hành động khi người dùng chọn một POI từ danh sách tìm kiếm
            searchPage.OnPoiSelected = async (poi) =>
            {
                // 1. Di chuyển bản đồ tới vị trí của điểm được chọn
                MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                    new Location(poi.Latitude, poi.Longitude),
                    Distance.FromMeters(400)));

                // 2. Hiển thị thông tin chi tiết (Bottom Sheet) của điểm đó
                await ShowPoiWithTransition(poi);
            };

            // Chuyển sang trang tìm kiếm
            await Navigation.PushAsync(searchPage);
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
        //private async void OnPoiItemSelected(object sender, SelectionChangedEventArgs e)
        //{
        //    if (e.CurrentSelection.FirstOrDefault() is PointOfInterest poi)
        //    {
        //        // 🔥 Move map tới POI
        //        MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(
        //            new Location(poi.Latitude, poi.Longitude),
        //            Distance.FromMeters(300)));

        //        // 🔥 Mở detail luôn (tuỳ bạn)
        //        await ShowPoiWithTransition(poi);
        //    }

        //    // ❗ reset selection để không bị highlight
        //    ((CollectionView)sender).SelectedItem = null;
        //}

        private async void OnSaveClicked(object sender, EventArgs e)
        {

            await ToggleFavorite(SelectedPoi);

        }
    }
}