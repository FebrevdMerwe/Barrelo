# Darts Platform — v1 Implementation Plan

## Context

`SCOPE.md` lays out a long-term vision (community games, online play, AI coaching, wearables, tournaments...) but makes zero technical commitments — no detection strategy, no tech stack, no MVP boundary. Before any code can be written, those decisions had to be made. A grilling session with the user established:

- **v1 goal**: a working detection → game logic → scoreboard loop for one traditional game (classic 501), plus the plugin architecture that proves a second game can be added later *without modifying the core*. That plugin architecture, not the game itself, is the actual deliverable.
- **Detection**: the user's initial idea was to "fork AutoDarts." Research showed AutoDarts' core detection engine is closed-source (binary-only distribution; only a docs repo is public on GitHub), so *forking* it isn't possible. But it can be *consumed* as a black-box detection source over its API — which is all v1 needs, since the platform treats any detector as a replaceable source behind `IDetectionSource` (see Detection abstraction). **v1 uses AutoDarts**: a mature, already-tuned 3-camera detector that works today, so the whole pipeline gets validated against a detector that actually detects rather than co-debugging the platform and a still-immature detector at once. The user has no hardware yet, so v1 must be fully developable against a mock/manual detection source, with the real AutoDarts adapter validated against replayed sample JSON rather than physical hardware. **This is explicitly a v1-pragmatism call, not a permanent commitment** — AutoDarts is closed-source and carries its own ToS, so if the project outgrows personal use (multi-user, commercial), detection is swapped for an owned stack. That swap is *just another `IDetectionSource`*, provable now because the boundary already exists (OpenDartboard — github.com/OpenDartboard/OpenDartboard, GPL-3.0, C++/OpenCV, Pi Zero 2 W with 3 USB webcams — remains the documented "own the stack" candidate for that future; see Detection abstraction).
- **Licensing / dependency posture**: v1 depends on AutoDarts as an external, closed-source product consumed over a network/API boundary — the .NET platform is just a *client* of it, linking none of its code. For personal v1 use that's fine. The real caveat is not copyleft (AutoDarts isn't GPL) but **third-party dependency + ToS**: relying on someone else's closed product (and possibly their cloud) is a business risk to retire before any commercial release — which is exactly the trigger for the detector swap above. Because we consume AutoDarts rather than redistribute it, we ship no detection binary and inherit no distribution obligation; the user installs/runs the AutoDarts board client themselves (see Phase 4).
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
- **No MediatR.** MediatR moved to a commercial license (v13, 2025 — paid above a revenue threshold). Since this is built toward a real commercial product, the scaffold's MediatR dependency and `AddMediatR(...)` registration are dropped in favour of a tiny in-house dispatcher (see "Request dispatcher" below). No other library in the stack carries a commercial-licensing obligation, so removing this one keeps v1 — and any later commercial release — free of runtime licensing fees. `ErrorOr` (MIT) is kept.
- Infrastructure: swap `Microsoft.EntityFrameworkCore.SqlServer` → `Microsoft.EntityFrameworkCore.Sqlite` (single-device local deployment, no server needed).
- `Darts.GameSdk`: **zero** project references, no dispatcher/EF/ErrorOr — BCL + `System.Text.Json` only. This is the only assembly a game plugin author (including a future third party) needs to reference.
- `Darts.Application` references `Darts.GameSdk` in addition to `Darts.Domain`. `Darts.Domain` does **not** reference GameSdk — keep the domain model pure; GameSdk is a sibling contracts library.
- `Darts.Api` also references `Darts.GameSdk` (hosts the plugin loader, shapes SignalR payloads).
- `Darts.Games.X01` references **only** `Darts.GameSdk` — this is what proves the plugin boundary is real, not aspirational.
- No separate SPA/front-end project for v1 — the web UI is static files served from `Darts.Api/wwwroot` (see UI section for why).

---

## Request dispatcher (in-house, replaces MediatR)

A ~1-file mediator living in `Darts.Application/Common/Dispatch/`. It covers exactly the two features the plan actually uses — request/response commands+queries and fire-and-forget notifications — and nothing else. No pipeline-behavior framework, no assembly-scanning magic beyond a single registration helper.

**Contracts (Application, dependency-free):**
- `IRequest<TResponse>` — marker for a command/query returning `TResponse` (typically `ErrorOr<T>`).
- `IRequestHandler<TRequest, TResponse>` — `Task<TResponse> Handle(TRequest request, CancellationToken ct)`.
- `INotification` + `INotificationHandler<TNotification>` — for `GameStateChangedEvent` fan-out (0..n handlers).
- `IDispatcher` — `Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct)` and `Task Publish(INotification notification, CancellationToken ct)`.

**Implementation (`Dispatcher.cs`):**
- Resolves the handler from `IServiceProvider` by constructing the closed generic `IRequestHandler<,>` type from the runtime request type, then invokes `Handle`. One cached `MethodInfo`/delegate per request type via a `ConcurrentDictionary<Type, Func<...>>` so reflection cost is paid once, not per call. Send is single-handler (throws if zero/many); Publish resolves `IEnumerable<INotificationHandler<T>>` and awaits each (sequential, exceptions aggregated).
- `AddDartsDispatcher(this IServiceCollection)` registers `IDispatcher` as singleton and scans the Application assembly for `IRequestHandler<,>`/`INotificationHandler<>` implementations, registering each as scoped/transient. This is the only reflection-scan; handlers themselves are plain classes.

**Why this is safe to hand-roll:** the surface MediatR gave us here is small and stable, all first-party handlers (no third-party pipeline plugins), and the boundary is already an interface (`IDispatcher`) — so if a future need outgrows it, swapping the implementation is localized. Keeping the `IRequest`/`IRequestHandler` shape close to MediatR's also means handler code reads familiarly and the migration cost (in either direction) stays near zero.

**Unit tests (`Darts.Application.UnitTests`):** dispatch routes to the correct handler; unregistered request throws a clear error; notification reaches all registered handlers; response (incl. `ErrorOr` failure) is returned unchanged.

---

## Game plugin architecture

**In-process loading via `AssemblyLoadContext` (ALC)**, not out-of-process game services. Out-of-process (a game as its own microservice) is the right answer once untrusted third-party/community plugins are real (a listed future goal), but for v1 — solo dev, single device, games written by the platform author — it's infrastructure with no current payoff. The migration path later is clean specifically *because* the `IGame` contract lives in a dependency-free SDK assembly rather than in Domain/Application types: only the hosting mechanism changes later, not the contract.

**ALC gotcha to design around now:** types crossing the boundary (`DetectedThrow`, `GameStateSnapshot`, etc.) must resolve to the same assembly instance on both sides, or identical-looking types throw `InvalidCastException`. Fix: the plugin's `AssemblyLoadContext.Load` override returns `null` for any assembly already loaded in the default context (i.e. `Darts.GameSdk`), forcing resolution against the host's copy. Only the plugin's own DLL loads into its collectible context.

### `Darts.GameSdk` contents
- `DetectedThrow` — canonical throw event, shared verbatim between the detection subsystem and game logic (no mapping layer needed since both sit on the same "outer, replaceable" side of the boundary relative to Domain).
- `DetectionEventType` (`Throw | EndOfTurn`) — real detectors signal visit-complete explicitly (OpenDartboard's wire protocol has an explicit `"END"` marker; AutoDarts exposes turn/match state), so the contract needs a real turn-boundary signal, not "3 throws = turn."
- `DetectionEvent` (Application-side wrapper, not GameSdk) — a discriminated envelope: `DetectionEventType Type` + a nullable `DetectedThrow Throw` (populated only when `Type == Throw`). `IDetectionSource.EventsAsync` yields these; the listener switches on `Type` to send `RecordDetectedThrowCommand` vs `RecordEndOfTurnCommand` through the dispatcher. Stated explicitly here because the split-command design otherwise leaves the on-the-wire event shape implicit.
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
  - **Per-match serialization of `IGame` access.** `IGame` is stateful and synchronous/passive, and multiple producers can target the same match concurrently: the manual-throw REST endpoint runs a dispatched command on a request thread while `DetectionListenerService` may be mid-throw on the background thread, and Phase 4 explicitly wants live manual correction alongside a real board (two active sources at once). The dictionary makes concurrent matches structurally possible, so all mutation of a given `IGame` must be serialized. `GameSessionManager` owns a per-`matchId` async lock (or funnels every producer through a single-writer channel per match); command handlers acquire it before calling `ReceiveThrow`/`ReceiveEvent`/`UndoLastThrow`. This keeps the "plugin is passive, host controls timing" contract true regardless of how many producers exist.
  - **Detection event → match routing.** A `DetectedThrow` carries a `BoardId`, not a `MatchId`, so the manager also owns the `BoardId → active MatchId` binding, established when a match starts. **v1 rule:** a board hosts at most one active match at a time; the listener resolves the incoming throw's `BoardId` to that match, and throws for a board with no active match are dropped (logged). Mock/manual sources use a well-known default `BoardId`. This is the routing step the data flow below depends on — without it "resolve the match's `IGame`" has no key.
- `Darts.Games.X01.csproj` gets a post-build target copying its DLL into `Darts.Api/plugins/Darts.Games.X01/`, so `dotnet build` produces a working plugins folder automatically while still proving dynamic loading (not a project reference) is what's happening at runtime.

---

## Detection abstraction

### Decision: consume AutoDarts for v1, do not fork or rewrite

For v1 the detection engine is **AutoDarts, consumed as a black box over its API** — the platform is a client of it, not a fork of it. Two alternatives were considered and deliberately deferred:

- **Forking a detector (e.g. OpenDartboard).** OpenDartboard is genuinely open-source (GPL-3.0, C++/OpenCV, runs on a Pi Zero 2 W with 3 USB webcams) and *forkable* — but it's early-stage (v0.1.4, scoring detection itself WIP). Starting v1 on it means debugging the platform and an immature detector simultaneously, with no hardware yet to tune against. AutoDarts, by contrast, is mature and already tuned, so it validates the platform against a detector that actually works. OpenDartboard remains the leading **"own the stack" candidate** for when detection is brought in-house.
- **A from-scratch rewrite (in particular a .NET port).** Rejected regardless of which detector we start on: the value is in the CV — multi-camera calibration, perspective transforms, dart-landing detection, occlusion handling, and above all empirical tuning against real boards — not the language, which is the cheapest part. The Pi Zero 2 W (512 MB RAM, quad A53) is a memory/per-watt-bound workload favouring native C++/OpenCV, and the mature .NET OpenCV binding (Emgu.CV) is dual GPL/**commercial (paid)** anyway. Detection is the highest-risk part of the system; rewriting it in a less-suited language on constrained hardware before any hardware exists to validate against is the worst possible sequencing. A future from-scratch detector would more likely be **ML-based** (YOLO-style) than a classical-CV port, and still favour C++/Python.

**When to swap AutoDarts out:** when the project outgrows personal use — multi-user or commercial release makes the closed-source, third-party-ToS, possibly-cloud dependency an unacceptable business risk — or if AutoDarts proves limiting or becomes unavailable. The swap target is a forked OpenDartboard or an owned ML detector.

**Crucially, none of this is a core change.** Every detector — AutoDarts today, a forked OpenDartboard or custom ML detector later — is just **another `IDetectionSource` implementation** behind the same abstraction. A future detector can be built in parallel and A/B'd against the same board before any switchover; the platform never has a big-bang detection replacement. This is the whole payoff of putting detection behind `IDetectionSource` + a network boundary, and it's what makes "AutoDarts for v1, replace it later" a one-adapter change rather than a rewrite.

`IDetectionSource` in `Application/Common/Interfaces/Services/`:
```
IAsyncEnumerable<DetectionEvent> EventsAsync(CancellationToken ct);
Task<bool> IsConnectedAsync();
```
`IAsyncEnumerable` over raw C# events — trivial to unit test (`await foreach` a mock async-enumerable) and composes cleanly with a `BackgroundService` consumer with no manual event unsubscription.

`DetectedThrow` lives in `Darts.GameSdk` (Application already depends on it for `IGame`), so detection events flow straight into `IGame.ReceiveThrow(...)` with no intermediate mapping type.

### AutoDarts wire format (to verify in Phase 3 — not yet confirmed)

Unlike the OpenDartboard schema (which was confirmed against its public `docs/api.md`), AutoDarts' event API is **not yet confirmed here** and must be pinned down before Phase 3 by inspecting a live board manager and the community integration ecosystem (`autodarts-caller`, `autodarts-tools`, etc. already consume it). Known unknowns to resolve:
- **Local vs cloud.** AutoDarts runs a local board-manager (default `http://127.0.0.1:3180`) *and* relays to `autodarts.io`. v1's "single device, no cloud" constraint (Deployment) means we want the **local** board-manager event stream if it exposes per-throw events; confirm whether real-time throws are available locally or only via the authenticated cloud WebSocket. Hard gate — if throws are cloud-only, either the "no cloud" constraint bends for v1 or the adapter carries an auth/token flow.
- **Event schema & cardinality.** Confirm the throw event shape and that AutoDarts emits **one fused throw per physical dart** (it does its own multi-camera fusion, so the per-camera-duplication worry that dogged OpenDartboard should not arise — verify, don't assume). Also confirm the turn-boundary / match-state signal that maps to `DetectionEventType.EndOfTurn`, and how AutoDarts represents a dart: expect a segment number + bed/ring (single/double/triple/bull) rather than OpenDartboard's `"D20"`-style token, so the adapter's notation parsing differs.
- **Corrections.** AutoDarts supports throw correction (auto + manual in its UI). Decide whether the adapter listens for correction/retract events and maps them to `UndoLastThrow`, or ignores them in v1 (manual correction stays the platform's own `POST /api/detection/manual-throw` path).

The adapter maps whatever AutoDarts emits onto the **detector-agnostic** `DetectedThrow` (below); the internal canonical ring/segment model doesn't change per detector. For reference, OpenDartboard's confirmed shape (the contract the future OpenDartboard adapter targets) was:
```json
{ "score": "D20", "position": {"x":150,"y":200}, "confidence": 0.95,
  "camera": 0, "processing_time": 15, "timestamp": 1699123456789 }
```
with `score` ∈ `S1..S20`, `D1..D20`, `T1..T20`, `BULL` (50), `OUTER` (25), `MISS`, `END` (turn-boundary marker), endpoint `ws://<host>:13520/scores`.

`DetectedThrow` fields: `ThrowId` (Guid, adapter-assigned, for undo correlation), `Segment`, `Ring` (enum incl. `Miss`), `Score` (precomputed), `RawNotation` (original token, for debugging), `Position` (nullable), `Confidence` (nullable — mock/manual always `1.0`/`null`), `BoardId`, `CameraIndex` (nullable — AutoDarts fuses upstream so typically null; retained for a future per-camera detector), `DetectedAtUtc`, `Source` (`AutoDarts | Mock | Manual`).

### Detection implementations, one DI switch (`Detection:Mode`)
- **`AutoDartsDetectionSource.cs`** — connects to the AutoDarts event stream (local board-manager endpoint confirmed in Phase 3; `ClientWebSocket` or SSE/HTTP per what that API turns out to be), maps each event to `DetectedThrow`/`DetectionEvent`, simple exponential-backoff reconnect. Config: `Detection:AutoDarts:*` (base URL/port, and — only if throws prove cloud-only — auth token).
- **`MockDetectionSource.cs`** — programmatic `SimulateThrow(...)`, used directly by tests and the Phase-1 demo harness; no network involved.
- **`ManualEntryDetectionSource.cs`** — driven by `POST /api/detection/manual-throw`, feeding the *same* `IDetectionSource` shape. Deliberately not a bolted-on "manual mode" special case — it's just another producer of the abstraction, which is what makes it a legitimate permanent fallback (no-hardware operation, or live correction alongside a real board) with no special-casing elsewhere. **Manual entry is a first-class way to play, not only a fallback.** Crucially it is **orthogonal to the `Detection:Mode` switch**: that switch selects which *streaming* source the `DetectionListenerService` runs (`AutoDarts`/`Mock`), whereas manual entry is **REST-driven and therefore always available**, even with no streaming source running at all. A fully-manual match needs no hardware: it is bound to the well-known manual `BoardId` at start, and every throw POSTed to the manual endpoints routes through the identical `DetectedThrow` → command → `IGame` → SignalR path. Because manual and a live board can be active for the same match simultaneously (live correction), the per-`matchId` serialization above is what keeps that safe.
- *(Future)* **`OpenDartboardWebSocketDetectionSource.cs`** — the "own the stack" adapter against the confirmed `ws://{host}:13520/scores` schema; not built in v1, but the reason the wire format above is retained.

### Data flow
`DetectionListenerService : BackgroundService` (Infrastructure) does `await foreach` over the active source → dispatches a command (`RecordDetectedThrowCommand` / `RecordEndOfTurnCommand`) via the in-house dispatcher → handler resolves the match's `IGame` via `IGameSessionManager`, calls it (catching `GameRuleViolationException` → `ErrorOr`), persists a `ThrowRecord`, publishes `GameStateChangedEvent` → an Api-side handler forwards the fresh `GameStateSnapshot` to SignalR.

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
- `Api/wwwroot/index.html` + `scoreboard.js` (SignalR client via CDN script tag, no npm/build pipeline) rendering: players + remaining score, current visit's darts, last N throws, leg/set score, winner banner, minimal start-match form (`GET /api/games` for the catalog, `POST /api/matches`, incl. the **input-source selector** below), plus the **virtual dartboard input** (below) replacing a plain manual-throw text panel.
- **Virtual dartboard (`dartboard.js`).** A clickable **SVG standard 20-segment board**: for each number, four wedge paths (inner-single, triple, outer-single, double) plus outer-bull (25) and inner-bull (50) circles, each carrying `data-segment` and `data-ring`. A click resolves segment+ring directly from the clicked element's data attributes — SVG hit-testing does the geometry, no pixel-to-polar math (polar math is only a fallback if a single `<canvas>` is ever preferred over discrete paths). Alongside the board: **Miss** (segment 0 / `Ring.Miss`), **Undo**, and **End turn / Next player** controls. Each action POSTs to the matching manual endpoint; the board computes no score and holds no game state — current-player highlight and "darts this visit (1/2/3)" come straight from the pushed `GameStateSnapshot`, so the existing `GameStateUpdated` event re-renders everything. The board is purely an input device, making manual play fully hardware-free.
- **Input-source selection.** The start-match form offers `Manual` vs `Board`. A `Manual` match is bound to the well-known manual `BoardId` (no detector needed); a `Board` match binds the real board id but the manual endpoints still work for live correction.
- `Program.cs`: `AddSignalR()`, `UseStaticFiles()`, `MapHub<GameHub>("/hubs/game")`, `AddDartsDispatcher()` (registers the in-house dispatcher + scans for handlers), minimal-API endpoint groups under `Api/Endpoints/` (`MatchEndpoints`, `PlayerEndpoints`, `DetectionEndpoints`, `GameEndpoints`) that resolve `IDispatcher` and send requests.

**Manual-play endpoints (`DetectionEndpoints`).** Three REST endpoints drive the always-available manual source; all go through the same dispatcher / per-`matchId` lock / SignalR path, so they add no new plumbing pattern:
- `POST /api/detection/manual-throw` — body `{ boardId?, segment, ring }` (boardId defaults to the well-known manual id). `ManualEntryDetectionSource` builds the canonical `DetectedThrow` server-side (`Source = Manual`, `Confidence = null`, `Position = null`, `Score` computed from segment+ring by the shared scoring helper). Sending the **resolved segment+ring rather than pixel coordinates** keeps the endpoint UI-agnostic (a mobile companion or keypad UI reuses it unchanged); the server/`IGame` stays authoritative for scoring.
- `POST /api/detection/manual-end-turn` — body `{ boardId? }` → dispatches the existing `RecordEndOfTurnCommand` (`IGame.ReceiveEvent(EndOfTurn)`), letting a player end a visit before 3 darts (checkouts/busts).
- `POST /api/detection/undo` — body `{ boardId? }` → new thin **`UndoLastThrowCommand`** whose handler resolves the match's `IGame` under the per-`matchId` lock, calls the already-first-class `UndoLastThrow()`, retracts the last `ThrowRecord`, and publishes `GameStateChangedEvent`. This exposes undo (required by the contract, line 87) to the manual UI.

**Why not Blazor Server:** it would keep everything in C#, but ties rendering to a stateful per-tab circuit owned by the Api process, muddying the boundary between "the platform's network API" and "this particular UI." REST + a purpose-built SignalR hub keeps `Darts.Api` a clean surface that future consumers (mobile companion app, spectator/TV display — both explicitly in the long-term vision) can hit without caring how the reference web scoreboard is built. It also mirrors the same network-boundary philosophy already forced onto the OpenDartboard integration.

v1 UI scope: live scoreboard + trivial match-start form only. No player CRUD screen, no stats views.

---

## Persistence (SQLite via EF Core)

- **`Player`** — `Id`, `Name`, `CreatedAtUtc`. No auth fields.
- **`Match`** (aggregate root) — `Id`, `GameId`, `GameConfigJson` (the opaque `GameSetup` blob), `Status`, `InputSource` (`Manual | Board`, defaults `Manual` when no detector is configured — drives the `BoardId` binding at match start), `CreatedAtUtc`, `CompletedAtUtc`, `WinnerPlayerId`, owned `MatchParticipant` collection (`PlayerId`, `Order`, `FinalPosition`).
- **`ThrowRecord`** — `Id`, `MatchId`, `PlayerId`, `SetNumber`, `LegNumber`, `Sequence`, `Segment`, `Ring`, `Score`, `RawNotation`, `Source`, `DetectedAtUtc`. Recorded for history/audit and to seed future stats work without a schema redesign — not read back to resume a live match.

No `Leg` table — leg/set boundaries are derivable from `ThrowRecord` fields if ever needed; not worth a dedicated entity in v1.

EF: `Infrastructure/Persistence/Configurations/{Player,Match,ThrowRecord}Configuration.cs`, repositories `IPlayerRepository`/`IMatchRepository` (Application interfaces) implemented in `Infrastructure/Persistence/Repositories/`. Connection string `Data Source=darts.db`.

---

## Phased build order

**Phase 0 — Scaffold.** Run the DDD scaffold in place at `C:\Projects\Darts`; swap SqlServer→Sqlite; add `Darts.GameSdk` and `src/Games/Darts.Games.X01` (+ test projects) with the reference rules above; `dotnet build`; `git init` + initial commit.

**Phase 1 — Domain + GameSdk + X01 plugin + mock detection, no UI (earliest demoable slice).** Large phase; sequence it internally so the **`GameSdk` contracts and the `Darts.Games.X01` state machine + its unit tests land and pass first** (bust incl. double-bull finish, checkout, leg/set progression, undo across a leg boundary, undo of a busting dart, win), *before* the plumbing that asserts against them: domain entities/value objects; plugin loader + `GameCatalog`/`GameSessionManager` (incl. per-`matchId` serialization + `BoardId`→match routing); `MockDetectionSource`; SQLite context + repositories + first migration; the in-house dispatcher + commands/handlers (incl. the manual `RecordEndOfTurnCommand` and new `UndoLastThrowCommand`); the three manual-play endpoints (`manual-throw`, `manual-end-turn`, `undo`) in `DetectionEndpoints`; `Match.InputSource` + the manual `BoardId` binding on manual-match start; bare Api endpoints exercised via Scalar/curl plus an integration test scripting a full mock 501 leg end-to-end **and** a full *manual* 501 leg (normal throws + `Miss` + an early `end-turn` + `undo` of a busting dart across a leg boundary) with **no streaming source running**. **Deliverable: a full 501 leg playable and asserted via API + tests — from both the mock stream and pure manual entry with zero hardware — with the plugin genuinely loaded from a `plugins/` folder DLL, before any UI exists.**

**Phase 2 — Web UI + SignalR.** `GameHub`, `GameHubNotifier`, `wwwroot` scoreboard + start-match form (with input-source selector) + **virtual dartboard input** (`dartboard.js`: clickable SVG board + Miss/Undo/End-turn controls, all driven by the manual endpoints and re-rendered from `GameStateUpdated`), minimal player create/list. **Verify manual play end-to-end in the browser: start a `Manual` match, click segments/rings, confirm live SignalR updates and a full leg/match completion — with no tracker connected.**

**Phase 3 — AutoDarts adapter.** `AutoDartsDetectionSource`, reconnect logic, `Detection:Mode` switch. No hardware yet, so validate against a local test server in `Infrastructure.IntegrationTests` replaying canned frames matching AutoDarts' event schema — gives confidence ahead of hardware and becomes a regression test once hardware exists. **Gate (see AutoDarts wire format):** pin down the unconfirmed API first — (a) local board-manager stream vs cloud-only (and whether that forces an auth flow or bends the "no cloud" constraint), (b) the exact throw/turn-boundary event schema and notation, (c) confirm one-fused-throw-per-dart, (d) decide correction/retract handling. These must be settled here, since they determine how a raw AutoDarts event maps to a `DetectedThrow`. Capture real sample frames from a live board manager to drive the replay tests.

**Phase 4 — Hardening.** Logging, board connected/disconnected UI indicator, reconnect resilience, appsettings profiles, and a startup script that launches `Darts.Api` on the target device. Unlike a forked GPL detector, **AutoDarts is not shipped by us** — it's a separate product the user installs and runs (its board client/manager); the startup script just assumes it's already running and points `Detection:AutoDarts:*` at it. (A future owned detector like a forked OpenDartboard would reintroduce the "launch the detector binary alongside `Darts.Api`" step.)

**Phase 5 (stretch, not required for "v1 done").** A second, deliberately different game plugin (e.g. Around the Clock) purely to validate the architecture claim end-to-end.

---

## Critical files
- `src/Darts.GameSdk/{IGame,DetectedThrow,GameSetup,GameStateSnapshot,IGameFactory}.cs` — the entire plugin boundary hinges on this assembly.
- `src/Games/Darts.Games.X01/X01Game.cs` — reference implementation setting the pattern for every future game.
- `src/Darts.Application/Common/Interfaces/Services/IDetectionSource.cs` — detection abstraction every adapter (AutoDarts, Mock, Manual, future OpenDartboard) satisfies.
- `src/Darts.Infrastructure/External/GamePlugins/{PluginLoadContext,PluginGameLoader}.cs` — actual ALC loading + shared-assembly resolution fix.
- `src/Darts.Infrastructure/Persistence/DartsDbContext.cs` + `Persistence/Configurations/*` — SQLite schema.

## Verification
- Phase 1 end-to-end integration test (script a full mock 501 leg through the dispatched commands / Api endpoints, assert final `GameStateSnapshot` and persisted `ThrowRecord`s) is the primary correctness gate before any UI work starts.
- `dotnet build` and `dotnet test` (all layers) must pass at the end of every phase.
- Phase 1's plugin-loading must be verified as genuinely dynamic: delete/rebuild the plugin DLL independently and confirm the host picks it up from `plugins/` without a solution-wide rebuild.
- Phase 2: manually drive a match through the browser UI (start match → manual-throw panel → confirm live scoreboard updates via SignalR → leg/match completion banner).
- Phase 3: run the AutoDarts adapter against the replayed-frame test server and confirm it reconnects correctly after a dropped connection, before ever touching real hardware — and that the wire-format gate (local vs cloud, event schema, fusion, corrections) was resolved with real captured samples, not assumed.
