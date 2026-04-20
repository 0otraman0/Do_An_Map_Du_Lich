using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MauiAppMain.Models;
using Microsoft.Maui.Storage;

namespace MauiAppMain.Services
{
    public static class PoiTrafficLogger
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string ApiUrl = "https://mvpnapane43yxbxpb2vdqz5zn40luikz.lambda-url.ap-southeast-1.on.aws/";

        public static async Task LogPoiPlayAsync(PointOfInterest poi)
        {
            if (poi == null) return;
            
            try
            {
                var deviceId = Preferences.Get("DeviceHeartbeatId", string.Empty);
                if (string.IsNullOrEmpty(deviceId))
                {
                    deviceId = Guid.NewGuid().ToString();
                    Preferences.Set("DeviceHeartbeatId", deviceId);
                }

                var payload = new
                {
                    poiId = poi.Id,
                    deviceId = deviceId,
                    latitude = poi.Latitude,
                    longitude = poi.Longitude,
                    poiName = poi.Name,
                    isWeb = false
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                System.Diagnostics.Debug.WriteLine($"[PoiTraffic] Sending Log for POI {poi.Name}...");
                
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await _httpClient.PostAsync(ApiUrl, content, cts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[PoiTraffic] Log saved successfully. Status: {response.StatusCode}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[PoiTraffic] Failed to save log. Status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PoiTraffic] Error: {ex.Message}");
            }
        }
    }
}
