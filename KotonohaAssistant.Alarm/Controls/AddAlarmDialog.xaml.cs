using KotonohaAssistant.Alarm.Models;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace KotonohaAssistant.Alarm.Controls
{
    /// <summary>
    /// AddAlarmDialog.xaml の相互作用ロジック
    /// </summary>
    public partial class AddAlarmDialog
    {
        public AddAlarmDialog()
        {
            InitializeComponent();

            Loaded += AddAlarmDialog_Loaded;
        }

        private void AddAlarmDialog_Loaded(object sender, RoutedEventArgs e)
        {
            var hours = Enumerable.Range(0, 12);
            var minutes = Enumerable.Range(0, 12).Select(m => m * 5);

            Hour.ItemsSource = hours;
            Minute.ItemsSource = minutes;
        }

        private void OKButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (AMPM.SelectedValue is null ||
                Hour.SelectedValue is null ||
                Minute.SelectedValue is null ||
                string.IsNullOrWhiteSpace(FilePath.Text))
            {
                return;
            }

            DialogResult = true;
        }

        private void CancelButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void VoiceFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Audio files (*.mp3)|*.mp3"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            if (!File.Exists(dialog.FileName))
            {
                return;
            }

            FilePath.Text = dialog.FileName;
        }

        public TimeSpan AlarmTime
        {
            get
            {
                var hour = (int)Hour.SelectedValue;
                int hour24;
                if (AMPM.Text == "AM")
                {
                    hour24 = (hour == 12) ? 0 : hour;
                }
                else
                {
                    hour24 = (hour == 12) ? 12 : hour + 12;
                }

                return new TimeSpan(hour24, (int)Minute.SelectedValue, 0);
            }
        }

        public AlarmSetting AlarmSetting => new AlarmSetting
        {
            Id = -1,
            TimeInSeconds = AlarmTime.TotalSeconds,
            VoicePath = FilePath.Text,
            IsRepeated = IsRepeated.IsChecked ?? false,
            IsEnabled = true
        };
    }
}
