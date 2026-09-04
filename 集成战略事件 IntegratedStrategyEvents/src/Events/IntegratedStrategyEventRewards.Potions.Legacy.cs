#if !STS2_109_OR_NEWER
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
namespace IntegratedStrategyEvents.Events;
internal static partial class IntegratedStrategyEventRewards
{
	private static List<PotionModel> GetPotionOptions(Player owner) => PotionFactory.GetPotionOptions(owner, []).ToList();
}
#endif
