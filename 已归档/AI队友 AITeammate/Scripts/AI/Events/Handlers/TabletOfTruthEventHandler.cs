using System;
using System.Linq;

namespace AITeammate.Scripts;

internal sealed class TabletOfTruthEventHandler : EventSpecialHandlerBase
{
    private const int SmashHealAmount = 20;
    private const string NormalizationSource = "special:TabletOfTruth";

    public override string HandlerName => nameof(TabletOfTruthEventHandler);

    protected override string EventTypeName => "TabletOfTruth";

    public override EventOptionDescriptor Normalize(EventVisitState snapshot, EventOptionDescriptor option)
    {
        string textKey = option.TextKey;
        if (textKey.Contains(".SMASH", StringComparison.Ordinal))
        {
            int healAmount = Math.Max(0, Math.Min(SmashHealAmount, snapshot.MaxHp - snapshot.CurrentHp));
            return WithKnownOutcome(option, HandlerName, NormalizationSource, true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.Heal], new EventOutcomeSummary
            {
                HpDelta = healAmount,
                Notes = ["heal up to 20 hp"]
            });
        }

        if (textKey.Contains(".GIVE_UP", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, NormalizationSource, true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.Leave, EventOptionKind.Proceed], new EventOutcomeSummary
            {
                LeaveLike = true,
                ProceedLike = true,
                Notes = ["stop deciphering and leave the event"]
            });
        }

        if (textKey.Contains(".INITIAL.options.DECIPHER_1", StringComparison.Ordinal))
        {
            return BuildDecipherOption(snapshot, option, 3, "lose 3 max hp and randomly upgrade one card");
        }

        if (textKey.Contains(".DECIPHER_1.options.DECIPHER", StringComparison.Ordinal))
        {
            return BuildDecipherOption(snapshot, option, 6, "lose 6 max hp and randomly upgrade one card");
        }

        if (textKey.Contains(".DECIPHER_2.options.DECIPHER", StringComparison.Ordinal))
        {
            return BuildDecipherOption(snapshot, option, 12, "lose 12 max hp and randomly upgrade one card");
        }

        if (textKey.Contains(".DECIPHER_3.options.DECIPHER", StringComparison.Ordinal))
        {
            return BuildDecipherOption(snapshot, option, 24, "lose 24 max hp and randomly upgrade one card");
        }

        if (textKey.Contains(".DECIPHER_4.options.DECIPHER", StringComparison.Ordinal))
        {
            return BuildFinalDecipherOption(snapshot, option);
        }

        return option;
    }

    private static EventOptionDescriptor BuildDecipherOption(
        EventVisitState snapshot,
        EventOptionDescriptor option,
        int maxHpLoss,
        string note)
    {
        int safeMaxHpLoss = Math.Min(maxHpLoss, Math.Max(snapshot.MaxHp - 1, 0));
        int lethalHpLoss = option.WillKillPlayer ? Math.Max(snapshot.CurrentHp, 1) : 0;
        int upgradeCount = CountUpgradableCards(snapshot) > 0 ? 1 : 0;
        return WithKnownOutcome(option, nameof(TabletOfTruthEventHandler), NormalizationSource, true, EventSupportLevel.SpecialPartial, EventPlannerTrustLevel.Medium, false, [EventOptionKind.LoseMaxHp, EventOptionKind.UpgradeCard, EventOptionKind.Randomized, EventOptionKind.MultiStep], new EventOutcomeSummary
        {
            HpDelta = -lethalHpLoss,
            MaxHpDelta = -safeMaxHpLoss,
            UpgradeCount = upgradeCount,
            HasRandomness = upgradeCount > 0,
            Notes = [note]
        }, ["upgrade target is random", "deciphering continues into another decision page"]);
    }

    private static EventOptionDescriptor BuildFinalDecipherOption(EventVisitState snapshot, EventOptionDescriptor option)
    {
        int maxHpLoss = Math.Max(0, snapshot.MaxHp - 1);
        int hpLoss = Math.Max(0, snapshot.CurrentHp - 1);
        return WithKnownOutcome(option, nameof(TabletOfTruthEventHandler), NormalizationSource, true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, false, [EventOptionKind.LoseMaxHp, EventOptionKind.LoseHp, EventOptionKind.UpgradeCard], new EventOutcomeSummary
        {
            MaxHpDelta = -maxHpLoss,
            HpDelta = -hpLoss,
            UpgradeCount = CountUpgradableCards(snapshot),
            Notes = ["lose all but 1 max hp", "current hp is scored as falling to 1 for conservative planning", "upgrade every upgradable card"]
        });
    }

    private static int CountUpgradableCards(EventVisitState snapshot)
    {
        return snapshot.Player.Deck.Cards.Count(static card => card.IsUpgradable);
    }
}
