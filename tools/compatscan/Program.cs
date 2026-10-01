using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Text;

// compatscan [--game DIR] [--exclude REL ...] [--add DIR ...]
// Resolves every TypeRef / MemberRef in every BepInEx plugin DLL (minus excluded, plus added) against the
// current game's Managed/ assemblies (+ BepInEx/core + the same plugin set), and checks Harmony string targets.
string gameDir = Path.Combine(Environment.GetEnvironmentVariable("HOME")!, "Library/Application Support/Steam/steamapps/common/ROUNDS");
var excludes = new List<string>(); var adds = new List<string>();
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--game") gameDir = args[++i];
    else if (args[i] == "--exclude") excludes.Add(args[++i]);
    else if (args[i] == "--add") adds.Add(args[++i]);
}
var managed = Path.Combine(gameDir, "ROUNDS.app/Contents/Resources/Data/Managed");
var core = Path.Combine(gameDir, "BepInEx/core");
var plugins = Path.Combine(gameDir, "BepInEx/plugins");

var resolver = new MapResolver();
foreach (var f in Directory.GetFiles(managed, "*.dll")) resolver.Add(f, "Managed");
foreach (var f in Directory.GetFiles(core, "*.dll")) resolver.Add(f, "BepInEx/core");
// (label, path)
var pluginDlls = new List<(string label, string path)>();
foreach (var d in adds)
    foreach (var f in Directory.GetFiles(d, "*.dll", SearchOption.AllDirectories).OrderBy(x => x))
        pluginDlls.Add(("STAGED/" + Path.GetFileName(Path.GetDirectoryName(f)) + " :: " + Path.GetFileName(f), f));
foreach (var f in Directory.GetFiles(plugins, "*.dll", SearchOption.AllDirectories).OrderBy(x => x))
{
    var rel = Path.GetRelativePath(plugins, f);
    if (excludes.Any(e => rel.StartsWith(e))) continue;
    pluginDlls.Add((rel.Split(Path.DirectorySeparatorChar)[0] + " :: " + Path.GetFileName(f), f));
}
foreach (var (_, f) in pluginDlls) resolver.Add(f, "plugins"); // staged first => wins on name clash
var rp = new ReaderParameters { AssemblyResolver = resolver, ReadingMode = ReadingMode.Deferred };
var mdResolver = new MetadataResolver(resolver);

var sb = new StringBuilder();
var byMember = new SortedDictionary<string, SortedSet<string>>();
var perMod = new SortedDictionary<string, List<string>>();
var harmony = new List<string>();
var counts = new SortedDictionary<string, (int types, int members, int harmony)>();

foreach (var (mod, dll) in pluginDlls)
{
    var issues = new List<string>();
    int nt = 0, nm = 0, nh = 0;
    ModuleDefinition module;
    try { module = ModuleDefinition.ReadModule(dll, rp); }
    catch (Exception e) { issues.Add($"  !! cannot read module: {e.Message}"); perMod[mod] = issues; counts[mod] = (0, 0, 0); continue; }

    foreach (var tr in module.GetTypeReferences())
    {
        string? why = TryResolveType(tr);
        if (why != null)
        {
            nt++;
            var key = $"[type] {Scope(tr)} {tr.FullName}";
            issues.Add($"  {key}  -- {why}");
            Add(byMember, key, mod);
        }
    }
    foreach (var mr in module.GetMemberReferences())
    {
        // Skip members on array/generic-param declaring types that cecil handles specially
        if (mr.DeclaringType is ArrayType) continue;
        string? why = null;
        try
        {
            var declType = mr.DeclaringType.GetElementType();
            var dt = SafeResolve(declType);
            if (dt == null) why = "declaring type unresolved";
            else
            {
                IMemberDefinition? res = mr switch
                {
                    MethodReference m => SafeResolveM(m),
                    FieldReference f => SafeResolveF(f),
                    _ => null
                };
                if (res == null) why = "member not found (" + Hints(dt, mr.Name) + ")";
            }
        }
        catch (Exception e) { why = "exception: " + e.GetType().Name + " " + e.Message; }
        if (why != null)
        {
            nm++;
            var key = $"[{(mr is FieldReference ? "field" : "method")}] {Scope(mr.DeclaringType)} {mr.FullName}";
            issues.Add($"  {key}  -- {why}");
            Add(byMember, key, mod);
        }
    }

    // Inheritance checks: abstract members of external base types must be implemented; overrides must still override
    foreach (var t in AllTypes(module))
    {
        if (t.BaseType == null || t.IsInterface) continue;
        var chain = new List<TypeDefinition>();
        for (var b = SafeResolve(t.BaseType); b != null; b = b.BaseType == null ? null : SafeResolve(b.BaseType)) chain.Add(b);
        if (!chain.Any(b => b.Module.Assembly.Name.Name != module.Assembly.Name.Name && !b.Module.Assembly.Name.Name.StartsWith("UnityEngine") && b.Module.Assembly.Name.Name != "mscorlib")) continue;
        var all = new List<TypeDefinition> { t }; all.AddRange(chain);
        if (!t.IsAbstract)
            foreach (var b in chain)
                foreach (var am in b.Methods.Where(x => x.IsAbstract))
                {
                    bool impl = all.TakeWhile(x => x != b).Any(x => x.Methods.Any(m => m.Name == am.Name && !m.IsAbstract && SigEq(m, am)));
                    if (!impl) { nh++; var l = $"  [inherit] {t.FullName} does not implement abstract {am.FullName} (TypeLoadException at load)"; issues.Add(l); Add(byMember, $"[abstract not implemented] {am.FullName}", mod); }
                }
        foreach (var m in t.Methods.Where(m => m.IsVirtual && !m.IsNewSlot && !m.IsAbstract))
        {
            bool overrides = chain.Any(b => b.Methods.Any(x => x.IsVirtual && x.Name == m.Name && SigEq(m, x)));
            if (!overrides && chain.Any(b => b.Module.Assembly.Name.Name == "Assembly-CSharp" && b.Methods.Any(x => x.Name == m.Name)))
            { nh++; var l = $"  [inherit] {t.FullName}::{m.Name} 'override' no longer matches any base virtual (base has: {string.Join(" | ", chain.SelectMany(b => b.Methods.Where(x => x.Name == m.Name)).Select(x => x.FullName))})"; issues.Add(l); Add(byMember, $"[override orphaned] {m.FullName}", mod); }
        }
    }

    // Harmony attribute + reflection string-target checks
    foreach (var t in AllTypes(module))
    {
        var classAttr = HarmonyInfo.From(t.CustomAttributes, module);
        bool typeHasPatchMethods = t.Methods.Any(m => m.CustomAttributes.Any(IsHarmonyPatchAttr));
        if (classAttr != null && !typeHasPatchMethods && !t.Methods.Any(m => m.Name is "TargetMethod" or "TargetMethods"))
        { /* class-level only: Prefix/Postfix by name */ }
        var targets = new List<(HarmonyInfo info, string where)>();
        if (classAttr != null && !t.Methods.Any(m => m.Name is "TargetMethod" or "TargetMethods"))
        {
            // Methods with own attributes override/merge; a class-level-only patch uses Prefix/Postfix/Transpiler by name
            var patchMethods = t.Methods.Where(m => m.CustomAttributes.Any(IsHarmonyPatchAttr)).ToList();
            if (patchMethods.Count == 0) targets.Add((classAttr, t.FullName));
            foreach (var pm in patchMethods)
                targets.Add((HarmonyInfo.Merge(classAttr, HarmonyInfo.From(pm.CustomAttributes, module)!), t.FullName + "::" + pm.Name));
        }
        else if (classAttr == null)
        {
            foreach (var pm in t.Methods.Where(m => m.CustomAttributes.Any(IsHarmonyPatchAttr)))
            {
                var info = HarmonyInfo.From(pm.CustomAttributes, module)!;
                targets.Add((info, t.FullName + "::" + pm.Name));
            }
        }
        foreach (var (info, where) in targets)
        {
            var problem = CheckHarmony(info);
            if (problem != null)
            {
                nh++;
                var line = $"  [harmony-attr] {where} -> {info}  -- {problem}";
                issues.Add(line); harmony.Add(mod + "\n" + line);
                Add(byMember, $"[harmony-attr target] {info}", mod);
            }
            else
            {
                // Parameter-name injection check for Prefix/Postfix/Finalizer
                var patchMethods = where.Contains("::") ? t.Methods.Where(pm => t.FullName + "::" + pm.Name == where).ToList()
                    : t.Methods.Where(pm => pm.Name is "Prefix" or "Postfix" or "Finalizer" || pm.CustomAttributes.Any(a => a.AttributeType.Name is "HarmonyPrefix" or "HarmonyPostfix" or "HarmonyFinalizer")).ToList();
                foreach (var pm in patchMethods)
                {
                    if (pm.Name == "Transpiler" || pm.CustomAttributes.Any(a => a.AttributeType.Name == "HarmonyTranspiler")) continue;
                    var pp = CheckPatchParams(info, pm);
                    if (pp != null)
                    {
                        nh++;
                        var line = $"  [harmony-params] {t.FullName}::{pm.Name} -> {info}  -- {pp}";
                        issues.Add(line); harmony.Add(mod + "\n" + line);
                        Add(byMember, $"[harmony-params] {info} :: {pp}", mod);
                    }
                }
            }
        }

        foreach (var m in t.Methods.Where(m => m.HasBody))
        {
            var ins = m.Body.Instructions;
            for (int i = 0; i < ins.Count; i++)
            {
                if (ins[i].OpCode.Code is not (Code.Call or Code.Callvirt) || ins[i].Operand is not MethodReference call) continue;
                var dn = call.DeclaringType.Name;
                var n = call.Name;
                bool accessTools = dn == "AccessTools" && n is "Method" or "Field" or "Property" or "PropertyGetter" or "PropertySetter"
                    or "DeclaredMethod" or "DeclaredField" or "DeclaredProperty" or "DeclaredPropertyGetter" or "DeclaredPropertySetter" or "FieldRefAccess" or "StaticFieldRefAccess" or "Constructor";
                bool reflect = call.DeclaringType.FullName == "System.Type" && n is "GetMethod" or "GetField" or "GetProperty";
                bool traverse = dn == "Traverse" && n is "Field" or "Method" or "Property";
                if (!(accessTools || reflect || traverse)) continue;
                if (dn == "AccessTools" && n == "Method" && call.Parameters.Count > 0 && call.Parameters[0].ParameterType.FullName == "System.String" && call.Parameters.Count <= 3 && FindLdstr(ins, i, 6) is string tm && tm.Contains(':'))
                {
                    // AccessTools.Method("Type:Method")
                    var parts = tm.Split(':');
                    var td = FindTypeByName(parts[0]);
                    string? p = td == null ? "type not found" : (HasMember(td, parts[1], "Method") ? null : "member not found (" + Hints(td, parts[1]) + ")");
                    if (p != null) { nh++; var l = $"  [reflect] {t.FullName}::{m.Name} AccessTools.Method(\"{tm}\")  -- {p}"; issues.Add(l); harmony.Add(mod + "\n" + l); Add(byMember, $"[reflect target] {tm}", mod); }
                    continue;
                }
                string? name = FindLdstr(ins, i, 8);
                if (name == null) continue;
                TypeReference? tref = null;
                if (call is GenericInstanceMethod gim && n.Contains("FieldRefAccess")) tref = gim.GenericArguments[0];
                else tref = FindLdtoken(ins, i, 12);
                string kind = n.Contains("Field") ? "Field" : n.Contains("Propert") ? "Property" : n.Contains("Constructor") ? "Ctor" : "Method";
                if (kind == "Ctor") continue;
                if (tref == null)
                {
                    // Unknown target type (Traverse on object / GetType()); check any game type has such a member
                    if (!AnyGameTypeHas(name, kind) && !AnyTypeIn(module, name, kind))
                    {
                        nh++; var l = $"  [reflect?] {t.FullName}::{m.Name} {dn}.{n}(\"{name}\") on unknown type -- no game/mod type has a {kind} named '{name}'";
                        issues.Add(l); harmony.Add(mod + "\n" + l); Add(byMember, $"[reflect target ?] {kind} {name}", mod);
                    }
                    continue;
                }
                var tdef = SafeResolve(tref);
                string? prob = tdef == null ? "type not found" : (HasMember(tdef, name, kind) ? null : "member not found (" + Hints(tdef, name) + ")");
                if (prob != null)
                {
                    nh++;
                    var l = $"  [reflect] {t.FullName}::{m.Name} {dn}.{n}({tref.FullName}, \"{name}\")  -- {prob}";
                    issues.Add(l); harmony.Add(mod + "\n" + l); Add(byMember, $"[reflect target] {kind} {tref.FullName}::{name}", mod);
                }
            }
        }
    }
    perMod[mod] = issues;
    counts[mod] = (nt, nm, nh);
}

sb.AppendLine($"compatscan — game: {gameDir}");
sb.AppendLine("excluded: " + string.Join(", ", excludes) + " | added: " + string.Join(", ", adds));
sb.AppendLine("resolution map: " + string.Join(", ", resolver.Map().Where(kv => !kv.Value.Contains("/Managed/")).Select(kv => kv.Key + "=" + Path.GetFileName(Path.GetDirectoryName(kv.Value)) + "/" + Path.GetFileName(kv.Value))));
sb.AppendLine($"Generated {DateTime.Now:u}. Resolution order: Managed > BepInEx/core > plugins.");
sb.AppendLine();
sb.AppendLine("=== SUMMARY (unresolved typeRefs / memberRefs / harmony+reflection string targets) ===");
foreach (var (k, v) in counts) sb.AppendLine($"{v.types,4} {v.members,4} {v.harmony,4}  {k}");
sb.AppendLine($"TOTAL {counts.Values.Sum(v => v.types)} types, {counts.Values.Sum(v => v.members)} members, {counts.Values.Sum(v => v.harmony)} harmony/reflection");
sb.AppendLine();
sb.AppendLine("=== BY MISSING MEMBER (-> mods referencing it) ===");
foreach (var (k, v) in byMember) { sb.AppendLine(k); foreach (var mm in v) sb.AppendLine("    " + mm); }
sb.AppendLine();
sb.AppendLine("=== BY MOD ===");
foreach (var (k, v) in perMod) { if (v.Count == 0) continue; sb.AppendLine(k); foreach (var l in v) sb.AppendLine(l); sb.AppendLine(); }
Console.Write(sb.ToString());

// ---------------- helpers ----------------
string Scope(TypeReference t)
{
    while (t.DeclaringType != null) t = t.DeclaringType;
    return "[" + t.Scope?.Name + "]";
}
void Add(SortedDictionary<string, SortedSet<string>> d, string k, string v) { if (!d.TryGetValue(k, out var s)) d[k] = s = new(); s.Add(v); }
TypeDefinition? SafeResolve(TypeReference t) { try { return t.Resolve(); } catch { return null; } }
MethodDefinition? SafeResolveM(MethodReference m) { try { return mdResolver.Resolve(m); } catch { return null; } }
FieldDefinition? SafeResolveF(FieldReference f) { try { return mdResolver.Resolve(f); } catch { return null; } }
string? TryResolveType(TypeReference t)
{
    try { return t.Resolve() == null ? "type not found in " + t.Scope?.Name : null; }
    catch (AssemblyResolutionException e) { return "assembly not found: " + e.AssemblyReference.FullName; }
    catch (Exception e) { return e.Message; }
}
string Hints(TypeDefinition td, string name)
{
    var hits = new List<string>();
    for (var cur = td; cur != null; cur = cur.BaseType == null ? null : SafeResolve(cur.BaseType))
    {
        hits.AddRange(cur.Methods.Where(x => x.Name == name || x.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || x.Name == "get_" + name || x.Name == "m_" + name).Select(x => "has " + x.FullName));
        hits.AddRange(cur.Fields.Where(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || x.Name.Equals("m_" + name, StringComparison.OrdinalIgnoreCase)).Select(x => "has field " + x.FullName));
        hits.AddRange(cur.Properties.Where(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(x => "has prop " + x.FullName));
        if (cur.Module.Assembly.Name.Name != td.Module.Assembly.Name.Name) break;
    }
    return hits.Count == 0 ? "no same-name member" : string.Join("; ", hits.Distinct());
}
bool HasMember(TypeDefinition td, string name, string kind)
{
    for (var cur = td; cur != null; cur = cur.BaseType == null ? null : SafeResolve(cur.BaseType))
    {
        if (kind == "Field" && cur.Fields.Any(f => f.Name == name)) return true;
        if (kind == "Property" && cur.Properties.Any(p => p.Name == name)) return true;
        if (kind == "Method" && cur.Methods.Any(p => p.Name == name)) return true;
        if (kind == "Getter" && cur.Properties.Any(p => p.Name == name && p.GetMethod != null)) return true;
        if (kind == "Setter" && cur.Properties.Any(p => p.Name == name && p.SetMethod != null)) return true;
    }
    return false;
}
IEnumerable<TypeDefinition> AllTypes(ModuleDefinition md)
{
    foreach (var t in md.Types) { yield return t; foreach (var n in Nested(t)) yield return n; }
}
IEnumerable<TypeDefinition> Nested(TypeDefinition t) { foreach (var n in t.NestedTypes) { yield return n; foreach (var x in Nested(n)) yield return x; } }
bool IsHarmonyPatchAttr(CustomAttribute a) => a.AttributeType.Name is "HarmonyPatch";
string? FindLdstr(Mono.Collections.Generic.Collection<Instruction> ins, int i, int back)
{
    for (int j = i - 1; j >= 0 && j >= i - back; j--) if (ins[j].OpCode.Code == Code.Ldstr) return (string)ins[j].Operand;
    return null;
}
TypeReference? FindLdtoken(Mono.Collections.Generic.Collection<Instruction> ins, int i, int back)
{
    for (int j = i - 1; j >= 0 && j >= i - back; j--)
    {
        if (ins[j].OpCode.Code == Code.Ldtoken && ins[j].Operand is TypeReference tr) return tr;
        if (ins[j].OpCode.Code is Code.Call or Code.Callvirt && ins[j].Operand is MethodReference mr && mr.Name is "GetMethod" or "GetField" or "GetProperty" or "Method" or "Field" or "Property") return null; // crossed another lookup
    }
    return null;
}
TypeDefinition? FindTypeByName(string n)
{
    foreach (var a in resolver.Loaded()) { var t = a.MainModule.GetType(n); if (t != null) return t; }
    var gm = resolver.Get("Assembly-CSharp"); return gm?.MainModule.Types.FirstOrDefault(t => t.Name == n);
}
bool AnyGameTypeHas(string name, string kind)
{
    foreach (var an in new[] { "Assembly-CSharp", "UnityEngine.CoreModule", "UnityEngine.PhysicsModule", "UnityEngine.Physics2DModule", "UnityEngine.UI", "Unity.TextMeshPro", "PhotonUnityNetworking", "PhotonRealtime" })
    {
        var a = resolver.Get(an); if (a == null) continue;
        foreach (var t in AllTypes(a.MainModule))
            if ((kind == "Field" && t.Fields.Any(f => f.Name == name)) || (kind == "Property" && t.Properties.Any(p => p.Name == name)) || (kind == "Method" && t.Methods.Any(p => p.Name == name))) return true;
    }
    return false;
}
bool AnyTypeIn(ModuleDefinition md, string name, string kind) =>
    AllTypes(md).Any(t => (kind == "Field" && t.Fields.Any(f => f.Name == name)) || (kind == "Property" && t.Properties.Any(p => p.Name == name)) || (kind == "Method" && t.Methods.Any(p => p.Name == name)));
string? CheckHarmony(HarmonyInfo h)
{
    if (h.Type == null && h.TypeName == null) return null; // incomplete (dynamic) - can't check
    TypeDefinition? td = h.Type != null ? SafeResolve(h.Type) : FindTypeByName(h.TypeName!);
    if (td == null) return "target type not found";
    var mt = h.MethodType ?? 0; // 0 Normal,1 Getter,2 Setter,3 Constructor,4 StaticConstructor,5 Enumerator
    if (mt == 3) return td.Methods.Any(m => m.IsConstructor && !m.IsStatic && ArgsMatch(m, h.ArgTypes)) ? null : "ctor with those args not found";
    if (mt == 4) return td.Methods.Any(m => m.IsConstructor && m.IsStatic) ? null : "cctor not found";
    if (h.Method == null) return null;
    if (mt == 1) return HasMember(td, h.Method, "Getter") ? null : "property getter not found (" + Hints(td, h.Method) + ")";
    if (mt == 2) return HasMember(td, h.Method, "Setter") ? null : "property setter not found (" + Hints(td, h.Method) + ")";
    for (var cur = td; cur != null; cur = cur.BaseType == null ? null : SafeResolve(cur.BaseType))
    {
        var cands = cur.Methods.Where(m => m.Name == h.Method).ToList();
        if (cands.Count == 0) continue;
        if (h.ArgTypes == null) return cands.Count > 1 && !cands.Any(c => c.Parameters.Count == 0) /* Harmony falls back to the parameterless overload */ ? "AMBIGUOUS (Harmony throws AmbiguousMatchException; no parameterless fallback): no argumentTypes but " + cands.Count + " overloads: " + string.Join(" | ", cands.Select(c => c.FullName)) : null;
        if (cands.Any(m => ArgsMatch(m, h.ArgTypes))) return null;
        return "overload with args (" + string.Join(",", h.ArgTypes.Select(a => a.FullName)) + ") not found; have: " + string.Join(" | ", cands.Select(c => c.FullName));
    }
    return "method not found (" + Hints(td, h.Method) + ")";
}
string? CheckPatchParams(HarmonyInfo h, MethodDefinition patch)
{
    if (h.Type == null && h.TypeName == null) return null;
    TypeDefinition? td = h.Type != null ? SafeResolve(h.Type) : FindTypeByName(h.TypeName!);
    if (td == null) return null;
    var mt = h.MethodType ?? 0;
    List<MethodDefinition> cands = new();
    for (var cur = td; cur != null; cur = cur.BaseType == null ? null : SafeResolve(cur.BaseType))
    {
        if (mt == 3) cands.AddRange(cur.Methods.Where(m => m.IsConstructor && !m.IsStatic && ArgsMatch(m, h.ArgTypes)));
        else if (mt == 1) cands.AddRange(cur.Properties.Where(p => p.Name == h.Method && p.GetMethod != null).Select(p => p.GetMethod));
        else if (mt == 2) cands.AddRange(cur.Properties.Where(p => p.Name == h.Method && p.SetMethod != null).Select(p => p.SetMethod));
        else if (h.Method != null) cands.AddRange(cur.Methods.Where(m => m.Name == h.Method && ArgsMatch(m, h.ArgTypes)));
        if (cands.Count > 0) break;
    }
    if (cands.Count == 0) return null;
    var problems = new List<string>();
    foreach (var target in cands)
    {
        var errs = new List<string>();
        bool passThrough = patch.ReturnType.FullName != "System.Void" && patch.Parameters.Count > 0 && patch.Parameters[0].ParameterType.FullName == patch.ReturnType.FullName && patch.ReturnType.FullName != "System.Boolean";
        foreach (var p in patch.Parameters.Skip(passThrough ? 1 : 0))
        {
            var n = p.Name;
            if (p.CustomAttributes.Any(a => a.AttributeType.Name == "HarmonyArgument")) continue;
            if (n.StartsWith("___"))
            {
                var fname = n.Substring(3);
                bool found = false;
                for (var cur = td; cur != null && !found; cur = cur.BaseType == null ? null : SafeResolve(cur.BaseType))
                    found = cur.Fields.Any(f => f.Name == fname);
                if (!found) errs.Add($"injected field '{fname}' not on {td.Name}" + " (" + Hints(td, fname) + ")");
                continue;
            }
            if (n is "__instance" or "__result" or "__state" or "__originalMethod" or "__args" or "__runOriginal" or "__exception") continue;
            if (n.StartsWith("__") && int.TryParse(n.Substring(2), out _)) continue;
            var tp = target.Parameters.FirstOrDefault(x => x.Name == n);
            if (tp == null) { errs.Add($"param '{n}' not in target ({string.Join(", ", target.Parameters.Select(x => x.ParameterType.Name + " " + x.Name))})"); continue; }
            var a = p.ParameterType is ByReferenceType br ? br.ElementType : p.ParameterType;
            var b = tp.ParameterType is ByReferenceType br2 ? br2.ElementType : tp.ParameterType;
            if (a.FullName != b.FullName && a.FullName != "System.Object") errs.Add($"param '{n}' type {a.Name} != target {b.Name}");
        }
        if (errs.Count == 0) return null;
        problems.Add(string.Join("; ", errs));
    }
    return string.Join(" || ", problems.Distinct());
}
bool SigEq(MethodDefinition a, MethodDefinition b)
{
    if (a.Parameters.Count != b.Parameters.Count) return false;
    for (int i = 0; i < a.Parameters.Count; i++) if (a.Parameters[i].ParameterType.FullName != b.Parameters[i].ParameterType.FullName) return false;
    return a.ReturnType.FullName == b.ReturnType.FullName || a.ReturnType.IsGenericParameter || b.ReturnType.IsGenericParameter;
}
bool ArgsMatch(MethodDefinition m, List<TypeReference>? args)
{
    if (args == null) return true;
    if (m.Parameters.Count != args.Count) return false;
    for (int i = 0; i < args.Count; i++)
    {
        var pt = m.Parameters[i].ParameterType;
        var a = args[i];
        string pn = pt is ByReferenceType br ? br.ElementType.FullName : pt.FullName;
        if (pn != a.FullName && pt.Name.TrimEnd('&') != a.Name) return false;
    }
    return true;
}

class HarmonyInfo
{
    public TypeReference? Type; public string? TypeName; public string? Method; public int? MethodType; public List<TypeReference>? ArgTypes;
    public override string ToString() => $"{Type?.FullName ?? TypeName ?? "?"}::{Method ?? "?"}" + (MethodType is int mt && mt != 0 ? $" [MethodType {mt}]" : "") + (ArgTypes != null ? "(" + string.Join(",", ArgTypes.Select(a => a.Name)) + ")" : "");
    public static HarmonyInfo Merge(HarmonyInfo a, HarmonyInfo b) => new()
    { Type = b.Type ?? a.Type, TypeName = b.TypeName ?? a.TypeName, Method = b.Method ?? a.Method, MethodType = b.MethodType ?? a.MethodType, ArgTypes = b.ArgTypes ?? a.ArgTypes };
    public static HarmonyInfo? From(IEnumerable<CustomAttribute> attrs, ModuleDefinition md)
    {
        HarmonyInfo? h = null;
        foreach (var a in attrs.Where(a => a.AttributeType.Name == "HarmonyPatch"))
        {
            h ??= new HarmonyInfo();
            bool firstString = true;
            foreach (var arg in a.ConstructorArguments)
            {
                var v = arg.Value;
                if (v is TypeReference tr) h.Type = tr;
                else if (v is string s) { if (a.ConstructorArguments.Count >= 2 && a.ConstructorArguments[0].Value is string && firstString && a.ConstructorArguments[1].Value is string) h.TypeName = s; else h.Method = s; firstString = false; }
                else if (arg.Type.Name == "MethodType") h.MethodType = Convert.ToInt32(v);
                else if (v is CustomAttributeArgument[] arr && arr.Length > 0 && arr[0].Value is TypeReference) h.ArgTypes = arr.Select(x => (TypeReference)x.Value).ToList();
                else if (v is CustomAttributeArgument[] arr2 && arr2.Length == 0 && arg.Type.GetElementType().FullName == "System.Type") h.ArgTypes = new();
            }
        }
        return h;
    }
}

class MapResolver : IAssemblyResolver
{
    readonly Dictionary<string, string> paths = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, AssemblyDefinition> cache = new(StringComparer.OrdinalIgnoreCase);
    public void Add(string path, string origin)
    {
        string n;
        try { using var a = AssemblyDefinition.ReadAssembly(path); n = a.Name.Name; } catch { n = Path.GetFileNameWithoutExtension(path); }
        if (!paths.ContainsKey(n)) paths[n] = path;
    }
    public IEnumerable<KeyValuePair<string,string>> Map() => paths.OrderBy(k => k.Key);
    public IEnumerable<AssemblyDefinition> Loaded() => cache.Values.ToList();
    public AssemblyDefinition? Get(string name) { try { return Resolve(new AssemblyNameReference(name, null)); } catch { return null; } }
    public AssemblyDefinition Resolve(AssemblyNameReference name) => Resolve(name, new ReaderParameters { AssemblyResolver = this });
    public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
    {
        if (cache.TryGetValue(name.Name, out var a)) return a;
        if (!paths.TryGetValue(name.Name, out var p)) throw new AssemblyResolutionException(name);
        a = AssemblyDefinition.ReadAssembly(p, new ReaderParameters { AssemblyResolver = this, ReadingMode = ReadingMode.Deferred });
        cache[name.Name] = a; return a;
    }
    public void Dispose() { foreach (var a in cache.Values) a.Dispose(); }
}
