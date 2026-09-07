using System;
using System.Diagnostics;
using System.Windows;

namespace TS6_SpeakerOverlay.Views
{
    public partial class CrashWindow : Window
    {
        private readonly string _report;
        private readonly string _logPath;

        public CrashWindow(string report, string logPath, bool isFatal)
        {
            InitializeComponent();
            _report = report;
            _logPath = logPath;

            TxtDetails.Text = report;

            if (isFatal)
            {
                TxtSubtitle.Text = "The TsOverlay needs to close due to this error.";
                BtnClose.Content = "Close App";
            }
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Clipboard.SetText(_report);
                System.Windows.MessageBox.Show("Error copied to clipboard.",
                    "TsOverlay", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch
            {
            }
        }

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Clipboard.SetText(_report);

                if (!string.IsNullOrEmpty(_logPath))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_logPath}\"")
                    {
                        UseShellExecute = true
                    });
                }

                System.Windows.MessageBox.Show(
                    "The error report was copied and the log folder was opened.\n" +
                    "Please attach this file/text to the support team.",
                    "TsOverlay", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch
            {
                // Ignore
            }

            this.Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
