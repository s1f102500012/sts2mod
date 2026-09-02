using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Saves;
using System.Runtime.CompilerServices;

namespace HextechRunes;

public sealed partial class DoubleVisionRune
{
	private async Task DuplicateRewardCards(IReadOnlyList<CardModel> sourceCards)
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return;
		}

		List<CardPileAddResult> results = new();
		foreach (CardModel sourceCard in sourceCards)
		{
			if (sourceCard.Owner != Owner || !Owner.RunState.ContainsCard(sourceCard))
			{
				continue;
			}

			CardModel copy = Owner.RunState.CloneCard(sourceCard);
			CardPileAddResult result = await RunWithCommandDuplicationSuppressed(
				() => CardPileCmd.Add(copy, PileType.Deck, clonedBy: this));
			if (!result.success)
			{
				continue;
			}

			SaveManager.Instance.MarkCardAsSeen(result.cardAdded);
			TrySyncObtainedCard(result.cardAdded);
			results.Add(result);
		}

		if (results.Count > 0)
		{
			Flash();
			CardCmd.PreviewCardPileAdd(results, 2f);
		}
	}

	private async Task DuplicateGoldReward(Player player, GoldReward reward)
	{
		if (reward.Amount <= 0)
		{
			return;
		}

		bool wasGoldStolenBack = GoldRewardWasStolenBackField?.GetValue(reward) is true;
		await DuplicateGoldAmount(player, reward.Amount, wasGoldStolenBack);
	}

	private async Task DuplicateGoldAmount(Player player, int amount, bool wasGoldStolenBack)
	{
		if (amount <= 0)
		{
			return;
		}

		Flash();
		await RunWithCommandDuplicationSuppressed(
			() => PlayerCmd.GainGold(amount, player, wasGoldStolenBack));
		TrySyncObtainedGold(amount);
	}

	private async Task DuplicatePotionReward(Player player, PotionReward reward)
	{
		PotionModel? claimedPotion = reward.ClaimedPotion;
		if (claimedPotion == null)
		{
			return;
		}

		await DuplicateObtainedPotion(player, claimedPotion);
	}

	private async Task DuplicateObtainedPotion(Player player, PotionModel sourcePotion)
	{
		PotionModel copy = ModelDb.GetById<PotionModel>(sourcePotion.CanonicalInstance?.Id ?? sourcePotion.Id).ToMutable();
		PotionProcureResult result = await RunWithCommandDuplicationSuppressed(
			() => PotionCmd.TryToProcure(copy, player));
		if (!result.success)
		{
			return;
		}

		Flash();
		TrySyncObtainedPotion(result.potion);
	}

	private async Task DuplicateRelicReward(Player player, RelicReward reward)
	{
		RelicModel? claimedRelic = reward.ClaimedRelic;
		if (claimedRelic == null)
		{
			return;
		}

		await DuplicateObtainedRelic(player, claimedRelic);
	}

	private async Task DuplicateObtainedRelic(Player player, RelicModel sourceRelic, bool syncReward = true)
	{
		// 复视不复制海克斯模组自己的符文/遗物/锻造；原版及已注册的外部遗物仍按其模型 ID 复制。
		// 复杂且对多人敏感,重复获得易引发分叉/卡死(玩家实测黑屏的一类来源)。按需求收窄复视作用域为原版遗物。
		// 判据取并,覆盖本体 + 拓展包(HextechRunesSponsorPack)且不硬引用拓展包程序集:
		//   ① 继承 HextechRelicBase 的——本体+拓展包的符文、以及 HextechForgeBase 锻造;
		//   ② 程序集名以 "HextechRunes" 开头的——覆盖拓展包里直接继承 RelicModel 的事件遗物(如 GoldStarRelic)。
		// 原版遗物程序集名为 "sts2" 且非 HextechRelicBase,故不受影响,复视照常复制。
		if (sourceRelic is HextechRelicBase
			|| sourceRelic.GetType().Assembly.GetName().Name?.StartsWith("HextechRunes", StringComparison.Ordinal) == true)
		{
			return;
		}

		if (sourceRelic is DustyTome dustyTome)
		{
			await DuplicateDustyTome(player, dustyTome, syncReward);
			return;
		}

		// 复视不对 Orobas 先古遗物「古老牙齿」「欧洛巴斯之触」生效（不复制它们）：
		// 它们的获得/转化流程不适合被复制（古老牙齿重复获得会因牌组无可转化牌而卡死）。
		// 黄金罗盘（GoldenCompass）同样跳过：其 AfterObtained 会 await RunManager.GenerateMap() 重建全图、
		// 消耗共享 RNG 并改写共享地图。即使事件事务在所有端执行,多人各玩家事件任务仍可能并发完成,
		// 不能把全局地图重建挂到玩家级奖励事务里。
		// 先古遗物本就不该被双倍，复制出第二枚黄金罗盘语义上也不成立。
		if (sourceRelic is ArchaicTooth or TouchOfOrobas or GoldenCompass)
		{
			return;
		}

		ModelId sourceId = sourceRelic.CanonicalInstance?.Id ?? sourceRelic.Id;
		RelicModel? canonical = sourceId == ModelId.none
			? null
			: ModelDb.GetByIdOrNull<RelicModel>(sourceId);
		if (canonical == null)
		{
			Log.Warn(
				$"[{ModInfo.Id}][DoubleVision] Skipped relic duplication because its model is not registered: "
				+ $"player={player.NetId} relic={sourceId.Entry} type={sourceRelic.GetType().FullName}.");
			return;
		}

		RelicModel copy = canonical.ToMutable();
		CopyWaxState(sourceRelic, copy);
		RelicModel obtained = await RunWithCommandDuplicationSuppressed(
			() => RelicCmd.Obtain(copy, player));
		if (LocalContext.IsMe(player))
		{
			Flash();
		}
		if (syncReward)
		{
			TrySyncObtainedRelic(obtained);
		}
	}

	private async Task DuplicateDustyTome(Player player, DustyTome sourceTome, bool syncReward)
	{
		if (sourceTome.AncientCard == null)
		{
			Log.Warn($"[{ModInfo.Id}][DoubleVision] Refused to duplicate Dusty Tome without an AncientCard.");
			return;
		}

		DustyTome obtained = await DuplicateDustyTomeSpecialized(
			sourceTome,
			syncReward,
			copy => RunWithCommandDuplicationSuppressed(async () =>
				(DustyTome)await RelicCmd.Obtain(copy, player)),
			static copy => TrySyncObtainedRelic(copy));
		if (LocalContext.IsMe(player))
		{
			Flash();
		}
	}

	internal static Task<T> DuplicateDustyTomeSpecialized<T>(
		DustyTome sourceTome,
		bool syncReward,
		Func<DustyTome, Task<T>> obtainCopy,
		Action<T> synchronize)
	{
		return DuplicateDustyTomeSpecializedCore(
			sourceTome,
			syncReward,
			obtainCopy,
			synchronize,
			createCopy: null,
			assignAncientCard: null);
	}

	internal static Task<T> DuplicateDustyTomeSpecializedForTest<T>(
		DustyTome sourceTome,
		bool syncReward,
		Func<DustyTome, Task<T>> obtainCopy,
		Action<T> synchronize,
		Func<DustyTome> createCopy,
		Action<DustyTome, ModelId> assignAncientCard)
	{
		return DuplicateDustyTomeSpecializedCore(
			sourceTome,
			syncReward,
			obtainCopy,
			synchronize,
			createCopy,
			assignAncientCard);
	}

	private static async Task<T> DuplicateDustyTomeSpecializedCore<T>(
		DustyTome sourceTome,
		bool syncReward,
		Func<DustyTome, Task<T>> obtainCopy,
		Action<T> synchronize,
		Func<DustyTome>? createCopy,
		Action<DustyTome, ModelId>? assignAncientCard)
	{
		ArgumentNullException.ThrowIfNull(sourceTome);
		ArgumentNullException.ThrowIfNull(obtainCopy);
		ArgumentNullException.ThrowIfNull(synchronize);
		if (sourceTome.AncientCard is not { } ancientCardId)
		{
			throw new InvalidOperationException("Cannot duplicate Dusty Tome without an AncientCard.");
		}

		DustyTome copy = createCopy?.Invoke()
			?? (DustyTome)ModelDb
				.GetById<RelicModel>(sourceTome.CanonicalInstance?.Id ?? sourceTome.Id)
				.ToMutable();
		CopyWaxState(sourceTome, copy);
		assignAncientCard ??= static (dustyTome, cardId) => dustyTome.AncientCard = cardId;
		assignAncientCard(copy, ancientCardId);
		T obtained = await RunWithDustyTomeAfterObtainedSuppressed(
			copy,
			() => obtainCopy(copy));
		if (syncReward)
		{
			synchronize(obtained);
		}

		return obtained;
	}

	internal static void CopyWaxState(RelicModel source, RelicModel copy)
	{
		copy.IsWax = source.IsWax;
	}

	internal static bool ShouldSuppressDustyTomeAfterObtained(DustyTome dustyTome)
	{
		return ReferenceEquals(SuppressedDustyTomeAfterObtained.Value, dustyTome);
	}

	private static async Task<T> RunWithDustyTomeAfterObtainedSuppressed<T>(
		DustyTome dustyTome,
		Func<Task<T>> action)
	{
		DustyTome? previous = SuppressedDustyTomeAfterObtained.Value;
		SuppressedDustyTomeAfterObtained.Value = dustyTome;
		try
		{
			return await action();
		}
		finally
		{
			SuppressedDustyTomeAfterObtained.Value = previous;
		}
	}

	private async Task DuplicateForgeReward(Player player, HextechForgeChoiceReward reward)
	{
		await DuplicateForgeById(player, reward.ClaimedForgeId);
	}

	// 商店购买的属性锻造器不走 AfterRewardTaken/HextechForgeChoiceReward,而直接 RelicCmd.Obtain 又会被
	// DuplicateObtainedRelic 的「本模组程序集」闸门跳过(锻造器全是本模组类型),因此复视此前复制不到商店锻造器。
	// 由商店购买流程在成功获得后显式调用本入口,为玩家持有的每个复视各复制一份,复用与锻造奖励完全相同的
	// ObtainSelectedForge(syncObtainedRelic) 路径。GetActiveRunes 已含「本地持有者」联机闸门(远端由广播兜底)。
	internal static async Task DuplicatePurchasedForge(Player player, RelicModel forge)
	{
		ModelId forgeId = forge.CanonicalInstance?.Id ?? forge.Id;
		if (forgeId == ModelId.none)
		{
			return;
		}

		foreach (DoubleVisionRune rune in GetActiveRunes(player))
		{
			await rune.DuplicateForgeById(player, forgeId);
		}
	}

	private async Task DuplicateForgeById(Player player, ModelId forgeId)
	{
		// (B1)附魔锻造的 AfterObtained 会开交互式选牌(FromDeckForEnchantment),必须走「持有者开UI+SyncLocalChoice、
		// 远端 WaitForRemoteChoice」的选择同步协议(每次选牌都 ReserveChoiceId)。复制份只能在【本地持有者】这一端
		// 真正开第二次选牌,再靠 ObtainSelectedForge 的 syncObtainedRelic 广播让远端经 RewardSynchronizer 获得这份
		// 锻造并 WaitForRemoteChoice 回放同一选牌——这是原版 HextechForgeChoiceReward.OnSelect(仅选取端运行)的镜像。
		// 缺这道本地闸门时,远端也各自跑 ObtainSelectedForge 开自己的选牌→复制份 choiceId 与持有者错位→远端拿到
		// Index 型结果→AsDeckCards 抛异常(玩家实测黑屏/卡的来源之一)。非本地持有者直接返回,由广播兜底。
		if (forgeId == ModelId.none || !ShouldDuplicateForPlayer(player))
		{
			return;
		}

		RelicModel forge = ModelDb.GetById<RelicModel>(forgeId).ToMutable();
		Flash();
		await RunWithCommandDuplicationSuppressed(
			() => HextechForgeGrantHelper.ObtainSelectedForge(player, forge, syncObtainedRelic: true));
	}
}
