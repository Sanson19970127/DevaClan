using System;
using HarmonyLib;

namespace DevaClan
{
    // Unit-ability target pruning calls the effect test in preview mode. Native
    // GainEnergy correctly forbids preview execution, but that also removes the
    // caster from the selectable targets. Relax only the selection-only test;
    // actual combat previews still cannot apply the energy effect.
    [HarmonyPatch(typeof(CommonSelectionBehavior), "PrunePossibleTargets")]
    static class ConversionTargetingScope
    {
        [ThreadStatic] internal static int Depth;
        static void Prefix(CardState thisCard, out bool __state)
        {
            __state = Plugin.Is(thisCard, "Convert");
            if (__state) Depth++;
        }
        static Exception Finalizer(Exception __exception, bool __state)
        {
            if (__state) Depth--;
            return __exception;
        }
    }
    [HarmonyPatch(typeof(GameEffectHelper), nameof(GameEffectHelper.TestEffect))]
    static class ConversionTargetingTest
    {
        static void Prefix(CardEffectState effectState, CardEffectParams cardEffectParams, ref bool previewMode)
        {
            if (ConversionTargetingScope.Depth > 0 && Plugin.Is(cardEffectParams.playedCard, "Convert")
                && effectState.GetCardEffect() is CardEffectGainEnergy) previewMode = false;
        }
    }
}
