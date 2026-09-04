using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.AutoSlay.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace AITeammate.Scripts;

internal sealed partial class AiTeammateDummyController
{
    private static readonly TimeSpan ShopUiStabilizationDelay = TimeSpan.FromMilliseconds(450);

    private CardModel? _pendingShopRemovalTarget;
    private string? _pendingShopRemovalTargetId;

    private async Task<AiActionExecutionResult> ExecuteBestMerchantStepAsync(string expectedSnapshotFingerprint, string expectedActionId)
    {
        ShopVisitState? snapshot = TryBuildCurrentShopSnapshot();
        if (snapshot == null)
        {
            Log.Warn($"[AITeammate][Shop] Execute aborted player={PlayerId} reason=no_snapshot");
            return AiActionExecutionResult.Completed;
        }

        if (!IsMerchantExecutionSupported(snapshot, out string unsupportedReason))
        {
            Log.Warn($"[AITeammate][Shop] Execute aborted player={PlayerId} reason={unsupportedReason} localNetId={LocalContext.NetId?.ToString() ?? "none"} inventoryOwner={snapshot.InventoryOwnerPlayerId?.ToString() ?? "none"}");
            return AiActionExecutionResult.Completed;
        }

        ShopPlannerResult planResult = ShopPlanner.Evaluate(snapshot);
        Log.Info($"[AITeammate][Shop] Replan before execution player={PlayerId} fingerprint={snapshot.SnapshotFingerprint} bestPlan={planResult.BestPlan.PlanId} bestScore={planResult.BestPlan.TotalScore:F1} expectedFingerprint={expectedSnapshotFingerprint} expectedAction={expectedActionId}");

        ShopPlanStep? currentStep = planResult.BestPlan.Steps.FirstOrDefault();
        if (currentStep == null)
        {
            Log.Warn($"[AITeammate][Shop] Execute aborted player={PlayerId} reason=no_plan_step");
            return AiActionExecutionResult.Completed;
        }

        if (!string.Equals(snapshot.SnapshotFingerprint, expectedSnapshotFingerprint, StringComparison.Ordinal) ||
            !string.Equals(currentStep.ActionId, expectedActionId, StringComparison.Ordinal))
        {
            Log.Info($"[AITeammate][Shop] Replan mismatch player={PlayerId} expectedFingerprint={expectedSnapshotFingerprint} actualFingerprint={snapshot.SnapshotFingerprint} expectedAction={expectedActionId} actualAction={currentStep.ActionId}");
        }

        if (!TryResolveCurrentShopStep(snapshot, planResult, currentStep, out ShopAction? action, out ShopActionEvaluation? actionEvaluation, out string failureReason))
        {
            Log.Warn($"[AITeammate][Shop] Step resolution failed player={PlayerId} actionId={currentStep.ActionId} reason={failureReason}");
            await TryExecuteMerchantSafeStopAsync(failureReason);
            return AiActionExecutionResult.Completed;
        }

        ShopAction resolvedAction = action!;
        ShopActionEvaluation resolvedEvaluation = actionEvaluation!;

        Log.Info($"[AITeammate][Shop] Chosen step player={PlayerId} bestPlan={planResult.BestPlan.PlanId} action={resolvedAction.ActionId} kind={resolvedAction.Kind} score={resolvedEvaluation.ImmediateScore:F1} desc=\"{resolvedAction.Description}\"");
        return await ExecuteValidatedMerchantActionAsync(snapshot, resolvedAction, resolvedEvaluation);
    }

    private ShopVisitState? TryBuildCurrentShopSnapshot()
    {
        if (!TryGetControlledPlayer(out Player player, out _) ||
            player.RunState.CurrentRoom is not MerchantRoom)
        {
            return null;
        }

        return ShopSnapshotBuilder.Build(player);
    }

    private bool TryResolveCurrentShopStep(
        ShopVisitState snapshot,
        ShopPlannerResult planResult,
        ShopPlanStep step,
        out ShopAction? action,
        out ShopActionEvaluation? actionEvaluation,
        out string failureReason)
    {
        action = snapshot.Actions.FirstOrDefault(candidate => string.Equals(candidate.ActionId, step.ActionId, StringComparison.Ordinal));
        actionEvaluation = planResult.ActionEvaluations.FirstOrDefault(candidate => string.Equals(candidate.ActionId, step.ActionId, StringComparison.Ordinal));

        if (action == null)
        {
            failureReason = "action_missing_from_snapshot";
            return false;
        }

        if (actionEvaluation == null)
        {
            failureReason = "action_evaluation_missing";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    private async Task<AiActionExecutionResult> ExecuteValidatedMerchantActionAsync(
        ShopVisitState snapshot,
        ShopAction action,
        ShopActionEvaluation actionEvaluation)
    {
        if (snapshot.ExecutionMode == ShopExecutionMode.VirtualAiDirect)
        {
            return await ExecuteVirtualMerchantActionAsync(snapshot, action, actionEvaluation);
        }

        if (snapshot.Player.RunState.CurrentRoom is not MerchantRoom ||
            NRun.Instance?.MerchantRoom is not NMerchantRoom merchantRoom)
        {
            Log.Warn($"[AITeammate][Shop] Validation failed player={PlayerId} action={action.ActionId} reason=merchant_room_missing");
            return AiActionExecutionResult.RetrySoon;
        }

        bool validationSucceeded = action.Kind switch
        {
            ShopActionKind.OpenInventory => !snapshot.InventoryIsOpen,
            ShopActionKind.CloseInventory => snapshot.InventoryIsOpen,
            ShopActionKind.LeaveShop => !snapshot.InventoryIsOpen,
            ShopActionKind.BuyOffer => ValidateBuyAction(snapshot, action, out _),
            ShopActionKind.RemoveCard => ValidateRemovalAction(snapshot, action, actionEvaluation, out _),
            ShopActionKind.UseFoulPotionAtMerchant => ValidateFoulPotionAction(snapshot, out _),
            _ => false
        };

        if (!validationSucceeded)
        {
            Log.Warn($"[AITeammate][Shop] Validation failed player={PlayerId} action={action.ActionId}");
            await TryExecuteMerchantSafeStopAsync($"validation_failed:{action.ActionId}");
            return AiActionExecutionResult.Completed;
        }

        Log.Info($"[AITeammate][Shop] Execution attempt player={PlayerId} action={action.ActionId} kind={action.Kind}");
        switch (action.Kind)
        {
            case ShopActionKind.OpenInventory:
                merchantRoom.OpenInventory();
                await DelayAfterMerchantUiChangeAsync("open_inventory");
                Log.Info($"[AITeammate][Shop] Execution success player={PlayerId} action={action.ActionId} opened=true");
                return AiActionExecutionResult.Completed;

            case ShopActionKind.CloseInventory:
            {
                NBackButton? backButton = UiHelper.FindFirst<NBackButton>((Node)(object)merchantRoom);
                if (backButton == null)
                {
                    Log.Warn($"[AITeammate][Shop] Execution failed player={PlayerId} action={action.ActionId} reason=back_button_missing");
                    await TryExecuteMerchantSafeStopAsync("close_inventory_back_button_missing");
                    return AiActionExecutionResult.Completed;
                }

                await UiHelper.Click(backButton);
                await DelayAfterMerchantUiChangeAsync("close_inventory");
                Log.Info($"[AITeammate][Shop] Execution success player={PlayerId} action={action.ActionId} closed=true");
                return AiActionExecutionResult.Completed;
            }

            case ShopActionKind.LeaveShop:
                await UiHelper.Click(merchantRoom.ProceedButton);
                await DelayAfterMerchantUiChangeAsync("leave_shop");
                Log.Info($"[AITeammate][Shop] Execution success player={PlayerId} action={action.ActionId} left=true");
                return AiActionExecutionResult.Completed;

            case ShopActionKind.BuyOffer:
                return await ExecuteBuyOfferAsync(snapshot, merchantRoom, action);

            case ShopActionKind.RemoveCard:
                return await ExecuteRemovalAsync(snapshot, merchantRoom, action, actionEvaluation);

            case ShopActionKind.UseFoulPotionAtMerchant:
                return await ExecuteFoulPotionAsync(snapshot);

            default:
                Log.Warn($"[AITeammate][Shop] Execution unsupported player={PlayerId} action={action.ActionId} kind={action.Kind}");
                await TryExecuteMerchantSafeStopAsync($"unsupported:{action.Kind}");
                return AiActionExecutionResult.Completed;
        }
    }

    private async Task<AiActionExecutionResult> ExecuteVirtualMerchantActionAsync(
        ShopVisitState snapshot,
        ShopAction action,
        ShopActionEvaluation actionEvaluation)
    {
        bool validationSucceeded = action.Kind switch
        {
            ShopActionKind.BuyOffer => ValidateBuyAction(snapshot, action, out _),
            ShopActionKind.RemoveCard => ValidateRemovalAction(snapshot, action, actionEvaluation, out _),
            ShopActionKind.UseFoulPotionAtMerchant => ValidateFoulPotionAction(snapshot, out _),
            ShopActionKind.LeaveShop => true,
            _ => false
        };

        if (!validationSucceeded)
        {
            Log.Warn($"[AITeammate][Shop] Virtual validation failed player={PlayerId} action={action.ActionId} kind={action.Kind}");
            await TryExecuteMerchantSafeStopAsync($"virtual_validation_failed:{action.ActionId}");
            return AiActionExecutionResult.Completed;
        }

        Log.Info($"[AITeammate][Shop] Virtual execution attempt player={PlayerId} action={action.ActionId} kind={action.Kind} roomKey={snapshot.RoomVisitKey}");
        switch (action.Kind)
        {
            case ShopActionKind.BuyOffer:
                return await ExecuteVirtualBuyOfferAsync(snapshot, action);

            case ShopActionKind.RemoveCard:
                return await ExecuteVirtualRemovalAsync(snapshot, action, actionEvaluation);

            case ShopActionKind.UseFoulPotionAtMerchant:
                return await ExecuteVirtualFoulPotionAsync(snapshot);

            case ShopActionKind.LeaveShop:
                ShopInventoryResolver.MarkVisitCompleted(snapshot.Player, snapshot.RoomVisitKey, "planner_leave");
                Log.Info($"[AITeammate][Shop] Virtual execution success player={PlayerId} action={action.ActionId} left=true");
                return AiActionExecutionResult.Completed;

            default:
                Log.Warn($"[AITeammate][Shop] Virtual execution unsupported player={PlayerId} action={action.ActionId} kind={action.Kind}");
                await TryExecuteMerchantSafeStopAsync($"virtual_unsupported:{action.Kind}");
                return AiActionExecutionResult.Completed;
        }
    }

    private bool ValidateBuyAction(ShopVisitState snapshot, ShopAction action, out ShopOffer? offer)
    {
        offer = null;
        if (!snapshot.InventoryIsOpen || action.OfferId == null)
        {
            return false;
        }

        offer = snapshot.Offers.FirstOrDefault(candidate => string.Equals(candidate.OfferId, action.OfferId, StringComparison.Ordinal));
        if (offer == null || !offer.IsStocked || !offer.IsAffordable || !offer.IsPurchaseLegalNow)
        {
            return false;
        }

        if (action.GoldCost.HasValue && offer.Cost != action.GoldCost.Value)
        {
            return false;
        }

        return offer.Entry != null;
    }

    private bool ValidateRemovalAction(
        ShopVisitState snapshot,
        ShopAction action,
        ShopActionEvaluation actionEvaluation,
        out ShopRemovalCandidate? removalCandidate)
    {
        removalCandidate = actionEvaluation.RemovalCandidate;
        if (!snapshot.InventoryIsOpen ||
            !snapshot.CardRemovalAvailable ||
            removalCandidate?.DeckCard.RuntimeCard == null ||
            action.OfferId == null)
        {
            return false;
        }

        ShopOffer? removalOffer = snapshot.Offers.FirstOrDefault(candidate => string.Equals(candidate.OfferId, action.OfferId, StringComparison.Ordinal));
        return removalOffer != null &&
               removalOffer.IsStocked &&
               removalOffer.IsAffordable &&
               removalOffer.IsPurchaseLegalNow &&
               removalOffer.Entry is MerchantCardRemovalEntry;
    }

    private bool ValidateFoulPotionAction(ShopVisitState snapshot, out PotionModel? potion)
    {
        potion = snapshot.Player.PotionSlots
            .FirstOrDefault(static candidate => candidate is FoulPotion { PassesCustomUsabilityCheck: true });
        bool valid = potion != null && snapshot.HasUsableFoulPotion;
        if (!valid)
        {
            string potionState = string.Join(
                "|",
                snapshot.Player.PotionSlots.Select(static candidate =>
                    candidate == null
                        ? "empty"
                        : $"{candidate.Id.Entry}:usable={candidate.PassesCustomUsabilityCheck}:type={candidate.GetType().Name}"));
            Log.Warn($"[AITeammate][Shop] Foul potion validation detail player={snapshot.PlayerId} hasUsableSnapshot={snapshot.HasUsableFoulPotion} potions={potionState}");
        }

        return valid;
    }

    private async Task<AiActionExecutionResult> ExecuteBuyOfferAsync(ShopVisitState snapshot, NMerchantRoom merchantRoom, ShopAction action)
    {
        if (!ValidateBuyAction(snapshot, action, out ShopOffer? offer) || offer?.Entry == null)
        {
            Log.Warn($"[AITeammate][Shop] Buy validation failed player={PlayerId} action={action.ActionId}");
            await TryExecuteMerchantSafeStopAsync($"buy_validation_failed:{action.ActionId}");
            return AiActionExecutionResult.Completed;
        }

        bool success = await offer.Entry.OnTryPurchaseWrapper(merchantRoom.Inventory.Inventory);
        await DelayAfterMerchantUiChangeAsync($"buy_offer:{offer.ModelId}");
        Log.Info($"[AITeammate][Shop] Execution {(success ? "success" : "failure")} player={PlayerId} action={action.ActionId} offer={offer.OfferId} model={offer.ModelId} cost={offer.Cost}");
        return AiActionExecutionResult.Completed;
    }

    private async Task<AiActionExecutionResult> ExecuteVirtualBuyOfferAsync(ShopVisitState snapshot, ShopAction action)
    {
        if (!ValidateBuyAction(snapshot, action, out ShopOffer? offer) || offer?.Entry == null)
        {
            Log.Warn($"[AITeammate][Shop] Virtual buy validation failed player={PlayerId} action={action.ActionId}");
            await TryExecuteMerchantSafeStopAsync($"virtual_buy_validation_failed:{action.ActionId}");
            return AiActionExecutionResult.Completed;
        }

        bool success = await offer.Entry.OnTryPurchaseWrapper(snapshot.RuntimeInventory);
        await DelayAfterMerchantUiChangeAsync($"virtual_buy_offer:{offer.ModelId}");
        Log.Info($"[AITeammate][Shop] Virtual execution {(success ? "success" : "failure")} player={PlayerId} action={action.ActionId} offer={offer.OfferId} model={offer.ModelId} cost={offer.Cost} entryType={offer.Entry.GetType().Name} mode=virtual_ai_inventory");
        return AiActionExecutionResult.Completed;
    }

    private async Task<AiActionExecutionResult> ExecuteRemovalAsync(
        ShopVisitState snapshot,
        NMerchantRoom merchantRoom,
        ShopAction action,
        ShopActionEvaluation actionEvaluation)
    {
        if (!ValidateRemovalAction(snapshot, action, actionEvaluation, out ShopRemovalCandidate? removalCandidate) ||
            action.RuntimeLocator?.Entry is not MerchantCardRemovalEntry removalEntry ||
            removalCandidate?.DeckCard.RuntimeCard == null)
        {
            Log.Warn($"[AITeammate][Shop] Removal validation failed player={PlayerId} action={action.ActionId}");
            await TryExecuteMerchantSafeStopAsync($"removal_validation_failed:{action.ActionId}");
            return AiActionExecutionResult.Completed;
        }

        bool success;
        SetPendingShopRemovalTarget(removalCandidate.DeckCard.RuntimeCard);
        try
        {
            success = await removalEntry.OnTryPurchaseWrapper(merchantRoom.Inventory.Inventory);
        }
        finally
        {
            ClearPendingShopRemovalTarget("removal_purchase_complete");
        }

        await DelayAfterMerchantUiChangeAsync($"remove_card:{removalCandidate.CardId}");
        Log.Info($"[AITeammate][Shop] Execution {(success ? "success" : "failure")} player={PlayerId} action={action.ActionId} target={removalCandidate.CardId} name={removalCandidate.Name} cost={action.GoldCost ?? removalEntry.Cost} mode=merchant_purchase_wrapper");

        return AiActionExecutionResult.Completed;
    }

    private async Task<AiActionExecutionResult> ExecuteVirtualRemovalAsync(
        ShopVisitState snapshot,
        ShopAction action,
        ShopActionEvaluation actionEvaluation)
    {
        if (!ValidateRemovalAction(snapshot, action, actionEvaluation, out ShopRemovalCandidate? removalCandidate) ||
            action.RuntimeLocator?.Entry is not MerchantCardRemovalEntry removalEntry ||
            removalCandidate?.DeckCard.RuntimeCard == null)
        {
            Log.Warn($"[AITeammate][Shop] Virtual removal validation failed player={PlayerId} action={action.ActionId}");
            await TryExecuteMerchantSafeStopAsync($"virtual_removal_validation_failed:{action.ActionId}");
            return AiActionExecutionResult.Completed;
        }

        int goldCost = action.GoldCost ?? removalEntry.Cost;
        bool success = await ExecuteVirtualRemovalDirectAsync(
            snapshot.Player,
            removalEntry,
            removalCandidate.DeckCard.RuntimeCard,
            goldCost);

        await DelayAfterMerchantUiChangeAsync($"virtual_remove_card:{removalCandidate.CardId}");
        Log.Info($"[AITeammate][Shop] Virtual execution {(success ? "success" : "failure")} player={PlayerId} action={action.ActionId} target={removalCandidate.CardId} name={removalCandidate.Name} cost={goldCost} mode=virtual_ai_direct_remove");
        return AiActionExecutionResult.Completed;
    }

    private async Task<bool> ExecuteVirtualRemovalDirectAsync(
        Player player,
        MerchantCardRemovalEntry removalEntry,
        CardModel targetCard,
        int goldCost)
    {
        if (removalEntry.Used)
        {
            Log.Warn($"[AITeammate][Shop] Virtual removal failed player={PlayerId} reason=service_already_used target={targetCard.Id.Entry}");
            return false;
        }

        if (player.Gold < goldCost)
        {
            Log.Warn($"[AITeammate][Shop] Virtual removal failed player={PlayerId} reason=not_enough_gold gold={player.Gold} cost={goldCost} target={targetCard.Id.Entry}");
            removalEntry.InvokePurchaseFailed(PurchaseStatus.FailureGold);
            return false;
        }

        if (targetCard.Owner != player || targetCard.Pile?.Type != PileType.Deck)
        {
            Log.Warn($"[AITeammate][Shop] Virtual removal failed player={PlayerId} reason=target_not_in_ai_deck target={targetCard.Id.Entry} owner={targetCard.Owner?.NetId.ToString() ?? "none"} pile={targetCard.Pile?.Type.ToString() ?? "none"}");
            return false;
        }

        await PlayerCmd.LoseGold(goldCost, player, GoldLossType.Spent);
        await CardPileCmd.RemoveFromDeck(targetCard, showPreview: false);
        player.ExtraFields.CardShopRemovalsUsed++;
        removalEntry.SetUsed();
        await Hook.AfterItemPurchased(player.RunState, player, removalEntry, goldCost);
        removalEntry.InvokePurchaseCompleted(removalEntry);
        return true;
    }

    private async Task<AiActionExecutionResult> ExecuteFoulPotionAsync(ShopVisitState snapshot)
    {
        if (!ValidateFoulPotionAction(snapshot, out PotionModel? potion) || potion == null)
        {
            Log.Warn($"[AITeammate][Shop] Foul potion validation failed player={PlayerId}");
            await TryExecuteMerchantSafeStopAsync("foul_potion_validation_failed");
            return AiActionExecutionResult.Completed;
        }

        UsePotionAction usePotionAction = new(potion, null, isCombatInProgress: false);
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(usePotionAction);
        Log.Info($"[AITeammate][Shop] Execution success player={PlayerId} action=shop_use_foul_potion potion={potion.Id.Entry} tracking={usePotionAction.GetType().Name}");
        return new AiActionExecutionResult
        {
            GameAction = usePotionAction,
            WaitForQueueSettle = true
        };
    }

    private async Task<AiActionExecutionResult> ExecuteVirtualFoulPotionAsync(ShopVisitState snapshot)
    {
        if (!ValidateFoulPotionAction(snapshot, out PotionModel? potion) || potion == null)
        {
            Log.Warn($"[AITeammate][Shop] Virtual foul potion validation failed player={PlayerId}");
            await TryExecuteMerchantSafeStopAsync("virtual_foul_potion_validation_failed");
            return AiActionExecutionResult.Completed;
        }

        UsePotionAction usePotionAction = new(potion, null, isCombatInProgress: false);
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(usePotionAction);
        Log.Info($"[AITeammate][Shop] Virtual execution enqueued player={PlayerId} action=shop_use_foul_potion potion={potion.Id.Entry} tracking={usePotionAction.GetType().Name} mode=virtual_ai_inventory_action_queue");
        return new AiActionExecutionResult
        {
            GameAction = usePotionAction,
            WaitForQueueSettle = true
        };
    }

    private async Task TryExecuteMerchantSafeStopAsync(string reason)
    {
        ShopVisitState? snapshot = TryBuildCurrentShopSnapshot();
        if (snapshot == null)
        {
            Log.Warn($"[AITeammate][Shop] Safe stop skipped player={PlayerId} reason={reason} state=missing_snapshot");
            return;
        }

        if (snapshot.ExecutionMode == ShopExecutionMode.VirtualAiDirect)
        {
            ShopInventoryResolver.MarkVisitCompleted(snapshot.Player, snapshot.RoomVisitKey, $"safe_stop:{reason}");
            Log.Warn($"[AITeammate][Shop] Virtual safe stop player={PlayerId} reason={reason} roomKey={snapshot.RoomVisitKey}");
            return;
        }

        if (NRun.Instance?.MerchantRoom is not NMerchantRoom merchantRoom)
        {
            Log.Warn($"[AITeammate][Shop] Safe stop skipped player={PlayerId} reason={reason} state=missing_local_merchant_room");
            return;
        }

        Log.Warn($"[AITeammate][Shop] Safe stop player={PlayerId} reason={reason} inventoryOpen={snapshot.InventoryIsOpen}");
        if (snapshot.InventoryIsOpen)
        {
            NBackButton? backButton = UiHelper.FindFirst<NBackButton>((Node)(object)merchantRoom);
            if (backButton != null)
            {
                await UiHelper.Click(backButton);
                await DelayAfterMerchantUiChangeAsync("safe_stop_close_inventory");
            }

            return;
        }

        await UiHelper.Click(merchantRoom.ProceedButton);
        await DelayAfterMerchantUiChangeAsync("safe_stop_leave");
    }

    private async Task DelayAfterMerchantUiChangeAsync(string phase)
    {
        Log.Info($"[AITeammate][Shop] Waiting for merchant state update player={PlayerId} phase={phase} delayMs={(int)ShopUiStabilizationDelay.TotalMilliseconds}");
        await Task.Delay(ShopUiStabilizationDelay);
    }

    private void SetPendingShopRemovalTarget(CardModel target)
    {
        _pendingShopRemovalTarget = target;
        _pendingShopRemovalTargetId = target.Id.Entry;
        Log.Info($"[AITeammate][Shop] Pending removal target set player={PlayerId} card={target.Id.Entry}");
    }

    private void ClearPendingShopRemovalTarget(string reason)
    {
        if (_pendingShopRemovalTarget == null)
        {
            return;
        }

        Log.Info($"[AITeammate][Shop] Pending removal target cleared player={PlayerId} card={_pendingShopRemovalTargetId ?? "unknown"} reason={reason}");
        _pendingShopRemovalTarget = null;
        _pendingShopRemovalTargetId = null;
    }

    public static bool TryConsumePendingShopRemovalSelection(Player player, IEnumerable<CardModel> options, out IEnumerable<CardModel> selected)
    {
        selected = [];
        if (!TryGetControllerFor(player.NetId, out AiTeammateDummyController controller))
        {
            return false;
        }

        return controller.TryConsumePendingShopRemovalSelection(options, out selected);
    }

    private bool TryConsumePendingShopRemovalSelection(IEnumerable<CardModel> options, out IEnumerable<CardModel> selected)
    {
        selected = [];
        if (_pendingShopRemovalTarget == null)
        {
            return false;
        }

        List<CardModel> optionList = options.ToList();
        CardModel? match = optionList.FirstOrDefault(candidate => ReferenceEquals(candidate, _pendingShopRemovalTarget))
            ?? optionList.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, _pendingShopRemovalTarget.Id.Entry, StringComparison.Ordinal));
        if (match == null)
        {
            Log.Warn($"[AITeammate][Shop] Pending removal target missing from selector player={PlayerId} target={_pendingShopRemovalTargetId ?? "unknown"}");
            ClearPendingShopRemovalTarget("selector_target_missing");
            return false;
        }

        selected = [match];
        Log.Info($"[AITeammate][Shop] Pending removal target selected player={PlayerId} card={match.Id.Entry}");
        ClearPendingShopRemovalTarget("selector_consumed");
        return true;
    }
}
