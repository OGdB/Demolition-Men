# Co-op relay

Pairs two players by a join code and forwards their bytes over WebSockets. It runs no game
code and never reads game data. Design: `docs/multiplayer.md` §8.

```
Relay/            ASP.NET Core app (RelayServer.cs holds rooms, limits and forwarding)
Relay.Tests/      xUnit tests against the app in memory
Dockerfile        the image: built on the VPS, runs on the chiseled ASP.NET image as non-root
compose.yaml      the container behind the VPS's existing Caddy container
deploy/           deploy.sh, rollback.sh, install.sh (runs on the VPS)
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
`Relay__<Key>`. On the VPS put them in `/opt/demolition-relay/relay.env` (deploys never touch
it), one per line, then `cd /opt/demolition-relay && sudo docker compose up -d`.

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

| `TrustedProxyNetworks` | empty | CIDRs whose `X-Forwarded-For` is believed, besides loopback. The deploy sets it to the subnet of Caddy's Docker network. |

The client IP comes from `X-Forwarded-For` only when the request comes from loopback or a
`TrustedProxyNetworks` address. Get this wrong and every player shares the proxy's address:
the per-IP limits then act on everyone at once (3 rooms in total).

## Deploy to the VPS

The relay runs as a Docker container behind the VPS's existing Caddy container: it joins
Caddy's network (`web`), publishes no ports, and Caddy proxies `https://<domain>` to it and
gets its certificate. The deploy is set up for this VPS: Caddy in a container named `caddy`,
its Caddyfile at `/opt/portainer/Caddyfile` on the host (bind-mounted to
`/etc/caddy/Caddyfile`), the `web` network. Flags change these.

Before the first deploy:
1. **DNS:** add an A record for the relay's name pointing at the VPS (for example
   `relay.oeds.space → 138.201.155.228`) and wait until it resolves. There is no wildcard
   record, and Caddy can't get a certificate for a name that doesn't resolve.
2. **On your machine:** bash (Linux, macOS, WSL or Git Bash), ssh, tar and curl. The VPS
   builds the image, so you don't need .NET locally to deploy.
3. **On the VPS:** SSH as root or as a user with sudo; Docker with the compose plugin.

```
server/relay/deploy/deploy.sh root@138.201.155.228 relay.oeds.space
```

On Windows, run it from Git Bash (or WSL), not PowerShell: PowerShell doesn't run `.sh`
files, so nothing happens there.

What it does:
1. Checks that the name resolves, then uploads the sources to a temporary folder.
2. On the VPS (`install.sh`, as root): checks that the `caddy` container runs on `web` and
   mounts the Caddyfile, before changing anything.
3. Copies the sources to `/opt/demolition-relay` and writes `.env` with `web`'s subnet (the
   trusted proxy network). It tags the running image `demolition-relay:previous` and runs
   `docker compose up -d --build`: container `demolition-relay`, read-only, all capabilities
   dropped, 256 MB memory cap, log rotation.
4. Checks that Caddy reaches `demolition-relay:5080/health` over `web`.
5. If the Caddyfile has no block for the domain yet, it keeps a dated backup and appends:
   ```
   relay.oeds.space {
   	reverse_proxy demolition-relay:5080 {
   		stream_close_delay 30m
   	}
   }
   ```
   `stream_close_delay` stops a Caddy reload (for any of the sites) from cutting games in
   progress. The block is appended in place, because the Caddyfile is bind-mounted into the
   container by inode: a replaced file (`sed -i`, most editors) wouldn't show up inside it.
   Caddy validates the result; if it refuses, the backup goes back.
6. Reloads Caddy (`caddy reload`, no restart: the other sites keep running), then waits for
   `https://<domain>/health` and prints the URL for the game: `wss://<domain>/ws`.

**Update:** run the same command again. The Caddy block is only added once.
**Roll back:** `server/relay/deploy/rollback.sh root@138.201.155.228` swaps the `latest` and
`previous` images and restarts the container; running it again swaps back.
**Logs:** `docker logs -f demolition-relay`. Once a minute, if anything happened: rooms open,
peers, bytes/s in and out, connections closed by reason. Certificates: `docker logs caddy`.
**Disk:** the first build pulls the .NET SDK image (about 900 MB) and keeps it as build cache;
`docker builder prune` frees it.

Flags: `--network web`, `--caddy caddy`, `--caddyfile /opt/portainer/Caddyfile`.

## Privacy

The relay sees players' IP addresses: it has to, to talk to them, and it keys the rate limits
on them in memory for a minute. It never logs IP addresses or join codes, and never reads
the game data it forwards. The site block it adds to Caddy has no access log. The game's privacy
policy should still say that the relay sees IP addresses (`docs/monetization.md` checklist).
