using MauiAppMain.NewFolder1;
using Microsoft.Maui.Devices.Sensors;

namespace MauiAppMain
{
    public partial class MainPage : ContentPage
    {
        // ===== POI CONFIG =====
        List<PointOfInterest> _pois = new()
{
    new PointOfInterest
    {
        Name = "School",
        Latitude = 10.759893,
        Longitude = 106.679930,
        RadiusMeters = 10
    },
    new PointOfInterest
    {
        Name = "Coffee Shop",
        Latitude = 10.759548,
        Longitude = 106.679105,
        RadiusMeters = 10
    },
    new PointOfInterest
    {
        Name = "Library",
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
        Latitude = 10.758453,
        Longitude = 106.677928,
        RadiusMeters = 10
    }
};
        int count = 0;

        public MainPage()
        {
            InitializeComponent();
        }

        private void OnCounterClicked(object? sender, EventArgs e)
        {
            count++;

            if (count == 1)
                CounterBtn.Text = $"Clicked {count} time";
            else
                CounterBtn.Text = $"Clicked {count} times";

            SemanticScreenReader.Announce(CounterBtn.Text);
        }
        CancellationTokenSource? _cts;

        async Task<bool> EnsureLocationPermissionAsync()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            return status == PermissionStatus.Granted;
        }

        async void StartTracking()
        {
            if (!await EnsureLocationPermissionAsync())
                return;

            _cts = new CancellationTokenSource();

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

                    // ---- UI UPDATE ----
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        LatitudeLabel.Text = location.Latitude.ToString("0.000000");
                        LongitudeLabel.Text = location.Longitude.ToString("0.000000");
                        SpeedLabel.Text = location.Speed?.ToString("0.00") ?? "0";
                    });

                    double nearestDistance = double.MaxValue;
                    string nearestPoi = "None";

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
                            nearestPoi = poi.Name;
                        }

                        if (poi.IsTriggered)
                            continue;

                        if (distance <= poi.RadiusMeters)
                        {
                            poi.IsTriggered = true;

                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                PoiStatusLabel.Text = $"Entered: {poi.Name}";
                                ShowAndroidNotification(
                                    "POI Reached",
                                    $"You entered {poi.Name}");
                            });

                            break; // remove this if you want multiple POIs in one tick
                        }
                    }

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        DistanceLabel.Text =
                            $"Nearest POI: {nearestPoi} ({nearestDistance:0.0} m)";
                    });

                    await Task.Delay(250);
                }
            }
            catch (OperationCanceledException)
            {
                // stopped
            }
        }



        void StopTracking()
        {
            _cts?.Cancel();

            // Reset GPS UI
            LatitudeLabel.Text = "--";
            LongitudeLabel.Text = "--";
            SpeedLabel.Text = "--";

            // Reset POI UI
            PoiStatusLabel.Text = "POI status: --";
            DistanceLabel.Text = "--";
            // --CANCEL GPS OBJECT--
        }

         async void OnStartClicked(object sender, EventArgs e)
            {
            await EnsureNotificationPermissionAsync();
            StartTracking();
            }

            void OnStopClicked(object sender, EventArgs e)
            {
                StopTracking();
            }
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

        double DegreesToRadians(double deg) => deg * Math.PI / 180;

    }
}

