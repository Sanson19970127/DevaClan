using System;
using System.Collections.Generic;
using System.Linq;

namespace DevaClan
{
    // Shared plumbing only. Each concrete effect owns one behavior; JSON no
    // longer chooses an operation by passing a string to a universal effect.
    public abstract class DevaEffectBase : CardEffectBase
    {
        public override PropDescriptions CreateEditorInspectorDescriptions() => new PropDescriptions();
        protected static bool Alive(CharacterState c) => c != null && c.IsAlive && !c.IsDestroyed;
        protected static CharacterState Actor(CardEffectParams p) => p.characterThatActivatedAbility ?? p.selfTarget;
        protected static IEnumerable<CharacterState> Targets(CardEffectParams p) => p.targets.Where(Alive).ToArray();
        protected static RoomState Room(CardEffectParams p, ICoreGameManagers g) =>
            Actor(p)?.GetCurrentRoom() ?? p.GetSelectedRoom(g.GetRoomManager());
        protected static bool CanSpawn(CardEffectParams p, ICoreGameManagers g)
        {
            var room = Room(p, g);
            return room != null && !room.GetIsPyreRoom() && room.GetRemainingSpawnPointCount(Team.Type.Monsters) > 0;
        }
        protected static int StatusCount(CardEffectState e, int fallback, int index = 0)
        {
            var stacks = e.GetParamStatusEffectStackData();
            return stacks != null && stacks.Length > index ? stacks[index].count : fallback;
        }
        protected static string StatusId(CardEffectState e, string fallback, int index = 0)
        {
            var stacks = e.GetParamStatusEffectStackData();
            return stacks != null && stacks.Length > index && !string.IsNullOrEmpty(stacks[index].statusId)
                ? stacks[index].statusId : fallback;
        }
        protected void AddStatus(CharacterState target, string id, int count, CardEffectState e, CardEffectParams p)
        {
            if (!Alive(target) || count == 0) return;
            var add = new CharacterState.AddStatusEffectParams {
                sourceCardState = p.playedCard, sourceRelicState = p.sourceRelic,
                sourceIsHero = e.GetSourceTeamType() == Team.Type.Heroes, fromEffectType = GetType()
            };
            target.AddStatusEffect(id, count, add, Actor(p), allowModification: true, isFromHiddenTrigger: p.isFromHiddenTrigger);
        }
        public override bool TestEffectOnTarget(CardEffectState e, CardEffectParams p, CharacterState target, ICoreGameManagers g) => Alive(target);
        public override void GetTooltipsStatusList(CardEffectState e, ref List<string> ids)
        {
            var stacks = e.GetParamStatusEffectStackData();
            if (stacks == null) return;
            foreach (var stack in stacks)
                if (!string.IsNullOrEmpty(stack.statusId) && !ids.Contains(stack.statusId)) ids.Add(stack.statusId);
        }
    }

    public abstract class DevaTargetEffect : DevaEffectBase
    {
        public override bool TestEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g) =>
            p.targets.Any(t => TestEffectOnTarget(e, p, t, g));
    }

    // Preserve the ability's special targeting, but use native status application
    // (including native modifiers and tooltips) rather than reimplementing it.
    public sealed class CardEffectOtherAllyStatus : CardEffectAddStatusEffect
    {
        public override bool TestEffectOnTarget(CardEffectState e, CardEffectParams p, CharacterState target, ICoreGameManagers g) =>
            target != null && target.IsAlive && !target.IsDestroyed && target.GetTeamType() == Team.Type.Monsters
            && p.characterThatActivatedAbility != null && target != p.characterThatActivatedAbility
            && target.GetCurrentRoomIndex() == p.characterThatActivatedAbility.GetCurrentRoomIndex();
        public override bool TestEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g) =>
            p.targets.Any(t => TestEffectOnTarget(e, p, t, g));
    }
}
