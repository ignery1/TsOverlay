using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TS6_SpeakerOverlay.Helpers
{
    public static class WindowHelper
    {
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TOOLWINDOW = 0x00000080; // [新增] esconde de Alt+Tab / Task View
        private const int GWL_EXSTYLE = -20;

        // 新增：置顶相关常量
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        // Foreground window detection
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        // [Added] Window title via pure Win32 (GetWindowText), without opening a process handle
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        /// <summary>
        /// Name (without .exe) of the process owning the currently foreground window.
        /// Returns null if it cannot be resolved (window closed, insufficient permissions, etc.).
        /// </summary>
        public static string? GetForegroundProcessName()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;

            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return null;

            try
            {
                using var proc = System.Diagnostics.Process.GetProcessById((int)pid);
                return proc.ProcessName;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// True if the foreground window belongs to our own process
        /// (MainWindow, SettingsWindow, etc.). Used to avoid hiding the overlay
        /// simply because the user clicked on it (settings, dragging, etc.).
        /// </summary>
        public static bool IsForegroundOwnProcess()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;

            GetWindowThreadProcessId(hwnd, out uint pid);
            return pid == (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        }

        /// <summary>
        /// Foreground window title, obtained directly via Win32 (GetWindowText)
        /// without needing to open a handle to its owning process. More reliable than
        /// ProcessName for processes protected by anti-cheat, which often block
        /// most Process properties (MainModule, Handle, ExitCode, etc.)
        /// but do not hide the window title itself.
        /// </summary>
        public static string? GetForegroundWindowTitle()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;

            int length = GetWindowTextLength(hwnd);
            if (length == 0) return null;

            var sb = new System.Text.StringBuilder(length + 1);
            GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        // 新增：强制设置窗口位置的 API
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        public static void EnableClickThrough(Window window)
        {
            var helper = new WindowInteropHelper(window);
            if (helper.Handle == IntPtr.Zero) return;
            int extendedStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW);
        }

        public static void DisableClickThrough(Window window)
        {
            var helper = new WindowInteropHelper(window);
            if (helper.Handle == IntPtr.Zero) return;
            int extendedStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, (extendedStyle & ~WS_EX_TRANSPARENT) | WS_EX_TOOLWINDOW);
        }

        // Ensures the window never appears in Alt+Tab / Task View,
        // regardless of when WPF would normally apply this behavior.
        // Call once in the window's Loaded event and whenever it is shown again (Show()).
        public static void HideFromAltTab(Window window)
        {
            var helper = new WindowInteropHelper(window);
            if (helper.Handle == IntPtr.Zero) return;
            int extendedStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, extendedStyle | WS_EX_TOOLWINDOW);
        }

        // 新增：暴力置顶方法
        public static void ForceTopMost(Window window)
        {
            var helper = new WindowInteropHelper(window);
            if (helper.Handle == IntPtr.Zero) return;

            // 强制将窗口置于 Z 轴最顶层，且不改变大小和位置，不激活窗口
            SetWindowPos(helper.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }
    }
}