using MauiAppMain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Maps.Handlers;
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
                MapHandler.Mapper.AppendToMapping("DisableMyLocationButton", (handler, view) =>
                {
                #if ANDROID
                    handler.PlatformView.GetMapAsync(new MapReadyCallback(map =>
                    {
                        map.UiSettings.MyLocationButtonEnabled = false;
                    }));
                #endif
                });
            builder.Services.AddSingleton<DatabaseService>();
            

#if DEBUG
            builder.Logging.AddDebug();
            #endif

            return builder.Build();
        }
    }
}
