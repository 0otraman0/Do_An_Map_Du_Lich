namespace MauiAppMain
{
    public partial class MainPage : ContentPage
    {
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
                    Console.WriteLine($"TICK {DateTime.Now:HH:mm:ss.fff}");
                    var request = new GeolocationRequest(
                        GeolocationAccuracy.High,
                        TimeSpan.FromSeconds(4));

                    var location = await Geolocation.GetLocationAsync(request, _cts.Token);

                    if (location != null)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            LatitudeLabel.Text = location.Latitude.ToString();
                            LongitudeLabel.Text = location.Longitude.ToString();
                            SpeedLabel.Text = location.Speed?.ToString("0.00") ?? "0";
                        });
                    }
                    else
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            LatitudeLabel.Text = "1";
                            LongitudeLabel.Text = "wqe";
                            SpeedLabel.Text = "dqw";
                        });
                    }
                    Console.WriteLine("lat: " + location?.Latitude.ToString() + " long: " + location?.Longitude.ToString());

                    await Task.Delay(1000); // 1 second interval
                }
            }
            catch (OperationCanceledException)
            {
                // Tracking stopped
            }
        }


        void StopTracking()
        {
            _cts?.Cancel();
        }

        void OnStartClicked(object sender, EventArgs e)
            {
                StartTracking();
            }

            void OnStopClicked(object sender, EventArgs e)
            {
                StopTracking();
            }
        }
}

