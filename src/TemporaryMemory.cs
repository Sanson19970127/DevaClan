using System;
using HarmonyLib;

namespace DevaClan
{
    // Battle-only restriction. Use card INSTANCE identity: GetID() is a shared
    // CardData ID, so it would wrongly mark every copy of the same unit.
    internal static class TemporaryMemory
    {
        internal static bool Contains(CharacterState unit)
        {
            if (unit == null) return false;
            var memory = BattleMemory.Active;
            var card = unit.GetSpawnerCard();
            return memory.TemporaryUnits.Contains(unit)
                || (card != null && memory.TemporaryCards.Contains(card))
                || (!unit.IsDestroyed && unit.GetStatusEffectStacks(Effects.TemporaryId) > 0);
        }
        internal static void Remember(CharacterState unit, bool force = false)
        {
            if (unit == null || (!force && !Contains(unit))) return;
            BattleMemory.Active.TemporaryUnits.Add(unit);
            var card = unit.GetSpawnerCard();
            if (card != null) BattleMemory.Active.TemporaryCards.Add(card);
        }
        internal static void Mark(CharacterState unit)
        {
            if (unit == null || unit.IsDestroyed) return;
            Remember(unit, true);
            if (unit.GetStatusEffectStacks(Effects.TemporaryId) == 0)
                unit.AddStatusEffect(Effects.TemporaryId, 1, (CharacterState)null, allowModification: false);
        }
        internal static void Restore(CharacterState unit)
        {
            if (Contains(unit)) Mark(unit);
        }
        internal static void Copy(CardState source, CardState copy)
        {
            if (source != null && copy != null && BattleMemory.Active.TemporaryCards.Contains(source))
                BattleMemory.Active.TemporaryCards.Add(copy);
        }
    }

    // Cover purify/cleanse/equipment/status-copy cleanup without changing native statuses.
    [HarmonyPatch(typeof(CharacterState), nameof(CharacterState.RemoveStatusEffect),
        new Type[] { typeof(string), typeof(int), typeof(CharacterState.RemoveStatusEffectParams), typeof(bool) })]
    static class PreserveTemporaryStatus
    {
        static bool Prefix(CharacterState __instance, string statusId)
        {
            if (!string.Equals(statusId, Effects.TemporaryId, StringComparison.OrdinalIgnoreCase)) return true;
            TemporaryMemory.Remember(__instance);
            return false;
        }
    }
    [HarmonyPatch(typeof(CardManager), nameof(CardManager.CopyCardState))]
    static class CopyTemporaryCard
    {
        static void Postfix(CardState sourceCardState, CardState __result) => TemporaryMemory.Copy(sourceCardState, __result);
    }
    [HarmonyPatch(typeof(CharacterHelper), nameof(CharacterHelper.CopyCharacterStats))]
    static class CopyTemporaryUnit
    {
        static void Postfix(CharacterState copyToChar, CharacterState copyFromChar)
        {
            if (TemporaryMemory.Contains(copyFromChar)) TemporaryMemory.Mark(copyToChar);
        }
    }
}
