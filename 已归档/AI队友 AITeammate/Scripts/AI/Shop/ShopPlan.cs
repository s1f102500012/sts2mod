using System.Collections.Generic;
using System.Linq;

namespace AITeammate.Scripts;

internal sealed class ShopPlan
{
    public required string PlanId { get; init; }

    public required IReadOnlyList<ShopPlanStep> Steps { get; init; }

    public required double TotalScore { get; init; }

    public required int RemainingGold { get; init; }

    public required bool LeavesShop { get; init; }

    public required string OutcomeSummary { get; init; }

    public string Describe()
    {
        string steps = Steps.Count == 0
            ? "none"
            : string.Join(" -> ", Steps.Select(static step => step.ActionId));
        return $"plan={PlanId} score={TotalScore:F1} remainingGold={RemainingGold} leavesShop={LeavesShop} steps=[{steps}] summary=\"{OutcomeSummary}\"";
    }
}
