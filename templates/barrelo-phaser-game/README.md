# barrelo-phaser-game — Phaser out-of-process game template

A starting point for building a Barrelo game with a [Phaser 3](https://phaser.io/) board, using Phaser's
own modern project conventions (TypeScript + Vite) instead of a hand-rolled `<script>` tag. This is a
deliberately blank skeleton: every RPC endpoint and the `GameStateSnapshot` envelope are wired correctly,
but the actual rules and rendering are TODOs for you to fill in.

See the top-level README's "Out-of-process games" section for the full contract this implements —
nothing here is Barrelo-specific beyond that plain-HTTP spec, so this folder works as a starting point
regardless of what game you're building.

## Prerequisites

- [Node.js](https://nodejs.org) (any reasonably recent version).
- Nothing else — no .NET SDK needed to iterate on this folder in isolation.

## Getting started

```bash
npm install    # installs both the server's and the ui/ workspace's dependencies
npm run dev    # starts the rules server, Vite, and the dev harness together
```

Then open **http://localhost:8090** — that's the dev harness, a control panel that stands in for
Barrelo. Click **Create match**, then **Send throw** / **Random throw** / **Bullseye** / **End
turn** / **Undo** and watch the board (on the right) react in real time. Press **Ctrl+C** in the
terminal to stop all three processes together.

That single `npm run dev` is all you need for day-to-day work: it starts

| Process      | What it is                                  | Port |
| ------------ | ------------------------------------------- | ---- |
| Rules server | your game logic (`src/server.ts`)           | 6100 |
| Vite         | hot-reloading Phaser board (`ui/src`)       | 5173 |
| Dev harness  | the Barrelo stand-in control panel (`dev/`) | 8090 |

and prefixes each process's output in the terminal so you can tell them apart. See
`dev/start-harness.js` if you ever want to run the three pieces separately (e.g. to restart just
Vite) — the README further down documents each command individually.

### How the harness works

The harness (`dev/harness-server.js` + `dev/harness.html`) polls the rules server's `GET /state`
every 500ms and forwards each snapshot into the board's `<iframe>` via
`postMessage({type: "barrelo:gameState", ...})` — the exact contract Barrelo itself uses — so
`ui/src/bridge.ts` and `BoardScene.ts` behave identically to how they will under the real host. It
also transparently proxies every other request to the rules server so the browser stays
same-origin (no CORS needed).

### Other ways to run things

- **Board only, no server** — `cd ui && npm run dev` boots the board standalone and feeds it a
  canned sample snapshot after a moment (see `ui/src/bridge.ts`), for quick rendering checks with
  nothing else running.
- **Rules server only** — `npx tsx src/server.ts --port 6100` (from the template root) if you want
  to exercise the RPC endpoints directly with `curl`.
- **Harness only** (server + Vite already running elsewhere) — `npm run harness`.

## Where to put your game

- **Rules** — `src/server.ts`. Everything is wired (the `/create`, `/throw`, `/end-turn`, `/undo`,
  `/state`, `/result` endpoints, the replay-from-log pattern that makes undo trivial), but `replay()`
  never actually finishes a game. That function, and the `TemplatePayload`/`GameState` shapes around it,
  are the TODOs.
- **Rendering** — `ui/src/scenes/BoardScene.ts`. Draws one placeholder token per player; replace its
  `render()` method with whatever your game's board actually needs. Keep the `payload` shape in
  `BoardScene.ts`'s `TemplatePayload` interface in sync with whatever `src/server.ts` puts in
  `GameStateSnapshot.payload` — they're two ends of the same contract, not independently typed.
- **Shared wire types** — `shared/types.ts`, imported by both sides. Add your own settings/enums here if
  you extend the contract; don't duplicate the shapes in `src/server.ts` or `ui/src`.

`plugin.json`'s `launch.command` runs `node node_modules/tsx/dist/cli.mjs src/server.ts ...` rather than
`npx tsx src/server.ts` — on Windows, `npx` resolves to `npx.cmd`, and Barrelo spawns your process via
.NET's `Process.Start` without a shell, which can't launch `.cmd` wrappers. Invoking `node` directly
against tsx's own CLI script works identically on Windows, Linux, and macOS. `npx tsx ...` is still fine
for your own interactive/dev use (that goes through your shell, which resolves `.cmd` normally) — just
don't change `plugin.json` back to it.

## Deploy — read this before copying the folder

Because the UI is now a Vite project, deploying isn't a single folder copy like the plain-JS examples.
Vite's _source_ `ui/index.html` references unbundled `/src/main.ts`, which only works under `npm run
dev` — Barrelo serves `plugins/{gameId}/ui/index.html` as a static file with no build step, so it needs
the **built** `ui/dist/index.html` instead.

1. Build the UI:
   ```bash
   npm install
   npm run build   # builds ui/ via Vite, producing ui/dist/
   ```
2. Copy the server side into Barrelo's plugins directory, **renaming the destination folder to your
   `gameId`** (same rule as every other out-of-process example — the folder name must match
   `plugin.json`'s `gameId` exactly, since the UI is fetched from `/plugins/{gameId}/ui/...`):
   ```bash
   DEST=<path-to-barrelo>/src/Barrelo.Api/plugins/your-game-id
   mkdir -p "$DEST"
   cp plugin.json package.json tsconfig.json "$DEST/"
   cp -r src shared "$DEST/"
   ```
3. Install the server's dependencies **at the destination**, not by copying `node_modules` — this
   template's root `package.json` is an npm workspace root, so its own `node_modules` has the `ui/`
   workspace's dependencies (Phaser, Vite, esbuild — 200+ MB) hoisted into it too, none of which the
   server needs at runtime. A fresh install at the destination only pulls in `tsx`:
   ```bash
   (cd "$DEST" && npm install --omit=dev)
   ```
4. Copy the **contents of `ui/dist/`** — not the `ui/` source folder — into `$DEST/ui/`:
   ```bash
   mkdir -p "$DEST/ui"
   cp -r ui/dist/. "$DEST/ui/"
   ```
5. Update `plugin.json`'s `gameId`/`displayName`/`description` to match your game before shipping it —
   the template ships with placeholder values (`your-game-id`, "Your Game").

Skipping step 4 (copying `ui/` itself instead of `ui/dist/`'s contents) is the most likely mistake here:
the server will still run fine, but the board will silently fall back to a plain JSON dump because the
unbuilt `index.html`'s module script can't load without Vite's dev server behind it.

## Smoke test (plumbing, not gameplay)

There's no real game to verify yet, but confirm the wiring works end to end:

- [ ] `curl http://127.0.0.1:6100/health` (with the server running standalone) responds `{"status":"ok",...}`.
- [ ] After deploying, your game appears in Barrelo's start-screen game picker with no `node`/`tsx`
      process running yet — proves the manifest is read without spawning anything.
- [ ] Starting a match spawns the process and shows the placeholder board (tokens + names), not a raw
      JSON dump.
- [ ] Throwing (manual entry is enough) increases a player's throw count and triggers the scale-pop
      tween on their token — proves a throw reaches `src/server.ts`, round-trips through `GET /state`,
      and reaches `BoardScene` via the postMessage bridge.
- [ ] Undo reverts the last throw or end-of-turn.

## Testing

This template intentionally lives outside `Barrelo.slnx` and isn't part of `dotnet test`, same as the
other out-of-process examples — verify it manually using the checklist above.
