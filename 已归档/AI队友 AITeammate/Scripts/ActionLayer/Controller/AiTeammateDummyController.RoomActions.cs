using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.AutoSlay.Helpers;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace AITeammate.Scripts;

internal sealed partial class AiTeammateDummyController
{
    private static readonly MethodInfo? EventChooseOptionForEventMethod =
        AccessTools.Method(typeof(EventSynchronizer), "ChooseOptionForEvent");
    private static readonly MethodInfo? EventVoteForSharedOptionMethod =
        AccessTools.Method(typeof(EventSynchronizer), "PlayerVotedForSharedOptionIndex");
    private static readonly MethodInfo? RestSiteChooseOptionMethod =
        AccessTools.Method(typeof(RestSiteSynchronizer), "ChooseOption");
    private static readonly MethodInfo? RestSiteTryEnableProceedButtonMethod =
        AccessTools.Method(typeof(NRestSiteRoom), "TryEnableProceedButton");
    private static readonly MethodInfo? RestSiteProceedButtonReleasedMethod =
        AccessTools.Method(typeof(NRestSiteRoom), "OnProceedButtonReleased");
    private static readonly FieldInfo? EventPageIndexField =
        AccessTools.Field(typeof(EventSynchronizer), "_pageIndex");

    private IReadOnlyList<AiTeammateAvailableAction> DiscoverEventActions(Player player)
    {
        EventSynchronizer synchronizer = RunManager.Instance.EventSynchronizer;
        if (synchronizer.Events.Count == 0)
        {
            return [];
        }

        EventModel eventForPlayer;
        try
        {
            if (synchronizer.IsShared && synchronizer.GetPlayerVote(player).HasValue)
            {
                return [];
            }

            eventForPlayer = synchronizer.GetEventForPlayer(player);
        }
        catch (InvalidOperationException exception)
        {
            Log.Debug($"[AITeammate][Event] Event discovery skipped player={PlayerId} reason={exception.Message}");
            return [];
        }
        catch (ArgumentOutOfRangeException exception)
        {
            Log.Debug($"[AITeammate][Event] Event discovery skipped player={PlayerId} reason={exception.Message}");
            return [];
        }

        IReadOnlyList<EventOption> options = eventForPlayer.CurrentOptions;
        string eventFingerprint = BuildEventActionFingerprint(synchronizer, eventForPlayer);
        if (eventForPlayer.IsFinished && options.Count == 0)
        {
            bool allEventsFinished = synchronizer.Events.Count > 0 &&
                                     synchronizer.Events.All(static eventModel => eventModel.IsFinished);
            if (synchronizer.IsShared && !allEventsFinished)
            {
                return BuildSharedEventFinishedReadyAction(synchronizer, player, eventFingerprint);
            }

            if (LocalContext.NetId == player.NetId)
            {
                return BuildLocalEventProceedAction(eventFingerprint);
            }

            return [];
        }

        EventPlanningInspection inspection = InspectCurrentEventPlan(player, synchronizer, eventForPlayer, eventFingerprint);
        EventExecutionSelection selection = ResolveEventExecutionSelection(
            player,
            synchronizer,
            eventForPlayer,
            inspection,
            eventFingerprint,
            phase: "discover");

        if (selection.OptionIndex < 0 || selection.SelectedOption == null)
        {
            return [];
        }

        return
        [
            new AiTeammateAvailableAction(
                new AiLegalActionOption
                {
                    ActionId = BuildEventOptionActionId(eventFingerprint, selection.OptionIndex),
                    ActionType = AiTeammateActionKind.ChooseEventOption.ToString(),
                    Description = $"Choose event option {selection.SelectedOption.TextKey}",
                    Label = $"Event option {selection.SelectedOption.TextKey}",
                    Summary = $"Choose event option {selection.SelectedOption.TextKey}."
                },
                async () =>
                {
                    EventModel liveEvent = synchronizer.GetEventForPlayer(player);
                    string liveEventFingerprint = BuildEventActionFingerprint(synchronizer, liveEvent);
                    EventPlanningInspection liveInspection = InspectCurrentEventPlan(player, synchronizer, liveEvent, liveEventFingerprint);
                    EventExecutionSelection liveSelection = ResolveEventExecutionSelection(
                        player,
                        synchronizer,
                        liveEvent,
                        liveInspection,
                        liveEventFingerprint,
                        phase: "execute");
                    if (liveSelection.OptionIndex < 0 || liveSelection.SelectedOption == null)
                    {
                        return AiActionExecutionResult.Completed;
                    }

                    if (string.Equals(liveSelection.SelectionMode, "planner", System.StringComparison.Ordinal))
                    {
                        Log.Info($"[AITeammate][Event] Executing planner-selected event option player={PlayerId} optionIndex={liveSelection.OptionIndex} textKey={liveSelection.SelectedOption.TextKey} title=\"{DescribeOptionTitle(liveSelection.SelectedOption)}\"");
                    }
                    else
                    {
                        Log.Info($"[AITeammate][Event] Executing fallback event option player={PlayerId} optionIndex={liveSelection.OptionIndex} textKey={liveSelection.SelectedOption.TextKey} title=\"{DescribeOptionTitle(liveSelection.SelectedOption)}\" reason={liveSelection.Reason}");
                    }

                    await ChooseEventOptionAsync(synchronizer, player, liveSelection.OptionIndex);
                    return AiActionExecutionResult.Completed;
                },
                $"{PlayerId}:event:{eventFingerprint}:{selection.OptionIndex}")
        ];
    }

    private IReadOnlyList<AiTeammateAvailableAction> BuildLocalEventProceedAction(string eventFingerprint)
    {
        return
        [
            new AiTeammateAvailableAction(
                new AiLegalActionOption
                {
                    ActionId = $"event_proceed_{SanitizeActionToken(eventFingerprint)}",
                    ActionType = AiTeammateActionKind.ChooseEventOption.ToString(),
                    Description = "Proceed from completed event.",
                    Label = "Proceed from event",
                    Summary = "Proceed from the completed event screen to the map."
                },
                async () =>
                {
                    if (NEventRoom.Instance == null)
                    {
                        Log.Debug($"[AITeammate][Event] Proceed skipped player={PlayerId} reason=event_room_node_missing");
                        return AiActionExecutionResult.Completed;
                    }

                    Log.Info($"[AITeammate][Event] Proceeding from completed event player={PlayerId} fingerprint={eventFingerprint}");
                    await NEventRoom.Proceed();
                    return AiActionExecutionResult.Completed;
                },
                $"{PlayerId}:event_proceed:{eventFingerprint}")
        ];
    }

    private IReadOnlyList<AiTeammateAvailableAction> BuildSharedEventFinishedReadyAction(
        EventSynchronizer synchronizer,
        Player player,
        string eventFingerprint)
    {
        uint pageIndex = GetEventPageIndex(synchronizer);
        int voteIndex = ResolveSharedFinishedEventReadyVote(synchronizer);
        return
        [
            new AiTeammateAvailableAction(
                new AiLegalActionOption
                {
                    ActionId = $"event_finished_ready_{pageIndex}_{SanitizeActionToken(eventFingerprint)}",
                    ActionType = AiTeammateActionKind.ChooseEventOption.ToString(),
                    Description = "Mark finished shared event player ready.",
                    Label = "Shared event ready",
                    Summary = $"Mark the finished shared event player ready on page {pageIndex}."
                },
                async () =>
                {
                    uint livePageIndex = GetEventPageIndex(synchronizer);
                    int liveVoteIndex = ResolveSharedFinishedEventReadyVote(synchronizer);
                    Log.Info($"[AITeammate][Event] Voting ready for finished shared event player={PlayerId} voteIndex={liveVoteIndex} page={livePageIndex} fingerprint={eventFingerprint}");
                    EventVoteForSharedOptionMethod?.Invoke(synchronizer, new object[] { player, (uint)liveVoteIndex, livePageIndex });
                    await Task.CompletedTask;
                    return AiActionExecutionResult.Completed;
                },
                $"{PlayerId}:event_finished_ready:{pageIndex}:{eventFingerprint}:{voteIndex}")
        ];
    }

    private IReadOnlyList<AiTeammateAvailableAction> DiscoverRestSiteActions(Player player)
    {
        RestSiteSynchronizer synchronizer = RunManager.Instance.RestSiteSynchronizer;
        IReadOnlyList<RestSiteOption> options = synchronizer.GetOptionsForPlayer(player);
        RestSiteOption? preferredOption = SelectPreferredRestSiteOption(player, options);
        if (preferredOption == null)
        {
            if (options.Count == 0 && LocalContext.NetId == player.NetId)
            {
                return BuildLocalRestSiteProceedAction(player);
            }

            return [];
        }

        int optionIndex = options.ToList().IndexOf(preferredOption);
        return
        [
            new AiTeammateAvailableAction(
                new AiLegalActionOption
                {
                    ActionId = BuildRestSiteOptionActionId(preferredOption.OptionId, optionIndex),
                    ActionType = AiTeammateActionKind.ChooseRestSiteOption.ToString(),
                    Description = $"Choose rest site option {preferredOption.OptionId}",
                    Label = $"Rest site option {preferredOption.OptionId}",
                    Summary = $"Choose rest site option {preferredOption.OptionId}."
                },
                async () =>
                {
                    await ChooseRestSiteOptionAsync(synchronizer, player, optionIndex);
                    return AiActionExecutionResult.Completed;
                })
        ];
    }

    private IReadOnlyList<AiTeammateAvailableAction> BuildLocalRestSiteProceedAction(Player player)
    {
        NRestSiteRoom? restSiteRoom = NRestSiteRoom.Instance;
        TryRefreshRestSiteProceedButton(restSiteRoom, "discover");
        if (restSiteRoom?.ProceedButton is not { IsEnabled: true })
        {
            return [];
        }

        string deduplicationKey = $"{PlayerId}:rest_site_proceed:{player.RunState.CurrentRoomCount}";
        return
        [
            new AiTeammateAvailableAction(
                new AiLegalActionOption
                {
                    ActionId = $"rest_site_proceed_{player.RunState.CurrentRoomCount}",
                    ActionType = AiTeammateActionKind.ChooseRestSiteOption.ToString(),
                    Description = "Proceed from rest site.",
                    Label = "Proceed from rest site",
                    Summary = "Proceed from the completed rest site screen to the map."
                },
                async () =>
                {
                    NRestSiteRoom? liveRestSiteRoom = NRestSiteRoom.Instance;
                    TryRefreshRestSiteProceedButton(liveRestSiteRoom, "execute");
                    if (liveRestSiteRoom?.ProceedButton is not { IsEnabled: true } liveProceedButton)
                    {
                        Log.Debug($"[AITeammate][Rest] Proceed skipped player={PlayerId} reason=proceed_button_disabled");
                        return AiActionExecutionResult.RetrySoon;
                    }

                    Log.Info($"[AITeammate][Rest] Proceeding from rest site player={PlayerId} roomCount={player.RunState.CurrentRoomCount}");
                    bool pressedDirectly = TryPressRestSiteProceedButton(liveRestSiteRoom, liveProceedButton);
                    if (!pressedDirectly)
                    {
                        await UiHelper.Click(liveProceedButton);
                    }

                    await Task.Delay(800);
                    return player.RunState.CurrentRoom is MegaCrit.Sts2.Core.Rooms.RestSiteRoom
                        ? AiActionExecutionResult.RetrySoon
                        : AiActionExecutionResult.Completed;
                },
                deduplicationKey)
        ];
    }

    private static bool TryPressRestSiteProceedButton(NRestSiteRoom restSiteRoom, object proceedButton)
    {
        if (RestSiteProceedButtonReleasedMethod == null)
        {
            return false;
        }

        try
        {
            RestSiteProceedButtonReleasedMethod.Invoke(restSiteRoom, new[] { proceedButton });
            return true;
        }
        catch (Exception exception)
        {
            Log.Debug($"[AITeammate][Rest] Direct proceed press failed reason={exception.Message}");
            return false;
        }
    }

    private static void TryRefreshRestSiteProceedButton(NRestSiteRoom? restSiteRoom, string phase)
    {
        if (restSiteRoom == null)
        {
            return;
        }

        try
        {
            RestSiteTryEnableProceedButtonMethod?.Invoke(restSiteRoom, Array.Empty<object>());
        }
        catch (Exception exception)
        {
            Log.Debug($"[AITeammate][Rest] Proceed refresh failed phase={phase} reason={exception.Message}");
        }
    }

    private static RestSiteOption? SelectPreferredRestSiteOption(Player player, IReadOnlyList<RestSiteOption> options)
    {
        RestSiteOption? smith = options.FirstOrDefault(static option => option.OptionId == "SMITH" && option.IsEnabled);
        RestSiteOption? heal = options.FirstOrDefault(static option => option.OptionId is "HEAL" or "REST" && option.IsEnabled);
        if (smith != null && ShouldUpgradeAtRestSite(player, heal != null))
        {
            return smith;
        }

        return heal ?? smith ?? options.FirstOrDefault(static option => option.IsEnabled);
    }

    private static bool ShouldUpgradeAtRestSite(Player player, bool canHeal)
    {
        if (!canHeal)
        {
            return true;
        }

        int currentHp = player.Creature.CurrentHp;
        int maxHp = Math.Max(player.Creature.MaxHp, 1);
        double hpRatio = currentHp / (double)maxHp;
        double bestUpgradeValue = EstimateBestRestSiteUpgradeValue(player);

        if (currentHp >= maxHp - 6)
        {
            return true;
        }

        if (currentHp <= 16 || hpRatio <= 0.34d)
        {
            return false;
        }

        if (IsThreateningMapPointSoon(player, maxDepth: 2) && hpRatio < 0.58d)
        {
            return false;
        }

        if (IsThreateningMapPointSoon(player, maxDepth: 1) && hpRatio < 0.68d && bestUpgradeValue < 14d)
        {
            return false;
        }

        if (bestUpgradeValue >= 16d && currentHp >= 22 && hpRatio >= 0.48d)
        {
            return true;
        }

        if (currentHp >= 24 && hpRatio >= 0.55d)
        {
            return true;
        }

        if (currentHp >= 18 && hpRatio >= 0.66d)
        {
            return true;
        }

        return false;
    }

    private static double EstimateBestRestSiteUpgradeValue(Player player)
    {
        return PileType.Deck.GetPile(player).Cards
            .Where(static card => card.IsUpgradable)
            .Select(card => ScoreUpgradeCandidate(card, player))
            .DefaultIfEmpty(double.NegativeInfinity)
            .Max();
    }

    private static bool IsThreateningMapPointSoon(Player player, int maxDepth)
    {
        MapPoint? current = player.RunState.CurrentMapPoint;
        if (current == null)
        {
            return false;
        }

        Queue<(MapPoint Point, int Depth)> queue = new();
        foreach (MapPoint child in current.Children)
        {
            queue.Enqueue((child, 1));
        }

        HashSet<MapPoint> seen = [];
        while (queue.Count > 0)
        {
            (MapPoint point, int depth) = queue.Dequeue();
            if (!seen.Add(point) || depth > maxDepth)
            {
                continue;
            }

            if (point.PointType is MapPointType.Elite or MapPointType.Boss)
            {
                return true;
            }

            foreach (MapPoint child in point.Children)
            {
                queue.Enqueue((child, depth + 1));
            }
        }

        return false;
    }

    private static string BuildEventActionFingerprint(EventSynchronizer synchronizer, EventModel eventForPlayer)
    {
        uint pageIndex = GetEventPageIndex(synchronizer);
        Player? owner = eventForPlayer.Owner;
        string ownerState = owner != null
            ? $"owner={owner.NetId};hp={owner.Creature.CurrentHp}/{owner.Creature.MaxHp};gold={owner.Gold}"
            : "owner=none";
        string optionFingerprint = string.Join(
            ",",
            eventForPlayer.CurrentOptions.Select(static option => $"{option.TextKey}:{option.IsLocked}:{option.IsProceed}"));
        return $"{eventForPlayer.Id}|finished={eventForPlayer.IsFinished}|page={pageIndex}|{ownerState}|options={optionFingerprint}";
    }

    private static async Task ChooseEventOptionAsync(EventSynchronizer synchronizer, Player player, int optionIndex)
    {
        IDisposable? selectorScope = CanUseDirectSelectionAutomation(player)
            ? PushDeterministicCardSelector()
            : null;
        try
        {
            if (synchronizer.IsShared)
            {
                uint pageIndex = GetEventPageIndex(synchronizer);
                EventVoteForSharedOptionMethod?.Invoke(synchronizer, new object[] { player, (uint)optionIndex, pageIndex });
                await Task.CompletedTask;
                return;
            }

            EventChooseOptionForEventMethod?.Invoke(synchronizer, new object[] { player, optionIndex });
            await Task.CompletedTask;
        }
        finally
        {
            selectorScope?.Dispose();
        }
    }

    private static uint GetEventPageIndex(EventSynchronizer synchronizer)
    {
        return EventPageIndexField?.GetValue(synchronizer) is uint currentPageIndex
            ? currentPageIndex
            : 0u;
    }

    private static int ResolveSharedFinishedEventReadyVote(EventSynchronizer synchronizer)
    {
        foreach (EventModel eventModel in synchronizer.Events)
        {
            if (eventModel.IsFinished || eventModel.Owner == null)
            {
                continue;
            }

            uint? existingVote = synchronizer.GetPlayerVote(eventModel.Owner);
            if (existingVote.HasValue)
            {
                return (int)existingVote.Value;
            }
        }

        foreach (EventModel eventModel in synchronizer.Events)
        {
            if (eventModel.IsFinished)
            {
                continue;
            }

            for (int index = 0; index < eventModel.CurrentOptions.Count; index++)
            {
                if (!eventModel.CurrentOptions[index].IsLocked)
                {
                    return index;
                }
            }
        }

        return 0;
    }

    private static async Task ChooseRestSiteOptionAsync(RestSiteSynchronizer synchronizer, Player player, int optionIndex)
    {
        if (RestSiteChooseOptionMethod?.Invoke(synchronizer, new object[] { player, optionIndex }) is Task<bool> task)
        {
            await task;
        }
    }

    private static string BuildEventOptionActionId(string eventFingerprint, int optionIndex)
    {
        return $"event_option_{optionIndex}_{SanitizeActionToken(eventFingerprint)}";
    }

    private static string BuildRestSiteOptionActionId(string optionId, int optionIndex)
    {
        return $"rest_site_option_{optionIndex}_{SanitizeActionToken(optionId)}";
    }
}
