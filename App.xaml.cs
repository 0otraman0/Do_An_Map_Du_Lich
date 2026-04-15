using MauiAppMain.Services;

namespace MauiAppMain
{
    public partial class App : Application
    {
        private readonly DataFetch dataFetch;
        private readonly HeartbeatService _heartbeatService;
        public App(DataFetch dataFetch, HeartbeatService heartbeatService)
        {
            InitializeComponent();
            this.dataFetch = dataFetch;
            _heartbeatService = heartbeatService;
        }


        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }

        protected override void OnStart()
        {
            base.OnStart();

            // Start heartbeat timer (guaranteed on startup)
            _heartbeatService.StartHeartbeatTimer(TimeSpan.FromSeconds(10));

            // Perform background data fetch after the app has started
            Task.Run(async () =>
            {
                try
                {
                    bool forLanguage = false;
                    await dataFetch.FetchData(forLanguage);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("FETCH DATA ERROR: " + ex.Message);
                }
            });
        }

        protected override void OnSleep()
        {
            base.OnSleep();
            _heartbeatService.StopHeartbeatTimer();
        }

        protected override void OnResume()
        {
            base.OnResume();
            _heartbeatService.StartHeartbeatTimer(TimeSpan.FromSeconds(10));
        }
    }
}