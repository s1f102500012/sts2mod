using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Actions;
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
    private static readonly IAiDecisionBackend DecisionBackend = AiDecisionBackendFactory.CreateDefault();
    private static readonly TimeSpan IdleTickInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan EndTurnGraceInterval = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan ActionSettleTimeout = TimeSpan.FromMilliseconds(5000);
    private static readonly TimeSpan QueueSettleTimeout = TimeSpan.FromMilliseconds(2500);
    private static readonly TimeSpan PostSettleGraceInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaxInitialCombatDecisionStagger = TimeSpan.FromMilliseconds(200);

    private DateTime _nextDecisionAtUtc = DateTime.MinValue;
    private bool _isExecutingAction;
    private string? _pendingEndTurnActionId;
    private string? _pendingEndTurnActionSetFingerprint;
    private DateTime _pendingEndTurnCommitAtUtc = DateTime.MinValue;
    private string? _lastDeduplicationKey;
    private int _lastCompletedEndTurnRound = -1;
    private int _lastCombatRoundWithInitialStagger = -1;
    private PendingIssuedActionSettlement? _pendingIssuedActionSettlement;

    public AiTeammateDummyController(int slotIndex, ulong playerId, CharacterModel character)
    {
        SlotIndex = slotIndex;
        PlayerId = playerId;
        Character = character;
    }

    public int SlotIndex { get; }

    public ulong PlayerId { get; }

    public CharacterModel Character { get; }

    public void Tick()
    {
        if (TryGetControlledPlayer(out Player controlledPlayer, out RunState controlledRunState))
        {
            AiTeammateTestCombatHelper.ApplyOneHpEnemiesIfNeeded(controlledPlayer, controlledRunState);
        }

        if (_isExecutingAction || DateTime.UtcNow < _nextDecisionAtUtc)
        {
            return;
        }

        if (TryWaitForIssuedActionSettlement())
        {
            return;
        }

        IReadOnlyList<AiTeammateAvailableAction> availableActions = DiscoverAvailableActions();
        List<AiTeammateAvailableAction> decisionActions = BuildDecisionActions(availableActions);
        bool isCombatDecision = TryGetControlledPlayer(out Player promptPlayer, out _)
            && IsCombatDecisionWindow(promptPlayer);
        ResetCompletedEndTurnTrackingIfNeeded(isCombatDecision ? promptPlayer : null, isCombatDecision);
        string actionSetFingerprint = decisionActions.Count > 0
            ? BuildActionSetFingerprint(decisionActions)
            : string.Empty;

        if (TryApplyInitialCombatDecisionStagger(decisionActions, promptPlayer, isCombatDecision))
        {
            return;
        }

        if (TryHandlePendingEndTurn(decisionActions, actionSetFingerprint, isCombatDecision))
        {
            return;
        }

        if (decisionActions.Count == 0)
        {
            _nextDecisionAtUtc = DateTime.UtcNow + IdleTickInterval;
            return;
        }

        if (ShouldSuppressRepeatedEndTurn(decisionActions, promptPlayer, isCombatDecision))
        {
            _nextDecisionAtUtc = DateTime.UtcNow + IdleTickInterval;
            return;
        }

        Log.Info($"[AITeammate] Player={PlayerId} legal actions: {string.Join(", ", decisionActions.Select(static action => action.ActionId))}");

        AiDecisionRequest request = new()
        {
            RequestId = BuildDecisionRequestId(),
            SnapshotId = BuildDecisionSnapshotId(),
            ActorId = PlayerId.ToString(),
            LegalActions = decisionActions.Select(static action => action.Option).ToList()
        };

        if (isCombatDecision && ShouldScheduleDelayedEndTurn(decisionActions))
        {
            string endTurnActionId = decisionActions[0].ActionId;
            if (!string.Equals(_pendingEndTurnActionId, endTurnActionId, StringComparison.Ordinal))
            {
                _pendingEndTurnActionId = endTurnActionId;
                _pendingEndTurnActionSetFingerprint = actionSetFingerprint;
                _pendingEndTurnCommitAtUtc = DateTime.UtcNow + EndTurnGraceInterval;
                _nextDecisionAtUtc = DateTime.UtcNow + IdleTickInterval;
                Log.Info($"[AITeammate] Player={PlayerId} scheduled delayed end turn actionId={endTurnActionId} graceMs={(int)EndTurnGraceInterval.TotalMilliseconds}");
            }

            return;
        }

        if (isCombatDecision && ShouldExecuteImmediateCombatDecision(decisionActions))
        {
            Log.Info($"[AITeammate] Player={PlayerId} using immediate combat decision path actionId={decisionActions[0].ActionId}");
        }

        ExecuteImmediateDecision(request, actionSetFingerprint);
    }

    public IReadOnlyList<AiTeammateAvailableAction> DiscoverAvailableActions()
    {
        if (!TryGetControlledPlayer(out Player player, out RunState runState))
        {
            return Array.Empty<AiTeammateAvailableAction>();
        }

        if (!player.Creature.IsAlive)
        {
            return Array.Empty<AiTeammateAvailableAction>();
        }

        if (IsCombatDecisionWindow(player))
        {
            return DiscoverCombatActions(player);
        }

        if (runState.CurrentRoom is MegaCrit.Sts2.Core.Rooms.EventRoom)
        {
            return DiscoverEventActions(player);
        }

        if (runState.CurrentRoom is MegaCrit.Sts2.Core.Rooms.RestSiteRoom)
        {
            return DiscoverRestSiteActions(player);
        }

        if (runState.CurrentRoom is MegaCrit.Sts2.Core.Rooms.MerchantRoom)
        {
            return DiscoverMerchantActions(player);
        }

        return Array.Empty<AiTeammateAvailableAction>();
    }

    public static bool IsAiPlayer(Player? player)
    {
        return player != null &&
               AiTeammateSessionRegistry.ActiveRunSession?.AiControllers.ContainsKey(player.NetId) == true;
    }

    public static bool IsAutomatedAiPlayer(Player? player)
    {
        return AiTeammateSessionRegistry.ShouldAutomateAiPlayer(player);
    }

    public static bool CanUseDirectSelectionAutomation(Player? player)
    {
        return AiTeammateSessionRegistry.CanUseDirectSelectionAutomation(player);
    }

    public static bool TryGetControllerFor(ulong playerId, out AiTeammateDummyController controller)
    {
        if (AiTeammateSessionRegistry.ActiveRunSession is { } session &&
            session.AiControllers.TryGetValue(playerId, out AiTeammateDummyController? foundController))
        {
            controller = foundController;
            return true;
        }

        if (AiTeammateSessionRegistry.AutopilotEnabled &&
            AiTeammateSessionRegistry.ActiveRunSession is { } hostSession &&
            hostSession.HostPlayerId == playerId)
        {
            controller = hostSession.HostController;
            return true;
        }

        if (AiTeammateSessionRegistry.TryGetAutopilotHostController(out AiTeammateDummyController hostController) &&
            hostController.PlayerId == playerId)
        {
            controller = hostController;
            return true;
        }

        controller = null!;
        return false;
    }

    private static bool IsCombatDecisionWindow(Player player)
    {
        return MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsInProgress &&
               MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsPlayPhase &&
               player.Creature.CombatState?.CurrentSide == player.Creature.Side &&
               !MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsPlayerReadyToEndTurn(player);
    }

    private List<AiTeammateAvailableAction> BuildDecisionActions(IReadOnlyList<AiTeammateAvailableAction> actions)
    {
        return actions
            .Where(action => action.DeduplicationKey == null || action.DeduplicationKey != _lastDeduplicationKey)
            .ToList();
    }

    private bool TryExecuteActionById(string actionId)
    {
        AiTeammateAvailableAction? action = DiscoverAvailableActions()
            .FirstOrDefault(candidate => string.Equals(candidate.ActionId, actionId, StringComparison.Ordinal));
        if (action == null)
        {
            Log.Warn($"[AITeammate] Player={PlayerId} could not resolve actionId={actionId} at commit time.");
            return false;
        }

        _isExecutingAction = true;
        TaskHelper.RunSafely(ExecuteResolvedActionAsync(action));
        return true;
    }

    private async Task ExecuteResolvedActionAsync(AiTeammateAvailableAction action)
    {
        try
        {
            AiActionExecutionResult executionResult = await action.ExecuteAsync();
            if (executionResult.ShouldRememberDeduplication && !string.IsNullOrEmpty(action.DeduplicationKey))
            {
                _lastDeduplicationKey = action.DeduplicationKey;
            }

            if (executionResult.HasTrackedGameAction)
            {
                BeginIssuedActionSettlement(action, executionResult);
                Log.Info($"[AITeammate] Player={PlayerId} issued actionId={action.ActionId} tracking={DescribeTrackedAction(executionResult.GameAction!)} queueSettle={executionResult.WaitForQueueSettle}");
            }
            else
            {
                string retrySuffix = executionResult.ShouldRememberDeduplication ? string.Empty : " retryRequested=true";
                Log.Info($"[AITeammate] Player={PlayerId} executed non-tracked actionId={action.ActionId}{retrySuffix}");
                if (IsCombatEndTurnAction(action.ActionId) &&
                    TryGetControlledPlayer(out Player controlledPlayer, out _))
                {
                    _lastCompletedEndTurnRound = controlledPlayer.Creature.CombatState?.RoundNumber ?? _lastCompletedEndTurnRound;
                }
            }
        }
        catch (Exception exception)
        {
            Log.Warn($"[AITeammate] Dummy controller {PlayerId} failed to execute actionId={action.ActionId}: {exception}");
        }
        finally
        {
            _isExecutingAction = false;
        }
    }

    private static string BuildActionSetFingerprint(IReadOnlyList<AiTeammateAvailableAction> actions)
    {
        return string.Join("|", actions.Select(static action => action.ActionId));
    }

    private static bool ShouldExecuteImmediateCombatDecision(IReadOnlyList<AiTeammateAvailableAction> actions)
    {
        return actions.Count == 1 && !IsEndTurnAction(actions[0]);
    }

    private static bool ShouldScheduleDelayedEndTurn(IReadOnlyList<AiTeammateAvailableAction> actions)
    {
        return actions.Count == 1 && IsEndTurnAction(actions[0]);
    }

    private bool TryHandlePendingEndTurn(
        IReadOnlyList<AiTeammateAvailableAction> decisionActions,
        string actionSetFingerprint,
        bool isCombatDecision)
    {
        if (string.IsNullOrEmpty(_pendingEndTurnActionId))
        {
            return false;
        }

        if (!isCombatDecision)
        {
            ClearPendingEndTurn("left_combat_window");
            return false;
        }

        if (!decisionActions.Any(action => string.Equals(action.ActionId, _pendingEndTurnActionId, StringComparison.Ordinal)))
        {
            ClearPendingEndTurn("action_missing");
            return false;
        }

        if (ShouldCancelPendingEndTurnForBetterAction(decisionActions))
        {
            ClearPendingEndTurn("better_actions_available");
            return false;
        }

        if (DateTime.UtcNow < _pendingEndTurnCommitAtUtc)
        {
            _nextDecisionAtUtc = DateTime.UtcNow + IdleTickInterval;
            return true;
        }

        string actionId = _pendingEndTurnActionId;
        string fingerprint = _pendingEndTurnActionSetFingerprint ?? actionSetFingerprint;
        ClearPendingEndTurn("commit");
        Log.Info($"[AITeammate] Player={PlayerId} committing delayed end turn actionId={actionId}");
        CommitResolvedAction(actionId, fingerprint, allowDelayedEndTurn: false);
        return true;
    }

    private bool ShouldCancelPendingEndTurnForBetterAction(IReadOnlyList<AiTeammateAvailableAction> decisionActions)
    {
        if (!decisionActions.Any(action => !IsEndTurnAction(action)))
        {
            return false;
        }

        AiDecisionRequest request = new()
        {
            RequestId = BuildDecisionRequestId(),
            SnapshotId = BuildDecisionSnapshotId(),
            ActorId = PlayerId.ToString(),
            LegalActions = decisionActions.Select(static action => action.Option).ToList()
        };

        try
        {
            AiDecisionResult result = DecisionBackend.DecideAsync(request, CancellationToken.None).GetAwaiter().GetResult();
            if (IsCombatEndTurnAction(result.ChosenActionId))
            {
                Log.Info($"[AITeammate] Player={PlayerId} keeping delayed end turn actionId={_pendingEndTurnActionId}; current best is still {result.ChosenActionId}");
                return false;
            }

            Log.Info($"[AITeammate] Player={PlayerId} found better action while delaying end turn actionId={_pendingEndTurnActionId} bestActionId={result.ChosenActionId} reason={result.Reason ?? "none"}");
            return true;
        }
        catch (Exception exception)
        {
            Log.Warn($"[AITeammate] Player={PlayerId} failed to re-score pending end turn; keeping delayed end turn. {exception}");
            return false;
        }
    }

    private void ExecuteImmediateDecision(AiDecisionRequest request, string actionSetFingerprint)
    {
        try
        {
            AiDecisionResult result = DecisionBackend.DecideAsync(request, CancellationToken.None).GetAwaiter().GetResult();
            Log.Info($"[AITeammate] Player={PlayerId} chose actionId={result.ChosenActionId} reason={result.Reason ?? "none"}");
            CommitResolvedAction(result.ChosenActionId, actionSetFingerprint);
        }
        catch (Exception exception)
        {
            Log.Warn($"[AITeammate] Dummy controller {PlayerId} failed to choose an action: {exception}");
            _nextDecisionAtUtc = DateTime.UtcNow + IdleTickInterval;
        }
    }

    private void CommitResolvedAction(string actionId, string actionSetFingerprint)
    {
        CommitResolvedAction(actionId, actionSetFingerprint, allowDelayedEndTurn: true);
    }

    private void CommitResolvedAction(string actionId, string actionSetFingerprint, bool allowDelayedEndTurn)
    {
        if (allowDelayedEndTurn && IsCombatEndTurnAction(actionId))
        {
            if (!string.Equals(_pendingEndTurnActionId, actionId, StringComparison.Ordinal))
            {
                _pendingEndTurnActionId = actionId;
                _pendingEndTurnActionSetFingerprint = actionSetFingerprint;
                _pendingEndTurnCommitAtUtc = DateTime.UtcNow + EndTurnGraceInterval;
                Log.Info($"[AITeammate] Player={PlayerId} scheduled delayed end turn actionId={actionId} graceMs={(int)EndTurnGraceInterval.TotalMilliseconds}");
            }

            _nextDecisionAtUtc = DateTime.UtcNow + IdleTickInterval;
            return;
        }

        Log.Info($"[AITeammate] Player={PlayerId} chose actionId={actionId}");
        _nextDecisionAtUtc = DateTime.UtcNow + IdleTickInterval;
        if (!TryExecuteActionById(actionId))
        {
            _nextDecisionAtUtc = DateTime.UtcNow + IdleTickInterval;
        }
    }

    private void ClearPendingEndTurn(string reason)
    {
        if (string.IsNullOrEmpty(_pendingEndTurnActionId))
        {
            return;
        }

        Log.Info($"[AITeammate] Player={PlayerId} canceled delayed end turn actionId={_pendingEndTurnActionId} reason={reason}");
        _pendingEndTurnActionId = null;
        _pendingEndTurnActionSetFingerprint = null;
        _pendingEndTurnCommitAtUtc = DateTime.MinValue;
    }

    private string BuildDecisionRequestId()
    {
        return $"player_{PlayerId}_request_{DateTime.UtcNow.Ticks}";
    }

    private string BuildDecisionSnapshotId()
    {
        return $"player_{PlayerId}_ticks_{DateTime.UtcNow.Ticks}";
    }

    private bool ShouldSuppressRepeatedEndTurn(
        IReadOnlyList<AiTeammateAvailableAction> decisionActions,
        Player player,
        bool isCombatDecision)
    {
        if (!isCombatDecision)
        {
            return false;
        }

        int currentRound = player.Creature.CombatState?.RoundNumber ?? -1;
        if (currentRound != _lastCompletedEndTurnRound)
        {
            return false;
        }

        if (decisionActions.Count == 0 || decisionActions.Any(action => !IsEndTurnAction(action)))
        {
            return false;
        }

        Log.Info($"[AITeammate] Player={PlayerId} suppressing repeated end turn for round={currentRound} while combat state settles.");
        return true;
    }

    private void ResetCompletedEndTurnTrackingIfNeeded(Player? player, bool isCombatDecision)
    {
        if (!isCombatDecision || player?.Creature?.CombatState == null)
        {
            _lastCompletedEndTurnRound = -1;
            _lastCombatRoundWithInitialStagger = -1;
            return;
        }

        int currentRound = player.Creature.CombatState.RoundNumber;
        if (currentRound != _lastCompletedEndTurnRound)
        {
            _lastCompletedEndTurnRound = -1;
        }
    }

    private bool TryApplyInitialCombatDecisionStagger(
        IReadOnlyList<AiTeammateAvailableAction> decisionActions,
        Player player,
        bool isCombatDecision)
    {
        if (!isCombatDecision || player.Creature?.CombatState == null)
        {
            _lastCombatRoundWithInitialStagger = -1;
            return false;
        }

        if (decisionActions.Count == 0 || decisionActions.All(IsEndTurnAction))
        {
            return false;
        }

        int currentRound = player.Creature.CombatState.RoundNumber;
        if (currentRound == _lastCombatRoundWithInitialStagger)
        {
            return false;
        }

        _lastCombatRoundWithInitialStagger = currentRound;
        int delayMs = Random.Shared.Next(0, (int)MaxInitialCombatDecisionStagger.TotalMilliseconds + 1);
        if (delayMs <= 0)
        {
            return false;
        }

        _nextDecisionAtUtc = DateTime.UtcNow + TimeSpan.FromMilliseconds(delayMs);
        Log.Info($"[AITeammate] Player={PlayerId} applying initial combat decision stagger round={currentRound} delayMs={delayMs}");
        return true;
    }

    private static bool IsCombatEndTurnAction(string actionId)
    {
        return string.Equals(actionId, "end_turn", StringComparison.Ordinal)
               || actionId.StartsWith("end_turn_", StringComparison.Ordinal);
    }

    private static bool IsEndTurnAction(AiTeammateAvailableAction action)
    {
        return string.Equals(action.ActionType, AiTeammateActionKind.EndTurn.ToString(), StringComparison.Ordinal)
               || IsCombatEndTurnAction(action.ActionId);
    }

    private void BeginIssuedActionSettlement(AiTeammateAvailableAction action, AiActionExecutionResult executionResult)
    {
        _pendingIssuedActionSettlement = new PendingIssuedActionSettlement
        {
            ActionId = action.ActionId,
            ActionType = action.ActionType,
            GameAction = executionResult.GameAction!,
            IssuedAtUtc = DateTime.UtcNow,
            WaitForQueueSettle = executionResult.WaitForQueueSettle
        };
    }

    private bool TryWaitForIssuedActionSettlement()
    {
        if (_pendingIssuedActionSettlement == null)
        {
            return false;
        }

        PendingIssuedActionSettlement settlement = _pendingIssuedActionSettlement;
        DateTime now = DateTime.UtcNow;

        if (!settlement.ActionCompleted)
        {
            if (settlement.GameAction.CompletionTask.IsCompleted ||
                settlement.GameAction.State is GameActionState.Finished or GameActionState.Canceled)
            {
                settlement.ActionCompleted = true;
                settlement.ActionCompletedAtUtc = now;
                if (IsCombatEndTurnAction(settlement.ActionId) &&
                    TryGetControlledPlayer(out Player controlledPlayer, out _))
                {
                    settlement.CompletedEndTurnRound = controlledPlayer.Creature.CombatState?.RoundNumber;
                }

                Log.Info($"[AITeammate] Player={PlayerId} action settled actionId={settlement.ActionId} state={settlement.GameAction.State}");
            }
            else if (now - settlement.IssuedAtUtc >= ActionSettleTimeout)
            {
                settlement.ActionCompleted = true;
                settlement.ActionCompletedAtUtc = now;
                settlement.WasTimeoutFallback = true;
                Log.Warn($"[AITeammate] Player={PlayerId} action settle timeout actionId={settlement.ActionId} state={settlement.GameAction.State}; falling back to queue settle check.");
            }
            else
            {
                _nextDecisionAtUtc = now + IdleTickInterval;
                return true;
            }
        }

        if (settlement.WaitForQueueSettle && !settlement.QueueSettled)
        {
            if (IsQueueSettledForReplan(settlement))
            {
                settlement.QueueSettled = true;
                settlement.QueueSettledAtUtc = now;
                settlement.GraceEndsAtUtc = now + PostSettleGraceInterval;
                Log.Info($"[AITeammate] Player={PlayerId} queue settled after actionId={settlement.ActionId}; graceMs={(int)PostSettleGraceInterval.TotalMilliseconds}");
            }
            else if (settlement.ActionCompletedAtUtc.HasValue &&
                     now - settlement.ActionCompletedAtUtc.Value >= QueueSettleTimeout)
            {
                settlement.QueueSettled = true;
                settlement.QueueSettledAtUtc = now;
                settlement.GraceEndsAtUtc = now + PostSettleGraceInterval;
                settlement.WasTimeoutFallback = true;
                Log.Warn($"[AITeammate] Player={PlayerId} queue settle timeout after actionId={settlement.ActionId}; continuing after grace period.");
            }
            else
            {
                _nextDecisionAtUtc = now + IdleTickInterval;
                return true;
            }
        }

        if (settlement.GraceEndsAtUtc.HasValue && now < settlement.GraceEndsAtUtc.Value)
        {
            _nextDecisionAtUtc = settlement.GraceEndsAtUtc.Value;
            return true;
        }

        if (settlement.CompletedEndTurnRound.HasValue)
        {
            _lastCompletedEndTurnRound = settlement.CompletedEndTurnRound.Value;
        }

        Log.Info($"[AITeammate] Player={PlayerId} ready to replan after actionId={settlement.ActionId} timeoutFallback={settlement.WasTimeoutFallback}");
        _pendingIssuedActionSettlement = null;
        return false;
    }

    private bool IsQueueSettledForReplan(PendingIssuedActionSettlement settlement)
    {
        GameAction? runningAction = RunManager.Instance.ActionExecutor.CurrentlyRunningAction;
        if (runningAction != null &&
            ActionQueueSet.IsGameActionPlayerDriven(runningAction) &&
            runningAction.OwnerId == PlayerId)
        {
            return false;
        }

        GameAction? readyAction;
        try
        {
            readyAction = RunManager.Instance.ActionQueueSet.GetReadyAction();
        }
        catch (InvalidOperationException exception)
        {
            Log.Debug($"[AITeammate] Player={PlayerId} waiting for action queue to settle after actionId={settlement.ActionId}: {exception.Message}");
            return false;
        }

        if (readyAction != null &&
            ActionQueueSet.IsGameActionPlayerDriven(readyAction) &&
            readyAction.OwnerId == PlayerId)
        {
            return false;
        }

        Log.Debug($"[AITeammate] Player={PlayerId} treating queue as settled for actionId={settlement.ActionId}; runningOwner={runningAction?.OwnerId.ToString() ?? "none"} readyOwner={readyAction?.OwnerId.ToString() ?? "none"}");
        return true;
    }

    private static string DescribeTrackedAction(GameAction action)
    {
        return $"{action.GetType().Name}:{action.State}";
    }

    private bool TryGetControlledPlayer(out Player player, out RunState runState)
    {
        runState = RunManager.Instance.DebugOnlyGetState()!;
        player = runState?.GetPlayer(PlayerId)!;
        return runState != null && player != null;
    }

    private sealed class PendingIssuedActionSettlement
    {
        public required string ActionId { get; init; }

        public required string ActionType { get; init; }

        public required GameAction GameAction { get; init; }

        public required DateTime IssuedAtUtc { get; init; }

        public bool WaitForQueueSettle { get; init; }

        public bool ActionCompleted { get; set; }

        public DateTime? ActionCompletedAtUtc { get; set; }

        public bool QueueSettled { get; set; }

        public DateTime? QueueSettledAtUtc { get; set; }

        public DateTime? GraceEndsAtUtc { get; set; }

        public int? CompletedEndTurnRound { get; set; }

        public bool WasTimeoutFallback { get; set; }
    }
}
