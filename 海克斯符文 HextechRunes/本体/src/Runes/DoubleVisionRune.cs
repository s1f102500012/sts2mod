using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Saves;
using System.Runtime.CompilerServices;

namespace HextechRunes;

public sealed partial class DoubleVisionRune : HextechRelicBase
{
	private static readonly AsyncLocal<CardRewardTracker?> CurrentCardRewardTracker = new();
	private static readonly AsyncLocal<int> CommandDuplicationSuppressionDepth = new();
	private static readonly AsyncLocal<EventRelicTransaction?> CurrentEventRelicTransaction = new();
	private static readonly AsyncLocal<int> EventRelicObtainDepth = new();
	private static readonly AsyncLocal<DustyTome?> SuppressedDustyTomeAfterObtained = new();
	private static readonly ConditionalWeakTable<EventRoom, EventRelicTransactionBatch> EventRelicTransactionBatches = new();
	private static readonly FieldInfo? GoldRewardWasStolenBackField = typeof(GoldReward).GetField("_wasGoldStolenBack", BindingFlags.Instance | BindingFlags.NonPublic);

	// 0.109.0 以前的版本会把事件遗物写进此字段。新代码只将它作为旧存档恢复队列:
	// 正常事件获得由 EventOption.Chosen 外层事务在原选项 Task 完成后立即结算,不再新增 pending。
	private readonly List<string> _pendingEventRelicIds = new();

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public string SavedPendingEventRelicIdsJson
	{
		get => _pendingEventRelicIds.Count == 0 ? "" : string.Join(",", _pendingEventRelicIds);
		set
		{
			_pendingEventRelicIds.Clear();
			if (!string.IsNullOrEmpty(value))
			{
				_pendingEventRelicIds.AddRange(value.Split(',', StringSplitOptions.RemoveEmptyEntries));
			}
		}
	}

	public override Task AfterRoomEntered(AbstractRoom room)
	{
		// 只恢复旧版本存档留下的 pending。事件房和战斗初始化仍不是安全恢复点:
		// 华美手镯等交互式 AfterObtained 在战斗 init 开不出 UI,联机也不能在此广播旧奖励消息。
		if (room is EventRoom || room is CombatRoom || CombatManager.Instance.IsInProgress)
		{
			return Task.CompletedTask;
		}

		return FlushPendingEventRelics();
	}

	// 旧存档恰好从事件直接进入战斗时,在胜利判定后恢复 pending。
	public override Task AfterCombatEnd(CombatRoom room)
	{
		if (Owner == null
			|| room.CombatState == null
			|| HextechCombatCreatureHelper.GetAliveEnemies(room.CombatState).Count > 0)
		{
			return Task.CompletedTask;
		}

		return FlushPendingEventRelics();
	}

	private async Task FlushPendingEventRelics()
	{
		if (Owner == null || _pendingEventRelicIds.Count == 0)
		{
			return;
		}

		// 联机时 SavedProperty 状态同步可能把待复制清单带到远端实例,远端只清账不复制(由持有端广播兜底)。
		if (Owner.Creature.IsDead || !ShouldDuplicateForPlayer(Owner))
		{
			_pendingEventRelicIds.Clear();
			return;
		}

		List<string> pending = new(_pendingEventRelicIds);
		foreach (string idEntry in pending)
		{
			// 旧格式只有 id,无法恢复真实实例身份;沿用旧语义,但成功处理一项后才移除,
			// 避免复制/交互抛异常时把整个恢复队列提前清空。
			RelicModel? source = Owner.Relics.FirstOrDefault(relic => (relic.CanonicalInstance?.Id ?? relic.Id).Entry == idEntry);
			if (source != null)
			{
				await DuplicateObtainedRelic(Owner, source);
			}

			_pendingEventRelicIds.Remove(idEntry);
		}
	}

	public override async Task AfterRewardTaken(Player player, Reward reward)
	{
		// 联机时奖励领取在各端都会触发本 hook:必须只在持有者本地端复制并广播,
		// 否则 N 人局的 N-1 个远端各复制一份(玩家实测 4 人局一瓶药水复制成三瓶,塞爆药水栏黑屏)。
		if (Owner == null
			|| !ReferenceEquals(player, Owner)
			|| player.Creature.IsDead
			|| !ShouldDuplicateForPlayer(player))
		{
			return;
		}

		switch (reward)
		{
			case GoldReward goldReward:
				await DuplicateGoldReward(player, goldReward);
				break;
			case PotionReward potionReward:
				await DuplicatePotionReward(player, potionReward);
				break;
			case HextechForgeChoiceReward forgeReward:
				await DuplicateForgeReward(player, forgeReward);
				break;
		}
	}

}
