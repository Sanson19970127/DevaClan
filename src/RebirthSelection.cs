using System;
using System.Linq;

namespace DevaClan
{
    // One eligibility rule for play validation, the native exclusion filter,
    // and the final selection. CardData IDs are shared by different copies.
    internal sealed class RebirthSelection
    {
        readonly CardManager cards;
        internal bool Completed { get; private set; }
        internal CardState Selected { get; private set; }
        internal RebirthSelection(CardManager cards) { this.cards = cards; }

        internal static bool IsCandidate(CardState card, CardManager cards) =>
            card != null && card.GetCardType() == CardType.Monster
            && BattleMemory.Active.Dead.Contains(card) && cards.GetExhaustedPile().Contains(card);

        internal bool HasCandidates => cards.GetExhaustedPile().Any(c => IsCandidate(c, cards));

        internal DeckScreen.Params CreateParams(string titleKey) => new DeckScreen.Params {
            mode = DeckScreen.Mode.CardEffectSelection, targetMode = TargetMode.Exhaust,
            cardTypeFilter = CardType.Monster, allowSelectChampion = true,
            showCancel = true, excludeFilteredOutCards = true, ignoreDefaultFilters = true,
            titleKey = titleKey, instructionsKey = "ScreenDeck_Select_CardEffectRecursion",
            // DeckScreen removes cards when filterFunc returns true.
            filterFunc = c => !IsCandidate(c, cards)
        };

        internal void Choose(CardState card)
        {
            if (Completed) return;
            Selected = IsCandidate(card, cards) ? card : null;
            Completed = true;
        }
        internal void Cancel() { if (!Completed) { Selected = null; Completed = true; } }

        internal void Bind(DeckScreen deck, Action close)
        {
            deck.AddDeckScreenCardStateChosenDelegate(c => { Choose(c); close(); });
            // The native Cancel method closes the screen itself.
            deck.AddDeckScreenCancelCallback(Cancel);
        }
    }
}
