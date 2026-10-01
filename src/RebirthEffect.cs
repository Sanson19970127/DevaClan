using System;
using System.Collections;

namespace DevaClan
{
    public sealed class CardEffectRebirth : DevaEffectBase
    {
        public override bool CanPlayWhenHandFull => false;
        public override bool TestEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g) =>
            new RebirthSelection(g.GetCardManager()).HasCandidates;
        public override IEnumerator ApplyEffect(CardEffectState e, CardEffectParams p, ICoreGameManagers g, ISystemManagers sys)
        {
            if (g.GetSaveManager().PreviewMode) yield break;
            var selection = new RebirthSelection(g.GetCardManager());
            if (!selection.HasCandidates) yield break;
            var screens = sys.GetScreenManager();
            screens.SetScreenActive(ScreenName.Deck, true, screen => {
                if (!(screen is DeckScreen deck)) { selection.Cancel(); return; }
                deck.Setup(selection.CreateParams(p.playedCard.GetTitleKey()));
                selection.Bind(deck, () => screens.SetScreenActive(ScreenName.Deck, false));
            });
            while (!selection.Completed) yield return null;
            var revived = selection.Selected;
            if (!RebirthSelection.IsCandidate(revived, g.GetCardManager())) yield break;
            var free = new CardUpgradeState(); free.Setup(Plugin.Upgrades["RebirthFree"]);
            free.SetSerializeScalingValues(true); free.SetCostReduction(Math.Max(0, revived.GetCostWithoutTraits()));
            revived.GetTemporaryCardStateModifiers().AddUpgrade(free);
            BattleMemory.Active.Karma.TryGetValue(revived, out int karma);
            BattleMemory.Active.Karma[revived] = karma + StatusCount(e, 5);
            g.GetCardManager().RestoreExhaustedOrEatenCard(revived);
            g.GetCardManager().DrawSpecificCard(revived, false, HandUI.DrawSource.Consume, p.playedCard);
        }
    }
}
