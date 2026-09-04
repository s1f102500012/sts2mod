using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;

namespace AITeammate.Scripts;

internal static class RegentCharacterStrategy
{
    private const int CriticalStarReserve = 2;

    public static bool IsRegentPlayer(Player? player)
    {
        if (player == null)
        {
            return false;
        }

        try
        {
            if (AiTeammateSessionRegistry.TryGetParticipant(player.NetId, out var participant) &&
                ContainsToken(participant.Character.Id.Entry, "REGENT"))
            {
                return true;
            }
        }
        catch
        {
        }

        return player.Deck.Cards.Any(card => IsRegentCardName(card?.GetType().Name) || IsRegentCardName(card?.Id.Entry));
    }

    public static bool IsRegentDeck(CardEvaluationContext context)
    {
        return IsRegentPlayer(context.Player) || context.DeckCards.Any(IsRegentCard);
    }

    public static bool IsRegentCard(ResolvedCardView? card)
    {
        if (card == null)
        {
            return false;
        }

        return IsRegentCardName(card.CardId) || IsRegentCardName(card.Name);
    }

    public static bool TransformsDrawPileWithoutDrawing(ResolvedCardView? card)
    {
        return HasAnyToken(card, "CHARGE");
    }

    public static bool IsForgeOrCastCard(ResolvedCardView? card)
    {
        return HasAnyToken(card,
            "FURNACE",
            "FORGE",
            "FORGED",
            "CAST",
            "CONVERGENCE",
            "GATHER_LIGHT",
            "ASTRAL_PULSE",
            "STARDUST",
            "REFLECT",
            "PARTICLE_WALL");
    }

    public static bool IsStarGenerator(ResolvedCardView? card)
    {
        return card != null &&
            (card.GetStarsGenerated() > 0 || HasAnyToken(card, "VENERATE", "ROYAL_GAMBLE"));
    }

    public static bool IsStarSpender(ResolvedCardView? card)
    {
        return card != null &&
            (card.StarCost > 0 ||
             card.HasXStarCost ||
             HasAnyToken(card, "CLOAK_OF_STARS", "PARTICLE_WALL", "ASTRAL_PULSE", "SEVEN_STARS", "STARDUST"));
    }

    public static bool IsEngineCard(ResolvedCardView? card)
    {
        return IsStarGenerator(card) ||
            TransformsDrawPileWithoutDrawing(card) ||
            IsForgeOrCastCard(card) ||
            HasAnyToken(card, "ORBIT", "ARSENAL", "CHILD_OF_THE_STARS", "PARTICLE_WALL");
    }

    public static bool IsScalingCard(ResolvedCardView? card)
    {
        return IsForgeOrCastCard(card) ||
            HasAnyToken(card, "ORBIT", "ARSENAL", "CHILD_OF_THE_STARS", "SEVEN_STARS");
    }

    public static int ScoreCombatAction(DeterministicCombatContext context, AiLegalActionOption action, ResolvedCardView card)
    {
        if (!IsRegentPlayer(context.Actor) && !IsRegentCard(card))
        {
            return 0;
        }

        int score = 0;
        int availableStars = Math.Max(0, context.Stars);
        int starCost = Math.Max(0, card.StarCost);
        int starGain = Math.Max(0, card.GetStarsGenerated());

        if (TransformsDrawPileWithoutDrawing(card))
        {
            score += ScoreDrawPileTransform(context);
        }

        if (starGain > 0)
        {
            score += ScoreStarGeneration(context, card, availableStars, starGain);
        }

        if (starCost > 0 || card.HasXStarCost)
        {
            score += ScoreStarSpending(context, card, availableStars, starCost);
        }

        if (HasAnyToken(card, "ORBIT"))
        {
            score += HasActorPower(context, "ORBIT") ? -55 : (context.IsEliteOrBossCombat ? 52 : 24);
        }

        if (HasAnyToken(card, "ARSENAL"))
        {
            int transformPayoffs = KnownHandCards(context).Concat(context.KnownDrawPileTopCards)
                .Count(candidate => TransformsDrawPileWithoutDrawing(candidate) || HasAnyToken(candidate, "MINION"));
            score += HasActorPower(context, "ARSENAL") ? -45 : 22 + transformPayoffs * 12;
            if (context.IsEliteOrBossCombat)
            {
                score += 20;
            }
        }

        if (HasAnyToken(card, "ROYALTIES") && context.IncomingDamageAfterBlock > 0)
        {
            score -= 28;
        }

        if (HasAnyToken(card, "HEGEMONY") && context.IncomingDamageAfterBlock >= Math.Max(8, context.CurrentHp / 4))
        {
            score -= 12;
        }

        return score;
    }

    public static double ScoreRewardCard(ResolvedCardView card, CardEvaluationContext context)
    {
        if (!IsRegentDeck(context) && !IsRegentCard(card))
        {
            return 0.0;
        }

        int starGenerators = context.DeckCards.Count(IsStarGenerator);
        int starSpenders = context.DeckCards.Count(IsStarSpender);
        int transformCards = context.DeckCards.Count(TransformsDrawPileWithoutDrawing);
        bool hasArsenal = context.DeckCards.Any(candidate => HasAnyToken(candidate, "ARSENAL"));
        bool hasOrbit = context.DeckCards.Any(candidate => HasAnyToken(candidate, "ORBIT"));
        bool bossSoon = context.ActFloor >= 10 || context.TotalFloor >= 12 || context.CurrentActIndex >= 1;

        double score = 0.0;

        if (HasAnyToken(card, "ARSENAL"))
        {
            score += 10 + transformCards * 8;
            if (hasArsenal)
            {
                score -= 28;
            }

            if (bossSoon)
            {
                score += 10;
            }
        }

        if (HasAnyToken(card, "ORBIT"))
        {
            score += hasOrbit ? -22 : 16;
            score += Math.Min(12, context.DeckSummary.EngineCards * 2);
            if (bossSoon)
            {
                score += 8;
            }
        }

        if (IsStarGenerator(card))
        {
            score += starSpenders > starGenerators ? 14 : 5;
            if (HasAnyToken(card, "ROYAL_GAMBLE") && starSpenders < 2)
            {
                score -= 10;
            }
        }

        if (IsStarSpender(card))
        {
            score += starGenerators > 0 ? 8 : -12;
            if (HasAnyToken(card, "SEVEN_STARS"))
            {
                score += starGenerators >= 2 ? 18 : -18;
            }
        }

        if (HasAnyToken(card, "PARTICLE_WALL", "CLOAK_OF_STARS") && context.DeckSummary.BlockSources < 5)
        {
            score += 8;
        }

        if (HasAnyToken(card, "ROYALTIES"))
        {
            score -= context.DeckSummary.ScalingSources < 2 ? 16 : 8;
        }

        score += ScoreActOneRewardPreference(card, context);

        return score;
    }

    private static double ScoreActOneRewardPreference(ResolvedCardView card, CardEvaluationContext context)
    {
        return CharacterRewardProfiles.Regent.Score(
            card,
            context,
            premiumAttackAdjust: (_, attackBonus) =>
            {
            if (context.DeckSummary.FrontloadDamageSources >= 7 && NeedsActOneDefense(context))
            {
                attackBonus *= 0.65;
            }

                return attackBonus;
            },
            premiumDefenseAdjust: (_, defenseBonus) =>
            {
            if (NeedsActOneDefense(context))
            {
                defenseBonus += 6.0;
            }

                return defenseBonus;
            },
            weakDefenseAdjust: (_, defensePenalty) =>
            {
            if (NeedsActOneDefense(context) && context.DeckSummary.BlockSources <= 3)
            {
                defensePenalty *= 0.75;
            }

                return defensePenalty;
            });
    }

    private static bool NeedsActOneDefense(CardEvaluationContext context)
    {
        return context.DeckSummary.BlockSources < 5 || context.DeckSummary.QualityDefenseSources < 3;
    }

    public static double ScoreStandaloneCard(ResolvedCardView card, Player? player)
    {
        if (!IsRegentCard(card) && !IsRegentPlayer(player))
        {
            return 0.0;
        }

        double score = 0.0;

        if (IsStarGenerator(card))
        {
            score += 10;
        }

        if (HasAnyToken(card, "ORBIT", "ARSENAL"))
        {
            score += 16;
        }

        if (HasAnyToken(card, "PARTICLE_WALL", "ASTRAL_PULSE", "SEVEN_STARS"))
        {
            score += 10;
        }

        if (HasAnyToken(card, "ROYALTIES"))
        {
            score -= 6;
        }

        if (card.StarCost > 0)
        {
            score -= Math.Min(10, card.StarCost * 2);
        }

        return score;
    }

    public static double ScoreRemovalOrTransformBurden(ResolvedCardView card, Player? player)
    {
        if (!IsRegentCard(card) && !IsRegentPlayer(player))
        {
            return 0.0;
        }

        double burden = 0.0;

        if (IsGoodTransformCandidate(card))
        {
            burden += 34;
        }

        if (IsProtectedRegentCard(card))
        {
            burden -= 70;
        }

        if (IsStarGenerator(card) || IsStarSpender(card))
        {
            burden -= 26;
        }

        if (TransformsDrawPileWithoutDrawing(card))
        {
            burden += 8;
        }

        return burden;
    }

    private static int ScoreDrawPileTransform(DeterministicCombatContext context)
    {
        int badTargets = context.KnownDrawPileTopCards.Count(IsGoodTransformCandidate);
        int protectedTargets = context.KnownDrawPileTopCards.Count(IsProtectedRegentCard);
        int score = -58 + badTargets * 38 - protectedTargets * 34;

        if (badTargets == 0)
        {
            score -= 34;
        }

        if (context.IncomingDamageAfterBlock >= Math.Max(8, context.CurrentHp / 4))
        {
            score -= 32;
        }

        if (HasActorPower(context, "ARSENAL") && badTargets > 0)
        {
            score += 34;
        }

        return score;
    }

    private static int ScoreStarGeneration(DeterministicCombatContext context, ResolvedCardView card, int availableStars, int starGain)
    {
        int spenderDemand = KnownHandCards(context).Count(IsStarSpender);
        int score = starGain * 10 + spenderDemand * 12;

        if (availableStars >= CriticalStarReserve + 4 && spenderDemand == 0)
        {
            score -= 20;
        }

        if (HasActorPower(context, "ORBIT"))
        {
            score += 8;
        }

        if (HasAnyToken(card, "ROYAL_GAMBLE") && spenderDemand == 0)
        {
            score -= 28;
        }

        return score;
    }

    private static int ScoreStarSpending(DeterministicCombatContext context, ResolvedCardView card, int availableStars, int starCost)
    {
        int score = 0;

        if (card.HasXStarCost)
        {
            starCost = Math.Max(0, availableStars);
        }

        int remainingStars = availableStars - starCost;
        bool hasFutureStarBlock = KnownHandCards(context).Any(candidate =>
            candidate != card && HasAnyToken(candidate, "PARTICLE_WALL", "CLOAK_OF_STARS"));

        if (remainingStars < CriticalStarReserve && hasFutureStarBlock && context.IncomingDamageAfterBlock > 0)
        {
            score -= 32;
        }

        if (card.GetEstimatedDamage() >= Math.Max(10, context.TotalEnemyHp / 3))
        {
            score += 18;
        }

        if (card.GetEstimatedBlock() >= Math.Max(8, context.IncomingDamageAfterBlock))
        {
            score += 16;
        }

        if (HasAnyToken(card, "SEVEN_STARS", "ASTRAL_PULSE", "STARDUST") && context.EnemyCount >= 2)
        {
            score += 14;
        }

        return score;
    }

    private static bool IsGoodTransformCandidate(ResolvedCardView? card)
    {
        if (card == null)
        {
            return false;
        }

        if (card.Type is CardType.Curse or CardType.Status || HasAnyToken(card, "BECKON"))
        {
            return true;
        }

        if (string.Equals(card.Rarity, "Basic", StringComparison.Ordinal))
        {
            return true;
        }

        return HasAnyToken(card, "STRIKE_REGENT", "DEFEND_REGENT") ||
            (!IsProtectedRegentCard(card) && card.GetEstimatedDamage() + card.GetEstimatedBlock() <= 7 && card.GetCardsDrawn() == 0);
    }

    private static bool IsProtectedRegentCard(ResolvedCardView? card)
    {
        if (card == null)
        {
            return false;
        }

        return HasAnyToken(card,
            "VENERATE",
            "ORBIT",
            "ARSENAL",
            "HEGEMONY",
            "PARTICLE_WALL",
            "ASTRAL_PULSE",
            "SEVEN_STARS",
            "FURNACE",
            "CONVERGENCE",
            "GATHER_LIGHT",
            "REFLECT",
            "ROYAL_GAMBLE",
            "CHILD_OF_THE_STARS",
            "CLOAK_OF_STARS") ||
            string.Equals(card.Rarity, "Rare", StringComparison.Ordinal);
    }

    private static bool HasActorPower(DeterministicCombatContext context, string token)
    {
        return context.ActorPowerAmounts.Keys.Any(key => ContainsToken(key, token));
    }

    private static bool IsRegentCardName(string? value)
    {
        return HasToken(value, "REGENT") ||
            HasToken(value, "VENERATE") ||
            HasToken(value, "FALLING_STAR") ||
            HasToken(value, "HEGEMONY") ||
            HasToken(value, "ARSENAL") ||
            HasToken(value, "ORBIT") ||
            HasToken(value, "FURNACE") ||
            HasToken(value, "FORGE") ||
            HasToken(value, "CAST") ||
            HasToken(value, "CONVERGENCE") ||
            HasToken(value, "GATHER_LIGHT") ||
            HasToken(value, "REFLECT") ||
            HasToken(value, "ROYALTIES") ||
            HasToken(value, "STARDUST") ||
            HasToken(value, "ASTRAL_PULSE") ||
            HasToken(value, "PARTICLE_WALL") ||
            HasToken(value, "CLOAK_OF_STARS") ||
            HasToken(value, "ROYAL_GAMBLE") ||
            HasToken(value, "SEVEN_STARS");
    }

    private static bool HasAnyToken(ResolvedCardView? card, params string[] tokens)
    {
        return card != null && tokens.Any(token => HasToken(card.CardId, token) || HasToken(card.Name, token));
    }

    private static IEnumerable<ResolvedCardView> KnownHandCards(DeterministicCombatContext context)
    {
        return context.HandCardsByInstanceId.Values;
    }

    private static bool HasToken(string? value, string token)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ContainsToken(string? value, string token)
    {
        return HasToken(value, token);
    }
}
