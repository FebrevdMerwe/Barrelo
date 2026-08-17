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
shows you the messages your game sends back. Click segments to throw, and use End turn / Undo / Reset. It
starts as two teams of two (Barrelo's default); the Roster button swaps to four solo players so you can
check both without editing anything.

## Teams are the default — read this one too

Barrelo is a team platform first. A match is **N teams**, and a solo match is simply **N teams of one** —
there is no separate solo mode, and no game should have a separate solo code path.

So the template starts you on the team shape:

- `plugin.json` declares a `playerGroup` setting. That's what makes Barrelo's start screen show the team
  chalkboard instead of a flat "Playing" bucket — **delete it and your game becomes solo-only**, because
  the host then never collects a team assignment and sends you an empty `playerGroups`.
- `replay()` in `ui/src/rules.ts` folds the roster into teams (`buildTeams()`), scores per team, and
  rotates one visit per team per round — each team rotating its own thrower. Rotating over the flat
  roster instead would give a three-player team three visits a round against a two-player team's two,
  which decides most games on roster size alone.
- `BoardScene` draws one token per team, and marks the thrower within it.

Solo needs no work from you: `effectiveGroupIndex()` falls a player with no assignment back to their own
roster position — an implicit team of one — mirroring the host's `GameSetupExtensions.EffectiveGroupIndex`
exactly. Four solo players are four teams of one, and every rule you wrote for teams already holds.

`winnerPlayerIds` and `finalStandings` stay lists of *player* ids: a winning team contributes all of its
members, and standings list each team's members together, best team first. That's what gets every member
of a winning team their leaderboard points.

Two things worth knowing before you change the setting:

- Declaring a `playerGroup` setting makes team assignment **mandatory**, but it does *not* require an
  opponent: one player in one team is a valid match, exactly as it is for X01, Cricket and Around The
  Clock. If your game genuinely can't be played alone, say so — `"minPlayers": 2` at the manifest's top
  level, and `"minGroups": 2` inside the `playerGroup` setting if the second player has to be on the
  *other* team (Kickoff's case, since every dart is a kick at one of two goals). Both default to `1`.
- `maxGroups`/`maxPlayersPerGroup` in the manifest are your game's real limits (the template ships 4 and
  4). In-repo games range from 2 teams (X01, Kickoff) to 6 (Around The Clock).

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
  the seed, and every dart thrown so far; it returns the teams, whose turn it is, whatever your game
  tracks per team, and who has won. Undo needs no code at all — Barrelo shortens the log and you
  re-derive.
- **Rendering** — `ui/src/scenes/BoardScene.ts`. Draws one placeholder token per team; replace its
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
   placeholders (`your-game-id`, "Your Game"). Set the `teams` setting's `maxGroups`/`maxPlayersPerGroup`
   to your game's real limits while you're there; keep the setting itself unless your game genuinely
   cannot be played in teams (see "Teams are the default" above).

That's the whole deployment: a manifest and a folder of static files. No `npm install` at the destination,
no `node_modules`, no runtime dependencies.

Copying `ui/` itself instead of the contents of `ui/dist/` is the likely mistake: the board will silently
fall back to a plain JSON dump, because the unbuilt `index.html`'s module script can't load without Vite's
dev server behind it.

## Smoke test (plumbing, not gameplay)

There's no real game to verify yet, but confirm the wiring works end to end:

- [ ] `dev/harness.html` shows the placeholder board — **two** tokens, each labelled with two names — and
      clicking segments moves the turn ring and updates that team's score.
- [ ] End turn passes the throw to the *other* team, and after a full round the first team's second member
      is the one marked as throwing.
- [ ] The Roster button switches to solo: four tokens, one name each, and everything else behaves the
      same. (Same code path — this is the check that you haven't special-cased either mode.)
- [ ] The harness's "Sent up by your game" panel shows a `barrelo:display` message with a
      `currentPlayerId` and a `stateHash` after each throw.
- [ ] Undo in the harness reverts the last throw, and undo straight after "End turn" re-opens that visit
      rather than dropping a dart.
- [ ] After deploying, your game appears in Barrelo's start-screen game picker **with the team chalkboard**
      (not a single "Playing" bucket) — that's what confirms the manifest's `playerGroup` setting parsed.
- [ ] Starting a match shows the placeholder board, not a raw JSON dump, and throwing (manual entry is
      enough) updates it — that proves the whole path: dart → host log → `barrelo:gameState` → `replay()`
      → `BoardScene`.
- [ ] With `view.html` open on a second screen, both show the same thing after the same throws.

## Testing

This template intentionally lives outside `Barrelo.slnx` and isn't part of `dotnet test` — verify it
manually using the checklist above.
