using System.Collections.Generic;
using Demolition.Levels;
using Demolition.Run;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Demolition.Online
{
    /// <summary>
    /// The run's online sync (docs/multiplayer.md §2–§4), one per Level scene, as NGO named
    /// messages. The host decides the standing structure, pickups, run state and score; a
    /// client sends what its own player does to them and mirrors the rest:
    /// <list type="bullet">
    /// <item>Damage: the client's hits on standing blocks go to the host (<c>DM.Damage</c>);
    /// the host sends every changed cell once per tick (<c>DM.Cells</c>), then the points it
    /// awarded for the score popups (<c>DM.Score</c>). Detaching follows on each device from the
    /// same cells, debris is local. Cells of a building the client hasn't streamed in yet wait
    /// until it has.</item>
    /// <item>Pickups: the client claims (<c>DM.Claim</c>), the host grants first come (<c>DM.Taken</c>).</item>
    /// <item>Dynamite: the thrower's device sends the throw and the blast (<c>DM.Thrown</c>,
    /// <c>DM.Blast</c>); the other flies a harmless copy.</item>
    /// <item>Run: the host's run state, score, demolition, pause and level hash at 10 Hz (<c>DM.State</c>),
    /// once the client says its Level scene is up (<c>DM.Ready</c>); in development builds a
    /// standing-cells hash per building every 5 s (<c>DM.Check</c>).</item>
    /// </list>
    /// </summary>
    public sealed class RunNet : MonoBehaviour
    {
        private const string Ready = "DM.Ready", Cells = "DM.Cells", Damage = "DM.Damage", Claim = "DM.Claim",
            Taken = "DM.Taken", Thrown = "DM.Thrown", Blast = "DM.Blast", State = "DM.State", Check = "DM.Check",
            Score = "DM.Score";
        private static readonly string[] Names = { Ready, Cells, Damage, Claim, Taken, Thrown, Blast, State, Check, Score };
        private const NetworkDelivery Reliable = NetworkDelivery.ReliableFragmentedSequenced;
        private const float CellsEvery = 0.05f, StateEvery = 0.1f, CheckEvery = 5f;
        private const float MaxDamage = 1000f; // one request can't flatten a building

        private static RunNet _current;

        private NetworkManager _net;
        private LevelLayout _layout;
        private bool _host;
        private ulong _peer;
        private int _layoutHash;
        private bool _clientReady;
        private bool _hashChecked;
        private float _nextCells, _nextState, _nextCheck;
        private readonly List<(int building, int cell, byte health)> _changed = new List<(int, int, byte)>();
        private readonly List<(int player, int points, Vector2 at)> _scored = new List<(int, int, Vector2)>();
        private RunController _run;
        // Client: the latest health of cells in buildings it hasn't streamed in yet (its camera
        // trails the host's a little), applied once they are.
        private readonly Dictionary<int, Dictionary<int, byte>> _pending = new Dictionary<int, Dictionary<int, byte>>();
        private readonly List<int> _arrived = new List<int>();
        private readonly HashSet<int> _reportedDesync = new HashSet<int>();
        private readonly Dictionary<Dynamite, int> _ownSticks = new Dictionary<Dynamite, int>();
        private readonly Dictionary<int, Dynamite> _copies = new Dictionary<int, Dynamite>();
        private int _nextStick;

        /// <summary><see cref="NetRole.LevelBuilt"/>: the Level scene built its level.</summary>
        public static void Create(LevelLayout layout)
        {
            OnlineSession session = OnlineSession.Current;
            if (session == null || NetworkManager.Singleton == null)
                return;
            if (_current != null)
                Destroy(_current.gameObject);
            _current = new GameObject("RunNet").AddComponent<RunNet>();
            _current.Init(session, layout);
        }

        /// <summary>The session ended: stop syncing this run (the host may play on alone).</summary>
        public static void Stop()
        {
            if (_current == null)
                return;
            RunNet net = _current;
            net.Detach();
            Destroy(net.gameObject);
        }

        private void Init(OnlineSession session, LevelLayout layout)
        {
            _net = NetworkManager.Singleton;
            _layout = layout;
            _host = session.IsHost;
            _peer = session.Peer;
            _layoutHash = layout.Hash();

            CustomMessagingManager messages = _net.CustomMessagingManager;
            messages.RegisterNamedMessageHandler(Thrown, OnThrown);
            messages.RegisterNamedMessageHandler(Blast, OnBlast);
            NetRole.DynamiteThrown = SendThrown;
            NetRole.DynamiteExploded = SendBlast;
            if (_host)
            {
                messages.RegisterNamedMessageHandler(Ready, (sender, reader) => _clientReady = true);
                messages.RegisterNamedMessageHandler(Damage, OnDamage);
                messages.RegisterNamedMessageHandler(Claim, OnClaim);
                NetRole.BlockHealthChanged = OnBlockChanged;
                NetRole.PickupTaken = (pickup, player) => SendTaken(pickup.NetIndex, player);
            }
            else
            {
                messages.RegisterNamedMessageHandler(Cells, OnCells);
                messages.RegisterNamedMessageHandler(Taken, OnTaken);
                messages.RegisterNamedMessageHandler(State, OnState);
                messages.RegisterNamedMessageHandler(Check, OnCheck);
                messages.RegisterNamedMessageHandler(Score, OnScore);
                NetRole.RequestDamage = SendDamage;
                NetRole.ClaimPickup = pickup => Send(Claim, writer => BytePacker.WriteValueBitPacked(writer, pickup.NetIndex));
                Send(Ready, writer => { });
            }
        }

        private void OnDestroy() => Detach();

        private void Detach()
        {
            if (_run != null)
                _run.Scored -= OnScored;
            _run = null;
            if (_current != this)
                return; // replaced: the new one owns the handlers and hooks
            _current = null;
            if (!_host)
                PauseController.SetHostPaused(false); // a host gone while paused leaves nothing frozen
            NetRole.DynamiteThrown = null;
            NetRole.DynamiteExploded = null;
            NetRole.BlockHealthChanged = null;
            NetRole.PickupTaken = null;
            NetRole.RequestDamage = null;
            NetRole.ClaimPickup = null;
            if (_net != null && _net.CustomMessagingManager != null)
                foreach (string name in Names)
                    _net.CustomMessagingManager.UnregisterNamedMessageHandler(name);
        }

        private void Update()
        {
            if (!_host)
            {
                if (_pending.Count > 0)
                    ApplyPending();
                return;
            }
            if (_run != RunController.Current)
            {
                if (_run != null)
                    _run.Scored -= OnScored;
                _run = RunController.Current;
                if (_run != null)
                    _run.Scored += OnScored;
            }
            float now = Time.unscaledTime;
            if (now >= _nextCells)
            {
                _nextCells = now + CellsEvery;
                SendCells();
                SendScores(); // after the cells: the block is gone when its points pop up
            }
            if (!_clientReady)
                return;
            if (now >= _nextState)
            {
                _nextState = now + StateEvery;
                SendState();
            }
            if (Debug.isDebugBuild && now >= _nextCheck)
            {
                _nextCheck = now + CheckEvery;
                SendCheck();
            }
        }

        // ---------------- Sending ----------------

        private void Send(string name, System.Action<FastBufferWriter> write)
        {
            if (_net == null || !_net.IsListening)
                return;
            var writer = new FastBufferWriter(64, Allocator.Temp, 64 * 1024);
            try
            {
                write(writer);
                _net.CustomMessagingManager.SendNamedMessage(name, _peer, writer, Reliable);
            }
            finally
            {
                writer.Dispose();
            }
        }

        private int Width(int building) => _layout.Buildings[building].Spec.Width;

        private bool Address(BuildingBlock block, out int building, out int cell)
        {
            DestructibleBuilding owner = block.Building;
            building = owner != null ? owner.NetIndex : -1;
            cell = 0;
            if (building < 0 || building >= _layout.Buildings.Count || block.Coords.Count == 0)
                return false;
            cell = NetPacking.CellIndex(block.Coords[0], Width(building));
            return true;
        }

        private bool TryGetBlock(int building, int cell, out BuildingBlock block)
        {
            block = null;
            return building >= 0 && building < _layout.Buildings.Count
                && DestructibleBuilding.TryGetByNetIndex(building, out DestructibleBuilding owner)
                && owner.TryGetBlock(NetPacking.Cell(cell, Width(building)), out block) && block != null;
        }

        // ---------------- Damage and cells ----------------

        private void SendDamage(BuildingBlock block, float amount)
        {
            if (!Address(block, out int building, out int cell))
                return;
            Send(Damage, writer =>
            {
                BytePacker.WriteValueBitPacked(writer, building);
                BytePacker.WriteValueBitPacked(writer, cell);
                writer.WriteValueSafe(amount);
            });
        }

        private void OnDamage(ulong sender, FastBufferReader reader)
        {
            ByteUnpacker.ReadValueBitPacked(reader, out int building);
            ByteUnpacker.ReadValueBitPacked(reader, out int cell);
            reader.ReadValueSafe(out float amount);
            // The instigator is the sender (player 2), never something the message says.
            if (TryGetBlock(building, cell, out BuildingBlock block) && amount > 0f)
                block.TakeDamage(Mathf.Min(amount, MaxDamage), 1);
        }

        private void OnBlockChanged(BuildingBlock block)
        {
            if (Address(block, out int building, out int cell))
                _changed.Add((building, cell, NetPacking.PackCellHealth(NetPacking.QuantizeHealth(block.HealthFraction), block.LastInstigator)));
        }

        private void SendCells()
        {
            if (_changed.Count == 0)
                return;
            Send(Cells, writer =>
            {
                BytePacker.WriteValueBitPacked(writer, _changed.Count);
                foreach ((int building, int cell, byte health) in _changed)
                {
                    BytePacker.WriteValueBitPacked(writer, building);
                    BytePacker.WriteValueBitPacked(writer, cell);
                    writer.WriteValueSafe(health);
                }
            });
            _changed.Clear();
        }

        private void OnCells(ulong sender, FastBufferReader reader)
        {
            ByteUnpacker.ReadValueBitPacked(reader, out int count);
            for (int i = 0; i < count; i++)
            {
                ByteUnpacker.ReadValueBitPacked(reader, out int building);
                ByteUnpacker.ReadValueBitPacked(reader, out int cell);
                reader.ReadValueSafe(out byte packed);
                if (building < 0 || building >= _layout.Buildings.Count)
                    continue;
                if (DestructibleBuilding.TryGetByNetIndex(building, out DestructibleBuilding owner) && owner.IsReady && !_pending.ContainsKey(building))
                {
                    ApplyCell(building, cell, packed);
                    continue;
                }
                // Not streamed in here yet: keep the latest (health only ever goes down).
                if (!_pending.TryGetValue(building, out Dictionary<int, byte> cells))
                    _pending[building] = cells = new Dictionary<int, byte>();
                cells[cell] = packed;
            }
        }

        private void ApplyCell(int building, int cell, byte packed)
        {
            if (!TryGetBlock(building, cell, out BuildingBlock block))
                return; // already destroyed here
            NetPacking.UnpackCellHealth(packed, out int steps, out int instigator);
            block.ApplyNetHealth(NetPacking.HealthFraction(steps), instigator);
        }

        private void ApplyPending()
        {
            _arrived.Clear();
            foreach (int building in _pending.Keys)
                if (DestructibleBuilding.TryGetByNetIndex(building, out DestructibleBuilding owner) && owner.IsReady)
                    _arrived.Add(building);
            foreach (int building in _arrived)
            {
                Dictionary<int, byte> cells = _pending[building];
                _pending.Remove(building);
                foreach (KeyValuePair<int, byte> cell in cells)
                    ApplyCell(building, cell.Key, cell.Value);
            }
        }

        // ---------------- Score popups ----------------

        private void OnScored(int player, int points, Vector3 at)
        {
            if (points > 0)
                _scored.Add((player, points, at));
        }

        private void SendScores()
        {
            if (_scored.Count == 0)
                return;
            Send(Score, writer =>
            {
                BytePacker.WriteValueBitPacked(writer, _scored.Count);
                foreach ((int player, int points, Vector2 at) in _scored)
                {
                    writer.WriteValueSafe((sbyte)player);
                    BytePacker.WriteValueBitPacked(writer, points);
                    writer.WriteValueSafe(at);
                }
            });
            _scored.Clear();
        }

        private void OnScore(ulong sender, FastBufferReader reader)
        {
            ByteUnpacker.ReadValueBitPacked(reader, out int count);
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out sbyte player);
                ByteUnpacker.ReadValueBitPacked(reader, out int points);
                reader.ReadValueSafe(out Vector2 at);
                RunController run = RunController.Current;
                if (run != null)
                    run.ShowScored(player, points, at);
            }
        }

        // ---------------- Pickups ----------------

        private static Pickup FindPickup(int index)
        {
            foreach (Pickup pickup in FindObjectsByType<Pickup>(FindObjectsSortMode.None))
                if (pickup.NetIndex == index)
                    return pickup;
            return null;
        }

        private static PlayerCombat Combat(int player)
        {
            GameObject go = GameObject.Find($"Player_{player + 1}");
            return go != null ? go.GetComponent<PlayerCombat>() : null;
        }

        private void OnClaim(ulong sender, FastBufferReader reader)
        {
            ByteUnpacker.ReadValueBitPacked(reader, out int index);
            Pickup pickup = FindPickup(index);
            if (pickup == null)
                return; // someone was first
            if (pickup.Kind == PickupKind.Resurrect && (RunController.Current == null || !RunController.Current.TryResurrect(1)))
                return;
            pickup.Take(Combat(1), local: false);
            SendTaken(index, 1);
        }

        private void SendTaken(int index, int player)
        {
            if (index < 0)
                return;
            Send(Taken, writer =>
            {
                BytePacker.WriteValueBitPacked(writer, index);
                writer.WriteValueSafe((byte)player);
            });
        }

        private void OnTaken(ulong sender, FastBufferReader reader)
        {
            ByteUnpacker.ReadValueBitPacked(reader, out int index);
            reader.ReadValueSafe(out byte player);
            Pickup pickup = FindPickup(index);
            if (pickup != null)
                pickup.Take(Combat(player), local: player == NetRole.LocalPlayer);
            else
                Pickup.MarkTaken(index); // not streamed in here yet: it never will be
        }

        // ---------------- Dynamite ----------------

        private void SendThrown(Dynamite stick, Vector2 force)
        {
            int id = _nextStick++;
            _ownSticks[stick] = id;
            Vector3 at = stick.transform.position;
            Send(Thrown, writer =>
            {
                BytePacker.WriteValueBitPacked(writer, id);
                writer.WriteValueSafe(at.x);
                writer.WriteValueSafe(at.y);
                writer.WriteValueSafe(force.x);
                writer.WriteValueSafe(force.y);
            });
        }

        private void SendBlast(Dynamite stick)
        {
            if (!_ownSticks.TryGetValue(stick, out int id))
                return;
            _ownSticks.Remove(stick);
            Vector3 at = stick.transform.position;
            Send(Blast, writer =>
            {
                BytePacker.WriteValueBitPacked(writer, id);
                writer.WriteValueSafe(at.x);
                writer.WriteValueSafe(at.y);
            });
        }

        private void OnThrown(ulong sender, FastBufferReader reader)
        {
            ByteUnpacker.ReadValueBitPacked(reader, out int id);
            reader.ReadValueSafe(out float x);
            reader.ReadValueSafe(out float y);
            reader.ReadValueSafe(out float fx);
            reader.ReadValueSafe(out float fy);
            PlayerCombat thrower = Combat(_host ? 1 : 0);
            if (thrower != null)
                _copies[id] = thrower.ThrowCosmeticDynamite(new Vector3(x, y, 0f), new Vector2(fx, fy));
        }

        private void OnBlast(ulong sender, FastBufferReader reader)
        {
            ByteUnpacker.ReadValueBitPacked(reader, out int id);
            reader.ReadValueSafe(out float x);
            reader.ReadValueSafe(out float y);
            if (_copies.TryGetValue(id, out Dynamite copy))
            {
                _copies.Remove(id);
                if (copy != null)
                    copy.ExplodeAt(new Vector3(x, y, 0f));
            }
        }

        // ---------------- Run state ----------------

        private void SendState()
        {
            RunController run = RunController.Current;
            if (run == null)
                return;
            RunSnapshot s = run.State.Snapshot();
            RunScore score = run.Score;
            float destroyed = run.DestroyedFraction;
            Send(State, writer =>
            {
                writer.WriteValueSafe(_layoutHash);
                writer.WriteValueSafe((byte)s.Phase);
                writer.WriteValueSafe(s.Countdown);
                writer.WriteValueSafe(s.Elapsed);
                writer.WriteValueSafe(s.LastCall);
                writer.WriteValueSafe(destroyed);
                writer.WriteValueSafe((byte)s.Status.Length);
                for (int i = 0; i < s.Status.Length; i++)
                {
                    writer.WriteValueSafe((byte)((int)s.Status[i] | (s.Crossed[i] ? 4 : 0) | (s.FreeRespawnUsed[i] ? 8 : 0)));
                    writer.WriteValueSafe(s.OutOfBounds[i]);
                    writer.WriteValueSafe(s.Rejoin[i]);
                    BytePacker.WriteValueBitPacked(writer, i < score.PlayerCount ? score.Points(i) : 0);
                }
                BytePacker.WriteValueBitPacked(writer, score.Unattributed);
                writer.WriteValueSafe(PauseController.IsPaused);
            });
        }

        private void OnState(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out int layoutHash);
            if (!_hashChecked)
            {
                _hashChecked = true;
                if (layoutHash != _layoutHash)
                {
                    OnlineSession.Current?.End("THE LEVEL CAME OUT DIFFERENT ON THE TWO DEVICES");
                    return;
                }
            }
            reader.ReadValueSafe(out byte phase);
            var s = new RunSnapshot { Phase = (RunPhase)phase };
            reader.ReadValueSafe(out s.Countdown);
            reader.ReadValueSafe(out s.Elapsed);
            reader.ReadValueSafe(out s.LastCall);
            reader.ReadValueSafe(out float destroyed);
            reader.ReadValueSafe(out byte players);
            s.Status = new PlayerStatus[players];
            s.OutOfBounds = new float[players];
            s.Rejoin = new float[players];
            s.Crossed = new bool[players];
            s.FreeRespawnUsed = new bool[players];
            var points = new int[players];
            for (int i = 0; i < players; i++)
            {
                reader.ReadValueSafe(out byte bits);
                s.Status[i] = (PlayerStatus)(bits & 3);
                s.Crossed[i] = (bits & 4) != 0;
                s.FreeRespawnUsed[i] = (bits & 8) != 0;
                reader.ReadValueSafe(out s.OutOfBounds[i]);
                reader.ReadValueSafe(out s.Rejoin[i]);
                ByteUnpacker.ReadValueBitPacked(reader, out points[i]);
            }
            ByteUnpacker.ReadValueBitPacked(reader, out int unattributed);
            reader.ReadValueSafe(out bool hostPaused);

            RunController run = RunController.Current;
            if (run == null)
                return; // still waiting for the players
            // Score and demolition first: a phase change to Finished banks them.
            run.NetFraction = destroyed;
            run.Score.Restore(points, unattributed);
            run.State.ApplySnapshot(s);
            PauseController.SetHostPaused(hostPaused);
        }

        // Development builds: catch a desync early (docs/multiplayer.md §3).
        private void SendCheck()
        {
            SendCells(); // the hash includes every change so far: the client must have them first
            var hashes = new List<(int index, int hash)>();
            foreach (DestructibleBuilding building in DestructibleBuilding.Loaded)
                if (building != null)
                    hashes.Add((building.NetIndex, building.StandingHash()));
            Send(Check, writer =>
            {
                BytePacker.WriteValueBitPacked(writer, hashes.Count);
                foreach ((int index, int hash) in hashes)
                {
                    BytePacker.WriteValueBitPacked(writer, index);
                    writer.WriteValueSafe(hash);
                }
            });
        }

        private void OnCheck(ulong sender, FastBufferReader reader)
        {
            ByteUnpacker.ReadValueBitPacked(reader, out int count);
            for (int i = 0; i < count; i++)
            {
                ByteUnpacker.ReadValueBitPacked(reader, out int index);
                reader.ReadValueSafe(out int hash);
                if (DestructibleBuilding.TryGetByNetIndex(index, out DestructibleBuilding building) && !_pending.ContainsKey(index)
                    && building.StandingHash() != hash && _reportedDesync.Add(index))
                    Debug.LogError($"Online desync: building {index} has different standing cells on the two devices.");
            }
        }
    }
}
