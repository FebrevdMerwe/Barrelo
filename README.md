<div align="center">

<img src="docs/banner.svg" alt="Barrelo — a platform for interactive dart experiences" width="100%" />

<br/>

![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)
![SignalR](https://img.shields.io/badge/live_updates-SignalR-1c2e29?logo=dotnet&logoColor=white)
![SQLite](https://img.shields.io/badge/storage-SQLite-2e7d5b?logo=sqlite&logoColor=white)
![Plugin architecture](https://img.shields.io/badge/games-plugin_architecture-c89b3c)
![Status](https://img.shields.io/badge/status-v1_in_progress-a5312a)

**A dart platform, not a scoring app.** Detection is decoupled from game rules, and game rules are decoupled
from the core — so new games (and new detectors) plug in without touching the platform.

[Overview](#overview) · [Quick start](#quick-start) · [Running a release package](#running-a-release-package) ·
[Playing without hardware](#playing-without-hardware) ·
[Configuration](#configuration) · [Adding a game](#adding-a-new-game) ·
[Adding a detector](#adding-a-new-dart-detector) · [Building your own package](#building-your-own-package) ·
[Project layout](#project-layout) · [Testing](#testing) · [Roadmap](#roadmap)

</div>

---

## Overview

Barrelo is a self-hosted dart platform built around one idea: **detecting a dart, running a game's rules, and
showing a scoreboard are three separate concerns.** Any detector (a real board, a hand-drawn dartboard
clicked in a browser, a mock stream) can drive any game, and any game can be added as a plugin without
recompiling — or even restarting — the core.

```mermaid
flowchart LR
    subgraph Detection["Detection sources (IDetectionSource)"]
        direction TB
        Board["Board Simulator<br/>(WebSocket)"]
        Manual["Manual entry<br/>(REST)"]
        Future["Real board detector<br/>(future adapter)"]
    end

    subgraph Core["Barrelo core"]
        direction TB
        Listener["DetectionListenerService"]
        Dispatch["IDispatcher"]
        Session["GameSessionManager"]
        Notify["GameHubNotifier"]
    end

    subgraph Plugins["Game plugins (IGame, loaded from /plugins)"]
        direction TB
        X01["X01"]
        Cricket["Cricket"]
        NewGame["Your game here"]
    end

    UI["Browser scoreboard<br/>(SignalR + REST)"]

    Detection --> Listener --> Dispatch --> Session --> Plugins
    Plugins --> Dispatch --> Notify --> UI
    UI -.manual throw / undo.-> Dispatch
```

**Core principles** (see [`SCOPE.md`](SCOPE.md) for the full vision):

- **Modular** — a game is a DLL dropped in `plugins/`, resolved at runtime. Adding one never means changing
  the core.
- **Detector-agnostic** — every detector, real or simulated, speaks the same `IDetectionSource` contract.
- **Fast & live** — every throw pushes an updated scoreboard over SignalR; no polling, no page refresh.
- **Hardware-optional** — a full match is playable from a browser with no board at all, via a virtual
  dartboard or the standalone Board Simulator.

## Features

- 🎯 **Plugin-based games** — ships with **X01** (301/501/701, double-out), **Cricket**, **Kickoff** and
  **Around The Clock** (race 1→20 then finish on the bull) today; see
  [Adding a new game](#adding-a-new-game) to add your own.
- 🖱️ **Play with zero hardware** — click a virtual SVG dartboard, or drive throws through the standalone
  **Board Simulator** tool over WebSocket.
- ⚡ **Live scoreboard** — every throw, undo, and turn change is pushed to every connected browser instantly
  via SignalR.
- 👥 **Roster & teams** — a drag-and-drop chalkboard for sorting players into teams/spectators, permanent
  players (persisted) and session-only "chalked" players (memory-only, gone when the process restarts).
  Deleted players can be undone within a few seconds.
  <!-- <img src="docs/screenshot-start.png" width="720" alt="Start-match chalkboard screenshot"> -->
- 🏆 **Session leaderboard** — every completed match awards placement points; a running leaderboard is shown
  in the win banner and can be reset per session.
  <!-- <img src="docs/screenshot-match.png" width="720" alt="Live match screenshot"> -->
- 🔌 **Dynamic plugin loading** — game DLLs load into a collectible `AssemblyLoadContext` at startup; delete
  or rebuild a plugin independently and the host picks it up without a solution-wide rebuild.
- 🗄️ **Local-first** — SQLite, no external services, no accounts, no cloud. Runs entirely on one machine next
  to the board.

> Screenshots aren't checked in yet — run the app locally (see [Quick start](#quick-start)) to see the
> chalkboard start screen and live scoreboard for yourself, or drop your own into `docs/` and update the
> `<img>` tags above.

## Quick start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (this repo targets `net10.0`; check with `dotnet --version`)
- Any editor — Visual Studio, Rider, or VS Code with the C# extension. The solution file is `Barrelo.slnx`.
- No database server, no Node/npm, no external services required.

### Clone, build, run

```bash
git clone <this-repo-url>
cd Barrelo

# restore + build everything (core, plugins, tools, tests)
dotnet build Barrelo.slnx

# run the API — applies EF Core migrations automatically on startup
dotnet run --project src/Barrelo.Api
```

The API starts at **http://localhost:5295** (see
[`src/Barrelo.Api/Properties/launchSettings.json`](src/Barrelo.Api/Properties/launchSettings.json)) and serves
the web UI itself — open that URL in a browser to reach the chalkboard start screen. In `Development`, an
OpenAPI/Scalar reference is also available at `/scalar`.

Every `dotnet build` of the solution copies each game plugin's compiled DLL and `ui/` assets into
`src/Barrelo.Api/plugins/{gameId}/` automatically (see [`Directory.Build.targets`](src/Games/Directory.Build.targets)) —
there's no separate "install a plugin" step for the games that ship in this repo.

## Running a release package

No .NET SDK, no clone, no build — just download and run:

1. Grab the latest release for your platform from [Releases](../../releases) and unzip it anywhere:
   - Windows: `Barrelo-*-win-x64.zip`
   - Linux (x64): `Barrelo-*-linux-x64.zip`
2. Run the Api:
   - Windows: double-click `Barrelo.Api.exe`, or run it from a terminal in that folder.
   - Linux: zip archives don't preserve the executable bit, so run
     `chmod +x Barrelo.Api tools/BoardSimulator/Barrelo.BoardSimulator` once, then `./Barrelo.Api`.

   It's self-contained — no separate .NET runtime install needed — and applies EF Core migrations to its own
   `barrelo.db` automatically on first launch.
3. Open **http://localhost:5295** in a browser to reach the chalkboard start screen (the same URL as a
   from-source run — see [Configuration](#configuration) to change it).
4. Optional — for board-simulator play, also run the Board Simulator from the unzipped folder (defaults to
   **http://localhost:5250**); the Api is configured to talk to it out of the box:
   - Windows: `tools\BoardSimulator\Barrelo.BoardSimulator.exe`
   - Linux: `./tools/BoardSimulator/Barrelo.BoardSimulator`

   Manual entry via the on-screen dartboard works either way, with or without the simulator running.

The package bundles the built-in game plugins (`plugins/x01`, `plugins/cricket`, `plugins/kickoff`,
`plugins/around-the-clock`) and the
Board Simulator tool together, so a full match is playable immediately with zero real hardware. Published for
win-x64 and linux-x64. To change ports, database location, or detection mode, edit `appsettings.json` next to
the Api executable — see [Configuration](#configuration).

### Running continuously on Linux (systemd)

For a headless box (e.g. a Proxmox LXC or a Raspberry Pi sitting next to the board), run the Api as a
`systemd` service so it survives reboots and crashes:

```ini
# /etc/systemd/system/barrelo.service
[Unit]
Description=Barrelo dart platform
After=network.target

[Service]
Type=simple
WorkingDirectory=/opt/barrelo
ExecStart=/opt/barrelo/Barrelo.Api
Restart=always
RestartSec=5
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

Then `systemctl daemon-reload && systemctl enable --now barrelo`. By default the Api only binds to
`localhost`; to reach it from other devices on your LAN, add `"Urls": "http://0.0.0.0:5295"` to an
`appsettings.Production.json` next to `Barrelo.Api` — see [Configuration](#configuration).

## Playing without hardware

Barrelo is designed to be fully playable with no physical dartboard, in two ways:

1. **Manual entry (always on).** The start screen's virtual SVG dartboard
   ([`wwwroot/dartboard.js`](src/Barrelo.Api/wwwroot/dartboard.js)) posts every click to
   `POST /api/detection/manual-throw`, independent of whichever streaming detector is configured. This is a
   first-class way to play, not a fallback.
2. **Board Simulator (`tools/Barrelo.BoardSimulator`).** A standalone, dependency-free app that stands in for
   a real detector behind the exact same `IDetectionSource` contract a real board adapter would use. Run it
   alongside the API to drive throws over the same WebSocket path a hardware detector eventually will:

   ```bash
   # terminal 1 — the simulator (defaults to http://localhost:5250)
   dotnet run --project tools/Barrelo.BoardSimulator

   # terminal 2 — the API, configured to consume it (see Configuration below)
   dotnet run --project src/Barrelo.Api
   ```

   With `Detection:Mode` set to `Simulator` (the shipped default in
   [`appsettings.json`](src/Barrelo.Api/appsettings.json)), throws made in the simulator's own browser tab
   appear live on any match bound to the simulator board.

## Configuration

All configuration lives in [`src/Barrelo.Api/appsettings.json`](src/Barrelo.Api/appsettings.json) (and the
`.Development.json` override), following standard ASP.NET Core conventions — override any key with an
environment variable (`Detection__Mode=Mock`) or `appsettings.Production.json` for a real deployment.

| Key | Default | Meaning |
|---|---|---|
| `ConnectionStrings:BarreloDb` | `Data Source=barrelo.db` | SQLite connection string. |
| `Plugins:Directory` | `plugins` | Folder (relative to the Api's working directory) scanned for game plugin DLLs on startup. |
| `Detection:Mode` | `AutoDarts` | Which streaming `IDetectionSource` to run: `AutoDarts` (a local AutoDarts board manager), `Simulator` (Board Simulator over WebSocket) or `Mock` (in-process, driven only by tests/code). Manual REST entry works regardless of this setting. |
| `Detection:Simulator:Url` | `ws://localhost:5250/stream` | WebSocket endpoint of a running `Barrelo.BoardSimulator` instance. |
| `Detection:AutoDarts:Url` | `ws://localhost:3180/api/events` | Event-stream endpoint of the local AutoDarts board manager. The connection reconnects with backoff on its own; the header pill shows whether it is currently up. |

## Adding a new game

A game plugin is a small class library that references **only** [`Barrelo.GameSdk`](src/Barrelo.GameSdk) —
never `Barrelo.Domain`, `Barrelo.Application`, or `Barrelo.Api`. That one-way boundary is what makes "add a
game" not require touching the core; `Barrelo.Games.X01` and `Barrelo.Games.Cricket` are the two reference
implementations to copy from.

1. **Create the project** under `src/Games/`, e.g. `src/Games/Barrelo.Games.YourGame/`, referencing only
   `Barrelo.GameSdk` and setting `<GameId>` — this is the only MSBuild wiring a new game needs:

   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <ItemGroup>
       <ProjectReference Include="..\..\Barrelo.GameSdk\Barrelo.GameSdk.csproj" />
     </ItemGroup>
     <PropertyGroup>
       <TargetFramework>net10.0</TargetFramework>
       <ImplicitUsings>enable</ImplicitUsings>
       <Nullable>enable</Nullable>
       <GameId>yourgame</GameId>
     </PropertyGroup>
   </Project>
   ```

   The shared [`Directory.Build.targets`](src/Games/Directory.Build.targets) picks up `<GameId>` and copies
   the built DLL (plus an optional `ui/` folder) into `Barrelo.Api/plugins/{GameId}/` after every build.

2. **Implement `IGameFactory`** — describes the game to the catalog and creates instances:

   ```csharp
   public sealed class YourGameFactory : IGameFactory
   {
       public const string GameId = "yourgame";

       public GameDescriptor Describe() => new(
           GameId,
           "Your Game",
           "One-line description shown on the start screen.",
           new GameSettingDefinition[]
           {
               // Optional: a GameModeSetting (radio choices merged into GameSetup.Options)
               // and/or a PlayerGroupSetting (declares fixed team buckets, e.g. teams of up to 4).
           });

       public Task<IGame> Create(GameSetup setup, CancellationToken ct)
       {
           if (setup.PlayerIds.Count == 0)
               throw new GameRuleViolationException("Your game requires at least one player.");

           return Task.FromResult<IGame>(new YourGame(setup.PlayerIds));
       }
   }
   ```

3. **Implement `IGame`** — the rules engine. It's **pull-based**: the host calls in
   (`ReceiveThrow`/`ReceiveEndOfTurn`/`UndoLastThrow`) and pulls state out (`GetState`/`GetResult`); a plugin
   never raises callbacks into host code, which is what keeps it safely unloadable from its
   `AssemblyLoadContext`.

   ```csharp
   public interface IGame
   {
       Task ReceiveThrow(DetectedThrow detectedThrow, CancellationToken ct);
       Task ReceiveEndOfTurn(CancellationToken ct);
       Task UndoLastThrow(CancellationToken ct);
       Task<GameStateSnapshot> GetState();
       bool IsComplete { get; }
       Task<GameResult> GetResult();
   }
   ```

   Put whatever your game needs to track (remaining score, marks hit, target progression...) into
   `GameStateSnapshot.Payload` — the envelope itself (`MatchId`, `Status`, `CurrentPlayerId`, `LegNumber`,
   `SetNumber`, `RecentThrows`, `IsComplete`, `WinnerPlayerIds`) is deliberately game-agnostic and must stay
   that way. Throw malformed input as `GameRuleViolationException`; the host turns it into a `400` response,
   never a crash.

4. **Ship a board UI.** Drop a `ui/render.js` next to your game's `.csproj` defining
   `window.renderGameBoard(container, snapshot)` — it's called on every state push with the `#game-board`
   element and the parsed `GameStateSnapshot`, and is what actually draws your game's scores/targets/board
   state. Both `view.js` (the passive TV scoreboard) and `control.js` (the interactive dartboard/scoring
   page) load it the same way. It's technically optional: if `render.js` is missing, they fall back to a raw
   key/value dump of `Payload` so the match is never unplayable, but that fallback is a debugging aid, not a
   real UI — ship a `render.js` for anything you want players to actually look at.

5. **Deploy it.** A game plugin doesn't need to live in this solution at all — `Barrelo.Games.X01` and
   `Barrelo.Games.Cricket` are only wired into `Barrelo.slnx`/`Barrelo.Api.csproj` because they ship as the
   built-in reference games. For any other game, just build your project and copy the output DLL, `render.js`,
   and `style.css` (if any) into `plugins/{gameId}/` next to the running Api:

   ```
   src/Barrelo.Api/plugins/
     yourgame/
       Barrelo.Games.YourGame.dll
       render.js      (optional)
       style.css      (optional)
   ```

   The plugin loader picks it up from there on the next Api startup, and the game appears in the start
   screen's game picker via `GET /api/games` with no core changes and no rebuild of the solution.

   **Vendoring a prebuilt plugin in this repo.** The copy above is for a deployment you don't want to
   rebuild — it lands on a running server's filesystem and isn't tracked anywhere. If instead you want the
   plugin to ship with every clone/publish of this repo (without pulling its source into `src/Games/`), drop
   the same built output into [`external-plugins/{gameId}/`](external-plugins/README.md) at the repo root
   instead. It's git-tracked, and `Barrelo.Api.csproj`'s existing plugin-copy targets fold it into
   `plugins/{gameId}/` automatically on every `dotnet build`/`dotnet run`/`dotnet publish` — no manual
   copying, no solution/project wiring, no source included.

### Client-owned games (any UI engine, no .NET)

The steps above load a game as a .NET DLL in-process. If you'd rather write your rules in TypeScript and
render the board with Phaser, PixiJS, a Unity WebGL build, or anything else, use the **client-owned** path
instead — it's additive alongside the in-process plugin loader above, not a replacement for it.

A client-owned game is **entirely a browser app**. There is no server half, no process for Barrelo to
spawn, and no HTTP contract to implement. Barrelo records what was thrown and pushes that log to your
board; your board works out what it means. Deployment is a manifest plus a folder of static files — the
machine running Barrelo needs no Node, no Python, and no runtime of any kind beyond the browser already
showing the scoreboard.

**Copy [`templates/barrelo-phaser-game`](templates/barrelo-phaser-game) as your starting point** — a
TypeScript + Vite + Phaser skeleton with all the wiring in place and the rules/rendering left as TODOs.
What follows is the contract it implements.

1. **Drop a `plugin.json` manifest** in `plugins/{gameId}/` (same folder convention as an in-process
   plugin's DLL) — or, to vendor it into this repo instead of a running deployment,
   [`external-plugins/{gameId}/`](external-plugins/README.md) works the same way:

   ```json
   {
     "protocolVersion": 2,
     "gameId": "yourgame",
     "displayName": "Your Game",
     "description": "One-line description shown on the start screen.",
     "stateOwner": "client",
     "settings": []
   }
   ```

   `settings` is the same `GameSettingDefinition` shape (`GameModeSetting`/`PlayerGroupSetting`) a .NET
   `GameDescriptor` already uses — no separate schema.

   **The containing folder's name must match `gameId` exactly** (`plugins/yourgame/plugin.json` for
   `"gameId": "yourgame"`), because UI assets are fetched from `/plugins/{gameId}/...`. A mismatch
   silently 404s `ui/index.html` while the game still appears in the picker; Barrelo logs a warning on
   startup if it detects this.

2. **Ship your board as `ui/index.html`.** The shell embeds it in a sandboxed `<iframe>` and talks to it
   over `postMessage`. Barrelo pushes state down on every change:

   ```jsonc
   { "type": "barrelo:gameState",
     "snapshot": { /* GameStateSnapshot; the interesting part is payload */ },
     "playerNames": { "<playerId>": "Alex" } }
   ```

   `snapshot.payload` is everything Barrelo knows about the match, which is deliberately only who's
   playing, how it was set up, and what has been thrown:

   ```jsonc
   { "seed": 1234,
     "playerIds": ["…"],
     "options": { "startingScore": "501" },
     "playerGroups": { "<playerId>": 0 },
     "visits": [ { "throws": [ /* DetectedThrow */ ], "ended": true },
                 { "throws": [ /* … */ ],            "ended": false } ] }
   ```

   The log is grouped into **visits** — one player's turn at the board — rather than kept flat, because
   that's the shape darts actually has, and because detectors like AutoDarts report a whole visit at a
   time. A visit is open (`ended: false`) until the turn boundary arrives; only the last one can be open.
   Visits are created lazily, on the first dart, so an open-but-empty visit never appears.

   Note what a visit deliberately does **not** carry: a player id. Whose turn it is is a *rules* decision
   — an elimination game skips dead players, a "hit a bull, throw again" game doesn't rotate at all — so
   the host doesn't guess. For the same reason `snapshot.currentPlayerId` is always `null` and
   `legNumber`/`setNumber` are always `1` for a client-owned game. Derive them from your own replay.

   Undo needs no code on your side: Barrelo shortens the log (popping the last dart, or re-opening the
   visit if the last thing that happened was the turn ending) and you re-derive from what's left.

3. **Send two messages back up.** Your rules know things the host doesn't, so the iframe talks back:

   | Message | When | Effect |
   |---|---|---|
   | `barrelo:display` | every state change | Drives the chrome around your board: `currentPlayerId`, `legNumber`, `setNumber`, `visitThrows` (the dart 1/2/3 slots), `deadTargets` (segments to grey out on the input dartboard). All fields optional; anything omitted falls back to what the host already shows. Advisory — never trusted for scoring. |
   | `barrelo:matchComplete` | once, when your rules say the match is over | `{ winnerPlayerIds, finalStandings }`. Ends the session and awards session-leaderboard points by placement. Both lists are validated against the match roster and rejected if they don't match. |

   `barrelo:display` should also carry `logHash` and `stateHash` — see the determinism contract below.

4. **Determinism is a contract, not a suggestion.** Barrelo keeps the log; **every screen replays it
   independently** — the control tablet, the TV, and any tab refreshed mid-match. They agree only if
   replaying the same log always produces the same state. So inside your replay:

   - No `Math.random()` — derive randomness from `payload.seed`, which is fixed for the match and
     identical on every screen.
   - No `Date.now()` / `new Date()` — use `throw.detectedAtUtc` from the log.
   - No `crypto.randomUUID()` — derive ids from the log (e.g. `throw.throwId`).
   - No module-level mutable state, no `localStorage`, no `fetch`.

   Break one of these and the TV quietly shows something different from the tablet. Because that is
   near-impossible to diagnose after the fact, each screen reports a hash of the log it replayed plus a
   hash of the state it derived; Barrelo logs a warning when the *same* log yields *different* states.
   Keying on the log hash is what keeps it quiet for screens that are simply observing the match a moment
   apart.

5. **If a client-owned game misbehaves**, there is no process to crash and nothing to abort — the match
   simply doesn't progress, and reloading the page rebuilds state from the log via
   `GET /api/session/current`.

Automated tests never launch a browser or require Node — `dotnet test` covers the host half (the visit
log, undo semantics, manifest loading) directly, keeping the primary correctness gate pure-.NET.

**Known limitation.** Protocol versioning is all-or-nothing: `ClientGameLoader` checks a manifest's
`protocolVersion` against a single supported constant and skips the game entirely on a mismatch, so
bumping the protocol is a breaking change for every existing plugin until they're updated.

### Walkthrough: building your first client-owned game

The contract above is easier to follow with a concrete example. This is a complete, minimal game — "first
to hit a bullseye wins" — in one HTML file with no build step and no dependencies.

1. **Write the manifest.** Create `plugins/bullseye-duel/plugin.json`:

   ```json
   {
     "protocolVersion": 2,
     "gameId": "bullseye-duel",
     "displayName": "Bullseye Duel",
     "description": "First player to hit any bullseye wins.",
     "stateOwner": "client",
     "settings": []
   }
   ```

2. **Write the board.** Create `plugins/bullseye-duel/ui/index.html`. The whole game is the `replay()`
   function — a pure fold over the visit log:

   ```html
   <!doctype html>
   <html>
     <head>
       <meta charset="utf-8" />
       <style>
         body { font-family: sans-serif; margin: 0; padding: 2rem; background: #111; color: #eee; }
         .current { color: #ffd54f; font-weight: bold; }
       </style>
     </head>
     <body>
       <h1 id="message">Waiting for the match to start…</h1>
       <p>Current player: <span id="current" class="current">–</span></p>
       <p>Darts thrown: <span id="darts">0</span></p>

       <script>
         let reportedComplete = false;

         function isBullseye(t) {
           return t.segment === 25 && (t.ring === "Single" || t.ring === "Double");
         }

         // The entire rules engine: same log in, same state out, every time.
         function replay(payload) {
           const players = payload.playerIds;
           let turnIndex = 0;
           let winner = null;
           let dartsThrown = 0;

           for (const visit of payload.visits) {
             for (const t of visit.throws) {
               dartsThrown++;
               if (!winner && isBullseye(t)) winner = players[turnIndex];
             }
             if (visit.ended || visit.throws.length >= 3) {
               turnIndex = (turnIndex + 1) % players.length;
             }
           }

           return { winner, dartsThrown, currentPlayerId: winner ? null : players[turnIndex] };
         }

         window.addEventListener("message", (event) => {
           if (event.data?.type !== "barrelo:gameState") return;
           const { snapshot, playerNames } = event.data;
           const state = replay(snapshot.payload);

           document.getElementById("message").textContent =
             state.winner ? "We have a winner!" : "Aiming for the bull...";
           document.getElementById("current").textContent =
             state.currentPlayerId ? (playerNames[state.currentPlayerId] ?? "–") : "–";
           document.getElementById("darts").textContent = state.dartsThrown;

           // Tell Barrelo whose turn it is — it has no way to know.
           window.parent.postMessage(
             { type: "barrelo:display", currentPlayerId: state.currentPlayerId },
             window.location.origin);

           if (state.winner && !reportedComplete) {
             reportedComplete = true;
             const losers = snapshot.payload.playerIds.filter((id) => id !== state.winner);
             window.parent.postMessage(
               { type: "barrelo:matchComplete",
                 winnerPlayerIds: [state.winner],
                 finalStandings: [state.winner, ...losers] },
               window.location.origin);
           }
         });
       </script>
     </body>
   </html>
   ```

3. **Try it out.** With `plugins/bullseye-duel/{plugin.json,ui/index.html}` in place:

   ```bash
   dotnet run --project src/Barrelo.Api
   ```

   Open `http://localhost:5295` — the chalkboard start screen — and "Bullseye Duel" should appear in the
   game picker. Add at least two players and click **Start match**.

   Play it with **manual entry** — no Board Simulator needed — by clicking segments on the on-page
   dartboard, or by calling the same endpoint it uses:

   ```bash
   curl -X POST http://localhost:5295/api/detection/manual-throw \
     -H "Content-Type: application/json" \
     -d '{ "segment": 25, "ring": "Double" }'
   ```

   The scoreboard should immediately show a win banner for whichever player was up. If it doesn't, check
   the browser console first (your rules run there now), then the Api's console — a manifest that fails
   validation is logged there rather than surfaced in the browser.

From here, [`templates/barrelo-phaser-game`](templates/barrelo-phaser-game) is a fuller TypeScript/Vite
scaffold to copy if you want a real rendering engine instead of a hand-rolled HTML page.

## Adding a new dart detector

Every detector — a real board, the Board Simulator, manual entry — is just another implementation of
`IDetectionSource` in `Barrelo.Application`:

```csharp
public interface IDetectionSource
{
    DetectionSourceType SourceType { get; }
    IAsyncEnumerable<DetectionEvent> EventsAsync(CancellationToken ct);
    Task<bool> IsConnectedAsync();
}
```

`SourceType` and `IsConnectedAsync()` are what the board pill in the header rail renders, via
`GET /api/detection/status` on load and a `DetectionStatusChanged` push on the game hub thereafter. Naming
a detector on screen is a lookup in `wwwroot/board-status.js`, so a new detector needs one entry there and
nothing else UI-side.

1. **Implement the interface** under `src/Barrelo.Infrastructure/External/Detection/`. If your detector
   speaks WebSocket, derive from
   [`WebSocketDetectionSource`](src/Barrelo.Infrastructure/External/Detection/WebSocketDetectionSource.cs)
   and implement `ParseMessage` alone — connecting, reconnecting with backoff, keep-alive with a ping
   deadline, dropping an uninterpretable message without losing the socket, and publishing connection
   transitions are all handled for you. Otherwise connect however your hardware/API talks (HTTP polling, a
   native SDK) and map every incoming event onto the canonical, detector-agnostic `DetectedThrow`. See also
   [`AutoDartsDetectionSource.cs`](src/Barrelo.Infrastructure/External/Detection/AutoDartsDetectionSource.cs)
   for an example against a real third-party detector, including diffing a source that reports the
   *cumulative* darts of a visit on every event rather than one message per dart:

   ```csharp
   public sealed record DetectedThrow(
       Guid ThrowId, int Segment, Ring Ring, int Score, string RawNotation,
       BoardPosition Position, double? Confidence, string BoardId, int? CameraIndex,
       DateTimeOffset DetectedAtUtc, DetectionSourceType Source);
   ```

   Yield a `DetectionEvent` of type `Throw` (carrying the mapped `DetectedThrow`) or `EndOfTurn` for whatever
   your source uses as a turn-boundary signal. `DartScoring.Score(ring, segment)` and
   `BoardGeometry.CenterOf(segment, ring)` in `Barrelo.GameSdk` are available if your source doesn't already
   provide a computed score or a click position.

2. **Publish connection transitions** if the source is a persistent connection, as
   `DetectionEvent.ConnectionChanged(boardId, isConnected)` on the same stream as the darts.
   `WebSocketDetectionSource` already does this; a source that connects some other way should do the same,
   so a board that goes away says so on every screen instead of just going quiet.

3. **Register it behind the `Detection:Mode` switch** in
   [`DependencyInjection.cs`](src/Barrelo.Infrastructure/DependencyInjection.cs):

   ```csharp
   else if (string.Equals(detectionMode, "YourDetector", StringComparison.OrdinalIgnoreCase))
   {
       services.AddSingleton<IDetectionSource>(sp => new YourDetectorDetectionSource(/* ... */));
   }
   ```

4. **Set `Detection:Mode`** to your new value in `appsettings.json` (or an environment-specific override) to
   switch the running `DetectionListenerService` over to it. No changes to any game plugin, endpoint, or UI
   code are needed — the whole platform only ever depends on the `IDetectionSource` abstraction.

## Building your own package

To publish for a different RID, or with your own configuration baked in:

```bash
# self-contained, single-machine deployment (adjust the RID for your target device, e.g. linux-arm64 for a Pi)
dotnet publish src/Barrelo.Api/Barrelo.Api.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -o ./publish
```

- `dotnet publish` builds every referenced game plugin first (via the build-order-only project references)
  and the `Directory.Build.targets` copy step lands each one in `plugins/{gameId}/` inside the publish
  output — no manual plugin packaging step.
- The app migrates the SQLite database automatically on startup; no separate migration step is needed for a
  fresh deployment.
- Point `Detection:Mode`/`Detection:Simulator:Url` at your target environment via
  `appsettings.Production.json` or environment variables before shipping.
- Run the published `Barrelo.Api` executable (or `dotnet Barrelo.Api.dll` for a framework-dependent publish)
  on the device that sits next to the board — see [Configuration](#configuration) for what to change first.
- Deploying to a home server (e.g. a Proxmox LXC) over SSH? See [`deploy/README.md`](deploy/README.md) for a
  one-command `linux-x64` publish-and-restart script.

## Project layout

```
src/
  Barrelo.Domain              domain entities/value objects — no dependency on GameSdk
  Barrelo.Application         commands/queries, in-house dispatcher, IDetectionSource/IGameCatalog contracts
  Barrelo.Infrastructure       EF Core (SQLite), plugin loader (ALC), detection sources, SignalR notifier plumbing
  Barrelo.Api                 minimal-API endpoints, SignalR hub, static wwwroot UI, plugin static-asset hosting
  Barrelo.GameSdk              dependency-free plugin contracts — the entire boundary a game plugin sees
  Games/
    Barrelo.Games.X01          reference game: classic 301/501/701
    Barrelo.Games.Cricket       reference game: standard Cricket
    Barrelo.Games.Kickoff       reference game: one shared ball, two goals
    Barrelo.Games.AroundTheClock  reference game: 1→20 then the bull, doubles/trebles jump
tests/
  Barrelo.*.UnitTests / .IntegrationTests   one per src/ project, plus tests/Games/* per game plugin
tools/
  Barrelo.BoardSimulator       standalone, zero-Barrelo-dependency stand-in for a real detector
external-plugins/              vendored, prebuilt game plugin packages (no source) — see its own README
diagrams/                     architecture diagrams (Mermaid)
docs/                         README assets
```

See [`PLAN.md`](PLAN.md) for the full architectural rationale (why an in-house dispatcher instead of MediatR,
why plugins load via a collectible `AssemblyLoadContext`, the detection-source design history, etc.) and
[`SCOPE.md`](SCOPE.md) for the long-term product vision this v1 is scoped down from.

## Testing

```bash
dotnet test Barrelo.slnx
```

Test projects mirror the solution layout 1:1 — `Barrelo.Domain.UnitTests`, `Barrelo.Application.UnitTests`,
`Barrelo.GameSdk.UnitTests`, `Barrelo.Infrastructure.IntegrationTests`, `Barrelo.Api.IntegrationTests`, and
`tests/Games/Barrelo.Games.X01.UnitTests` / `Barrelo.Games.Cricket.UnitTests` /
`Barrelo.Games.Kickoff.UnitTests` / `Barrelo.Games.AroundTheClock.UnitTests` for the rules engines. The
integration tests script full matches end-to-end (mock stream and pure manual entry) through the real
dispatcher/plugin-loader stack — the primary correctness gate before any UI change.

## License

Licensed under the [Apache License 2.0](LICENSE).
