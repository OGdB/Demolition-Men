# Co-op relay

Pairs two players by a join code and forwards their bytes over WebSockets. It runs no game
code and never reads game data. Design: `docs/multiplayer.md` §8.

```
Relay/            ASP.NET Core app (RelayServer.cs holds rooms, limits and forwarding)
Relay.Tests/      xUnit tests against the app in memory
deploy/           deploy.sh, rollback.sh, install.sh (runs on the VPS), the systemd unit
```

## Local development

Needs the .NET 10 SDK.

```
cd server/relay
dotnet run --project Relay      # http://localhost:5080, the editor connects to ws://localhost:5080/ws
dotnet test Relay.Tests
```

## Protocol

Protocol version `v` is `1` (config `Protocol`). Bump it when the game's wire format changes,
so old builds get a clear `4003` instead of garbage.

| Step | Request / message |
|---|---|
| Host opens a room | `GET /ws?v=1&role=host` (WebSocket). The relay sends TEXT `code:K7MX2P`: 6 characters from `ABCDEFGHJKLMNPQRSTUVWXYZ23456789`. |
| Joiner joins | `GET /ws?v=1&role=join&code=k7mx2p` (case-insensitive). Joiner gets TEXT `joined`, host gets TEXT `peer-joined`. |
| Game data | Every BINARY message goes unchanged to the other peer as one binary message (fragments are reassembled, up to `MaxMessageBytes`). Binary sent while alone in the room is dropped. TEXT from peers is ignored. |
| Joiner leaves | Host gets TEXT `peer-left`. The room stays open; the same code takes a new joiner. |
| Host leaves | The room closes; the joiner is closed with `4005`. |
| Health | `GET /health` answers `ok`. |

Errors come as WebSocket close codes after the accept, because browsers can't read HTTP
error bodies:

| Code | Reason | When |
|---|---|---|
| 4001 | `unknown code` | No room with that code. Counts as a failed join. |
| 4002 | `room full` | The room already has a joiner. Counts as a failed join. |
| 4003 | `version` | `v` is not the relay's protocol version. |
| 4004 | `server full` | `MaxRooms` rooms are open. |
| 4005 | `host left` | The host disconnected. |
| 4006 | `bad request` | `v` or `role` missing or wrong. |
| 4008 | `too large` | A message over `MaxMessageBytes`. |
| 4009 | `too slow` | This peer didn't read fast enough and its outbound queue overflowed (256 messages or 4 × `MaxMessageBytes`). |
| 4010 | `idle` | Host alone longer than `LobbyTimeout`, a paired room with no binary message either way for `IdleTimeout`, or a room older than `MaxRoomLifetime`. |
| 4029 | `rate limited` | Too many connections or failed joins from this IP in the last minute, or too many rooms hosted from it. |

A plain HTTP request to `/ws` gets 400; a browser Origin not in `AllowedOrigins` gets 403.

For the Unity transport: the relay pings every 15 s on its own, but pings don't count as
data. If the game can go quiet for a minute (the host choosing a level), send an empty binary
message every ~20 s and ignore empty messages on receive.

## Configuration

`Relay/appsettings.json`, section `Relay`. Environment variables override it as
`Relay__<Key>`. On the VPS put them in `/etc/demolition-relay.env` (deploys never touch it),
one per line, then `sudo systemctl restart demolition-relay`.

| Key | Default | |
|---|---|---|
| `Protocol` | 1 | Protocol version the game must send as `v`. |
| `MaxRooms` | 500 | |
| `MaxRoomsPerIp` | 3 | Rooms one IP may host at once (a household behind one router can still host a few). |
| `MaxMessageBytes` | 65536 | Larger messages close the sender with 4008. |
| `ConnectsPerMinute` | 30 | Per client IP. |
| `FailedJoinsPerMinute` | 10 | Per client IP, so codes can't be brute-forced. |
| `LobbyTimeout` | 00:15:00 | Host waiting alone. |
| `IdleTimeout` | 00:01:00 | Paired room with no data either way. |
| `MaxRoomLifetime` | 06:00:00 | |
| `KeepAliveInterval` / `KeepAliveTimeout` | 00:00:15 | WebSocket pings; a peer that doesn't answer is dropped. |
| `AllowedOrigins` | empty (all) | Browser origins allowed to connect, e.g. `Relay__AllowedOrigins__0=https://html-classic.itch.zone`. Only checked when an `Origin` header is present: native clients send none. |

The client IP comes from `X-Forwarded-For` only when the request comes from loopback (Caddy
on the same box).

## Deploy to a VPS

Prerequisites:
- A Debian or Ubuntu VPS, and SSH access as a user with sudo. An SSH key saves you three
  password prompts.
- A domain name whose A record (and AAAA, if the VPS has IPv6) points at the VPS. A free
  [DuckDNS](https://www.duckdns.org) subdomain (`yourname.duckdns.org`) works.
- Ports 80 and 443 open at the hosting provider, and nothing else listening on them.
- On your machine: bash (Linux, macOS, WSL or Git Bash), ssh, scp, curl and the .NET 10 SDK.

```
server/relay/deploy/deploy.sh admin@203.0.113.7 relay.example.org --email you@example.org --firewall
```

It builds a self-contained single binary for the VPS's CPU (the VPS needs no .NET), uploads
it, then with sudo: creates the `relay` system user, installs to
`/opt/demolition-relay/current` (the release before goes to `previous`), installs and starts
`demolition-relay.service`, installs Caddy from its apt repository if missing and points
`<domain>` at the relay in `/etc/caddy/conf.d/demolition-relay.caddy`. Caddy gets the Let's
Encrypt certificate itself. At the end it waits for `https://<domain>/health` and prints
the URL for the game: `wss://<domain>/ws`.

- `--email` gives Let's Encrypt an address for expiry warnings. Optional.
- `--firewall` enables ufw allowing only your SSH port, 80 and 443. Without it the firewall
  is left alone.
- It stops before changing anything if ports 80/443 belong to something other than Caddy.

**Update:** run the same command again. **Roll back:** `server/relay/deploy/rollback.sh
admin@203.0.113.7` swaps `current` and `previous` and restarts; running it again swaps back.

**Logs:** `ssh admin@203.0.113.7 journalctl -u demolition-relay -f`. Once a minute, if anything
happened: rooms open, peers, bytes/s in and out, and connections closed by reason.
Certificate problems: `journalctl -u caddy`.

### Behind nginx

If nginx already owns 80/443, `deploy.sh` stops. Add this to the nginx `server` block for
your domain (TLS stays nginx's job):

```nginx
location = /ws {
    proxy_pass http://127.0.0.1:5080;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_set_header X-Forwarded-For $remote_addr;
    proxy_read_timeout 1h;
}
location = /health { proxy_pass http://127.0.0.1:5080; }
```

Then install the service by hand (from `server/relay`, use `linux-arm64` on ARM):

```
dotnet publish Relay -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -p:DebugType=None -o publish
scp publish/Relay publish/appsettings.json deploy/demolition-relay.service admin@vps:
ssh -t admin@vps 'sudo useradd --system --user-group --no-create-home --home-dir /nonexistent --shell /usr/sbin/nologin relay;
  sudo install -d /opt/demolition-relay/current && sudo install -m 755 Relay /opt/demolition-relay/current/ &&
  sudo install -m 644 appsettings.json /opt/demolition-relay/current/ &&
  sudo install -m 644 demolition-relay.service /etc/systemd/system/ &&
  sudo systemctl daemon-reload && sudo systemctl enable --now demolition-relay'
```

## Privacy

The relay sees players' IP addresses: it has to, to talk to them, and it keys the rate limits
on them in memory for a minute. It never logs IP addresses or join codes, and never reads
the game data it forwards. Caddy keeps no access log unless you add one. The game's privacy
policy should still say that the relay sees IP addresses (`docs/monetization.md` checklist).
