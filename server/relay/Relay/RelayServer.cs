using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using System.Threading.RateLimiting;

/// Pairs a host and a joiner by code and copies binary messages between them. Runs no game code.
public sealed class RelayServer(RelayOptions opt, ILogger log)
{
    const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O, 1/I
    static readonly byte[] Joined = "joined"u8.ToArray(), PeerJoined = "peer-joined"u8.ToArray(), PeerLeft = "peer-left"u8.ToArray();
    static readonly Dictionary<int, string> Reasons = new()
    {
        [4001] = "unknown code", [4002] = "room full", [4003] = "version", [4004] = "server full",
        [4005] = "host left", [4006] = "bad request", [4008] = "too large", [4009] = "too slow",
        [4010] = "idle", [4029] = "rate limited",
    };

    // shortcut: 2 peers per room, so data needs no client id; a room for more players prefixes one byte of client id.
    readonly Dictionary<string, Room> rooms = []; // every membership change holds lock(rooms)
    // shortcut: IPv6 is limited per address, not per /64; key on the /64 if abuse shows up.
    readonly PartitionedRateLimiter<string> connects = PerMinute(opt.ConnectsPerMinute);
    readonly PartitionedRateLimiter<string> failedJoins = PerMinute(opt.FailedJoinsPerMinute);
    readonly ConcurrentDictionary<string, int> closes = new();
    long bytesIn, bytesOut;

    public static string Reason(int code) => Reasons.GetValueOrDefault(code, "");

    static PartitionedRateLimiter<string> PerMinute(int limit) =>
        PartitionedRateLimiter.Create<string, string>(ip => RateLimitPartition.GetFixedWindowLimiter(ip,
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = TimeSpan.FromMinutes(1) }));

    public async Task Handle(HttpContext ctx)
    {
        if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
        // Only browsers send Origin: this keeps other websites off the relay, native clients pass.
        string? origin = ctx.Request.Headers.Origin;
        if (opt.AllowedOrigins.Length > 0 && !string.IsNullOrEmpty(origin)
            && !opt.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            Count("origin");
            ctx.Response.StatusCode = 403;
            return;
        }

        // Errors go out as close codes after the accept: browsers can't read HTTP error bodies.
        using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
        var peer = new Peer(socket, this, 4 * opt.MaxMessageBytes);
        var error = Admit(ctx, peer, out var room);
        if (error != 0) await Refuse(socket, error);
        else await Run(room!, peer);
    }

    int Admit(HttpContext ctx, Peer peer, out Room? room)
    {
        room = null;
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "";
        var query = ctx.Request.Query;
        if (!connects.AttemptAcquire(ip).IsAcquired) return 4029;
        if (!int.TryParse(query["v"].ToString(), out var version)) return 4006;
        if (version != opt.Protocol) return 4003;
        switch (query["role"].ToString())
        {
            case "host":
                return Open(peer, ip, out room);
            case "join":
                // Unknown codes and full rooms count against the IP, so codes can't be brute-forced.
                if (!failedJoins.AttemptAcquire(ip, 0).IsAcquired) return 4029;
                var error = Join(peer, query["code"].ToString(), out room);
                if (error != 0) failedJoins.AttemptAcquire(ip);
                return error;
            default:
                return 4006;
        }
    }

    int Open(Peer host, string ip, out Room? room)
    {
        lock (rooms)
        {
            room = null;
            if (rooms.Count >= opt.MaxRooms) return 4004;
            // Otherwise one IP could hold most rooms open through the lobby timeout and lock everyone out.
            if (rooms.Values.Count(r => r.HostIp == ip) >= opt.MaxRoomsPerIp) return 4029;
            string code;
            do code = RandomNumberGenerator.GetString(Alphabet, 6); while (rooms.ContainsKey(code));
            rooms[code] = room = new Room(code, host, ip);
        }
        host.SendText(Encoding.ASCII.GetBytes("code:" + room.Code));
        return 0;
    }

    int Join(Peer joiner, string code, out Room? room)
    {
        lock (rooms)
        {
            if (!rooms.TryGetValue(code.ToUpperInvariant(), out room) || room.Host.Closing) return 4001;
            if (room.Joiner != null) return 4002;
            room.Joiner = joiner;
            room.LastData = Environment.TickCount64;
            // Queued under the lock, so both go out before any game data.
            joiner.SendText(Joined);
            room.Host.SendText(PeerJoined);
            return 0;
        }
    }

    void Leave(Room room, Peer peer)
    {
        lock (rooms)
        {
            if (peer == room.Host)
            {
                rooms.Remove(room.Code);
                room.Joiner?.Close(4005);
            }
            else if (peer == room.Joiner)
            {
                room.Joiner = null;
                room.LastData = Environment.TickCount64; // the lobby timeout starts again
                room.Host.SendText(PeerLeft);
            }
        }
    }

    async Task Run(Room room, Peer peer)
    {
        var writing = peer.WriteLoop();
        var max = opt.MaxMessageBytes;
        var buffer = ArrayPool<byte>.Shared.Rent(max);
        try
        {
            var length = 0;
            while (true)
            {
                var result = await peer.Socket.ReceiveAsync(buffer.AsMemory(length, max - length), peer.Aborted);
                if (result.MessageType == WebSocketMessageType.Close) break;
                length += result.Count;
                Interlocked.Add(ref bytesIn, result.Count);
                if (!result.EndOfMessage)
                {
                    // Reading goes on after the close, so the client still gets the close handshake.
                    if (length == max) { peer.Close(4008); length = 0; }
                    continue;
                }
                if (result.MessageType == WebSocketMessageType.Binary) Forward(room, peer, buffer.AsSpan(0, length));
                length = 0;
            }
        }
        catch (Exception) { } // connection lost, keep-alive timeout, or the close grace ran out
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            Leave(room, peer);
            peer.Close(1000);
            await writing;
        }
    }

    static void Forward(Room room, Peer from, ReadOnlySpan<byte> message)
    {
        if (from.Closing) return;
        var to = from == room.Host ? room.Joiner : room.Host;
        if (to == null) return; // not paired: dropped
        room.LastData = Environment.TickCount64;
        to.Send(message);
    }

    async Task Refuse(WebSocket socket, int code)
    {
        Count(Reason(code));
        using var grace = new CancellationTokenSource(Peer.CloseGrace);
        try { await socket.CloseAsync((WebSocketCloseStatus)code, Reason(code), grace.Token); }
        catch (Exception) { } // the client went away or never answered the close
    }

    /// Every second: close expired rooms. Every minute: one stats line, if anything happened.
    public async Task Tick(CancellationToken stopping)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            for (var second = 1; await timer.WaitForNextTickAsync(stopping); second++)
            {
                Expire();
                if (second % 60 == 0) LogStats();
            }
        }
        catch (OperationCanceledException) { }
    }

    void Expire()
    {
        var now = Environment.TickCount64;
        lock (rooms)
            foreach (var room in rooms.Values)
            {
                var timeout = room.Joiner == null ? opt.LobbyTimeout : opt.IdleTimeout;
                if (now - room.LastData < timeout.TotalMilliseconds && now - room.Opened < opt.MaxRoomLifetime.TotalMilliseconds) continue;
                room.Joiner?.Close(4010);
                room.Host.Close(4010);
            }
    }

    void LogStats()
    {
        int open, peers;
        lock (rooms)
        {
            open = rooms.Count;
            peers = open + rooms.Values.Count(r => r.Joiner != null);
        }
        var received = Interlocked.Exchange(ref bytesIn, 0);
        var sent = Interlocked.Exchange(ref bytesOut, 0);
        var counts = new List<string>();
        foreach (var reason in closes.Keys)
            if (closes.TryRemove(reason, out var n)) counts.Add($"{reason} {n}");
        if (open == 0 && received == 0 && sent == 0 && counts.Count == 0) return;
        // Never log IP addresses or room codes.
        log.LogInformation("rooms {Rooms}, peers {Peers}, in {In} B/s, out {Out} B/s, closed: {Closed}",
            open, peers, received / 60, sent / 60, counts.Count == 0 ? "none" : string.Join(", ", counts));
    }

    internal void Count(string reason) => closes.AddOrUpdate(reason, 1, (_, n) => n + 1);
    internal void Sent(int bytes) => Interlocked.Add(ref bytesOut, bytes);
}

sealed class Room(string code, Peer host, string hostIp)
{
    public readonly string Code = code;
    public readonly Peer Host = host;
    public readonly string HostIp = hostIp; // in memory only, never logged
    public volatile Peer? Joiner;
    public readonly long Opened = Environment.TickCount64;
    public long LastData = Environment.TickCount64; // last forwarded message, or when the host was last left alone
}

/// One WebSocket. A single writer loop drains a bounded queue, because SendAsync is not safe to call concurrently.
sealed class Peer(WebSocket socket, RelayServer server, int maxQueuedBytes)
{
    public static readonly TimeSpan CloseGrace = TimeSpan.FromSeconds(5);
    // Both bounds cap what a peer that stopped reading can pin in memory: bytes for big messages, count for empty ones.
    readonly Channel<(byte[] Data, int Length, bool Text)> queue =
        Channel.CreateBounded<(byte[], int, bool)>(new BoundedChannelOptions(256) { SingleReader = true });
    readonly CancellationTokenSource abort = new();
    int queuedBytes, closeCode;

    public WebSocket Socket => socket;
    public CancellationToken Aborted => abort.Token;
    public bool Closing => Volatile.Read(ref closeCode) != 0;

    public void SendText(byte[] text) => Enqueue(text, text.Length, true);

    public void Send(ReadOnlySpan<byte> message)
    {
        var copy = ArrayPool<byte>.Shared.Rent(message.Length);
        message.CopyTo(copy);
        Enqueue(copy, message.Length, false);
    }

    void Enqueue(byte[] data, int length, bool text)
    {
        if (!Closing && Interlocked.Add(ref queuedBytes, length) <= maxQueuedBytes && queue.Writer.TryWrite((data, length, text))) return;
        if (!text) ArrayPool<byte>.Shared.Return(data);
        if (!Closing) Close(4009);
    }

    /// Sends the close frame after the message in flight. A peer that doesn't finish the close within the grace is aborted.
    public void Close(int code)
    {
        if (Interlocked.CompareExchange(ref closeCode, code, 0) != 0) return;
        if (code >= 4000) server.Count(RelayServer.Reason(code));
        queue.Writer.TryComplete();
        abort.CancelAfter(CloseGrace);
    }

    public async Task WriteLoop()
    {
        try
        {
            await foreach (var (data, length, text) in queue.Reader.ReadAllAsync(abort.Token))
            {
                if (Closing) break; // the close frame goes next; nothing queued matters to a closing peer
                try { await socket.SendAsync(data.AsMemory(0, length), text ? WebSocketMessageType.Text : WebSocketMessageType.Binary, true, abort.Token); }
                finally { if (!text) ArrayPool<byte>.Shared.Return(data); }
                Interlocked.Add(ref queuedBytes, -length);
                server.Sent(length);
            }
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                await socket.CloseOutputAsync((WebSocketCloseStatus)closeCode, RelayServer.Reason(closeCode), abort.Token);
        }
        catch (Exception) { socket.Abort(); }
    }
}
