using System.Collections;
using System.Linq;
using HarmonyLib;

namespace DevaClan
{
    internal static class AsuraArenaFilter
    {
        internal static bool Excludes(TrainRoomAttachmentState attachment, CharacterState character) =>
            Plugin.Is(attachment?.CardState, "AsuraArena") && character != null
            && (character.IsAnyBoss() || character.IsCompanionBoss());
        internal static bool ArenaRelentlessOnly(CharacterState character)
        {
            if (character == null || character.IsAnyBoss() || character.IsCompanionBoss()
                || !Plugin.Upgrades.TryGetValue("AsuraArenaUpgrade", out var upgrade) || !character.HasUpgrade(upgrade)) return false;
            // Preserve an enemy's own Relentless when it was not supplied by us.
            var data = character.GetSourceCharacterData();
            return data == null || data.GetStartingStatusEffects()?.Any(s => s.statusId == "relentless" && s.count > 0) != true;
        }
    }
    [HarmonyPatch(typeof(CharacterState), nameof(CharacterState.CanDestroyRoom))]
    static class AsuraArenaNoRoomDestruction
    {
        static void Postfix(CharacterState __instance, ref bool __result)
        {
            if (__result && AsuraArenaFilter.ArenaRelentlessOnly(__instance)) __result = false;
        }
    }
    [HarmonyPatch(typeof(TrainRoomAttachmentState), nameof(TrainRoomAttachmentState.ShouldIgnoreCharacter))]
    static class AsuraArenaIgnoreBosses
    {
        static void Postfix(TrainRoomAttachmentState __instance, CharacterState characterState, ref bool __result)
        {
            if (AsuraArenaFilter.Excludes(__instance, characterState)) __result = true;
        }
    }
    // Guard direct callers too, without stripping a boss's native Relentless.
    [HarmonyPatch(typeof(TrainRoomAttachmentState), nameof(TrainRoomAttachmentState.ApplyToCharacter))]
    static class AsuraArenaApplyGuard
    {
        static IEnumerator Postfix(IEnumerator result, TrainRoomAttachmentState __instance, CharacterState characterState)
        {
            if (AsuraArenaFilter.Excludes(__instance, characterState)) yield break;
            while (result.MoveNext()) yield return result.Current;
        }
    }
    public sealed class RelicEffectBlueprint : RelicEffectBase, ITurnPhaseStartOfPlayerTurnAfterDrawRelicEffect
    {
        public override PropDescriptions CreateEditorInspectorDescriptions() => new PropDescriptions();
        public bool TestEffectTurnPhaseTiming(RelicEffectParams p, ICoreGameManagers g) => true;
        public IEnumerator ApplyEffectTurnPhaseTiming(RelicEffectParams p, ICoreGameManagers g) { Effects.CreateConstruct(g,true); yield break; }
    }
    [HarmonyPatch(typeof(CombatManager), "CheckTriggerRelentless")]
    static class AsuraMark
    {
        static IEnumerator Postfix(IEnumerator result, RoomState room, AllGameManagers ___allGameManagers)
        {
            var g=___allGameManagers.GetCoreManagers();
            var all=Plugin.Floor(room,Team.Type.Heroes|Team.Type.Monsters);
            if(Plugin.Has(g,"AsuraMark") && all.Any(c=>c.HasStatusEffect("relentless")) && room.HasCharacters(Team.Type.Monsters) && (room.HasCharacters(Team.Type.Heroes)||room.HasOuterTrainBoss()) && BattleMemory.Active.Relentless.Add(room.GetRoomIndex()))
                foreach(var c in Plugin.Floor(room)) c.AddStatusEffect(Plugin.KarmaId,2);
            while(result.MoveNext()) yield return result.Current;
        }
    }
}
