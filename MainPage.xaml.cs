using MauiAppMain.Models;
using MauiAppMain.Services;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using System.Globalization;
using MauiAppMain.Resources.Localization;

namespace MauiAppMain
{
    public partial class MainPage : ContentPage
    {
        private readonly DatabaseService _database;
        private readonly LanguageService _languageService;
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

        public MainPage(DatabaseService database)
        {
            InitializeComponent();
            _database = database;
            BindingContext = this;
            LanguageService.LoadSavedLanguage(); // Mặc định là tiếng Việt, bạn có thể thay đổi theo ý muốn
        }
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            SearchEntry.Text = AppResource.Search_placeholder;
            try
            {
                await SeedData();
                _pois = await _database.GetPOIsAsync();

                if (_pois.Count > 0)
                {
                    LoadPoisOnMap();

                    // Di chuyển tới điểm đầu tiên khi khởi động
                    var firstPoi = _pois[0];
                    MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                        new Location(firstPoi.Latitude, firstPoi.Longitude),
                        Distance.FromMeters(500)));
                }

                StartTracking();
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
                await Navigation.PushAsync(new SettingPage());
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

            // Phát âm thanh mô tả
            _ = SpeakPoiDescription(poi);
        }

        public async Task SpeakPoiDescription(PointOfInterest poi)
        {
            if (poi == null || string.IsNullOrWhiteSpace(poi.Description)) return;

            // Ép chạy trên luồng giao diện (Main Thread)
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    // Thử phát âm câu đơn giản nhất, không dùng Options phức tạp
                    await TextToSpeech.Default.SpeakAsync(poi.Description);
                }
                catch (Exception ex)
                {
                    // Nếu có lỗi, nó sẽ hiện thông báo lên màn hình cho bạn biết
                    await Shell.Current.DisplayAlert("Lỗi TTS", ex.Message, "OK");
                }
            });
        }

        // --- GPS TRACKING LOGIC ---
        async void StartTracking()
        {
            if (await Permissions.RequestAsync<Permissions.LocationWhenInUse>() != PermissionStatus.Granted)
                return;

            _cts = new CancellationTokenSource();
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(4));
                    var location = await Geolocation.GetLocationAsync(request, _cts.Token);

                    if (location != null)
                    {
                        // Kiểm tra khoảng cách để tự động đọc audio khi đi ngang qua
                        foreach (var poi in _pois)
                        {
                            // Sử dụng hàm CalculateDistance có sẵn của đối tượng Location
                            double distance = location.CalculateDistance(new Location(poi.Latitude, poi.Longitude), DistanceUnits.Kilometers);

                            // Vì kết quả trả về là Kilometers, ta nhân với 1000 để ra Meters
                            double distanceInMeters = distance * 1000;

                            if (distanceInMeters < 50 && _lastSpokenPoi != poi)
                            {
                                _lastSpokenPoi = poi;
                                _ = SpeakPoiDescription(poi);
                                break;
                            }
                        }

                        if (!_mapInitialized)
                        {
                            MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(location, Distance.FromMeters(200)));
                            _mapInitialized = true;
                        }
                    }
                    await Task.Delay(4000, _cts.Token);
                }
            }
            catch { }
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

        //lưu dữ liệu pin trên bản đồ
        async Task SeedData()
        {
            // kiểm tra dữ liệu POI đã tồn tại chưa, nếu chưa thì thêm vào
            await _database.Init();
            var existing = await _database.GetPOIsAsync();
            if (existing.Count == 0)
            {
                var imageList1 = new List<string> { "school_1.jpg", "school_2.jpg", "school_3.jpg" };
                var imageList2 = new List<string> { "cafe_1.jpg", "cafe_2.jpg" };

                var initialPois = new List<PointOfInterest>
                {
                new PointOfInterest
                {
                    Name = "Trường học",
                    Description = "Trường học là nơi tôi được học.",
                    Latitude = 10.759893,
                    Longitude = 106.679930,
                    ImageUrlsJson = System.Text.Json.JsonSerializer.Serialize(imageList1)
                },
                    new PointOfInterest
                    {
                    Name = "Quán Cà Phê",
                    Description = "Cà phê ngon nhất ở đây.",
                    Latitude = 10.759548,
                    Longitude = 106.679105,
                    ImageUrlsJson = System.Text.Json.JsonSerializer.Serialize(imageList2)
                    }
                };
                foreach (var poi in initialPois) await _database.AddPOIAsync(poi);
            }
            // kiểm tra dữ liệu ngôn ngữ đã tồn tại chưa, nếu chưa thì thêm vào
            var existing_lang = await _database.GetLanguagesAsync();
            if (existing_lang.Count == 0)
            {
                var initialLanguage = new List<Language_option>
                {
                    new Language_option
                    {
                        Code = "en",
                        Language = "English"
                    },
                    new Language_option
                    {
                        Code = "vi",
                        Language = "Tiếng Việt"
                    },
                    new Language_option
                    {
                        Code = "ja",
                        Language = "日本語"
                    }
                };
                foreach (var lang in initialLanguage) await _database.AddLanguageAsync(lang);
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
        
    }
}