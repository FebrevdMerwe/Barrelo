# Darts Platform — v1 Implementation Plan

## Context

`SCOPE.md` lays out a long-term vision (community games, online play, AI coaching, wearables, tournaments...) but makes zero technical commitments — no detection strategy, no tech stack, no MVP boundary. Before any code can be written, those decisions had to be made. A grilling session with the user established:

- **v1 goal**: a working detection → game logic → scoreboard loop for one traditional game (classic 501), plus the plugin architecture that proves a second game can be added later *without modifying the core*. That plugin architecture, not the game itself, is the actual deliverable.
- **Detection**: the user's initial idea was to "fork AutoDarts." Research showed AutoDarts' core detection engine is closed-source (binary-only distribution; only a docs repo is public on GitHub), so that's not literally possible. Instead we're forking **OpenDartboard** (github.com/OpenDartboard/OpenDartboard) — a genuinely open-source (GPL-3.0), C++/OpenCV detection engine that runs on a Raspberry Pi Zero 2 W with 3 USB webcams and streams detected throws over a WebSocket API (confirmed against `docs/api.md`). It's early-stage (v0.1.4, scoring detection itself marked WIP), and the user has no hardware yet — so v1 must be fully developable against a mock/manual detection source, with the real adapter validated against replayed sample JSON rather than physical hardware.
- **Why GPL-3.0 is fine**: the .NET platform talks to the (forked) detection engine over a network boundary (WebSocket), not by linking its code in-process, so GPL's copyleft obligations stay contained to the detection engine itself and don't propagate into the .NET platform — important since the user is building toward a real, potentially commercial product. *Distribution nuance:* Phase 4 ships a startup script that launches the forked GPL binary next to `Darts.Api`. Shipping them side by side is "mere aggregation" (fine), but it does carry the obligation to offer the source of *that binary* — trivially satisfied since it's the user's own public fork. It's a distribution obligation to be aware of, not a zero-obligation situation.
- **Deployment**: single device runs everything (detection process + .NET core + web UI) next to the board.
- **UI**: web app first, with live updates (a dart thrown should update the scoreboard immediately).
- **Accounts**: local player profiles only, no auth/login, no cloud — deliberately minimal for v1.
- **Stack**: .NET core (user's strongest language), following the existing `new-dotnet-project` DDD scaffold convention (`~/.claude/commands/new-dotnet-project.md`) that's already used for other projects.

The plugin architecture is designed so that "add a new game" and "add a new detection source" are both proven extension points in v1, not promises deferred to later — this is the concrete test of whether the platform's core principle (detection separated from game logic, games independent from the platform) actually holds up.

---

## Solution layout

Root: `C:\Projects\Darts` (already exists, contains only `SCOPE.md`, not yet a git repo). Solution `Darts.sln`, root namespace `Darts`.

Run the `new-dotnet-project` scaffold conventions **in place** in `C:\Projects\Darts` (adapt the skill's steps — skip creating a new directory under `~/projects`, since this directory and its SCOPE.md already exist) to produce the standard four layers + test projects, then layer these additions/deviations on top:

```
src/
  Darts.Domain
  Darts.Application
  Darts.Infrastructure
  Darts.Api
  Darts.GameSdk                      ← NEW: slim, dependency-free plugin contracts library
  Games/
    Darts.Games.X01                  ← NEW: first reference game plugin
tests/
  Darts.Domain.UnitTests
  Darts.Application.UnitTests
  Darts.Infrastructure.IntegrationTests
  Darts.Api.IntegrationTests
  Darts.GameSdk.UnitTests            ← NEW
  Games/
    Darts.Games.X01.UnitTests        ← NEW (bulk of rules-engine tests live here)
```

**Deviations from the default scaffold:**
- Infrastructure: swap `Microsoft.EntityFrameworkCore.SqlServer` → `Microsoft.EntityFrameworkCore.Sqlite` (single-device local deployment, no server needed).
- `Darts.GameSdk`: **zero** project references, no MediatR/EF/ErrorOr — BCL + `System.Text.Json` only. This is the only assembly a game plugin author (including a future third party) needs to reference.
- `Darts.Application` references `Darts.GameSdk` in addition to `Darts.Domain`. `Darts.Domain` does **not** reference GameSdk — keep the domain model pure; GameSdk is a sibling contracts library.
- `Darts.Api` also references `Darts.GameSdk` (hosts the plugin loader, shapes SignalR payloads).
- `Darts.Games.X01` references **only** `Darts.GameSdk` — this is what proves the plugin boundary is real, not aspirational.
- No separate SPA/front-end project for v1 — the web UI is static files served from `Darts.Api/wwwroot` (see UI section for why).

---

## Game plugin architecture

**In-process loading via `AssemblyLoadContext` (ALC)**, not out-of-process game services. Out-of-process (a game as its own microservice) is the right answer once untrusted third-party/community plugins are real (a listed future goal), but for v1 — solo dev, single device, games written by the platform author — it's infrastructure with no current payoff. The migration path later is clean specifically *because* the `IGame` contract lives in a dependency-free SDK assembly rather than in Domain/Application types: only the hosting mechanism changes later, not the contract.

**ALC gotcha to design around now:** types crossing the boundary (`DetectedThrow`, `GameStateSnapshot`, etc.) must resolve to the same assembly instance on both sides, or identical-looking types throw `InvalidCastException`. Fix: the plugin's `AssemblyLoadContext.Load` override returns `null` for any assembly already loaded in the default context (i.e. `Darts.GameSdk`), forcing resolution against the host's copy. Only the plugin's own DLL loads into its collectible context.

### `Darts.GameSdk` contents
- `DetectedThrow` — canonical throw event, shared verbatim between the detection subsystem and game logic (no mapping layer needed since both sit on the same "outer, replaceable" side of the boundary relative to Domain).
- `DetectionEventType` (`Throw | EndOfTurn`) — OpenDartboard's wire protocol has an explicit `"END"` marker for visit-complete, so the contract needs a real turn-boundary signal, not "3 throws = turn."
- `DetectionEvent` (Application-side wrapper, not GameSdk) — a discriminated envelope: `DetectionEventType Type` + a nullable `DetectedThrow Throw` (populated only when `Type == Throw`). `IDetectionSource.EventsAsync` yields these; the listener switches on `Type` to dispatch `RecordDetectedThrowCommand` vs `RecordEndOfTurnCommand`. Stated explicitly here because the split-command design otherwise leaves the on-the-wire event shape implicit.
- `IGameFactory` — `GameDescriptor Describe()` + `IGame Create(GameSetup setup)`. Split from `IGame` so the host can list available games without instantiating one.
- `GameSetup` — ordered player list + a loosely-typed options blob (`IReadOnlyDictionary<string,string>` or JSON) that only the plugin interprets (e.g. X01's `startingScore`, `doubleOut`, `legsToWin`). This is what makes "new game, different config shape, zero core changes" literally true.
- `IGame` — **pull-based, not event-based**: `ReceiveThrow(...)`, `ReceiveEvent(EndOfTurn)`, `UndoLastThrow()` (required, not optional — scoring UIs need this), `GetState() → GameStateSnapshot`, `IsComplete`, `GetResult()`. Pull-based deliberately: an `IGame` in a collectible ALC raising .NET events back into host code creates cross-boundary delegate references that block ALC unloading and create lifetime bugs. Synchronous call-in/pull-out keeps the plugin passive and the host in control of when state gets pushed to SignalR.
- `GameStateSnapshot` — universal envelope (`MatchId`, `GameId`, `Status`, `CurrentPlayerId`, `LegNumber`, `SetNumber`, `RecentThrows[]`, `IsComplete`, `WinnerPlayerId`) + a game-specific `object Payload` (X01's per-player remaining score; a future game's own shape). Don't bake X01 fields into the universal envelope — that's what would make a second game secretly require core changes.
- `GameRuleViolationException` — plugins throw this on malformed input; the host catches it at the command-handler boundary and turns it into an `ErrorOr` failure instead of crashing.

### Plugin loading (Infrastructure/External/GamePlugins/)
- `PluginLoadContext.cs` — collectible ALC subclass with the shared-assembly resolution override above.
- `PluginGameLoader.cs` — scans `Plugins:Directory` (default `./plugins`) for `*.dll` on startup, loads each into its own context, reflects for `IGameFactory` implementations.
- `GameCatalog.cs` — implements `IGameCatalog` (Application): `ListAvailable()`, `Resolve(gameId)`.
- `GameSessionManager.cs` — implements `IGameSessionManager` (Application): holds live `IGame` instances per in-progress match in a `ConcurrentDictionary<Guid, IGame>`. **Explicit v1 limitation:** not persisted/rehydrated across process restart — an interrupted match is lost. Resumability would require every `IGame` to support state serialize/deserialize, which is real design weight with no current requirement driving it.
  - **Per-match serialization of `IGame` access.** `IGame` is stateful and synchronous/passive, and multiple producers can target the same match concurrently: the manual-throw REST endpoint runs a MediatR command on a request thread while `DetectionListenerService` may be mid-throw on the background thread, and Phase 4 explicitly wants live manual correction alongside a real board (two active sources at once). The dictionary makes concurrent matches structurally possible, so all mutation of a given `IGame` must be serialized. `GameSessionManager` owns a per-`matchId` async lock (or funnels every producer through a single-writer channel per match); command handlers acquire it before calling `ReceiveThrow`/`ReceiveEvent`/`UndoLastThrow`. This keeps the "plugin is passive, host controls timing" contract true regardless of how many producers exist.
  - **Detection event → match routing.** A `DetectedThrow` carries a `BoardId`, not a `MatchId`, so the manager also owns the `BoardId → active MatchId` binding, established when a match starts. **v1 rule:** a board hosts at most one active match at a time; the listener resolves the incoming throw's `BoardId` to that match, and throws for a board with no active match are dropped (logged). Mock/manual sources use a well-known default `BoardId`. This is the routing step the data flow below depends on — without it "resolve the match's `IGame`" has no key.
- `Darts.Games.X01.csproj` gets a post-build target copying its DLL into `Darts.Api/plugins/Darts.Games.X01/`, so `dotnet build` produces a working plugins folder automatically while still proving dynamic loading (not a project reference) is what's happening at runtime.

---

## Detection abstraction

`IDetectionSource` in `Application/Common/Interfaces/Services/`:
```
IAsyncEnumerable<DetectionEvent> EventsAsync(CancellationToken ct);
Task<bool> IsConnectedAsync();
```
`IAsyncEnumerable` over raw C# events — trivial to unit test (`await foreach` a mock async-enumerable) and composes cleanly with a `BackgroundService` consumer with no manual event unsubscription.

`DetectedThrow` lives in `Darts.GameSdk` (Application already depends on it for `IGame`), so detection events flow straight into `IGame.ReceiveThrow(...)` with no intermediate mapping type.

### Confirmed OpenDartboard wire format (verified against `docs/api.md`)
```json
{ "score": "D20", "position": {"x":150,"y":200}, "confidence": 0.95,
  "camera": 0, "processing_time": 15, "timestamp": 1699123456789 }
```
`score` ∈ `S1..S20`, `D1..D20`, `T1..T20`, `BULL` (50), `OUTER` (25), `MISS`, `END` (turn-boundary marker, not a scored throw). Endpoint: `ws://<host>:13520/scores`.

**Unresolved before Phase 3 — message cardinality / camera fusion.** The schema is confirmed, but the *cardinality* is not: the `"camera"` field plus OpenDartboard's 3-camera rig raises the question of whether `/scores` emits one fused message per physical dart or one message per camera per dart. If per-camera, a naive "one message = one `DetectedThrow`" adapter registers each dart up to 3×. Given OpenDartboard marks scoring detection itself WIP, treat this as a **Phase 3 verification gate**: confirm against replayed real-board captures whether fusion already happens upstream. If it doesn't, the adapter needs a dedup/fusion window (same segment/position within a short time window → one throw) *before* emitting `DetectedThrow`. The `CameraIndex` field on `DetectedThrow` is retained precisely to make this diagnosable.

`DetectedThrow` fields: `ThrowId` (Guid, adapter-assigned, for undo correlation), `Segment`, `Ring` (enum incl. `Miss`), `Score` (precomputed), `RawNotation` (original token, for debugging), `Position` (nullable), `Confidence` (nullable — mock/manual always `1.0`/`null`), `BoardId`, `CameraIndex` (nullable), `DetectedAtUtc`, `Source` (`OpenDartboard | Mock | Manual`).

### Two Infrastructure implementations, one DI switch (`Detection:Mode`)
- **`OpenDartboardWebSocketDetectionSource.cs`** — `ClientWebSocket` to `ws://{host}:13520/scores`, maps to `DetectedThrow`/`DetectionEvent`, simple exponential-backoff reconnect. Config: `Detection:OpenDartboard:Uri`.
- **`MockDetectionSource.cs`** — programmatic `SimulateThrow(...)`, used directly by tests and the Phase-1 demo harness; no network involved.
- **`ManualEntryDetectionSource.cs`** — driven by `POST /api/detection/manual-throw`, feeding the *same* `IDetectionSource` shape. Deliberately not a bolted-on "manual mode" special case — it's just another producer of the abstraction, which is what makes it a legitimate permanent fallback (no-hardware operation, or live correction alongside a real board) with no special-casing elsewhere.

### Data flow
`DetectionListenerService : BackgroundService` (Infrastructure) does `await foreach` over the active source → dispatches MediatR command (`RecordDetectedThrowCommand` / `RecordEndOfTurnCommand`) → handler resolves the match's `IGame` via `IGameSessionManager`, calls it (catching `GameRuleViolationException` → `ErrorOr`), persists a `ThrowRecord`, publishes `GameStateChangedEvent` → an Api-side handler forwards the fresh `GameStateSnapshot` to SignalR.

---

## First reference game: classic 501 (`Darts.Games.X01`)

Simplest state machine that still exercises the whole `IGame` contract (turn/leg/set progression, bust rules, undo, winner determination) — validates the contract cheaply and sets the pattern every future game follows.

- `GameSetup` options: `startingScore` (default 501, kept parametric so 301/701 are a config change not a new plugin), `doubleOut` (bool), `legsToWin`, `setsToWin` (default a short best-of for the v1 demo).
- Turn = up to 3 throws, ended early by `EndOfTurn` or after the 3rd throw.
- Bust: `remaining < 0`, or `remaining == 1` under double-out, or (`remaining == 0` and the finishing dart wasn't a valid double under double-out) → revert the visit's score change, end turn. **Valid double-out finisher = `Ring.Double` *or* `Ring.Bull` (inner bull, 50)** — the checkout predicate must accept double-bull, otherwise a legal 50 finish is wrongly busted. `Ring.Outer` (25) is not a double.
- `remaining == 0` with checkout satisfied → leg won; new leg starts with alternating start player; sets aggregate legs the same way.
- `GetState()` payload: per-player `RemainingScore`, `LegsWon`, `SetsWon`, current visit's throws so far. Throw history retained internally for `UndoLastThrow` even though no stats UI ships in v1 — nearly free now, avoids a redesign later.
- **Undo model: replay from history, not delta-reversal.** `UndoLastThrow` pops the last throw and rebuilds derived state (`RemainingScore`, leg/set counters, current player, start-player alternation) by replaying the retained history. This is chosen over "reverse the last applied delta" because the hard cases — undoing the dart that *completed a leg or set*, or undoing back into a reverted bust visit — are exactly where delta-reversal diverges: it would have to un-win the leg, restore the pre-checkout remaining, decrement `LegsWon`/`SetsWon`, and roll back the alternation, each a separate special case. Replay makes all of them fall out of one code path. Unit tests must cover undo across a leg boundary and undo of a busting dart specifically.

---

## Web UI: static wwwroot + REST + SignalR (not Blazor Server, not a separate SPA project)

- `Api/Hubs/GameHub.cs` — `JoinMatch(matchId)` joins group `match-{id}`; no client→server score input via the hub (scores only flow from detection/REST).
- `IGameNotifier` (Application interface) implemented by `Api/Hubs/GameHubNotifier.cs`, pushing `GameStateSnapshot` via `hubContext.Clients.Group(...).SendAsync("GameStateUpdated", snapshot)` — keeps SignalR types out of Application.
- `Api/wwwroot/index.html` + `scoreboard.js` (SignalR client via CDN script tag, no npm/build pipeline) rendering: players + remaining score, current visit's darts, last N throws, leg/set score, winner banner, minimal start-match form (`GET /api/games` for the catalog, `POST /api/matches`), plus the manual-throw entry panel.
- `Program.cs`: `AddSignalR()`, `UseStaticFiles()`, `MapHub<GameHub>("/hubs/game")`, MediatR-backed minimal-API endpoint groups under `Api/Endpoints/` (`MatchEndpoints`, `PlayerEndpoints`, `DetectionEndpoints`, `GameEndpoints`).

**Why not Blazor Server:** it would keep everything in C#, but ties rendering to a stateful per-tab circuit owned by the Api process, muddying the boundary between "the platform's network API" and "this particular UI." REST + a purpose-built SignalR hub keeps `Darts.Api` a clean surface that future consumers (mobile companion app, spectator/TV display — both explicitly in the long-term vision) can hit without caring how the reference web scoreboard is built. It also mirrors the same network-boundary philosophy already forced onto the OpenDartboard integration.

v1 UI scope: live scoreboard + trivial match-start form only. No player CRUD screen, no stats views.

---

## Persistence (SQLite via EF Core)

- **`Player`** — `Id`, `Name`, `CreatedAtUtc`. No auth fields.
- **`Match`** (aggregate root) — `Id`, `GameId`, `GameConfigJson` (the opaque `GameSetup` blob), `Status`, `CreatedAtUtc`, `CompletedAtUtc`, `WinnerPlayerId`, owned `MatchParticipant` collection (`PlayerId`, `Order`, `FinalPosition`).
- **`ThrowRecord`** — `Id`, `MatchId`, `PlayerId`, `SetNumber`, `LegNumber`, `Sequence`, `Segment`, `Ring`, `Score`, `RawNotation`, `Source`, `DetectedAtUtc`. Recorded for history/audit and to seed future stats work without a schema redesign — not read back to resume a live match.

No `Leg` table — leg/set boundaries are derivable from `ThrowRecord` fields if ever needed; not worth a dedicated entity in v1.

EF: `Infrastructure/Persistence/Configurations/{Player,Match,ThrowRecord}Configuration.cs`, repositories `IPlayerRepository`/`IMatchRepository` (Application interfaces) implemented in `Infrastructure/Persistence/Repositories/`. Connection string `Data Source=darts.db`.

---

## Phased build order

**Phase 0 — Scaffold.** Run the DDD scaffold in place at `C:\Projects\Darts`; swap SqlServer→Sqlite; add `Darts.GameSdk` and `src/Games/Darts.Games.X01` (+ test projects) with the reference rules above; `dotnet build`; `git init` + initial commit.

**Phase 1 — Domain + GameSdk + X01 plugin + mock detection, no UI (earliest demoable slice).** Large phase; sequence it internally so the **`GameSdk` contracts and the `Darts.Games.X01` state machine + its unit tests land and pass first** (bust incl. double-bull finish, checkout, leg/set progression, undo across a leg boundary, undo of a busting dart, win), *before* the plumbing that asserts against them: domain entities/value objects; plugin loader + `GameCatalog`/`GameSessionManager` (incl. per-`matchId` serialization + `BoardId`→match routing); `MockDetectionSource`; SQLite context + repositories + first migration; MediatR commands/handlers; bare Api endpoints exercised via Scalar/curl plus an integration test scripting a full mock 501 leg end-to-end. **Deliverable: a full 501 leg playable and asserted via API + tests, with the plugin genuinely loaded from a `plugins/` folder DLL — before any UI or hardware exists.**

**Phase 2 — Web UI + SignalR.** `GameHub`, `GameHubNotifier`, `wwwroot` scoreboard + start-match form + manual-throw panel, minimal player create/list.

**Phase 3 — OpenDartboard adapter.** `OpenDartboardWebSocketDetectionSource` against the confirmed `/scores` schema, reconnect logic, `Detection:Mode` switch. No hardware yet, so validate against a local test WebSocket server in `Infrastructure.IntegrationTests` replaying canned JSON frames matching the real schema — gives confidence ahead of hardware and becomes a regression test once hardware exists. **Gate:** resolve the message-cardinality question (see Detection wire format) — confirm whether `/scores` emits one fused message per dart or one per camera, and add a fusion/dedup window in the adapter if per-camera. This must be settled here, since it determines whether a raw message maps 1:1 to a `DetectedThrow`.

**Phase 4 — Hardening.** Logging, board connected/disconnected UI indicator, reconnect resilience, appsettings profiles, a startup script that launches the forked OpenDartboard binary and `Darts.Api` together on the target device.

**Phase 5 (stretch, not required for "v1 done").** A second, deliberately different game plugin (e.g. Around the Clock) purely to validate the architecture claim end-to-end.

---

## Critical files
- `src/Darts.GameSdk/{IGame,DetectedThrow,GameSetup,GameStateSnapshot,IGameFactory}.cs` — the entire plugin boundary hinges on this assembly.
- `src/Games/Darts.Games.X01/X01Game.cs` — reference implementation setting the pattern for every future game.
- `src/Darts.Application/Common/Interfaces/Services/IDetectionSource.cs` — detection abstraction both adapters satisfy.
- `src/Darts.Infrastructure/External/GamePlugins/{PluginLoadContext,PluginGameLoader}.cs` — actual ALC loading + shared-assembly resolution fix.
- `src/Darts.Infrastructure/Persistence/DartsDbContext.cs` + `Persistence/Configurations/*` — SQLite schema.

## Verification
- Phase 1 end-to-end integration test (script a full mock 501 leg through the MediatR commands / Api endpoints, assert final `GameStateSnapshot` and persisted `ThrowRecord`s) is the primary correctness gate before any UI work starts.
- `dotnet build` and `dotnet test` (all layers) must pass at the end of every phase.
- Phase 1's plugin-loading must be verified as genuinely dynamic: delete/rebuild the plugin DLL independently and confirm the host picks it up from `plugins/` without a solution-wide rebuild.
- Phase 2: manually drive a match through the browser UI (start match → manual-throw panel → confirm live scoreboard updates via SignalR → leg/match completion banner).
- Phase 3: run the adapter against the replayed-JSON test WebSocket server and confirm it reconnects correctly after a dropped connection, before ever touching real hardware.
