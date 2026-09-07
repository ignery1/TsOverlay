using System.Windows;

namespace TS6_SpeakerOverlay.Views
{
    public partial class LoadingWindow : Window
    {
        public LoadingWindow()
        {
            InitializeComponent();
        }

        public void SetStatus(string text) => TxtStatus.Text = text;
    }
}
