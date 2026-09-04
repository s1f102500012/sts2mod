using MegaCrit.Sts2.Core.Models;

namespace AITeammate.Scripts;

internal interface ICardResolver
{
    ResolvedCardView Resolve(CardModel liveCard, string cardInstanceId);
}
