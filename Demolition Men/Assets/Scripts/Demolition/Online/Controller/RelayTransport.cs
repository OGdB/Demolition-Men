using System;
using System.Collections.Generic;
using System.Text;
using NativeWebSocket;
using Unity.Netcode;
using UnityEngine;

namespace Demolition.Online
{
    /// <summary>
    /// NGO transport over the co-op relay (server/relay, docs/multiplayer.md §8). Host and
    /// joiner both connect out over a WebSocket, so it works in a browser and behind any NAT.
    /// The relay pairs them by code and copies binary messages; its text messages say what
    /// happened (<c>code:</c>, <c>joined</c>, <c>peer-joined</c>, <c>peer-left</c>) and its close
    /// codes why a connection ended. Each binary message starts with a <see cref="Kind"/> byte.
    /// Every NGO delivery mode becomes reliable and ordered (TCP).
    /// shortcut: unreliable sends are never dropped when the connection backs up; add that if the remote player lags on bad links.
    /// </summary>
    public sealed class RelayTransport : NetworkTransport
    {
        public const int Protocol = 1;
        private const ulong Peer = 1; // the one remote client, as the host's transport numbers it
        private const float PingEvery = 1f; // also the keep-alive: the relay closes a pair quiet for 60 s

        private enum Kind : byte { Data, Ping, Pong, Drop }

        /// <summary>The relay's WebSocket endpoint, e.g. wss://relay.oeds.space/ws.</summary>
        public string Url { get; set; }
        /// <summary>The room to join (client).</summary>
        public string JoinCode { get; set; }
        /// <summary>The host's room code, once the relay sent it.</summary>
        public string Code { get; private set; }
        /// <summary>Why the relay closed the connection (4001 unknown code, 4003 version, ...), 0 if it didn't.</summary>
        public int CloseCode { get; private set; }
        public event Action<string> CodeReady;

        private readonly Queue<(NetworkEvent type, ulong id, ArraySegment<byte> data, float time)> _events =
            new Queue<(NetworkEvent, ulong, ArraySegment<byte>, float)>();
        private WebSocket _socket;
        private bool _server;
        private bool _paired;
        private float _lastPing;
        private ulong _rtt;

        public override ulong ServerClientId => 0;

        public override void Initialize(NetworkManager networkManager = null) { }

        public override bool StartServer()
        {
            _server = true;
            return Open($"{Url}?v={Protocol}&role=host");
        }

        public override bool StartClient()
        {
            _server = false;
            return Open($"{Url}?v={Protocol}&role=join&code={Uri.EscapeDataString(JoinCode ?? string.Empty)}");
        }

        private bool Open(string url)
        {
            Close();
            CloseCode = 0;
            Code = null;
            _paired = false;
            _events.Clear();
            var socket = new WebSocket(url);
            _socket = socket;
            // A socket replaced or closed by us still reports in: only the current one counts.
            socket.OnMessage += data => { if (socket == _socket) Received(data); };
            socket.OnClose += code => { if (socket == _socket) Closed((int)code); };
            socket.OnOpen += () => { if (socket != _socket) _ = socket.Close(); };
            socket.OnError += error => { if (socket == _socket) Debug.LogWarning($"Relay: {error}"); };
            _ = socket.Connect(); // runs until the socket closes; its events arrive on the main thread
            return true;
        }

        private void Received(byte[] data)
        {
            float now = Time.realtimeSinceStartup;
            if (data.Length > 0 && data[0] <= (byte)Kind.Drop)
            {
                ulong from = _server ? Peer : ServerClientId;
                switch ((Kind)data[0])
                {
                    case Kind.Data:
                        _events.Enqueue((NetworkEvent.Data, from, new ArraySegment<byte>(data, 1, data.Length - 1), now));
                        break;
                    case Kind.Ping:
                        data[0] = (byte)Kind.Pong;
                        Write(data);
                        break;
                    case Kind.Pong when data.Length >= 5:
                        _rtt = (ulong)Mathf.Max(0f, (now - BitConverter.ToSingle(data, 1)) * 1000f);
                        break;
                    case Kind.Drop:
                        DisconnectLocalClient();
                        break;
                }
                return;
            }

            // Relay text messages start with a letter, never with a Kind byte.
            string text = Encoding.ASCII.GetString(data);
            if (text.StartsWith("code:", StringComparison.Ordinal))
            {
                Code = text.Substring(5);
                CodeReady?.Invoke(Code);
            }
            else if (text == "joined" || text == "peer-joined")
            {
                _paired = true;
                _events.Enqueue((NetworkEvent.Connect, _server ? Peer : ServerClientId, default, now));
            }
            else if (text == "peer-left")
            {
                _paired = false;
                _events.Enqueue((NetworkEvent.Disconnect, Peer, default, now));
            }
        }

        private void Closed(int code)
        {
            CloseCode = code;
            _socket = null;
            _paired = false;
            // The host losing the relay loses the whole session.
            _events.Enqueue((_server ? NetworkEvent.TransportFailure : NetworkEvent.Disconnect, ServerClientId, default, Time.realtimeSinceStartup));
        }

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            if (_events.Count == 0)
            {
                clientId = 0;
                payload = default;
                receiveTime = Time.realtimeSinceStartup;
                return NetworkEvent.Nothing;
            }
            (NetworkEvent type, ulong id, ArraySegment<byte> data, float time) = _events.Dequeue();
            clientId = id;
            payload = data;
            receiveTime = time;
            return type;
        }

        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery)
        {
            var message = new byte[payload.Count + 1];
            message[0] = (byte)Kind.Data;
            Buffer.BlockCopy(payload.Array, payload.Offset, message, 1, payload.Count);
            Write(message);
        }

        // Pings measure the round trip and keep the pair from timing out on the relay.
        protected override void OnEarlyUpdate()
        {
            float now = Time.realtimeSinceStartup;
            if (!_paired || now - _lastPing < PingEvery)
                return;
            _lastPing = now;
            var ping = new byte[5];
            ping[0] = (byte)Kind.Ping;
            BitConverter.GetBytes(now).CopyTo(ping, 1);
            Write(ping);
        }

        private void Write(byte[] message)
        {
            if (_socket != null && _socket.State == WebSocketState.Open)
                _ = _socket.Send(message);
        }

        // Two peers per room: the host can only drop its one client by telling it to leave.
        public override void DisconnectRemoteClient(ulong clientId) => Write(new[] { (byte)Kind.Drop });

        public override void DisconnectLocalClient() => Close();

        public override ulong GetCurrentRtt(ulong clientId) => _rtt;

        public override void Shutdown() => Close();

        private void Close()
        {
            WebSocket socket = _socket;
            _socket = null;
            _paired = false;
            if (socket != null && socket.State == WebSocketState.Open)
                _ = socket.Close();
        }
    }
}
