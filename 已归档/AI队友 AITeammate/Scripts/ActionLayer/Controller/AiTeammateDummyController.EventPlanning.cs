using System;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;

namespace AITeammate.Scripts;

internal sealed partial class AiTeammateDummyController
{
    private static readonly EventSnapshotBuilder EventSnapshotBuilder = new();
    private static readonly EventPlanner EventPlanner = new();
    private string? _lastLoggedEventSnapshotFingerprint;

    private EventPlanningInspection InspectCurrentEventPlan(Player player, EventSynchronizer synchronizer, EventModel eventForPlayer, string eventFingerprint)
    {
        uint pageIndex = EventPageIndexField?.GetValue(synchronizer) is uint currentPageIndex
            ? currentPageIndex
            : 0u;
        EventVisitState snapshot = EventSnapshotBuilder.Build(synchronizer, player, pageIndex, eventFingerprint);
        EventPlannerResult plannerResult = EventPlanner.Evaluate(snapshot);
        EventPlanningInspection inspection = new()
        {
            Snapshot = snapshot,
            PlannerResult = plannerResult
        };

        if (string.Equals(_lastLoggedEventSnapshotFingerprint, eventFingerprint, StringComparison.Ordinal))
        {
            return inspection;
        }

        _lastLoggedEventSnapshotFingerprint = eventFingerprint;
        Log.Info($"[AITeammate][Event] Entered event discovery player={PlayerId} {snapshot.DescribeSummary()}");
        Log.Info($"[AITeammate][Event] Deck summary player={PlayerId} {snapshot.DescribeDeckSummary()}");
        Log.Info($"[AITeammate][Event] Description player={PlayerId} locKey={snapshot.DescriptionLocKey ?? "none"} text=\"{snapshot.DescriptionText}\"");
        Log.Info($"[AITeammate][Event] Relics player={PlayerId} [{string.Join(", ", snapshot.RelicIds.OrderBy(static id => id, StringComparer.Ordinal))}]");
        Log.Info($"[AITeammate][Event] Modifiers player={PlayerId} [{string.Join(", ", snapshot.ModifierIds.OrderBy(static id => id, StringComparer.Ordinal))}]");

        foreach (EventOptionDescriptor option in snapshot.Options)
        {
            Log.Info($"[AITeammate][Event] Normalized option player={PlayerId} {option.Describe()} locator={option.RuntimeLocator.Describe()}");
        }

        foreach (EventOptionEvaluation evaluation in plannerResult.RankedOptions)
        {
            Log.Info($"[AITeammate][Event] Option evaluation player={PlayerId} {evaluation.Describe()}");
        }

        if (plannerResult.BaselineOption != null)
        {
            double delta = plannerResult.BestOption.TotalScore - plannerResult.BaselineOption.TotalScore;
            Log.Info($"[AITeammate][Event] Baseline comparison player={PlayerId} best={plannerResult.BestOption.TextKey} baseline={plannerResult.BaselineOption.TextKey} delta={delta:F1}");
        }
        else
        {
            Log.Info($"[AITeammate][Event] Baseline comparison player={PlayerId} no explicit leave/proceed baseline on current page");
        }

        Log.Info($"[AITeammate][Event] Planner result player={PlayerId} {plannerResult.Describe()}");
        Log.Info($"[AITeammate][Event] Coverage gate player={PlayerId} overallTrust={plannerResult.OverallTrustLevel} bestSafeLater={plannerResult.IsBestOptionSafeForPlannerSelectionLater} supportedCount={plannerResult.SupportedOptionCount} fullyNormalizedCount={plannerResult.FullyNormalizedOptionCount} highTrustCount={plannerResult.HighTrustOptionCount}");
        Log.Info($"[AITeammate][Event] Would choose player={PlayerId} optionIndex={plannerResult.BestOption.OptionIndex} textKey={plannerResult.BestOption.TextKey} title=\"{plannerResult.BestOption.Title}\" reasons=[{string.Join("; ", plannerResult.BestOption.Reasons)}]");
        Log.Info($"[AITeammate][Event] Execution mode player={PlayerId} plannerGateActive=true fallback=first_unlocked_option currentEventType={eventForPlayer.GetType().Name}");
        return inspection;
    }

    private EventExecutionSelection ResolveEventExecutionSelection(
        Player player,
        EventSynchronizer synchronizer,
        EventModel eventForPlayer,
        EventPlanningInspection inspection,
        string expectedEventFingerprint,
        string phase)
    {
        EventOptionEvaluation bestOption = inspection.PlannerResult.BestOption;
        EventOption? fallbackOption = FindFallbackEventOption(eventForPlayer, bestOption);
        int fallbackOptionIndex = fallbackOption != null
            ? eventForPlayer.CurrentOptions
                .Select((option, index) => new { option, index })
                .Where(x => ReferenceEquals(x.option, fallbackOption))
                .Select(x => x.index)
                .DefaultIfEmpty(-1)
                .First()
            : -1;
        string currentEventFingerprint = BuildEventActionFingerprint(synchronizer, eventForPlayer);
        if (eventForPlayer.CurrentOptions.Count == 0)
        {
            return new EventExecutionSelection
            {
                OptionIndex = -1,
                SelectedOption = null,
                SelectionMode = "none",
                Reason = "no_event_options_on_page",
                PlannerEvaluation = bestOption,
                SuppressNoSelectionLog = true
            };
        }

        string? denyReason = ValidatePlannerExecutionEligibility(
            player,
            synchronizer,
            eventForPlayer,
            inspection,
            expectedEventFingerprint,
            currentEventFingerprint,
            out EventOption? plannerOption);

        if (denyReason == null && plannerOption != null)
        {
            Log.Info(
                $"[AITeammate][Event] Planner execution gate player={PlayerId} phase={phase} allowed=true reason=high_confidence_safeLater optionIndex={bestOption.OptionIndex} textKey={bestOption.TextKey} title=\"{DescribeOptionTitle(plannerOption)}\" support={bestOption.SupportLevel} trust={bestOption.TrustLevel} safeLater={bestOption.IsSafeForPlannerSelectionLater} fingerprint={currentEventFingerprint}");
            return new EventExecutionSelection
            {
                OptionIndex = bestOption.OptionIndex,
                SelectedOption = plannerOption,
                SelectionMode = "planner",
                Reason = "high_confidence_safeLater",
                PlannerEvaluation = bestOption
            };
        }

        Log.Info(
            $"[AITeammate][Event] Planner execution gate player={PlayerId} phase={phase} allowed=false reason={denyReason ?? "unknown"} optionIndex={bestOption.OptionIndex} textKey={bestOption.TextKey} title=\"{bestOption.Title}\" support={bestOption.SupportLevel} trust={bestOption.TrustLevel} safeLater={bestOption.IsSafeForPlannerSelectionLater} fingerprintExpected={expectedEventFingerprint} fingerprintCurrent={currentEventFingerprint}");

        if (fallbackOption != null)
        {
            Log.Info(
                $"[AITeammate][Event] Falling back to safe_unlocked_option player={PlayerId} phase={phase} reason={denyReason ?? "unknown"} fallbackOptionIndex={fallbackOptionIndex} textKey={fallbackOption.TextKey} title=\"{fallbackOption.Title}\" proceedOrLeave={IsProceedOrLeaveOption(fallbackOption, bestOption)}");
            return new EventExecutionSelection
            {
                OptionIndex = fallbackOptionIndex,
                SelectedOption = fallbackOption,
                SelectionMode = "fallback",
                Reason = denyReason ?? "unknown",
                PlannerEvaluation = bestOption
            };
        }

        if (!string.Equals(denyReason, "planner_option_missing", StringComparison.Ordinal))
        {
            Log.Warn(
                $"[AITeammate][Event] No selectable event option player={PlayerId} phase={phase} denyReason={denyReason ?? "unknown"}");
        }

        return new EventExecutionSelection
        {
            OptionIndex = -1,
            SelectedOption = null,
            SelectionMode = "none",
            Reason = fallbackOption == null ? "no_unlocked_options" : (denyReason ?? "unknown"),
            PlannerEvaluation = bestOption,
            SuppressNoSelectionLog = string.Equals(denyReason, "planner_option_missing", StringComparison.Ordinal)
        };
    }

    private string? ValidatePlannerExecutionEligibility(
        Player player,
        EventSynchronizer synchronizer,
        EventModel eventForPlayer,
        EventPlanningInspection inspection,
        string expectedEventFingerprint,
        string currentEventFingerprint,
        out EventOption? plannerOption)
    {
        plannerOption = null;
        EventPlannerResult plannerResult = inspection.PlannerResult;
        EventOptionEvaluation bestOption = plannerResult.BestOption;

        if (player.RunState.CurrentRoom is not EventRoom)
        {
            return "current_room_not_event";
        }

        if (bestOption.OptionIndex < 0)
        {
            return "planner_option_missing";
        }

        if (!plannerResult.IsBestOptionSafeForPlannerSelectionLater || !bestOption.IsSafeForPlannerSelectionLater)
        {
            return "safeLater_false";
        }

        if (plannerResult.OverallTrustLevel != EventPlannerTrustLevel.High)
        {
            return "trust_not_high";
        }

        if (bestOption.TrustLevel != EventPlannerTrustLevel.High)
        {
            return "best_option_trust_not_high";
        }

        if (bestOption.SupportLevel != EventSupportLevel.GenericHighConfidence &&
            bestOption.SupportLevel != EventSupportLevel.SpecialHighConfidence)
        {
            return "support_not_high_confidence";
        }

        if (!string.Equals(eventForPlayer.Id.Entry, inspection.Snapshot.EventId, StringComparison.Ordinal))
        {
            return "event_id_mismatch";
        }

        uint currentPageIndex = EventPageIndexField?.GetValue(synchronizer) is uint pageIndex
            ? pageIndex
            : 0u;
        if (currentPageIndex != inspection.Snapshot.PageIndex)
        {
            return "page_index_mismatch";
        }

        if (!string.Equals(currentEventFingerprint, expectedEventFingerprint, StringComparison.Ordinal))
        {
            return "event_fingerprint_mismatch";
        }

        if (bestOption.OptionIndex >= eventForPlayer.CurrentOptions.Count)
        {
            return "planner_option_out_of_range";
        }

        plannerOption = eventForPlayer.CurrentOptions[bestOption.OptionIndex];
        if (plannerOption.IsLocked)
        {
            plannerOption = null;
            return "planner_option_locked";
        }

        if (!string.Equals(plannerOption.TextKey, bestOption.TextKey, StringComparison.Ordinal))
        {
            plannerOption = null;
            return "planner_option_text_mismatch";
        }

        return null;
    }

    private static EventOption? FindFallbackEventOption(EventModel eventForPlayer, EventOptionEvaluation bestOption)
    {
        if (bestOption.OptionIndex >= 0 &&
            bestOption.OptionIndex < eventForPlayer.CurrentOptions.Count)
        {
            EventOption plannerOption = eventForPlayer.CurrentOptions[bestOption.OptionIndex];
            if (!plannerOption.IsLocked && IsProceedOrLeaveOption(plannerOption, bestOption))
            {
                return plannerOption;
            }
        }

        return eventForPlayer.CurrentOptions.FirstOrDefault(static option => !option.IsLocked && option.IsProceed)
               ?? eventForPlayer.CurrentOptions.FirstOrDefault(option => !option.IsLocked && IsProceedOrLeaveOption(option, null))
               ?? eventForPlayer.CurrentOptions.FirstOrDefault(static option => !option.IsLocked);
    }

    private static bool IsProceedOrLeaveOption(EventOption option, EventOptionEvaluation? evaluation)
    {
        if (option.IsProceed ||
            evaluation?.IsBaselineOption == true ||
            evaluation?.Option.Outcome.ProceedLike == true ||
            evaluation?.Option.Outcome.LeaveLike == true)
        {
            return true;
        }

        string token = $"{option.TextKey}|{evaluation?.TextKey ?? string.Empty}|{evaluation?.Title ?? string.Empty}".ToUpperInvariant();
        return token.Contains("PROCEED", StringComparison.Ordinal) ||
               token.Contains("CONTINUE", StringComparison.Ordinal) ||
               token.Contains("LEAVE", StringComparison.Ordinal) ||
               token.Contains("EXIT", StringComparison.Ordinal) ||
               token.Contains("RETURN", StringComparison.Ordinal) ||
               token.Contains("DEPART", StringComparison.Ordinal);
    }

    private static string DescribeOptionTitle(EventOption option)
    {
        return DescribeLocString(option.Title);
    }

    private static string DescribeLocString(LocString? locString)
    {
        if (locString == null || locString.IsEmpty)
        {
            return "none";
        }

        try
        {
            string formatted = locString.GetFormattedText();
            if (!string.IsNullOrWhiteSpace(formatted))
            {
                return formatted.Replace('"', '\'');
            }
        }
        catch (LocException)
        {
        }

        try
        {
            string raw = locString.GetRawText();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                return raw.Replace('"', '\'');
            }
        }
        catch (LocException)
        {
        }

        return $"{locString.LocTable}.{locString.LocEntryKey}";
    }

    private sealed class EventPlanningInspection
    {
        public required EventVisitState Snapshot { get; init; }

        public required EventPlannerResult PlannerResult { get; init; }
    }

    private sealed class EventExecutionSelection
    {
        public required int OptionIndex { get; init; }

        public required EventOption? SelectedOption { get; init; }

        public required string SelectionMode { get; init; }

        public required string Reason { get; init; }

        public required EventOptionEvaluation PlannerEvaluation { get; init; }

        public bool SuppressNoSelectionLog { get; init; }
    }
}
