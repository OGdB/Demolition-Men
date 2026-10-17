using System;
using System.Collections.Generic;
using System.Text;
using Demolition.Levels;
using Demolition.Run;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Demolition.Online
{
    /// <summary>
    /// One online co-op session (docs/multiplayer.md §5, §6): a NetworkManager on a persistent
    /// object, NGO over the relay (<see cref="RelayTransport"/>), host or join by room code.
    /// The host's level choices load on both devices (NGO scene management, after a start
    /// message so the client builds the same level); the host network-spawns both players
    /// once the client has loaded the run. Player 2 leaving mid-run lets the host play on
    /// alone; any other end of the session goes back to the menu with the reason
    /// (<see cref="TakeEndReason"/>).
    /// </summary>
    public sealed class OnlineSession : MonoBehaviour
    {
        private const int TickRate = 20;
        private const string StartMessage = "DM.Start";

        public static OnlineSession Current { get; private set; }

        /// <summary>Host: the room code to show.</summary>
        public static event Action<string> CodeReady;
        /// <summary>The other player is in (host: they joined; client: the host let us in).</summary>
        public static event Action Connected;
        /// <summary>The session ended, with the reason to show.</summary>
        public static event Action<string> Ended;

        private static string _endReason;

        public bool IsHost => _host;
        public bool IsConnected { get; private set; }
        public string Code => _transport.Code;
        /// <summary>Host: the client's NGO id.</summary>
        public ulong Peer { get; private set; }

        private NetworkManager _net;
        private RelayTransport _transport;
        private bool _host;
        private Vector3? _spawn;
        private readonly List<NetworkObject> _players = new List<NetworkObject>();
        private bool _clientLoaded;
        private bool _ending;

        public static void Host() => Begin(true, null);
        public static void Join(string code) => Begin(false, code);
        public static void Leave() => Current?.End(null);

        /// <summary>Why the last session ended, once (the menu shows it).</summary>
        public static string TakeEndReason()
        {
            string reason = _endReason;
            _endReason = null;
            return reason;
        }

        private static void Begin(bool host, string code)
        {
            Leave();
            var go = new GameObject("OnlineSession");
            go.SetActive(false); // configure everything before NetworkManager's Awake
            var session = go.AddComponent<OnlineSession>();
            session._host = host;
            session._transport = go.AddComponent<RelayTransport>();
            session._transport.Url = GameConfig.Instance.relayUrl;
            session._transport.JoinCode = code;
            session._net = go.AddComponent<NetworkManager>();
            NetworkConfig config = session._net.NetworkConfig ??= new NetworkConfig();
            config.NetworkTransport = session._transport;
            config.TickRate = TickRate;
            config.ConnectionApproval = true;
            config.ConnectionData = Encoding.UTF8.GetBytes(Application.version);
            config.EnableSceneManagement = true;
            config.PlayerPrefab = null;
            go.SetActive(true);
            session._net.SetSingleton();
            foreach (GameObject prefab in GameConfig.Instance.netPlayerPrefabs)
                if (prefab != null)
                    session._net.AddNetworkPrefab(prefab);
            Current = session;
            session.Run();
        }

        private void Run()
        {
            NetRole.Mode = _host ? NetMode.Host : NetMode.Client;
            NetRole.LoadScene = LoadScene;
            NetRole.SpawnPlayers = SpawnPlayers;
            NetRole.Leave = Leave;
            NetRole.LevelBuilt = RunNet.Create;

            _transport.CodeReady += code => CodeReady?.Invoke(code);
            _net.ConnectionApprovalCallback = Approve;
            _net.OnClientConnectedCallback += OnClientConnected;
            _net.OnClientDisconnectCallback += OnClientDisconnected;
            _net.OnServerStopped += _ => End(Reason());
            _net.OnClientStopped += _ => End(Reason());

            if (!(_host ? _net.StartHost() : _net.StartClient()))
            {
                End("COULDN'T START ONLINE PLAY");
                return;
            }
            _net.CustomMessagingManager.RegisterNamedMessageHandler(StartMessage, OnStart);
            if (_host)
                _net.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
        }

        // ---------------- Connection ----------------

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool self = request.ClientNetworkId == NetworkManager.ServerClientId;
            bool sameVersion = request.Payload != null && Encoding.UTF8.GetString(request.Payload) == Application.version;
            response.Approved = self || (sameVersion && !IsConnected);
            response.CreatePlayerObject = false;
            response.Reason = !sameVersion ? "THE OTHER DEVICE RUNS ANOTHER VERSION OF THE GAME" : "THE ROOM IS FULL";
        }

        private void OnClientConnected(ulong id)
        {
            if (_host ? id == NetworkManager.ServerClientId : id != _net.LocalClientId)
                return;
            Peer = _host ? id : NetworkManager.ServerClientId;
            IsConnected = true;
            Connected?.Invoke();
        }

        private void OnClientDisconnected(ulong id)
        {
            if (!_host)
            {
                End(Reason());
                return;
            }
            if (!IsConnected || id != Peer)
                return; // the host itself, or a joiner it turned away (another version)
            RunController run = RunController.Current;
            if (run == null || !run.enabled)
            {
                End("PLAYER 2 LEFT");
                return;
            }
            // Mid-run the host plays on alone: keep both players (shutting down would destroy
            // the network's objects), end the session and take player 2 out of the run.
            foreach (NetworkObject player in _players)
                if (player != null && player.IsSpawned)
                    player.Despawn(destroy: false);
            End(null, playOn: true);
            run.PlayerLeft(1);
        }

        private string Reason()
        {
            if (!string.IsNullOrEmpty(_net.DisconnectReason))
                return _net.DisconnectReason;
            switch (_transport.CloseCode)
            {
                case 4001: return "NO ROOM WITH THAT CODE";
                case 4002: return "THAT ROOM IS FULL";
                case 4003: return "UPDATE THE GAME: THE SERVER SPEAKS A NEWER VERSION";
                case 4004: return "THE SERVER IS FULL: TRY AGAIN LATER";
                case 4005: return "THE HOST LEFT";
                case 4010: return "THE ROOM TIMED OUT";
                case 4029: return "TOO MANY TRIES: WAIT A MINUTE";
                default: return IsConnected ? "CONNECTION LOST" : "CAN'T REACH THE SERVER";
            }
        }

        /// <summary>Stop the session. <paramref name="reason"/> null: this device left on purpose
        /// (whoever asked takes it where it goes next), otherwise back to the menu to show why.</summary>
        /// <param name="playOn">The host keeps playing the run alone.</param>
        public void End(string reason, bool playOn = false)
        {
            if (_ending)
                return;
            _ending = true;
            RunNet.Stop();
            if (RunController.Current != null && !playOn)
                RunController.Current.enabled = false; // its network players are about to go
            NetRole.Mode = NetMode.Offline;
            NetRole.LoadScene = null;
            NetRole.SpawnPlayers = null;
            NetRole.Leave = null;
            NetRole.LevelBuilt = null;
            if (_net != null && _net.IsListening)
                _net.Shutdown();
            if (Current == this)
                Current = null;
            Destroy(gameObject);

            _endReason = reason;
            if (reason == null)
                return;
            Ended?.Invoke(reason);
            // Mid-run or in the online menu: back to this device's own menu and level select.
            GameConfig config = GameConfig.Instance;
            if (SceneManager.GetActiveScene().name != config.menuScene)
            {
                LevelSelection.ReturnToLevelSelect = true;
                RunController.LoadScene(config.menuScene);
            }
        }

        // ---------------- Levels ----------------

        // Host: every scene change goes to both devices; a run first tells the client which level.
        private bool LoadScene(string scene)
        {
            if (!_host || !IsConnected)
                return false;
            GameConfig config = GameConfig.Instance;
            if (scene == config.tutorialScene)
                return false;
            if (scene == config.runScene)
                SendStart();
            _clientLoaded = false;
            _spawn = null;
            _players.Clear();
            SceneEventProgressStatus status = _net.SceneManager.LoadScene(scene, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
                Debug.LogWarning($"Online: loading {scene} didn't start ({status}).");
            return true;
        }

        private void SendStart()
        {
            using var writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe(LevelSelection.CampaignIndex);
            writer.WriteValueSafe(LevelSelection.DailyDate.Ticks);
            // Same delivery as NGO's scene events, so it arrives before the load.
            _net.CustomMessagingManager.SendNamedMessage(StartMessage, Peer, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        private void OnStart(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out int campaignIndex);
            reader.ReadValueSafe(out long dailyTicks);
            if (campaignIndex >= 0)
                LevelSelection.Select(campaignIndex);
            else
                LevelSelection.SelectDaily(new DateTime(dailyTicks));
            LevelSelection.ReturnToLevelSelect = true;
        }

        // ---------------- Players ----------------

        private int SpawnPlayers(Vector3 spawn)
        {
            _spawn = spawn;
            TrySpawnPlayers();
            return 2;
        }

        private void OnLoadEventCompleted(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (sceneName != GameConfig.Instance.runScene)
                return;
            if (timedOut.Count > 0)
            {
                End("PLAYER 2 COULDN'T LOAD THE LEVEL");
                return;
            }
            _clientLoaded = true;
            TrySpawnPlayers();
        }

        // Host: both players once the client has loaded the run (spawning earlier would race its load).
        private void TrySpawnPlayers()
        {
            if (!_host || !_clientLoaded || _spawn == null)
                return;
            GameObject[] prefabs = GameConfig.Instance.netPlayerPrefabs;
            if (prefabs.Length < 2 || prefabs[0] == null || prefabs[1] == null)
            {
                End("NETWORK PLAYERS MISSING: RUN TOOLS > DEMOLITION > ONLINE > BUILD NETWORK PREFABS");
                return;
            }
            for (int i = 0; i < 2; i++)
            {
                GameObject player = Instantiate(prefabs[i], _spawn.Value + new Vector3(-1.5f * i, 0f, 0f), Quaternion.identity);
                var networkObject = player.GetComponent<NetworkObject>();
                networkObject.DontDestroyWithOwner = true; // player 2 leaving mid-run: the host still needs it (OnClientDisconnected)
                networkObject.SpawnWithOwnership(i == 0 ? NetworkManager.ServerClientId : Peer, destroyWithScene: true);
                _players.Add(networkObject);
            }
            _spawn = null;
        }
    }
}
