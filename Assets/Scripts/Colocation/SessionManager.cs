#if FUSION_PRESENT || FUSION_WEAVER
using System;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArtsOfEntanglement.Colocation
{
    public class SessionManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static SessionManager Instance { get; private set; }

        [Header("Fusion")]
        [SerializeField] private NetworkRunner runnerPrefab;
        [SerializeField] private NetworkObject sharedAnchorStatePrefab;
        [SerializeField] private NetworkObject avatarPrefab;
        [SerializeField] private bool autoCreateRunner = true;
        [SerializeField] private bool autoStartSingleOnNetworkFailure = true;
        [SerializeField] private string defaultSessionName = "MRRoom_";

        private NetworkRunner runner;
        private SharedAnchorState anchorState;
        private string pendingAnchorId;
        private static readonly ReliableKey AnchorReliableKey = ReliableKey.FromInts(0x414E4348, 0x4F524944, 0x5F4B4559, 0x00000001);

        public event Action<string> StatusMessage;
        public event Action<string> SharedAnchorIdChanged;
        public event Action<string> AutoHostRequested;
        public event Action<string> AutoHostRequested;

        public string DefaultSessionName => defaultSessionName;
        public NetworkRunner Runner => runner;

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

        public async void StartHost(string sessionName)
        {
            await StartSession(GameMode.Host, sessionName, reportFailure: true, allowSingleFallback: true);
        }

        public async void StartClient(string sessionName)
        {
            await StartSession(GameMode.Client, sessionName, reportFailure: true, allowSingleFallback: true);
        }

        public async void StartAuto(string sessionName)
        {
            sessionName = ResolveSessionName(sessionName);

            if (runner != null && runner.IsRunning)
            {
                ReportStatus("Runner already running. Auto start skipped.");
                return;
            }

            ReportStatus($"Auto start: trying Client for '{sessionName}'...");
            var clientResult = await StartSession(GameMode.Client, sessionName, reportFailure: false, allowSingleFallback: false);
            if (clientResult.Ok)
            {
                ReportStatus("Auto start: Client connected.");
                return;
            }

            ReportStatus($"Auto start: Client failed ({clientResult.ShutdownReason}).");

            if (IsHostFallbackReason(clientResult.ShutdownReason))
            {
                ReportStatus("Auto start: switching to Host.");
                if (AutoHostRequested != null)
                {
                    AutoHostRequested.Invoke(sessionName);
                }
                else
                {
                    StartHost(sessionName);
                }

                return;
            }

            if (autoStartSingleOnNetworkFailure && IsNetworkFailureReason(clientResult.ShutdownReason))
            {
                ReportStatus("Auto start: network unavailable. Starting Single mode.");
                await StartSession(GameMode.Single, sessionName, reportFailure: true, allowSingleFallback: false);
            }
        }

        public void ReportStatus(string message)
        {
            Debug.Log($"[SessionManager] {message}");
            StatusMessage?.Invoke(message);
        }

        public void RegisterAnchorState(SharedAnchorState state)
        {
            anchorState = state;
            anchorState.AnchorIdChanged += HandleAnchorIdChanged;

            if (!string.IsNullOrEmpty(pendingAnchorId) && anchorState.HasStateAuthority)
            {
                anchorState.SetAnchorId(pendingAnchorId);
            }
        }

        public void SetSharedAnchorId(string anchorId)
        {
            pendingAnchorId = anchorId;

            if (anchorState != null && anchorState.HasStateAuthority)
            {
                anchorState.SetAnchorId(anchorId);
            }
            else
            {
                SharedAnchorIdChanged?.Invoke(anchorId);
            }

            if (runner != null && runner.IsServer)
            {
                BroadcastAnchorId(anchorId);
            }
        }

        private void HandleAnchorIdChanged(string anchorId)
        {
            SharedAnchorIdChanged?.Invoke(anchorId);
        }

        private NetworkRunner EnsureRunner()
        {
            if (runner != null)
            {
                return runner;
            }

            runner = FindObjectOfType<NetworkRunner>();
            if (runner == null && autoCreateRunner)
            {
                if (runnerPrefab != null)
                {
                    runner = Instantiate(runnerPrefab);
                }
                else
                {
                    var runnerGo = new GameObject("NetworkRunner");
                    runner = runnerGo.AddComponent<NetworkRunner>();
                }
            }

            if (runner != null)
            {
                runner.ProvideInput = true;
                DontDestroyOnLoad(runner.gameObject);
            }

            return runner;
        }

        private async Task<StartGameResult> StartSession(GameMode mode, string sessionName, bool reportFailure, bool allowSingleFallback)
        {
            runner = EnsureRunner();
            if (runner == null)
            {
                ReportStatus("NetworkRunner missing.");
                return StartGameResult.BuildGameResultFromException(new InvalidOperationException("NetworkRunner missing."));
            }

            runner.AddCallbacks(this);

            var sceneManager = runner.GetComponent<INetworkSceneManager>();
            if (sceneManager == null)
            {
                sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            }

            var args = new StartGameArgs
            {
                GameMode = mode,
                SessionName = sessionName,
                Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
                SceneManager = sceneManager
            };

            ReportStatus($"Starting {mode} session '{sessionName}'...");
            var result = await runner.StartGame(args);
            if (!result.Ok)
            {
                if (reportFailure)
                {
                    ReportStatus($"StartGame failed: {result.ShutdownReason}");
                }

                if (allowSingleFallback && autoStartSingleOnNetworkFailure && mode != GameMode.Single &&
                    IsNetworkFailureReason(result.ShutdownReason))
                {
                    ReportStatus("Network unavailable. Starting Single mode.");
                    return await StartSession(GameMode.Single, sessionName, reportFailure: true, allowSingleFallback: false);
                }
            }

            return result;
        }

        private void EnsureAnchorStateSpawned(NetworkRunner runnerRef)
        {
            if (anchorState != null)
            {
                return;
            }

            if (sharedAnchorStatePrefab != null)
            {
                runnerRef.Spawn(sharedAnchorStatePrefab, Vector3.zero, Quaternion.identity, runnerRef.LocalPlayer);
            }
            else
            {
                ReportStatus("SharedAnchorState prefab not set. Add one or place a scene object.");
            }
        }

        private void SpawnAvatar(NetworkRunner runnerRef, PlayerRef player)
        {
            if (avatarPrefab == null)
            {
                ReportStatus("Avatar prefab not set.");
                return;
            }

            runnerRef.Spawn(avatarPrefab, Vector3.zero, Quaternion.identity, player);
        }

        public void OnPlayerJoined(NetworkRunner runnerRef, PlayerRef player)
        {
            ReportStatus($"Player joined: {player.PlayerId}");
            if (runnerRef.IsServer)
            {
                EnsureAnchorStateSpawned(runnerRef);
                SpawnAvatar(runnerRef, player);

                if (!string.IsNullOrEmpty(pendingAnchorId))
                {
                    SendAnchorIdToPlayer(runnerRef, player, pendingAnchorId);
                }
            }
        }

        public void OnPlayerLeft(NetworkRunner runnerRef, PlayerRef player)
        {
            ReportStatus($"Player left: {player.PlayerId}");
        }

        public void OnInput(NetworkRunner runnerRef, NetworkInput input)
        {
        }

        public void OnInputMissing(NetworkRunner runnerRef, PlayerRef player, NetworkInput input)
        {
        }

        public void OnShutdown(NetworkRunner runnerRef, ShutdownReason shutdownReason)
        {
            ReportStatus($"Shutdown: {shutdownReason}");
        }

        public void OnConnectedToServer(NetworkRunner runnerRef)
        {
            ReportStatus("Connected to server.");
        }

        public void OnDisconnectedFromServer(NetworkRunner runnerRef, NetDisconnectReason reason)
        {
            ReportStatus($"Disconnected from server: {reason}");
        }

        public void OnConnectRequest(NetworkRunner runnerRef, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            request.Accept();
        }

        public void OnConnectFailed(NetworkRunner runnerRef, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            ReportStatus($"Connect failed: {reason}");
        }

        public void OnUserSimulationMessage(NetworkRunner runnerRef, SimulationMessagePtr message)
        {
        }

        public void OnSessionListUpdated(NetworkRunner runnerRef, System.Collections.Generic.List<SessionInfo> sessionList)
        {
        }

        public void OnCustomAuthenticationResponse(NetworkRunner runnerRef, System.Collections.Generic.Dictionary<string, object> data)
        {
        }

        public void OnHostMigration(NetworkRunner runnerRef, HostMigrationToken hostMigrationToken)
        {
        }

        public void OnReliableDataReceived(NetworkRunner runnerRef, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
        {
            if (key != AnchorReliableKey)
            {
                return;
            }

            if (runnerRef.IsServer)
            {
                return;
            }

            if (data.Array == null || data.Count == 0)
            {
                return;
            }

            string anchorId = Encoding.UTF8.GetString(data.Array, data.Offset, data.Count);
            ApplyAnchorIdFromNetwork(anchorId);
        }

        public void OnReliableDataProgress(NetworkRunner runnerRef, PlayerRef player, ReliableKey key, float progress)
        {
        }

        public void OnSceneLoadDone(NetworkRunner runnerRef)
        {
        }

        public void OnSceneLoadStart(NetworkRunner runnerRef)
        {
        }

        public void OnObjectEnterAOI(NetworkRunner runnerRef, NetworkObject obj, PlayerRef player)
        {
        }

        public void OnObjectExitAOI(NetworkRunner runnerRef, NetworkObject obj, PlayerRef player)
        {
        }

        private static bool IsHostFallbackReason(ShutdownReason reason)
        {
            return reason == ShutdownReason.GameNotFound || reason == ShutdownReason.GameClosed;
        }

        private static bool IsNetworkFailureReason(ShutdownReason reason)
        {
            switch (reason)
            {
                case ShutdownReason.Error:
                case ShutdownReason.InvalidRegion:
                case ShutdownReason.InvalidAuthentication:
                case ShutdownReason.CustomAuthenticationFailed:
                case ShutdownReason.AuthenticationTicketExpired:
                case ShutdownReason.PhotonCloudTimeout:
                case ShutdownReason.ConnectionTimeout:
                case ShutdownReason.ConnectionRefused:
                case ShutdownReason.OperationTimeout:
                case ShutdownReason.OperationCanceled:
                case ShutdownReason.MaxCcuReached:
                    return true;
                default:
                    return false;
            }
        }

        private string ResolveSessionName(string sessionName)
        {
            if (!string.IsNullOrWhiteSpace(sessionName))
            {
                return sessionName.Trim();
            }

            return defaultSessionName;
        }

        private void ApplyAnchorIdFromNetwork(string anchorId)
        {
            if (string.IsNullOrWhiteSpace(anchorId))
            {
                return;
            }

            pendingAnchorId = anchorId;
            SharedAnchorIdChanged?.Invoke(anchorId);
        }

        private void BroadcastAnchorId(string anchorId)
        {
            if (runner == null || string.IsNullOrWhiteSpace(anchorId))
            {
                return;
            }

            byte[] data = Encoding.UTF8.GetBytes(anchorId);
            foreach (PlayerRef player in runner.ActivePlayers)
            {
                SendAnchorIdToPlayer(runner, player, anchorId, data);
            }
        }

        private static void SendAnchorIdToPlayer(NetworkRunner runnerRef, PlayerRef player, string anchorId, byte[] cachedData = null)
        {
            if (runnerRef == null || string.IsNullOrWhiteSpace(anchorId))
            {
                return;
            }

            byte[] payload = cachedData ?? Encoding.UTF8.GetBytes(anchorId);
            runnerRef.SendReliableDataToPlayer(player, AnchorReliableKey, payload);
        }
    }
}
#else
using System;
using UnityEngine;

namespace ArtsOfEntanglement.Colocation
{
    public class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }

        [SerializeField] private string defaultSessionName = "MRRoom_";

        public event Action<string> StatusMessage;
        public event Action<string> SharedAnchorIdChanged;

        public string DefaultSessionName => defaultSessionName;
        public object Runner => null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void StartHost(string sessionName)
        {
            ReportMissing("StartHost");
        }

        public void StartClient(string sessionName)
        {
            ReportMissing("StartClient");
        }

        public void StartAuto(string sessionName)
        {
            ReportMissing("StartAuto");
        }

        public void SetSharedAnchorId(string anchorId)
        {
            SharedAnchorIdChanged?.Invoke(anchorId);
        }

        public void RegisterAnchorState(SharedAnchorState state)
        {
        }

        private void ReportMissing(string call)
        {
            var message = $"Fusion not installed. '{call}' ignored.";
            Debug.LogWarning($"[SessionManager] {message}");
            StatusMessage?.Invoke(message);
        }
    }
}
#endif
