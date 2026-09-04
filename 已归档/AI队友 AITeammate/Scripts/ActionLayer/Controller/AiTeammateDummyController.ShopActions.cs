using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;

namespace AITeammate.Scripts;

internal sealed partial class AiTeammateDummyController
{
    private static readonly ShopSnapshotBuilder ShopSnapshotBuilder = new();
    private static readonly ShopPlanner ShopPlanner = new();
    private string? _lastLoggedShopSnapshotFingerprint;
    private string? _lastLoggedMerchantExecutionUnsupportedFingerprint;
    private string? _lastLoggedCompletedMerchantVisitKey;

    private IReadOnlyList<AiTeammateAvailableAction> DiscoverMerchantActions(Player player)
    {
        ShopVisitState? snapshot = ShopSnapshotBuilder.Build(player);
        if (snapshot == null)
        {
            return [];
        }

        if (snapshot.VisitCompleted)
        {
            if (!string.Equals(_lastLoggedCompletedMerchantVisitKey, snapshot.RoomVisitKey, StringComparison.Ordinal))
            {
                _lastLoggedCompletedMerchantVisitKey = snapshot.RoomVisitKey;
                Log.Info($"[AITeammate][Shop] Merchant visit already complete player={PlayerId} roomKey={snapshot.RoomVisitKey} mode={snapshot.ExecutionMode}");
            }

            return [];
        }

        _lastLoggedCompletedMerchantVisitKey = null;

        ShopPlannerResult planResult = ShopPlanner.Evaluate(snapshot);
        LogMerchantSnapshotIfNeeded(snapshot, planResult);
        if (!IsMerchantExecutionSupported(snapshot, out string unsupportedReason))
        {
            LogMerchantExecutionUnsupportedIfNeeded(snapshot, unsupportedReason);
            return [];
        }

        ShopPlanStep? nextStep = planResult.BestPlan.Steps.FirstOrDefault();
        if (nextStep == null)
        {
            Log.Warn($"[AITeammate][Shop] No executable shop step player={PlayerId} fingerprint={snapshot.SnapshotFingerprint}");
            return [];
        }

        return
        [
            new AiTeammateAvailableAction(
                new AiLegalActionOption
                {
                    ActionId = BuildShopDecisionActionId(snapshot, nextStep),
                    ActionType = AiTeammateActionKind.ExecuteShopStep.ToString(),
                    Description = nextStep.Description,
                    Label = BuildShopDecisionLabel(nextStep),
                    Summary = $"Execute shop step {nextStep.ActionId}. bestPlan={planResult.BestPlan.PlanId} score={planResult.BestPlan.TotalScore:F1} remainingGold={planResult.BestPlan.RemainingGold}",
                    PriorityTags =
                    [
                        "shop",
                        "execution",
                        nextStep.Kind.ToString(),
                        snapshot.InventoryIsOpen ? "inventory_open" : "inventory_closed"
                    ],
                    Metadata = BuildShopInspectionMetadata(snapshot, planResult, nextStep)
                },
                () => ExecuteBestMerchantStepAsync(snapshot.SnapshotFingerprint, nextStep.ActionId),
                $"{PlayerId}:shop_step:{snapshot.SnapshotFingerprint}:{nextStep.ActionId}")
        ];
    }

    private void LogMerchantExecutionUnsupportedIfNeeded(ShopVisitState snapshot, string reason)
    {
        string unsupportedFingerprint = $"unsupported:{snapshot.SnapshotFingerprint}:{reason}";
        if (string.Equals(_lastLoggedMerchantExecutionUnsupportedFingerprint, unsupportedFingerprint, StringComparison.Ordinal))
        {
            return;
        }

        _lastLoggedMerchantExecutionUnsupportedFingerprint = unsupportedFingerprint;
        Log.Warn($"[AITeammate][Shop] Live merchant execution disabled player={PlayerId} reason={reason} localNetId={LocalContext.NetId?.ToString() ?? "none"} controlledPlayer={snapshot.PlayerId} inventoryOwner={snapshot.InventoryOwnerPlayerId?.ToString() ?? "none"}");
    }

    private void LogMerchantSnapshotIfNeeded(ShopVisitState snapshot, ShopPlannerResult planResult)
    {
        if (string.Equals(_lastLoggedShopSnapshotFingerprint, snapshot.SnapshotFingerprint, StringComparison.Ordinal))
        {
            return;
        }

        _lastLoggedShopSnapshotFingerprint = snapshot.SnapshotFingerprint;

        Log.Info($"[AITeammate][Shop] Entered merchant discovery player={PlayerId} {snapshot.DescribeSummary()}");
        Log.Info($"[AITeammate][Shop] Deck summary player={PlayerId} {snapshot.DescribeDeckSummary()}");
        Log.Info($"[AITeammate][Shop] Relics player={PlayerId} [{string.Join(", ", snapshot.RelicIds.OrderBy(static id => id, StringComparer.Ordinal))}]");
        Log.Info($"[AITeammate][Shop] Modifiers player={PlayerId} [{string.Join(", ", snapshot.ModifierIds.OrderBy(static id => id, StringComparer.Ordinal))}]");
        bool foulPotionSupported = snapshot.ExecutionMode == ShopExecutionMode.LocalSharedUi || snapshot.ExecutionMode == ShopExecutionMode.VirtualAiDirect;
        Log.Info($"[AITeammate][Shop] Execution capability player={PlayerId} mode={snapshot.ExecutionMode} buyOffers={(snapshot.ExecutionMode == ShopExecutionMode.VirtualAiDirect || snapshot.ExecutionMode == ShopExecutionMode.LocalSharedUi)} removeCard=true leaveShop=true foulPotion={foulPotionSupported} localUi={(snapshot.ExecutionMode == ShopExecutionMode.LocalSharedUi)}");

        if (snapshot.OwnedPotions.Count == 0)
        {
            Log.Info($"[AITeammate][Shop] Owned potions player={PlayerId} none");
        }
        else
        {
            foreach (ShopOwnedPotion potion in snapshot.OwnedPotions)
            {
                Log.Info($"[AITeammate][Shop] Owned potion player={PlayerId} slot={potion.SlotIndex} id={potion.PotionId} name={potion.Name} foul={potion.IsFoulPotion} usableAtMerchant={potion.IsUsableAtMerchant}");
            }
        }

        foreach (ShopOffer offer in snapshot.Offers)
        {
            Log.Info($"[AITeammate][Shop] Offer player={PlayerId} {offer.Describe()} locator={offer.RuntimeLocator?.LocatorId ?? "none"}");
        }

        foreach (ShopAction action in snapshot.Actions)
        {
            Log.Info($"[AITeammate][Shop] Normalized action player={PlayerId} {action.Describe()}");
        }

        foreach (ShopOfferEvaluation evaluation in planResult.OfferEvaluations)
        {
            Log.Info($"[AITeammate][Shop] Offer evaluation player={PlayerId} {evaluation.Describe()}");
        }

        foreach (ShopActionEvaluation evaluation in planResult.ActionEvaluations)
        {
            Log.Info($"[AITeammate][Shop] Action evaluation player={PlayerId} {evaluation.Describe()}");
            if (evaluation.RemovalCandidate != null)
            {
                Log.Info($"[AITeammate][Shop] Removal target player={PlayerId} {evaluation.RemovalCandidate.Describe()}");
            }
        }

        foreach (ShopPlan plan in planResult.ConsideredPlans.Take(8))
        {
            Log.Info($"[AITeammate][Shop] Candidate plan player={PlayerId} {plan.Describe()}");
            foreach (ShopPlanStep step in plan.Steps)
            {
                Log.Info($"[AITeammate][Shop] Candidate step player={PlayerId} {step.Describe()}");
            }
        }

        double bestDelta = planResult.BestPlan.TotalScore - planResult.LeavePlan.TotalScore;
        Log.Info($"[AITeammate][Shop] Final plan player={PlayerId} {planResult.BestPlan.Describe()}");
        Log.Info($"[AITeammate][Shop] Plan comparison player={PlayerId} bestPlan={planResult.BestPlan.PlanId} leavePlan={planResult.LeavePlan.PlanId} delta={bestDelta:F1}");
    }

    private static Dictionary<string, string> BuildShopInspectionMetadata(ShopVisitState snapshot, ShopPlannerResult planResult, ShopPlanStep nextStep)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["shop_fingerprint"] = snapshot.SnapshotFingerprint,
            ["room_type"] = snapshot.RoomType,
            ["room_visit_key"] = snapshot.RoomVisitKey,
            ["execution_mode"] = snapshot.ExecutionMode.ToString(),
            ["inventory_open"] = snapshot.InventoryIsOpen.ToString(),
            ["offer_count"] = snapshot.Offers.Count.ToString(),
            ["action_count"] = snapshot.Actions.Count.ToString(),
            ["gold"] = snapshot.Gold.ToString(),
            ["foul_potion"] = snapshot.HasUsableFoulPotion.ToString(),
            ["courier"] = snapshot.HasCourier.ToString(),
            ["membership_card"] = snapshot.HasMembershipCard.ToString(),
            ["sozu"] = snapshot.HasSozu.ToString(),
            ["hoarder"] = snapshot.HasHoarder.ToString(),
            ["best_plan_id"] = planResult.BestPlan.PlanId,
            ["best_plan_score"] = planResult.BestPlan.TotalScore.ToString("F1"),
            ["best_plan_remaining_gold"] = planResult.BestPlan.RemainingGold.ToString(),
            ["leave_plan_score"] = planResult.LeavePlan.TotalScore.ToString("F1"),
            ["next_step_action_id"] = nextStep.ActionId,
            ["next_step_kind"] = nextStep.Kind.ToString(),
            ["next_step_score"] = nextStep.ScoreContribution.ToString("F1")
        };
    }

    private static string BuildShopDecisionActionId(ShopVisitState snapshot, ShopPlanStep nextStep)
    {
        return $"shop_step_{SanitizeActionToken(snapshot.SnapshotFingerprint)}_{SanitizeActionToken(nextStep.ActionId)}";
    }

    private static bool IsMerchantExecutionSupported(ShopVisitState snapshot, out string reason)
    {
        if (snapshot.ExecutionMode == ShopExecutionMode.VirtualAiDirect)
        {
            reason = string.Empty;
            return true;
        }

        ulong? localNetId = LocalContext.NetId;
        if (!localNetId.HasValue)
        {
            reason = "local_context_missing";
            return false;
        }

        if (snapshot.InventoryOwnerPlayerId != snapshot.PlayerId)
        {
            reason = $"inventory_owner_mismatch:{snapshot.InventoryOwnerPlayerId?.ToString() ?? "none"}";
            return false;
        }

        if (snapshot.PlayerId != localNetId.Value)
        {
            reason = $"controlled_player_not_local:{localNetId.Value}";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static string BuildShopDecisionLabel(ShopPlanStep nextStep)
    {
        return nextStep.Kind switch
        {
            ShopActionKind.BuyOffer => "Buy from shop",
            ShopActionKind.RemoveCard => "Remove card",
            ShopActionKind.UseFoulPotionAtMerchant => "Use foul potion",
            ShopActionKind.OpenInventory => "Open merchant",
            ShopActionKind.CloseInventory => "Close merchant",
            ShopActionKind.LeaveShop => "Leave merchant",
            _ => "Shop action"
        };
    }
}
