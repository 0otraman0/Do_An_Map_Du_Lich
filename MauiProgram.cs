using MauiAppMain.Resources.Localization;
using MauiAppMain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Maps.Handlers;
using System.Globalization;
namespace MauiAppMain
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiMaps()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                })
                .ConfigureMauiHandlers(handlers =>
                {
#if ANDROID
                    handlers.AddHandler<Microsoft.Maui.Controls.Maps.Map, CustomMapHandler>();
#endif
                });
#if ANDROID
            MapHandler.Mapper.AppendToMapping("DisableMyLocationButton", (handler, view) =>
            {

                handler.PlatformView.GetMapAsync(new MapReadyCallback(map =>
                {
                    map.UiSettings.MyLocationButtonEnabled = false;
                }));
            });
#endif
            builder.Services.AddSingleton<DatabaseService>();
            builder.Services.AddSingleton<DataFetch>();
            builder.Services.AddSingleton<HttpClient>();
#if ANDROID

            builder.Services.AddSingleton<IDeviceInfoService, DeviceInfoService>();
#endif

#if DEBUG
            builder.Logging.AddDebug();
            #endif


            return builder.Build();
        }
    }
}
