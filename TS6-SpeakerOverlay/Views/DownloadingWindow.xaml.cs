using System.Windows;

namespace TS6_SpeakerOverlay.Views
{
    public partial class DownloadingWindow : Window
    {
        public DownloadingWindow(string version)
        {
            InitializeComponent();
            TxtTitle.Text = $"Downloading version {version}...";
        }

        public void SetProgress(double percent)
        {
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            ProgressBarControl.Value = percent;
            TxtPercent.Text = $"{percent:0}%";
        }
    }
}
