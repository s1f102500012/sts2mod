using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HextechRunes;

public sealed class GoldStarRelic : RelicModel, IHextechSharedCombatVictoryRune
{
	private const int NormalMonsterMinGold = 10;
	private const int NormalMonsterMaxGold = 20;
	private const int CardRewardOptionCount = 3;
	private const string RelicIconPath = "res://HextechRunesSponsorPack/images/relics/goldStarRelic.png";

	public sealed override RelicRarity Rarity => RelicRarity.Event;

	public override string PackedIconPath => RelicIconPath;

	protected override string PackedIconOutlinePath => RelicIconPath;

	protected override string BigIconPath => RelicIconPath;

	public override Task AfterCombatVictory(CombatRoom room)
	{
		if (HextechRelicBase.IsNetworkMultiplayerRun())
		{
			return Task.CompletedTask;
		}

		return ApplySharedCombatVictory(room);
	}

	public Task ApplySharedCombatVictory(CombatRoom room)
	{
		if (Owner == null || Owner.Creature.IsDead || room.RoomType != RoomType.Elite)
		{
			return Task.CompletedTask;
		}

		Flash(Array.Empty<Creature>());
		AddNormalMonsterGoldReward(room);
		AddNormalMonsterPotionRewardIfRolled(room);
		room.AddExtraReward(Owner, new CardReward(CardCreationOptions.ForRoom(Owner, RoomType.Monster), CardRewardOptionCount, Owner));
		return Task.CompletedTask;
	}

	private void AddNormalMonsterGoldReward(CombatRoom room)
	{
		if (Owner == null || room.GoldProportion <= 0f)
		{
			return;
		}

		int minGold = (int)Math.Round(NormalMonsterMinGold * room.GoldProportion, MidpointRounding.AwayFromZero);
		int maxGold = (int)Math.Round(NormalMonsterMaxGold * room.GoldProportion, MidpointRounding.AwayFromZero);
		if (maxGold <= 0)
		{
			return;
		}

		room.AddExtraReward(Owner, new GoldReward(minGold, maxGold, Owner));
	}

	private void AddNormalMonsterPotionRewardIfRolled(CombatRoom room)
	{
		if (Owner == null || RunManager.Instance?.AscensionManager == null)
		{
			return;
		}

		if (RollPotionReward(Owner))
		{
			room.AddExtraReward(Owner, new PotionReward(Owner));
		}
	}

	// 0.108.0 起 Roll 去掉 AscensionManager 参数。这里按运行时实际签名自适应而非条件编译:
	// 拓展包是单物品双分支,创意工坊错发分支构建时(玩家实报公开分支拿到 beta 构建),
	// 编译期绑定会在杀精英触发本调用时 MissingMethodException 卡死;反射调用让错包也不炸。
	// 每次精英胜利才调用一次,反射开销可忽略。
	private static bool RollPotionReward(Player owner)
	{
		object odds = owner.PlayerOdds.PotionReward;
		MethodInfo? roll = odds.GetType().GetMethod("Roll", BindingFlags.Instance | BindingFlags.Public);
		if (roll == null)
		{
			return false;
		}

		object?[] args = roll.GetParameters().Length switch
		{
			2 => [owner, RoomType.Monster],
			3 => [owner, RunManager.Instance!.AscensionManager, RoomType.Monster],
			_ => []
		};
		if (args.Length == 0)
		{
			return false;
		}

		try
		{
			return roll.Invoke(odds, args) is true;
		}
		catch (Exception ex)
		{
			Log.Warn($"[HextechRunesSponsorPack][GoldStar] Potion reward roll failed: {ex.GetType().Name}: {ex.Message}");
			return false;
		}
	}
}
