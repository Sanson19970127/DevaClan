using System;
using System.Collections;
using System.Linq;

namespace DevaClan
{
    // The installed API has no remove_after_combat_ends field. Keep the
    // existing next-player-turn lifetime until a native equivalent is verified.
    public sealed class CardEffectTurnSilence : DevaEffectBase
    {
        public override IEnumerator ApplyEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g, ISystemManagers sys)
        {
            foreach (var target in Targets(p).Where(t => t.GetTeamType() == Team.Type.Heroes))
            {
                int before = target.GetStatusEffectStacks("silenced");
                AddStatus(target, StatusId(e, "silenced"), StatusCount(e, e.GetParamInt()), e, p);
                int added = Math.Max(0, target.GetStatusEffectStacks("silenced") - before);
                if (added > 0)
                    BattleMemory.Active.TurnSilence[target] = BattleMemory.Active.TurnSilence.TryGetValue(target, out var old) ? old + added : added;
            }
            yield break;
        }
    }
    public sealed class CardEffectTurnStasis : DevaEffectBase
    {
        public override IEnumerator ApplyEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g, ISystemManagers sys)
        {
            foreach (var target in Targets(p))
            {
                target.AddStatusEffect("untouchable", 1);
                BattleMemory.Active.Stasis.Add(target);
            }
            yield break;
        }
    }
}
