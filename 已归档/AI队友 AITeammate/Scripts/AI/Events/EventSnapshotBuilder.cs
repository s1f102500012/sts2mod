using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;

namespace AITeammate.Scripts;

internal sealed class EventSnapshotBuilder
{
    private readonly CardEvaluationContextFactory _cardContextFactory = new();

    public EventVisitState Build(EventSynchronizer synchronizer, Player player, uint pageIndex, string snapshotFingerprint)
    {
        EventModel runtimeEvent = synchronizer.GetEventForPlayer(player);
        CardEvaluationContext context = _cardContextFactory.Create(
            player,
            CardChoiceSource.Event,
            skipAllowed: true,
            debugSource: "event_snapshot");

        List<EventOptionDescriptor> options = runtimeEvent.CurrentOptions
            .Select((option, index) => BuildOptionDescriptor(runtimeEvent, option, index))
            .ToList();

        return new EventVisitState
        {
            PlayerId = player.NetId,
            Player = player,
            RuntimeEvent = runtimeEvent,
            EventId = runtimeEvent.Id.Entry,
            EventTypeName = runtimeEvent.GetType().Name,
            RoomType = player.RunState.CurrentRoom?.GetType().Name ?? "UnknownRoom",
            SnapshotFingerprint = snapshotFingerprint,
            PageIndex = pageIndex,
            IsShared = runtimeEvent.IsShared,
            IsDeterministic = runtimeEvent.IsDeterministic,
            IsFinished = runtimeEvent.IsFinished,
            Gold = player.Gold,
            CurrentHp = player.Creature.CurrentHp,
            MaxHp = player.Creature.MaxHp,
            DeckSummary = context.DeckSummary,
            DeckCards = context.DeckCards,
            RelicIds = context.RelicIds,
            ModifierIds = context.ModifierIds,
            DescriptionText = GetLocTextSafe(runtimeEvent.Description),
            DescriptionLocKey = runtimeEvent.Description?.LocEntryKey,
            Options = options
        };
    }

    private static EventOptionDescriptor BuildOptionDescriptor(EventModel runtimeEvent, EventOption option, int optionIndex)
    {
        List<string> hoverTipKinds = option.HoverTips?
            .Select(static hoverTip => hoverTip.GetType().Name)
            .ToList() ?? [];
        string titleText = GetLocTextSafe(option.Title);
        string descriptionText = GetLocTextSafe(option.Description);
        bool isLikelyLeave = LooksLikeLeaveOrExit(option.TextKey, titleText, descriptionText);
        bool willKillPlayer = runtimeEvent.Owner != null && (option.WillKillPlayer?.Invoke(runtimeEvent.Owner) ?? false);
        List<EventOptionKind> kinds = [];
        List<string> notes = [];
        List<string> unknownReasons = [];
        List<string> relicIds = [];
        bool fullyNormalized = false;
        string normalizationSource = "raw";

        if (option.IsProceed)
        {
            kinds.Add(EventOptionKind.Proceed);
            notes.Add("proceed option");
            fullyNormalized = true;
            normalizationSource = "generic_proceed";
        }

        if (isLikelyLeave)
        {
            kinds.Add(EventOptionKind.Leave);
            notes.Add("text matched leave/exit baseline");
            if (!option.IsProceed)
            {
                fullyNormalized = true;
                normalizationSource = "generic_leave";
            }
        }

        if (option.Relic != null)
        {
            kinds.Add(EventOptionKind.GainRelic);
            relicIds.Add(option.Relic.Id.Entry);
            notes.Add($"attachedRelic={option.Relic.Id.Entry}");
            if (!fullyNormalized)
            {
                normalizationSource = "generic_attached_relic";
            }
        }

        bool hasUnknownEffects = false;
        if (!fullyNormalized && !option.IsLocked)
        {
            hasUnknownEffects = true;
            kinds.Add(EventOptionKind.Unsupported);
            unknownReasons.Add("runtime option metadata does not expose complete effect semantics");
        }

        return new EventOptionDescriptor
        {
            OptionIndex = optionIndex,
            TextKey = option.TextKey,
            Title = titleText,
            Description = descriptionText,
            IsLocked = option.IsLocked,
            IsProceed = option.IsProceed,
            IsLikelyLeaveOrExit = isLikelyLeave,
            WillKillPlayer = willKillPlayer,
            IsFullyNormalized = fullyNormalized,
            NormalizationSource = normalizationSource,
            HandlerName = "GenericRuntimeSnapshot",
            SupportLevel = fullyNormalized ? EventSupportLevel.GenericHighConfidence : EventSupportLevel.GenericPartial,
            TrustLevel = fullyNormalized ? EventPlannerTrustLevel.Medium : EventPlannerTrustLevel.Low,
            IsSafeForPlannerSelectionLater = fullyNormalized && !hasUnknownEffects && !willKillPlayer,
            RuntimeLocator = new EventRuntimeLocator
            {
                LocatorId = $"event_option:{optionIndex}:{option.TextKey}",
                OptionIndex = optionIndex,
                TextKey = option.TextKey
            },
            RelicId = option.Relic?.Id.Entry,
            RelicName = GetLocTextSafe(option.Relic?.Title),
            HoverTipKinds = hoverTipKinds,
            Kinds = kinds,
            UnknownReasons = unknownReasons,
            Outcome = new EventOutcomeSummary
            {
                LeaveLike = isLikelyLeave,
                ProceedLike = option.IsProceed,
                HasUnknownEffects = hasUnknownEffects,
                RelicIds = relicIds,
                Notes = notes
            }
        };
    }

    private static bool LooksLikeLeaveOrExit(string textKey, string title, string description)
    {
        string combined = $"{textKey} {title} {description}".ToUpperInvariant();
        return combined.Contains("LEAVE", StringComparison.Ordinal) ||
               combined.Contains("EXIT", StringComparison.Ordinal) ||
               combined.Contains("ABSTAIN", StringComparison.Ordinal) ||
               combined.Contains("PROCEED", StringComparison.Ordinal) ||
               combined.Contains("IGNORE", StringComparison.Ordinal);
    }

    private static string GetLocTextSafe(LocString? locString)
    {
        if (locString == null || locString.IsEmpty)
        {
            return string.Empty;
        }

        try
        {
            string raw = locString.GetRawText();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                return raw;
            }
        }
        catch (LocException)
        {
        }

        return string.IsNullOrWhiteSpace(locString.LocEntryKey)
            ? string.Empty
            : locString.LocEntryKey;
    }
}
