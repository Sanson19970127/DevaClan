using System.Collections;

namespace DevaClan
{
    public sealed class CardEffectGainCharge : DevaEffectBase
    {
        public override IEnumerator ApplyEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g, ISystemManagers sys)
        {
            Plugin.Gain(g, e.GetParamInt(), p.playedCard);
            yield break;
        }
    }

    public sealed class CardEffectDevaSpike : DevaEffectBase
    {
        public override IEnumerator ApplyEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g, ISystemManagers sys)
        {
            int spent = p.playedCard.GetLastPlayedCost();
            foreach (var target in Targets(p)) AddStatus(target, Plugin.KarmaId, StatusCount(e, 2) * spent, e, p);
            Plugin.Gain(g, spent, p.playedCard);
            yield break;
        }
    }
}
