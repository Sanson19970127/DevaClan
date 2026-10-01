using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShinyShoe;

namespace DevaClan
{
    internal static class Effects
    {
        internal static string TemporaryId => Plugin.Guid.ToLowerInvariant() + "_temporary";
        internal static void CreateConstruct(ICoreGameManagers g, bool ephemeral)
        {
            if (g.GetSaveManager().PreviewMode) return;
            var pool = ContentIds.RandomConstructs;
            var data = Plugin.Cards[pool[RandomManager.Range(0, pool.Length, RngId.Battle)]];
            var info = new CardManager.AddCardUpgradingInfo { tempCardUpgrade = true };
            if (ephemeral) info.upgradeDatas.Add(Plugin.Upgrades["EphemeralUpgrade"]);
            g.GetCardManager().AddNewCardWithSpecialPlacement(data, CardPile.HandPile, ephemeral, false, 0, 1, info);
        }
        internal static IEnumerator Attack(CharacterState c, ICoreGameManagers g, CharacterState enemy = null)
        {
            if (c == null || !c.IsAlive || c.IsDestroyed || (enemy != null && (!enemy.IsAlive || enemy.IsDestroyed))) yield break;
            yield return g.GetCombatManager().RunUnitTurn(c, g.GetMonsterManager(), g.GetHeroManager(), c.GetCurrentRoomIndex(), false,
                overrideTargets: enemy == null ? null : new List<CharacterState> { enemy }, ignoreAttackPrevention: true);
            if (!g.GetSaveManager().PreviewMode && c != null && !c.IsDestroyed)
            {
                var info = new TargetHelper.AttackInfo();
                c.GetCurrentRoom()?.AddCharactersToList(info.othersInRoom, Team.Type.Heroes | Team.Type.Monsters);
                yield return g.GetCombatManager().DoForegroundMoveAnimations(info);
            }
        }
        internal static IEnumerator Damage(CharacterState c, int n, CardEffectParams p, ICoreGameManagers g)
        {
            if (c == null || !c.IsAlive || c.IsDestroyed) yield break;
            yield return g.GetCombatManager().ApplyDamageToTarget(n, c, new CombatManager.ApplyDamageToTargetParameters {
                playedCard = p.playedCard, selfTarget = p.selfTarget, finalEffectInSequence = true });
        }
        internal static IEnumerator Grow(CharacterState c, int attack, int health, ICoreGameManagers g, bool permanent)
        {
            var up = new CardUpgradeState();
            up.Setup(Plugin.Upgrades["StatGrowth"]);
            up.SetSerializeScalingValues(true);
            up.SetAttackDamage(attack); up.SetAdditionalHP(health);
            yield return c.ApplyCardUpgrade(up);
            var card = c.GetSpawnerCard();
            if (permanent && card != null && !g.GetSaveManager().PreviewMode) card.GetCardStateModifiers().AddUpgrade(up);
        }
        internal static void Standby(CardState card, CharacterState c, ICoreGameManagers g)
        {
            if (card == null || c.HasStatusEffect("cardless") || g.GetSaveManager().PreviewMode) return;
            var cm = g.GetCardManager();
            cm.RestoreExhaustedOrEatenCard(card);
            var condition = new RemoveFromStandByCondition(() => cm.CheckMonsterRemoveFromStandByCondition(c));
            if (cm.IsCardInStandByPile(card)) cm.ChangeStandbyCondition(card, condition);
            else cm.MoveToStandByPile(card, false, false, condition);
        }
        internal static IEnumerator Reset(CharacterState c, CardEffectParams p, ICoreGameManagers g, Action<CharacterState> done = null)
        {
            if (c == null || !c.IsAlive || c.IsDestroyed || c.GetTeamType() != Team.Type.Monsters) yield break;
            TemporaryMemory.Remember(c);
            bool temporary = TemporaryMemory.Contains(c);
            var room = c.GetCurrentRoom();
            int index = c.GetSpawnPoint().GetIndexInRoom();
            var data = c.GetSourceCharacterData();
            var card = c.GetSpawnerCard();
            int karma = c.GetStatusEffectStacks(Plugin.KarmaId);
            if (Plugin.Is(c, "Sinner")) karma *= 2;
            bool construct = Plugin.IsConstruct(c);
            var backup = c.GetEquipment().FirstOrDefault(e => Plugin.Is(e, "SoulBackup"));
            var positive = new List<CharacterState.StatusEffectStack>();
            if (backup != null) c.GetStatusEffects(ref positive);
            var saved = positive.Where(s => s.Count > 0 && s.State.GetDisplayCategory() == StatusEffectData.DisplayCategory.Positive && s.State.GetStatusId() != Plugin.KarmaId)
                .Select(s => new KeyValuePair<string,int>(s.State.GetStatusId(), s.Count)).ToArray();
            // Reset is a replacement, even when the target normally revives through Undying.
            c.RemoveStatusEffect("undying", -1, false);
            yield return c.Sacrifice(p.playedCard);
            if (card != null) BattleMemory.Active.Karma[card] = karma;
            CharacterState replacement = null;
            yield return g.GetMonsterManager().CreateMonsterState(data, card, room.GetRoomIndex(), n => replacement = n,
                SpawnMode.SelectedSlot, room.GetMonsterPoint(Math.Min(index, g.GetSaveManager().GetNumSpawnPointsPerFloor(Team.Type.Monsters) - 1)));
            if (replacement == null) yield break;
            if (temporary) TemporaryMemory.Mark(replacement);
            Standby(card, replacement, g);
            if (card == null && karma > 0) replacement.AddStatusEffect(Plugin.KarmaId, karma, (CharacterState)null, allowModification: false);
            if (backup != null)
            {
                yield return replacement.AddEquipment(backup, g, false);
                if (!g.GetSaveManager().PreviewMode)
                {
                    var cm = g.GetCardManager();
                    cm.RestoreExhaustedOrEatenCard(backup);
                    var condition = new RemoveFromStandByCondition(() => replacement.IsAlive && !replacement.IsDestroyed && replacement.GetEquipment().Contains(backup) ? CardPile.KeepInStandBy : CardPile.DiscardPile);
                    if (cm.IsCardInStandByPile(backup)) cm.ChangeStandbyCondition(backup, condition);
                    else cm.MoveToStandByPile(backup, false, false, condition);
                }
                foreach (var s in saved)
                {
                    int add = s.Value - replacement.GetStatusEffectStacks(s.Key);
                    if (add > 0) replacement.AddStatusEffect(s.Key, add, (CharacterState)null, allowModification: false);
                }
            }
            if (construct && Plugin.Has(g, "PrajnaShield")) replacement.AddStatusEffect("damage shield", 1);
            done?.Invoke(replacement);
        }
        internal static IEnumerator Spawn(string id, RoomState room, CardEffectParams p, ICoreGameManagers g, bool temporary, CharacterState owner = null)
        {
            if (room == null || room.GetRemainingSpawnPointCount(Team.Type.Monsters) <= 0) yield break;
            // Tokens retain an internal state for karma/reset/giant upgrades,
            // but never enter any card pile. Their native cardless status is
            // part of CharacterData and is also restored on reconstruction.
            var card = new CardState();
            card.Setup(Plugin.Cards[id], g.GetSaveManager());
            if (id == "Giant" && owner != null)
            {
                var ownerCard = owner.GetSpawnerCard();
                foreach (var u in ownerCard?.GetCardStateModifiers().GetCardUpgrades() ?? new List<CardUpgradeState>())
                    for (int level = 1; level <= 3; level++)
                        if (u.GetCardUpgradeDataId() == Plugin.Upgrades["GiantGrowth" + level].GetID())
                        {
                            var grow = new CardUpgradeState(); grow.Setup(Plugin.Upgrades["StatGrowth"]);
                            grow.SetSerializeScalingValues(true);
                            grow.SetAttackDamage(level == 3 ? 2 : 1); grow.SetAdditionalHP(level == 1 ? 1 : 2);
                            card.GetCardStateModifiers().AddUpgrade(grow);
                        }
                BattleMemory.Active.GiantOwners[card] = ownerCard;
            }
            CharacterState spawned = null;
            // FrontSlot means first EMPTY slot, which can be behind the caster.
            // SelectedSlot inserts at the occupied slot and shifts its occupant back.
            var insertion = (id.StartsWith("Perfect", StringComparison.Ordinal) || id == "Giant") && owner != null
                ? owner.GetSpawnPoint() : id == "Firewall" ? room.GetMonsterPoint(0) : null;
            yield return g.GetMonsterManager().CreateMonsterState(Plugin.Units[id], card, room.GetRoomIndex(), c => spawned = c,
                insertion != null ? SpawnMode.SelectedSlot : SpawnMode.FrontSlot, insertion,
                afterCharacterSetup: c => { if (temporary) TemporaryMemory.Mark(c); });
            if (spawned != null) Standby(card, spawned, g);
        }
    }

    public sealed class StatusEffectTemporary : StatusEffectState
    {
        public override void OnStacksAdded(CharacterState c, int n, CharacterState.AddStatusEffectParams p, ICoreGameManagers g)
        {
            if (n > 0) TemporaryMemory.Remember(c, true);
        }
    }

}
