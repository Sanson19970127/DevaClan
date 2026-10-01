using System.Collections;

namespace DevaClan
{
    internal static class SingingBowl
    {
        internal static IEnumerator AfterAbility(CardState ability, CharacterState user, bool hasRelic)
        {
            if (!hasRelic || ability == null || !ability.IsUnitAbility() || user == null
                || user.GetTeamType() != Team.Type.Monsters || user.IsDestroyed || !user.IsAlive)
                yield break;
            // A unit-only battle buff, without a trigger or a deck-wide card upgrade.
            var upgrade = new CardUpgradeState();
            upgrade.Setup();
            upgrade.SetSerializeScalingValues(true);
            upgrade.SetAttackDamage(3);
            upgrade.SetAdditionalHP(3);
            yield return user.ApplyCardUpgrade(upgrade);
        }
    }
}
