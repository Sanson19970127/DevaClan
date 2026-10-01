using System;
using HarmonyLib;

namespace DevaClan
{
    // Some native statuses only have character tooltip text. Reuse that
    // localized text on our cards, and defer to a card translation if supplied.
    [HarmonyPatch(typeof(StatusEffectManager), nameof(StatusEffectManager.GetLocalizedCardTooltipTextKey))]
    static class NativeStatusTooltips
    {
        internal static string Resolve(string statusId, string cardKey, Func<string, bool> hasTranslation)
        {
            if (statusId != "relentless" && statusId != "untouchable") return cardKey;
            if (hasTranslation(cardKey)) return cardKey;
            string characterKey = StatusEffectManager.GetLocalizedCharacterTooltipTextKey(statusId);
            return hasTranslation(characterKey) ? characterKey : cardKey;
        }
        static void Postfix(string statusId, ref string __result)
        {
            __result = Resolve(statusId, __result, key => key.HasTranslation());
        }
    }
}
