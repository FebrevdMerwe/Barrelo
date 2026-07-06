# barrelo-remote-game-node — reference out-of-process game

A minimal, dependency-free example proving Barrelo's out-of-process game contract works for a rules engine
that isn't .NET and a board that isn't hand-rolled JS: `server.js` is a plain Node.js `http` server (no
`npm install` needed) implementing the RPC contract, and `ui/index.html` renders the board with PixiJS,
loaded into the host page's scoreboard via an `<iframe>` — never as C#, never as a global JS function.

This lives outside `Barrelo.slnx` on purpose and isn't part of `dotnet test` — see the README's
"Out-of-process games" section for why. Copy this folder as your own starting point; nothing here is
Barrelo-specific beyond the plain-HTTP contract described in that section.

## The game: Round the Clock

Each player hits 1 through 20 in order (any ring counts); first to complete 20 wins. Chosen deliberately to
look nothing like X01/Cricket/Kickoff's score-countdown shape — this is target-progression — so it actually
exercises the contract rather than reskinning an existing rules engine.

## Run it

1. Install [Node.js](https://nodejs.org) (any reasonably recent version — no packages to `npm install`).
2. Copy this whole folder into Barrelo's plugins directory, **renaming it to `round-the-clock`** — the
   folder name must match `plugin.json`'s `gameId` exactly, since the scoreboard fetches `ui/index.html`
   from `/plugins/{gameId}/...`. Copying the folder without renaming it (e.g. leaving it as
   `barrelo-remote-game-node`) is the single most common mistake here: the game still plays correctly (the
   RPC/spawn side doesn't care about the folder name), but the board silently falls back to a plain JSON
   dump because the UI asset URL no longer resolves. Barrelo's log will warn about this exact mismatch on
   startup if you hit it.
   ```bash
   cp -r examples/barrelo-remote-game-node <path-to-barrelo>/src/Barrelo.Api/plugins/round-the-clock
   ```
3. Start Barrelo as usual (`dotnet run --project src/Barrelo.Api`) — do **not** run `node server.js`
   yourself; Barrelo spawns it the moment a match starts (see manual checklist below).

## Manual verification checklist

- [ ] "Round the Clock" appears in the start-screen game picker with no `node` process running yet
      (check your process list) — proves `Describe()` only reads `plugin.json`, never spawns anything.
- [ ] Starting a match spawns a `node server.js --port ...` process (visible in your process list/task
      manager) and the match opens successfully.
- [ ] The PixiJS ring renders in the iframe and updates live as you throw (manual entry is enough — no
      hardware needed).
- [ ] Undo and End Turn both work and are reflected on the board.
- [ ] Completing the game (some player hits 20) shows the normal win banner, and the `node` process for
      that match is no longer running afterward.
- [ ] Killing the `node` process manually mid-match (e.g. `kill <pid>`) surfaces a "Game interrupted" banner
      in the scoreboard within a few seconds, instead of hanging.
