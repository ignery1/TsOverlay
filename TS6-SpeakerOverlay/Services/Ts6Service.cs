using System.IO;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reactive.Linq;
using Websocket.Client;
using TS6_SpeakerOverlay.Models;

namespace TS6_SpeakerOverlay.Services
{
    public class Ts6Service
    {
        private const string URL = "ws://127.0.0.1:5899";

        // --- [核心修改] 定义 AppData 存储路径 ---
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TS6-SpeakerOverlay"
        );
        private static readonly string KEY_FILE = Path.Combine(AppDataFolder, "apikey.txt");
        // --------------------------------------

        private WebsocketClient _client;
        private string _savedApiKey = "";

        // Cache of ALL previously seen clients (any channel), used to
        // resolve the names of those who joined/left before the next full refresh arrives,
        // and also to build the room list on the fly when I switch channels.
        private readonly Dictionary<int, User> _knownClients = new();
        private readonly object _knownClientsLock = new();

        // My own clientId (from the current TS6 connection), used to determine if a
        // clientMoved event concerns ME switching channels (in which case the UI should
        // update the list immediately using the cache, rather than waiting for the round-trip).
        private volatile int _myClientId = 0;

        // Debounce for the refresh triggered by clientMoved (bursts of entries/exits
        // trigger only one SendAuth at the end, but ALWAYS trigger it)
        private CancellationTokenSource? _moveRefreshCts;

        // Details of a channel move, passed to the consumer of the
        // OnClientMoved event. CachedUser comes from our internal cache (may be null if
        // this is the first time we've seen this client); IsMe indicates whether *we*
        // moved, not someone else.
        public class ClientMovedEventArgs
        {
            public int ClientId { get; init; }
            public string NewChannelId { get; init; } = "";
            public string OldChannelId { get; init; } = "";
            public User? CachedUser { get; init; }
            public bool IsMe { get; init; }
        }

        public event Action<List<User>, string>? OnChannelListUpdated;
        public event Action<int, bool>? OnTalkStatusChanged;
        public event Action<int, bool?, bool?, bool?>? OnUserPropertiesChanged;
        public event Action<ClientMovedEventArgs>? OnClientMoved;
        public event Action<bool>? OnConnectionStateChanged;

        public Ts6Service()
        {
            LoadApiKey();
            var factory = new Func<ClientWebSocket>(() => new ClientWebSocket());
            _client = new WebsocketClient(new Uri(URL), factory);

            _client.ReconnectTimeout = TimeSpan.FromSeconds(5);
            _client.ErrorReconnectTimeout = TimeSpan.FromSeconds(5);

            _client.ReconnectionHappened.Subscribe(info =>
            {
                OnConnectionStateChanged?.Invoke(true);
                SendAuth();
            });

            _client.DisconnectionHappened.Subscribe(info =>
            {
                OnConnectionStateChanged?.Invoke(false);
            });

            _client.MessageReceived.Subscribe(msg => HandleMessage(msg.Text));
        }

        public async Task StartAsync() => await _client.Start();

        // Properly terminates the connection and reconnection loop of the WebsocketClient.
        // Without this, upon closing the window, the process could remain active in the background
        // (automatically reconnecting), preventing the .exe from being located or deleted later.
        public void Stop()
        {
            try
            {
                _moveRefreshCts?.Cancel();
                _client.ReconnectTimeout = null; // evita novo agendamento de reconexão
                _client.IsReconnectionEnabled = false;
                _client.Dispose();
            }
            catch
            {
                // Já pode estar desconectado/disposed; ignora.
            }
        }

        private void LoadApiKey()
        {
            // 确保 AppData 文件夹存在
            if (!Directory.Exists(AppDataFolder)) Directory.CreateDirectory(AppDataFolder);

            if (File.Exists(KEY_FILE))
            {
                _savedApiKey = File.ReadAllText(KEY_FILE).Trim();
            }
        }

        public void SendAuth()
        {
            var auth = new AuthRequest();
            if (!string.IsNullOrEmpty(_savedApiKey)) auth.Payload.Content.ApiKey = _savedApiKey;
            _client.Send(JsonSerializer.Serialize(auth));
        }

        private void HandleMessage(string? json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                var node = JsonNode.Parse(json);
                string? type = node?["type"]?.ToString();

                switch (type)
                {
                    case "auth": HandleAuthResponse(node); break;
                    case "talkStatusChanged": HandleTalkStatus(node); break;
                    case "clientPropertiesUpdated": HandlePropertiesUpdated(node); break;
                    case "clientMoved": HandleClientMoved(node); break;
                }
            }
            catch (Exception ex)
            {
                // [新增] 记录到底为什么崩溃，以及导致崩溃的 JSON 是什么
                LogService.LogError(ex, "HandleMessage");
                LogService.Log($"Raw JSON: {json}");
            }
        }

        private void HandleAuthResponse(JsonNode? node)
        {
            var payload = node?["payload"];
            if (payload == null) return;

            var newKey = payload["apiKey"]?.ToString();
            if (!string.IsNullOrEmpty(newKey) && newKey != _savedApiKey)
            {
                _savedApiKey = newKey;
                // 确保文件夹存在后再保存
                if (!Directory.Exists(AppDataFolder)) Directory.CreateDirectory(AppDataFolder);
                File.WriteAllText(KEY_FILE, newKey);
            }

            var connections = payload["connections"]?.AsArray();
            if (connections == null || connections.Count == 0)
            {
                OnChannelListUpdated?.Invoke(new List<User>(), "");
                return;
            }

            var conn = connections.FirstOrDefault();
            if (conn == null) return;

            int myClientId = conn["clientId"]?.GetValue<int>() ?? 0;
            _myClientId = myClientId;
            var clientInfos = conn["clientInfos"]?.AsArray();

            if (clientInfos == null)
            {
                OnChannelListUpdated?.Invoke(new List<User>(), "");
                return;
            }

            var allUsers = new List<User>();
            string myChannelId = "";

            foreach (var client in clientInfos)
            {
                int id = client["id"]?.GetValue<int>() ?? 0;
                var props = client["properties"];
                string chId = client["channelId"]?.ToString() ?? "";

                if (id == myClientId) myChannelId = chId;

                string avatarRaw = props?["myteamspeakAvatar"]?.ToString() ?? "";
                string avatarUrl = "";
                if (!string.IsNullOrEmpty(avatarRaw) && avatarRaw.Contains(','))
                {
                    var parts = avatarRaw.Split(',');
                    if (parts.Length > 1) avatarUrl = parts[1];
                }

                allUsers.Add(new User
                {
                    ClientId = id,
                    Name = props?["nickname"]?.ToString() ?? "Unknown",
                    ChannelId = chId,
                    AvatarUrl = avatarUrl,
                    IsTalking = props?["flagTalking"]?.GetValue<bool>() ?? false,
                    IsInputMuted = props?["inputMuted"]?.GetValue<bool>() ?? false,
                    IsOutputMuted = props?["outputMuted"]?.GetValue<bool>() ?? false,
                    IsAway = props?["away"]?.GetValue<bool>() ?? false
                });
            }

            // Updates the cache with ALL observed clients (not just those on my channel),
            // so that when someone joins or leaves, we can retrieve their name immediately.
            lock (_knownClientsLock)
            {
                foreach (var u in allUsers)
                {
                    _knownClients[u.ClientId] = u;
                }
            }

            OnChannelListUpdated?.Invoke(allUsers, myChannelId);
        }

        // Returns a copy (independent of the cache) of all known clients
        // currently in a specific channel. Used to build the room list
        // IMMEDIATELY when I switch channels, without waiting for the SendAuth round-trip.
        public List<User> GetUsersInChannel(string channelId)
        {
            lock (_knownClientsLock)
            {
                return _knownClients.Values
                    .Where(u => u.ChannelId == channelId)
                    .Select(u => u.Clone())
                    .ToList();
            }
        }

        private void HandleClientMoved(JsonNode? node)
        {
            var payload = node?["payload"];
            if (payload == null) return;

            int clientId = payload["clientId"]?.GetValue<int>() ?? 0;
            string newCh = payload["newChannelId"]?.ToString() ?? "";
            string oldCh = payload["oldChannelId"]?.ToString() ?? "";

            // Attempts to resolve the client using the known connections cache.
            // If not found (a new client that hasn't appeared in any refresh yet),
            // the event consumer receives null and uses its own fallback.
            // updates this client's ChannelId in the cache IMMEDIATELY,
            // right here, instead of waiting for the next full refresh (SendAuth) to arrive.
            User? cachedUser;
            lock (_knownClientsLock)
            {
                if (_knownClients.TryGetValue(clientId, out cachedUser))
                {
                    cachedUser.ChannelId = newCh;
                }
            }

            // When we see this client for the FIRST time (empty cache), the TS6 API itself sends
            // a "properties" block (nickname, avatar, etc.) along with the `clientMoved` event.
            if (cachedUser == null)
            {
                var props = payload["properties"];
                string? nickname = props?["nickname"]?.ToString();
                if (!string.IsNullOrEmpty(nickname))
                {
                    string avatarRaw = props?["myteamspeakAvatar"]?.ToString() ?? "";
                    string avatarUrl = "";
                    if (!string.IsNullOrEmpty(avatarRaw) && avatarRaw.Contains(','))
                    {
                        var parts = avatarRaw.Split(',');
                        if (parts.Length > 1) avatarUrl = parts[1];
                    }

                    cachedUser = new User
                    {
                        ClientId = clientId,
                        Name = nickname,
                        ChannelId = newCh,
                        AvatarUrl = avatarUrl,
                        IsTalking = props?["flagTalking"]?.GetValue<bool>() ?? false,
                        IsInputMuted = props?["inputMuted"]?.GetValue<bool>() ?? false,
                        IsOutputMuted = props?["outputMuted"]?.GetValue<bool>() ?? false,
                        IsAway = props?["away"]?.GetValue<bool>() ?? false
                    };

                    // Store it in the cache as well—that way, if that same client leaves right after, the next 'clientMoved' event will find them immediately.
                    lock (_knownClientsLock)
                    {
                        _knownClients[clientId] = cachedUser;
                    }
                }
            }

            var args = new ClientMovedEventArgs
            {
                ClientId = clientId,
                NewChannelId = newCh,
                OldChannelId = oldCh,
                CachedUser = cachedUser,
                IsMe = clientId != 0 && clientId == _myClientId
            };

            OnClientMoved?.Invoke(args);

            // Sempre agenda uma atualização da sala após qualquer entrada/saída.
            ScheduleRoomRefresh();
        }

        // Debounce with a ceiling: groups multiple closely spaced movement events into
        // a single SendAuth, but if the events keep coming (an excessively long burst),
        // it forces a refresh anyway after MaxBurstWaitMs—preventing the full
        // refresh from being "starved" indefinitely (the ChannelId fix above handles the
        // common case, but this acts as a safety net for everything else that only a
        // full refresh resolves: name, avatar, mute status, etc.).
        private DateTime? _burstStartedAt;
        private const int DebounceMs = 250;
        private const int MaxBurstWaitMs = 1000;

        private void ScheduleRoomRefresh()
        {
            var now = DateTime.UtcNow;
            _burstStartedAt ??= now;

            if ((now - _burstStartedAt.Value).TotalMilliseconds >= MaxBurstWaitMs)
            {
                _moveRefreshCts?.Cancel();
                _burstStartedAt = null;
                SendAuth();
                return;
            }

            _moveRefreshCts?.Cancel();
            var cts = new CancellationTokenSource();
            _moveRefreshCts = cts;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(DebounceMs, cts.Token);
                    if (!cts.Token.IsCancellationRequested)
                    {
                        _burstStartedAt = null;
                        SendAuth();
                    }
                }
                catch (TaskCanceledException)
                {
                    // ignora substituído por um evento mais recente.
                }
            }, cts.Token);
        }

        private void HandleTalkStatus(JsonNode? node)
        {
            var payload = node?["payload"];
            int clientId = payload?["clientId"]?.GetValue<int>() ?? 0;
            int status = payload?["status"]?.GetValue<int>() ?? 0;
            OnTalkStatusChanged?.Invoke(clientId, status == 1);
        }

        private void HandlePropertiesUpdated(JsonNode? node)
        {
            var payload = node?["payload"];
            int clientId = payload?["clientId"]?.GetValue<int>() ?? 0;
            var props = payload?["properties"];

            if (props != null && clientId != 0)
            {
                bool? inputMuted = null;
                if (props["inputMuted"] != null) inputMuted = props["inputMuted"].GetValue<bool>();
                bool? outputMuted = null;
                if (props["outputMuted"] != null) outputMuted = props["outputMuted"].GetValue<bool>();
                bool? away = null;
                if (props["away"] != null) away = props["away"].GetValue<bool>();

                if (inputMuted.HasValue || outputMuted.HasValue || away.HasValue)
                {
                    OnUserPropertiesChanged?.Invoke(clientId, inputMuted, outputMuted, away);
                }
            }
        }
    }
}