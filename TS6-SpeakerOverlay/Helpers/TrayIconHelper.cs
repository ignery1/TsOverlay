using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace TS6_SpeakerOverlay.Helpers
{
    public class TrayIconHelper : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private readonly Window _mainWindow;
        private readonly Func<bool> _getIsLocked;
        private readonly Action _openSettingsAction;
        private readonly Action _refreshAction;
        // [新增] Item "Verificar atualização" - multi-idioma via LanguageHelper.
        private readonly Action _checkUpdateAction;
        private readonly Action<TrayIconHelper?> _setTrayIconRef;
        private bool _isExiting;

        // 菜单项引用
        private ToolStripMenuItem? _settingsMenuItem;
        private ToolStripMenuItem? _refreshMenuItem;
        private ToolStripMenuItem? _checkUpdateMenuItem; // [新增]
        private ToolStripMenuItem? _showMenuItem;
        private ToolStripMenuItem? _hideMenuItem;
        private ToolStripMenuItem? _exitMenuItem;

        // [修改] Removidos lockAction/unlockAction - bloquear/desbloquear o arrasto agora
        // só é possível pela tela de Settings (abre já destravado, trava sozinho ao
        // fechar). O ícone da bandeja continua mostrando a cor conforme o estado
        // (via getIsLocked), só não permite mais alternar por aqui.
        public TrayIconHelper(Window mainWindow, Func<bool> getIsLocked, Action openSettingsAction, Action refreshAction, Action checkUpdateAction, Action<TrayIconHelper?> setTrayIconRef)
        {
            _mainWindow = mainWindow;
            _getIsLocked = getIsLocked;
            _openSettingsAction = openSettingsAction;
            _refreshAction = refreshAction;
            _checkUpdateAction = checkUpdateAction;
            _setTrayIconRef = setTrayIconRef;
            InitializeTrayIcon();
            UpdateTrayIcon();
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = CreateIcon(Color.Gray),
                Visible = true,
                Text = "TS6 Speaker Overlay - by Cleri"
            };

            var contextMenu = new ContextMenuStrip();

            _settingsMenuItem = new ToolStripMenuItem("Settings");
            _settingsMenuItem.Click += (_, _) => _openSettingsAction.Invoke();

            _refreshMenuItem = new ToolStripMenuItem("Refresh");
            _refreshMenuItem.Click += (_, _) => _refreshAction.Invoke();

            // [新增] Verificar atualização - dispara o mesmo fluxo usado na abertura do
            // app, mas avisando o usuário mesmo se não achar nada novo (checagem manual).
            _checkUpdateMenuItem = new ToolStripMenuItem("Check for updates");
            _checkUpdateMenuItem.Click += (_, _) => _checkUpdateAction.Invoke();

            _showMenuItem = new ToolStripMenuItem("Show");
            _showMenuItem.Click += (_, _) => { ShowWindow(); UpdateTrayIcon(); };

            _hideMenuItem = new ToolStripMenuItem("Hide");
            _hideMenuItem.Click += (_, _) => { HideWindow(); UpdateTrayIcon(); };

            _exitMenuItem = new ToolStripMenuItem("Exit");
            _exitMenuItem.Click += (_, _) => ExitApplication();

            contextMenu.Items.Add(_settingsMenuItem);
            contextMenu.Items.Add(_refreshMenuItem);
            contextMenu.Items.Add(_checkUpdateMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(_showMenuItem);
            contextMenu.Items.Add(_hideMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(_exitMenuItem);

            contextMenu.Opening += (_, _) => UpdateMenuText();

            _notifyIcon.ContextMenuStrip = contextMenu;
            UpdateMenuText();
        }

        private void UpdateMenuText()
        {
            if (_settingsMenuItem == null) return;

            _settingsMenuItem.Text = LanguageHelper.GetString("Lang_Tray_Settings");
            _refreshMenuItem!.Text = LanguageHelper.GetString("Lang_Tray_Refresh");
            // [新增] Precisa da chave "Lang_Tray_CheckUpdate" nos dicionários de idioma
            // (pt-BR/en-US/zh-CN etc.) - mesmo padrão das demais strings da bandeja.
            _checkUpdateMenuItem!.Text = LanguageHelper.GetString("Lang_Tray_CheckUpdate");
            _showMenuItem!.Text = LanguageHelper.GetString("Lang_Tray_Show");
            _hideMenuItem!.Text = LanguageHelper.GetString("Lang_Tray_Hide");
            _exitMenuItem!.Text = LanguageHelper.GetString("Lang_Tray_Exit");
        }

        private Icon CreateIcon(Color color)
        {
            var bitmap = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var brush = new SolidBrush(color))
                {
                    g.FillEllipse(brush, 2, 2, 12, 12);
                }
            }
            return Icon.FromHandle(bitmap.GetHicon());
        }

        public void UpdateTrayIcon()
        {
            if (_notifyIcon == null) return;

            var isVisible = _mainWindow.Visibility == Visibility.Visible;
            var isLocked = _getIsLocked();

            Color iconColor;
            string statusKey;

            if (!isVisible)
            {
                iconColor = Color.Gray;
                statusKey = " (Hidden)";
            }
            else if (isLocked)
            {
                iconColor = Color.FromArgb(79, 205, 142);
                statusKey = " (Locked)";
            }
            else
            {
                iconColor = Color.DodgerBlue;
                statusKey = " (Unlocked - Settings aberto)";
            }

            _notifyIcon.Text = "TS6 Speaker Overlay - by Cleri" + statusKey;

            var oldIcon = _notifyIcon.Icon;
            _notifyIcon.Icon = CreateIcon(iconColor);
            oldIcon?.Dispose();

            if (_showMenuItem != null) _showMenuItem.Enabled = !isVisible;
            if (_hideMenuItem != null) _hideMenuItem.Enabled = isVisible;

            UpdateMenuText();
        }

        private void ShowWindow() { _mainWindow.Show(); _mainWindow.Activate(); }
        private void HideWindow() => _mainWindow.Hide();

        private void ExitApplication()
        {
            _setTrayIconRef(null);
            Dispose();
            if (_mainWindow is TS6_SpeakerOverlay.MainWindow mw)
                mw.ExitApplication();
            else
                Application.Current.Shutdown();
        }

        public void Dispose()
        {
            if (_isExiting) return;
            _isExiting = true;
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }
    }
}