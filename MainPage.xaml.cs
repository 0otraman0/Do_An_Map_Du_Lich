using MauiAppMain.Models;
using MauiAppMain.Services;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using System.Collections.ObjectModel;
using System.ComponentModel;



namespace MauiAppMain
{
    public partial class MainPage : ContentPage
    {
        //DATABASE & DATA
        private readonly DatabaseService _database;
        private ObservableCollection<PointOfInterest> _pois = new ObservableCollection<PointOfInterest>();
        private Dictionary<Pin, PointOfInterest> _pinPoiMap = new();
        private ObservableCollection<PointOfInterest> _favorites = new ObservableCollection<PointOfInterest>();
        private ObservableCollection<PointOfInterest> _displayedPois = new();




        //UI STATE
        bool _isPoiTransitionRunning = false;
        //bool _isSavingFavorite = false;
        private bool _mapInitialized = false;
        private bool _sheetVisible = false;
        private bool _isUiVisible = true;
        private int _currentTab = 0; // 0 = All, 1 = Favorites


        // BOTTOM SHEET
        private double _sheetFullY = 10;
        private double _sheetHalfY;
        private double _sheetHiddenY;
        private double _startY;
        //double _lastPanY;
        //DateTime _lastPanTime;

        // SELECTIONS
        private PointOfInterest? _selectedPoi;
        private PointOfInterest? _lastSpokenPoi;

        // CANCEL TOKENS
        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _ttsCts;

        // PROPERTY
        public PointOfInterest? SelectedPoi
        {
            get => _selectedPoi;
            set
            {
                if (_selectedPoi != null)
                    _selectedPoi.PropertyChanged -= OnPoiSelectionChanged;

                _selectedPoi = value;
                

                if (_selectedPoi != null)
                    _selectedPoi.PropertyChanged += OnPoiSelectionChanged;

                UpdateSaveButtonUI(_selectedPoi);
                OnPropertyChanged();
            }
        }


        // ---------------Constructor & Lifecycle-----------------------//
        public MainPage(DatabaseService database)
        {
            InitializeComponent();
            
            _database = database;
            BindingContext = this;

        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            _currentTab = 0;

            try
            {
                await SeedData(); // phải seed trước

                _displayedPois = new ObservableCollection<PointOfInterest>(_pois);
                PoiListView.ItemsSource = _displayedPois;
                // 👇 load DB nền
                _ = Task.Run(async () =>
                {
                    var list = await _database.GetPOIsAsync();

                    await Task.Delay(100);

                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        _pois.Clear();
                        foreach (var poi in list)
                            _pois.Add(poi);

                        _favorites.Clear();
                        foreach (var poi in _pois)
                            if (poi.IsFavorite)
                                _favorites.Add(poi);

                        // 🔥 PRELOAD LIST (quan trọng)
                        PoiListContainer.IsVisible = true;
                        PoiListContainer.Opacity = 0;

                        await Task.Delay(50);

                        PoiListContainer.Opacity = 1;
                        PoiListContainer.IsVisible = false;

                        if (_pois.Count > 0)
                        {
                            LoadPoisOnMap();

                            var firstPoi = _pois[0];
                            MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                                new Location(firstPoi.Latitude, firstPoi.Longitude),
                                Distance.FromMeters(500)));
                        }
                    });
                });

                StartTracking();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG LỖI: {ex.Message}");
            }
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


        // ---------------Database / Seed / Load POIs-----------------------//
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

        private async Task LoadPoisAsync()
        {
            var list = await _database.GetPOIsAsync();  // list từ DB

            _pois.Clear();                               // xóa dữ liệu cũ
            foreach (var poi in list)
                _pois.Add(poi);                          // thêm từng item vào ObservableCollection
        }


        // ---------------Map & Pin logic-----------------------//
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


        // ---------------Bottom Sheet / POI UI-----------------------//
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
                SinglePoiView.IsVisible = false;
                PoiListContainer.IsVisible = true;

                await PoiSheet.TranslateTo(0, _sheetFullY, 50, Easing.CubicOut);
            }
        }

        async Task ShowPoiWithTransition(PointOfInterest poi)
        {
            if (_isPoiTransitionRunning) return;

            _isPoiTransitionRunning = true;

            try
            {
                SelectedPoi = poi;

                UpdateSaveButtonUI(poi);

                UpdateTabTitle();

                PoiContent.CancelAnimations();

                if (!_sheetVisible)
                {
                    // Show sheet ở vị trí half ngay từ đầu
                    PoiSheet.TranslationY = _sheetHalfY;
                    PoiSheet.IsVisible = true;
                    _sheetVisible = true;
                }
                SinglePoiView.IsVisible = true;
                PoiListContainer.IsVisible = false;

                PoiContent.TranslationY = 10;

                await Task.WhenAll(
                    PoiSheet.TranslateTo(0, _sheetHalfY, 200, Easing.CubicOut),
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


        // ---------------Tabs / Selection / Favorite-----------------------//
        //hàm hiện tất cả poi
        private async void OnAllPoiTabClicked(object sender, EventArgs e)
        {
            _currentTab = 0;
            UpdateTabTitle();

            _displayedPois.Clear();
            foreach (var poi in _pois)
                _displayedPois.Add(poi);

            await ShowBottomSheet(true);
        }

        //địa điểm yêu thích 
        private async void OnFavoriteTabClicked(object sender, EventArgs e)
        {
            _currentTab = 1;
            UpdateTabTitle();

            _displayedPois.Clear();
            foreach (var poi in _favorites)
                _displayedPois.Add(poi);

            await ShowBottomSheet(true);
        }

        private void OnPoiSelectionChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PointOfInterest.IsFavorite))
            {
                UpdateSaveButtonUI(SelectedPoi); // cập nhật Single POI button
                //UpdateFavoritesTab();            // live update tab Favorites
            }
        }

        // hàm hiện poi yêu thích
        private async void OnFavoriteToggleClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is PointOfInterest poi)
            {
                await ToggleFavorite(poi);


                // Update button bottom sheet nếu đang mở
                UpdateSaveButtonUI(poi);
            }

            if (_currentTab == 1) // đang ở tab Favorites
            {
                _displayedPois.Clear();
                foreach (var item in _favorites)
                    _displayedPois.Add(item);
            }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {

            await ToggleFavorite(SelectedPoi);

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


        // ---------------GPS / Tracking-----------------------//
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
            catch (TaskCanceledException) { }
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


        // ---------------Utility / Helpers-----------------------//
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
        private async Task ToggleFavorite(PointOfInterest poi)
        {
            if (poi == null) return;

            var realPoi = _pois.FirstOrDefault(p => p.Id == poi.Id);
            if (realPoi == null) return;

            realPoi.IsFavorite = !realPoi.IsFavorite;
            await _database.UpdatePOIAsync(realPoi);

            if (realPoi.IsFavorite)
            {
                if (!_favorites.Any(f => f.Id == realPoi.Id))
                    _favorites.Add(realPoi);
            }
            else
            {
                var item = _favorites.FirstOrDefault(f => f.Id == realPoi.Id);
                if (item != null)
                    _favorites.Remove(item);
            }

            if (SelectedPoi != null && SelectedPoi.Id == realPoi.Id)
                UpdateSaveButtonUI(SelectedPoi);
        }

        //private void UpdateFavoritesTab()
        //{
        //    var favorites = _pois.Where(p => p.IsFavorite).ToList();

        //    // Xoá những POI không còn favorite
        //    for (int i = _favorites.Count - 1; i >= 0; i--)
        //    {
        //        if (!favorites.Any(f => f.Id == _favorites[i].Id))
        //            _favorites.RemoveAt(i);
        //    }

        //    // Thêm POI mới
        //    foreach (var poi in favorites)
        //    {
        //        if (!_favorites.Any(f => f.Id == poi.Id))
        //            _favorites.Add(poi);
        //    }
        //}

        // Hàm refresh danh sách tab bar
        void RefreshPoiListView()
        {
            PoiListView.ItemsSource = _currentTab == 0 ? _pois : _favorites;
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

        void UpdateTabTitle()
        {
            string title = _currentTab == 0 ? "Tất cả POI" : "Yêu thích";

            TabTitleLabel.Text = title;          // list view
            //SingleTabTitleLabel.Text = title;    // single view
        }


        // ---------------????-----------------------//
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


        private async void OnSheetPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:

                    _startY = PoiSheet.TranslationY;

                    break;

                case GestureStatus.Running:

                    double GetDragFactor()
                    {
                        return DeviceInfo.Idiom == DeviceIdiom.Desktop ? 1.8 : 1.1;
                    }

                    double targetY = _startY + e.TotalY * GetDragFactor();

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
  
    }
}