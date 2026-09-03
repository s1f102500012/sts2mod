using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs; // 只有 STS2_107_1 分支用(RunManager.Instance),0.108+ 目标下 IDE0005 会报它多余,不要删

namespace HextechRunesSponsorPack;

// 0.108.0 起 PotionRewardOdds.Roll 去掉了 AscensionManager 参数;版本差异收在这个分部文件里,GoldStarRelic 主体不带 #if。
//
// 加载器按"不高于宿主的最大打包目标"选变体:宿主是 0.108.x / 0.109.x 时会拿到 0.107.1 变体,
// 这时三参 Roll 不存在,JIT 编译 RollPotionRewardCore 会抛 MissingMethodException。把真正的调用
// 隔离在一个禁止内联的方法里,调用方就能接住这个异常:金星在那两个版本上只是不掉药水,不会卡死精英结算。
public sealed partial class GoldStarRelic
{
	private static bool _loggedRollSignatureMismatch;

	private static bool RollPotionReward(Player owner)
	{
		try
		{
			return RollPotionRewardCore(owner);
		}
		catch (MissingMethodException ex)
		{
			if (!_loggedRollSignatureMismatch)
			{
				_loggedRollSignatureMismatch = true;
				Log.Warn($"[{ModInfo.Id}] GoldStar potion roll unavailable on this game version (variant compiled for {ModInfo.TargetGameVersion}): {ex.Message}", 2);
			}

			return false;
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool RollPotionRewardCore(Player owner)
	{
#if STS2_107_1
		return owner.PlayerOdds.PotionReward.Roll(owner, RunManager.Instance!.AscensionManager, RoomType.Monster);
#else
		return owner.PlayerOdds.PotionReward.Roll(owner, RoomType.Monster);
#endif
	}
}
