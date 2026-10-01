using System.Collections;
using System.Linq;

namespace DevaClan
{
    public sealed class CardEffectImmediateAttack : DevaEffectBase
    {
        public override IEnumerator ApplyEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g, ISystemManagers sys)
        { yield return Effects.Attack(Actor(p), g); }
    }
    public sealed class CardEffectDirectedAttack : DevaTargetEffect
    {
        public override bool TestEffectOnTarget(CardEffectState e, CardEffectParams p, CharacterState target, ICoreGameManagers g) =>
            Alive(target) && target.GetTeamType() == Team.Type.Heroes
            && target.GetCurrentRoomIndex() == p.characterThatActivatedAbility?.GetCurrentRoomIndex();
        public override IEnumerator ApplyEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g, ISystemManagers sys)
        {
            foreach (var target in Targets(p)) yield return Effects.Attack(Actor(p), g, target);
        }
    }
    public sealed class CardEffectFrontAllyAttack : DevaEffectBase
    {
        public override IEnumerator ApplyEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g, ISystemManagers sys)
        { yield return Effects.Attack(Plugin.Floor(Room(p, g)).FirstOrDefault(), g); }
    }
    public sealed class CardEffectIdatenRecall : DevaEffectBase
    {
        public override bool CanPlayWhenHandFull => false;
        public override IEnumerator ApplyEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g, ISystemManagers sys)
        {
            var self = Actor(p);
            yield return Effects.Attack(self, g);
            // Native recall mutates actual piles and must not run in preview.
            if (!g.GetSaveManager().PreviewMode && Alive(self) && !self.HasStatusEffect("cardless"))
            {
                var cm = g.GetCardManager();
                var card = self.GetSpawnerCard();
                // Blueprint cards are Ephemeral: vanilla purges their card on
                // play even though the spawned unit remains alive. Recover that
                // same instance before invoking native recall; do not make a copy.
                if (card != null && cm.GetBelowHandSize() && !cm.IsCardInStandByPile(card) && !cm.GetHand().Contains(card)
                    && card.HasTrait<CardTraitEphemeral>() && cm.GetPurgedPile().Contains(card))
                {
                    cm.GetPurgedPile().RemoveAll(c => ReferenceEquals(c, card));
                    cm.MoveToStandByPile(card, false, false,
                        new RemoveFromStandByCondition(() => cm.CheckMonsterRemoveFromStandByCondition(self)),
                        wasRestoredExhaustedOrEaten: true);
                }
                if (card != null && cm.IsCardInStandByPile(card) && !cm.GetHand().Contains(card))
                    yield return cm.ReturnUnitCardToHand(self, true, false, false);
            }
        }
    }
    public sealed class CardEffectVajraGlare : DevaEffectBase
    {
        public override IEnumerator ApplyEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g, ISystemManagers sys)
        {
            var self = Actor(p);
            if (!Alive(self)) yield break;
            foreach (var target in Plugin.Floor(Room(p, g), Team.Type.Heroes))
                yield return Effects.Damage(target, self.GetAttackDamage(), p, g);
        }
    }
    public sealed class CardEffectFloorFocusAttack : DevaTargetEffect
    {
        public override IEnumerator ApplyEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g, ISystemManagers sys)
        {
            foreach (var target in Targets(p))
                foreach (var ally in Plugin.Floor(target.GetCurrentRoom())) yield return Effects.Attack(ally, g, target);
        }
    }
    public sealed class CardEffectSilkPull : DevaTargetEffect
    {
        public override bool TestEffectOnTarget(CardEffectState e, CardEffectParams p, CharacterState target, ICoreGameManagers g) =>
            Alive(target) && p.characterThatActivatedAbility != null && target != p.characterThatActivatedAbility
            && target.GetCurrentRoomIndex() != p.characterThatActivatedAbility.GetCurrentRoomIndex()
            && !target.IsOuterTrainBoss() && !target.HasStatusEffect("immobile")
            && (p.characterThatActivatedAbility.GetCurrentRoom()?.GetRemainingSpawnPointCount(target.GetTeamType()) ?? 0) > 0;
        public override IEnumerator ApplyEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g, ISystemManagers sys)
        {
            var self = Actor(p);
            foreach (var target in Targets(p).Where(t => TestEffectOnTarget(e, p, t, g)))
            {
                var move = new CardEffectParams { playedCard = p.playedCard, characterThatActivatedAbility = self };
                move.targets.Add(target);
                yield return CardEffectBump.Bump(null, move, g, self.GetCurrentRoomIndex() - target.GetCurrentRoomIndex());
            }
        }
    }
}
