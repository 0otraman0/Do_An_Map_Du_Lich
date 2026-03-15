using MauiAppMain.Models;
using MauiAppMain.Services;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;

namespace MauiAppMain
{
    public partial class MainPage : ContentPage
    {
        private readonly DatabaseService _database;
        private List<PointOfInterest> _pois = new();
        private Dictionary<Pin, PointOfInterest> _pinPoiMap = new();
        bool _isPoiTransitionRunning = false;
        bool _isSavingFavorite = false;
        private bool _mapInitialized = false;
        private bool _sheetVisible = false;
        //private double SheetHiddenY = 500;
        //private double SheetVisibleY = 0;
        int _currentTab = 0; // 0 = All, 1 = Favorites
        double _sheetFullY = 10;
        double _sheetHalfY;
        double _sheetHiddenY;
        //double _lastPanY;
        //DateTime _lastPanTime;
        private PointOfInterest? _selectedPoi;
        private PointOfInterest? _lastSpokenPoi;
        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _ttsCts;

        private bool _isUiVisible = true;

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
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
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
            {
                await ShowPoiWithTransition(poi);
            }
        }
        //-------------------------------------//

        async Task ShowPoiWithTransition(PointOfInterest poi)
        {
            if (_isPoiTransitionRunning) return;

            _isPoiTransitionRunning = true;

            try
            {
                SelectedPoi = poi;

                SinglePoiView.IsVisible = true;
                PoiListView.IsVisible = false;

                UpdateSaveButtonUI(poi);

                PoiContent.CancelAnimations();

                if (!_sheetVisible)
                {
                    await ShowBottomSheet(false);
                }

                PoiContent.Opacity = 0;
                PoiContent.TranslationY = 10;

                await Task.WhenAll(
                    PoiContent.FadeTo(1, 180, Easing.CubicOut),
                    PoiContent.TranslateTo(0, 0, 180, Easing.CubicOut)
                );

                if (_lastSpokenPoi != poi)
                {
                    _lastSpokenPoi = poi;
                    _ = SpeakPoiDescription(poi);
                }
            }
            finally
            {
                _isPoiTransitionRunning = false;
            }
        }

        public async Task SpeakPoiDescription(PointOfInterest poi)
        {
            if (poi == null || string.IsNullOrWhiteSpace(poi.Description))
                return;

            try
            {
                // Hủy audio cũ
                _ttsCts?.Cancel();

                _ttsCts = new CancellationTokenSource();

                await TextToSpeech.Default.SpeakAsync(
                    poi.Description,
                    cancelToken: _ttsCts.Token);
            }
            catch (OperationCanceledException)
            {
                // bình thường khi audio bị hủy
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TTS Error: {ex.Message}");
            }
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
                    var location = await Geolocation.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)),
                _cts.Token);

                    if (location != null)
                    {
                        // Kiểm tra khoảng cách để tự động đọc audio khi đi ngang qua
                        CheckNearbyPoi(location);

                        if (!_mapInitialized)
                        {
                            MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(location, Distance.FromMeters(200)));
                            _mapInitialized = true;
                        }
                    }
                    await Task.Delay(4000, _cts.Token);
                }
            }
            catch(TaskCanceledException) { }
        }

        void CheckNearbyPoi(Location location)
        {
            foreach (var poi in _pois)
            {
                double distance = location.CalculateDistance(
                    new Location(poi.Latitude, poi.Longitude),
                    DistanceUnits.Kilometers) * 1000;

                if (distance < 50 && _lastSpokenPoi != poi)
                {
                    _lastSpokenPoi = poi;
                    _ = SpeakPoiDescription(poi);
                    break;
                }
            }
        }

        // --- BOTTOM SHEET ANIMATION ---
        async Task HideBottomSheet()
        {
            PoiSheet.CancelAnimations();
            _sheetVisible = false;
            // 150ms + Easing.Linear hoặc CubicIn sẽ làm bảng biến mất rất nhanh
            await PoiSheet.TranslateTo(0, _sheetHiddenY, 150, Easing.CubicIn);
        }

        async Task ShowBottomSheet(bool showList, IEnumerable<PointOfInterest>? pois = null)
        {
            PoiSheet.CancelAnimations();

            _sheetVisible = true;

            if (showList)
            {
                PoiListView.ItemsSource = pois?.ToList();

                SinglePoiView.IsVisible = false;
                PoiListView.IsVisible = true;

                await PoiSheet.TranslateTo(0, _sheetFullY, 200, Easing.CubicOut);
            }
            else
            {
                SinglePoiView.IsVisible = true;
                PoiListView.IsVisible = false;

                await PoiSheet.TranslateTo(0, _sheetHalfY, 200, Easing.CubicOut);
            }
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

                    Console.WriteLine("Y: " + PoiSheet.TranslationY);   

                    PoiSheet.TranslationY = Math.Clamp(
                        targetY,
                        _sheetFullY,   // giới hạn trên
                        _sheetHiddenY  // giới hạn dưới
                    );


                    break;

                case GestureStatus.Completed:
                    double currentY = PoiSheet.TranslationY;

                    // Nếu vị trí hiện tại thấp hơn nấc Half khoảng 50px -> Ẩn luôn
                    if (currentY > (_sheetHalfY + 50))
                    {
                        await HideBottomSheet();
                    }
                    // Nếu gần nấc Full (giữa Full và Half) -> Snap lên Full
                    else if (currentY < (_sheetFullY + _sheetHalfY) / 2)
                    {
                        await PoiSheet.TranslateTo(0, _sheetFullY, 200, Easing.CubicOut);
                    }
                    // Còn lại -> Snap về Half
                    else
                    {
                        await PoiSheet.TranslateTo(0, _sheetHalfY, 200, Easing.CubicOut);
                    }
                    break;
            }
        }

        //lưu dữ liệu pin trên bản đồ
        async Task SeedData()
        {
            await _database.Init();
            var existing = await _database.GetPOIsAsync();
            if (existing.Count > 0) return;

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

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            if (height <= 0) return;

            // Thay vì ẩn mất tiêu xuống đáy, ta trừ đi 70 (chiều cao của Tab Bar)
            // để khi ẩn, PoiSheet nằm ngay sát trên đầu Tab Bar hoặc lặn hẳn dưới nó
            _sheetHiddenY = height;
            _sheetHalfY = height - 320;
            //_sheetFullY = 50;

            if (!_sheetVisible)
            {
                PoiSheet.TranslationY = _sheetHiddenY;
            }
        }

        //logic một chạm ẩn nhanh drag handle
        private async void OnMapClicked(object sender, MapClickedEventArgs e)
        {
            // 1. Nếu Bottom Sheet đang hiện, thì ẩn nó đi (Logic cũ của bạn) 
            if (_sheetVisible)
            {
                await HideBottomSheet();
            }

            // 2. Đồng thời đảo ngược trạng thái ẩn/hiện của các thanh menu
            // Ấn vào map: Nếu đang hiện thì ẩn, nếu đang ẩn thì hiện lại
            //await SetUIVisibility(!_isUiVisible);
        }
        //địa điểm yêu thích 
        private async void OnFavoriteTabClicked(object sender, EventArgs e)
        {
            _currentTab = 1;

            var favorites = _pois.Where(p => p.IsFavorite).ToList();
            await ShowBottomSheet(true, favorites);
        }

        private async void OnAllPoiTabClicked(object sender, EventArgs e)
        {
            _currentTab = 0;
            await ShowBottomSheet(true, _pois);
        }


        private async void OnFavoriteToggleClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is PointOfInterest poi)
            {
                await ToggleFavorite(poi);           

                // Update button bottom sheet nếu đang mở
                UpdateSaveButtonUI(poi);
            }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {

            await ToggleFavorite(SelectedPoi);

        }

        private async void OnPoiSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is PointOfInterest selected)
            {
                PoiListView.SelectedItem = null;

                SelectedPoi = selected;

                UpdateSaveButtonUI(selected);

                await ShowPoiWithTransition(selected);
            }
        }

        private async Task SetUIVisibility(bool isVisible)
        {
            if (_isUiVisible == isVisible) return;
            _isUiVisible = isVisible;

            double opacity = isVisible ? 1 : 0;
            uint duration = 250; // Tốc độ ẩn hiện (ms)

            // Danh sách các thành phần bạn muốn ẩn/hiện
            // Sau này muốn thêm cái nào, bạn chỉ cần bỏ tên nó vào đây
            var elementsToToggle = new List<VisualElement>
    {
        MainTabBar,    // Thanh Tab Bar mới thêm
        // SearchBarFrame, // Ví dụ: Thanh tìm kiếm phía trên (nếu bạn đặt tên cho nó)
    };

            var tasks = new List<Task>();

            foreach (var element in elementsToToggle)
            {
                if (element != null)
                {
                    tasks.Add(element.FadeTo(opacity, duration));

                    // Tùy chọn: Chặn tương tác khi đã ẩn
                    element.InputTransparent = !isVisible;
                }
            }

            await Task.WhenAll(tasks);
        }

        void UpdateSaveButtonUI(PointOfInterest poi)
        {
            if (poi.IsFavorite)
            {
                SaveBtn.Text = "Saved";
                SaveBtn.BackgroundColor = Color.FromArgb("#E3F2FD");
                SaveBtn.TextColor = Colors.DeepSkyBlue;
            }
            else
            {
                SaveBtn.Text = "Save";
                SaveBtn.BackgroundColor = Color.FromArgb("#F0F0F0");
                SaveBtn.TextColor = Colors.Black;
            }
        }

        //bật/tắt trạng thái Save (Favorite) của một POI, lưu vào database, và cập nhật lại UI
        async Task ToggleFavorite(PointOfInterest poi)
        {
            if (poi == null)
                return;

            poi.IsFavorite = !poi.IsFavorite;

            await _database.UpdatePOIAsync(poi);

            if (SelectedPoi == poi)
                UpdateSaveButtonUI(poi);

            // Refresh list để UI đồng bộ
            if (PoiListView.IsVisible)
            {
                PoiListView.ItemsSource = null;

                if (_currentTab == 0)
                    PoiListView.ItemsSource = _pois;
                else
                    PoiListView.ItemsSource = _pois.Where(p => p.IsFavorite).ToList();
            }
        }

    }
}