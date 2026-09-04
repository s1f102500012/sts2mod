using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AITeammate.Scripts;

internal sealed class DeterministicDecisionBackend : IAiDecisionBackend
{
    public Task<AiDecisionResult> DecideAsync(AiDecisionRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        AiLegalActionOption chosenAction = request.LegalActions.FirstOrDefault()
            ?? throw new InvalidOperationException("DeterministicDecisionBackend requires at least one legal action.");

        return Task.FromResult(new AiDecisionResult
        {
            ChosenActionId = chosenAction.ActionId,
            RankedActionIds = request.LegalActions.Select(static action => action.ActionId).ToList(),
            Reason = "Selected the first legal action."
        });
    }
}
