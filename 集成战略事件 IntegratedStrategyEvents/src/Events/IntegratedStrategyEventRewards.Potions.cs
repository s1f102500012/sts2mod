using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;

namespace IntegratedStrategyEvents.Events;

internal static partial class IntegratedStrategyEventRewards
{
	public static PotionModel RollPotion(
		Player owner,
		PotionRarity? preferredRarity = null,
		string sourceName = "integrated strategy event")
	{
		List<PotionModel> options = GetPotionOptions(owner);
		if (options.Count == 0)
		{
			throw new InvalidOperationException($"No potion options were available for {sourceName}.");
		}

		List<PotionModel> candidates = preferredRarity.HasValue
			? options.Where(potion => potion.Rarity == preferredRarity.Value).ToList()
			: options;
		if (candidates.Count == 0)
		{
			candidates = options;
		}

		return owner.PlayerRng.Rewards.NextItem(candidates)
			?? throw new InvalidOperationException($"Failed to roll a potion for {sourceName}.");
	}
}
