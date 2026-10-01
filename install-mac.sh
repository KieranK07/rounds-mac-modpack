#!/bin/bash
# ROUNDS modded on macOS — one-step installer / patcher.
#
#   curl -fsSL https://raw.githubusercontent.com/KieranK07/rounds-mac-modpack/main/install-mac.sh | bash
#
# What it does (nothing here is uploaded anywhere):
#   1. checks Steam, installs ROUNDS through Steam if it isn't installed yet
#   2. installs BepInEx 5 (official release). On Apple Silicon it's patched to run natively (arm64); Intel Macs
#      and --rosetta use the unpatched release, under Rosetta 2 on Apple Silicon
#   3. downloads every mod from its original source (Thunderstore / the author's GitHub) and checks its SHA-256
#   4. applies this repo's binary patches (fixes for the 2025 ROUNDS update + macOS) and checks the result
#   5. adds the Mac Compat Fixes and Hot Reload plugins, configs, and the Steam launch option, then starts the game
#
# Safe to run again: it rebuilds the setup in a temporary folder and compares it with the game file by file. Missing
# files are added, files it installed earlier are updated, and a file you changed is left alone and listed.
#
# Options: --no-steam-config   don't touch Steam's launch options (you paste it yourself)
#          --no-launch         don't start the game at the end
#          --game-dir <path>   ROUNDS folder, if Steam keeps it somewhere unusual
#          --rosetta           run the game under Rosetta instead of natively (the setup before v1.1.0)
#          --repair            also replace files you changed (your copies are moved to a backup folder)
set -euo pipefail

REPO="KieranK07/rounds-mac-modpack"
REF="${ROUNDS_MODPACK_REF:-v1.2.1}"
APPID=1557740
BEPINEX_URL="https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_macos_universal_5.4.23.5.zip"
BEPINEX_SHA="01c2ae782eb016dfd6c345a18dbd2dcafffb3d9d318449d6486689f426b4a323"
# Native arm64 needs doorstop_jit_memcpy, new in UnityDoorstop 4.6.0 (only a CI build so far). If this file has
# changed upstream, the installer falls back to Rosetta.
DOORSTOP_URL="https://github.com/NeighTools/UnityDoorstop/releases/download/ci/doorstop_macos_release_4.6.0.zip"
DOORSTOP_SHA="22790b63ef25a3737eb4a80dfe49ea80cfce3bb2f48689ef19dc11bcdd390c6f"

STEAM_CONFIG=1; LAUNCH=1; GAME_DIR=""; ROSETTA=0; REPAIR=0
while [ $# -gt 0 ]; do
  case "$1" in
    --no-steam-config) STEAM_CONFIG=0;;
    --no-launch) LAUNCH=0;;
    --game-dir) GAME_DIR="$2"; shift;;
    --rosetta) ROSETTA=1;;
    --repair) REPAIR=1;;
    *) echo "unknown option: $1"; exit 1;;
  esac; shift
done

say()  { printf '\n\033[1m==> %s\033[0m\n' "$*"; }
note() { printf '    %s\n' "$*"; }
die()  { printf '\n\033[31mERROR: %s\033[0m\n' "$*" >&2; exit 1; }
sha()  { shasum -a 256 < "$1" | cut -d' ' -f1; }
fetch() { # url dest [sha]
  curl -fL --retry 3 --retry-delay 2 -sS -A "Mozilla/5.0 rounds-mac-modpack" -o "$2" "$1" || die "download failed: $1"
  if [ -n "${3:-}" ] && [ "$(sha "$2")" != "$3" ]; then die "checksum mismatch for $1 (file changed upstream?)"; fi
}

[ "$(uname -s)" = Darwin ] || die "this installer is for macOS (Windows support is planned, see README)"
STEAM_ROOT="$HOME/Library/Application Support/Steam"
WORK="$(mktemp -d -t rounds-modpack)"; trap 'rm -rf "$WORK"' EXIT

# ---------------------------------------------------------------- payload (patches, plugin, configs)
say "Getting the modpack files ($REF)"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")" 2>/dev/null && pwd || true)"
if [ -n "$HERE" ] && [ -f "$HERE/manifest/sources.tsv" ] && [ -z "${ROUNDS_MODPACK_REF:-}" ]; then
  PAYLOAD="$HERE"; note "using local copy: $PAYLOAD"
else
  fetch "https://codeload.github.com/$REPO/tar.gz/refs/tags/$REF" "$WORK/payload.tgz"
  mkdir -p "$WORK/payload" && tar -xzf "$WORK/payload.tgz" -C "$WORK/payload" --strip-components 1
  PAYLOAD="$WORK/payload"
fi

# ---------------------------------------------------------------- native arm64 or Rosetta
APPLE_SILICON=0
case "$(sysctl -n machdep.cpu.brand_string 2>/dev/null)" in Apple*) APPLE_SILICON=1;; esac
NATIVE=0; [ "$APPLE_SILICON" = 1 ] && [ "$ROSETTA" = 0 ] && NATIVE=1
ensure_rosetta() {
  if arch -x86_64 /usr/bin/true 2>/dev/null; then note "Rosetta is installed"; return; fi
  note "installing Rosetta (macOS may ask for your password)"
  /usr/sbin/softwareupdate --install-rosetta --agree-to-license || sudo /usr/sbin/softwareupdate --install-rosetta --agree-to-license \
    || die "couldn't install Rosetta; run: softwareupdate --install-rosetta"
}

# ---------------------------------------------------------------- Steam + ROUNDS
say "Finding Steam and ROUNDS"
[ -d /Applications/Steam.app ] || [ -d "$HOME/Applications/Steam.app" ] || {
  open "https://store.steampowered.com/about/"; die "Steam isn't installed. Install it, sign in, then run this again."; }

find_game() {
  [ -n "$GAME_DIR" ] && { echo "$GAME_DIR"; return; }
  local lib
  { echo "$STEAM_ROOT"; grep '"path"' "$STEAM_ROOT/steamapps/libraryfolders.vdf" 2>/dev/null | sed -E 's/.*"path"[[:space:]]*"(.*)"/\1/'; } |
  while read -r lib; do
    [ -d "$lib/steamapps/common/ROUNDS/ROUNDS.app" ] && grep -q '"StateFlags"[[:space:]]*"4"' "$lib/steamapps/appmanifest_$APPID.acf" 2>/dev/null \
      && { echo "$lib/steamapps/common/ROUNDS"; return; }
  done
}
G="$(find_game || true)"
if [ -z "$G" ]; then
  note "ROUNDS isn't installed yet - opening Steam's install dialog (you need to own ROUNDS)."
  pgrep -x steam_osx >/dev/null || { open -a Steam; sleep 10; }
  open "steam://install/$APPID"
  note "Click Install in Steam. Waiting for the download to finish..."
  for _ in $(seq 1 360); do sleep 5; G="$(find_game || true)"; [ -n "$G" ] && break; printf '.'; done; echo
  [ -n "$G" ] || die "ROUNDS didn't finish installing within 30 minutes. Run this again when it's done."
fi
note "ROUNDS: $G"
if pgrep -x ROUNDS >/dev/null; then die "ROUNDS is running - quit it and run this again."; fi

# ---------------------------------------------------------------- BepInEx
# Everything is put together in $ST first (laid out like the game folder) and copied over at the end, so running
# this again only checks the install.
ST="$WORK/stage"; mkdir -p "$ST"
say "Preparing BepInEx 5.4.23.5"
fetch "$BEPINEX_URL" "$WORK/bepinex.zip" "$BEPINEX_SHA"
ditto -x -k "$WORK/bepinex.zip" "$ST"
if [ "$NATIVE" = 1 ]; then
  # BepInEx 5.4.23.5 can't write Harmony patches into arm64 code. The fix (BepInEx PR #1402, by cdobbyn) is merged
  # but not released yet: patch the two DLLs it touches to that build (tools/bepinex-arm64) and use Doorstop 4.6.0.
  note "setting up native Apple Silicon (arm64)"
  if curl -fL --retry 3 -sS -A "Mozilla/5.0 rounds-mac-modpack" -o "$WORK/doorstop.zip" "$DOORSTOP_URL" \
      && [ "$(sha "$WORK/doorstop.zip")" = "$DOORSTOP_SHA" ]; then
    ditto -x -k "$WORK/doorstop.zip" "$WORK/doorstop"
    cp "$WORK/doorstop/universal/libdoorstop.dylib" "$WORK/doorstop/universal/.doorstop_version" "$ST/"
    while IFS=$'\t' read -r rel before after patch; do
      [ -z "$rel" ] && continue
      f="$ST/BepInEx/core/$rel"
      [ "$(sha "$f")" = "$before" ] || die "unexpected original for BepInEx/core/$rel"
      /usr/bin/bspatch "$f" "$f.patched" "$PAYLOAD/patches/$patch"
      [ "$(sha "$f.patched")" = "$after" ] || die "patch result mismatch for BepInEx/core/$rel"
      mv "$f.patched" "$f"; note "BepInEx/core/$rel"
    done < "$PAYLOAD/manifest/patches-native.tsv"
  else
    note "Doorstop 4.6.0 isn't available as expected (changed upstream?), so using Rosetta instead"
    NATIVE=0
  fi
fi
if [ "$APPLE_SILICON" = 1 ] && [ "$NATIVE" = 0 ]; then say "Checking Rosetta 2"; ensure_rosetta; fi
PREF="arm64,x86_64"; [ "$NATIVE" = 0 ] && PREF="x86_64,arm64"
# Which slice of the universal game binary to run. ROUNDS_ARCH in the Steam launch options overrides it.
awk -v pref="$PREF" '
  /^executable_name=""$/ { print "executable_name=\"ROUNDS.app\""; print "";
    print "# MACOS: architectures to run the game as, most preferred first.";
    print "# \"arm64,x86_64\" = native Apple Silicon, \"x86_64,arm64\" = Rosetta.";
    print "# Override for one launch with ROUNDS_ARCH, e.g. ROUNDS_ARCH=x86_64,arm64 in Steam launch options.";
    print "archpreference=\"${ROUNDS_ARCH:-" pref "}\""; next }
  { sub(/export ARCHPREFERENCE="arm64,x86_64"/, "export ARCHPREFERENCE=\"${archpreference}\""); print }
' "$ST/run_bepinex.sh" > "$WORK/run_bepinex.sh"
grep -q "^archpreference=\"\${ROUNDS_ARCH:-$PREF}\"" "$WORK/run_bepinex.sh" && grep -q 'export ARCHPREFERENCE="${archpreference}"' "$WORK/run_bepinex.sh" \
  || die "couldn't configure run_bepinex.sh"
cp "$WORK/run_bepinex.sh" "$ST/run_bepinex.sh"
chmod +x "$ST/run_bepinex.sh"
mkdir -p "$ST/BepInEx/plugins" "$ST/BepInEx/scripts" "$ST/BepInEx/config"
cp "$PAYLOAD"/config/*.cfg "$ST/BepInEx/config/"

# Hot Reload (this repo, MIT): loads BepInEx/scripts and swaps mods in and out while the game runs
mkdir -p "$ST/BepInEx/plugins/HotReload"
cp "$PAYLOAD"/bundled/HotReload.dll "$PAYLOAD"/bundled/HotReload.pdb "$ST/BepInEx/plugins/HotReload/"

# ---------------------------------------------------------------- mods from their original sources
say "Downloading mods from their authors (Thunderstore / GitHub)"
P="$ST/BepInEx/plugins"
while IFS=$'\t' read -r id kind url want; do
  [ -z "$id" ] && continue
  case "$kind" in
    thunderstore)
      fetch "$url" "$WORK/m.zip" "$want"
      mkdir -p "$P/$id" && unzip -q -o "$WORK/m.zip" -d "$P/$id" 2>/dev/null || [ $? -eq 1 ]   # 1 = warnings (Windows paths)
      note "$id";;
    github)
      mkdir -p "$P/$(dirname "$id")"; fetch "$url" "$P/$id" "$want"; note "$id";;
  esac
done < "$PAYLOAD/manifest/sources.tsv"

# ---------------------------------------------------------------- our patches
say "Applying fixes (binary patches, each checked before and after)"
while IFS=$'\t' read -r rel before after patch; do
  [ -z "$rel" ] && continue
  f="$P/$rel"
  [ "$(sha "$f")" = "$before" ] || die "unexpected original for $rel"
  /usr/bin/bspatch "$f" "$f.patched" "$PAYLOAD/patches/$patch"
  [ "$(sha "$f.patched")" = "$after" ] || die "patch result mismatch for $rel"
  mv "$f.patched" "$f"; note "$rel"
done < "$PAYLOAD/manifest/patches.tsv"

# Odin Serializer stand-ins for MapsExtended (built from TeamSirenix/odin-serializer, Apache-2.0)
cp "$PAYLOAD"/bundled/odin/* "$P/olavim-MapsExtended-1.4.2/"
# Mac Compat Fixes (this repo, MIT) - loaded by Hot Reload, so it can be swapped while the game runs
cp "$PAYLOAD"/bundled/MacCompatFixes.dll "$PAYLOAD"/bundled/MacCompatFixes.pdb "$ST/BepInEx/scripts/"

# ---------------------------------------------------------------- copy into the game, or check what's there
say "Installing into the game folder"
MARK="BepInEx/rounds-mac-modpack.sha256"   # every file this installer put in the game, as `shasum -a 256` lines
BACKUP="$G/BepInEx.backup-$(date +%Y%m%d-%H%M%S)"
backup() { mkdir -p "$BACKUP/$(dirname "$1")" && mv "$G/$1" "$BACKUP/$1"; }
FRESH=0
if [ ! -f "$G/$MARK" ]; then
  # First install (or one from before v1.2.1): other mods could clash with these, so start from empty plugin
  # folders (moved to a backup, not deleted). Settings in BepInEx/config stay.
  FRESH=1
  for d in plugins scripts patchers; do
    if [ -d "$G/BepInEx/$d" ]; then mkdir -p "$BACKUP"; mv "$G/BepInEx/$d" "$BACKUP/"; fi
  done
  if [ -d "$BACKUP" ]; then note "mods that were already there were moved to $(basename "$BACKUP")"; fi
fi
(cd "$ST" && find . -type f ! -name .DS_Store | sed 's#^\./##' | LC_ALL=C sort) > "$WORK/files"
(cd "$ST" && tr '\n' '\0' < "$WORK/files" | xargs -0 shasum -a 256) > "$WORK/new.sha256"
: > "$WORK/old.sha256"; if [ -f "$G/$MARK" ]; then cp "$G/$MARK" "$WORK/old.sha256"; fi
installed() { awk -v p="$1" 'substr($0, 67) == p { print substr($0, 1, 64); exit }' "$WORK/old.sha256"; }
ok=0; added=0; updated=0; kept=0; removed=0
while IFS= read -r f; do
  if [ ! -e "$G/$f" ]; then
    mkdir -p "$G/$(dirname "$f")"; cp -p "$ST/$f" "$G/$f"; added=$((added + 1))
  elif cmp -s "$ST/$f" "$G/$f"; then
    ok=$((ok + 1))
  elif [ "$FRESH" = 1 ] || [ "$REPAIR" = 1 ] || [ "$(sha "$G/$f")" = "$(installed "$f")" ]; then
    backup "$f"; cp -p "$ST/$f" "$G/$f"; updated=$((updated + 1))   # ours from before (e.g. an update), or asked to
  else
    kept=$((kept + 1)); note "you changed this, left as is: $f"
  fi
done < "$WORK/files"
# Files an earlier version installed and this one doesn't (ScriptEngine before v1.2.0): out, unless you changed them.
while IFS= read -r line; do
  f="${line:66}"
  [ -n "$f" ] && [ -f "$G/$f" ] || continue
  grep -qxF "$f" "$WORK/files" && continue
  if [ "$(sha "$G/$f")" = "${line:0:64}" ]; then backup "$f"; removed=$((removed + 1)); else note "no longer part of the pack, left as is: $f"; fi
done < "$WORK/old.sha256"
cp "$WORK/new.sha256" "$G/$MARK"
xattr -dr com.apple.quarantine "$G" 2>/dev/null || true
note "$(wc -l < "$WORK/files" | tr -d ' ') files checked: $ok already right, $added added, $updated updated, $removed removed, $kept changed by you"
if [ "$kept" -gt 0 ]; then note "to put back the pack's copies, run this again with --repair (yours go to a backup folder)"; fi
if [ "$FRESH" = 0 ] && [ "$((updated + removed))" -gt 0 ]; then note "replaced files were moved to $(basename "$BACKUP")"; fi

# ---------------------------------------------------------------- Steam launch option
OPT="\"$G/run_bepinex.sh\" %command%"
launch_option_vdf() { # in out: localconfig.vdf with ROUNDS' launch option set
  LO="$OPT" awk -v app="\"$APPID\"" '
    function q(s){ gsub(/\\/,"\\\\",s); gsub(/"/,"\\\"",s); return "\"" s "\"" }
    BEGIN{ val=q(ENVIRON["LO"]); inapps=0; inblk=0; done=0 }
    {
      line=$0
      if (!done && line ~ /^\t\t\t\t"apps"$/) { inapps=1; print; next }
      if (inapps && !inblk && line ~ /^\t\t\t\t\{$/) { print; next }
      if (inapps && !inblk && line == "\t\t\t\t\t" app) { inblk=1; print; next }
      if (inblk && line ~ /^\t\t\t\t\t\t"LaunchOptions"/) { print "\t\t\t\t\t\t\"LaunchOptions\"\t\t" val; done=1; next }
      if (inblk && line ~ /^\t\t\t\t\t\}$/) { if (!done) print "\t\t\t\t\t\t\"LaunchOptions\"\t\t" val; done=1; inblk=0; inapps=0; print; next }
      if (inapps && !inblk && line ~ /^\t\t\t\t\}$/) { if (!done) { print "\t\t\t\t\t" app; print "\t\t\t\t\t{"; print "\t\t\t\t\t\t\"LaunchOptions\"\t\t" val; print "\t\t\t\t\t}"; done=1 } inapps=0; print; next }
      print
    }' "$1" > "$2"
}
VDFS=(); STALE=0
for f in "$STEAM_ROOT"/userdata/*/config/localconfig.vdf; do
  [ -f "$f" ] || continue
  VDFS+=("$f"); launch_option_vdf "$f" "$WORK/vdf"; awk '{ print }' "$f" > "$WORK/vdf.now"
  cmp -s "$WORK/vdf.now" "$WORK/vdf" || STALE=$((STALE + 1))
done
if [ "$STEAM_CONFIG" = 1 ] && [ "${#VDFS[@]}" -gt 0 ] && [ "$STALE" = 0 ]; then
  say "Steam launch option"; note "already set"
elif [ "$STEAM_CONFIG" = 1 ] && [ "${#VDFS[@]}" -gt 0 ]; then
  say "Setting the ROUNDS launch option in Steam (Steam will restart)"
  if pgrep -x steam_osx >/dev/null; then
    osascript -e 'quit app "Steam"' >/dev/null 2>&1 || true
    for _ in $(seq 1 60); do pgrep -x steam_osx >/dev/null || break; sleep 1; done
    if pgrep -x steam_osx >/dev/null; then   # Steam sometimes ignores the quit request
      note "Steam didn't quit; closing it"; pkill -x steam_osx || true
      for _ in $(seq 1 20); do pgrep -x steam_osx >/dev/null || break; sleep 1; done
    fi
    pgrep -x steam_osx >/dev/null && die "Steam didn't quit. Close Steam and run this again (or use --no-steam-config)."
  fi
  for f in "${VDFS[@]}"; do cp "$f" "$f.bak-rounds-modpack"; launch_option_vdf "$f.bak-rounds-modpack" "$f"; done
  note "updated ${#VDFS[@]} Steam account(s)"
  open -a Steam
else
  printf '%s' "$OPT" | pbcopy
  note "Paste this into Steam > ROUNDS > Properties > Launch Options (copied to clipboard):"
  note "$OPT"
fi

say "Done! ROUNDS is modded."
if [ "$NATIVE" = 1 ]; then note "Runs natively on Apple Silicon. To use Rosetta instead, run this again with --rosetta."
elif [ "$APPLE_SILICON" = 1 ]; then note "Runs under Rosetta."; fi
note "Mods: 31 from Thunderstore/GitHub + Mac Compat Fixes. Credits: in-game CREDITS > KIERAN'S UNBOUND, and the README."
note "Hot Reload: mods in BepInEx/scripts swap in and out while the game runs (F6 reloads them all)."
note "Uninstall: Steam > ROUNDS > Properties > clear Launch Options (or delete the BepInEx folder)."
if [ "$LAUNCH" = 1 ]; then
  sleep 8; open "steam://rungameid/$APPID"
fi
