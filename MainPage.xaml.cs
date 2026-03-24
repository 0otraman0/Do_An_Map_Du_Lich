using MauiAppMain.Models;
using MauiAppMain.Services;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using MauiAppMain.Resources.Localization;

namespace MauiAppMain
{
    public partial class MainPage : ContentPage
    {
        private readonly DatabaseService _database;
        private readonly LanguageService _languageService;
        private readonly DataFetch _dataFetch;
        private List<PointOfInterest> _pois = new();
        private bool _mapInitialized = false;
        private bool _sheetVisible = false;
        private double SheetHiddenY = 500;
        private double SheetVisibleY = 0;
        private PointOfInterest? _selectedPoi;
        private PointOfInterest? _lastSpokenPoi;
        private CancellationTokenSource? _cts;

        double _startY;

        public PointOfInterest? SelectedPoi
        {
            get => _selectedPoi;
            set
            {
                _selectedPoi = value;
                OnPropertyChanged();
            }
        }

        public MainPage(DatabaseService database, DataFetch dataFetch)
        {
            InitializeComponent();
            _database = database;
            BindingContext = this;
            LanguageService.LoadSavedLanguage(); // Mặc định là tiếng Việt, bạn có thể thay đổi theo ý muốn
            _dataFetch = dataFetch;
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
            if (!LocationForegroundService.IsRunning)
            {
                var intent = new Android.Content.Intent(
                Android.App.Application.Context,
                typeof(LocationForegroundService));

                Android.App.Application.Context.StartForegroundService(intent);
            }
#endif
            try
            {
                //await _database.SeedData();
                _pois = await _database.GetPOIsAsync();

                if (_pois.Count > 0)
                {
                    LoadPoisOnMap();
                }
                await ZoomToUserAndFarthestPoi();
                // show map only after zoom is set
                MyMap.IsVisible = true;
                // set the zoom distance of map
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG LỖI: {ex.Message}");
            }
        }

        // ☰ HÀM XỬ LÝ MENU (Sửa lỗi "Not Found")
        public async void OnMenuClicked(object sender, EventArgs e)
        {
            try
            {
                // Chắc chắn rằng bạn đã tạo file SettingPage.xaml
                await Navigation.PushAsync(new SettingPage(_dataFetch, _database));
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", "Không thể mở trang cài đặt. Hãy đảm bảo SettingPage đã tồn tại.", "OK");
                Console.WriteLine(ex.Message);
            }
        }

        void LoadPoisOnMap()
        {
            MyMap.Pins.Clear();
            foreach (var poi in _pois)
            {
                var pin = new Pin
                {
                    Label = poi.Name,
                    Address = poi.Description,
                    Location = new Location(poi.Latitude, poi.Longitude)
                };

                pin.MarkerClicked += async (s, e) =>
                {
                    e.HideInfoWindow = true; // Ẩn info window mặc định
                    await ShowPoiWithTransition(poi);
                };

                MyMap.Pins.Add(pin);
            }
        }

        async Task ShowPoiWithTransition(PointOfInterest poi)
        {
            SelectedPoi = poi;

            if (!_sheetVisible)
            {
                await ShowBottomSheet();
                await Task.WhenAll(
                    PoiContent.FadeTo(1, 180, Easing.CubicOut),
                    PoiContent.TranslateTo(0, 0, 180, Easing.CubicOut)
                );
            }
            else
            {
                await PoiContent.FadeTo(0, 120);
                PoiContent.TranslationY = 10;
                await Task.Delay(30);
                await Task.WhenAll(
                    PoiContent.FadeTo(1, 180, Easing.CubicOut),
                    PoiContent.TranslateTo(0, 0, 180, Easing.CubicOut)
                );
            }

        }
        // --- BOTTOM SHEET ANIMATION ---
        async Task HideBottomSheet()
        {
            PoiSheet.CancelAnimations();
            _sheetVisible = false;
            // 150ms + Easing.Linear hoặc CubicIn sẽ làm bảng biến mất rất nhanh
            await PoiSheet.TranslateTo(0, SheetHiddenY, 150, Easing.CubicIn);
        }

        async Task ShowBottomSheet()
        {
            _sheetVisible = true;
            // Dùng SpringOut nếu muốn có độ nảy nhẹ, hoặc CubicOut để dừng lại mượt mà
            await PoiSheet.TranslateTo(0, SheetVisibleY, 200, Easing.CubicOut);
        }

        private async void OnSheetPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _startY = PoiSheet.TranslationY; // Chạm vào là ghi nhớ vị trí ngay
                    break;

                case GestureStatus.Running:
                    // Bảng phải chạy theo tay ngay lập tức, không có độ trễ
                    double targetY = _startY + e.TotalY;
                    PoiSheet.TranslationY = Math.Clamp(targetY, SheetVisibleY, SheetHiddenY);
                    break;

                case GestureStatus.Completed:
                    // TỐI ƯU NHẠY: 
                    // 1. Chỉ cần kéo xuống hơn 40-50px (thay vì 100px)
                    // 2. HOẶC nếu người dùng quẹt tay xuống (TotalY > 0)
                    if (e.TotalY > 40 || PoiSheet.TranslationY > SheetVisibleY + 100)
                    {
                        _ = HideBottomSheet();
                    }
                    else
                    {
                        _ = ShowBottomSheet();
                    }
                    break; ;
            }
        }
        private async void OnSearchTapped(object sender, EventArgs e)
        {
            Console.WriteLine("asdsdfSDFASDFSF_________+======");
            try
            {
                // Chắc chắn rằng bạn đã tạo file SettingPage.xaml
                await Navigation.PushAsync(new SearchPage());
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", "Không thể mở trang cài đặt. Hãy đảm bảo SearchPage đã tồn tại.", "OK");
                Console.WriteLine(ex.Message);
            }
        }
        async Task ZoomToUserAndFarthestPoi()
        {
            var location = await Geolocation.GetLocationAsync();

            if (location == null || _pois.Count == 0)
                return;

            var userLocation = new Location(location.Latitude, location.Longitude);

            double maxDistance = 0;

            foreach (var poi in _pois)
            {
                var poiLocation = new Location(poi.Latitude, poi.Longitude);

                double distance = Location.CalculateDistance(
                    userLocation,
                    poiLocation,
                    DistanceUnits.Kilometers);

                if (distance > maxDistance)
                    maxDistance = distance;
            }

            // add padding so markers are not at the edge
            double radius = maxDistance * 1.3;

            MyMap.MoveToRegion(
                MapSpan.FromCenterAndRadius(
                    userLocation,
                    Distance.FromKilometers(radius)
                )
            );
        }
    }
}