using HarmonyLib;
using UnityEngine;

namespace MacCompatFixes
{
    // Kieran's menu tweaks: hide UnboundLib's Discord/Thunderstore links and retitle "UNBOUND".
    // Kept reload-safe: Undo() puts both back when the plugin unloads.
    internal static class MenuTweaks
    {
        public const string Title = "KIERAN'S UNBOUND";
        static System.Type linksType;

        static System.Type LinksType => linksType ?? (linksType = AccessTools.TypeByName("UnboundLib.Utils.UI.MainMenuLinks"));

        static GameObject LinksObject() =>
            LinksType == null ? null : AccessTools.Field(LinksType, "links")?.GetValue(null) as GameObject;

        public static BepInEx.Logging.ManualLogSource Log;
        struct Original { public Vector2 size; public float lePref, leMin; public bool auto; public float font; public TMPro.FontStyles style; }
        static readonly System.Collections.Generic.Dictionary<int, Original> originals = new System.Collections.Generic.Dictionary<int, Original>();

        // UnboundLib's LOCAL/MODS/CREDITS buttons are 72px tall with auto-sized text (and get stuck bold), while the
        // game's own buttons are 92px with fixed 60pt text. Copy the vanilla button's metrics onto the mod ones.
        static void NormaliseButtons()
        {
            if (MainMenuHandler.instance == null) return;
            var group = MainMenuHandler.instance.transform.Find("Canvas/ListSelector/Main/Group");
            if (group == null) return;
            RectTransform reference = null; TMPro.TMP_Text refText = null;
            foreach (Transform c in group)
                if (c.gameObject.activeSelf && c.GetComponent<ListMenuButton>() != null && c.GetComponent<UnityEngine.UI.LayoutElement>() == null)
                { reference = c as RectTransform; refText = c.GetComponentInChildren<TMPro.TMP_Text>(true); break; }
            if (reference == null || refText == null) return;
            foreach (Transform c in group)
            {
                var rt = c as RectTransform;
                var le = c.GetComponent<UnityEngine.UI.LayoutElement>();
                var tx = c.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (rt == null || le == null || le.ignoreLayout || tx == null || c.GetComponent<ListMenuButton>() == null) continue;
                if (originals.ContainsKey(c.GetInstanceID())) continue;   // only once per button, so selection bold still works
                originals[c.GetInstanceID()] = new Original { size = rt.sizeDelta, lePref = le.preferredHeight, leMin = le.minHeight, auto = tx.enableAutoSizing, font = tx.fontSize, style = tx.fontStyle };
                var h = reference.sizeDelta.y;
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);
                le.preferredHeight = h; le.minHeight = h;
                tx.enableAutoSizing = false;
                tx.fontSize = refText.fontSize;
                tx.characterSpacing = refText.characterSpacing;
                tx.fontStyle = (tx.fontStyle & ~TMPro.FontStyles.Bold) | (refText.fontStyle & TMPro.FontStyles.UpperCase);
                Log?.LogInfo($"[menu] {c.name}: {originals[c.GetInstanceID()].size.y}px/auto -> {h}px/{refText.fontSize}pt");
            }
        }

        static void UndoButtons()
        {
            if (MainMenuHandler.instance == null) return;
            var group = MainMenuHandler.instance.transform.Find("Canvas/ListSelector/Main/Group");
            if (group == null) return;
            foreach (Transform c in group)
            {
                if (!originals.TryGetValue(c.GetInstanceID(), out var o)) continue;
                var le = c.GetComponent<UnityEngine.UI.LayoutElement>();
                var tx = c.GetComponentInChildren<TMPro.TMP_Text>(true);
                ((RectTransform)c).sizeDelta = o.size;
                if (le != null) { le.preferredHeight = o.lePref; le.minHeight = o.leMin; }
                if (tx != null) { tx.enableAutoSizing = o.auto; tx.fontSize = o.font; tx.fontStyle = o.style; }
            }
            originals.Clear();
        }

        static string Describe(Transform t)
        {
            var rt = t as RectTransform;
            var le = t.GetComponent<UnityEngine.UI.LayoutElement>();
            var tx = t.GetComponentInChildren<TMPro.TMP_Text>(true);
            return $"{t.name}: size={rt?.sizeDelta} pos={rt?.anchoredPosition} scale={t.localScale} " +
                   $"le=[min {le?.minHeight} pref {le?.preferredHeight} flex {le?.flexibleHeight} ignore {le?.ignoreLayout}] " +
                   $"text=[{tx?.font?.name} size {tx?.fontSize} auto {tx?.enableAutoSizing} min {tx?.fontSizeMin} max {tx?.fontSizeMax} sp {tx?.characterSpacing} style {tx?.fontStyle} rt {(tx?.transform as RectTransform)?.sizeDelta}]";
        }

        public static void Apply()
        {
            NormaliseButtons();
            KieranCredits.Ensure(Log);

            var links = LinksObject();
            if (links != null && links.activeSelf) links.SetActive(false);

            var go = GameObject.Find("Unbound Text Object");
            var text = go != null ? go.GetComponent<TMPro.TMP_Text>() : null;
            if (text != null && text.text == "UNBOUND")
            {
                text.enableWordWrapping = false;
                text.text = Title;
            }
        }

        public static void Undo()
        {
            UndoButtons();
            KieranCredits.Unregister();
            var links = LinksObject();
            if (links != null) links.SetActive(true);
            var go = GameObject.Find("Unbound Text Object");
            var text = go != null ? go.GetComponent<TMPro.TMP_Text>() : null;
            if (text != null && text.text == Title) text.text = "UNBOUND";
        }
    }

    // A page under CREDITS (UnboundLib builds the credits menu each time the main menu loads).
    internal static class KieranCredits
    {
        public const string Page = MenuTweaks.Title;
        public const string Repo = "https://github.com/KieranK07/rounds-mac-modpack";
        static readonly string[] Lines =
        {
            "Kieran (Mac port, fixes for the 2025 ROUNDS update, patcher)",
            " ",
            "Built entirely on other people's work:",
            "Willis, Tilastokeskus, Pykess, Ascyst, Boss Sloth Inc., willuwontu, otDan (UnboundLib)",
            "Bknibb (UnboundLib + RoundsWithFriends updates for ROUNDS v1.1.2)",
            "olavim (RoundsWithFriends, MapsExtended)",
            "Pykess (ModdingUtils + patches)",
            "XAngelMoonX (Cosmic Rounds)",
            "Root / Tess-y (Classes Manager Reborn, RarityLib, CardThemeLib + patches)",
            "willis81808 (ModsPlus), willuwontu (Wacky Map Objects + patches)",
            "BossSloth, TeamDK, Senyksia, Ascyst, RoundsModding (patches)",
            "BepInEx team (BepInEx, ScriptEngine), TeamSirenix (Odin Serializer)",
            "Landfall Games (ROUNDS)",
        };

        public static void Register(BepInEx.Logging.ManualLogSource log)
        {
            var m = AccessTools.Method(AccessTools.TypeByName("UnboundLib.Unbound"), "RegisterCredits",
                new[] { typeof(string), typeof(string[]), typeof(string), typeof(string) });
            if (m == null) { log.LogWarning("credits: UnboundLib.RegisterCredits not found"); return; }
            m.Invoke(null, new object[] { Page, Lines, "GITHUB", Repo });
        }

        static System.Collections.IDictionary Registered()
        {
            var credits = AccessTools.TypeByName("UnboundLib.Utils.UI.Credits");
            var inst = credits == null ? null : AccessTools.Field(credits, "Instance")?.GetValue(null);
            return inst == null ? null : AccessTools.Field(credits, "modCredits")?.GetValue(inst) as System.Collections.IDictionary;
        }

        // On hot reload the old copy's Unregister runs after the new copy registered, so re-check regularly.
        public static void Ensure(BepInEx.Logging.ManualLogSource log)
        {
            var d = Registered();
            if (d != null && !d.Contains(Page)) Register(log);
        }

        public static void Unregister() => Registered()?.Remove(Page);
    }

    // UnboundLib re-shows the links every time the main menu opens.
    [HarmonyPatch]
    internal static class MainMenuLinks_AddLinks_Skip
    {
        static bool Prepare() => AccessTools.TypeByName("UnboundLib.Utils.UI.MainMenuLinks") != null;
        static System.Reflection.MethodBase TargetMethod() =>
            AccessTools.Method(AccessTools.TypeByName("UnboundLib.Utils.UI.MainMenuLinks"), "AddLinks");
        static bool Prefix() => false;
    }

    internal class MenuTweaksRunner : MonoBehaviour
    {
        float next;
        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.25f;
            MenuTweaks.Apply();
        }
        void OnDestroy() => MenuTweaks.Undo();
    }
}
