# ROUNDS modded on Mac (and Windows)

One command takes a Mac from **no ROUNDS** to **31 mods working** (UnboundLib, Cosmic Rounds, RoundsWithFriends,
Classes Manager Reborn, MapsExtended and friends) on the **current** ROUNDS build.

```sh
curl -fsSL https://raw.githubusercontent.com/KieranK07/rounds-mac-modpack/main/install-mac.sh | bash
```

On **Windows**, the same pack (file for file, so Mac and Windows players can play together), in PowerShell:

```powershell
irm https://raw.githubusercontent.com/KieranK07/rounds-mac-modpack/main/install-windows.ps1 | iex
```

Paste that into Terminal (or PowerShell). It will:

1. install ROUNDS through Steam if you don't have it yet (you need to own it and click **Install**)
2. install BepInEx 5.4.23.5, set up to run **natively on Apple Silicon** (no Rosetta) on a Mac
3. download every mod **from its original author** (Thunderstore / GitHub), checking SHA-256s
4. apply this repo's patches on your machine, checking every result
5. on a Mac, set the ROUNDS launch option in Steam (Steam restarts once); then start the game

Everyone you play with needs the same setup, on the current ROUNDS build (Steam → ROUNDS → Properties → Betas →
**None**, not `old-rounds-for-mods`).

**Status:** tested on an Apple M4, macOS 27.2, both native and under Rosetta. Intel Macs should work but are
untested. Windows: tested on a Windows PC with Windows PowerShell 5.1.
Online through RoundsWithFriends: tested with two Macs and a Windows PC in one lobby.

Running it again is safe: it checks every file against the pack, adds what's missing and updates what it installed
before, but leaves files you changed (and mods you added) alone and lists them. `--repair` puts the pack's copies
back, moving yours to a backup folder.

Options: `--no-steam-config` (don't edit Steam; paste the launch option yourself), `--no-launch`,
`--game-dir <path>`, `--rosetta` (run under Rosetta instead of natively), `--repair`.
Windows: `-Repair`, `-NoLaunch`, `-GameDir <path>`, passed as
`& ([scriptblock]::Create((irm <url>))) -Repair`.
To switch for one launch, put `ROUNDS_ARCH=x86_64,arm64` (Rosetta) or `ROUNDS_ARCH=arm64,x86_64` (native) in front
of the Steam launch option. To run from a clone: `./install-mac.sh`.

## Why this exists

Two things broke modded ROUNDS on Mac:

- **The 2025 ROUNDS update** renamed and removed game code that every mod used (`playerID`, `teamID`,
  `maxHealth`, damage methods, card names, …). The `old-rounds-for-mods` beta branch that Windows players fall
  back to **has no macOS build**, so the mods themselves had to be ported. [Bknibb](https://github.com/Bknibb) had
  already ported UnboundLib and RoundsWithFriends; this repo ports the rest.
- **macOS itself**: BepInEx 5.4.23.5 can't patch arm64 code, UnboundLib calls a Windows-only API at startup,
  and every mod's asset bundle only contains DirectX shaders (pink text and effects on Metal).

## What's changed

| Area | Fix |
|---|---|
| 12 mods' DLLs | Renamed/removed game members rewritten (Mono.Cecil IL patches, `tools/compatfix`), ModdingUtils rebuilt from source (`tools/moddingutils`) |
| BepInEx 5.4.23.5 | Patched to the BepInEx source with [cdobbyn's native arm64 fix](https://github.com/BepInEx/BepInEx/pull/1402) (merged, not released yet; `tools/bepinex-arm64`), plus UnityDoorstop 4.6.0 |
| UnboundLib 4.2.5 | Windows-only `user32.dll` call removed, so it starts on Mac (`tools/unboundlib-macfix`) |
| MapsExtended | Odin Serializer stand-in built from the Apache-2.0 open-source version |
| Mac Compat Fixes plugin (`src/MacCompatFixes`) | Mod shaders swapped to the game's Metal copies or rebuilt; card names, card-bar hover, toggle-cards menu art; Cosmic Rounds runtime errors; menu layout |
| Hot Reload plugin (`src/HotReload`) | Swaps mods in `BepInEx/scripts` while the game runs and removes what the old copy left behind; replaces BepInEx ScriptEngine ([`docs/HOTRELOAD.md`](docs/HOTRELOAD.md)) |

Full details: [`docs/`](docs/). Each patch's before/after hash is in [`manifest/patches.tsv`](manifest/patches.tsv).

## Credits

This is a port. Almost all of it is other people's work — see **[CREDITS.md](CREDITS.md)** for every mod, its
authors, links and license, and **[NOTICE.md](NOTICE.md)** for what's downloaded vs. built here. In game:
**CREDITS → KIERAN'S UNBOUND**.

No mod files are re-uploaded here: the installer fetches them from their authors and patches them locally.
If you're an author and want something changed, please open an issue.

## Porting your own mod

[`tools/rounds-port`](tools/rounds-port) scans any ROUNDS mod DLL for what the 2025 update broke and fixes the
mechanical parts (renamed fields, new damage parameters, moved types, Harmony and reflection renames). It runs on
Windows, macOS and Linux and is how most of the mods here were ported.

```sh
cd tools/rounds-port && dotnet run -c Release -- scan path/to/MyMod.dll
```

`rounds-port hot MyMod.dll` ports it and swaps it into the running game.

## Developing

Mods in `BepInEx/scripts`, including `MacCompatFixes`, load through [Hot Reload](docs/HOTRELOAD.md): rebuild one
while the game is running and the new version is live a second later (**F6** reloads them all).

```sh
cd src/MacCompatFixes && dotnet build -c Release -p:Deploy=true
```

## Uninstall

Mac: Steam → ROUNDS → Properties → clear **Launch Options**. To remove everything, delete the `BepInEx` folder (and
`run_bepinex.sh`, `libdoorstop.dylib`) from the game folder, or uninstall ROUNDS.

Windows: delete `winhttp.dll` from the game folder (and `BepInEx`, `doorstop_config.ini` to remove everything).

## License

Original work here is MIT ([LICENSE](LICENSE)). Third-party components keep their own licenses
([NOTICE.md](NOTICE.md)). ROUNDS is © Landfall Games; not affiliated with Landfall or the mod authors.
