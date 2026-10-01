# Credits

This patcher downloads every mod below from its original Thunderstore or GitHub page and patches it on your machine. We don't re-upload anyone's DLLs; the one exception is the OdinSerializer build, which its Apache-2.0 license allows us to ship.

Versions are the ones the patcher targets. "none found" means we found no license in the repo or the Thunderstore package. Anything marked (unverified) is our best guess.

## Game

**ROUNDS** by **Landfall Games** ([Steam](https://store.steampowered.com/app/1557740/ROUNDS/), [full credits](https://landfall.se/rounds-credits)). Game design, art and programming by Wilhelm Nylund; card art by Pontus Ullbors; music by Karl Flodin; sound by Vile Hartman and Victor Engström; face customizer art by Natalia Martinsson. ROUNDS is proprietary and not included; you need your own copy.

## Core libraries

| Mod | Authors | Links | License |
|---|---|---|---|
| **UnboundLib (Bknibb fork)** 4.2.5<br>Core modding library: networking, custom cards/maps, game-mode framework, mod menus. | Bknibb (fork maintainer; ROUNDS v1.1.2 update), willis81808 / Willis (creator), olavim / Tilastokeskus, Pykess (GitHub: pdcook), BossSloth (Boss Sloth Inc.), willuwontu, otDan, Ascyst, Root (GitHub: Tess-y, System-of-Root), scottfp | [Source](https://github.com/Bknibb/UnboundLib) | none found |
| **RoundsWithFriends (Bknibb fork)** 3.0.10<br>Multiplayer extension: more than two players, Deathmatch and Team Deathmatch modes. | Bknibb (fork maintainer; ROUNDS v1.1.2 update), olavim / Tilastokeskus (creator), Pykess (GitHub: pdcook), BossSloth, willuwontu | [Source](https://github.com/Bknibb/RoundsWithFriends) | GPL-3.0 |
| **MapsExtended** 1.4.2<br>Custom map framework and in-game map editor. | olavim (Olavi Mustanoja, aka Tilastokeskus), Ascyst, BossSloth, Root (GitHub: Tess-y) | [Thunderstore](https://thunderstore.io/c/rounds/p/olavim/MapsExtended/) · [Source](https://github.com/olavim/RoundsMapsExtended) | MIT |
| **ModdingUtils** 0.4.8<br>Shared utilities that many card and effect mods depend on. | Pykess (GitHub: pdcook), willuwontu, olavim, Root (GitHub: Tess-y), BossSloth | [Thunderstore](https://thunderstore.io/c/rounds/p/Pykess/ModdingUtils/) · [Source](https://github.com/pdcook/ModdingUtils) | GPL-3.0 |
| **ModsPlus** 1.6.2<br>Willis' modding helper library. | willis81808 (Reid Willis) | [Thunderstore](https://thunderstore.io/c/rounds/p/willis81808/ModsPlus/) · [Source](https://github.com/willis81808/ModsPlus) | MIT |
| **RarityLib** 1.3.0<br>Library for adding custom card rarities. | Root (GitHub: Tess-y) | [Thunderstore](https://thunderstore.io/c/rounds/p/Root/RarityLib/) · [Source](https://github.com/Tess-y/RarityLib) | Unlicense |
| **CardThemeLib** 1.1.7<br>Library for adding custom card themes. | Root (GitHub: Tess-y) | [Thunderstore](https://thunderstore.io/c/rounds/p/Root/CardThemeLib/) · [Source](https://github.com/Tess-y/CardThemeLib) | Unlicense |
| **Classes Manager Reborn** 1.5.5<br>Class system for card mods; remake of the original Classes Manager. | Root (GitHub: Tess-y), willuwontu | [Thunderstore](https://thunderstore.io/c/rounds/p/Root/Classes_Manager_Reborn/) · [Source](https://github.com/Tess-y/ClassesManagerReborn) | Unlicense |

## Content mods

| Mod | Authors | Links | License |
|---|---|---|---|
| **Cosmic Rounds (CR)** 2.7.0<br>Adds 100 new cards. | XAngelMoonX | [Thunderstore](https://thunderstore.io/c/rounds/p/XAngelMoonX/CR/) · [Source](https://github.com/XAngelMoonX/CosmicRounds) | none found |
| **SetRounds** 1.2.4<br>Set rounds per game and points per round from Mod Options. | Ascyst, Pykess (GitHub: pdcook; sync fixes) | [Thunderstore](https://thunderstore.io/c/rounds/p/Ascyst/SetRounds/) · [Source](https://github.com/Ascyst/SetRounds) | none found |
| **Will's Wacky Map Objects** 1.2.4<br>Adds extra objects to the MapsExtended map editor. | willuwontu | [Thunderstore](https://thunderstore.io/c/rounds/p/willuwontu/WillsWackyMapObjects/) · [Source](https://github.com/willuwontu/WillsWackyMapObjects) | none found |
| **CardBarPatch** 2.1.1<br>Customize the in-game card bar. | BossSloth (GitHub: BossSloth, formerly tddebart) | [Thunderstore](https://thunderstore.io/c/rounds/p/BossSloth/CardBarPatch/) · [Source](https://github.com/BossSloth/BossSlothsMods) | GPL-3.0 |

## Patches & fixes

| Mod | Authors | Links | License |
|---|---|---|---|
| **AllThePatch** 1.0.0<br>Modpack bundling the fundamental vanilla bug-fix patches. | RoundsModding (Thunderstore team; individuals unverified) | [Thunderstore](https://thunderstore.io/c/rounds/p/RoundsModding/AllThePatch/) | none found |
| **CardChoiceSpawnUniqueCardPatch** 0.1.10<br>Fixes broken logic in the vanilla CardChoice.SpawnUniqueCard method. | Pykess (GitHub: pdcook), willuwontu, Root (GitHub: Tess-y, System-of-Root), olavim | [Thunderstore](https://thunderstore.io/c/rounds/p/Pykess/CardChoiceSpawnUniqueCardPatch/) · [Source](https://github.com/Rounds-Modding/CardChoiceSpawnUniqueCardPatch) | none found |
| **DemonicPactPatch** 0.0.0<br>Fixes Demonic Pact breaking certain cards after a rematch. | Pykess (GitHub: pdcook) | [Thunderstore](https://thunderstore.io/c/rounds/p/Pykess/DemonicPactPatch/) · [Source](https://github.com/Rounds-Modding/DemonicPactPatch) | none found |
| **GunUnblockablePatch** 0.0.0<br>Makes the gun 'unblockable' property actually work. | Pykess (GitHub: pdcook) | [Thunderstore](https://thunderstore.io/c/rounds/p/Pykess/GunUnblockablePatch/) · [Source](https://github.com/Rounds-Modding/GunUnblockablePatch) | none found |
| **LegRaycastersPatch** 0.0.0<br>Fixes ground-detection physics issues for small players. | Pykess (GitHub: pdcook) | [Thunderstore](https://thunderstore.io/c/rounds/p/Pykess/LegRaycastersPatch/) · [Source](https://github.com/Rounds-Modding/LegRaycastersPatch) | none found |
| **PlayerJumpPatch** 0.0.2<br>Fixes the vanilla PlayerJump.Jump method. | Pykess (GitHub: pdcook) | [Thunderstore](https://thunderstore.io/c/rounds/p/Pykess/PlayerJumpPatch/) · [Source](https://github.com/Rounds-Modding/PlayerJumpPatch) | none found |
| **TeleportPatch** 0.0.1<br>Fixes several issues with Teleport. | Pykess (GitHub: pdcook) | [Thunderstore](https://thunderstore.io/c/rounds/p/Pykess/TeleportPatch/) · [Source](https://github.com/pdcook/TeleportPatch) | GPL-3.0 |
| **TemporaryStatsPatch** 0.0.2<br>Fixes vanilla handling of temporary stat effects. | Pykess (GitHub: pdcook), Root (GitHub: Tess-y) | [Thunderstore](https://thunderstore.io/c/rounds/p/Pykess/TemporaryStatsPatch/) · [Source](https://github.com/Rounds-Modding/TemporaryStatsPatch) | none found |
| **Grow Patch** 0.0.0<br>Fixes Grow's power depending on framerate. | RoundsModding (Thunderstore team), Pykess (GitHub: pdcook), willuwontu | [Thunderstore](https://thunderstore.io/c/rounds/p/RoundsModding/Grow_Patch/) · [Source](https://github.com/Rounds-Modding/GrowPatch) | none found |
| **Performance Improvements** 0.2.0<br>Options to improve performance and stability. | RoundsModding (Thunderstore team), Pykess (GitHub: pdcook), willuwontu, willis81808 | [Thunderstore](https://thunderstore.io/c/rounds/p/RoundsModding/Performance_Improvements/) · [Source](https://github.com/Rounds-Modding/PerformanceImprovements) | GPL-3.0 |
| **GravityPatch** 0.0.0<br>Stops gravity modifications persisting between rounds. | Root (GitHub: Tess-y) (unverified mapping) | [Thunderstore](https://thunderstore.io/c/rounds/p/Root/GravityPatch/) | none found |
| **RegenerationPatch** 0.0.0<br>Stops the regeneration stat persisting between rounds. | Root (GitHub: Tess-y) (unverified mapping) | [Thunderstore](https://thunderstore.io/c/rounds/p/Root/RegenerationPatch/) | none found |
| **ResetAttackCooldownPatch** 1.0.1<br>Fixes attack cooldown persisting between rounds, points and death. | Senyksia | [Thunderstore](https://thunderstore.io/c/rounds/p/Senyksia/ResetAttackCooldownPatch/) · [Source](https://github.com/Senyksia/Rounds-RACP) | none found |
| **ZeroGBulletPatch** 1.1.0<br>Fixes zero-gravity bullet trajectories. | TeamDK (GitHub: dpklinge) | [Thunderstore](https://thunderstore.io/c/rounds/p/TeamDK/ZeroGBulletPatch/) · [Source](https://github.com/dpklinge/ZeroGBulletPatch) | none found |
| **AttackLevelPatch** 0.0.0<br>Fixes attack-level components destroying themselves. | willuwontu | [Thunderstore](https://thunderstore.io/c/rounds/p/willuwontu/AttackLevelPatch/) · [Source](https://github.com/willuwontu/AttackLevelPatch) | none found |
| **BlockForcePatch** 1.0.1<br>Fixes some block stats not working. | willuwontu | [Thunderstore](https://thunderstore.io/c/rounds/p/willuwontu/BlockForcePatch/) · [Source](https://github.com/willuwontu/BlockForcePatch) | none found |
| **RespawnPatch** 1.0.6<br>Fixes players getting fewer respawns than they should. | willuwontu | [Thunderstore](https://thunderstore.io/c/rounds/p/willuwontu/RespawnPatch/) · [Source](https://github.com/willuwontu/RespawnPatch) | none found |
| **StopShootingYoureDead** 0.0.0<br>Stops bursts and blocks persisting between rounds. | willuwontu | [Thunderstore](https://thunderstore.io/c/rounds/p/willuwontu/StopShootingYoureDead/) | GPL-3.0 |

## Tooling

| Mod | Authors | Links | License |
|---|---|---|---|
| **BepInEx** 5.4.23.5<br>Unity plugin/mod loader; downloaded from the upstream GitHub release. | BepInEx team (ghorsington, bbepis, ManlyMarco, js6pak, and contributors) | [Source](https://github.com/BepInEx/BepInEx) | LGPL-2.1 |
| **BepInEx native Apple Silicon fix** (v5-lts f4c1b11)<br>Lets Harmony patch arm64 code on macOS, so ROUNDS runs without Rosetta. Merged upstream, not yet released; the installer patches BepInEx to that build. | cdobbyn | [PR #1402](https://github.com/BepInEx/BepInEx/pull/1402) | LGPL-2.1 |
| **UnityDoorstop** 4.6.0<br>Loads BepInEx into the game; 4.6.0 adds `doorstop_jit_memcpy` for Apple Silicon. | NeighTools (ghorsington and contributors); jit_memcpy by cdobbyn | [Source](https://github.com/NeighTools/UnityDoorstop), [PR #114](https://github.com/NeighTools/UnityDoorstop/pull/114) | LGPL-2.1 |
| **BepInEx.Debug ScriptEngine** r11.1<br>Hot-reloads plugins from BepInEx/scripts; this repo's Hot Reload plugin is modelled on it. Not shipped. | BepInEx team (ManlyMarco, ghorsington, and contributors) | [Source](https://github.com/BepInEx/BepInEx.Debug) | LGPL-3.0 |
| **HarmonyX**<br>Runtime method patching library (fork of Harmony), bundled with BepInEx. | BepInEx team (ghorsington and contributors), Andreas Pardeike (pardeike; original Harmony) | [Source](https://github.com/BepInEx/HarmonyX) | MIT |
| **MonoMod**<br>Runtime detours and HookGen; bundled with BepInEx. | 0x0ade, nike4613, and contributors | [Source](https://github.com/MonoMod/MonoMod) | MIT |
| **bsdiff / bspatch**<br>Binary patch format of `patches/`. macOS's built-in `bspatch` applies them on a Mac; `tools/bspatch-cs` is our own C# reader of the format for Windows. | Colin Percival | [Site](https://www.daemonology.net/bsdiff/) | BSD-2-Clause |
| **Mono.Cecil**<br>Reads and writes .NET assemblies; used by our patch tools. | Jb Evain (jbevain), and contributors | [Source](https://github.com/jbevain/cecil) | MIT |
| **OdinSerializer (Sirenix.Serialization / Utilities / Config stand-ins)** commit ba19025<br>Open-source serializer we build into the three Sirenix.* DLLs that MapsExtended loads. Shipped by us. | Sirenix IVS (TeamSirenix) | [Source](https://github.com/TeamSirenix/odin-serializer) | Apache-2.0 |
| **ILSpy**<br>.NET decompiler; used for analysis only, not shipped. | ICSharpCode team (siegfriedpammer, dgrunwald, christophwille, and contributors) | [Source](https://github.com/icsharpcode/ILSpy) | MIT |
| **Octokit.net (Octokit.dll)**<br>GitHub API client; ships inside the UnboundLib release (used for update checks). | GitHub / octokit contributors | [Source](https://github.com/octokit/octokit.net) | MIT |
| **MMHOOK_Assembly-CSharp.dll**<br>Generated hook stubs for the game's code; ships inside the UnboundLib release. | Generated by MonoMod HookGen from ROUNDS' Assembly-CSharp.dll (Landfall Games) | n/a | none found |

### Notes on specific entries

- **UnboundLib (Bknibb fork)**: GitHub release v4.2.5 (also ships Octokit.dll and MMHOOK_Assembly-CSharp.dll). Fork of https://github.com/Rounds-Modding/UnboundLib (no license file either); original Thunderstore package: https://thunderstore.io/c/rounds/p/willis81808/UnboundLib/. An unlisted Bknibb-UnboundLib_Bknibb 4.0.6 upload exists on Thunderstore but is not in the ROUNDS community listing (unverified why). Author list = the in-mod UNBOUND credits plus GitHub contributors.
- **RoundsWithFriends (Bknibb fork)**: GitHub release v3.0.10. Fork of https://github.com/olavim/RoundsWithFriends (GPL-3.0); original Thunderstore package: https://thunderstore.io/c/rounds/p/olavim/RoundsWithFriends/.
- **MapsExtended**: LICENSE file = MIT (project, (c) 2021 Olavi Mustanoja) plus BSD-3-Clause for bundled NetTopologySuite; GitHub reports NOASSERTION because of the combined file.
- **Cosmic Rounds (CR)**: Linked repo holds source for v1.7.7 only; source for 2.7.0 not found (unverified). Thunderstore manifest has no website_url.
- **Will's Wacky Map Objects**: Marked deprecated on Thunderstore.
- **CardBarPatch**: Source is the CardBarPatch folder of the BossSlothsMods repo (manifest still links the old tddebart URL, which redirects).
- **AllThePatch**: Metadata-only package (manifest, README, icon); contains no code.
- **GravityPatch**: No website_url, no README text, no public repo found. Root = Tess-y inferred from Root's other packages.
- **RegenerationPatch**: No website_url, no README text, no public repo found. Root = Tess-y inferred from Root's other packages.
- **StopShootingYoureDead**: License from the GPL-3.0 LICENSE file inside the Thunderstore package. The manifest's website_url (github.com/willuwontu/EvenSpreadPatch) is a different, unlicensed mod; the DLL was built from a 'BurstPatch' repo that is not public (unverified).
- **BepInEx**: ROUNDS Thunderstore pack (older 5.4.1901): https://thunderstore.io/c/rounds/p/BepInEx/BepInExPack_ROUNDS/
- **HarmonyX**: Upstream Harmony: https://github.com/pardeike/Harmony (MIT).
- **OdinSerializer (Sirenix.Serialization / Utilities / Config stand-ins)**: Modified per Apache-2.0 s.4(b): namespaces OdinSerializer -> Sirenix.Serialization and OdinSerializer.Utilities -> Sirenix.Utilities, split into three assemblies (v2.1.6.0). Not the proprietary Odin Inspector product.
- **Octokit.net (Octokit.dll)**: Downloaded with UnboundLib, not re-uploaded by us.
- **MMHOOK_Assembly-CSharp.dll**: Derived from proprietary game code; downloaded with UnboundLib, never re-uploaded by us.

## License notes

These components have no license we could find. With no license, all rights stay with their authors, so the patcher only ever downloads them from the original source and patches them on your machine. We never re-upload them.

- UnboundLib (Bknibb fork) (Bknibb, willis81808 / Willis) - https://github.com/Bknibb/UnboundLib
- Cosmic Rounds (CR) (XAngelMoonX) - https://github.com/XAngelMoonX/CosmicRounds
- SetRounds (Ascyst, Pykess) - https://github.com/Ascyst/SetRounds
- Will's Wacky Map Objects (willuwontu) - https://github.com/willuwontu/WillsWackyMapObjects
- AllThePatch (RoundsModding) - https://thunderstore.io/c/rounds/p/RoundsModding/AllThePatch/
- CardChoiceSpawnUniqueCardPatch (Pykess, willuwontu) - https://github.com/Rounds-Modding/CardChoiceSpawnUniqueCardPatch
- DemonicPactPatch (Pykess) - https://github.com/Rounds-Modding/DemonicPactPatch
- GunUnblockablePatch (Pykess) - https://github.com/Rounds-Modding/GunUnblockablePatch
- LegRaycastersPatch (Pykess) - https://github.com/Rounds-Modding/LegRaycastersPatch
- PlayerJumpPatch (Pykess) - https://github.com/Rounds-Modding/PlayerJumpPatch
- TemporaryStatsPatch (Pykess, Root) - https://github.com/Rounds-Modding/TemporaryStatsPatch
- Grow Patch (RoundsModding, Pykess) - https://github.com/Rounds-Modding/GrowPatch
- GravityPatch (Root) - https://thunderstore.io/c/rounds/p/Root/GravityPatch/
- RegenerationPatch (Root) - https://thunderstore.io/c/rounds/p/Root/RegenerationPatch/
- ResetAttackCooldownPatch (Senyksia) - https://github.com/Senyksia/Rounds-RACP
- ZeroGBulletPatch (TeamDK) - https://github.com/dpklinge/ZeroGBulletPatch
- AttackLevelPatch (willuwontu) - https://github.com/willuwontu/AttackLevelPatch
- BlockForcePatch (willuwontu) - https://github.com/willuwontu/BlockForcePatch
- RespawnPatch (willuwontu) - https://github.com/willuwontu/RespawnPatch

Other points:

- **GPL-3.0** (RoundsWithFriends, ModdingUtils, TeleportPatch, Performance Improvements, CardBarPatch, StopShootingYoureDead): we still download these from upstream rather than re-hosting them.
- **MIT / Unlicense** (MapsExtended, ModsPlus, RarityLib, CardThemeLib, Classes Manager Reborn): also downloaded from upstream. MapsExtended's LICENSE also covers its bundled NetTopologySuite (BSD-3-Clause).
- **OdinSerializer** (Apache-2.0, (c) Sirenix IVS): the only third-party code we ship. Built from commit ba19025 with renamed namespaces; the full license and change notice ship next to the DLLs. It is not the proprietary Odin Inspector.
- **MMHOOK_Assembly-CSharp.dll** is generated from the game's own code and comes inside the UnboundLib release. We never redistribute it.
- **ROUNDS** itself is (c) Landfall Games. This project isn't affiliated with or endorsed by Landfall or any mod author.
