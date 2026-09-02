using System.Globalization;
using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Saves;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

/// <summary>
/// 棱彩蛋:宝箱房遗物不走 TryModifyRewards——开箱是 TreasureRoomRelicSynchronizer 的
/// 「roll 遗物列表 → 玩家投票选牌位 → 发放」小游戏。替换必须在 roll 阶段
/// (BeginRelicPicking 之后、UI 初始化之前)做:UI 节点按遗物实例反查,发放阶段换实例会让
/// AnimateRelicAwards 的 First 匹配不到 → 状态机卡死无法拾取(实测踩坑)。
/// 语义:N 个玩家 N 个遗物,局内有 A 个持蛋玩家就把其中 A 个换成随机符文,谁拿谁定。
/// 槽位与符文都用确定性随机(盐=楼层+槽位),联机两端一致;排除所有玩家已拥有的符文。
/// </summary>
internal static class HextechTreasureRuneHooks
{
	internal static readonly FieldInfo CurrentRelicsField = RequireField(typeof(TreasureRoomRelicSynchronizer), "_currentRelics");
	internal static readonly FieldInfo PlayerCollectionField = RequireField(typeof(TreasureRoomRelicSynchronizer), "_playerCollection");


	internal static void ReplaceRelicsForEggOwners(TreasureRoomRelicSynchronizer synchronizer)
	{
		if (CurrentRelicsField.GetValue(synchronizer) is not List<RelicModel> relics
			|| relics.Count == 0
			|| PlayerCollectionField.GetValue(synchronizer) is not IPlayerCollection playerCollection)
		{
			return;
		}

		IReadOnlyList<Player> players = playerCollection.Players;
		List<Player> eggOwners = players.Where(static player => player?.GetRelic<PrismaticEggRune>() != null).ToList();
		if (eggOwners.Count == 0)
		{
			return;
		}

		Player anchorPlayer = eggOwners[0];
		RunState runState = (RunState)anchorPlayer.RunState;
		string floor = runState.TotalFloor.ToString(CultureInfo.InvariantCulture);
		int replaceCount = Math.Min(eggOwners.Count, relics.Count);

		// 确定性挑 A 个槽位(避免总是替换固定位置;两端同盐同结果)。
		List<int> slots = HextechStableRandom.PickDistinct(
			Enumerable.Range(0, relics.Count),
			replaceCount,
			runState,
			static slot => slot.ToString(CultureInfo.InvariantCulture),
			"prismatic-egg-slots",
			floor);

		// 奖励可能被任意玩家领取:排除口径取所有玩家已拥有符文的并集;
		// 多人时还要求符文对所有在场玩家可用(避免角色专属符文落到错误角色手里变白板)。
		HashSet<ModelId> blocked = HextechRuneGrantHelper.CollectRuneIdsOwnedByAnyPlayer(players);
		Func<Type, bool>? allPlayersFilter = players.Count <= 1
			? null
			: type =>
			{
				RelicModel relic = ModelDb.GetById<RelicModel>(ModelDb.GetId(type));
				return players.All(player => HextechCatalog.IsAvailableForPlayer(relic, player));
			};
		foreach (int slot in slots)
		{
			Type? runeType = HextechRuneGrantHelper.PickRewardRuneType(
				anchorPlayer,
				blocked,
				allPlayersFilter,
				"prismatic-egg",
				floor,
				slot.ToString(CultureInfo.InvariantCulture));
			if (runeType == null)
			{
				// 池空:保留该槽原遗物兜底。
				continue;
			}

			// 列表持 canonical 实例:发放动画会对领取的遗物再 ToMutable,塞 mutable 会触发
			// AssertCanonical 崩(实测踩坑,MutableModelException)。
			RelicModel rune = ModelDb.GetById<RelicModel>(ModelDb.GetId(runeType));
			SaveManager.Instance.MarkRelicAsSeen(rune);
			relics[slot] = rune;
			blocked.Add(rune.Id);
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] PrismaticEgg replaced treasure relic: slot={slot} rune={rune.Id.Entry} eggOwners={eggOwners.Count}");
		}
	}

}
