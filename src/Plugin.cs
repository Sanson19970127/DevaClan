using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Conductor.Extensions;
using Conductor.TrackedValues;
using Conductor.UI;
using TrainworksReloaded.Core;
using TrainworksReloaded.Core.Extensions;
using TrainworksReloaded.Core.Interfaces;
using TrainworksReloaded.Base;
using TrainworksReloaded.Base.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace DevaClan
{
    [BepInPlugin(Guid, "Deva Clan / 天人氏族", "0.2.9")]
    [BepInDependency("TrainworksReloaded.Plugin")]
    [BepInDependency("Conductor")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "sanson.DevaClan";
        internal static ManualLogSource Log;
        internal static ChargeResource Charge;
        internal static CardStatistics.TrackedValueType ChargeStat;
        internal static readonly Dictionary<string, CardData> Cards = new Dictionary<string, CardData>();
        internal static readonly Dictionary<string, CharacterData> Units = new Dictionary<string, CharacterData>();
        internal static readonly Dictionary<string, CardUpgradeData> Upgrades = new Dictionary<string, CardUpgradeData>();
        internal static readonly Dictionary<string, RelicData> Relics = new Dictionary<string, RelicData>();
        internal static readonly Dictionary<string, CharacterTriggerData> Triggers = new Dictionary<string, CharacterTriggerData>();
        internal static SubtypeData Construct;
        internal static string KarmaId;
        public void Awake()
        {
            Log = Logger;
            var root = Path.GetDirectoryName(Info.Location);
            var files = Directory.GetFiles(Path.Combine(root, "json"), "*.json", SearchOption.AllDirectories)
                .Select(p => p.Substring(root.Length + 1).Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (files.Length == 0) throw new InvalidOperationException("DevaClan content JSON is missing; install the complete package.");
            if (files.Length > 1 && files.Any(p => string.Equals(p, "json/content.json", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Old json/content.json and modular JSON coexist. Back up and replace the complete DevaClan folder, or run Install.cmd.");
            Railhead.GetBuilder().Configure(Guid, c => c.AddMergedJsonFile(files.ToList()));
            Railend.ConfigurePostAction(c =>
            {
                var cards = c.GetInstance<IRegister<CardData>>();
                foreach (string id in ContentIds.Cards) Cards[id] = cards.GetValueOrDefault(Guid.GetId(TemplateConstants.Card, id));
                var units = c.GetInstance<IRegister<CharacterData>>();
                foreach (string id in ContentIds.Units) Units[id] = units.GetValueOrDefault(Guid.GetId(TemplateConstants.Character, id));
                var upgrades = c.GetInstance<IRegister<CardUpgradeData>>();
                foreach (string id in ContentIds.Upgrades) Upgrades[id] = upgrades.GetValueOrDefault(Guid.GetId(TemplateConstants.Upgrade, id));
                var relics = c.GetInstance<IRegister<RelicData>>();
                foreach (string id in ContentIds.Relics) Relics[id] = relics.GetValueOrDefault(Guid.GetId(TemplateConstants.RelicData, id));
                var triggers = c.GetInstance<IRegister<CharacterTriggerData>>();
                foreach (string id in ContentIds.Triggers) Triggers[id] = triggers.GetValueOrDefault(Guid.GetId(TemplateConstants.CharacterTrigger, id));
                Construct = c.GetInstance<IRegister<SubtypeData>>().GetValueOrDefault(Guid.GetId(TemplateConstants.Subtype, "Construct"));
                KarmaId = c.GetInstance<IRegister<StatusEffectData>>().GetValueOrDefault(Guid.GetId(TemplateConstants.StatusEffect, "karmergy")).GetStatusId();
                ChargeStat = c.GetInstance<IRegister<CardStatistics.TrackedValueType>>().GetValueOrDefault(Guid.GetId(TemplateConstants.TrackedValueTypeEnum, "Charge"));
                Charge = new ChargeResource();
                ChargeStat.SetTrackedValueHandler(Charge);
                HudManager.GetHUD(Guid, "ChargeHud")?.SetTrackedValueHandler(Charge);
                int missing = Cards.Count(k => k.Value == null) + Units.Count(k => k.Value == null) + Upgrades.Count(k => k.Value == null) + Relics.Count(k => k.Value == null) + Triggers.Count(k => k.Value == null);
                Log.LogInfo($"DEVA VALIDATION: {Cards.Count} cards, {Units.Count} units, {Relics.Count} relics; missing registrations={missing}.");
                if (missing != 0) Log.LogError("Deva content registration incomplete.");
                Log.LogInfo($"DEVA LOCALIZATION: initialized translation storage for {LocalizationCompatibility.RepairedTerms} terms.");
            });
            new Harmony(Guid).PatchAll();
            Log.LogInfo("Deva Clan 0.2.9 loaded; gameplay bugfix candidate.");
        }
        internal static bool Has(ICoreGameManagers g, string id) => g != null && Relics.TryGetValue(id, out var data) && data != null && g.GetSaveManager().GetAllRelics().Any(r => r.GetRelicDataID() == data.GetID());
        internal static bool Is(CharacterState c, string id) => c != null && Units.TryGetValue(id, out var d) && c.GetSourceCharacterData() == d;
        internal static bool Is(CardState c, string id) => c != null && Cards.TryGetValue(id, out var d) && c.GetCardDataID() == d.GetID();
        internal static bool IsConstruct(CharacterState c) => c != null && Construct != null && c.GetHasSubtype(Construct);
        internal static bool IsConstruct(CardState c) => c?.GetSpawnCharacterData()?.GetSubtypes()?.Contains(Construct) == true;
        internal static int Level(CharacterState c, string path)
        {
            if (c == null) return 0;
            // Champion path upgrades live on the spawning card. They are not
            // necessarily present in the character's later applied-upgrade list.
            var card = c.GetSpawnerCard();
            for (int i = 3; i > 0; --i)
                if (Upgrades.TryGetValue(path + i, out var d) && (card?.HasUpgrade(d) == true || c.HasUpgrade(d))) return i;
            return 0;
        }
        internal static List<CharacterState> Floor(RoomState room, Team.Type team = Team.Type.Monsters)
        {
            var list = new List<CharacterState>();
            if (room != null) room.AddCharactersToList(list, team);
            return list.Where(c => c != null && c.IsAlive && !c.IsDestroyed).ToList();
        }
        internal static void Gain(ICoreGameManagers g, int n, CardState card = null)
        {
            if (Charge != null)
            {
                g.GetCardStatistics().IncrementStat(card, ChargeStat, n);
                if (!g.GetSaveManager().PreviewMode && !SaveManager.IsInUndoModeStatic)
                {
                    g.GetMonsterManager().RefreshCharacterAbilityUI();
                    var rooms = g.GetRoomManager();
                    var ui = rooms.GetRoomUI();
                    if (ui != null && rooms.IsValidRoomIndex(rooms.GetSelectedRoom()))
                        ui.RefreshTrainRoomAttachmentDisplay(rooms.GetRoom(rooms.GetSelectedRoom()));
                }
            }
        }
    }

    public sealed class ChargeHud : ClassMechanicHud
    {
        public override void Refresh(SaveManager saveManager, PlayerManager playerManager, CardManager cardManager)
        {
            // Replays suppress count animations. Rebind the visible number to
            // the authoritative resource when the game refreshes its HUD.
            if (Plugin.Charge != null) ShowCount(Plugin.Charge.GetCurrentValue());
        }
        protected override void HandleValueChanged(TrackedValueChangedParams changedParams)
        {
            if (changedParams.entryDuration != CardStatistics.EntryDuration.ThisTurn) return;
            if (SaveManager.IsInUndoModeStatic) ShowCount(changedParams.value);
            else base.HandleValueChanged(changedParams);
        }
    }
    public sealed class ChargeResource : SimpleGlobalTrackedValueHandler
    {
        // Current-turn storage deliberately persists across turns, but resets between battles.
        public override void UpdateStatsForNextTurn() { }
        public override void UpdateStatsForFirstTurn() { Reset(); }
        public override void Reset() { base.Reset(); BattleMemory.Clear(); }
        public override void OnCombatPreviewEnabled() { base.OnCombatPreviewEnabled(); BattleMemory.BeginPreview(); }
        public override void OnCombatPreviewDisabled() { base.OnCombatPreviewDisabled(); BattleMemory.EndPreview(); }
    }

    internal sealed class Memory
    {
        public Dictionary<CardState,int> Karma = new Dictionary<CardState,int>();
        public Dictionary<CardState,int> Discounts = new Dictionary<CardState,int>();
        public HashSet<CardState> Dead = new HashSet<CardState>();
        public int Abilities;
        public HashSet<int> Relentless = new HashSet<int>();
        public Dictionary<CardState,CardState> GiantOwners = new Dictionary<CardState,CardState>();
        public HashSet<CharacterState> Stasis = new HashSet<CharacterState>();
        public Dictionary<CharacterState,int> TurnSilence = new Dictionary<CharacterState,int>();
        public HashSet<CardState> TemporaryCards = new HashSet<CardState>();
        public HashSet<CharacterState> TemporaryUnits = new HashSet<CharacterState>();
        public Memory Clone() => new Memory { Karma = new Dictionary<CardState,int>(Karma), Discounts = new Dictionary<CardState,int>(Discounts), Dead = new HashSet<CardState>(Dead), Abilities = Abilities, Relentless = new HashSet<int>(Relentless), GiantOwners = new Dictionary<CardState,CardState>(GiantOwners), Stasis = new HashSet<CharacterState>(Stasis), TurnSilence = new Dictionary<CharacterState,int>(TurnSilence), TemporaryCards = new HashSet<CardState>(TemporaryCards), TemporaryUnits = new HashSet<CharacterState>(TemporaryUnits) };
    }
    internal static class BattleMemory
    {
        static Memory actual = new Memory(), preview = new Memory();
        static bool isPreview;
        internal static Memory Active => isPreview ? preview : actual;
        internal static void Clear() { actual = new Memory(); preview = new Memory(); isPreview = false; }
        internal static void BeginPreview() { preview = actual.Clone(); isPreview = true; }
        internal static void EndPreview() { preview = actual.Clone(); isPreview = false; }
        internal static void Remember(CharacterState unit)
        {
            TemporaryMemory.Remember(unit);
            var card = unit?.GetSpawnerCard();
            if (card != null && Plugin.KarmaId != null) Active.Karma[card] = unit.GetStatusEffectStacks(Plugin.KarmaId);
        }
    }

    public sealed class StatusEffectKarmergy : StatusEffectState
    {
        int AttackPerStack(ICoreGameManagers g) => 10 + (Plugin.Has(g, "KarmicIncense") ? 5 : 0);
        public override void OnStacksAdded(CharacterState c, int n, CharacterState.AddStatusEffectParams p, ICoreGameManagers g)
        {
            c.BuffDamage(n * AttackPerStack(g), null, true);
            c.SetHealth(c.GetHP() + n * 10, c.GetMaxHP() + n * 10);
        }
        public override void OnStacksRemoved(CharacterState c, int n, ICoreGameManagers g)
        {
            c.DebuffDamage(n * AttackPerStack(g), null, true);
            int max = Math.Max(1, c.GetMaxHP() - n * 10);
            c.SetHealth(Math.Min(max, Math.Max(c.IsAlive ? 1 : 0, c.GetHP() - n * 10)), max);
        }
        public override int GetEffectMagnitude(int stacks = 1) => stacks * 10;
    }
    public sealed class RelicEffectDevaMarker : RelicEffectBase
    {
        public override PropDescriptions CreateEditorInspectorDescriptions() => new PropDescriptions();
    }

    [HarmonyPatch(typeof(CardManager), nameof(CardManager.OnCharacterKilled))]
    static class DeathMemory
    {
        static void Prefix(CharacterState killedUnit, ICoreGameManagers coreGameManagers)
        {
            if (killedUnit.GetTeamType() != Team.Type.Monsters) return;
            BattleMemory.Remember(killedUnit);
            var card = killedUnit.GetSpawnerCard();
            if (card != null && !killedUnit.HasStatusEffect("cardless")) BattleMemory.Active.Dead.Add(card);
            if (Plugin.IsConstruct(killedUnit) && Plugin.Has(coreGameManagers, "ChargeRecycler")) Plugin.Gain(coreGameManagers, 1);
        }
    }
    [HarmonyPatch(typeof(CardManager), nameof(CardManager.ReturnUnitCardToHand))]
    static class ReturnMemory
    {
        static void Prefix(CharacterState character) => BattleMemory.Remember(character);
    }
    [HarmonyPatch(typeof(CharacterState), nameof(CharacterState.OnSpawn))]
    static class SpawnMemory
    {
        static IEnumerator Postfix(IEnumerator result, CharacterState __instance, AllGameManagers ___allGameManagers)
        {
            var g = ___allGameManagers.GetCoreManagers();
            if (__instance.GetTeamType() == Team.Type.Monsters && Plugin.KarmaId != null)
            {
                var card = __instance.GetSpawnerCard();
                if (card != null && BattleMemory.Active.Karma.TryGetValue(card, out int n) && n > 0) __instance.AddStatusEffect(Plugin.KarmaId, n, (CharacterState)null, allowModification: false);
            }
            TemporaryMemory.Restore(__instance);
            while (result.MoveNext()) yield return result.Current;
        }
    }
    [HarmonyPatch(typeof(CombatManager), "RunMonsterTurn")]
    static class TurnStart
    {
        static IEnumerator Postfix(IEnumerator result, bool isFirstTurn, AllGameManagers ___allGameManagers)
        {
            var g = ___allGameManagers.GetCoreManagers();
            BattleMemory.Active.Karma.Clear();
            BattleMemory.Active.Relentless.Clear();
            foreach (var item in BattleMemory.Active.TurnSilence.ToArray())
            {
                var character = item.Key;
                if (character != null && !character.IsDestroyed && character.IsAlive)
                {
                    int current = character.GetStatusEffectStacks("silenced");
                    if (current > 0) character.RemoveStatusEffect("silenced", Math.Min(current, item.Value), false);
                }
            }
            BattleMemory.Active.TurnSilence.Clear();
            foreach(var c in BattleMemory.Active.Stasis) if(c != null && !c.IsDestroyed && c.IsAlive) c.RemoveStatusEffect("untouchable",1,false);
            BattleMemory.Active.Stasis.Clear();
            for (int i = 0; i < g.GetRoomManager().GetNumRooms(); ++i)
            {
                var units = Plugin.Floor(g.GetRoomManager().GetRoom(i), Team.Type.Monsters | Team.Type.Heroes);
                int retain = units.Where(c => c.GetTeamType() == Team.Type.Monsters && !c.HasStatusEffect("silenced")).Select(c => Plugin.Level(c, "MaraLiberation")).DefaultIfEmpty(0).Max();
                foreach (var c in units)
                {
                    int n = c.GetStatusEffectStacks(Plugin.KarmaId);
                    int keep = c.GetTeamType() == Team.Type.Monsters ? (retain == 3 ? n : retain > 0 ? n / 2 : 0) : 0;
                    if (n > keep) c.RemoveStatusEffect(Plugin.KarmaId, n - keep, false);
                    if (TemporaryMemory.Contains(c)) yield return c.Sacrifice(null);
                }
            }
            if (isFirstTurn && Plugin.Has(g, "ReserveEnergy")) Plugin.Gain(g, 5);
            while (result.MoveNext()) yield return result.Current;
        }
    }
    [HarmonyPatch(typeof(CharacterState), nameof(CharacterState.GetTriggerFireCount))]
    static class DoubleEvoke
    {
        static void Postfix(CharacterState __instance, CharacterTriggerData.Trigger trigger, AllGameManagers ___allGameManagers, ref int __result)
        {
            if (__instance.GetTeamType() == Team.Type.Monsters && trigger == Conductor.Triggers.CharacterTriggers.Evoke && Plugin.Has(___allGameManagers.GetCoreManagers(), "SyncMatrix")) __result++;
        }
    }
    [HarmonyPatch(typeof(CardManager), nameof(CardManager.FireUnitTriggersForCardPlayed))]
    [HarmonyAfter("Conductor")]
    static class AbilityEvents
    {
        static IEnumerator Postfix(IEnumerator result, ICharacterManager characterManager, CardState playedCard, CharacterState characterThatActivatedAbility, int playedRoomIndex, AllGameManagers ___allGameManagers)
        {
            var g = ___allGameManagers.GetCoreManagers();
            while (result.MoveNext()) yield return result.Current;
            if (!ReferenceEquals(characterManager, g.GetMonsterManager())) yield break;
            if (Plugin.Is(playedCard, "SixRealms")) BattleMemory.Active.Discounts.Remove(playedCard);
            if (!playedCard.IsAnyAbility()) yield break;
            if (characterThatActivatedAbility != null && characterThatActivatedAbility.GetTeamType() != Team.Type.Monsters) yield break;
            var memory = BattleMemory.Active;
            foreach (var card in g.GetCardManager().GetHand())
                if (Plugin.Is(card, "SixRealms")) { memory.Discounts.TryGetValue(card, out int old); memory.Discounts[card] = old + 1; }
            g.GetCardManager().RefreshHandCards();
            if (characterThatActivatedAbility != null)
            {
                memory.Abilities++;
                yield return SingingBowl.AfterAbility(playedCard, characterThatActivatedAbility, Plugin.Has(g, "SingingBowl"));
                if (Plugin.Has(g, "PrayerWheel") && memory.Abilities % 3 == 0) g.GetCardManager().DrawCards(1);
                if (Plugin.Has(g, "InsightCircuit") && RandomManager.Range(0, 4, g.GetSaveManager().PreviewMode ? RngId.BattleTest : RngId.Battle) == 0)
                    characterThatActivatedAbility.RemoveStatusEffect("cooldown", 999, false);
            }
            else
            {
                // Conductor's Evoke handles unit abilities. Extend it to room abilities too.
                foreach (var c in Plugin.Floor(g.GetRoomManager().GetRoom(playedRoomIndex)))
                    yield return g.GetCombatManager().QueueAndRunTrigger(c, Conductor.Triggers.CharacterTriggers.Evoke);
            }
        }
    }
    [HarmonyPatch(typeof(CardState), nameof(CardState.GetCost))]
    static class SixRealmsCost
    {
        static void Postfix(CardState __instance, ref int __result)
        {
            if (Plugin.Is(__instance, "SixRealms") && BattleMemory.Active.Discounts.TryGetValue(__instance, out int n)) __result = Math.Max(0, __result - n);
        }
    }
}
