using System;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MacCompatFixes
{
    // Small fixes for running the (ported) mod stack on the current ROUNDS build, on macOS.
    // Loaded by ScriptEngine from BepInEx/scripts, so it can be hot-reloaded: every load must be able to
    // tear itself down completely in OnDestroy.
    [BepInPlugin("kieran.rounds.maccompatfixes", "Mac Compat Fixes", "1.8.1")]
    [BepInDependency("com.willis.rounds.unbound")]
    [BepInDependency("pykess.rounds.plugins.moddingutils", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.XAngelMoonX.rounds.CosmicRounds", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        Harmony harmony;
        GameObject helper;

        static readonly HashSet<Type> ShaderHooks = new HashSet<Type>
            { typeof(AssetBundleLoad_Hook), typeof(MaterialCtorShader_Hook), typeof(MaterialCtorMaterial_Hook) };

        // Only macOS (Metal) needs the shader work; on Windows the mods' own D3D shaders are fine.
        internal static bool IsMetal => SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Metal;

        private void Awake()
        {
            // Unique id per load: ScriptEngine loads the new copy before destroying the old one, and the old
            // copy's UnpatchSelf must not remove the new copy's patches.
            harmony = new Harmony("kieran.rounds.maccompatfixes." + Guid.NewGuid().ToString("N"));
            foreach (var t in typeof(Plugin).Assembly.GetTypes())
            {
                if (t.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0) continue;
                if (!IsMetal && ShaderHooks.Contains(t)) continue;
                try { harmony.CreateClassProcessor(t).Patch(); }
                catch (Exception e) { Logger.LogError($"patch {t.Name} failed: {e.GetBaseException().Message}"); }
            }
            ShaderFix.Log = Logger;
            CardBarHover_Fix.Log = Logger;
            helper = LetterboxClear.Create();
            MissingText.Apply(Logger);
            MenuTweaks.Log = Logger;
            try { KieranCredits.Register(Logger); } catch (Exception e) { Logger.LogWarning("credits: " + e.GetBaseException().Message); }
            helper.AddComponent<MenuTweaksRunner>();
            CardVisualFixesRunner.Log = Logger;
            helper.AddComponent<CardVisualFixesRunner>();
            if (IsMetal)
            {
                ShaderFix.Sweep();
                SceneManager.sceneLoaded += OnSceneLoaded;
                helper.AddComponent<ShaderFixRunner>();
            }
            Logger.LogInfo($"Mac Compat Fixes {Info.Metadata.Version} loaded ({SystemInfo.graphicsDeviceType})");
        }

        void OnSceneLoaded(Scene s, LoadSceneMode m) => ShaderFix.Sweep();

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            harmony?.UnpatchSelf();
            MissingText.Restore();
            if (helper != null) Destroy(helper);
            Logger.LogInfo("Mac Compat Fixes unloaded");
        }
    }

    internal static class CardNames
    {
        public static string Strip(string s) => s?.Replace("(Clone)", "").Trim();

        public static CardInfo FindByName(string name)
        {
            if (CardChoice.instance != null)
                foreach (var c in CardChoice.instance.cards)
                    if (c != null && Strip(c.gameObject.name) == name) return c;
            foreach (var c in HiddenCards())
                if (c != null && Strip(c.gameObject.name) == name) return c;
            foreach (var c in Resources.FindObjectsOfTypeAll<CardInfo>())
                if (c != null && Strip(c.gameObject.name) == name) return c;
            return null;
        }

        // Cards picked from pooled/network-spawned objects don't always carry the exact "<name>(Clone)" name
        // UnboundLib's GetSourceCard prefix requires, so it returns null and the card bar gets an empty button.
        public static CardInfo FindSource(CardChoice choice, CardInfo info)
        {
            if (info == null) return null;
            var name = Strip(info.gameObject.name);
            foreach (var c in choice.cards)
                if (c != null && Strip(c.gameObject.name) == name) return c;
            foreach (var c in HiddenCards())
                if (c != null && Strip(c.gameObject.name) == name) return c;
            return null;
        }

        static IEnumerable<CardInfo> HiddenCards()
        {
            var t = AccessTools.TypeByName("ModdingUtils.Utils.Cards");
            var inst = t == null ? null : AccessTools.Field(t, "instance")?.GetValue(null);
            var hidden = inst == null ? null : AccessTools.Property(t, "HiddenCards")?.GetValue(inst, null) as IEnumerable<CardInfo>;
            return hidden ?? Array.Empty<CardInfo>();
        }
    }

    [HarmonyPatch(typeof(CardChoice), nameof(CardChoice.GetSourceCard))]
    internal static class GetSourceCard_Fallback
    {
        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        static void Postfix(CardChoice __instance, CardInfo info, ref CardInfo __result)
        {
            if (__result == null) __result = CardNames.FindSource(__instance, info);
        }
    }


    // Card bar buttons can end up pointing at a destroyed CardInfo (the hover then throws inside
    // CardChoice.AddCardVisual). Remember each button's card name and re-resolve it on hover.
    [HarmonyPatch(typeof(CardBar))]
    internal static class CardBarHover_Fix
    {
        static readonly Dictionary<int, string> buttonCard = new Dictionary<int, string>();
        static readonly AccessTools.FieldRef<CardBar, List<CardBarButton>> cardsRef = AccessTools.FieldRefAccess<CardBar, List<CardBarButton>>("m_cards");
        public static BepInEx.Logging.ManualLogSource Log;

        [HarmonyPostfix, HarmonyPatch(nameof(CardBar.AddCard))]
        static void AddCard_Postfix(CardBar __instance, CardInfo card)
        {
            var list = cardsRef(__instance);
            if (card == null || list == null || list.Count == 0 || list[0] == null) return;
            buttonCard[list[0].GetInstanceID()] = CardNames.Strip(card.gameObject.name);
            Log?.LogDebug($"card bar + {card.gameObject.name} (scene '{card.gameObject.scene.name}')");
        }

        [HarmonyPrefix, HarmonyPatch(nameof(CardBar.OnHover), typeof(CardBarButton))]
        static bool OnHover_Prefix(CardBarButton cardButton)
        {
            if (cardButton == null) return false;
            if (cardButton.m_cardInfo != null) return true;
            if (buttonCard.TryGetValue(cardButton.GetInstanceID(), out var name))
            {
                var found = CardNames.FindByName(name);
                if (found != null) { cardButton.m_cardInfo = found; return true; }
                Log?.LogWarning($"card bar hover: no live card named {name}");
            }
            return false;   // skip instead of throwing
        }
    }

    // UnboundLib builds mod cards with LocalizedString("StringTableCards", <title>) but never adds the entry,
    // so CardName returns the "missing translation" text (with the title in quotes). Return the title instead.
    [HarmonyPatch(typeof(CardInfo), nameof(CardInfo.CardName), MethodType.Getter)]
    internal static class CardName_MissingTranslation
    {
        static void Postfix(CardInfo __instance, ref string __result)
        {
            var ls = __instance.LocalizedCardName;
            if (ls == null || ls.IsEmpty || __result == null) return;
            var key = ls.TableEntryReference.Key;
            if (string.IsNullOrEmpty(key) || __result == key) return;
            if (__result.Contains("'" + key + "'") || __result.StartsWith("No translation found", StringComparison.Ordinal))
                __result = key;
        }
    }
}

namespace MacCompatFixes
{
    // Mod asset bundles were built for Windows only (D3D11 shader programs, no Metal), so on macOS anything
    // using their shaders renders magenta. The game ships Metal builds of the same TextMeshPro / particle
    // shaders, so materials are re-pointed at those *objects* (not by name: the bundle copies share names).
    // Shaders the game doesn't ship are rebuilt on top of Particles/Standard Unlit with the original blend state.
    internal static class ShaderFix
    {
        public static BepInEx.Logging.ManualLogSource Log;
        static readonly HashSet<AssetBundle> recordedBundles = new HashSet<AssetBundle>();
        static readonly HashSet<string> reported = new HashSet<string>();
        // Shaders that came out of mod asset bundles. Their isSupported is unreliable (Unity reports true until
        // the first draw fails), so anything in this set is replaced regardless.
        static readonly HashSet<Shader> foreign = new HashSet<Shader>();
        static Dictionary<string, Shader> native;
        static int nativeFrame = -1;

        public static bool IsForeignPublic(Shader sh) => sh != null && foreign.Contains(sh);
        static bool IsForeign(Shader sh) => sh != null && (foreign.Contains(sh) || !sh.isSupported);

        // The game's own copy of a shader: mod bundles add same-named duplicates (D3D-only) that may still
        // report isSupported=true. Game shaders load first (sharedassets0), so the lowest instance id wins.
        static Shader Native(string name)
        {
            if ((native == null || !native.ContainsKey(name)) && nativeFrame != Time.frameCount)
            {
                nativeFrame = Time.frameCount;   // rebuild at most once per frame
                native = new Dictionary<string, Shader>();
                foreach (var sh in Resources.FindObjectsOfTypeAll<Shader>())
                {
                    if (sh == null || foreign.Contains(sh) || !sh.isSupported || sh.GetInstanceID() <= 0) continue;
                    if (!native.TryGetValue(sh.name, out var cur) || sh.GetInstanceID() < cur.GetInstanceID()) native[sh.name] = sh;
                }
            }
            return native != null && native.TryGetValue(name, out var s) ? s : null;
        }

        public static void RecordBundle(AssetBundle bundle)
        {
            if (bundle == null || bundle.isStreamedSceneAssetBundle || !recordedBundles.Add(bundle)) return;
            // The game's own Addressables bundles ("<hash>.bundle", e.g. CJK fonts) are built for Metal already and are
            // large; loading all their assets on every (hot) load caused frame hitches.
            if (bundle.name.EndsWith(".bundle")) return;
            var assets = bundle.LoadAllAssets();
            foreach (var o in assets) if (o is Shader sh) foreign.Add(sh);
            foreach (var o in assets)
            {
                if (o is Material mat) Fix(mat, bundle.name);
                // Particle/trail materials are often only prefab dependencies, not listed bundle assets.
                else if (o is GameObject go)
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                        foreach (var mm in r.sharedMaterials) Fix(mm, bundle.name);
            }
        }

        static readonly HashSet<string> ModOnlyShaders = new HashSet<string>
            { "Legacy Shaders/Particles/Additive", "Custom/Add", "Custom/Opacity2", "Standard" };
        static bool changed = true;   // first sweep after (re)load always refreshes the UI

        public static void Sweep()
        {
            foreach (var bundle in AssetBundle.GetAllLoadedAssetBundles()) RecordBundle(bundle);
            foreach (var m in Resources.FindObjectsOfTypeAll<Material>()) Fix(m, null);
            if (!changed) return;
            changed = false;
            // UI canvases cache their draw batches; without this, swapped text keeps drawing with the old shader.
            foreach (var g in Resources.FindObjectsOfTypeAll<UnityEngine.UI.Graphic>())
                if (g != null && g.isActiveAndEnabled) { g.SetMaterialDirty(); g.SetVerticesDirty(); }
            Canvas.ForceUpdateCanvases();
        }

        public static void Fix(Material m, string where)
        {
            if (m == null || m.shader == null) return;
            var sh = m.shader;
            var from = sh.name;
            // The game ships none of these, so any material using them came from a (D3D-only) mod bundle.
            // Rebuild up front: their isSupported only turns false after the first failed draw (a pink frame).
            if (ModOnlyShaders.Contains(from))
            {
                if (!Rebuild(m, from)) { Report($"waiting for a Metal shader for {from} ({m.name})"); return; }
                changed = true;
                Report($"{(where ?? "runtime")}/{m.name}: {from}#{sh.GetInstanceID()} -> {m.shader.name}#{m.shader.GetInstanceID()} (rebuilt)");
                return;
            }
            var same = Native(from);
            if (same != null)
            {
                if (same == sh) return;
                m.shader = same;
            }
            else if (!IsForeign(sh)) return;
            else if (!Rebuild(m, from)) { Report($"waiting for a Metal shader for {from} ({m.name})"); return; }
            changed = true;
            Report($"{(where ?? "runtime")}/{m.name}: {from}#{sh.GetInstanceID()} -> {m.shader.name}#{m.shader.GetInstanceID()}");
        }

        static void Report(string msg) { if (reported.Add(msg)) Log?.LogInfo(msg); }

        // BlendMode values (UnityEngine.Rendering.BlendMode): Zero=0 One=1 SrcAlpha=5 OneMinusSrcAlpha=10
        static bool Rebuild(Material m, string name)
        {
            var unlit = Native("Particles/Standard Unlit");
            if (unlit == null) return false;
            Texture tex; Color color; int src, dst; bool zwrite;
            switch (name)
            {
                case "Legacy Shaders/Particles/Additive":   // Blend SrcAlpha One; col = 2 * tint * vertex * tex
                    tex = Tex(m, "_MainTex"); color = (m.HasProperty("_TintColor") ? m.GetColor("_TintColor") : new Color(.5f, .5f, .5f, .5f)) * 2f;
                    src = 5; dst = 1; zwrite = false; break;
                case "Custom/Add":                          // Blend One One
                    tex = Tex(m, "_MainTexture"); color = Color.white; src = 1; dst = 1; zwrite = false; break;
                case "Custom/Opacity2":                     // Blend One Zero, ZWrite On (opaque)
                    tex = Tex(m, "_MainTexture"); color = Color.white; src = 1; dst = 0; zwrite = true; break;
                case "Standard":                            // keep the material's own blend setup
                    tex = Tex(m, "_MainTex"); color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                    src = m.HasProperty("_SrcBlend") ? (int)m.GetFloat("_SrcBlend") : 1;
                    dst = m.HasProperty("_DstBlend") ? (int)m.GetFloat("_DstBlend") : 0;
                    zwrite = !m.HasProperty("_ZWrite") || m.GetFloat("_ZWrite") > 0.5f; break;
                default:
                    var sprites = Native("Sprites/Default");
                    if (sprites == null) return false;
                    m.shader = sprites; return true;
            }
            int queue = m.renderQueue;
            m.shader = unlit;
            m.shaderKeywords = new string[0];
            if (tex != null) m.SetTexture("_MainTex", tex);
            m.SetColor("_Color", color);
            m.SetFloat("_BlendOp", 0);
            m.SetFloat("_SrcBlend", src);
            m.SetFloat("_DstBlend", dst);
            m.SetFloat("_ZWrite", zwrite ? 1 : 0);
            m.SetFloat("_Cull", 0);
            bool transparent = !(src == 1 && dst == 0);
            if (transparent)
            {
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.renderQueue = queue >= 2500 ? queue : 3000;
            }
            else
            {
                m.SetOverrideTag("RenderType", "Opaque");
                m.renderQueue = queue > 0 && queue < 2500 ? queue : 2000;
            }
            return true;
        }

        static Texture Tex(Material m, string prop) => m.HasProperty(prop) ? m.GetTexture(prop) : null;
    }

    // Fix bundle materials the moment a bundle loads, before anything using them is drawn.
    [HarmonyPatch]
    internal static class AssetBundleLoad_Hook
    {
        static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            foreach (var mi in typeof(AssetBundle).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                if (mi.ReturnType == typeof(AssetBundle) && mi.Name.StartsWith("LoadFrom") && !mi.Name.EndsWith("Async")
                    && mi.GetMethodBody() != null)   // skip extern (native) entry points
                    yield return mi;
        }
        static void Postfix(AssetBundle __result) => ShaderFix.RecordBundle(__result);
    }

    // Mods also build materials at runtime from bundle shaders (new Material(shader) / new Material(mat)).
    [HarmonyPatch(typeof(Material), MethodType.Constructor, typeof(Shader))]
    internal static class MaterialCtorShader_Hook { static void Postfix(Material __instance) => ShaderFix.Fix(__instance, "new"); }

    [HarmonyPatch(typeof(Material), MethodType.Constructor, typeof(Material))]
    internal static class MaterialCtorMaterial_Hook { static void Postfix(Material __instance) => ShaderFix.Fix(__instance, "copy"); }

    // Re-run the sweep regularly: mods instantiate prefabs / create material instances at runtime.
    internal class ShaderFixRunner : MonoBehaviour
    {
        float next;
        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.5f;
            ShaderFix.Sweep();
        }
    }

    // The game letterboxes to 16:9; on 16:10 Mac screens nothing clears the bars, so old UI pixels ghost there.
    internal static class LetterboxClear
    {
        public static GameObject Create()
        {
            var go = new GameObject("MacCompatFixes_LetterboxClear");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            var cam = go.AddComponent<Camera>();
            cam.depth = -100;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 0;
            cam.rect = new Rect(0, 0, 1, 1);
            cam.useOcclusionCulling = false;
            return go;
        }
    }
}

namespace MacCompatFixes
{
    // UnboundLib registers mod card titles/descriptions as LocalizedString keys that don't exist in the game's
    // string tables, so every UI that localizes them shows the missing-translation text ('Title' in quotes).
    // Make a missing entry render as its key, which for mod cards is the intended text.
    internal static class MissingText
    {
        static string previous;
        public static void Apply(BepInEx.Logging.ManualLogSource log)
        {
            try
            {
                var db = UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase;
                if (db == null) return;
                if (previous == null) previous = db.NoTranslationFoundMessage;
                db.NoTranslationFoundMessage = "{key}";
                log.LogInfo($"missing-translation text: '{previous}' -> '{{key}}'");
            }
            catch (System.Exception e) { log.LogWarning("missing-translation text not changed: " + e.Message); }
        }
        public static void Restore()
        {
            try { if (previous != null) UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase.NoTranslationFoundMessage = previous; }
            catch { }
        }
    }
}
