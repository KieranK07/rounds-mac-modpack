using Mono.Cecil;

// `rounds-port hot`: port a mod and drop it into the game's hot-reload folder, where the Hot Reload plugin
// (src/HotReload) swaps it in while the game runs. With --watch, do it again every time the mod's DLL changes.
static class Hot
{
    public static int Run(Game game, List<string> inputs, bool watch)
    {
        var bepinex = Path.Combine(game.Dir, "BepInEx");
        var plugins = Path.Combine(bepinex, "plugins");
        var hotDir = Path.Combine(bepinex, HotFolder(bepinex));
        Directory.CreateDirectory(hotDir);

        if (!Directory.Exists(plugins) || !Directory.GetFiles(plugins, "HotReload.dll", SearchOption.AllDirectories).Any())
            Out.Warn("the Hot Reload plugin isn't installed in BepInEx/plugins, so nothing will load these (see tools/rounds-port/README.md)");
        if (Directory.Exists(plugins) && Directory.GetFiles(plugins, "ScriptEngine.dll", SearchOption.AllDirectories).Any())
            Out.Warn("ScriptEngine is installed too and loads the same folder; remove BepInEx/plugins/ScriptEngine.dll");

        var files = Program.Expand(inputs).Select(Path.GetFullPath).ToList();
        if (files.Count == 0) { Out.Error("no mod DLLs found"); return 3; }
        int worst = 0;
        foreach (var f in files) worst = Math.Max(worst, Deploy(game, f, hotDir, plugins));
        if (!watch) return worst;

        Out.Line($"\nwatching {files.Count} file{(files.Count == 1 ? "" : "s")}; every rebuild is ported and swapped in. Ctrl+C to stop.");
        var changed = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        var watchers = files.GroupBy(Path.GetDirectoryName).Select(g =>
        {
            var w = new FileSystemWatcher(g.Key!) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size };
            void On(string p) { if (files.Contains(Path.GetFullPath(p), StringComparer.OrdinalIgnoreCase)) lock (changed) changed[Path.GetFullPath(p)] = DateTime.UtcNow; }
            w.Changed += (_, e) => On(e.FullPath); w.Created += (_, e) => On(e.FullPath); w.Renamed += (_, e) => On(e.FullPath);
            w.EnableRaisingEvents = true;
            return w;
        }).ToList();
        while (true)
        {
            Thread.Sleep(250);
            List<string> ready;
            lock (changed)
            {
                ready = changed.Where(kv => DateTime.UtcNow - kv.Value > TimeSpan.FromMilliseconds(600)).Select(kv => kv.Key).ToList();
                foreach (var r in ready) changed.Remove(r);
            }
            foreach (var r in ready)
            {
                if (!Readable(r)) { lock (changed) changed[r] = DateTime.UtcNow; continue; }   // build still writing
                Out.Line($"\n{DateTime.Now:HH:mm:ss} {Path.GetFileName(r)} changed");
                Deploy(game, r, hotDir, plugins);
            }
        }
    }

    // The folder the plugin watches (its config can change it).
    static string HotFolder(string bepinex)
    {
        var cfg = Path.Combine(bepinex, "config", "kieran.rounds.hotreload.cfg");
        if (File.Exists(cfg))
            foreach (var line in File.ReadLines(cfg))
                if (line.Trim().StartsWith("Folder") && line.Contains('=')) return line[(line.IndexOf('=') + 1)..].Trim();
        return "scripts";
    }

    static bool Readable(string path)
    {
        try { using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)) return true; } catch { return false; }
    }

    static int Deploy(Game game, string dll, string hotDir, string plugins)
    {
        try
        {
            var scanner = new Scanner(game);
            var rp = new ReaderParameters { AssemblyResolver = game.Resolver, ReadingMode = ReadingMode.Immediate, InMemory = true };
            var module = ModuleDefinition.ReadModule(dll, rp);
            var before = scanner.Scan(module);
            var fixer = new Fixer(module, game, scanner);
            if (before.Count > 0) fixer.Run();

            var guids = module.Types.SelectMany(t => t.CustomAttributes).Where(a => a.AttributeType.FullName == "BepInEx.BepInPlugin")
                              .Select(a => (string)a.ConstructorArguments[0].Value).ToList();
            TakeOver(plugins, module.Assembly.Name.Name, guids);

            // One write of the finished bytes, so the plugin never sees a half-written DLL.
            var ms = new MemoryStream();
            module.Write(ms);
            var dest = Path.Combine(hotDir, Path.GetFileName(dll));
            File.WriteAllBytes(dest, ms.ToArray());
            var stalePdb = Path.ChangeExtension(dest, ".pdb");
            var srcPdb = Path.ChangeExtension(dll, ".pdb");
            if (!fixer.Changed && File.Exists(srcPdb)) File.Copy(srcPdb, stalePdb, true);   // unchanged: its .pdb still matches
            else if (File.Exists(stalePdb)) File.Delete(stalePdb);

            var left = fixer.Changed ? scanner.Scan(ModuleDefinition.ReadModule(new MemoryStream(ms.ToArray()), rp)) : before;
            Out.Line($"{Path.GetFileName(dll)} -> {dest}" + (fixer.Changed ? $" (ported: {before.Count - left.Count} fixed)" : ""));
            foreach (var i in left.Where(i => i.Fix == Fix.Manual)) Out.Line($"  MANUAL {i.What}: {i.Detail}");
            return left.Count == 0 ? 0 : left.Any(i => i.Fix == Fix.Manual) ? 2 : 1;
        }
        catch (Exception e) { Out.Error($"{Path.GetFileName(dll)}: {e.Message}"); return 3; }
    }

    // A copy in BepInEx/plugins would be loaded at startup too (same plugin twice); park it outside plugins.
    static void TakeOver(string plugins, string assemblyName, List<string> guids)
    {
        if (!Directory.Exists(plugins)) return;
        var parked = Path.Combine(Path.GetDirectoryName(plugins)!, "plugins-parked-by-rounds-port");
        foreach (var f in Directory.GetFiles(plugins, "*.dll", SearchOption.AllDirectories))
        {
            bool same;
            try
            {
                using var m = ModuleDefinition.ReadModule(f);
                same = m.Assembly.Name.Name == assemblyName || m.Types.SelectMany(t => t.CustomAttributes)
                    .Any(a => a.AttributeType.FullName == "BepInEx.BepInPlugin" && guids.Contains((string)a.ConstructorArguments[0].Value));
            }
            catch { continue; }
            if (!same) continue;
            var dest = Path.Combine(parked, Path.GetRelativePath(plugins, f));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Move(f, dest, true);
            Out.Warn($"moved {Path.GetRelativePath(plugins, f)} out of BepInEx/plugins to {Path.GetRelativePath(Path.GetDirectoryName(plugins)!, dest)} " +
                     "(otherwise it loads twice). If the game is running with that copy, restart it once.");
        }
    }
}
