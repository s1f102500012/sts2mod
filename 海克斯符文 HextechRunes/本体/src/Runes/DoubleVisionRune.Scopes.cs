using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Saves;
using System.Runtime.CompilerServices;

namespace HextechRunes;

public sealed partial class DoubleVisionRune
{
	private static DirectCommandRewardScope? BeginDirectCommandReward(Player player)
	{
		if (IsCommandDuplicationSuppressed()
			|| CombatManager.Instance.IsInProgress
			|| !ShouldDuplicateForPlayer(player))
		{
			return null;
		}

		// 事件房不走 Direct 类内联复制。事件选项内的 RelicCmd.Obtain 由 EventOption.Chosen
		// 外层事务捕获,等原 OnChosen Task 返回后再在所有端顺序提交;不在事务内的背景命令不猜测为奖励。
		// 战后奖励屏的翻倍仍走 RelicReward.OnSelect 等 Reward 专用 hook。
		if (player.RunState.CurrentRoom is EventRoom)
		{
			return null;
		}

		IReadOnlyList<DoubleVisionRune> runes = GetActiveRunes(player);
		if (runes.Count == 0)
		{
			return null;
		}

		int previousDepth = CommandDuplicationSuppressionDepth.Value;
		CommandDuplicationSuppressionDepth.Value = previousDepth + 1;
		return new DirectCommandRewardScope(player, runes, previousDepth);
	}

	private static void RestoreCommandRewardScope(DirectCommandRewardScope scope)
	{
		CommandDuplicationSuppressionDepth.Value = scope.PreviousSuppressionDepth;
	}

	private static bool ShouldDuplicateDirectDeckCard(CardModel card, PileType newPileType, AbstractModel? clonedBy)
	{
		return newPileType == PileType.Deck
			&& clonedBy == null
			&& !IsCommandDuplicationSuppressed()
			&& !CombatManager.Instance.IsInProgress
			&& card.Owner != null
			&& ShouldDuplicateForPlayer(card.Owner);
	}

	private static bool IsCommandDuplicationSuppressed()
	{
		return CommandDuplicationSuppressionDepth.Value > 0;
	}

	private static async Task<T> RunWithCommandDuplicationSuppressed<T>(Func<Task<T>> action)
	{
		int previousDepth = CommandDuplicationSuppressionDepth.Value;
		CommandDuplicationSuppressionDepth.Value = previousDepth + 1;
		try
		{
			return await action();
		}
		finally
		{
			CommandDuplicationSuppressionDepth.Value = previousDepth;
		}
	}

	private static async Task RunWithCommandDuplicationSuppressed(Func<Task> action)
	{
		int previousDepth = CommandDuplicationSuppressionDepth.Value;
		CommandDuplicationSuppressionDepth.Value = previousDepth + 1;
		try
		{
			await action();
		}
		finally
		{
			CommandDuplicationSuppressionDepth.Value = previousDepth;
		}
	}

	private static IReadOnlyList<DoubleVisionRune> GetActiveRunes(Player player)
	{
		if (!ShouldDuplicateForPlayer(player))
		{
			return [];
		}

		return player.Relics
			.OfType<DoubleVisionRune>()
			.Where(static rune => rune.Owner != null)
			.ToList();
	}

	private static IReadOnlyList<DoubleVisionRune> GetEventActiveRunes(Player player)
	{
		if (player.Creature.IsDead)
		{
			return [];
		}

		return player.Relics
			.OfType<DoubleVisionRune>()
			.Where(static rune => rune.Owner != null)
			.ToList();
	}

	private static bool ShouldDuplicateForPlayer(Player player)
	{
		if (player.Creature.IsDead)
		{
			return false;
		}

		RunManager? runManager = RunManager.Instance;
		INetGameService? netService = runManager?.NetService;
		if (netService != null
			&& netService.Type is NetGameType.Host or NetGameType.Client
			&& netService.IsConnected
			&& !LocalContext.IsMe(player))
		{
			return false;
		}

		return true;
	}

	private static bool ShouldSyncReward()
	{
		RunManager? runManager = RunManager.Instance;
		INetGameService? netService = runManager?.NetService;
		return netService != null
			&& netService.Type is NetGameType.Host or NetGameType.Client
			&& netService.IsConnected;
	}

	private static void TrySyncObtainedCard(CardModel card)
	{
		if (!ShouldSyncReward())
		{
			return;
		}

		try
		{
			RunManager.Instance.RewardSynchronizer.SyncLocalObtainedCard(card);
		}
		catch (Exception ex)
		{
			Log.Error(
				$"[{ModInfo.Id}][DoubleVision][DESYNC-RISK] Local duplicated card reward was already granted, "
				+ $"but its multiplayer broadcast failed: card={card.Id.Entry} error={ex.GetType().Name}: {ex.Message}");
		}
	}

	private static void TrySyncObtainedGold(int amount)
	{
		if (!ShouldSyncReward())
		{
			return;
		}

		try
		{
			RunManager.Instance.RewardSynchronizer.SyncLocalObtainedGold(amount);
		}
		catch (Exception ex)
		{
			Log.Error(
				$"[{ModInfo.Id}][DoubleVision][DESYNC-RISK] Local duplicated gold reward was already granted, "
				+ $"but its multiplayer broadcast failed: amount={amount} error={ex.GetType().Name}: {ex.Message}");
		}
	}

	private static void TrySyncObtainedPotion(PotionModel potion)
	{
		if (!ShouldSyncReward())
		{
			return;
		}

		try
		{
			RunManager.Instance.RewardSynchronizer.SyncLocalObtainedPotion(potion);
		}
		catch (Exception ex)
		{
			Log.Error(
				$"[{ModInfo.Id}][DoubleVision][DESYNC-RISK] Local duplicated potion reward was already granted, "
				+ $"but its multiplayer broadcast failed: potion={potion.Id.Entry} error={ex.GetType().Name}: {ex.Message}");
		}
	}

	private static void TrySyncObtainedRelic(RelicModel relic)
	{
		if (!ShouldSyncReward())
		{
			return;
		}

		try
		{
			RunManager.Instance.RewardSynchronizer.SyncLocalObtainedRelic(relic);
		}
		catch (Exception ex)
		{
			Log.Error(
				$"[{ModInfo.Id}][DoubleVision][DESYNC-RISK] Local duplicated relic reward was already granted, "
				+ $"but its multiplayer broadcast failed: relic={relic.Id.Entry} error={ex.GetType().Name}: {ex.Message}");
		}
	}
}
