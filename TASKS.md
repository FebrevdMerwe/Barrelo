# Darts Platform — Task Tracker

Mirrors the phases in `PLAN.md`. Check items off as they land; don't reorder phases without updating `PLAN.md` too. Each phase's "Done when" is its deliverable/verification line from `PLAN.md`.

---

## Phase 0 — Scaffold

- [x] Run `new-dotnet-project` DDD scaffold in place at `C:\Projects\Darts`
- [x] Swap `Microsoft.EntityFrameworkCore.SqlServer` → `Microsoft.EntityFrameworkCore.Sqlite`
- [x] Add `Darts.GameSdk` project (zero project references, BCL + `System.Text.Json` only)
- [x] Add `src/Games/Darts.Games.X01` + `tests/Games/Darts.Games.X01.UnitTests`
- [x] `dotnet build` succeeds
- [x] Commit scaffold

**Done when:** solution builds with the layout in PLAN.md's "Solution layout".

---

## Phase 1 — Domain + GameSdk + X01 plugin + mock detection (no UI)

**Sequence: GameSdk + X01 state machine + its tests land and pass *first*, before the plumbing below.**

### GameSdk contracts
- [x] `DetectedThrow`, `DetectionEventType`, `GameSetup`, `GameStateSnapshot`
- [x] `IGameFactory`, `IGame` (async-returning, pull-based), `GameRuleViolationException`

### X01 game plugin
- [x] `X01Game.cs` state machine: turn/leg/set progression, bust rules (incl. double-bull finish), undo
- [x] Unit tests: bust, checkout, leg/set progression, undo across a leg boundary, undo of a busting dart, win

### Domain + Application plumbing
- [x] Domain entities/value objects
- [x] In-house dispatcher (`IRequest`, `IRequestHandler`, `INotification`, `INotificationHandler`, `IDispatcher`, `Dispatcher.cs`, `AddDartsDispatcher`)
- [x] Dispatcher unit tests (routes correctly, unregistered request errors clearly, notification fan-out, `ErrorOr` passthrough)
- [x] `PluginLoadContext` + `PluginGameLoader` (ALC, shared-assembly resolution fix)
- [x] `GameCatalog` (`ListAvailable`, `Resolve`)
- [x] `GameSessionManager` (per-`matchId` lock, `BoardId → MatchId` routing)
- [x] `MockDetectionSource`
- [x] SQLite `DartsDbContext` + configurations + repositories + first migration
- [x] `RecordDetectedThrowCommand`, `RecordEndOfTurnCommand`, `UndoLastThrowCommand` + handlers
- [x] `DetectionEndpoints`: `manual-throw`, `manual-end-turn`, `undo`
- [x] `Match.InputSource` + manual `BoardId` binding on manual-match start
- [x] X01 post-build target copying DLL into `Darts.Api/plugins/Darts.Games.X01/`

### Verification
- [x] Bare API endpoints exercised via Scalar/curl
- [x] Integration test: full mock 501 leg end-to-end (commands → `IGame` → persisted `ThrowRecord`s)
- [x] Integration test: full manual 501 leg (throws + `Miss` + early end-turn + undo of a busting dart across a leg boundary), **no streaming source running**
- [x] Confirm plugin loading is genuinely dynamic: delete/rebuild plugin DLL independently, confirm host picks it up from `plugins/` without a solution-wide rebuild

**Done when:** a full 501 leg is playable and asserted via API + tests — from both mock stream and pure manual entry, zero hardware — with the plugin genuinely loaded from a `plugins/` folder DLL, before any UI exists.

---

## Phase 2 — Web UI + SignalR

- [x] `GameHub` (`JoinMatch`)
- [x] `IGameNotifier` / `GameHubNotifier`
- [x] `wwwroot/index.html` + `scoreboard.js` (players, remaining score, current visit, last N throws, leg/set score, winner banner, start-match form incl. input-source selector) — chrome only
- [x] `<div id="game-board">` region in `index.html` + default fallback renderer in `scoreboard.js` (generic `payload` dump when a game ships no `render.js`)
- [x] Per-game `ui/render.js` post-build copy target convention (`ui/` folder → `Darts.Api/wwwroot/plugins/{gameId}/`), wired for `Darts.Games.X01` even if it ships no custom renderer
- [x] `dartboard.js` — clickable SVG board (wedges + bull) + Miss/Undo/End-turn controls
- [x] Minimal player create/list
- [x] `Program.cs` wiring: `AddSignalR()`, `UseStaticFiles()`, `MapHub`, `AddDartsDispatcher()`, endpoint groups

**Done when:** manually drive a match through the browser — start a `Manual` match, click segments/rings, confirm live SignalR updates, leg/match completion banner, and the default board-region fallback rendering — with no tracker connected.

---

## Phase 3 — AutoDarts adapter

### Gate — resolve before building the adapter
- [ ] Confirm local board-manager exposes real-time throw events (vs. cloud-only) — hard gate on the "no cloud" constraint
- [ ] Confirm throw event schema + notation (segment/ring shape)
- [ ] Confirm turn-boundary / match-state signal → maps to `DetectionEventType.EndOfTurn`
- [ ] Confirm one fused throw per physical dart (not per-camera duplicated)
- [ ] Decide correction/retract event handling (map to `UndoLastThrow`, or ignore in v1)
- [ ] Capture real sample frames from a live board manager

### Build
- [ ] `AutoDartsDetectionSource.cs` + exponential-backoff reconnect
- [ ] `Detection:Mode` DI switch (`AutoDarts` / `Mock`)
- [ ] Local test server in `Infrastructure.IntegrationTests` replaying canned frames

**Done when:** adapter runs against the replayed-frame test server, reconnects correctly after a dropped connection, and the wire-format gate above was resolved with real captured samples — before ever touching real hardware.

---

## Phase 4 — Hardening

- [ ] Logging
- [ ] Board connected/disconnected UI indicator
- [ ] Reconnect resilience
- [ ] `appsettings` profiles
- [ ] Startup script launching `Darts.Api` on the target device (assumes AutoDarts already running; points `Detection:AutoDarts:*` at it)

---

## Phase 5 — Stretch (not required for "v1 done")

- [x] Second game plugin (**Cricket**) — validates the plugin architecture claim end-to-end with zero core changes
- [x] Second game ships its own `ui/render.js` rendering a board region structurally different from X01's — Cricket's marks-grid table, not X01's two-big-numbers panel

---

## Standing checks (every phase)

- [x] `dotnet build` passes
- [x] `dotnet test` (all layers) passes
