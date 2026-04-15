using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MauiAppMain.Services
{
    public class HeartbeatService
    {
        private readonly HttpClient _httpClient;
        private readonly string _lambdaUrl = "https://hy63tevx5bcvvy5e5mdvcuqhyy0mwxzy.lambda-url.ap-southeast-1.on.aws/";
        private PeriodicTimer? _timer;
        private CancellationTokenSource? _cts;

        public HeartbeatService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public string GetOrCreateDeviceId()
        {
            var deviceId = Preferences.Get("DeviceHeartbeatId", string.Empty);
            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = Guid.NewGuid().ToString();
                Preferences.Set("DeviceHeartbeatId", deviceId);
            }
            return deviceId;
        }

        public async Task SendHeartbeatAsync()
        {
            try
            {
                var location = await GetCurrentLocation();
                
                var payload = new HeartbeatPayload
                {
                    DeviceId = GetOrCreateDeviceId(),
                    Timestamp = DateTime.UtcNow.ToString("o"),
                    Status = "active",
                    Platform = DeviceInfo.Current.Platform.ToString(),
                    Model = DeviceInfo.Current.Model,
                    Name = DeviceInfo.Current.Name,
                    Latitude = location?.Latitude ?? 0,
                    Longitude = location?.Longitude ?? 0
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                System.Diagnostics.Debug.WriteLine($"[Heartbeat] Sending POST to: {_lambdaUrl}");
                
                // Use a dedicated timeout for the individual request
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await _httpClient.PostAsync(_lambdaUrl, content, timeoutCts.Token);
                
                System.Diagnostics.Debug.WriteLine("[Heartbeat] POST response received");
                string responseBody = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[Heartbeat] Success: {responseBody}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Heartbeat] Failed: {response.StatusCode} - {responseBody}");
                }
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[Heartbeat] Error: Request timed out after 10 seconds");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Heartbeat] Error: {ex.Message}");
            }
        }

        private async Task<Location?> GetCurrentLocation()
        {
            System.Diagnostics.Debug.WriteLine("[Heartbeat] Requesting location...");
            try
            {
                // Try last known first for speed
                var location = await Geolocation.Default.GetLastKnownLocationAsync();
                
                // If we have a fresh location (within 30 seconds), use it immediately
                if (location != null && (DateTimeOffset.Now - location.Timestamp).TotalSeconds < 30)
                {
                    System.Diagnostics.Debug.WriteLine("[Heartbeat] Using fresh last-known location");
                    return location;
                }

                // Otherwise request current with a short timeout
                var result = await Geolocation.Default.GetLocationAsync(new GeolocationRequest
                {
                    DesiredAccuracy = GeolocationAccuracy.Medium,
                    Timeout = TimeSpan.FromSeconds(3)
                });

                if (result != null)
                    System.Diagnostics.Debug.WriteLine("[Heartbeat] New location acquired");
                else
                    System.Diagnostics.Debug.WriteLine("[Heartbeat] Location request returned null");

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Heartbeat] Geolocation exception: {ex.Message}");
                return null;
            }
        }

        public void StartHeartbeatTimer(TimeSpan interval)
        {
            StopHeartbeatTimer();
            _cts = new CancellationTokenSource();
            _timer = new PeriodicTimer(interval);
            
            _ = Task.Run(async () =>
            {
                // Wait for the system to stabilize before the first heartbeat
                await Task.Delay(TimeSpan.FromSeconds(10));

                // Send initial heartbeat
                await SendHeartbeatAsync();

                try
                {
                    while (await _timer.WaitForNextTickAsync(_cts.Token))
                    {
                        await SendHeartbeatAsync();
                    }
                }
                catch (OperationCanceledException) { }
            }, _cts.Token);
        }

        public void StopHeartbeatTimer()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _timer?.Dispose();
            _cts = null;
            _timer = null;
        }
    }

    public class HeartbeatPayload
    {
        [JsonPropertyName("deviceId")]
        public string DeviceId { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("platform")]
        public string Platform { get; set; } = string.Empty;

        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }
    }
}
