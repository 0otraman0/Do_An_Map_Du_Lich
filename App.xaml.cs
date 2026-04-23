using MauiAppMain.Services;

namespace MauiAppMain
{
    public partial class App : Application
    {
        private readonly DataFetch dataFetch;
        private readonly HeartbeatService _heartbeatService;
        public App(DataFetch dataFetch, HeartbeatService heartbeatService)
        {
            LanguageService.LoadSavedLanguage(); // Must be called before InitializeComponent to translate UI
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
            
            // NOTE: Việc fetch dữ liệu lần đầu giờ đã được chuyển sang MainPage.xaml.cs 
            // để có thể hiển thị màn hình Loading (chặn UI) chờ cho đến khi tải xong.
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
            
            // Đảm bảo fetch dữ liệu mỗi khi App được mở lại từ Background
            Task.Run(async () =>
            {
                try
                {
                    await dataFetch.FetchData(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("FETCH DATA ERROR ON RESUME: " + ex.Message);
                }
            });
        }
    }
}