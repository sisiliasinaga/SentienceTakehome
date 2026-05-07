using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

#if UNITY_WEBGL && !UNITY_EDITOR
using NativeWebSocket;
#else
using System.Net.WebSockets;
using System.Threading;
#endif

namespace SentienceTakehome.Networking
{
    /// <summary>
    /// Plain WebSocket client for the Node ws backend.
    /// - Uses JsonUtility and the server's PascalCase fields.
    /// - Marshals callbacks to Unity main thread via an internal queue.
    /// </summary>
    public class WsBattleshipClient : MonoBehaviour
    {
        public static WsBattleshipClient Instance { get; private set; }

        [Header("Connection")]
        [Tooltip("Example: ws://localhost:8080/ws")]
        public string serverUrl = "ws://localhost:8080/ws";

        [Tooltip("Connect automatically in Start()")]
        public bool autoConnect = false;

        public bool IsConnected
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return _webglWs != null && _webglWs.State == WebSocketState.Open;
#else
                return _ws != null && _ws.State == WebSocketState.Open;
#endif
            }
        }

        public string RoomId { get; private set; }
        public string RoomCode { get; private set; }
        public string PlayerToken { get; private set; }
        public int? PlayerIndex { get; private set; }
        public bool? IsYourTurn { get; private set; }

        // ---- Events (all invoked on Unity main thread) ----
        public event Action Connected;
        public event Action<string> Disconnected;
        public event Action<WsError> Error;

        public event Action<WsQueued> Queued;
        public event Action<WsRoomCreated> RoomCreated;
        public event Action<WsMatch> Matched;
        public event Action<WsShipPlaced> ShipPlaced;
        public event Action<WsFleetSubmitted> FleetSubmitted;
        public event Action<WsReadyAck> ReadyAck;
        public event Action<WsBattleStart> BattleStart;
        public event Action<WsTurn> Turn;
        public event Action<WsFireResult> FireResult;
        public event Action<WsIncomingFire> IncomingFire;
        public event Action<WsFireRejected> FireRejected;
        public event Action<WsGameOver> GameOver;
        public event Action OpponentDisconnected;
        public event Action OpponentReconnected;
        public event Action<WsResumed> Resumed;
        public event Action<WsGameState> GameState;

#if UNITY_WEBGL && !UNITY_EDITOR
        private NativeWebSocket.WebSocket _webglWs;
#else
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private Task _recvLoop;
#endif
        private readonly ConcurrentQueue<Action> _mainThread = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (autoConnect)
            {
                _ = Connect(serverUrl);
            }
        }

        private void Update()
        {
            // Note: NativeWebSocket's DispatchMessageQueue() is needed on some non-WebGL platforms.
            // On WebGL, messages are already delivered on the main thread, and some package versions
            // don't expose DispatchMessageQueue() on WebGL builds.
            while (_mainThread.TryDequeue(out var a))
            {
                try { a(); }
                catch (Exception) { }
            }
        }

        private void OnDestroy()
        {
            _ = Disconnect("destroy");
        }

        public async Task Connect(string url)
        {
            if (IsConnected)
            {
                return;
            }

            serverUrl = url;
            RoomId = null;
            RoomCode = null;
            PlayerToken = null;
            PlayerIndex = null;
            IsYourTurn = null;

            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (_webglWs != null)
                {
                    await Disconnect("reconnect");
                }

                _webglWs = new NativeWebSocket.WebSocket(serverUrl);

                var tcs = new TaskCompletionSource<bool>();

                _webglWs.OnOpen += () =>
                {
                    EnqueueMain(() => Connected?.Invoke());
                    tcs.TrySetResult(true);
                };
                _webglWs.OnError += (msg) =>
                {
                    EnqueueMain(() => Error?.Invoke(new WsError { Op = "Error", Code = "SocketError", Detail = msg }));
                    tcs.TrySetException(new Exception(msg));
                };
                _webglWs.OnClose += (code) =>
                {
                    EnqueueMain(() => Disconnected?.Invoke($"close:{code}"));
                    tcs.TrySetException(new Exception($"WebSocket closed ({code})"));
                };
                _webglWs.OnMessage += (bytes) =>
                {
                    var text = Encoding.UTF8.GetString(bytes);
                    HandleIncoming(text);
                };

                // Some NativeWebSocket builds can hang awaiting Connect().
                // Kick off connect, then await open/error/close with a timeout.
                _ = _webglWs.Connect();
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(8000));
                if (completed != tcs.Task)
                {
                    throw new Exception("WebSocket connect timed out");
                }
                await tcs.Task;
#else
                _cts?.Cancel();
                _cts = new CancellationTokenSource();

                _ws?.Dispose();
                _ws = new ClientWebSocket();

                await _ws.ConnectAsync(new Uri(serverUrl), _cts.Token);
                EnqueueMain(() => Connected?.Invoke());
                _recvLoop = Task.Run(() => ReceiveLoop(_cts.Token));
#endif
            }
            catch (Exception e)
            {
                EnqueueMain(() => Disconnected?.Invoke(e.Message));
                throw;
            }
        }

        public async Task Disconnect(string reason = "client")
        {
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (_webglWs != null)
                {
                    await _webglWs.Close();
                }
#else
                _cts?.Cancel();

                if (_ws != null && (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.CloseReceived))
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, CancellationToken.None);
                }
#endif
            }
            catch
            {
                // ignore shutdown issues
            }
            finally
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                _webglWs = null;
#else
                _ws?.Dispose();
                _ws = null;
#endif
                EnqueueMain(() => Disconnected?.Invoke(reason));
            }
        }

        // ---- High-level API ----

        public Task JoinQueue() => SendJson(new WsJoin());

        public Task CreateRoom() => SendJson(new WsCreateRoom());

        public Task JoinRoom(string code) => SendJson(new WsJoinRoom { Code = code });

        public Task Resume(string code, string playerToken) =>
            SendJson(new WsResume { Code = code, PlayerToken = playerToken });

        public Task RequestState() => SendJson(new WsGetState());

        public Task SubmitFleet(WsFleetShip[] ships)
        {
            return SendJson(new WsSubmitFleet { Ships = ships });
        }

        public Task PlaceShip(ShipType shipType, Coordinate start, Orientation orientation)
        {
            return SendJson(new WsPlaceShip
            {
                ShipType = shipType.ToString(),
                Row = start.Row,
                Col = start.Col,
                Orientation = orientation.ToString()
            });
        }

        public Task ReadyUp() => SendJson(new WsReady());

        public Task FireAt(Coordinate target)
        {
            return SendJson(new WsFire { Row = target.Row, Col = target.Col });
        }

        // ---- Transport ----

        private async Task SendJson(object payload)
        {
            if (!IsConnected)
            {
                EnqueueMain(() => Error?.Invoke(new WsError { Op = "Error", Code = "NotConnected", Detail = "WebSocket is not connected" }));
                return;
            }

            var json = JsonUtility.ToJson(payload);

            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                await _webglWs.SendText(json);
#else
                var bytes = Encoding.UTF8.GetBytes(json);
                await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
#endif
            }
            catch (Exception e)
            {
                EnqueueMain(() => Error?.Invoke(new WsError { Op = "Error", Code = "SendFailed", Detail = e.Message }));
            }
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private async Task ReceiveLoop(CancellationToken ct)
        {
            var buffer = new byte[64 * 1024];
            var sb = new StringBuilder();

            try
            {
                while (!ct.IsCancellationRequested && _ws != null)
                {
                    sb.Clear();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            EnqueueMain(() => Disconnected?.Invoke("server-close"));
                            return;
                        }

                        sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    } while (!result.EndOfMessage);

                    HandleIncoming(sb.ToString());
                }
            }
            catch (OperationCanceledException)
            {
                // normal
            }
            catch (Exception e)
            {
                EnqueueMain(() => Disconnected?.Invoke(e.Message));
            }
        }
#endif

        private void HandleIncoming(string json)
        {
            WsEnvelope env;
            try
            {
                env = JsonUtility.FromJson<WsEnvelope>(json);
            }
            catch
            {
                EnqueueMain(() => Error?.Invoke(new WsError { Op = "Error", Code = "BadJson", Detail = "Could not parse incoming JSON" }));
                return;
            }

            if (env == null || string.IsNullOrEmpty(env.Op))
            {
                EnqueueMain(() => Error?.Invoke(new WsError { Op = "Error", Code = "BadMessage", Detail = "Missing Op" }));
                return;
            }

            switch (env.Op)
            {
                case "Error":
                    EnqueueMain(() => Error?.Invoke(JsonUtility.FromJson<WsError>(json)));
                    break;
                case "Queued":
                    EnqueueMain(() => Queued?.Invoke(JsonUtility.FromJson<WsQueued>(json)));
                    break;
                case "RoomCreated":
                {
                    var rc = JsonUtility.FromJson<WsRoomCreated>(json);
                    RoomCode = rc.Code;
                    PlayerToken = rc.PlayerToken;
                    EnqueueMain(() => RoomCreated?.Invoke(rc));
                    break;
                }
                case "Match":
                {
                    var m = JsonUtility.FromJson<WsMatch>(json);
                    RoomId = m.RoomId;
                    RoomCode = m.Code;
                    PlayerToken = m.PlayerToken;
                    PlayerIndex = m.PlayerIndex;
                    EnqueueMain(() => Matched?.Invoke(m));
                    break;
                }
                case "ShipPlaced":
                    EnqueueMain(() => ShipPlaced?.Invoke(JsonUtility.FromJson<WsShipPlaced>(json)));
                    break;
                case "FleetSubmitted":
                    EnqueueMain(() => FleetSubmitted?.Invoke(JsonUtility.FromJson<WsFleetSubmitted>(json)));
                    break;
                case "ReadyAck":
                    EnqueueMain(() => ReadyAck?.Invoke(JsonUtility.FromJson<WsReadyAck>(json)));
                    break;
                case "BattleStart":
                    EnqueueMain(() => BattleStart?.Invoke(JsonUtility.FromJson<WsBattleStart>(json)));
                    break;
                case "Turn":
                {
                    var t = JsonUtility.FromJson<WsTurn>(json);
                    IsYourTurn = t.Yours;
                    EnqueueMain(() => Turn?.Invoke(t));
                    break;
                }
                case "FireResult":
                    EnqueueMain(() => FireResult?.Invoke(JsonUtility.FromJson<WsFireResult>(json)));
                    break;
                case "IncomingFire":
                    EnqueueMain(() => IncomingFire?.Invoke(JsonUtility.FromJson<WsIncomingFire>(json)));
                    break;
                case "FireRejected":
                    EnqueueMain(() => FireRejected?.Invoke(JsonUtility.FromJson<WsFireRejected>(json)));
                    break;
                case "GameOver":
                    EnqueueMain(() => GameOver?.Invoke(JsonUtility.FromJson<WsGameOver>(json)));
                    break;
                case "OpponentDisconnected":
                    EnqueueMain(() => OpponentDisconnected?.Invoke());
                    break;
                case "OpponentReconnected":
                    EnqueueMain(() => OpponentReconnected?.Invoke());
                    break;
                case "Resumed":
                {
                    var r = JsonUtility.FromJson<WsResumed>(json);
                    RoomId = r.RoomId;
                    RoomCode = r.Code;
                    PlayerIndex = r.PlayerIndex;
                    EnqueueMain(() => Resumed?.Invoke(r));
                    break;
                }
                case "GameState":
                {
                    var s = JsonUtility.FromJson<WsGameState>(json);
                    RoomId = s.RoomId;
                    RoomCode = s.Code;
                    IsYourTurn = s.CurrentTurnIndex >= 0 ? s.CurrentTurnIndex == s.YourIndex : (bool?)null;
                    EnqueueMain(() => GameState?.Invoke(s));
                    break;
                }
                default:
                    EnqueueMain(() => Error?.Invoke(new WsError { Op = "Error", Code = "UnknownOp", Detail = $"Unknown Op: {env.Op}" }));
                    break;
            }
        }

        private void EnqueueMain(Action a)
        {
            _mainThread.Enqueue(a);
        }
    }
}
