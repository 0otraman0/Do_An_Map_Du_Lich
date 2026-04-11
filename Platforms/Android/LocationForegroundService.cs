using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using MauiAppMain;
using MauiAppMain.Models;
using MauiAppMain.Services;
using Stream = Android.Media.Stream;

[Service(ForegroundServiceType = Android.Content.PM.ForegroundService.TypeLocation)]
public class LocationForegroundService : Service
{
    public static bool IsRunning = false;
    private readonly DatabaseService database = new DatabaseService();
    private List<PointOfInterest> _pois = new();
    private Dictionary<int, DateTime> _poiLastSpoken = new();

    CancellationTokenSource _cts;

    public override IBinder OnBind(Intent intent) => null;

    public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
    {
        if (IsRunning)
            return StartCommandResult.NotSticky;
        IsRunning = true;

        StartForeground(1, CreateNotification());

        _cts = new CancellationTokenSource();
        _ = InitAndStartTracking(_cts.Token);
        _ = StartTracking(_cts.Token);

        return StartCommandResult.Sticky;
    }
    
    Notification CreateNotification()
    {
        string channelId = "location_service";

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(
                channelId,
                "Background Tracking",
                NotificationImportance.Default);

            var manager = (NotificationManager)GetSystemService(NotificationService);
            manager.CreateNotificationChannel(channel);
        }

        return new Notification.Builder(this, channelId)
            .SetContentTitle("Tour Guide")
            .SetContentText("Tracking location in background")
            .SetSmallIcon(Android.Resource.Drawable.IcMenuMyLocation)
            .Build();
    }

    async Task InitAndStartTracking(CancellationToken token)
    {
        _pois = await database.GetPOIsAsync();
    }

    async Task StartTracking(CancellationToken token)
    {
        // QUAN TRỌNG: Phải có chữ await để luồng được nhường quyền (Bảo vệ App khỏi treo ANR)
        await Task.Delay(5000); 
        try
        {
            while (!token.IsCancellationRequested)
            {
                var request = new GeolocationRequest(
                    GeolocationAccuracy.High,
                    TimeSpan.FromSeconds(4));

                var location = await Geolocation.GetLocationAsync(request, token);

                if (location != null)
                {
                    foreach (var poi in _pois)
                    {
                        double distance = location.CalculateDistance(
                            new Location(poi.Latitude, poi.Longitude),
                            DistanceUnits.Kilometers);

                        double meters = distance * 1000;

                        if (meters < poi.RadiusMeters)
                        {
                            if (_poiLastSpoken.TryGetValue(poi.Id, out var lastSpoken))
                            {
                                if ((DateTime.Now - lastSpoken).TotalMinutes < 1)
                                    continue; // skip recently spoken POIs
                            }

                            _poiLastSpoken[poi.Id] = DateTime.Now;
                            _ = SpeakPoiDescription(poi);
                            break; // only speak one per loop
                        }
                    }
                }

                await Task.Delay(4000, token);
            }
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    public override void OnDestroy()
    {
        IsRunning = false;
        _cts?.Cancel();
        base.OnDestroy();
    }
    // Hàm này sẽ được gọi khi bạn muốn phát âm mô tả của một POI nào đó
    public async Task SpeakPoiDescription(PointOfInterest poi)
    {
        if (poi == null || string.IsNullOrWhiteSpace(poi.Description) || Preferences.Get("SoundPlayWhenClickedPOI", false) == false) return;


        var audioManager = (AudioManager)GetSystemService(AudioService);

        var focusRequest = audioManager.RequestAudioFocus(
            null,
            Stream.Music,
            AudioFocus.GainTransientMayDuck
        );
        // Ép chạy trên luồng giao diện (Main Thread)
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {   
                AndroidTtsService.Speak(poi.Description);
            }
            catch (Exception ex)
            {
                // Nếu có lỗi, nó sẽ hiện thông báo lên màn hình cho bạn biết
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            finally
            {
                // Release audio focus after speaking
                audioManager.AbandonAudioFocus(null);
            }
        });
    }

    public override void OnTaskRemoved(Intent rootIntent)
    {
        StopForeground(true);
        Console.WriteLine("DEBUG: Service is stopping due to task removal.");
        StopSelf();
        base.OnTaskRemoved(rootIntent);
    }
}