using System.Windows;
using System.Windows.Controls; // 需要引用 ComboBox
using System.Windows.Input;
using System.Windows.Media;
using TS6_SpeakerOverlay.ViewModels;
using TS6_SpeakerOverlay.Models; // RunningProcessOption
using TS6_SpeakerOverlay.Helpers; // 引用 LanguageHelper
using Microsoft.Win32;

namespace TS6_SpeakerOverlay.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly TS6_SpeakerOverlay.MainWindow? _mainWindow;
        private bool _isInitialized = false;

        private static readonly System.Windows.Media.Brush SelectedPresetBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4F, 0xCD, 0x8E));

        public SettingsWindow(MainViewModel viewModel, TS6_SpeakerOverlay.MainWindow? mainWindow = null)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _mainWindow = mainWindow;
            this.DataContext = _viewModel;

            // Enabling/disabling dragging is now only possible while Settings is
            // open: it unlocks automatically upon opening (to allow dragging/testing the position) and
            // locks again automatically upon closing—this way, there is no risk of it remaining
            // unlocked unintentionally during gameplay.
            _mainWindow?.Unlock();
            this.Closing += (s, e) => _mainWindow?.Lock();

            _viewModel.RefreshProcessListCommand.Execute(null);
                        
            this.Loaded += (s, e) => RefreshPositionHighlight();
            _viewModel.Config.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.Config.WindowLeft) ||
                    e.PropertyName == nameof(_viewModel.Config.WindowTop))
                {
                    RefreshPositionHighlight();
                }
            };

            _isInitialized = true;
        }

        private void RefreshPositionHighlight()
        {
            string? current = PositionPresetHelper.DetectCurrent(_viewModel.Config);

            foreach (var btn in FindButtons(this))
            {
                string? preset = (btn.CommandParameter as string) ?? (btn.Tag as string);
                if (preset == null) continue;

                bool isSelected = preset == current;
                btn.BorderBrush = isSelected ? SelectedPresetBrush : null;
                btn.BorderThickness = isSelected ? new Thickness(2) : new Thickness(1);
                btn.FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal;
            }
        }

        private static IEnumerable<System.Windows.Controls.Button> FindButtons(DependencyObject root)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is System.Windows.Controls.Button btn) yield return btn;
                foreach (var sub in FindButtons(child)) yield return sub;
            }
        }

        //private void BtnLockDrag_Click(object sender, RoutedEventArgs e)
        //{
        //    _mainWindow?.Lock();
        //}

        //private void BtnUnlockDrag_Click(object sender, RoutedEventArgs e)
        //{
        //    _mainWindow?.Unlock();
        //}

        private void BtnRefreshProcesses_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.RefreshProcessListCommand.Execute(null);
        }

       
        private void ProcessComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;

            if (sender is System.Windows.Controls.ComboBox cb && cb.SelectedItem is RunningProcessOption selected)
            {
                TS6_SpeakerOverlay.Helpers.TargetProcessHelper.Add(_viewModel.Config, selected.ExeName);
                TS6_SpeakerOverlay.Helpers.TargetProcessHelper.AddTitle(_viewModel.Config, selected.WindowTitle);
            }
        }

        private void AutoStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(runKey, true)!)
                {
                    if (_viewModel.Config.AutoStart)
                    {
                        key.SetValue("TS6SpeakerOverlay", System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName!);
                    }
                    else
                    {
                        key.DeleteValue("TS6SpeakerOverlay", false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to set Auto-Start: {ex.Message}");
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                "Reset all settings to default?",
                "Confirm",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question
            );

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                _viewModel.Config.ResetDefaults();
                LanguageHelper.SetLanguage(_viewModel.Config.Language);
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;

            if (DataContext is MainViewModel vm)
            {
                if (e.AddedItems.Count > 0 && e.AddedItems[0] is ComboBoxItem item)
                {
                    string langCode = item.Tag.ToString() ?? "en-US";
                    vm.Config.Language = langCode;
                    LanguageHelper.SetLanguage(langCode);
                }
            }
        }
    }
}