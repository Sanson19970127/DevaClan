using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DevaClan
{
    public sealed class CardEffectCreateConstruct : DevaEffectBase
    {
        public override bool CanPlayWhenHandFull => false;
        public override IEnumerator ApplyEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g, ISystemManagers sys)
        { Effects.CreateConstruct(g, false); yield break; }
    }
    public sealed class CardEffectFormatConstruct : DevaTargetEffect
    {
        public override bool TestEffectOnTarget(CardEffectState e, CardEffectParams p, CharacterState target, ICoreGameManagers g) =>
            Alive(target) && target.GetTeamType() == Team.Type.Monsters && Plugin.IsConstruct(target);
        public override IEnumerator ApplyEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g, ISystemManagers sys)
        {
            foreach (var target in Targets(p).Where(Plugin.IsConstruct))
            {
                yield return target.Sacrifice(p.playedCard);
                Plugin.Gain(g, e.GetParamInt(), p.playedCard);
                if (!g.GetSaveManager().PreviewMode) g.GetCardManager().DrawCards(1, p.playedCard);
            }
        }
    }
    public sealed class CardEffectPerfectCreation : DevaEffectBase
    {
        public override bool TestEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g) => CanSpawn(p, g);
        public override IEnumerator ApplyEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g, ISystemManagers sys)
        { yield return Effects.Spawn("Perfect" + e.GetParamInt(), Room(p, g), p, g, true, Actor(p)); }
        public override void GetTooltipsStatusList(CardEffectState e, ref List<string> ids)
        { base.GetTooltipsStatusList(e, ref ids); if (!ids.Contains(Effects.TemporaryId)) ids.Add(Effects.TemporaryId); }
    }
    public sealed class CardEffectGiantCreation : DevaEffectBase
    {
        public override bool TestEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g) => CanSpawn(p, g);
        public override IEnumerator ApplyEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g, ISystemManagers sys)
        { yield return Effects.Spawn("Giant", Room(p, g), p, g, false, Actor(p)); }
    }
    public sealed class CardEffectFirewallCreation : DevaEffectBase
    {
        public override bool TestEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g) => CanSpawn(p, g);
        public override IEnumerator ApplyEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g, ISystemManagers sys)
        { yield return Effects.Spawn("Firewall", Room(p, g), p, g, true); }
        public override void GetTooltipsStatusList(CardEffectState e, ref List<string> ids)
        { base.GetTooltipsStatusList(e, ref ids); if (!ids.Contains(Effects.TemporaryId)) ids.Add(Effects.TemporaryId); }
    }
    public sealed class CardEffectHologram : DevaTargetEffect
    {
        public override bool TestEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g) =>
            CanSpawn(p, g) && base.TestEffect(e, p, g);
        public override IEnumerator ApplyEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g, ISystemManagers sys)
        {
            foreach (var target in Targets(p))
            {
                CharacterState clone = null;
                yield return g.GetMonsterManager().CloneMonsterState(target, target.GetCurrentRoomIndex(), c => clone = c, g, playedCard: p.playedCard);
                if (Alive(clone)) TemporaryMemory.Mark(clone);
            }
        }
        public override void GetTooltipsStatusList(CardEffectState e, ref List<string> ids)
        { base.GetTooltipsStatusList(e, ref ids); if (!ids.Contains(Effects.TemporaryId)) ids.Add(Effects.TemporaryId); }
    }
}
