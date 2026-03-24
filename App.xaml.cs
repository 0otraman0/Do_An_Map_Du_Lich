namespace MauiAppMain
{
    public partial class App : Application
    {
        private readonly DataFetch dataFetch;
        public App(DataFetch dataFetch)
        {
            InitializeComponent();
            Task.Run(async () =>
            {
                try
                {
                    bool forLanguage = false;
                    await dataFetch.FetchData(forLanguage);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("FETCH ERROR: " + ex.Message);
                }
            });
            this.dataFetch = dataFetch;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}