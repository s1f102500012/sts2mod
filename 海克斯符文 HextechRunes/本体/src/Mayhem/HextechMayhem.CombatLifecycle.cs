namespace HextechRunes;

internal sealed partial class HextechMayhemModifier
{
	public override async Task BeforeCombatStart()
	{
		HextechCombatHooks.ResetTransientCombatState();
		HextechGoldrendSync.BeginCombat(RunState);
		ResetCombatTracking();
		HextechMultiplayerScalingCompat.RefreshHostScalingFlagForLocalHost(this);
		if (RunState.CurrentRoom is CombatRoom currentCombatRoom)
		{
			await HextechMultiplayerScalingCompat.NormalizeCombatEnemyHpIfNeeded(this, currentCombatRoom);
		}

		await ApplyToCurrentEnemiesIfNeeded();

		if (RunState.CurrentRoom is CombatRoom combatRoom)
		{
			await ApplyCombatStartEnemyHexes(combatRoom);
		}

		_combatTracking.EnemyProtectiveVeilTurnCounter = 0;

		// R1:纯表现刷新移到所有会进 checksum 的同步写入(HP 归一化/持久 hex/起手 hex)之后,
		// 保证同步命令先于任何 UI 刷新发出;Refresh 自身已 throw-safe,双重保险。
		HextechEnemyUi.Refresh(this);
	}

	public override async Task AfterCombatEnd(CombatRoom room)
	{
		HextechCombatHooks.ResetTransientCombatState();
		await HextechEnemyHexDispatcher.ForEachActive(
			this,
			(effect, context) => effect.AfterCombatEnd(context, room));

		await HextechGoldrendSync.ApplyPendingCombatGoldLosses(RunState);
		ResetCombatTracking();
	}

	public override async Task AfterCombatVictory(CombatRoom room)
	{
		await HextechEnemyHexDispatcher.ForEachActive(
			this,
			(effect, context) => effect.AfterCombatVictory(context, room));

		if (HextechRelicBase.IsNetworkMultiplayerRun())
		{
			await ApplySharedCombatVictoryRunes(room);
		}
	}

	private async Task ApplySharedCombatVictoryRunes(CombatRoom room)
	{
		foreach (IHextechSharedCombatVictoryRune rune in RunState.Players
			.SelectMany(static player => player.Relics)
			.OfType<IHextechSharedCombatVictoryRune>())
		{
			await rune.ApplySharedCombatVictory(room);
		}
	}

	public override async Task AfterCreatureAddedToCombat(Creature creature)
	{
		if (creature.Side != CombatSide.Enemy || !creature.IsAlive)
		{
			return;
		}

		await HextechMultiplayerScalingCompat.NormalizeEnemyHpIfNeeded(this, creature);

		if (RunState.CurrentRoom is CombatRoom combatRoom
			&& await TryApplyDeferredBossStartHexes(creature, combatRoom))
		{
			HextechEnemyUi.Refresh(this);
			return;
		}

		if (ShouldDeferInitialBossStartHexes(creature))
		{
			HextechEnemyUi.Refresh(this);
			return;
		}

		await ApplyPersistentMonsterHexes(creature);
		HextechEnemyUi.Refresh(this);
	}

	public async Task ApplyToCurrentEnemiesIfNeeded()
	{
		if (RunState.CurrentRoom is not CombatRoom combatRoom)
		{
			return;
		}

		foreach (Creature enemy in combatRoom.CombatState.Enemies.Where(static creature => creature.IsAlive))
		{
			if (ShouldDeferInitialBossStartHexes(enemy))
			{
				continue;
			}

			await ApplyPersistentMonsterHexes(enemy);
		}

		HextechEnemyUi.Refresh(this);
	}

	public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, HextechCombatState combatState)
	{
		HextechCombatHooks.ClearPendingManualPlayState();
		await ApplyDeferredBossStartHexes(combatState);
		await HextechEnemyHexDispatcher.ForEachActive(
			this,
			(effect, context) => effect.BeforeSideTurnStart(context, choiceContext, side, combatState));

		IReadOnlyList<Creature> players = HextechCombatCreatureHelper.GetAlivePlayerSideCreatures(combatState);

		if (side == CombatSide.Player)
		{
			await BeforePlayerSideTurnStart(combatState, players);
			return;
		}

		if (side == CombatSide.Enemy)
		{
			await BeforeEnemySideTurnStart(combatState, players);
		}
	}
}
