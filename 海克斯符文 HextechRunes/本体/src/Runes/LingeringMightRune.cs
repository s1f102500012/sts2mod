using System.Globalization;

namespace HextechRunes;

/// <summary>
/// 余威(黄金,通用):战斗结束时,保留你身上随机一种增益效果(全部层数)至下一场战斗开始。
/// 单一增益构筑可以"操纵"这个随机——池里只有一种时必中,这是留给玩家自己发现的暗线。
/// </summary>
public sealed class LingeringMightRune : HextechRelicBase
{
	private string _pendingBuff = "";

	// "Category|Entry|Amount";联机下经状态同步两端一致。
	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public string SavedLingeringBuff
	{
		get => _pendingBuff;
		set => _pendingBuff = value ?? "";
	}

	// 快照必须挂 AfterCombatEnd:原版胜利流程里 Player.AfterCombatEnd() 会在
	// Hook.AfterCombatVictory 之前 RemoveAllPowersInternalExcept 清光玩家 powers
	// (反编译取证),挂 victory 时 powers 恒空 → 永不生效(玩家实报)。
	// AfterCombatEnd 两端确定性触发 + 稳定盐,联机无需额外同步。
	public override Task AfterCombatEnd(CombatRoom room)
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		// 只在胜利时保留:战斗结束且场上没有存活敌人(逃跑/败北不保留)。
		if (room.CombatState == null || HextechCombatCreatureHelper.GetAliveEnemies(room.CombatState).Count > 0)
		{
			return Task.CompletedTask;
		}

		List<PowerModel> buffs = Owner.Creature.Powers
			.Where(static power => power.IsVisible
				&& power.Amount > 0
				&& power.GetTypeForAmount(power.Amount) == PowerType.Buff)
			.ToList();
		if (buffs.Count == 0)
		{
			_pendingBuff = "";
			return Task.CompletedTask;
		}

		// 稳定随机:按局种子+玩家+楼层+池组成取索引,联机两端一致。
		int index = HextechStableRandom.Index(
			(RunState)Owner.RunState,
			buffs.Count,
			"lingering-might",
			HextechStableRandom.PlayerKey(Owner),
			((RunState)Owner.RunState).TotalFloor.ToString(CultureInfo.InvariantCulture),
			string.Join(",", buffs.Select(static power => power.Id.Entry)));
		PowerModel picked = buffs[index];
		_pendingBuff = string.Join("|",
			picked.Id.Category,
			picked.Id.Entry,
			((int)picked.Amount).ToString(CultureInfo.InvariantCulture));
		Flash();
		return Task.CompletedTask;
	}

	public override async Task BeforeCombatStart()
	{
		if (Owner == null || _pendingBuff.Length == 0)
		{
			return;
		}

		string[] parts = _pendingBuff.Split('|');
		_pendingBuff = "";
		if (parts.Length != 3
			|| !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount)
			|| amount <= 0)
		{
			return;
		}

		PowerModel? canonical;
		try
		{
			canonical = ModelDb.GetById<PowerModel>(new ModelId(parts[0], parts[1]));
		}
		catch (Exception ex)
		{
			// 上一局带过来的 power 可能来自已卸载的第三方内容,静默放弃比炸战斗开始流程好。
			Log.Warn($"[{ModInfo.Id}][LingeringMight] Failed to resolve carried buff '{parts[0]}|{parts[1]}': {ex.GetType().Name}: {ex.Message}");
			return;
		}

		if (canonical == null || Owner.Creature.IsDead)
		{
			return;
		}

		Flash();
		await PowerCmd.Apply(canonical.ToMutable(), Owner.Creature, amount, Owner.Creature, null);
	}
}
