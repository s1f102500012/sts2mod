using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace AITeammate.Scripts;

internal sealed partial class AiTeammateDummyController
{
    private IReadOnlyList<AiTeammateAvailableAction> DiscoverCombatActions(Player player)
    {
        List<AiTeammateAvailableAction> actions = [];
        Log.Debug($"[AITeammate] DiscoverCombatActions player={player.NetId} roomCount={player.RunState.CurrentRoomCount} currentRoom={player.RunState.CurrentRoom?.GetType().Name ?? "null"} inProgress={CombatManager.Instance.IsInProgress} playPhase={CombatManager.Instance.IsPlayPhase}");

        foreach (CardModel card in PileType.Hand.GetPile(player).Cards)
        {
            UnplayableReason reason;
            MegaCrit.Sts2.Core.Models.AbstractModel? preventer;
            if (!card.CanPlay(out reason, out preventer))
            {
                continue;
            }

            bool addedAction = false;
            foreach (Creature? target in GetOrderedTargets(card.TargetType, player))
            {
                if (target == null || IsPlayableTarget(card, target, player))
                {
                    AddPlayCardAction(actions, card, target);
                    addedAction = true;
                    if (!card.TargetType.IsSingleTarget())
                    {
                        break;
                    }
                }
            }

            if (!addedAction)
            {
                Log.Debug($"[AITeammate] Skipped combat action for card={card.Id.Entry} instance={GetCardInstanceId(card)} because no playable target was found for targetType={card.TargetType}.");
            }
        }

        List<PotionModel> potions = player.Potions.Where(static potion => !potion.IsQueued).ToList();
        for (int potionIndex = 0; potionIndex < potions.Count; potionIndex++)
        {
            PotionModel potion = potions[potionIndex];
            List<Creature?> targets = GetOrderedTargets(potion.TargetType, player).ToList();
            if (potion.TargetType.IsSingleTarget() && targets.Count == 0)
            {
                continue;
            }

            if (!potion.TargetType.IsSingleTarget())
            {
                targets = [targets.FirstOrDefault()];
            }

            foreach (Creature? target in targets)
            {
                string targetName = target?.ToString() ?? "none";
                string actionId = BuildUsePotionActionId(potion, target, potionIndex);
                actions.Add(new AiTeammateAvailableAction(
                    new AiLegalActionOption
                    {
                        ActionId = actionId,
                        ActionType = AiTeammateActionKind.UsePotion.ToString(),
                        Description = $"Use potion {potion.Id.Entry} -> {targetName}",
                        Label = $"Use potion {potion.Id.Entry}",
                        Summary = $"Use potion {potion.Id.Entry} targeting {targetName}.",
                        CardId = potion.Id.Entry,
                        TargetId = GetTargetId(target),
                        TargetLabel = targetName
                    },
                    () =>
                    {
                        UsePotionAction usePotionAction = new(potion, target, CombatManager.Instance.IsInProgress);
                        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(usePotionAction);
                        return Task.FromResult(new AiActionExecutionResult
                        {
                            GameAction = usePotionAction,
                            WaitForQueueSettle = true
                        });
                    }));
            }
        }

        actions.Add(new AiTeammateAvailableAction(
            new AiLegalActionOption
            {
                ActionId = BuildEndTurnActionId(player),
                ActionType = AiTeammateActionKind.EndTurn.ToString(),
                Description = "End turn",
                Label = "End turn",
                Summary = "Finish the actor's current turn."
            },
            () =>
            {
                int roundNumber = player.Creature.CombatState?.RoundNumber ?? 0;
                EndPlayerTurnAction endTurnAction = new(player, roundNumber);
                RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(endTurnAction);
                return Task.FromResult(new AiActionExecutionResult
                {
                    GameAction = endTurnAction,
                    WaitForQueueSettle = true
                });
            }));

        return actions;
    }

    private static IEnumerable<Creature?> GetOrderedTargets(TargetType targetType, Player player)
    {
        CombatState? combatState = player.Creature.CombatState;
        if (combatState == null)
        {
            return new Creature?[] { null };
        }

        return targetType switch
        {
            TargetType.AnyEnemy => combatState.HittableEnemies.OrderBy(static creature => creature.CombatId ?? uint.MaxValue).Cast<Creature?>(),
            TargetType.AnyAlly => combatState.PlayerCreatures.Where(static creature => creature.IsAlive).OrderBy(static creature => creature.Player?.NetId ?? 0UL).Cast<Creature?>(),
            TargetType.AnyPlayer => combatState.PlayerCreatures.Where(static creature => creature.IsAlive).OrderBy(static creature => creature.Player?.NetId ?? 0UL).Cast<Creature?>(),
            TargetType.Self => new Creature?[] { player.Creature },
            _ => new Creature?[] { null },
        };
    }

    private static bool IsPlayableTarget(CardModel card, Creature target, Player player)
    {
        if (card.TargetType == TargetType.Self && ReferenceEquals(target, player.Creature))
        {
            return true;
        }

        return card.CanPlayTargeting(target);
    }

    private static void AddPlayCardAction(List<AiTeammateAvailableAction> actions, CardModel card, Creature? target)
    {
        Creature? executionTarget = card.TargetType == TargetType.Self ? null : target;
        string targetName = card.TargetType == TargetType.Self ? "self" : target?.ToString() ?? "none";
        string actionId = BuildPlayCardActionId(card, executionTarget);
        actions.Add(new AiTeammateAvailableAction(
            new AiLegalActionOption
            {
                ActionId = actionId,
                ActionType = AiTeammateActionKind.PlayCard.ToString(),
                Description = $"Play {card.Id.Entry} -> {targetName}",
                Label = $"Play {card.Id.Entry}",
                Summary = $"Play {card.Id.Entry} targeting {targetName}.",
                CardId = card.Id.Entry,
                CardInstanceId = GetCardInstanceId(card),
                TargetId = GetTargetId(executionTarget),
                TargetLabel = targetName,
                EnergyCost = card.EnergyCost.GetAmountToSpend()
            },
            () =>
            {
                TaskHelper.RunSafely(card.OnEnqueuePlayVfx(executionTarget));
                PlayCardAction playCardAction = new(card, executionTarget);
                RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(playCardAction);
                return Task.FromResult(new AiActionExecutionResult
                {
                    GameAction = playCardAction,
                    WaitForQueueSettle = true
                });
            },
            deduplicationKey: $"card:{GetCardInstanceId(card)}"));
    }

    private static string BuildPlayCardActionId(CardModel card, Creature? target)
    {
        return $"play_card_{GetCardInstanceId(card)}_target_{GetTargetId(target)}";
    }

    private static string BuildUsePotionActionId(PotionModel potion, Creature? target, int potionIndex)
    {
        return $"use_potion_{SanitizeActionToken(potion.Id.Entry)}_{potionIndex}_target_{GetTargetId(target)}";
    }

    private static string BuildEndTurnActionId(Player player)
    {
        return $"end_turn_player_{player.NetId}";
    }

    private static string GetCardInstanceId(CardModel card)
    {
        return NetCombatCardDb.Instance.TryGetCardId(card, out uint cardId)
            ? $"combat_{cardId}"
            : SanitizeActionToken(card.Id.ToString());
    }

    private static string GetTargetId(Creature? target)
    {
        if (target == null)
        {
            return "none";
        }

        if (target.Player != null)
        {
            return $"player_{target.Player.NetId}";
        }

        return $"creature_{target.CombatId?.ToString() ?? SanitizeActionToken(target.ToString())}";
    }

    private static string SanitizeActionToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        return value.Replace(':', '_').Replace('/', '_').Replace(' ', '_');
    }
}
