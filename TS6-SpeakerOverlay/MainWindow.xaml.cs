using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TS6_SpeakerOverlay.Helpers;
using TS6_SpeakerOverlay.ViewModels;

// 消除歧义
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace TS6_SpeakerOverlay
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _topmostTimer;
        private DispatcherTimer _processWatchTimer;
        private TrayIconHelper? _trayIcon;

        private bool _isDragging = false;
        private Point _lastMousePosition;

        private bool _isExiting = false;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;

            this.MouseDown += MainWindow_MouseDown;
            this.MouseMove += MainWindow_MouseMove;
            this.MouseUp += MainWindow_MouseUp;

            _topmostTimer = new DispatcherTimer();
            _topmostTimer.Interval = TimeSpan.FromSeconds(2);
            _topmostTimer.Tick += (s, e) => WindowHelper.ForceTopMost(this);
            _topmostTimer.Start();

            _processWatchTimer = new DispatcherTimer();
            _processWatchTimer.Interval = TimeSpan.FromSeconds(3);
            _processWatchTimer.Tick += (s, e) => CheckTargetProcess();
            _processWatchTimer.Start();
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            OpenSettings();
        }

        private void Avatar_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.ToggleSettingsIconCommand.Execute(null);
            }
            e.Handled = true;
        }

        private void CheckTargetProcess()
        {
            if (DataContext is not MainViewModel vm) return;

            if (!vm.Config.OnlyShowForTargetProcess || string.IsNullOrWhiteSpace(vm.Config.TargetProcessName))
            {
                if (!this.IsVisible)
                {
                    this.Show();
                    WindowHelper.HideFromAltTab(this);
                }
                return;
            }

            string? foregroundProcess = WindowHelper.GetForegroundProcessName();
            string? foregroundTitle = WindowHelper.GetForegroundWindowTitle();
            bool targetIsForeground = TargetProcessHelper.Matches(vm.Config, foregroundProcess, foregroundTitle);
            bool ownWindowIsForeground = WindowHelper.IsForegroundOwnProcess();

            bool shouldShow = targetIsForeground || ownWindowIsForeground;

            if (shouldShow && !this.IsVisible)
            {
                this.Show();
                WindowHelper.HideFromAltTab(this);
            }
            else if (!shouldShow && this.IsVisible)
            {
                this.Hide();
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            WindowHelper.HideFromAltTab(this);

            _trayIcon = new TrayIconHelper(
                this, GetIsLocked, OpenSettings, RefreshData, CheckForUpdatesFromTray,
                (trayIcon) => _trayIcon = trayIcon
            );

            if (DataContext is MainViewModel vm)
            {
                this.Left = vm.Config.WindowLeft;
                this.Top = vm.Config.WindowTop;

                vm.Config.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(vm.Config.WindowLeft)) this.Left = vm.Config.WindowLeft;
                    if (args.PropertyName == nameof(vm.Config.WindowTop)) this.Top = vm.Config.WindowTop;
                };

                if (vm.IsOverlayLocked)
                {
                    Lock();
                }

                // The setup wizard (initial run) and the wait for the connection to the
                // TS6 are now orchestrated by App.xaml.cs, along with the "loading"
                // screen that remains visible throughout this process – see App.xaml.cs.
            }
        }

        private void CheckForUpdatesFromTray()
        {
            _ = App.CheckForUpdatesAsync(manual: true);
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowPosition();

            if (!_isExiting)
            {
                if (_trayIcon == null) return;
                e.Cancel = true;
                this.Hide();
                _trayIcon.UpdateTrayIcon();
                return;
            }

            _topmostTimer.Stop();
            _processWatchTimer.Stop();

            if (DataContext is MainViewModel vm)
            {
                vm.SaveConfig();
                vm.Shutdown();
            }
        }

        public void ExitApplication()
        {
            _isExiting = true;
            this.Close();
        }

        private void MainWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && !GetIsLocked())
            {
                _isDragging = true;
                _lastMousePosition = e.GetPosition(this);
                this.CaptureMouse();
            }
        }
        private void RefreshData()
        {
            if (DataContext is MainViewModel vm)
            {
                vm.RefreshData();
            }
        }
        private void MainWindow_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                Point currentMousePosition = e.GetPosition(this);
                double deltaX = currentMousePosition.X - _lastMousePosition.X;
                double deltaY = currentMousePosition.Y - _lastMousePosition.Y;
                this.Left += deltaX;
                this.Top += deltaY;
            }
        }

        private void MainWindow_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                this.ReleaseMouseCapture();
                SaveWindowPosition();
            }
        }

        private void SaveWindowPosition()
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Config.WindowLeft = this.Left;
                vm.Config.WindowTop = this.Top;
            }
        }

        private void OpenSettings()
        {
            if (DataContext is MainViewModel vm)
            {
                var settingsWindow = new Views.SettingsWindow(vm, this);
                settingsWindow.Show();
            }
        }

        private bool GetIsLocked()
        {
            return (DataContext is MainViewModel vm) && vm.IsOverlayLocked;
        }

        public void Lock()
        {
            if (DataContext is MainViewModel vm && !vm.IsOverlayLocked)
            {
                WindowHelper.EnableClickThrough(this);
                vm.IsOverlayLocked = true;
                // Do not remove – without persisting it here, the locked state was never
                // saved, and the app would always open in an unlocked state, even
                // after the setup process had already been completed.
                vm.Config.IsLocked = true;
                _trayIcon?.UpdateTrayIcon();
            }
        }

        public void Unlock()
        {
            if (DataContext is MainViewModel vm && vm.IsOverlayLocked)
            {
                WindowHelper.DisableClickThrough(this);
                vm.IsOverlayLocked = false;
                vm.Config.IsLocked = false;
                _trayIcon?.UpdateTrayIcon();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _trayIcon?.Dispose();
            base.OnClosed(e);
            Environment.Exit(0);
        }
    }
}
