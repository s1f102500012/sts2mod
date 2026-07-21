using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;

namespace IntegratedStrategyEvents.Events;

internal static partial class IntegratedStrategyEventEffects
{
	public static Task OfferPotionReward(Player owner, PotionModel potion)
	{
		return OfferRewards(owner, new PotionReward(potion.ToMutable(), owner));
	}

	public static Task ObtainRandomRelic(Player owner)
	{
		return ObtainRandomRelicVerified(owner, static o => IntegratedStrategyEventRewards.PullRandomRelic(o));
	}

	public static Task ObtainRandomRelic(Player owner, RelicRarity rarity)
	{
		return ObtainRandomRelicVerified(owner, o => IntegratedStrategyEventRewards.PullRandomRelic(o, rarity));
	}

	// 玩家反馈：个别环境下（疑似第三方模组补丁干扰）随机遗物未实际入账但事件正常收尾。
	// 这里在发放后核对遗物列表，未入账则重抽一次并留下可定位的日志。
	private static async Task ObtainRandomRelicVerified(Player owner, Func<Player, RelicModel> pull)
	{
		HashSet<RelicModel> relicsBefore = owner.Relics
			.Where(static relic => !relic.HasBeenRemovedFromState)
			.ToHashSet<RelicModel>(ReferenceEqualityComparer.Instance);
		RelicModel relic = pull(owner);
		await RelicCmd.Obtain(relic, owner);
		if (HasNewLiveRelic(owner, relicsBefore))
		{
			return;
		}

		Log.Warn(
			$"{ModInfo.LogPrefix} Random relic reward {relic.Id.Entry} was not added to player " +
			$"{owner.NetId}; retrying once with a fresh pull.");
		RelicModel retryRelic = pull(owner);
		await RelicCmd.Obtain(retryRelic, owner);
		if (!HasNewLiveRelic(owner, relicsBefore))
		{
			Log.Error(
				$"{ModInfo.LogPrefix} Random relic reward retry {retryRelic.Id.Entry} also failed for " +
				$"player {owner.NetId}; another mod may be intercepting RelicCmd.Obtain.");
		}
	}

	private static bool HasNewLiveRelic(Player owner, HashSet<RelicModel> relicsBefore)
	{
		return owner.Relics.Any(relic =>
			!relic.HasBeenRemovedFromState &&
			!relicsBefore.Contains(relic));
	}

	public static async Task ObtainRandomRelics(Player owner, int count)
	{
		for (int i = 0; i < count; i++)
		{
			await ObtainRandomRelic(owner);
		}
	}

	public static Task ObtainRelic<TRelic>(Player owner)
		where TRelic : RelicModel
	{
		return RelicCmd.Obtain<TRelic>(owner);
	}

	public static RelicModel? GetMostRecentlyObtainedRelic(Player owner)
	{
		for (int i = owner.Relics.Count - 1; i >= 0; i--)
		{
			RelicModel relic = owner.Relics[i];
			if (!relic.HasBeenRemovedFromState)
			{
				return relic;
			}
		}

		return null;
	}

	public static PotionModel? GetMostRecentlyObtainedPotion(Player owner)
	{
		return IntegratedStrategyPotionTracker.GetMostRecentlyObtainedPotion(owner);
	}

	public static Task DiscardPotion(PotionModel potion)
	{
		return PotionCmd.Discard(potion);
	}

	public static async Task DiscardPotionAndRemoveSlot(Player owner, PotionModel potion)
	{
		if (owner.GetPotionSlotIndex(potion) < 0)
		{
			return;
		}

		await DiscardPotion(potion);

		if (owner.MaxPotionCount > 0)
		{
			await PlayerCmd.LoseMaxPotionCount(1, owner);
		}
	}

	public static async Task ReplaceRelicWithRandomRelic(Player owner, RelicModel relic)
	{
		await RelicCmd.Remove(relic);
		await ObtainRandomRelic(owner);
	}

	public static Task OfferRewards(Player owner, params Reward[] rewards)
	{
		return RewardsCmd.OfferCustom(owner, rewards.ToList());
	}
}
