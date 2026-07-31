# barrelo-phaser-game — Phaser client-owned game template

A starting point for building a Barrelo game with a [Phaser 4](https://phaser.io/) board, using Phaser's
own modern project conventions (TypeScript + Vite) instead of a hand-rolled `<script>` tag. This is a
deliberately blank skeleton: the contract with Barrelo is wired correctly end to end, but the actual rules
and rendering are TODOs for you to fill in.

Your game is **entirely a browser app**. There is no server half, no process for Barrelo to spawn, and no
HTTP contract to implement — Barrelo keeps the log of what was thrown and pushes it to your board, and
your board works out what it means. See the top-level README's "Adding a new game" section for the full
contract.

## Prerequisites

- [Node.js](https://nodejs.org) — to build. Nothing needs Node at *runtime*, including on the machine
  running Barrelo: the build output is static files.
- Nothing else — no .NET SDK needed to iterate on this folder in isolation.

## Getting started

```bash
cd ui && npm install
npm run dev          # Vite, on http://localhost:5173
```

That alone boots the board and feeds it a canned payload after a moment (see `ui/src/bridge.ts`), which is
enough for rendering work.

To actually play it, open **`dev/harness.html`** in a browser as well. It stands in for Barrelo: it keeps
the visit log the same way the host does, posts the same `barrelo:gameState` message into your board, and
shows you the messages your game sends back. Click segments to throw, and use End turn / Undo / Reset.

## The determinism contract — read this one

Barrelo keeps the visit log; **every screen showing the match replays it independently** — the control
tablet in someone's hand, the TV on the wall, and any tab that gets refreshed mid-match. They only agree
if replaying the same log always produces the same state.

So inside `replay()` (and anything it calls):

- No `Math.random()` — use the `rng(payload.seed)` helper in `ui/src/rules.ts`. The seed is fixed for the
  match and identical on every screen.
- No `Date.now()` / `new Date()` — use `throw.detectedAtUtc` from the log.
- No `crypto.randomUUID()` — derive ids from the log (e.g. `throw.throwId`).
- No module-level mutable state, no `localStorage`, no `fetch`.

Break any of these and the TV quietly shows something different from the tablet. Barrelo does detect it —
every screen reports a hash of its derived state and the host logs a warning when two disagree — but the
fix is always in `replay()`.

## Where to put your game

- **Rules** — `ui/src/rules.ts`. `replay(payload)` is a pure fold over the visit log and is the only place
  your rules live. It gets the roster (`payload.playerIds`, `payload.playerGroups`), the match options,
  the seed, and every dart thrown so far; it returns whose turn it is, whatever your game tracks, and
  whether anyone has won. Undo needs no code at all — Barrelo shortens the log and you re-derive.
- **Rendering** — `ui/src/scenes/BoardScene.ts`. Draws one placeholder token per player; replace its
  `render()` method with whatever your board actually needs. It renders the state `replay()` derived, not
  the raw payload.
- **Wire types** — `shared/types.ts`. The shapes Barrelo sends and expects; don't redeclare them locally.
- **The bridge** — `ui/src/bridge.ts` connects the two and talks to Barrelo. You shouldn't need to change
  it, but it's worth reading once: it's the whole contract in one short file.

### What Barrelo does and doesn't know

Barrelo records darts and nothing else. In the snapshot it pushes you, `currentPlayerId` is **always
null** and `legNumber`/`setNumber` are **always 1** — those are rules output, and your rules own them.
Read them from your own replay.

That's also why the bridge sends a `barrelo:display` message back up on every change: it's what drives the
turn indicator, the dart 1/2/3 slots, the leg/set label, and greyed-out targets in Barrelo's own chrome
around your board. And when your `replay()` sets `isComplete`, the bridge sends `barrelo:matchComplete`
once, which is what ends the match and awards session-leaderboard points from your `finalStandings`.

## Deploy

Barrelo serves `plugins/{gameId}/ui/index.html` as a static file with no build step, so it needs the
**built** output, not the Vite sources.

1. Build the UI:
   ```bash
   cd ui && npm run build     # produces ui/dist/
   ```
2. Copy the manifest and the built board into Barrelo's plugins directory, **renaming the destination
   folder to your `gameId`** — the folder name must match `plugin.json`'s `gameId` exactly, since the UI
   is fetched from `/plugins/{gameId}/ui/...`:
   ```bash
   DEST=<path-to-barrelo>/src/Barrelo.Api/plugins/your-game-id
   mkdir -p "$DEST/ui"
   cp plugin.json "$DEST/"
   cp -r ui/dist/. "$DEST/ui/"
   ```
3. Update `plugin.json`'s `gameId`/`displayName`/`description` before shipping — the template ships with
   placeholders (`your-game-id`, "Your Game").

That's the whole deployment: a manifest and a folder of static files. No `npm install` at the destination,
no `node_modules`, no runtime dependencies.

Copying `ui/` itself instead of the contents of `ui/dist/` is the likely mistake: the board will silently
fall back to a plain JSON dump, because the unbuilt `index.html`'s module script can't load without Vite's
dev server behind it.

## Smoke test (plumbing, not gameplay)

There's no real game to verify yet, but confirm the wiring works end to end:

- [ ] `dev/harness.html` shows the placeholder board (tokens + names), and clicking segments moves the
      turn ring and updates each player's score.
- [ ] The harness's "Sent up by your game" panel shows a `barrelo:display` message with a
      `currentPlayerId` and a `stateHash` after each throw.
- [ ] Undo in the harness reverts the last throw, and undo straight after "End turn" re-opens that visit
      rather than dropping a dart.
- [ ] After deploying, your game appears in Barrelo's start-screen game picker.
- [ ] Starting a match shows the placeholder board, not a raw JSON dump, and throwing (manual entry is
      enough) updates it — that proves the whole path: dart → host log → `barrelo:gameState` → `replay()`
      → `BoardScene`.
- [ ] With `view.html` open on a second screen, both show the same thing after the same throws.

## Testing

This template intentionally lives outside `Barrelo.slnx` and isn't part of `dotnet test` — verify it
manually using the checklist above.
