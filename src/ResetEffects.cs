using System.Collections;
using System.Linq;

namespace DevaClan
{
    public class CardEffectResetUnit : DevaTargetEffect
    {
        public override IEnumerator ApplyEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g, ISystemManagers sys)
        {
            foreach (var target in Targets(p).Where(t => TestEffectOnTarget(e, p, t, g))) yield return Effects.Reset(target, p, g);
        }
    }
    public sealed class CardEffectResetConstruct : CardEffectResetUnit
    {
        public override bool TestEffectOnTarget(CardEffectState e, CardEffectParams p, CharacterState target, ICoreGameManagers g) =>
            Alive(target) && target.GetTeamType() == Team.Type.Monsters && Plugin.IsConstruct(target);
    }
    public sealed class CardEffectRitualReset : CardEffectResetUnit
    {
        public override bool TestEffectOnTarget(CardEffectState e, CardEffectParams p, CharacterState target, ICoreGameManagers g) =>
            Alive(target) && target.GetTeamType() == Team.Type.Monsters && !Plugin.Is(target, "RitualTechnician");
    }
    public sealed class CardEffectResetFloor : DevaEffectBase
    {
        public override IEnumerator ApplyEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g, ISystemManagers sys)
        { foreach (var target in Plugin.Floor(Room(p, g))) yield return Effects.Reset(target, p, g); }
    }
    public sealed class CardEffectImproveGiant : DevaTargetEffect
    {
        public override bool TestEffectOnTarget(CardEffectState e, CardEffectParams p, CharacterState target, ICoreGameManagers g) =>
            Alive(target) && Plugin.Is(target, "Giant");
        public override IEnumerator ApplyEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g, ISystemManagers sys)
        {
            int level = e.GetParamInt();
            foreach (var target in Targets(p).Where(t => Plugin.Is(t, "Giant")))
            {
                var oldCard = target.GetSpawnerCard();
                CardState owner = null;
                if (oldCard != null) BattleMemory.Active.GiantOwners.TryGetValue(oldCard, out owner);
                owner = owner ?? Actor(p)?.GetSpawnerCard();
                CharacterState replacement = null;
                yield return Effects.Reset(target, p, g, c => replacement = c);
                if (replacement == null) continue;
                yield return Effects.Grow(replacement, level == 3 ? 2 : 1, level == 1 ? 1 : 2, g, true);
                if (owner != null && !g.GetSaveManager().PreviewMode)
                {
                    var marker = new CardUpgradeState(); marker.Setup(Plugin.Upgrades["GiantGrowth" + level]);
                    owner.GetCardStateModifiers().AddUpgrade(marker);
                }
            }
        }
    }
    public sealed class CardEffectLiberation : DevaTargetEffect
    {
        public override IEnumerator ApplyEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g, ISystemManagers sys)
        {
            foreach (var target in Targets(p))
            {
                int damage = target.GetAttackDamage(); var floor = target.GetCurrentRoom();
                yield return target.Sacrifice(p.playedCard);
                foreach (var enemy in Plugin.Floor(floor, Team.Type.Heroes)) yield return Effects.Damage(enemy, damage, p, g);
            }
        }
    }
    public sealed class CardEffectConvertKarmergy : DevaTargetEffect
    {
        public override IEnumerator ApplyEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g, ISystemManagers sys)
        {
            foreach (var target in Targets(p))
            {
                int stacks = target.GetStatusEffectStacks(Plugin.KarmaId);
                target.RemoveStatusEffect(Plugin.KarmaId, stacks, false);
                yield return Effects.Grow(target, stacks * e.GetParamInt(), stacks * e.GetParamInt(), g, true);
            }
        }
    }
}
