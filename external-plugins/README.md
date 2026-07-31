# external-plugins/

Vendored, **prebuilt** game plugin packages — for games whose source lives in a different repo and that you
don't want to pull into `src/Games/`. This folder holds build output only: no `.csproj`, no `.cs`/source
files, no `bin`/`obj`. Everything under here is copied into `plugins/{gameId}/` automatically by
`src/Barrelo.Api/Barrelo.Api.csproj` on every `dotnet build`/`dotnet run`/`dotnet publish` — no manual
copying, no solution/project wiring.

Each subfolder name **must** equal the game's `gameId` exactly — plugin UI assets are served from
`/plugins/{gameId}/...`, and (for client-owned games) the folder name is also matched against the
`gameId` in `plugin.json`.

If you'd rather not check a build artifact into git at all — e.g. copying straight onto an already-running,
already-deployed server without touching this repo — see the "Deploy it" step in the main
[README.md](../README.md#adding-a-new-game) instead. This folder is for the opposite case: you want the
plugin to ship with every clone/publish of this repo.

## In-process (.NET) plugin

```
external-plugins/
  yourgame/
    Barrelo.Games.YourGame.dll
    render.js      (optional — falls back to a raw payload dump if missing)
    style.css      (optional)
```

Build the plugin project elsewhere (referencing only `Barrelo.GameSdk`) and copy just its output DLL plus
`ui/render.js` / `ui/style.css` here — same layout the in-repo `src/Games/*` projects produce.

## Client-owned (browser) plugin

```
external-plugins/
  yourgame/
    plugin.json
    ui/index.html   (your board; falls back to the render.js convention, then a raw payload dump)
    ui/assets/...   (whatever your board loads)
```

Static files only — nothing is spawned and nothing is installed, so there's no `node_modules` to vendor
and no runtime for the deployment machine to provide.

See the main README's ["Client-owned games"](../README.md#client-owned-games-any-ui-engine-no-net) section
for the `plugin.json` schema and the postMessage contract.
