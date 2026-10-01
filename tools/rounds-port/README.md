# rounds-port

Finds and fixes what breaks ROUNDS mods on the **current game build** (the 2025 update, Unity 2022.3). Works on
Windows, macOS and Linux, on compiled DLLs: you don't need the mod's source to see what's wrong, and `fix` writes a
patched copy you can test straight away.

```sh
rounds-port scan MyMod.dll     # list everything that no longer matches the game
rounds-port fix  MyMod.dll     # rewrite what it can, save ported/MyMod.dll, list what's left
```

Every problem is marked:

- **AUTO**: `fix` rewrites it.
- **REVIEW**: `fix` rewrites it or it's probably fine, but check it.
- **MANUAL**: change your source. The report says what the game has now.

## Setup

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download) (or newer) and ROUNDS installed through Steam.
It finds the game automatically, or pass `--game <folder>`.

```sh
git clone https://github.com/KieranK07/rounds-mac-modpack
cd rounds-mac-modpack/tools/rounds-port
dotnet run -c Release -- scan path/to/MyMod.dll
```

Mods are checked against UnboundLib 4 ([Bknibb's port](https://github.com/Bknibb/UnboundLib), the one that works on
the current build). Put it in `BepInEx/plugins` or pass `--ref <folder>`. The report says which UnboundLib it used.

Options: `--game <dir>`, `--ref <dir>` (repeatable, extra DLLs your mod uses), `-o <dir>` (where `fix` saves),
`--pdb` (also write a `.pdb`, for line numbers in error stack traces).
You can pass several DLLs or a whole folder. Exit code: 0 nothing to do, 1 only AUTO/REVIEW items, 2 MANUAL items.

## Hot reload: swap a mod into the running game

```sh
rounds-port hot MyMod.dll            # port it (if needed) and swap it into the running game
rounds-port hot bin/Debug/MyMod.dll --watch   # ...and again every time you rebuild
```

This needs the **Hot Reload** plugin ([`src/HotReload`](../../src/HotReload), installed by this repo's installer) in
`BepInEx/plugins`. It replaces BepInEx's ScriptEngine: mods in `BepInEx/scripts` load at startup like normal
plugins, and when one changes only that mod (and mods that depend on it) is reloaded, about a second later.
F6 reloads everything. How it works and what it can't undo: [`docs/HOTRELOAD.md`](../../docs/HOTRELOAD.md).

When a mod unloads, Hot Reload removes what the old copy left behind, so the new copy starts clean:

- its Harmony patches (whatever Harmony ID it used) and MonoMod hooks (`On.X.Y +=`, `Hook`, `ILHook`)
- its cards: out of UnboundLib, the toggle-cards menu, the card pool and the library registries
- its menus, game-mode hooks and other callbacks it registered, and handlers on game and Unity events
- its components, and the asset bundles it opened (so the new copy can open them again)

`hot` also moves any copy of the same mod out of `BepInEx/plugins` (into `BepInEx/plugins-parked-by-rounds-port`),
since that copy would load too. If the game was started with that copy, restart once.

Limits: players keep the cards they already have until the next round. A mod loaded for the first time while the
game is running can't add a new card rarity (RarityLib only accepts those at startup): put it in `BepInEx/scripts`
and restart once, after that every reload works.

## What `fix` handles

| Old | Now | Rewrite |
|---|---|---|
| `Player.playerID`, `Player.teamID` fields | `PlayerID`, `TeamID` properties | reads use the property. Writes use `SetPlayerID`, or the private `m_teamID` (`AssignTeamID` also syncs Photon) |
| `CharacterData.maxHealth` field | `MaxHealth` property | reads use the property. Writes use `m_maxHealth` (the setter can unlock an achievement) |
| `CardInfo.cardName` reads | private, and empty for UnboundLib 4 cards | a helper that falls back to the localized key, `CardName`, then the GameObject name |
| `CallTakeDamage`, `TakeDamage`, `DoDamage`, `TakeDamageOverTime` | gained a trailing `HealthHandler.DamageSource` | passes `DamageSource.Player` |
| Photon RPCs to those methods (`RPCA_SendTakeDamage`) | one more argument | appends `DamageSource.Player` (PUN drops RPCs with the wrong argument count) |
| `PlayerManager.AddPlayerDiedAction(...)` | removed; `PlayerDiedAction` is a public field | adds the handler to the field |
| `Optionshandler.vol_Master` / `vol_Sfx` | removed | reads the options slider (REVIEW) |
| `Steamworks.*` in Assembly-CSharp-firstpass | `com.rlabrecque.steamworks.net` | retargets the reference |
| `UnityEngine.Input` in CoreModule | `UnityEngine.InputLegacyModule` | retargets the reference |
| `CardChoice.GetRanomCard` | `GetRandomCard` (typo fixed) | Harmony targets and strings |
| Harmony `___field` / reflection `"field"` that became `m_field` | renamed | renames it (REVIEW) |

Helpers are copied into your mod as an internal `__RoundsCompat` class (source: `tools/compathelpers`), so the fixed
DLL has no new dependency.

## What it flags for you

- Harmony targets that are gone, renamed, or now ambiguous (e.g. `CardBar.OnHover` has two overloads), and patch
  parameters that don't match the new method
- reflection by name (`AccessTools`, `Traverse`, `GetField`...) that no longer finds anything
- overrides that broke because a base method's signature changed (a `TypeLoadException` at load)
- known behaviour changes: object pooling (`ObjectsToSpawn.SpawnObject` returns `PoolableWrapper[]`; don't
  `Destroy()` pooled objects), `TrickShot` setup moved to `Start`, `ChangeColor` is an empty marker, Odin Serializer
  is no longer shipped with the game

The full old to new list is in [`docs/MAPPING.md`](../../docs/MAPPING.md).

## How well it works

Tested on the 12 mods this repo ports (in `docs/`):

- for 8 of them (Cosmic Rounds, Classes Manager Reborn, RarityLib, ModsPlus, Will's Wacky Map Objects, CardBarPatch,
  GunUnblockablePatch, TemporaryStatsPatch), `fix` produces a byte-identical copy of the patch that was tested in game
- for the other 4 (ModdingUtils, MapsExtended, GrowPatch, Performance Improvements), it fixes the mechanical parts
  and flags what needed hand-written changes
- scanning the 28 game-facing DLLs of the working pack finds 2 REVIEW notes, both harmless
- 3 Thunderstore mods outside this pack (Cards+, KeysCards, ZOMC): fixed with no MANUAL items and load in game;
  KeysCards and ZOMC also swap in and out of the running game

It can't see everything: behaviour changes that still compile and resolve (e.g. pooled objects reused while you
hold a reference) only show up in game.

Built with [Mono.Cecil](https://github.com/jbevain/cecil) (MIT). MIT license, like the rest of this repo's own code.
