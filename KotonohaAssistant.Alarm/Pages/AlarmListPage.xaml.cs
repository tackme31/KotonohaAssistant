using KotonohaAssistant.Alarm.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace KotonohaAssistant.Alarm.Pages
{
    /// <summary>
    /// AlarmListPage.xaml の相互作用ロジック
    /// </summary>
    public partial class AlarmListPage : Page
    {
        public AlarmListPage()
        {
            InitializeComponent();

            Loaded += AlarmListPage_Loaded;
        }

        private async void AlarmListPage_Loaded(object sender, RoutedEventArgs e)
        {
            var vm = (AlarmListViewModel)DataContext;
            await vm.InitializeAsync();
        }
    }
}
