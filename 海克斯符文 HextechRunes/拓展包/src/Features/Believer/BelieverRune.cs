using HextechRunes;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HextechRunesSponsorPack;

// 信徒(棱彩,仅单人):击败 BOSS+6 / 精英+3 / 小怪+1 充能,满 6 排队一次「神迹」;获得时也排队一次。
//
// 神迹**不再**在战斗胜利 hook 里直接 EnterRoom —— 那会在战斗胜利流程中途把战斗房连同奖励一起弹掉,导致:
// 打完 Boss 不自动进下一层(要 SL)、SL 时事件丢失、末战拿到信徒无法结算。改为只记一个存档计数 SavedPendingMiracles,
// 由 MiracleEventTriggerPatch 在「战斗领奖屏点继续→开图」这个流程空闲的干净交接点安全注入。
// 计数在那一刻才于内存里消耗(晚于最后一次存档),所以 SL 回到事件中途会落到「计数仍 >0」的存档 → 神迹重现而非消失。
public sealed class BelieverRune : HextechRelicBase
{
	private const int ChargeThreshold = 6;

	private int _charge;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedCharge
	{
		get => _charge;
		set
		{
			_charge = Math.Max(0, value);
			InvokeDisplayAmountChanged();
		}
	}

	// 待触发的「神迹」次数(逐局持久)。由 AfterObtained / 充能满 6 累加,由 MiracleEventTriggerPatch 在干净交接点逐个消耗。
	private int _pendingMiracles;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedPendingMiracles
	{
		get => _pendingMiracles;
		set => _pendingMiracles = Math.Max(0, value);
	}

	internal bool HasPendingMiracle => _pendingMiracles > 0;

	internal void ConsumePendingMiracle()
	{
		if (_pendingMiracles > 0)
		{
			SavedPendingMiracles = _pendingMiracles - 1;
		}
	}

	public override bool ShowCounter => true;

	public override int DisplayAmount => !IsCanonical ? _charge : 0;

	// 本局锻造器售价的临时修正(可正可负),由神迹事件的选择累加;逐局持久。
	// 由 MiracleEventForgePricePatch 在主 mod 算价后叠加(纯拓展包,不动主 mod)。
	private int _forgePriceDelta;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedForgePriceDelta
	{
		get => _forgePriceDelta;
		set => _forgePriceDelta = value;
	}

	internal int ForgePriceDelta => _forgePriceDelta;

	internal void AddForgePriceDelta(int delta)
	{
		SavedForgePriceDelta = _forgePriceDelta + delta;
		Log.Info($"[{ModInfo.Id}] BelieverRune forge-price delta {(delta >= 0 ? "+" : "")}{delta} -> total {_forgePriceDelta}.");
	}

	// 仅单人游戏出现。
	public override bool IsAvailableForPlayer(Player player)
	{
		return !IsNetworkMultiplayerRun();
	}

	// 获得(激活)时排队一次神迹,在下一个「战斗领奖→开图」交接点触发。
	public override Task AfterObtained()
	{
		if (!IsNetworkMultiplayerRun())
		{
			SavedPendingMiracles = _pendingMiracles + 1;
		}

		return Task.CompletedTask;
	}

	public override Task AfterCombatVictory(CombatRoom room)
	{
		if (Owner == null || Owner.Creature.IsDead || IsNetworkMultiplayerRun())
		{
			return Task.CompletedTask;
		}

		int gain = room.RoomType switch
		{
			RoomType.Boss => 6,
			RoomType.Elite => 3,
			_ => 1
		};

		SavedCharge = _charge + gain;
		Flash(Array.Empty<Creature>());

		if (_charge >= ChargeThreshold)
		{
			SavedCharge = _charge - ChargeThreshold;
			// 只排队;真正进事件交给 MiracleEventTriggerPatch(它在干净交接点触发,并守卫末幕 Boss 之后不插事件)。
			SavedPendingMiracles = _pendingMiracles + 1;
		}

		return Task.CompletedTask;
	}
}
