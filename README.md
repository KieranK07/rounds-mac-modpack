# ROUNDS modded on Mac

One command takes a Mac from **no ROUNDS** to **31 mods working** (UnboundLib, Cosmic Rounds, RoundsWithFriends,
Classes Manager Reborn, MapsExtended and friends) on the **current** ROUNDS build.

```sh
curl -fsSL https://raw.githubusercontent.com/KieranK07/rounds-mac-modpack/main/install-mac.sh | bash
```

Paste that into Terminal. It will:

1. install Rosetta 2 if needed (BepInEx 5 runs the game's Intel code)
2. install ROUNDS through Steam if you don't have it yet (you need to own it and click **Install**)
3. install BepInEx 5.4.23.5 and download every mod **from its original author** (Thunderstore / GitHub), checking SHA-256s
4. apply this repo's patches on your machine, checking every result
5. set the ROUNDS launch option in Steam (Steam restarts once) and start the game

Everyone you play with needs the same setup.

**Status:** tested on an Apple M4, macOS 27.2. Intel Macs should work but are
untested. Windows is planned (the patches are platform-independent, but the installer is Mac-only for now).
Online lobbies through RoundsWithFriends haven't been tested yet.

Options: `--no-steam-config` (don't edit Steam; paste the launch option yourself), `--no-launch`,
`--game-dir <path>`. To run from a clone: `./install-mac.sh`.

## Why this exists

Two things broke modded ROUNDS on Mac:

- **The 2025 ROUNDS update** renamed and removed game code that every mod used (`playerID`, `teamID`,
  `maxHealth`, damage methods, card names, …). The `old-rounds-for-mods` beta branch that Windows players fall
  back to **has no macOS build**, so the mods themselves had to be ported. [Bknibb](https://github.com/Bknibb) had
  already ported UnboundLib and RoundsWithFriends; this repo ports the rest.
- **macOS itself**: BepInEx 5 can't patch arm64 code (so the game runs under Rosetta), UnboundLib calls a
  Windows-only API at startup, and every mod's asset bundle only contains DirectX shaders (pink text and effects
  on Metal).

## What's changed

| Area | Fix |
|---|---|
| 12 mods' DLLs | Renamed/removed game members rewritten (Mono.Cecil IL patches, `tools/compatfix`), ModdingUtils rebuilt from source (`tools/moddingutils`) |
| UnboundLib 4.2.5 | Windows-only `user32.dll` call removed, so it starts on Mac (`tools/unboundlib-macfix`) |
| MapsExtended | Odin Serializer stand-in built from the Apache-2.0 open-source version |
| Mac Compat Fixes plugin (`src/MacCompatFixes`) | Mod shaders swapped to the game's Metal copies or rebuilt; card names, card-bar hover, toggle-cards menu art; Cosmic Rounds runtime errors; menu layout |

Full details: [`docs/`](docs/). Each patch's before/after hash is in [`manifest/patches.tsv`](manifest/patches.tsv).

## Credits

This is a port. Almost all of it is other people's work — see **[CREDITS.md](CREDITS.md)** for every mod, its
authors, links and license, and **[NOTICE.md](NOTICE.md)** for what's downloaded vs. built here. In game:
**CREDITS → KIERAN'S UNBOUND**.

No mod files are re-uploaded here: the installer fetches them from their authors and patches them locally.
If you're an author and want something changed, please open an issue.

## Developing

`MacCompatFixes` loads through BepInEx's ScriptEngine from `BepInEx/scripts`, so it hot-reloads: rebuild it while
the game is running and the new version is live a second later (or press **F6**).

```sh
cd src/MacCompatFixes && dotnet build -c Release -p:Deploy=true
```

## Uninstall

Steam → ROUNDS → Properties → clear **Launch Options**. To remove everything, delete the `BepInEx` folder (and
`run_bepinex.sh`, `libdoorstop.dylib`) from the game folder, or uninstall ROUNDS.

## License

Original work here is MIT ([LICENSE](LICENSE)). Third-party components keep their own licenses
([NOTICE.md](NOTICE.md)). ROUNDS is © Landfall Games; not affiliated with Landfall or the mod authors.
