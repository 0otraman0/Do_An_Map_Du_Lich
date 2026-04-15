using Android.App;
using Android.Content.PM;
using Android.OS;

namespace MauiAppMain
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            // Set up a global exception handler for diagnostic logging
            Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) =>
            {
                Android.Util.Log.Error("MAUI_CRASH", "UNHANDLED EXCEPTION: " + args.Exception.ToString());
            };

            base.OnCreate(savedInstanceState);

            try 
            {
                AndroidTtsService.Init(this);
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error("MAUI_CRASH", "TTS INIT ERROR: " + ex.Message);
            }
        }
    }
}
