# Ranked boards server

Hands out attempts at the day's ranked level, opens and replays the run logs that come back with
the game's own model code, and keeps a solo and a co-op board a day. Design and the reasons:
`docs/ranked.md`. The relay (`server/relay`) is a separate service on the same VPS.

```
Ranked/            ASP.NET Core app: Program.cs (endpoints), RankedService.cs (the rules of the
                   boards), Store.cs (SQLite), Levels.cs (the campaign), Limits.cs (per-IP limits)
Ranked.Tests/      xUnit tests against the app in memory, on a one-street test level
Dockerfile         built on the VPS, runs on the chiseled ASP.NET image as non-root
compose.yaml       the container behind the VPS's existing Caddy container
deploy/            deploy.sh, rollback.sh, install.sh (runs on the VPS)
```

`Ranked.csproj` compiles every `Assets/Scripts/Demolition/**/Model/` file of the game with
`tools/unity-check/UnityShim.cs`, and ships `Assets/Content/*/Levels` and `Archetypes`: the
server builds a day's level as the game does and checks a run with `RunVerifier` and
`RankedRules`. So **deploy the server with the game build whose rules it checks**:
`RankedRules.Version` must match, and an older build is told to update (426).

## Local development

Needs the .NET 10 SDK.

```
cd server/ranked
Ranked__Database=/tmp/ranked.db Ranked__BackupFolder= dotnet run --project Ranked   # http://localhost:5090
dotnet test Ranked.Tests
```

Point the game at it with `GameConfig.rankedUrl` (`http://localhost:5090`).

## API

JSON, field names as in `Ranked/Model/RankedApi.cs` (shared with the game). Errors are
`{"error": "..."}` with the status.

| Request | Answer |
|---|---|
| `POST /start {player, name, mode, partner, partnerName, rules}` | `{attempt, key, day, template, seed, mode}`: the day's level (UTC), and the attempt's sealing key. 400 bad ids or mode, 426 another `rules`, 429 limits. A name that `PlayerName` doesn't allow is stored as the player's generated one. |
| `POST /submit {attempt, log}` | `{rank, of, score, best}`. The log is base64, sealed with the attempt's key (`Seal`). 400 not sealed with that key, changed, or unreadable; 404 no such attempt; 409 already sent; 410 older than `AttemptLifetime`. A run the replay turns down is answered as if it passed (docs/ranked.md K-D5). |
| `GET /board?mode=solo&day=&player=` | `{day, template, seed, mode, players, entries[], you}`: each player's best run, best first (score, then time), the top `BoardSize`; `you` is the asking player's (rank 0: none). |
| `GET /ghost?mode=solo&day=` | `{run, name, score, log}`: the best verified run's log (unsealed), or 204. |
| `POST /report {player, run}` | 204. One report per player and run. |
| `GET /health` | `ok` |

**Review**, with the admin token (`Authorization: Bearer <token>`; without it these are 404):

| Request | |
|---|---|
| `GET /admin/runs?limit=100` | The latest runs: verdict and reason, held or not, reports, claimed and replayed score. |
| `POST /admin/runs/{run}/approve` | Show a run held for being far above the rest. |
| `POST /admin/players/{player}/hide` / `show` | Hide a player's runs from everyone else, or show them again. |

```
curl -s -H "Authorization: Bearer $TOKEN" https://ranked.oeds.space/admin/runs | jq '.[] | select(.ok == false)'
```

## Configuration

`Ranked/appsettings.json`, section `Ranked`; environment variables override it as
`Ranked__<Key>`. On the VPS put them in `/opt/demolition-ranked/ranked.env` (deploys never
touch it), then `cd /opt/demolition-ranked && sudo docker compose up -d`.

| Key | Default | |
|---|---|---|
| `HideRejected` | false | docs/ranked.md K-D5. Off for the first weeks: every run shows, the reasons are only recorded. Read `/admin/runs` for runs turned down, fix the false ones, then set it to `true`: a turned-down run's player is then seen only by themselves. |
| `HoldFactor` / `HoldMinEntries` | 2 / 3 | A score this many times the day's best (once the board has this many) waits for `approve`. |
| `AttemptsPerDay` | 200 | Per player. |
| `StartsPerMinutePerIp` / `NewPlayersPerDayPerIp` / `ReportsPerDayPerIp` | 30 / 5 / 30 | In memory only. |
| `AttemptLifetime` | 00:40:00 | A log must come back within this. |
| `BoardSize` | 50 | |
| `AdminToken` | empty (no /admin) | `install.sh` writes a random one into `ranked.env` on the first deploy. |
| `Database` / `BackupFolder` / `BackupsKept` | `/data/ranked.db` / `/data/backups` / 7 | A copy of the database every day; `/data` is `/opt/demolition-ranked/data` on the host. |
| `RetentionDays` | 180 | Runs, attempts, reports and players with no run left are forgotten after this; the privacy policy says 180 days, so change both. |
| `TrustedProxyNetworks` | empty | The deploy sets it to the subnet of Caddy's Docker network, so per-IP limits see players, not the proxy. |

## Deploy to the VPS

Like the relay (its README has the details of the Caddy container, the network and Tailscale
SSH), on its own name, so the relay's Caddy block stays as it is.

1. **DNS:** an A record `ranked.oeds.space → 138.201.155.228`, and wait until it resolves.
2. `server/ranked/deploy/deploy.sh og@hetzner-vps ranked.oeds.space`

It uploads this folder, the game's model code and the campaign's JSON; on the VPS it copies them
to `/opt/demolition-ranked`, keeps `data/` and `ranked.env`, builds and starts the container
`demolition-ranked` (read-only, capabilities dropped, 384 MB), checks Caddy reaches it, appends
`ranked.oeds.space { reverse_proxy demolition-ranked:5090 }` to the Caddyfile once (validated,
backup kept) and reloads Caddy. **Update:** run it again. **Roll back:**
`server/ranked/deploy/rollback.sh og@hetzner-vps`. **Back up** `/opt/demolition-ranked/data`
(it has the database and its daily copies).
