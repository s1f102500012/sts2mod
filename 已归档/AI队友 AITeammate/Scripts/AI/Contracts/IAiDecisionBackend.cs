using System.Threading;
using System.Threading.Tasks;

namespace AITeammate.Scripts;

internal interface IAiDecisionBackend
{
    Task<AiDecisionResult> DecideAsync(AiDecisionRequest request, CancellationToken ct);
}
