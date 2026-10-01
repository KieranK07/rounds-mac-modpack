# Third-party notices

**No third-party mod or game files are stored in this repository.** The installer downloads every mod from its
original source (Thunderstore or the author's own GitHub release), verifies its SHA-256, and only then applies the
patches in `patches/` on your machine. If you're an author and want anything here changed or removed, open an issue.

| Component | Where it comes from | License | Notes |
|---|---|---|---|
| Odin Serializer stand-in (`bundled/odin/Sirenix.*.dll`, `tools/odin-stand-in/`) | Built from [TeamSirenix/odin-serializer](https://github.com/TeamSirenix/odin-serializer) @ ba19025 | Apache-2.0 | **Modified**: namespaces renamed to `Sirenix.Serialization` / `Sirenix.Utilities`, split into the three assemblies MapsExtended expects (v2.1.6.0). License: `tools/odin-stand-in/LICENSE`, `bundled/odin/Sirenix-OdinSerializer-LICENSE.txt`. Copyright Sirenix IVS. |
| ModdingUtils changes (`tools/moddingutils/moddingutils-0.4.8-compat.patch`, `patches/Pykess-ModdingUtils-*`) | [pdcook/ModdingUtils](https://github.com/pdcook/ModdingUtils) | GPL-3.0 | Source of every change is published in the `.patch` file, under GPL-3.0, as the license requires. |
| RoundsWithFriends 3.0.10 | [Bknibb/RoundsWithFriends](https://github.com/Bknibb/RoundsWithFriends) (fork of olavim's) | GPL-3.0 | Downloaded unmodified. |
| UnboundLib 4.2.5 (+ MMHOOK, Octokit) | [Bknibb/UnboundLib](https://github.com/Bknibb/UnboundLib) release | see CREDITS.md | Downloaded from the author's release; patched locally (one call to a Windows-only API removed). |
| BepInEx 5.4.23.5 | [BepInEx/BepInEx](https://github.com/BepInEx/BepInEx) release | LGPL-2.1 | Downloaded unmodified; `run_bepinex.sh` gets one setting changed (prefer x86_64 for Rosetta). |
| ScriptEngine r11.1 | [BepInEx/BepInEx.Debug](https://github.com/BepInEx/BepInEx.Debug) release | see CREDITS.md | Downloaded unmodified. |
| All other mods | Thunderstore (pinned versions in `manifest/sources.tsv`) | see CREDITS.md | Downloaded unmodified or patched locally. |

ROUNDS is © Landfall Games. This project is not affiliated with or endorsed by Landfall Games or any mod author.
