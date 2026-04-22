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
            // ALL AND FAVORITE


            // Hai biến để bật tắt nhanh Tab
            private bool _isAllTabVisible = true;
            public bool IsAllTabVisible { get => _isAllTabVisible; set { _isAllTabVisible = value; OnPropertyChanged(); } }
            private bool _isFavoriteTabVisible = false;
            public bool IsFavoriteTabVisible { get => _isFavoriteTabVisible; set { _isFavoriteTabVisible = value; OnPropertyChanged(); } }

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
            private Pin _lastHighlightedPin; // Lưu lại để xóa nhãn điểm cũ

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

                // Gán ItemsSource một lần duy nhất tại Constructor
                AllPoiListView.ItemsSource = _pois;
                FavoritePoiListView.ItemsSource = _favorites;

                LanguageService.LoadSavedLanguage();

                // ĐĂNG KÝ SỰ KIỆN CẬP NHẬT DỮ LIỆU
                _dataFetch.OnDataUpdated += () => {
                    MainThread.BeginInvokeOnMainThread(async () => {
                        await RefreshDataFromDb();
                    });
                };

    #if ANDROID
                AndroidTtsService.OnSpeechCompleted = () =>
                {
                    StopAudio();
                };
    #endif
            }

            private async Task RefreshDataFromDb()
            {
                try 
                {
                    var list = await _database.GetPOIsAsync();
                    
                    // Cập nhật danh sách hiển thị
                    _pois.Clear();
                    _favorites.Clear();

                    if (list != null)
                    {
                        foreach (var poi in list)
                        {
                            _pois.Add(poi); 
                            if (poi.IsFavorite) _favorites.Add(poi); 
                        }
                    }

                    // Cập nhật Bản đồ
                    if (MyMap != null)
                    {
                        LoadPoisOnMap();
                    }

                    // Nếu đang xem chi tiết một POI, hãy cập nhật lại dữ liệu cho nó (trường hợp bị xóa hoặc đổi tên)
                    if (SelectedPoi != null)
                    {
                        var updated = _pois.FirstOrDefault(p => p.Id == SelectedPoi.Id);
                        if (updated == null)
                        {
                            // POI đã bị xóa
                            await HideBottomSheet();
                        }
                        else
                        {
                            SelectedPoi = updated;
                        }
                    }
                    
                    FilterDisplayedPois(); // Cập nhật lại UI List
                }
                catch (Exception ex)
                {
                    Console.WriteLine("REFRESH DATA ERROR: " + ex.Message);
                }
            }

            bool _isLoaded = false;


            private void FilterDisplayedPois()
            {
                // Xác định danh sách nguồn dựa trên Tab hiện tại
                var targetSource = _currentTab == 0 ? _pois : _favorites;

                // Chỉ xóa và thêm phần tử vào Collection hiện tại để UI không bị nạp lại toàn bộ
                _displayedPois.Clear();
                foreach (var poi in targetSource)
                {
                    _displayedPois.Add(poi);
                }
            }

            protected override async void OnAppearing()
            {
                base.OnAppearing();

                if (_isLoaded)
                {
                    // Nếu quay lại từ trang cài đặt (hoặc nơi khác), cập nhật lại danh sách POI từ SQLite
                    await RefreshDataFromDb();
                    return;
                }
                
                _isLoaded = true;

                try 
                {
                    // 0. BẬT BẢN ĐỒ NGAY LẬP TỨC
                    //if (MyMap != null) MyMap.IsVisible = false;

                    // 1. KÍCH HOẠT ĐA LUỒNG: Vừa xin quyền GPS, vừa chạy chìm lấy Gói Data
                    // Note: Android 15 is strict about LocationAlways. Requesting WhenInUse first is safer.
                    // 1. Xin quyền GPS trước
                    var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

                    // 2. Load DB
                    //var list = await Task.Run(async () => await _database.GetPOIsAsync());

                    // 3. Xử lý UI và Bật bản đồ SAU KHI ĐÃ CÓ QUYỀN
                    if (status == PermissionStatus.Granted)
                    {
                        MyMap.IsShowingUser = true; // Giờ mới cho phép Map đọc GPS
                    }

                    MyMap.IsVisible = true; // Giờ bật Map lên mới an toàn
                    var permissionTask = Permissions.RequestAsync<Permissions.LocationWhenInUse>();

                    var dbTask = Task.Run(async () => await _database.GetPOIsAsync());

                    // 2. Chờ cả 2 nhiệm vụ hoàn tất song song
                    await Task.WhenAll(permissionTask, dbTask);
                    
                    // 3. Load dữ liệu lần đầu
                    await RefreshDataFromDb();

                    // 4. Di chuyển bản đồ đến điểm đầu tiên
                    if (_pois.Count > 0 && MyMap != null)
                    {
                        var firstPoi = _pois[0];
                        MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                            new Location(firstPoi.Latitude, firstPoi.Longitude),
                            Distance.FromMeters(500)));
                    }

                    // --- HACK WARM UP NATIVE VIEWS ---
                    if (PoiListContainer != null) PoiListContainer.IsVisible = true;
                    if (AllPoiListView != null) AllPoiListView.IsVisible = true;
                    if (FavoritePoiListView != null) FavoritePoiListView.IsVisible = true;

                    await Task.Delay(100);

                    if (PoiListContainer != null) PoiListContainer.IsVisible = false;
                    if (AllPoiListView != null) AllPoiListView.IsVisible = false;
                    if (FavoritePoiListView != null) FavoritePoiListView.IsVisible = false;

                    // BÂY GIỜ MỚI MỞ KHÓA TƯƠNG TÁC
                    if (TopOverlay != null) TopOverlay.IsEnabled = true;
                    if (MainTabBar != null) MainTabBar.IsEnabled = true;

                    // VÀ KẾT THÚC VAI TRÒ CỦA ÁP PHÍCH LÀM MỜ
                    if (LoadingOverlay != null)
                    {
                        await LoadingOverlay.FadeTo(0, 250, Easing.Linear);
                        LoadingOverlay.IsVisible = false;
                    }

    #if ANDROID
                    // Khởi chạy service ngầm (Chỉ 1 lần)
                    // Note: Delay to ensure app is fully in foreground for Android 15
                    if (!LocationForegroundService.IsRunning)
                    {
                        Task.Run(async () => {
                            await Task.Delay(2000);
                            var intent = new Android.Content.Intent(Android.App.Application.Context, typeof(LocationForegroundService));
                            Android.App.Application.Context.StartForegroundService(intent);
                        });
                    }
    #endif
                }
                catch (Exception ex)
                {
                    Console.WriteLine("CRITICAL STARTUP ERROR: " + ex.ToString());
                    // Ensure UI is usable even if background tasks fail
                    if (LoadingOverlay != null) LoadingOverlay.IsVisible = false;
                    if (TopOverlay != null) TopOverlay.IsEnabled = true;
                    if (MainTabBar != null) MainTabBar.IsEnabled = true;
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
                // Find pins that are no longer in _pois and remove them
                var pinsToRemove = _pinPoiMap.Where(kvp => !_pois.Any(p => p.Id == kvp.Value.Id)).ToList();
                foreach (var kvp in pinsToRemove)
                {
                    MyMap.Pins.Remove(kvp.Key);
                    _pinPoiMap.Remove(kvp.Key);
                }

                // Find POIs that don't have a pin yet and add them
                var existingPoiIds = _pinPoiMap.Values.Select(p => p.Id).ToList();
                foreach (var poi in _pois) 
                {
                    if (!existingPoiIds.Contains(poi.Id))
                    {
                        var pin = new Pin
                        {
                            Label = poi.Name,
                            Address = poi.Address, 
                            Location = new Location(poi.Latitude, poi.Longitude)
                        };
                        pin.MarkerClicked += OnPinClicked;
                        MyMap.Pins.Add(pin);
                        _pinPoiMap[pin] = poi;
                    }
                }
            }

            async void OnPinClicked(object sender, PinClickedEventArgs e)
            {
                e.HideInfoWindow = true;

                if (sender is Pin pin && _pinPoiMap.TryGetValue(pin, out var poi))
                    await FocusOnPoi(poi);
            }


        private async Task FocusOnPoi(PointOfInterest poi)
        {
            if (poi == null) return;

            SelectedPoi = poi;

            // Hiển thị Bottom Sheet TRƯỚC để người dùng thấy phản hồi ngay lập tức
            await ShowPoiWithTransition(poi);

            // Sau đó mới xử lý Map (nếu Map lỗi cũng không làm mất Detail)
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    double latitudeOffset = 0.002;
                    var cameraLocation = new Location(poi.Latitude - latitudeOffset, poi.Longitude);
                    MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(cameraLocation, Distance.FromMeters(300)));
                }
                catch (Exception ex) { /* Log lỗi map nhưng ko chặn UI */ }
            });
        }


        // ---------------- BOTTOM SHEET ----------------
        private async Task ShowPoiWithTransition(PointOfInterest poi)
        {
            if (poi == null) return;

            PoiSheet.CancelAnimations();

            // Đảm bảo UI cập nhật dữ liệu mới nhất
            SelectedPoi = poi;

            // Reset view hiển thị: Hiện Detail, ẩn List
            SinglePoiView.IsVisible = true;
            PoiListContainer.IsVisible = false;

            // QUAN TRỌNG: Nếu Sheet đang đóng, ta bắt đầu từ dưới đất đi lên
            if (!_sheetVisible)
            {
                PoiSheet.TranslationY = _sheetHiddenY;
                _sheetVisible = true;
            }
            else
            {
                // Nếu Sheet ĐANG MỞ (người dùng bấm từ POI này sang POI khác)
                // Ta nên tạo một hiệu ứng "nảy" nhẹ hoặc đơn giản là ép nó chạy lại TranslateTo
                // để người dùng biết là dữ liệu đã đổi.
                await PoiSheet.TranslateTo(0, _sheetHalfY + 20, 50, Easing.Linear); // Nhích xuống 1 tí
            }

            UpdateTabTitle();
            UpdateSaveButtonUI(poi);

            // Hiệu ứng trượt lên vị trí chuẩn (Half)
            await PoiSheet.TranslateTo(0, _sheetHalfY, 250, Easing.CubicOut);
        }

        async Task HideBottomSheet()
            {
                PoiSheet.CancelAnimations();
                _sheetVisible = false;

                // QUAN TRỌNG: Trước khi đóng, lọc lại danh sách _favorites 
                // để loại bỏ những điểm người dùng đã bấm "bỏ thích" (màu xám)
                var itemsToRemove = _favorites.Where(f => !f.IsFavorite).ToList();
                foreach (var item in itemsToRemove)
                {
                    _favorites.Remove(item);
                }

                await PoiSheet.TranslateTo(0, _sheetHiddenY, 200, Easing.CubicOut);
            }

            private async void OnSheetPanUpdated(object sender, PanUpdatedEventArgs e)
            {
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
                        double dragDelta = currentY - _startY; // Lấy gia tốc hướng kéo

                        // Độ nhạy cao: kéo tay khoảng 40px là nhận định hướng muốn vuốt xuống hạy lên
                        double swipeThreshold = 40;

                        // 1. Nếu vuốt nhẹ xuống
                        if (dragDelta > swipeThreshold)
                        {
                            // Đang ở một nửa (hoặc thấp hơn) -> Ẩn luôn
                            if (_startY >= _sheetHalfY - 20)
                            {
                                await HideBottomSheet();
                            }
                            // Đang mở Full trên cao -> Trở về dạng một nửa
                            else
                            {
                                await PoiSheet.TranslateTo(0, _sheetHalfY, 250, Easing.CubicOut);
                            }
                        }
                        // 2. Nếu vuốt nhẹ lên trên
                        else if (dragDelta < -swipeThreshold)
                        {
                            await PoiSheet.TranslateTo(0, _sheetFullY, 250, Easing.CubicOut);
                        }
                        // 3. Fallback: Nếu tay chỉ chạm nhẹ và thả ra ngay (chưa qua được threshold)
                        else
                        {
                            if (currentY > _sheetHalfY + swipeThreshold)
                            {
                                await HideBottomSheet();
                            }
                            else if (currentY > (_sheetFullY + _sheetHalfY) / 2)
                            {
                                await PoiSheet.TranslateTo(0, _sheetHalfY, 250, Easing.CubicOut);
                            }
                            else
                            {
                                await PoiSheet.TranslateTo(0, _sheetFullY, 250, Easing.CubicOut);
                            }
                        }
                        break;
                }
            }



            // ---------------- AUDIO ----------------
            private Task PlayAudio()
            {
                // Kiểm tra SelectedPoi VÀ Description có null không
                if (SelectedPoi == null || string.IsNullOrEmpty(SelectedPoi.Description))
                {
                    return Task.CompletedTask;
                }

                _isPlaying = true;
                UpdateAudioUI(true);
                _audioSessionId++;

    #if ANDROID
                AndroidTtsService.Speak(SelectedPoi.Description);
    #endif
                
                _ = PoiTrafficLogger.LogPoiPlayAsync(SelectedPoi);
                return Task.CompletedTask;
            }

            private void StopAudio()
            {
                _isPlaying = false;
    #if ANDROID
                AndroidTtsService.Stop();
    #endif

                // Cập nhật lại UI trên luồng chính
                MainThread.BeginInvokeOnMainThread(() => {
                    UpdateAudioUI(false);
                });
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

                // 1. Tìm đối tượng gốc trong danh sách chính để đảm bảo đồng bộ UI hoàn toàn
                var mainPoi = _pois.FirstOrDefault(p => p.Id == poi.Id);
                if (mainPoi != null)
                {
                    mainPoi.IsFavorite = !mainPoi.IsFavorite;
                    // Nếu đối tượng truyền vào khác đối tượng gốc thì cập nhật cả hai
                    if (mainPoi != poi) poi.IsFavorite = mainPoi.IsFavorite;
                }
                else
                {
                    poi.IsFavorite = !poi.IsFavorite;
                }

                // 2. Cập nhật danh sách Favorite trên RAM
                if (poi.IsFavorite)
                {
                    if (!_favorites.Any(f => f.Id == poi.Id))
                        _favorites.Insert(0, poi);
                }

                // 3. Cập nhật UI nút bấm
                if (SelectedPoi != null && SelectedPoi.Id == poi.Id)
                    UpdateSaveButtonUI(poi);

                // 4. LƯU QUAN TRỌNG: Hãy await nó hoặc đảm bảo nó chạy xong
                // Thử bỏ Task.Run để kiểm tra xem có phải do luồng không
                await _database.UpdateFavoriteStatusAsync(poi);

                System.Diagnostics.Debug.WriteLine($"==> Đã yêu cầu lưu Id: {poi.Id} - Status: {poi.IsFavorite}");
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


            // ---------------- TABS ----------------
            private async void OnAllPoiTabClicked(object sender, EventArgs e)
            {
                _currentTab = 0;
                _sheetVisible = true;
                UpdateTabTitle();

                SinglePoiView.IsVisible = false;
                PoiListContainer.IsVisible = true;

                // Xử lý bật/tắt hiển thị danh sách NGAY LẬP TỨC để tránh dính list cũ (bug)
                AllPoiListView.IsVisible = true;
                FavoritePoiListView.IsVisible = false;

                await PoiSheet.TranslateTo(0, _sheetFullY, 180, Easing.CubicOut);
            
            }

            private async void OnFavoriteTabClicked(object sender, EventArgs e)
            {
                _currentTab = 1;
                _sheetVisible = true;
                UpdateTabTitle();

                SinglePoiView.IsVisible = false;
                PoiListContainer.IsVisible = true;

                // Xử lý bật/tắt hiển thị danh sách NGAY LẬP TỨC
                AllPoiListView.IsVisible = false;
                FavoritePoiListView.IsVisible = true;

                await PoiSheet.TranslateTo(0, _sheetFullY, 180, Easing.CubicOut);
            }

            void UpdateTabTitle()
            {
                string title = _currentTab == 0 ? "Tất cả POI" : "Yêu thích";

                CurrentTabLabel.Text = title;          // cập nhật tên tab trên danh sách

            }

            // ---------------- SEARCH ----------------
            private async void OnSearchTapped(object sender, EventArgs e)
            {
                // Truyền biến _database (đã có sẵn trong MainPage) vào SearchPage
                var searchPage = new SearchPage(_database);

                // Thiết lập hành động khi người dùng chọn một POI từ danh sách tìm kiếm
                searchPage.OnPoiSelected = async (poi) =>
                {
                    // 1. Di chuyển bản đồ tới vị trí của điểm được chọn
                    //MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                    //    new Location(poi.Latitude, poi.Longitude),
                    //    Distance.FromMeters(400)));

                    // 2. Hiển thị thông tin chi tiết (Bottom Sheet) của điểm đó
                    //await ShowPoiWithTransition(poi);

                    await FocusOnPoi(poi);
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
        private void OnPoiItemSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is PointOfInterest selectedPoi)
            {
                FocusOnPoi(selectedPoi);

                // Dùng Dispatcher để reset Selection sau khi frame hình đã render
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ((CollectionView)sender).SelectedItem = null;
                });
            }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
            {

                await ToggleFavorite(SelectedPoi);

            }
        }
    }