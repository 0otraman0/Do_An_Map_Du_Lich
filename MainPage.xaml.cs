using MauiAppMain.Models;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Media;

namespace MauiAppMain
{
    public partial class MainPage : ContentPage
    {
        // ===== POI CONFIG =====
        // temporary hardcoded POIs, in real app this would come from a database or API

        List<PointOfInterest> _pois = new()
{
    new PointOfInterest
    {
        Name = "School",
        Description = "Trường học nơi tôi đã học",
        Latitude = 10.759893,
        Longitude = 106.679930,
        RadiusMeters = 10
    },
    new PointOfInterest
    {
        Name = "Coffee Shop",
        Description = "Cửa hàng cà phê nơi tôi thường đến",
        Latitude = 10.759548,
        Longitude = 106.679105,
        RadiusMeters = 10
    },
    new PointOfInterest
    {
        Name = "Library",
        Description = "Thư viện nơi tôi thường học bài",
        Latitude = 10.759328,
        Longitude = 106.678476,
        RadiusMeters = 10
    },
    new PointOfInterest
    {
        Name = "Góc nhà",
        Latitude = 10.758797,
        Longitude = 106.677039,
        RadiusMeters = 10
    },
    new PointOfInterest
    {
        Name = "Chuồng gà",
        Description = "where I keep my chickens fat and thick until it grows big enough. That's when I cook them",
        Latitude = 10.758453,
        Longitude = 106.677928,
        RadiusMeters = 10
    }
};
        public MainPage()
        {
            InitializeComponent();
            BindingContext = this;
            LoadPoisOnMap();
            StartTracking();
        }

        bool _mapInitialized = false;

        private PointOfInterest? _selectedPoi;

        public PointOfInterest? SelectedPoi
        {
            get => _selectedPoi;
            set
            {
                _selectedPoi = value;
                OnPropertyChanged();
            }
        }
        public bool _sheetVisible = false;
        private double SheetHiddenY = 300;
        private double SheetVisibleY = 0;

        // Cancellation token để dừng tracking khi không cần thiết (ví dụ khi thoát app)

        CancellationTokenSource? _cts;

        // Hàm đảm bảo quyền thông báo (chỉ cần thiết trên Android 13+)

        async Task EnsureNotificationPermissionAsync()
        {
#if ANDROID
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
                if (status != PermissionStatus.Granted)
                    await Permissions.RequestAsync<Permissions.PostNotifications>();
            }
#endif
        }

        // Hàm đảm bảo quyền lấy vị trí

        async Task<bool> EnsureLocationPermissionAsync()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            return status == PermissionStatus.Granted;
        }

        // Hàm dừng tracking khi không cần thiết (ví dụ khi thoát app)

        void StopTracking()
        {
            _cts?.Cancel();
        }

        // Hàm bắt đầu tracking vị trí của thiết bị và kiểm tra khoảng cách đến các POI
        private PointOfInterest _lastSpokenPoi = null!;

        async void StartTracking()
        {
            //kiểm tra quyền lấy vị trí 

            if (!await EnsureLocationPermissionAsync())
                return;

            _cts = new CancellationTokenSource();

            //get position in background loop

            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    var request = new GeolocationRequest(
                        GeolocationAccuracy.High,
                        TimeSpan.FromSeconds(4));

                    var location = await Geolocation.GetLocationAsync(request, _cts.Token);

                    if (location == null)
                        continue;


                    // ---- kiểm tra khoảng cách tới các điểm poi {not in use right now since there is no live voice in yet} ----

                    double nearestDistance = double.MaxValue;
                    PointOfInterest? nearestPoi = null;

                    foreach (var poi in _pois)
                    {
                        var distance = DistanceInMeters(
                            location.Latitude,
                            location.Longitude,
                            poi.Latitude,
                            poi.Longitude);

                        if (distance < nearestDistance)
                        {
                            nearestDistance = distance;
                            nearestPoi = poi;
                        }
                    }

                    Console.WriteLine(nearestPoi?.Name + " is " + nearestDistance + " meters away");

                    // Trigger when within 5) meters
                    if (nearestPoi != null && nearestDistance < 50)
                        {
                            if (_lastSpokenPoi != nearestPoi)
                            {
                                _lastSpokenPoi = nearestPoi;
                                await SpeakPoiDescription(nearestPoi);
                            }
                        }
                    
                    //pin map tại vị trí của thiết bị khi startTracking lần đầu

                    if (!_mapInitialized)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            MyMap.MoveToRegion(
                                MapSpan.FromCenterAndRadius(
                                    new Location(location.Latitude, location.Longitude),
                                    Distance.FromMeters(200)
                                )
                            );

                            MyMap.IsVisible = true;
                        });

                        _mapInitialized = true;
                    }


                    await Task.Delay(250);
                }
            }
            catch (OperationCanceledException)
            {
                // stopped
            }
        }

        // Hàm hiển thị thông báo khi người dùng đến gần POI{not in use}

        void ShowAndroidNotification(string title, string message)
        {
#if ANDROID
            var context = Android.App.Application.Context;
            const string channelId = "poi_channel";

            var manager =
                (Android.App.NotificationManager?)context.GetSystemService(
                    Android.Content.Context.NotificationService);

            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
            {
                var channel = new Android.App.NotificationChannel(
                    channelId,
                    "POI Alerts",
                    Android.App.NotificationImportance.High);

                manager?.CreateNotificationChannel(channel);
            }

            var notification = new Android.App.Notification.Builder(context, channelId)
                .SetContentTitle(title)
                .SetContentText(message)
                .SetSmallIcon(Android.Resource.Drawable.IcDialogMap)
                .SetAutoCancel(true)
                .Build();

            manager?.Notify(1001, notification);
#endif
        }

        // Hàm tính khoảng cách giữa 2 điểm dựa trên công thức Haversine

        double DistanceInMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000; // meters
            double dLat = DegreesToRadians(lat2 - lat1);
            double dLon = DegreesToRadians(lon2 - lon1);

            double a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) *
                Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        // Hàm chuyển đổi độ sang radians

        double DegreesToRadians(double deg) => deg * Math.PI / 180;

        // Hàm load tất cả các điểm POI

        void LoadPoisOnMap()
        {
            MyMap.Pins.Clear();
            foreach (var poi in _pois)
            {
                var pin = new Pin
                {
                    Label = poi.Name,
                    Address = poi.Description,
                    Type = PinType.SearchResult, // or SavedPin, SearchResult, Generic
                    Location = new Location(poi.Latitude, poi.Longitude),
                };

                pin.MarkerClicked += async (s, e) =>
                {
                    e.HideInfoWindow = false;

                    //SelectedPoi = poi; -> this will update in ShowPoiWithTransition() func

                    //await ShowBottomSheet();
                    await ShowPoiWithTransition(poi);
                };
                MyMap.Pins.Add(pin);
            }
        }

        // pop up sheet when click on to POI pin, in real app this would show more details about the POI and maybe some actions (like navigate, call, etc)

        async Task ShowBottomSheet()
        {
            if (_sheetVisible) return;

            _sheetVisible = true;
            await PoiSheet.TranslateTo(0, SheetVisibleY, 300, Easing.CubicOut);

        }

        // pop down sheet when click outside of it, in real app this would be triggered when user clicks a close button or swipes down the sheet

        async Task HideBottomSheet()
        {
            await PoiSheet.TranslateTo(0, SheetHiddenY, 300, Easing.CubicIn);
            _sheetVisible = false;

        }

        double _startY;

        // Hàm xử lý sự kiện pan trên bottom sheet để cho phép người dùng kéo sheet lên/xuống, nếu kéo xuống quá 50px thì sẽ tự động ẩn sheet đi

        private async void OnSheetPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _startY = PoiSheet.TranslationY;
                    break;

                case GestureStatus.Running:

                    double newY = _startY + e.TotalY;

                    // Prevent dragging upward beyond visible position
                    if (newY < SheetVisibleY)
                        newY = SheetVisibleY;

                    PoiSheet.TranslationY = newY;
                    break;

                case GestureStatus.Completed:

                    // If dragged down more than 100px → hide
                    if (PoiSheet.TranslationY > SheetVisibleY + 50)
                    {
                        await HideBottomSheet();
                    }
                    else
                    {
                        // Snap back to visible position
                        await ShowBottomSheet();
                    }

                    break;
            }
        }

        //transition for the bottom sheet

        async Task ShowPoiWithTransition(PointOfInterest poi)
        {

            // Ensure sheet is visible FIRST
            if (!_sheetVisible)
            {
                SelectedPoi = poi;
                await ShowBottomSheet(); // open once
                await Task.WhenAll(
                PoiContent.FadeTo(1, 180, Easing.CubicOut),
                PoiContent.TranslateTo(0, 0, 180, Easing.CubicOut)
            );
                return;
            }

            // Fade old content out
            await PoiContent.FadeTo(0, 120);
            PoiContent.TranslationY = 10;

            // Update data AFTER fade-out
            SelectedPoi = poi;

            await Task.Delay(30); // prevents layout race

            // Fade new content in
            await Task.WhenAll(
                PoiContent.FadeTo(1, 180, Easing.CubicOut),
                PoiContent.TranslateTo(0, 0, 180, Easing.CubicOut)
            );

            // audio play 
            await SpeakPoiDescription(poi);
        }

        // read and speak the description of the POI when click on it, in real app this would be triggered by walk though poi
        public async Task SpeakPoiDescription(PointOfInterest poi)
        {
            if (string.IsNullOrWhiteSpace(poi.Description))
                return;

            #if ANDROID
            AndroidTtsService.Speak(poi.Description);
            #endif
        }

        // navigate to setting page

        private async void OnMenuClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new SettingPage());
        }
    }
}

