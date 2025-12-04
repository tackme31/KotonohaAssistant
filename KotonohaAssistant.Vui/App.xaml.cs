namespace KotonohaAssistant.Vui
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new MainPage())
            {
                Title = "KotonohaAssistant.Vui",
                Width = 800,
                Height = 1000,
            };
        }
    }
}
