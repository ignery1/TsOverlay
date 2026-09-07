using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.VisualBasic.ApplicationServices;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TS6_SpeakerOverlay.Helpers;
using TS6_SpeakerOverlay.Models;
using TS6_SpeakerOverlay.Services;
using User = TS6_SpeakerOverlay.Models.User;

namespace TS6_SpeakerOverlay.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        public ObservableCollection<User> Users { get; } = new();
        public ObservableCollection<Notification> Notifications { get; } = new();

        public ObservableCollection<RunningProcessOption> AvailableProcesses { get; } = new();

        public AppConfig Config { get; }

        private readonly Ts6Service _tsService;
        private string _currentChannelId = "";

        [ObservableProperty] private bool _isOverlayLocked = false;

        [ObservableProperty] private bool _isSettingsIconVisible = false;

        // 连接状态文本
        [ObservableProperty] private string _connectionStatus = "Connecting...";
        [ObservableProperty] private bool _isConnected = false;

        public MainViewModel()
        {
            Config = ConfigService.Load();
            LanguageHelper.SetLanguage(Config.Language);

            // [新增] 启动时恢复锁定状态
            IsOverlayLocked = Config.IsLocked;

            _tsService = new Ts6Service();

            // 1. 连接状态变化
            _tsService.OnConnectionStateChanged += async (isConnected) =>
            {
                if (isConnected)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsConnected = true;
                        ConnectionStatus = ""; // 连上瞬间清空提示
                    });
                }
                else
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => IsConnected = false);

                    // [极速优化] 将防抖动延迟从 2000 改为 500
                    // 0.5秒足够过滤掉网络波动，同时让用户感觉反应很快
                    await Task.Delay(500);

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (!IsConnected)
                        {
                            ConnectionStatus = LanguageHelper.GetString("Lang_Status_Waiting");
                            Users.Clear();
                        }
                    });
                }
            };

            // 2. 列表更新 (核心修改)
            // Extracted to a dedicated method (UpdateUsers) – this way,
            // it can also be called again after a forced SendAuth in OnClientMoved,
            // without duplicating the list-rebuilding logic.
            _tsService.OnChannelListUpdated += UpdateUsers;

            _tsService.OnTalkStatusChanged += (clientId, isTalking) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var user = Users.FirstOrDefault(u => u.ClientId == clientId);
                    if (user != null) user.IsTalking = isTalking;
                });
            };

            _tsService.OnUserPropertiesChanged += (clientId, inMute, outMute, away) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var user = Users.FirstOrDefault(u => u.ClientId == clientId);
                    if (user != null)
                    {
                        if (inMute.HasValue) user.IsInputMuted = inMute.Value;
                        if (outMute.HasValue) user.IsOutputMuted = outMute.Value;
                        if (away.HasValue) user.IsAway = away.Value;
                    }
                });
            };

            // Now points to a dedicated (asynchronous) method that first updates the
            // visual list IMMEDIATELY (optimistic update, via cache) and only then handles the
            // "joined/left" text notification – see HandleClientMoved below.
            _tsService.OnClientMoved += HandleClientMoved;

            Task.Run(async () => await _tsService.StartAsync());
        }

        // Extracted from the OnChannelListUpdated handler – rebuilds the user list
        // (only those in MY room) from the full list received from TS6.
        // Reused by both the standard refresh and the forced SendAuth triggered
        // when a clientMoved event arrives without a resolved name (see HandleClientMoved below).
        private void UpdateUsers(List<User> allUsers, string myChannelId)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // Moved inside Dispatcher.Invoke – previously, this assignment
                // ran on the WebSocket thread, which could conflict with the
                // synchronous Dispatcher.Invoke used in HandleClientMoved to update
                // _currentChannelId when I switch channels.
                _currentChannelId = myChannelId;

                // 暂存说话状态
                var talkingStates = Users.ToDictionary(u => u.ClientId, u => u.IsTalking);

                Users.Clear();

                // [新增] 判断列表是否为空
                if (allUsers.Count == 0)
                {
                    // 如果列表为空，说明连上了 TS6 但没进服务器
                    ConnectionStatus = LanguageHelper.GetString("Lang_Status_Waiting");
                }
                else
                {
                    // 列表有人，清空提示
                    ConnectionStatus = "";

                    var roomUsers = allUsers.Where(u => u.ChannelId == _currentChannelId).OrderBy(u => u.Name);
                    foreach (var u in roomUsers)
                    {
                        if (talkingStates.ContainsKey(u.ClientId)) u.IsTalking = talkingStates[u.ClientId];
                        Users.Add(u);
                    }
                }
            });
        }

        // Important
        // 1) The visual list (Users) would lag for a few moments after a
        //    join/leave event because it only updated once the full SendAuth response arrived.
        //    Now, the list updates IMMEDIATELY (optimistically, using the Ts6Service cache),
        //    and the full refresh that arrives later simply corrects any discrepancies.
        // 2) When I switched channels myself, the list would briefly continue showing
        //    people from the PREVIOUS room. We now detect this (IsMe) and immediately
        //    swap the list for the known members of the new channel.
        // Additionally, name resolution for "joined/left" notifications now attempts
        // several methods before giving up: Ts6Service cache -> current list ->
        // multiple short refresh attempts (instead of a single 600ms wait).
        private async void HandleClientMoved(Ts6Service.ClientMovedEventArgs args)
        {
            try
            {
                // when I'm the one who changed the channel
                if (args.IsMe)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        _currentChannelId = args.NewChannelId;

                        var talkingStates = Users.ToDictionary(u => u.ClientId, u => u.IsTalking);
                        var cachedRoomUsers = _tsService.GetUsersInChannel(args.NewChannelId);

                        Users.Clear();
                        foreach (var u in cachedRoomUsers.OrderBy(u => u.Name))
                        {
                            if (talkingStates.TryGetValue(u.ClientId, out var wasTalking)) u.IsTalking = wasTalking;
                            Users.Add(u);
                        }

                        ConnectionStatus = cachedRoomUsers.Count == 0
                            ? LanguageHelper.GetString("Lang_Status_Waiting")
                            : "";
                    });

                    // It makes no sense to send "X joined/left" notifications for one's own
                    // activity—the full refresh (scheduled in Ts6Service) arrives
                    // immediately afterwards and corrects any cache discrepancies.
                    return;
                }

                // It was someone else: we’re only interested if it involves MY channel
                bool isJoining = args.NewChannelId == _currentChannelId;
                bool isLeaving = args.OldChannelId == _currentChannelId;
                if (!isJoining && !isLeaving) return;

                // Updates the visual list immediately (optimistic) – no longer waits for the
                // SendAuth round-trip for someone to appear/disappear on screen.
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (isJoining)
                    {
                        bool alreadyThere = Users.Any(u => u.ClientId == args.ClientId);
                        if (!alreadyThere && args.CachedUser != null)
                        {
                            var clone = args.CachedUser.Clone();
                            clone.ChannelId = args.NewChannelId;
                            Users.Add(clone);
                            ConnectionStatus = "";
                        }
                        // If CachedUser is null (new connection, never seen before), we
                        // don't yet have a name/avatar to display—the full refresh that has
                        // already been scheduled will resolve this in moments.
                    }
                    else if (isLeaving)
                    {
                        var existing = Users.FirstOrDefault(u => u.ClientId == args.ClientId);
                        if (existing != null) Users.Remove(existing);
                    }
                });

                // Resolves the NAME for the "joined/left" notification, trying every 
                // possible method before giving up and falling back to "Someone"
                string? name = args.CachedUser?.Name
                               ?? System.Windows.Application.Current.Dispatcher.Invoke(() => ResolveUserName(args.ClientId));

                if (name == null)
                {
                    // Last attempt: forces a refresh and makes several short attempts
                    // (instead of a single wait), giving the name a much better chance of arriving
                    // before giving up.
                    _tsService.SendAuth();
                    name = await WaitForNameAsync(args.ClientId);
                }

                string finalName = name ?? "Alguém"; // Só cai aqui em ÚLTIMO caso mesmo.

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (isJoining) ShowNotification($"{finalName} entrou", "#4FCD8E", "📥");
                    else ShowNotification($"{finalName} saiu", "#ED4245", "📤");
                });
            }
            catch (Exception ex)
            {
                LogService.LogError(ex, "MainViewModel.HandleClientMoved");
            }
        }

        // Makes multiple short attempts (instead of a single wait) to check if the
        // client name has already appeared in the list, minimizing instances where it
        // defaults to "Someone" simply because the full refresh took slightly longer than expected.
        private async Task<string?> WaitForNameAsync(int clientId, int maxWaitMs = 1500, int pollMs = 200)
        {
            int waited = 0;
            while (waited < maxWaitMs)
            {
                string? name = System.Windows.Application.Current.Dispatcher.Invoke(() => ResolveUserName(clientId));
                if (name != null) return name;

                await Task.Delay(pollMs);
                waited += pollMs;
            }
            return System.Windows.Application.Current.Dispatcher.Invoke(() => ResolveUserName(clientId));
        }

        private string? ResolveUserName(int clientId) =>
            Users.FirstOrDefault(u => u.ClientId == clientId)?.Name;

        // [新增] 手动刷新方法
        public void RefreshData()
        {
            // 给用户一个瞬间反馈，证明他点到了
            ConnectionStatus = LanguageHelper.GetString("Lang_Status_Refreshing");

            _tsService.SendAuth();
        }

        private async void ShowNotification(string msg, string color, string icon)
        {
            if (!Config.EnableNotifications) return;
            var note = new Notification { Message = msg, Color = color, Icon = icon };
            Notifications.Add(note);
            await Task.Delay(3000);
            if (Notifications.Contains(note)) Notifications.Remove(note);
        }

        // [新增] 固定位置预设 (posições fixas)
        private const double PresetWindowWidth = 300;
        private const double PresetWindowHeight = 600;
        private const double PresetMargin = 20;

        [RelayCommand]
        private void ApplyPositionPreset(string preset)
        {
            double screenW = SystemParameters.WorkArea.Width;
            double screenH = SystemParameters.WorkArea.Height;

            (double x, double y) = preset switch
            {
                "TopLeft" => (PresetMargin, PresetMargin),
                "TopRight" => (screenW - PresetWindowWidth - PresetMargin, PresetMargin),
                "BottomLeft" => (PresetMargin, screenH - PresetWindowHeight - PresetMargin),
                "BottomRight" => (screenW - PresetWindowWidth - PresetMargin, screenH - PresetWindowHeight - PresetMargin),
                "CenterLeft" => (PresetMargin, (screenH - PresetWindowHeight) / 2),
                "CenterRight" => (screenW - PresetWindowWidth - PresetMargin, (screenH - PresetWindowHeight) / 2),
                _ => (Config.WindowLeft, Config.WindowTop)
            };

            Config.WindowLeft = x;
            Config.WindowTop = y;

            // when the position is on the right side, the cards (avatar + name)
            // also stick to the right edge of the window, instead of floating on the left.
            Config.AnchorRight = preset is "TopRight" or "CenterRight" or "BottomRight";
        }

        [RelayCommand]
        private void ToggleSettingsIcon()
        {
            IsSettingsIconVisible = !IsSettingsIconVisible;
        }

        
        [RelayCommand]
        private void RefreshProcessList()
        {
            var options = new List<RunningProcessOption>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.MainWindowHandle == IntPtr.Zero) continue;

                    string title = p.MainWindowTitle;
                    if (string.IsNullOrWhiteSpace(title)) continue;

                    string exeName = p.ProcessName + ".exe";
                    if (!seen.Add(exeName)) continue;

                    options.Add(new RunningProcessOption
                    {
                        DisplayName = $"{title}  ({exeName})",
                        ExeName = exeName,
                        WindowTitle = title
                    });
                }
                catch
                {
                    // ignore processes that we can't access (e.g., system processes)
                }
            }

            options.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));

            AvailableProcesses.Clear();
            foreach (var o in options) AvailableProcesses.Add(o);
        }

        [RelayCommand]
        private void ToggleLockState(Window window)
        {
            IsOverlayLocked = !IsOverlayLocked;

            Config.IsLocked = IsOverlayLocked;

            if (IsOverlayLocked) WindowHelper.EnableClickThrough(window);
            else WindowHelper.DisableClickThrough(window);
        }

        public void SaveConfig() => ConfigService.Save(Config);

        // [Added] Called when the app actually closes (not when "hidden to tray"),
        // to ensure the WebsocketClient stops completely and the process doesn't
        // keep running as a "ghost" after the window disappears.
        public void Shutdown() => _tsService.Stop();
    }
}