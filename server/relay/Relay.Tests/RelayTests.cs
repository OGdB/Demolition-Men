using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

public class RelayTests
{
    static WebApplicationFactory<Program> Start(params (string Key, string Value)[] settings) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            foreach (var (key, value) in settings) b.UseSetting("Relay:" + key, value);
        });

    static Task<WebSocket> Connect(WebApplicationFactory<Program> app, string query) =>
        app.Server.CreateWebSocketClient().ConnectAsync(new Uri("ws://localhost/ws?" + query), CancellationToken.None);

    static async Task<(WebSocket Socket, string Code)> Host(WebApplicationFactory<Program> app)
    {
        var host = await Connect(app, "v=1&role=host");
        var message = await ReceiveText(host);
        Assert.Matches("^code:[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{6}$", message);
        return (host, message["code:".Length..]);
    }

    static async Task<WebSocket> Join(WebApplicationFactory<Program> app, WebSocket host, string code)
    {
        var joiner = await Connect(app, "v=1&role=join&code=" + code);
        Assert.Equal("joined", await ReceiveText(joiner));
        Assert.Equal("peer-joined", await ReceiveText(host));
        return joiner;
    }

    static async Task<(WebSocketMessageType Type, byte[] Data)> Receive(WebSocket socket)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var buffer = new byte[256 * 1024];
        var length = 0;
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, length, buffer.Length - length), timeout.Token);
            length += result.Count;
        } while (!result.EndOfMessage);
        // Answer a close like a browser does.
        if (result.MessageType == WebSocketMessageType.Close)
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        return (result.MessageType, buffer[..length]);
    }

    static async Task<string> ReceiveText(WebSocket socket)
    {
        var (type, data) = await Receive(socket);
        Assert.Equal(WebSocketMessageType.Text, type);
        return Encoding.UTF8.GetString(data);
    }

    static async Task<byte[]> ReceiveBinary(WebSocket socket)
    {
        var (type, data) = await Receive(socket);
        Assert.Equal(WebSocketMessageType.Binary, type);
        return data;
    }

    static async Task ExpectClose(WebSocket socket, int code)
    {
        var (type, _) = await Receive(socket);
        Assert.Equal(WebSocketMessageType.Close, type);
        Assert.Equal((WebSocketCloseStatus)code, socket.CloseStatus);
    }

    static Task SendBinary(WebSocket socket, byte[] data, bool end = true) =>
        socket.SendAsync(data, WebSocketMessageType.Binary, end, CancellationToken.None);

    [Fact]
    public async Task Health_AndPlainHttpOnWs()
    {
        using var app = Start();
        var http = app.CreateClient();
        Assert.Equal("ok", await http.GetStringAsync("/health"));
        Assert.Equal(400, (int)(await http.GetAsync("/ws?v=1&role=host")).StatusCode);
    }

    [Fact]
    public async Task PrivacyPolicy_ServedAsLockedDownHtml()
    {
        using var app = Start();
        var response = await app.CreateClient().GetAsync("/privacy");
        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains("Privacy Policy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Join_PairsAndForwardsBinaryBothWaysByteExact()
    {
        using var app = Start();
        var (host, code) = await Host(app);
        await SendBinary(host, [9]); // nobody to receive it yet: dropped
        var joiner = await Join(app, host, code.ToLowerInvariant());

        var all = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        await joiner.SendAsync("ignored"u8.ToArray(), WebSocketMessageType.Text, true, CancellationToken.None);
        await SendBinary(joiner, all[..100], end: false); // fragmented: arrives as one message
        await SendBinary(joiner, all[100..]);
        Assert.Equal(all, await ReceiveBinary(host));

        await SendBinary(host, [1, 2, 3]);
        await SendBinary(host, []);
        Assert.Equal([1, 2, 3], await ReceiveBinary(joiner));
        Assert.Empty(await ReceiveBinary(joiner));
    }

    [Fact]
    public async Task UnknownCode_Closes4001()
    {
        using var app = Start();
        var joiner = await Connect(app, "v=1&role=join&code=ZZZZZZ");
        await ExpectClose(joiner, 4001);
        Assert.Equal("unknown code", joiner.CloseStatusDescription);
    }

    [Fact]
    public async Task ThirdPeer_Closes4002()
    {
        using var app = Start();
        var (host, code) = await Host(app);
        await Join(app, host, code);
        await ExpectClose(await Connect(app, "v=1&role=join&code=" + code), 4002);
    }

    [Fact]
    public async Task VersionMismatch_Closes4003()
    {
        using var app = Start();
        await ExpectClose(await Connect(app, "v=2&role=host"), 4003);
    }

    [Fact]
    public async Task MaxRooms_Closes4004()
    {
        using var app = Start(("MaxRooms", "1"));
        await Host(app);
        await ExpectClose(await Connect(app, "v=1&role=host"), 4004);
    }

    [Fact]
    public async Task BadRole_Closes4006()
    {
        using var app = Start();
        await ExpectClose(await Connect(app, "v=1&role=spectator"), 4006);
    }

    [Fact]
    public async Task OversizeMessage_Closes4008()
    {
        using var app = Start(("MaxMessageBytes", "1024"));
        var (host, code) = await Host(app);
        var joiner = await Join(app, host, code);
        await SendBinary(joiner, new byte[1024]); // exactly the cap still goes through
        Assert.Equal(1024, (await ReceiveBinary(host)).Length);
        await SendBinary(joiner, new byte[1025]);
        await ExpectClose(joiner, 4008);
        Assert.Equal("peer-left", await ReceiveText(host));
    }

    [Fact]
    public async Task HostLeaving_ClosesJoiner4005()
    {
        using var app = Start();
        var (host, code) = await Host(app);
        var joiner = await Join(app, host, code);
        await host.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        await ExpectClose(joiner, 4005);
    }

    [Fact]
    public async Task JoinerLeaving_TellsHostAndRoomTakesANewJoiner()
    {
        using var app = Start();
        var (host, code) = await Host(app);
        var joiner = await Join(app, host, code);
        await joiner.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        Assert.Equal("peer-left", await ReceiveText(host));

        var next = await Join(app, host, code);
        await SendBinary(next, [7]);
        Assert.Equal([7], await ReceiveBinary(host));
    }

    [Fact]
    public async Task FailedJoins_RateLimited4029()
    {
        using var app = Start(("FailedJoinsPerMinute", "2"));
        await ExpectClose(await Connect(app, "v=1&role=join&code=AAAAAA"), 4001);
        await ExpectClose(await Connect(app, "v=1&role=join&code=BBBBBB"), 4001);
        await ExpectClose(await Connect(app, "v=1&role=join&code=CCCCCC"), 4029);
    }

    [Fact]
    public async Task RoomsPerIp_Capped4029()
    {
        using var app = Start(("MaxRoomsPerIp", "2"));
        await Host(app);
        var (second, _) = await Host(app);
        await ExpectClose(await Connect(app, "v=1&role=host"), 4029);

        await second.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        await Task.Delay(200); // the relay drops the room when the host's loop ends
        await Host(app);
    }

    [Fact]
    public async Task IdlePairedRoom_Closes4010()
    {
        using var app = Start(("IdleTimeout", "00:00:01"));
        var (host, code) = await Host(app);
        var joiner = await Join(app, host, code);
        await ExpectClose(joiner, 4010);
        await ExpectClose(host, 4010);
    }
}
