using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.AutoSlay.Helpers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace AITeammate.Scripts;

internal static class AiTeammateMapAndTreasurePatches
{
    private static readonly TimeSpan AutoMapVoteRetryInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TreasureUiRetryInterval = TimeSpan.FromMilliseconds(700);
    private static readonly FieldInfo? TreasureRoomRelicCollectionField =
        AccessTools.Field(typeof(NTreasureRoom), "_relicCollection");
    private static readonly FieldInfo? RelicCollectionHoldersInUseField =
        AccessTools.Field(typeof(NTreasureRoomRelicCollection), "_holdersInUse");
    private static readonly CardEvaluationContextFactory CardContextFactory = new();
    private static string? _lastAutoMapVoteKey;
    private static DateTime _lastAutoMapVoteAtUtc = DateTime.MinValue;
    private static string? _lastTreasureUiActionKey;
    private static DateTime _lastTreasureUiActionAtUtc = DateTime.MinValue;
    private static readonly Dictionary<ulong, DateTime> PendingTreasureRelicVotes = [];

    public static void TryAutoSelectMapNode()
    {
        if (!AiTeammateSessionRegistry.AutopilotEnabled)
        {
            return;
        }

        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        if (runState == null ||
            !AiTeammateSessionRegistry.TryGetAutopilotHostPlayer(runState, out Player hostPlayer))
        {
            return;
        }

        NMapScreen? mapScreen = NMapScreen.Instance;
        if (mapScreen is not { IsOpen: true, IsTravelEnabled: true } || mapScreen.IsTraveling)
        {
            return;
        }

        if (!IsMapActionQueueIdle())
        {
            return;
        }

        MapVote? existingHostVote = RunManager.Instance.MapSelectionSynchronizer.GetVote(hostPlayer);
        if (existingHostVote.HasValue &&
            existingHostVote.Value.mapGenerationCount == RunManager.Instance.MapSelectionSynchronizer.MapGenerationCount)
        {
            return;
        }

        if (!TryChooseAutopilotDestination(runState, hostPlayer, out MapPoint destination, out ScoredMapCandidate scoredCandidate))
        {
            return;
        }

        MapLocation source = runState.MapLocation;
        MapVote vote = new()
        {
            coord = destination.coord,
            mapGenerationCount = RunManager.Instance.MapSelectionSynchronizer.MapGenerationCount
        };
        string voteKey = BuildAutoMapVoteKey(source, vote);
        DateTime now = DateTime.UtcNow;
        if (string.Equals(_lastAutoMapVoteKey, voteKey, StringComparison.Ordinal) &&
            now - _lastAutoMapVoteAtUtc < AutoMapVoteRetryInterval)
        {
            return;
        }

        _lastAutoMapVoteKey = voteKey;
        _lastAutoMapVoteAtUtc = now;
        Log.Info($"[AITeammate] Autopilot voting map node source={FormatMapLocation(source)} destination={destination.coord} type={destination.PointType} score={scoredCandidate.Score:F1} base={scoredCandidate.BaseScore:F1} path={scoredCandidate.PathSignalScore:F1} route={scoredCandidate.StrategicRouteScore:F1} future={scoredCandidate.FutureRewardScore:F1} oracle=[{scoredCandidate.FutureRewards?.Describe() ?? "none"}]");
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(
            new VoteForMapCoordAction(hostPlayer, source, vote));
    }

    public static void TryAutoHandleTreasureRoom()
    {
        AiTeammateSessionState? session = AiTeammateSessionRegistry.ActiveRunSession;
        bool hostAutopilotEnabled = AiTeammateSessionRegistry.AutopilotEnabled;
        if (session == null && !hostAutopilotEnabled)
        {
            return;
        }

        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        if (runState?.CurrentRoom is not TreasureRoom)
        {
            return;
        }

        NTreasureRoom? treasureRoom = NRun.Instance?.TreasureRoom;
        if (treasureRoom == null)
        {
            return;
        }

        if (hostAutopilotEnabled && TryOpenTreasureChest(treasureRoom, runState))
        {
            return;
        }

        if (session != null)
        {
            TryAutoVoteTreasureRelics(session, treasureRoom, hostAutopilotEnabled);
        }

        if (hostAutopilotEnabled)
        {
            TryProceedTreasureRoom(treasureRoom, runState);
        }
    }

    private static bool TryOpenTreasureChest(NTreasureRoom treasureRoom, RunState runState)
    {
        if (RunManager.Instance.TreasureRoomRelicSynchronizer.CurrentRelics == null ||
            treasureRoom.DefaultFocusedControl != null)
        {
            return false;
        }

        NClickableControl? chest = treasureRoom.GetNodeOrNull<NClickableControl>("Chest");
        if (chest == null || !chest.Visible)
        {
            return false;
        }

        string actionKey = $"treasure_open:{runState.CurrentRoomCount}";
        if (!ShouldRunTreasureUiAction(actionKey))
        {
            return true;
        }

        Log.Info($"[AITeammate] Autopilot opening treasure chest roomCount={runState.CurrentRoomCount}");
        TaskHelper.RunSafely(UiHelper.Click(chest));
        return true;
    }

    private static void TryAutoVoteTreasureRelics(
        AiTeammateSessionState session,
        NTreasureRoom treasureRoom,
        bool hostAutopilotEnabled)
    {
        TreasureRoomRelicSynchronizer synchronizer = RunManager.Instance.TreasureRoomRelicSynchronizer;
        IReadOnlyList<RelicModel>? currentRelics = synchronizer.CurrentRelics;
        if (currentRelics == null || currentRelics.Count == 0)
        {
            return;
        }

        if (!TryGetTreasureRelicHolders(treasureRoom, out List<NTreasureRoomRelicHolder> holders) ||
            holders.Count == 0)
        {
            return;
        }

        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        int voteCursor = 0;
        if (hostAutopilotEnabled &&
            runState?.GetPlayer(session.HostPlayerId) is { } hostPlayer)
        {
            TreasureRoomRelicSynchronizer.PlayerVote hostVote = synchronizer.GetPlayerVote(hostPlayer);
            if (!hostVote.voteReceived)
            {
                if (!ShouldEnqueueTreasureRelicVote(hostPlayer.NetId))
                {
                    voteCursor++;
                }
                else
                {
                    int chosenRelicIndex = ChooseAvailableTreasureRelicIndex(currentRelics, holders, voteCursor);
                    voteCursor++;
                    Log.Info($"[AITeammate] Auto-voting treasure relic for host player={session.HostPlayerId} relicIndex={chosenRelicIndex} mode=action_queue");
                    RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new PickRelicAction(hostPlayer, chosenRelicIndex));
                }
            }
            else
            {
                PendingTreasureRelicVotes.Remove(hostPlayer.NetId);
                voteCursor++;
            }
        }

        foreach (AiTeammateSessionParticipant participant in session.Participants.Where(static participant => !participant.IsHost))
        {
            Player? aiPlayer = runState?.GetPlayer(participant.PlayerId);
            if (aiPlayer == null)
            {
                continue;
            }

            TreasureRoomRelicSynchronizer.PlayerVote playerVote = synchronizer.GetPlayerVote(aiPlayer);
            if (playerVote.voteReceived)
            {
                PendingTreasureRelicVotes.Remove(participant.PlayerId);
                voteCursor++;
                continue;
            }

            if (!ShouldEnqueueTreasureRelicVote(participant.PlayerId))
            {
                voteCursor++;
                continue;
            }

            int chosenRelicIndex = ChooseAvailableTreasureRelicIndex(currentRelics, holders, voteCursor);
            Log.Info($"[AITeammate] Auto-voting treasure relic for AI player={participant.PlayerId} relicIndex={chosenRelicIndex}");
            RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new PickRelicAction(aiPlayer, chosenRelicIndex));
            voteCursor++;
        }
    }

    private static bool ShouldEnqueueTreasureRelicVote(ulong playerId)
    {
        DateTime now = DateTime.UtcNow;
        if (PendingTreasureRelicVotes.TryGetValue(playerId, out DateTime lastQueuedAtUtc) &&
            now - lastQueuedAtUtc < TreasureUiRetryInterval)
        {
            return false;
        }

        PendingTreasureRelicVotes[playerId] = now;
        return true;
    }

    private static int ChooseAvailableTreasureRelicIndex(
        IReadOnlyList<RelicModel> currentRelics,
        IReadOnlyList<NTreasureRoomRelicHolder> holders,
        int voteCursor)
    {
        int fallback = Math.Clamp(voteCursor, 0, currentRelics.Count - 1);
        int wrappedIndex = voteCursor % currentRelics.Count;
        return holders.Any(holder => holder.Index == wrappedIndex)
            ? wrappedIndex
            : fallback;
    }

    private static bool TryGetTreasureRelicHolders(
        NTreasureRoom treasureRoom,
        out List<NTreasureRoomRelicHolder> holders)
    {
        holders = [];
        if (TreasureRoomRelicCollectionField?.GetValue(treasureRoom) is not NTreasureRoomRelicCollection relicCollection ||
            RelicCollectionHoldersInUseField?.GetValue(relicCollection) is not List<NTreasureRoomRelicHolder> holdersInUse)
        {
            return false;
        }

        holders = holdersInUse
            .Where(static holder => holder.IsInsideTree())
            .ToList();
        return holders.Count > 0;
    }

    private static void TryProceedTreasureRoom(NTreasureRoom treasureRoom, RunState runState)
    {
        if (RunManager.Instance.TreasureRoomRelicSynchronizer.CurrentRelics != null ||
            treasureRoom.ProceedButton is not { IsEnabled: true } proceedButton)
        {
            return;
        }

        string actionKey = $"treasure_proceed:{runState.CurrentRoomCount}";
        if (!ShouldRunTreasureUiAction(actionKey))
        {
            return;
        }

        Log.Info($"[AITeammate] Autopilot proceeding from treasure room roomCount={runState.CurrentRoomCount}");
        TaskHelper.RunSafely(UiHelper.Click(proceedButton));
    }

    private static bool ShouldRunTreasureUiAction(string actionKey)
    {
        DateTime now = DateTime.UtcNow;
        if (string.Equals(_lastTreasureUiActionKey, actionKey, StringComparison.Ordinal) &&
            now - _lastTreasureUiActionAtUtc < TreasureUiRetryInterval)
        {
            return false;
        }

        _lastTreasureUiActionKey = actionKey;
        _lastTreasureUiActionAtUtc = now;
        return true;
    }

    private static bool IsMapActionQueueIdle()
    {
        if (RunManager.Instance.ActionExecutor.CurrentlyRunningAction != null)
        {
            return false;
        }

        try
        {
            return RunManager.Instance.ActionQueueSet.GetReadyAction() == null;
        }
        catch (InvalidOperationException exception)
        {
            Log.Debug($"[AITeammate] Autopilot waiting for map action queue to settle: {exception.Message}");
            return false;
        }
    }

    private static bool TryChooseAutopilotDestination(
        RunState runState,
        Player hostPlayer,
        out MapPoint destination,
        out ScoredMapCandidate scoredCandidate)
    {
        destination = null!;
        scoredCandidate = null!;
        IEnumerable<MapPoint> candidates;
        MapPoint? currentMapPoint = runState.CurrentMapPoint;
        if (currentMapPoint != null)
        {
            candidates = currentMapPoint.Children;
        }
        else if (!runState.CurrentMapCoord.HasValue)
        {
            candidates = new[] { runState.Map.StartingMapPoint };
        }
        else
        {
            return false;
        }

        List<MapPoint> candidateList = candidates
            .Where(candidate => runState.Map.HasPoint(candidate.coord))
            .ToList();
        IReadOnlyDictionary<MapPoint, FutureRewardRouteEvaluation> futureRewards =
            FutureRewardOracle.Shared.EvaluateImmediateChoices(runState, hostPlayer, candidateList);

        ScoredMapCandidate? bestCandidate = candidateList
            .Select(candidate => ScoreAutopilotMapPoint(
                runState,
                candidate,
                hostPlayer,
                futureRewards.TryGetValue(candidate, out FutureRewardRouteEvaluation? future)
                    ? future
                    : FutureRewardRouteEvaluation.Empty))
            .OrderByDescending(static candidate => candidate.Score)
            .ThenBy(candidate => GetColumnDistanceFromCurrent(runState, candidate.Point))
            .ThenBy(static candidate => candidate.Point.coord.col)
            .FirstOrDefault();
        if (bestCandidate == null)
        {
            return false;
        }

        destination = bestCandidate.Point;
        scoredCandidate = bestCandidate;
        return true;
    }

    private static ScoredMapCandidate ScoreAutopilotMapPoint(
        RunState runState,
        MapPoint point,
        Player hostPlayer,
        FutureRewardRouteEvaluation futureRewards)
    {
        double hpRatio = EstimatePartyHpRatio(runState, hostPlayer);
        DeckSummary deckSummary = BuildDeckSummary(hostPlayer);

        double baseScore = point.PointType switch
        {
            MapPointType.Boss => 1000d,
            MapPointType.Ancient => 950d,
            MapPointType.Elite when hpRatio >= 0.78d => 104d,
            MapPointType.Elite when hpRatio >= 0.62d => 86d,
            MapPointType.Elite when hpRatio >= 0.48d => 48d,
            MapPointType.Elite => -28d,
            MapPointType.RestSite when hpRatio <= 0.40d => 116d,
            MapPointType.RestSite when hpRatio <= 0.55d => 96d,
            MapPointType.RestSite when hpRatio <= 0.72d => 66d,
            MapPointType.RestSite => 34d,
            MapPointType.Treasure => 74d,
            MapPointType.Unknown when hpRatio <= 0.42d => 76d,
            MapPointType.Unknown => 72d,
            MapPointType.Shop when hostPlayer.Gold >= 180 => 104d,
            MapPointType.Shop when hostPlayer.Gold >= 130 => 88d,
            MapPointType.Shop when hostPlayer.Gold >= 75 => 58d,
            MapPointType.Shop => 24d,
            MapPointType.Monster when hpRatio <= 0.36d => 10d,
            MapPointType.Monster when hpRatio <= 0.50d => 34d,
            MapPointType.Monster => 58d,
            _ => 0d
        };

        baseScore += ScoreImmediateMapNodeRisk(runState, point, deckSummary, hpRatio);
        double pathSignalScore = ScoreNearbyPathSignals(point, hpRatio, hostPlayer.Gold);
        double strategicRouteScore = ScoreStrategicRouteProfile(runState, point, deckSummary, hpRatio, hostPlayer.Gold);
        double futureRewardScore = Math.Min(futureRewards.RewardValue * 0.22d, 30d) +
                                   ScoreFutureEncounterHazards(futureRewards);
        return new ScoredMapCandidate(
            point,
            baseScore + pathSignalScore + strategicRouteScore + futureRewardScore,
            baseScore,
            pathSignalScore,
            strategicRouteScore,
            futureRewardScore,
            futureRewards);
    }

    private static double EstimatePartyHpRatio(RunState runState, Player fallbackPlayer)
    {
        List<double> ratios = [];
        foreach (Player player in runState.Players)
        {
            if (player.Creature.MaxHp <= 0)
            {
                continue;
            }

            ratios.Add(Math.Clamp(player.Creature.CurrentHp / (double)player.Creature.MaxHp, 0d, 1d));
        }

        if (ratios.Count == 0)
        {
            return fallbackPlayer.Creature.MaxHp > 0
                ? Math.Clamp(fallbackPlayer.Creature.CurrentHp / (double)fallbackPlayer.Creature.MaxHp, 0d, 1d)
                : 0d;
        }

        double average = ratios.Average();
        double weakest = ratios.Min();
        return Math.Min(average, weakest * 1.25d);
    }

    private static double ScoreImmediateMapNodeRisk(
        RunState runState,
        MapPoint point,
        DeckSummary deck,
        double hpRatio)
    {
        if (point.PointType != MapPointType.Elite)
        {
            return 0d;
        }

        bool actTwoOrLater = runState.CurrentActIndex >= 1;
        double score = 0d;
        if (PhantasmalGardenersStrategy.IsPredictedNextEliteGardeners(runState))
        {
            score -= hpRatio < 0.82d ? 115d : 82d;
        }

        if (hpRatio < 0.58d)
        {
            score -= actTwoOrLater ? 115d : 72d;
        }
        else if (hpRatio < 0.70d)
        {
            score -= actTwoOrLater ? 68d : 34d;
        }
        else if (hpRatio < 0.78d && actTwoOrLater)
        {
            score -= 32d;
        }

        if (runState.CurrentActIndex <= 1 && deck.AoESources == 0)
        {
            score -= actTwoOrLater ? 28d : 22d;
        }

        if (runState.CurrentActIndex == 0)
        {
            if (deck.AoESources == 0)
            {
                score -= hpRatio < 0.88d ? 28d : 14d;
            }

            if (deck.WeakSources + deck.VulnerableSources == 0)
            {
                score -= 16d;
            }

            if (deck.FrontloadDamageSources < 6)
            {
                score -= 18d;
            }
        }

        if (deck.QualityDefenseSources < (deck.CardCount < 18 ? 3 : 4))
        {
            score -= 18d;
        }

        if (runState.ActFloor >= 8 && deck.ScalingSources == 0)
        {
            score -= 14d;
        }

        return score;
    }

    private static double ScoreFutureEncounterHazards(FutureRewardRouteEvaluation futureRewards)
    {
        return futureRewards.EventRewards.Any(static preview => PhantasmalGardenersStrategy.IsEncounterId(preview.EventId))
            ? -85d
            : 0d;
    }

    private static double ScoreNearbyPathSignals(MapPoint point, double hpRatio, int gold)
    {
        double score = 0d;
        int restDistance = FindNearestDescendantDistance(point, MapPointType.RestSite, maxDepth: 4);
        int shopDistance = FindNearestDescendantDistance(point, MapPointType.Shop, maxDepth: 4);
        int eliteDistance = FindNearestDescendantDistance(point, MapPointType.Elite, maxDepth: 4);

        if (hpRatio <= 0.42d)
        {
            if (restDistance >= 0)
            {
                score += 42d - restDistance * 9d;
            }
            else if (point.PointType is MapPointType.Monster or MapPointType.Elite or MapPointType.Unknown)
            {
                score -= point.PointType == MapPointType.Elite ? 58d : 24d;
            }
        }
        else if (hpRatio <= 0.58d && restDistance >= 0)
        {
            score += 22d - restDistance * 5d;
        }

        if (gold >= 180)
        {
            score += shopDistance >= 0 ? 42d - shopDistance * 6d : 0d;
        }
        else if (gold >= 130)
        {
            score += shopDistance >= 0 ? 28d - shopDistance * 5d : 0d;
        }

        if (hpRatio <= 0.55d && gold >= 150 && shopDistance >= 0)
        {
            score += 34d - shopDistance * 6d;
        }

        if (hpRatio >= 0.76d && eliteDistance >= 0)
        {
            score += 24d - eliteDistance * 5d;
        }
        else if (hpRatio <= 0.68d && eliteDistance >= 0)
        {
            score -= 38d - eliteDistance * 6d;
        }
        else if (hpRatio <= 0.48d && eliteDistance >= 0)
        {
            score -= 24d - eliteDistance * 4d;
        }

        return score;
    }

    private static double ScoreStrategicRouteProfile(
        RunState runState,
        MapPoint point,
        DeckSummary deck,
        double hpRatio,
        int gold)
    {
        double bestScore = double.NegativeInfinity;
        foreach (RouteProfile profile in EnumerateRouteProfiles(runState, point, maxDepth: 5))
        {
            bestScore = Math.Max(bestScore, ScoreRouteProfile(runState, deck, hpRatio, gold, profile));
        }

        return double.IsNegativeInfinity(bestScore) ? 0d : bestScore;
    }

    private static double ScoreRouteProfile(
        RunState runState,
        DeckSummary deck,
        double hpRatio,
        int gold,
        RouteProfile profile)
    {
        double score = 0d;
        bool earlyAct = runState.ActFloor <= 7;
        bool lateAct = runState.ActFloor >= 10;
        int desiredDamageSources = deck.CardCount < 15 ? 6 : 8;

        if (earlyAct)
        {
            int usefulEarlyCombats = Math.Min(profile.Monsters, Math.Max(0, 3 - profile.ElitesBeforeFirstRest));
            score += usefulEarlyCombats * (deck.FrontloadDamageSources < desiredDamageSources ? 8d : 4d);
        }

        if (profile.FirstEliteDistance >= 0)
        {
            bool hasEnoughDamage = deck.FrontloadDamageSources >= Math.Min(desiredDamageSources, 6);
            bool hasEliteTools = deck.AoESources > 0 ||
                                  deck.WeakSources + deck.VulnerableSources > 0 ||
                                  deck.QualityDefenseSources >= 4;
            if (hpRatio >= 0.86d && hasEnoughDamage && hasEliteTools)
            {
                score += 32d - profile.FirstEliteDistance * 3d;
            }
            else if (hpRatio >= 0.76d && hasEnoughDamage && hasEliteTools && deck.QualityDefenseSources >= 3)
            {
                score += 14d - profile.FirstEliteDistance * 2d;
            }
            else if (profile.FirstRestDistance < 0 || profile.FirstEliteDistance < profile.FirstRestDistance)
            {
                score -= hpRatio <= 0.45d ? 70d : 42d;
            }

            if (runState.CurrentActIndex == 0 &&
                profile.FirstEliteDistance <= 2 &&
                (!hasEliteTools || deck.QualityDefenseSources < 3))
            {
                score -= 22d;
            }
        }

        if (profile.Elites > 0 && runState.CurrentActIndex >= 1 && hpRatio < 0.70d)
        {
            score -= (0.70d - hpRatio) * 120d + profile.Elites * 18d;
        }

        if (profile.ElitesBeforeFirstRest > 0 && hpRatio < 0.72d)
        {
            score -= profile.ElitesBeforeFirstRest * (hpRatio <= 0.52d ? 42d : 24d);
        }

        if (profile.FirstRestDistance >= 0)
        {
            if (hpRatio <= 0.45d)
            {
                score += 46d - profile.FirstRestDistance * 8d;
            }
            else if (hpRatio <= 0.60d || lateAct)
            {
                score += 20d - profile.FirstRestDistance * 4d;
            }
            else if (hpRatio >= 0.82d)
            {
                score -= Math.Max(0d, 8d - profile.FirstRestDistance * 2d);
            }
        }

        if (profile.FirstShopDistance >= 0)
        {
            if (gold >= 180)
            {
                score += 46d - profile.FirstShopDistance * 6d;
                if (hpRatio <= 0.60d)
                {
                    score += 18d - profile.FirstShopDistance * 3d;
                }
            }
            else if (gold >= 130 || deck.BasicCards >= 7 || deck.BadCards > 0)
            {
                score += 28d - profile.FirstShopDistance * 5d;
            }
            else if (gold < 70)
            {
                score -= Math.Max(0d, 10d - profile.FirstShopDistance * 3d);
            }
        }

        if (deck.AoESources == 0 && runState.CurrentActIndex <= 1)
        {
            score += profile.Monsters * 2d;
            if (profile.Elites > 0 && hpRatio < 0.82d)
            {
                score -= 8d;
            }
        }

        if (profile.CombatsBeforeFirstRest >= 3 && hpRatio <= 0.52d)
        {
            score -= (profile.CombatsBeforeFirstRest - 2) * 12d;
        }

        if (profile.Unknowns > 0 && hpRatio <= 0.42d)
        {
            score += profile.Unknowns * 5d;
        }

        return Math.Clamp(score, -95d, 95d);
    }

    private static IEnumerable<RouteProfile> EnumerateRouteProfiles(RunState runState, MapPoint start, int maxDepth)
    {
        List<MapPoint> path = [];
        HashSet<MapPoint> visiting = [];
        foreach (RouteProfile profile in Enumerate(start, depth: 0))
        {
            yield return profile;
        }

        IEnumerable<RouteProfile> Enumerate(MapPoint point, int depth)
        {
            if (!visiting.Add(point))
            {
                yield break;
            }

            path.Add(point);
            if (depth >= maxDepth || point.Children.Count == 0)
            {
                yield return BuildRouteProfile(path);
            }
            else
            {
                bool yieldedChild = false;
                foreach (MapPoint child in point.Children.Where(child => runState.Map.HasPoint(child.coord)))
                {
                    foreach (RouteProfile childProfile in Enumerate(child, depth + 1))
                    {
                        yieldedChild = true;
                        yield return childProfile;
                    }
                }

                if (!yieldedChild)
                {
                    yield return BuildRouteProfile(path);
                }
            }

            path.RemoveAt(path.Count - 1);
            visiting.Remove(point);
        }
    }

    private static RouteProfile BuildRouteProfile(IReadOnlyList<MapPoint> path)
    {
        int monsters = 0;
        int elites = 0;
        int restSites = 0;
        int shops = 0;
        int treasures = 0;
        int unknowns = 0;
        int firstEliteDistance = -1;
        int firstRestDistance = -1;
        int firstShopDistance = -1;
        int combatsBeforeFirstRest = 0;
        int elitesBeforeFirstRest = 0;

        for (int i = 0; i < path.Count; i++)
        {
            MapPoint point = path[i];
            switch (point.PointType)
            {
                case MapPointType.Monster:
                    monsters++;
                    if (firstRestDistance < 0)
                    {
                        combatsBeforeFirstRest++;
                    }

                    break;
                case MapPointType.Elite:
                    elites++;
                    if (firstEliteDistance < 0)
                    {
                        firstEliteDistance = i;
                    }

                    if (firstRestDistance < 0)
                    {
                        combatsBeforeFirstRest++;
                        elitesBeforeFirstRest++;
                    }

                    break;
                case MapPointType.RestSite:
                    restSites++;
                    if (firstRestDistance < 0)
                    {
                        firstRestDistance = i;
                    }

                    break;
                case MapPointType.Shop:
                    shops++;
                    if (firstShopDistance < 0)
                    {
                        firstShopDistance = i;
                    }

                    break;
                case MapPointType.Treasure:
                    treasures++;
                    break;
                case MapPointType.Unknown:
                    unknowns++;
                    break;
            }
        }

        return new RouteProfile(
            monsters,
            elites,
            restSites,
            shops,
            treasures,
            unknowns,
            firstEliteDistance,
            firstRestDistance,
            firstShopDistance,
            combatsBeforeFirstRest,
            elitesBeforeFirstRest);
    }

    private static int FindNearestDescendantDistance(MapPoint root, MapPointType pointType, int maxDepth)
    {
        Queue<(MapPoint Point, int Depth)> queue = new();
        queue.Enqueue((root, 0));
        HashSet<MapPoint> seen = [];
        while (queue.Count > 0)
        {
            (MapPoint point, int depth) = queue.Dequeue();
            if (!seen.Add(point))
            {
                continue;
            }

            if (point.PointType == pointType)
            {
                return depth;
            }

            if (depth >= maxDepth)
            {
                continue;
            }

            foreach (MapPoint child in point.Children)
            {
                queue.Enqueue((child, depth + 1));
            }
        }

        return -1;
    }

    private static int GetColumnDistanceFromCurrent(RunState runState, MapPoint candidate)
    {
        MapCoord? currentCoord = runState.CurrentMapCoord;
        if (!currentCoord.HasValue)
        {
            currentCoord = runState.Map.StartingMapPoint.coord;
        }

        return Math.Abs(candidate.coord.col - currentCoord.Value.col);
    }

    private static DeckSummary BuildDeckSummary(Player player)
    {
        try
        {
            return CardContextFactory.Create(
                player,
                CardChoiceSource.ForcedChoice,
                skipAllowed: false,
                debugSource: "map_route_strategy").DeckSummary;
        }
        catch (Exception exception)
        {
            Log.Debug($"[AITeammate] Failed to summarize deck for map routing player={player.NetId}: {exception.Message}");
            return new DeckSummary();
        }
    }

    private sealed record ScoredMapCandidate(
        MapPoint Point,
        double Score,
        double BaseScore,
        double PathSignalScore,
        double StrategicRouteScore,
        double FutureRewardScore,
        FutureRewardRouteEvaluation? FutureRewards);

    private readonly record struct RouteProfile(
        int Monsters,
        int Elites,
        int RestSites,
        int Shops,
        int Treasures,
        int Unknowns,
        int FirstEliteDistance,
        int FirstRestDistance,
        int FirstShopDistance,
        int CombatsBeforeFirstRest,
        int ElitesBeforeFirstRest);

    private static string BuildAutoMapVoteKey(MapLocation source, MapVote vote)
    {
        return $"{FormatMapLocation(source)}|gen={vote.mapGenerationCount}|dest={vote.coord.col},{vote.coord.row}";
    }

    private static string FormatMapLocation(MapLocation location)
    {
        return location.coord.HasValue
            ? $"act={location.actIndex}:{location.coord.Value.col},{location.coord.Value.row}"
            : $"act={location.actIndex}:start";
    }

    [HarmonyPatch(typeof(MapSelectionSynchronizer), nameof(MapSelectionSynchronizer.PlayerVotedForMapCoord))]
    private static class MapSelectionSynchronizerPatch
    {
        private static void Postfix(Player player, MapLocation source, MapVote? destination)
        {
            AiTeammateSessionState? session = AiTeammateSessionRegistry.ActiveRunSession;
            if (session == null ||
                player.NetId != session.HostPlayerId)
            {
                return;
            }

            foreach (AiTeammateSessionParticipant participant in session.Participants.Where(static participant => !participant.IsHost))
            {
                Player? aiPlayer = RunManager.Instance.DebugOnlyGetState()?.GetPlayer(participant.PlayerId);
                if (aiPlayer == null)
                {
                    continue;
                }

                MapVote? existingVote = RunManager.Instance.MapSelectionSynchronizer.GetVote(aiPlayer);
                if (existingVote.Equals(destination))
                {
                    continue;
                }

                RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(
                    new VoteForMapCoordAction(aiPlayer, source, destination));
            }
        }
    }

    [HarmonyPatch(typeof(TreasureRoomRelicSynchronizer), nameof(TreasureRoomRelicSynchronizer.BeginRelicPicking))]
    private static class TreasureRoomRelicSynchronizerBeginRelicPickingPatch
    {
        private static void Postfix(TreasureRoomRelicSynchronizer __instance)
        {
            AiTeammateSessionState? session = AiTeammateSessionRegistry.ActiveRunSession;
            if (session == null ||
                RunManager.Instance.NetService is not AiTeammateLoopbackHostGameService)
            {
                return;
            }

            IReadOnlyList<RelicModel>? currentRelics = __instance.CurrentRelics;
            if (currentRelics == null || currentRelics.Count == 0)
            {
                Log.Info("[AITeammate] Treasure relic picking started with no relic options.");
                return;
            }

            Log.Info($"[AITeammate] Treasure relic picking started. relicCount={currentRelics.Count}; waiting for treasure UI before AI votes.");
        }
    }
}
