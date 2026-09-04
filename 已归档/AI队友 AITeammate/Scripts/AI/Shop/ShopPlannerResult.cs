using System.Collections.Generic;

namespace AITeammate.Scripts;

internal sealed class ShopPlannerResult
{
    public required IReadOnlyList<ShopOfferEvaluation> OfferEvaluations { get; init; }

    public required IReadOnlyList<ShopActionEvaluation> ActionEvaluations { get; init; }

    public required ShopPlan BestPlan { get; init; }

    public required ShopPlan LeavePlan { get; init; }

    public required IReadOnlyList<ShopPlan> ConsideredPlans { get; init; }
}
