# Co-op relay

Pairs two players by a join code and forwards their bytes over WebSockets. It runs no game
code and never reads game data. Design: `docs/multiplayer.md` §8. It also keeps the game's
anonymous counters (below).

```
Relay/            ASP.NET Core app (RelayServer.cs holds rooms, limits and forwarding;
                  Counters.cs the anonymous counters and their SQLite file;
                  wwwroot/privacy.html is the game's privacy policy at /privacy)
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
Relay__CountersDatabase=/tmp/counters.db Relay__AdminToken=0123456789abcdef dotnet run --project Relay   # with the counters
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
| Privacy policy | `GET /privacy`: the game's privacy policy, `Relay/wwwroot/privacy.html`, read at startup (edit it, then deploy). |
| Counters | `POST /count`, `GET /admin/counts`: see Counters below. |

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
| `AllowedOrigins` | empty (all) | Browser origins allowed to connect, e.g. `Relay__AllowedOrigins__0=https://html-classic.itch.zone`. Only checked when an `Origin` header is present: native clients send none. The browser build's counts are allowed from the same origins (CORS). |
| `TrustedProxyNetworks` | empty | CIDRs whose `X-Forwarded-For` is believed, besides loopback. The deploy sets it to the subnet of Caddy's Docker network. |
| `CountersDatabase` | empty (no counters) | The counters' SQLite file. The first deploy writes `Relay__CountersDatabase=/data/counters.db` into `relay.env`; empty it there to turn them off. If the file can't be opened the relay logs an error and runs without counters. |
| `CountsPerMinute` | 60 | Posts to `/count` per client IP. A school behind one address can pass it; its counts are then lost, nothing else. |
| `CountRowsPerDay` | 2000 | Different counters one day may hold. The game's own make a few hundred at most. |
| `AdminToken` | empty (no /admin) | Reads `/admin/counts`. The first deploy writes a random one into `relay.env`. |

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
3. **On the VPS:** SSH as root or as a user with sudo; Docker with the compose plugin. This VPS
   only takes SSH over Tailscale, as `og` (Tailscale SSH), so deploy from a device on the tailnet.

```
server/relay/deploy/deploy.sh og@hetzner-vps relay.oeds.space
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
   dropped, 256 MB memory cap, log rotation. It makes `/opt/demolition-relay/data` for the
   counters' database (`counters.db`: back this folder up if the numbers matter to you) and, the
   first time, adds the database's path and a random admin token to `relay.env`, which it makes
   readable by root only. Deploys keep both.
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
   Caddy validates the result; if it refuses, the backup goes back. If the Caddyfile was
   replaced since the Caddy container started, the container still reads the old copy; the
   script then stops before editing and asks for `docker restart caddy` first.
6. Reloads Caddy (`caddy reload`, no restart: the other sites keep running), then waits for
   `https://<domain>/health` and prints the URL for the game: `wss://<domain>/ws`.

**Update:** run the same command again. The Caddy block is only added once.
**Roll back:** `server/relay/deploy/rollback.sh og@hetzner-vps` swaps the `latest` and
`previous` images and restarts the container; running it again swaps back.
**Logs:** `docker logs -f demolition-relay`. Once a minute, if anything happened: rooms open,
peers, bytes/s in and out, connections closed by reason. Certificates: `docker logs caddy`.
**Disk:** the first build pulls the .NET SDK image (about 900 MB) and keeps it as build cache;
`docker builder prune` frees it.

Flags: `--network web`, `--caddy caddy`, `--caddyfile /opt/portainer/Caddyfile`.

## Counters

The game's anonymous counts (`docs/hit-plan.md` §5.1, F0): no analytics SDK, no id. The game's
side is `Meta/Model/Counters.cs` (what is counted and when) and `Meta/Controller/CounterSender.cs`;
it sends nothing until `GameConfig.countersUrl` is `https://<domain>/count`, and never from the
editor. Here they are on when `CountersDatabase` names a SQLite file, which the deploy sets.

`POST /count` takes one event as JSON, 256 bytes at most, and adds one to a counter per (UTC
day, event, fields). It answers with a status and no body:

| Status | |
|---|---|
| 204 | Counted. |
| 400 | Not JSON, or not an event from the table below: an unknown event, platform or field, a value out of range, a field of another event. |
| 413 | A body over 256 bytes. |
| 429 | More than `CountsPerMinute` from this address in the last minute (refused posts count too). |
| 503 | The database didn't take it (a full disk). |
| 404 | The counters are off. |

| Event `e` | The game sends it | Fields besides `p` |
|---|---|---|
| `first_start` | Once: the first start on a device. | |
| `day` | Once on each of the days 0, 1, 7 and 30 after the first start, if the game is opened that day. The device counts its own calendar days; a session on any other day sends nothing. | `since`: 0, 1, 7 or 30 |
| `tutorial_done` | Once: the Tutorial's first finish. | |
| `level_done` | Every finished run. Failed runs aren't counted. Online both devices send theirs. | `level`: a campaign level's id (`town-01`), `tutorial`, `daily` or `ranked` (`a-z`, `0-9`, `-`, 24 at most); `stars`: 0–3; `players`: 1 or 2; `online`: 0 on one device, 1 the online host, 2 the online guest |
| `share` | SHARE on the results screen, once per run. | |
| `invite` | SHARE beside a room's code: the host sent the room's invite link. Once per room; the code is not in it. | |

`p` is what the game runs on: `web`, `android`, `pc` or `other`. A field an event doesn't have
must be left out, `""` or 0. The game writes every field every time:
`{"e":"day","p":"web","level":"","stars":0,"players":0,"online":0,"since":1}`.

The relay adds two counts of its own: `room` (a host opened a room) and `join` (a joiner was
paired with a host), with `p` = `web` when the connection sent an `Origin` header, which only
browsers do, and `app` otherwise. It looks at nothing else about the device.

**Read them** with the admin token (`Authorization: Bearer <token>`; without it, or with no token
of 16 characters or more configured, `/admin/counts` is 404). `from` and `to` are UTC days,
inclusive; without `from` it is the last 30 days.

```
curl -s -H "Authorization: Bearer $TOKEN" "https://relay.oeds.space/admin/counts?from=2027-01-15" \
  | jq -r '.[] | [.day, .event, .platform, .level, .stars, .players, .online, .since, .n] | @tsv'
```

What the numbers say (`docs/hit-plan.md` §7 and §5.2):
- **Day-1 retention:** `day` with `since` 1 over `day` with `since` 0 of the day before; day 7
  and day 30 likewise. The server's day is UTC and the device's is local, so a count can land a
  day early or late: compare sums over a week, not single days.
- **Tutorial completion:** `tutorial_done` over `first_start`.
- **Runs:** `level_done` with `online` 0 or 1 (a guest's `online` 2 is the same run as its host's).
  Two-player runs are those with `players` 2. A level's finishes over `tutorial_done` says how far
  players get.
- **Shares per finished run:** `share` over those runs (a guest has no SHARE button).
- **Guest runs per online host:** `level_done` with `online` 2 over `room`.
- **Invites** (`docs/hit-plan.md` §4.2): `invite` over `room` is the share of rooms whose link
  was sent; `join` from `web` over `invite` is roughly how many links were opened into a room
  (a browser that typed the code counts too); a guest's finished run is `level_done` from `web`
  with `online` 2. Installs that follow are not counted.

Not counted: failed runs, how long a session lasts, the game's version, visits from shared
links, and anything on days other than 0, 1, 7 and 30.

Anyone can post to `/count`, so the numbers are for deciding what to build and never for money.
The limits keep junk small: the fixed list above, the body cap, `CountsPerMinute` per address,
and `CountRowsPerDay` (past it, a day takes no new counters; those it has still go up).

## Privacy

The relay sees players' IP addresses: it has to, to talk to them, and it keys the rate limits
on them in memory for a minute. It never logs IP addresses or join codes, and never reads
the game data it forwards. The site block it adds to Caddy has no access log. The game's privacy
policy should still say that the relay sees IP addresses (`docs/monetization.md` checklist).

The counters' database is one table, `counts`: the UTC day, the event, its fields and how many.
No address, no user agent, no id, no time of day, nothing from a refused post
(`CountersTests.NothingButCountersIsStored` reads back every file the relay wrote).

### DRAFT, not live: the policy's wording for the counters

`Relay/wwwroot/privacy.html` says nothing about the counters yet, and three of its sentences are
no longer the whole story once they send. This is a proposal for the owner to approve and edit
into the page; until then leave `GameConfig.countersUrl` empty. It is not legal advice. One
question to settle first: the save file keeps the day of the first start so the device can work
out day 1, 7 and 30, and EU law on reading and storing information on a device (ePrivacy
Directive Art. 5(3)) is stricter than the GDPR about that. Several countries exempt audience
measurement this limited, the Netherlands among them, but not all in the same words.

- **Last updated:** the day it goes live.
- **In short**, a new point before the last one: "The game sends our server a few anonymous
  counts, for example that a level was finished with two stars. They carry no ID and no name, and
  the server keeps only daily totals, so they can't be traced back to you."
- **Section 2**, in place of "It never leaves your device and we cannot see it.": "It also notes
  the day you first started the game and which of the counts in section 5 were already sent, so
  each is sent once. The file itself never leaves your device and we cannot see it."
- **Section 3**, after "The relay records only totals (…), without addresses or codes.": "It
  also adds to the daily counts of section 5 how many rooms were opened and how many players
  joined one, and whether from a browser or from the app."
- **A new section 5, "Anonymous counts"** (the sections after it move up one; "section 5" in
  section 1 becomes "section 6"):

  > To see whether the game works for its players (do they finish the tutorial, which levels
  > get played, do they come back), the game sends our server a count when:
  >
  > - it is started for the first time on a device;
  > - it is opened on the day of that first start, or 1, 7 or 30 days after it. Your device
  >   works that out itself and sends only the number 0, 1, 7 or 30;
  > - the tutorial is finished for the first time;
  > - a level is finished: which level (or that it was the daily or the ranked level), the stars
  >   earned, whether one or two players played, and whether it was an online game you hosted or
  >   joined;
  > - SHARE is pressed on the results screen;
  > - SHARE is pressed for an online room, to send a friend its link.
  >
  > Each count also says what the game runs on: a browser, Android, a PC or something else.
  >
  > A count carries no device, install, advertising or player ID, no nickname, no join code and
  > nothing you typed. Our server adds one to that day's total and keeps nothing else: no IP
  > address, no time of day, nothing about your device or browser. The totals can't be traced
  > back to a person, so there is nothing in them we could look up, correct or delete for you.
  >
  > As with any request over the internet, our server sees your IP address while it receives a
  > count. It keeps the address in memory for about a minute, to limit how many counts one
  > address can send, and never writes it to logs or to disk.
  >
  > No analytics service or software of another company is involved, and the counts are not
  > used for advertising.
  >
  > Legal basis: our legitimate interest in knowing how the game is used, so we can improve it
  > (Art. 6(1)(f) GDPR).

- **Section 7** (Other services), after the sentence that Unity's analytics are switched off:
  "The only figures about play that reach us from the game are the anonymous counts of
  section 5."
- **Section 9** (How long data is kept), a new point: "Anonymous counts: daily totals with no
  personal data in them, kept for as long as we work on the game."
