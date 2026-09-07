using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TS6_SpeakerOverlay.Helpers;
using TS6_SpeakerOverlay.Models;
using TS6_SpeakerOverlay.ViewModels;

namespace TS6_SpeakerOverlay.Views
{
    // Mini setup shown on first launch: screen position (with live
    // draggable preview), target process, who to show in the list, and how to display each
    // person (photo/dot/text). "Finish" saves the settings and locks the overlay in place.
    public partial class SetupWizardWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly TS6_SpeakerOverlay.MainWindow? _mainWindow;
        private bool _isInitialized = false;

        private static readonly System.Windows.Media.Brush SelectedPresetBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4F, 0xCD, 0x8E));
        private double _scaleX = 1;
        private double _scaleY = 1;
        private bool _isDraggingPreview = false;
        private System.Windows.Point _dragStartMouse;
        private System.Windows.Point _dragStartOverlayPos;

        public SetupWizardWindow(MainViewModel viewModel, TS6_SpeakerOverlay.MainWindow? mainWindow = null)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _mainWindow = mainWindow;

            // list of running processes as soon as the screen opens.
            _viewModel.RefreshProcessListCommand.Execute(null);
            ProcessComboBox.ItemsSource = _viewModel.AvailableProcesses;

            ChkOnlyForTarget.IsChecked = _viewModel.Config.OnlyShowForTargetProcess;
            RbShowOnlyTalking.IsChecked = _viewModel.Config.ShowOnlyTalking;
            RbShowEveryone.IsChecked = !_viewModel.Config.ShowOnlyTalking;

            switch (_viewModel.Config.AvatarDisplayMode)
            {
                case AvatarMode.Indicator: RbAvatarIndicator.IsChecked = true; break;
                case AvatarMode.None: RbAvatarNone.IsChecked = true; break;
                default: RbAvatarPhoto.IsChecked = true; break;
            }

            // Pre-selects the current language from Config.
            foreach (var obj in LanguageComboBox.Items)
            {
                if (obj is System.Windows.Controls.ComboBoxItem item &&
                    (item.Tag as string) == _viewModel.Config.Language)
                {
                    LanguageComboBox.SelectedItem = item;
                    break;
                }
            }

            this.Loaded += (s, e) => RefreshPreview();

            _isInitialized = true;
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;

            if (e.AddedItems.Count > 0 && e.AddedItems[0] is System.Windows.Controls.ComboBoxItem item)
            {
                string langCode = item.Tag?.ToString() ?? "en-US";
                _viewModel.Config.Language = langCode;
                LanguageHelper.SetLanguage(langCode);
            }
        }

        // Draggable position preview
        private void RefreshPreview()
        {
            double screenW = SystemParameters.WorkArea.Width;
            double screenH = SystemParameters.WorkArea.Height;

            _scaleX = PreviewScreenBorder.ActualWidth / screenW;
            _scaleY = PreviewScreenBorder.ActualHeight / screenH;

            PreviewOverlay.Width = PositionPresetHelper.OverlayWidth * _scaleX;
            PreviewOverlay.Height = PositionPresetHelper.OverlayHeight * _scaleY;

            Canvas.SetLeft(PreviewOverlay, _viewModel.Config.WindowLeft * _scaleX);
            Canvas.SetTop(PreviewOverlay, _viewModel.Config.WindowTop * _scaleY);

            RefreshPresetButtonHighlight();
        }

        private void RefreshPresetButtonHighlight()
        {
            string? current = PositionPresetHelper.DetectCurrent(_viewModel.Config);
            foreach (var btn in new[] { BtnTopLeft, BtnTopRight, BtnCenterLeft, BtnCenterRight, BtnBottomLeft, BtnBottomRight })
            {
                bool isSelected = (btn.Tag as string) == current;
                btn.BorderBrush = isSelected ? SelectedPresetBrush : null;
                btn.BorderThickness = isSelected ? new Thickness(2) : new Thickness(1);
                btn.FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal;
            }
        }

        private void PreviewOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingPreview = true;
            _dragStartMouse = e.GetPosition(PreviewCanvas);
            _dragStartOverlayPos = new System.Windows.Point(Canvas.GetLeft(PreviewOverlay), Canvas.GetTop(PreviewOverlay));
            PreviewOverlay.CaptureMouse();
        }

        private void PreviewOverlay_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isDraggingPreview) return;

            System.Windows.Point current = e.GetPosition(PreviewCanvas);
            double deltaX = current.X - _dragStartMouse.X;
            double deltaY = current.Y - _dragStartMouse.Y;

            double maxLeft = PreviewScreenBorder.ActualWidth - PreviewOverlay.Width;
            double maxTop = PreviewScreenBorder.ActualHeight - PreviewOverlay.Height;

            double newLeft = Clamp(_dragStartOverlayPos.X + deltaX, 0, maxLeft);
            double newTop = Clamp(_dragStartOverlayPos.Y + deltaY, 0, maxTop);

            Canvas.SetLeft(PreviewOverlay, newLeft);
            Canvas.SetTop(PreviewOverlay, newTop);

            _viewModel.Config.WindowLeft = newLeft / _scaleX;
            _viewModel.Config.WindowTop = newTop / _scaleY;

            RefreshPresetButtonHighlight();
        }

        private void PreviewOverlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingPreview = false;
            PreviewOverlay.ReleaseMouseCapture();
        }

        private static double Clamp(double value, double min, double max) =>
            value < min ? min : (value > max ? max : value);

        private void PositionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string preset)
            {
                _viewModel.ApplyPositionPresetCommand.Execute(preset);
                RefreshPreview();
            }
        }


        private void BtnRefreshProcesses_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.RefreshProcessListCommand.Execute(null);
        }

        private void ProcessComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;

            if (sender is System.Windows.Controls.ComboBox cb && cb.SelectedItem is RunningProcessOption selected)
            {
                TargetProcessHelper.Add(_viewModel.Config, selected.ExeName);
                TargetProcessHelper.AddTitle(_viewModel.Config, selected.WindowTitle);
            }
        }

        private void ChkOnlyForTarget_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            _viewModel.Config.OnlyShowForTargetProcess = ChkOnlyForTarget.IsChecked == true;
        }

        private void ShowMode_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            _viewModel.Config.ShowOnlyTalking = RbShowOnlyTalking.IsChecked == true;
        }

        private void AvatarMode_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            if (RbAvatarIndicator.IsChecked == true) _viewModel.Config.AvatarDisplayMode = AvatarMode.Indicator;
            else if (RbAvatarNone.IsChecked == true) _viewModel.Config.AvatarDisplayMode = AvatarMode.None;
            else _viewModel.Config.AvatarDisplayMode = AvatarMode.Avatar;
        }

        private void BtnFinish_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Config.HasCompletedFirstRun = true;
            _viewModel.SaveConfig();

            _mainWindow?.Lock();

            this.Close();
        }
    }
}