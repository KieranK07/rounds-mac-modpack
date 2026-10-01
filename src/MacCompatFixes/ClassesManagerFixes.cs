using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MacCompatFixes
{
    // ClassesManagerReborn's JACK card cycles rainbow colours while its CardVisuals is "selected". UnboundLib's toggle
    // menu shows every card as selected, so JACK flashed nonstop there. In that menu, only run it while hovered and
    // put the card's theme colour back when the pointer leaves. Real card picks are untouched.
    [HarmonyPatch]
    internal static class CMR_RainbowMenuHover_Fix
    {
        static System.Type Rainbow => AccessTools.TypeByName("ClassesManagerReborn.Cards.Rainbow");
        static bool Prepare() => Rainbow != null && UnboundUi.MenuCard != null;
        static MethodBase TargetMethod() => AccessTools.Method(Rainbow, "Update");

        static readonly Dictionary<int, bool> wasHovered = new Dictionary<int, bool>();

        static bool Prefix(MonoBehaviour __instance)
        {
            var menuCard = __instance.GetComponentInParent(UnboundUi.MenuCard, true);
            if (menuCard == null) return true;                       // a real card in a pick: leave it alone
            var hover = menuCard.transform.parent != null ? menuCard.transform.parent.GetComponent<MenuCardHover>() : null;
            bool hovered = hover != null && hover.Hovered;
            int id = __instance.GetInstanceID();
            bool before = !wasHovered.TryGetValue(id, out var b) || b; // first frame: treat as "was hovered" so we restore
            wasHovered[id] = hovered;
            if (hovered) return true;
            if (before) Restore(__instance);
            return false;
        }

        static void Restore(MonoBehaviour rainbow)
        {
            var visuals = rainbow.GetComponentInChildren<CardVisuals>(true);
            var info = rainbow.GetComponentInParent<CardInfo>();
            if (visuals == null || info == null || CardChoice.instance == null) return;
            var c = CardChoice.instance.GetCardColor(info.colorTheme);
            visuals.defaultColor = c;
            if (visuals.images != null) foreach (var img in visuals.images) if (img != null) img.color = c;
            if (visuals.nameText != null) visuals.nameText.color = c;
        }
    }
}
