using KotonohaAssistant.Alarm.Models;
using Microsoft.Win32;
using System.IO;
using System.Windows;

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
            var hours = Enumerable.Range(0, 23);

            Hour.ItemsSource = hours;
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            if (Hour.SelectedValue is null ||
                Minute.Value is null ||
                string.IsNullOrWhiteSpace(FilePath.Text))
            {
                return;
            }

            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
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

        private TimeSpan AlarmTime => new ((int) Hour.SelectedValue, (int)(Minute.Value ?? 0), 0);


        public AlarmSetting AlarmSetting => new()
        {
            Id = -1,
            TimeInSeconds = AlarmTime.TotalSeconds,
            VoicePath = FilePath.Text,
            IsRepeated = IsRepeated.IsChecked ?? false,
            IsEnabled = true
        };
    }
}
