# ROUNDS modded on Windows - one-step installer / patcher. In PowerShell:
#
#   irm https://raw.githubusercontent.com/KieranK07/rounds-mac-modpack/main/install-windows.ps1 | iex
#
# The same pack as install-mac.sh, file for file, so Mac and Windows players can play together. It:
#   1. finds ROUNDS through Steam (installs it if needed) and checks it's the current build, not a beta
#   2. installs BepInEx 5.4.23.5 (official Windows x64 release)
#   3. downloads every mod from its original source (Thunderstore / the author's GitHub) and checks its SHA-256
#   4. applies this repo's binary patches (fixes for the 2025 ROUNDS update) and checks the result
#   5. adds the Mac Compat Fixes and Hot Reload plugins and the configs, then starts the game
#
# Safe to run again: it rebuilds the setup in a temporary folder and compares it with the game file by file. Missing
# files are added, files it installed earlier are updated, and a file you changed is left alone and listed.
#
# Options (to pass them:  & ([scriptblock]::Create((irm <url>))) -Repair ):
#   -Repair          also replace files you changed (your copies are moved to a backup folder)
#   -NoLaunch        don't start the game at the end
#   -GameDir <path>  ROUNDS folder, if Steam keeps it somewhere unusual
param([switch]$Repair, [switch]$NoLaunch, [string]$GameDir = "")

function Install-RoundsModpack {
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'
    $Repo = "KieranK07/rounds-mac-modpack"
    $Ref = if ($env:ROUNDS_MODPACK_REF) { $env:ROUNDS_MODPACK_REF } else { "v1.3.0" }
    $AppId = 1557740
    $BepInExUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip"
    $BepInExSha = "82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4"

    function Say($m) { Write-Host ""; Write-Host "==> $m" -ForegroundColor White }
    function Note($m) { Write-Host "    $m" }
    function Fail($m) { throw $m }
    function Sha($p) { (Get-FileHash -Algorithm SHA256 -LiteralPath $p).Hash.ToLowerInvariant() }
    function Join([string[]]$parts) { $p = $parts[0]; for ($i = 1; $i -lt $parts.Length; $i++) { $p = [IO.Path]::Combine($p, $parts[$i]) }; $p }
    function Native($rel) { $rel -replace '/', [IO.Path]::DirectorySeparatorChar }
    function MkParent($file) { [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($file)) }

    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    function Fetch($url, $dest, $want) {
        $wc = New-Object Net.WebClient
        $wc.Headers['User-Agent'] = 'Mozilla/5.0 rounds-mac-modpack'
        for ($i = 1; ; $i++) {
            try { $wc.DownloadFile($url, $dest); break }
            catch { if ($i -ge 3) { Fail "download failed: $url ($($_.Exception.Message))" }; Start-Sleep -Seconds 2 }
        }
        if ($want -and (Sha $dest) -ne $want) { Fail "checksum mismatch for $url (file changed upstream?)" }
    }
    # Thunderstore zips mix Windows and Unix separators; extract both the same way (and like install-mac.sh does).
    function Unzip($zip, $dir) {
        $z = [IO.Compression.ZipFile]::OpenRead($zip)
        try {
            foreach ($e in $z.Entries) {
                $name = $e.FullName -replace '\\', '/'
                if ($name -eq '' -or $name.EndsWith('/')) { continue }
                if ($name -match '(^|/)\.\.(/|$)') { Fail "unsafe path in $zip" }
                $dest = Join @($dir, (Native $name))
                MkParent $dest
                [IO.Compression.ZipFileExtensions]::ExtractToFile($e, $dest, $true)
            }
        } finally { $z.Dispose() }
    }

    $Work = Join @([IO.Path]::GetTempPath(), ("rounds-modpack-" + [Guid]::NewGuid().ToString('N').Substring(0, 8)))
    [void][IO.Directory]::CreateDirectory($Work)
    try {
        # ---------------------------------------------------------------- payload (patches, plugins, configs)
        Say "Getting the modpack files ($Ref)"
        if ($PSScriptRoot -and (Test-Path -LiteralPath (Join @($PSScriptRoot, 'manifest', 'sources.tsv'))) -and -not $env:ROUNDS_MODPACK_REF) {
            $Payload = $PSScriptRoot; Note "using local copy: $Payload"
        } else {
            Fetch "https://codeload.github.com/$Repo/zip/refs/tags/$Ref" (Join @($Work, 'payload.zip')) $null
            Unzip (Join @($Work, 'payload.zip')) (Join @($Work, 'payload'))
            $Payload = (Get-ChildItem -LiteralPath (Join @($Work, 'payload')) -Directory | Select-Object -First 1).FullName
        }

        # ---------------------------------------------------------------- Steam + ROUNDS
        Say "Finding Steam and ROUNDS"
        $Steam = $null
        foreach ($k in 'HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam') {
            $v = Get-ItemProperty -Path $k -ErrorAction SilentlyContinue
            if ($v -and $v.SteamPath) { $Steam = $v.SteamPath -replace '/', '\'; break }
            if ($v -and $v.InstallPath) { $Steam = $v.InstallPath; break }
        }
        if (-not $GameDir -and -not $Steam) {
            Start-Process "https://store.steampowered.com/about/"
            Fail "Steam isn't installed. Install it, sign in, then run this again."
        }
        function Find-Game {
            if ($GameDir) { return @{ Dir = $GameDir; Manifest = $null } }
            $libs = @($Steam)
            $vdf = Join @($Steam, 'steamapps', 'libraryfolders.vdf')
            if (Test-Path -LiteralPath $vdf) {
                foreach ($m in [regex]::Matches((Get-Content -LiteralPath $vdf -Raw), '"path"\s+"([^"]+)"')) { $libs += ($m.Groups[1].Value -replace '\\\\', '\') }
            }
            foreach ($lib in $libs) {
                $acf = Join @($lib, 'steamapps', "appmanifest_$AppId.acf")
                $dir = Join @($lib, 'steamapps', 'common', 'ROUNDS')
                if ((Test-Path -LiteralPath (Join @($dir, 'ROUNDS.exe'))) -and (Test-Path -LiteralPath $acf) -and
                    ((Get-Content -LiteralPath $acf -Raw) -match '"StateFlags"\s+"4"')) { return @{ Dir = $dir; Manifest = $acf } }
            }
            return $null
        }
        $found = Find-Game
        if (-not $found) {
            Note "ROUNDS isn't installed yet - opening Steam's install dialog (you need to own ROUNDS)."
            Start-Process "steam://install/$AppId"
            Note "Click Install in Steam. Waiting for the download to finish..."
            for ($i = 0; $i -lt 360 -and -not $found; $i++) { Start-Sleep -Seconds 5; $found = Find-Game; Write-Host -NoNewline '.' }
            Write-Host ""
            if (-not $found) { Fail "ROUNDS didn't finish installing within 30 minutes. Run this again when it's done." }
        }
        $G = $found.Dir
        Note "ROUNDS: $G"
        if (-not $GameDir -and -not (Test-Path -LiteralPath (Join @($G, 'ROUNDS.exe')))) { Fail "no ROUNDS.exe in $G" }
        # These mods are for the current game. The old-rounds-for-mods beta is a different game version: players on it
        # can't join the others, and the patched mods don't fit it.
        if ($found.Manifest -and ((Get-Content -LiteralPath $found.Manifest -Raw) -match '"BetaKey"\s+"([^"]+)"') -and $Matches[1] -ne 'public') {
            Fail ("ROUNDS is on the '" + $Matches[1] + "' beta. In Steam: ROUNDS > Properties > Betas > None, let it update, then run this again.")
        }
        if (Get-Process -Name ROUNDS -ErrorAction SilentlyContinue) { Fail "ROUNDS is running - quit it and run this again." }

        # ---------------------------------------------------------------- build the setup in a staging folder
        # Everything is put together in $St (laid out like the game folder) and copied over at the end, so running
        # this again only checks the install.
        $St = Join @($Work, 'stage')
        Say "Preparing BepInEx 5.4.23.5"
        Fetch $BepInExUrl (Join @($Work, 'bepinex.zip')) $BepInExSha
        Unzip (Join @($Work, 'bepinex.zip')) $St
        $BX = Join @($St, 'BepInEx')
        foreach ($d in 'plugins', 'scripts', 'config') { [void][IO.Directory]::CreateDirectory((Join @($BX, $d))) }
        Get-ChildItem -LiteralPath (Join @($Payload, 'config')) -Filter '*.cfg' | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join @($BX, 'config', $_.Name)) }
        # Hot Reload (this repo, MIT): loads BepInEx/scripts and swaps mods in and out while the game runs
        foreach ($f in 'HotReload.dll', 'HotReload.pdb') {
            $dest = Join @($BX, 'plugins', 'HotReload', $f); MkParent $dest
            Copy-Item -LiteralPath (Join @($Payload, 'bundled', $f)) -Destination $dest
        }

        Say "Downloading mods from their authors (Thunderstore / GitHub)"
        $P = Join @($BX, 'plugins')
        foreach ($line in Get-Content -LiteralPath (Join @($Payload, 'manifest', 'sources.tsv'))) {
            if (-not $line.Trim()) { continue }
            $id, $kind, $url, $want = $line -split "`t"
            if ($kind -eq 'thunderstore') {
                $zip = Join @($Work, 'm.zip'); Fetch $url $zip $want
                Unzip $zip (Join @($P, $id))
            } elseif ($kind -eq 'github') {
                $dest = Join @($P, (Native $id)); MkParent $dest
                Fetch $url $dest $want
            } else { continue }
            Note $id
        }

        Say "Applying fixes (binary patches, each checked before and after)"
        # Windows has no bspatch; this is a small C# one (tools/bspatch-cs).
        if (-not ('RoundsModpack.BsPatch' -as [type])) { Add-Type -LiteralPath (Join @($Payload, 'tools', 'bspatch-cs', 'BsPatch.cs')) }
        foreach ($line in Get-Content -LiteralPath (Join @($Payload, 'manifest', 'patches.tsv'))) {
            if (-not $line.Trim()) { continue }
            $rel, $before, $after, $patch = $line -split "`t"
            $f = Join @($P, (Native $rel))
            if ((Sha $f) -ne $before) { Fail "unexpected original for $rel" }
            [RoundsModpack.BsPatch]::Apply($f, (Join @($Payload, 'patches', $patch)), "$f.patched")
            if ((Sha "$f.patched") -ne $after) { Fail "patch result mismatch for $rel" }
            Move-Item -LiteralPath "$f.patched" -Destination $f -Force
            Note $rel
        }
        # Odin Serializer stand-ins for MapsExtended (built from TeamSirenix/odin-serializer, Apache-2.0)
        Get-ChildItem -LiteralPath (Join @($Payload, 'bundled', 'odin')) -File | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination (Join @($P, 'olavim-MapsExtended-1.4.2', $_.Name)) }
        # Mac Compat Fixes (this repo, MIT): its shader fixes only run on Metal; the rest (card names, menus, Cosmic
        # Rounds errors) applies on Windows too. Loaded by Hot Reload.
        foreach ($f in 'MacCompatFixes.dll', 'MacCompatFixes.pdb') { Copy-Item -LiteralPath (Join @($Payload, 'bundled', $f)) -Destination (Join @($BX, 'scripts', $f)) }

        # ---------------------------------------------------------------- copy into the game, or check what's there
        Say "Installing into the game folder"
        $MarkRel = 'BepInEx/rounds-mac-modpack.sha256'   # every file this installer put in the game, as sha256sum lines
        $Mark = Join @($G, (Native $MarkRel))
        $Backup = Join @($G, ('BepInEx.backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss')))
        function Backup-File($rel) {
            $dst = Join @($Backup, (Native $rel)); MkParent $dst
            Move-Item -LiteralPath (Join @($G, (Native $rel))) -Destination $dst -Force
        }
        $fresh = -not (Test-Path -LiteralPath $Mark)
        if ($fresh) {
            # First install: other mods could clash with these, so start from empty plugin folders (moved to a
            # backup, not deleted). Settings in BepInEx/config stay.
            foreach ($d in 'plugins', 'scripts', 'patchers') {
                $src = Join @($G, 'BepInEx', $d)
                if (Test-Path -LiteralPath $src) { [void][IO.Directory]::CreateDirectory($Backup); Move-Item -LiteralPath $src -Destination (Join @($Backup, $d)) }
            }
            if (Test-Path -LiteralPath $Backup) { Note "mods that were already there were moved to $(Split-Path -Leaf $Backup)" }
        }
        $old = @{}
        if (-not $fresh) {
            foreach ($line in Get-Content -LiteralPath $Mark) { if ($line.Length -gt 66) { $old[$line.Substring(66)] = $line.Substring(0, 64) } }
        }
        $base = (Get-Item -LiteralPath $St).FullName.TrimEnd('\', '/')
        [string[]]$files = Get-ChildItem -LiteralPath $St -Recurse -File -Force | Where-Object { $_.Name -ne '.DS_Store' } |
            ForEach-Object { $_.FullName.Substring($base.Length + 1) -replace '\\', '/' }
        [Array]::Sort($files, [StringComparer]::Ordinal)
        $ok = 0; $added = 0; $updated = 0; $kept = 0; $removed = 0
        $lines = New-Object 'System.Collections.Generic.List[string]'
        $inPack = @{}
        foreach ($rel in $files) {
            $inPack[$rel] = $true
            $src = Join @($St, (Native $rel)); $dst = Join @($G, (Native $rel))
            $want = Sha $src
            $lines.Add("$want  $rel")
            if (-not (Test-Path -LiteralPath $dst)) { MkParent $dst; Copy-Item -LiteralPath $src -Destination $dst; $added++; continue }
            $have = Sha $dst
            if ($have -eq $want) { $ok++ }
            elseif ($fresh -or $Repair -or $old[$rel] -eq $have) { Backup-File $rel; Copy-Item -LiteralPath $src -Destination $dst; $updated++ }
            else { $kept++; Note "you changed this, left as is: $rel" }
        }
        # Files an earlier version installed and this one doesn't: out, unless you changed them.
        foreach ($rel in @($old.Keys)) {
            if ($inPack.ContainsKey($rel)) { continue }
            $dst = Join @($G, (Native $rel))
            if (-not (Test-Path -LiteralPath $dst)) { continue }
            if ((Sha $dst) -eq $old[$rel]) { Backup-File $rel; $removed++ } else { Note "no longer part of the pack, left as is: $rel" }
        }
        [IO.File]::WriteAllText($Mark, (($lines -join "`n") + "`n"), (New-Object Text.UTF8Encoding($false)))
        Note "$($files.Length) files checked: $ok already right, $added added, $updated updated, $removed removed, $kept changed by you"
        if ($kept -gt 0) { Note "to put back the pack's copies, run this again with -Repair (yours go to a backup folder)" }
        if (-not $fresh -and ($updated + $removed) -gt 0) { Note "replaced files were moved to $(Split-Path -Leaf $Backup)" }

        Say "Done! ROUNDS is modded."
        Note "Mods: 31 from Thunderstore/GitHub + Mac Compat Fixes + Hot Reload, the same as the Mac pack."
        Note "Launch ROUNDS from Steam as usual (not through r2modman / Thunderstore Mod Manager)."
        Note "Uninstall: delete winhttp.dll from the game folder (and the BepInEx folder to remove everything)."
        if (-not $NoLaunch) { Start-Process "steam://rungameid/$AppId" }
    } finally {
        Remove-Item -LiteralPath $Work -Recurse -Force -ErrorAction SilentlyContinue
    }
}

try { Install-RoundsModpack }
catch { Write-Host ""; Write-Host ("ERROR: " + $_.Exception.Message) -ForegroundColor Red }
