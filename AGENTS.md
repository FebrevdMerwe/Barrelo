Barrelo is a self-hosted, plugin-architected dart platform (.NET 10) — detection, game rules, and UI are
decoupled from each other.

## Commands

- `dotnet build Barrelo.slnx` — builds everything; also copies each game plugin's DLL + `ui/` assets into
  `src/Barrelo.Api/plugins/{gameId}/` automatically.
- `dotnet run --project src/Barrelo.Api` — runs the API at `http://localhost:5295` (applies EF Core
  migrations automatically).
- `dotnet run --project tools/Barrelo.BoardSimulator` — runs the hardware-free board simulator at
  `http://localhost:5250`.
- `dotnet test Barrelo.slnx` — runs all unit + integration tests; this is the primary correctness gate
  (see `PLAN.md`'s Verification section) — should pass before considering a change done.

## Where to look for more

- `README.md` — features, setup, configuration, adding a game, adding a detector.
- `PLAN.md` — architectural rationale (why no MediatR, ALC plugin loading, detection-source design history).
- `SCOPE.md` — long-term product vision.
- `deploy/README.md` — Proxmox/systemd deployment.

## Hard rules

- No MediatR — use the in-house `IDispatcher` (`Application/Common/Dispatch`); never reintroduce MediatR.
- Game plugins (`src/Games/*`) reference **only** `Barrelo.GameSdk` — never `Domain`, `Application`, or `Api`.
- `IGame` is pull-based — a plugin must never raise callbacks into host code (breaks `AssemblyLoadContext`
  unloading).
- `GameStateSnapshot`'s envelope stays game-agnostic — game-specific data goes only in `Payload`.
- Every detector goes behind `IDetectionSource` — never special-case a specific detector elsewhere.
- Plugin DLLs land in `plugins/{gameId}/` automatically via `Directory.Build.targets` on build — don't
  hand-copy or hand-edit that folder.
- A game with no source in this repo (built prebuilt elsewhere) goes under `external-plugins/{gameId}/`
  (build output only), never `src/Games/` (in-repo source only) — see `external-plugins/README.md`.
