using KotonohaAssistant.Alarm.Pages;
using KotonohaAssistant.Alarm.ViewModels;
using System.Windows;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace KotonohaAssistant.Alarm
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly INavigationService _navigationService;

        public MainWindow(INavigationService navigationService)
        {
            InitializeComponent();

            _navigationService = navigationService;

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _navigationService.SetNavigationControl(Menu);
            _navigationService.Navigate(typeof(AlarmListPage));
        }

        private void Menu_Navigated(NavigationView sender, NavigatedEventArgs args)
        {
            var vm = (RootViewModel)DataContext;
            switch (args.Page)
            {
                case AlarmListPage alarmList:
                    alarmList.DataContext = vm.AlarmList;
                    break;
                case TimerPage timer:
                    timer.DataContext = vm.Timer;
                    break;
            }
        }
    }
}