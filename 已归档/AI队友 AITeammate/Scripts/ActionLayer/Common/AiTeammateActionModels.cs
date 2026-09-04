using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.GameActions;

namespace AITeammate.Scripts;

internal enum AiTeammateActionKind
{
    PlayCard,
    UsePotion,
    EndTurn,
    ChooseMapNode,
    ChooseEventOption,
    ChooseRestSiteOption,
    ClaimReward,
    InspectShopState,
    ExecuteShopStep,
}

internal sealed class AiTeammateAvailableAction
{
    public AiTeammateAvailableAction(
        AiLegalActionOption option,
        Func<Task<AiActionExecutionResult>> executeAsync,
        string? deduplicationKey = null)
    {
        Option = option;
        ExecuteAsync = executeAsync;
        DeduplicationKey = deduplicationKey;
    }

    public AiLegalActionOption Option { get; }

    public string ActionId => Option.ActionId;

    public string ActionType => Option.ActionType;

    public string Description => Option.Description;

    public string? CardId => Option.CardId;

    public string? CardInstanceId => Option.CardInstanceId;

    public string? TargetId => Option.TargetId;

    public int? EnergyCost => Option.EnergyCost;

    public Func<Task<AiActionExecutionResult>> ExecuteAsync { get; }

    public string? DeduplicationKey { get; }
}

internal sealed class AiActionExecutionResult
{
    public static AiActionExecutionResult Completed { get; } = new();

    public static AiActionExecutionResult RetrySoon { get; } = new()
    {
        ShouldRememberDeduplication = false
    };

    public GameAction? GameAction { get; init; }

    public bool WaitForQueueSettle { get; init; }

    public bool ShouldRememberDeduplication { get; init; } = true;

    public bool HasTrackedGameAction => GameAction != null;
}
