using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace AITeammate.Scripts;

internal sealed class CardEvaluationContextFactory
{
    private readonly ICardResolver _cardResolver = new CardResolver(
        CardCatalogRepository.Shared,
        new CardDefinitionRepository(),
        new RunCardStateStore(),
        new CombatCardStateStore());

    public CardEvaluationContext Create(
        Player player,
        CardChoiceSource source,
        bool skipAllowed,
        int? candidateGoldCost = null,
        string? debugSource = null,
        FutureRewardRouteEvaluation? futureRewards = null)
    {
        List<ResolvedCardView> deckCards = player.Deck.Cards
            .Select((card, index) => _cardResolver.Resolve(card, BuildDeckInstanceId(card, index)))
            .ToList();

        return new CardEvaluationContext
        {
            Player = player,
            ChoiceSource = source,
            DeckCards = deckCards,
            DeckSummary = DeckSummaryBuilder.Build(deckCards),
            RelicIds = player.Relics
                .Select(static relic => relic.Id.Entry.ToUpperInvariant())
                .ToHashSet(StringComparer.Ordinal),
            ModifierIds = player.RunState.Modifiers
                .Select(static modifier => modifier.Id.Entry.ToUpperInvariant())
                .ToHashSet(StringComparer.Ordinal),
            SkipAllowed = skipAllowed,
            Gold = player.Gold,
            AscensionLevel = player.RunState.AscensionLevel,
            CurrentActIndex = player.RunState.CurrentActIndex,
            ActFloor = player.RunState.ActFloor,
            TotalFloor = player.RunState.TotalFloor,
            CandidateGoldCost = candidateGoldCost,
            FutureRewards = futureRewards,
            DebugSource = debugSource
        };
    }

    public ResolvedCardView ResolveCandidate(CardModel card, int index)
    {
        return _cardResolver.Resolve(card, BuildCandidateInstanceId(card, index));
    }

    private static string BuildDeckInstanceId(CardModel card, int index)
    {
        return $"deck_{index}_{Sanitize(card.Id.Entry)}";
    }

    private static string BuildCandidateInstanceId(CardModel card, int index)
    {
        return $"candidate_{index}_{Sanitize(card.Id.Entry)}";
    }

    private static string Sanitize(string value)
    {
        return value.Replace(':', '_').Replace('/', '_').Replace(' ', '_');
    }
}
