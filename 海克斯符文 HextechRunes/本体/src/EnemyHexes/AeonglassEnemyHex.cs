namespace HextechRunes;

internal sealed class AeonglassEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.Aeonglass;

	internal override async Task AfterPlayerTurnStartLate(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, Player player)
	{
		if (player.Creature.IsDead
			|| player.Creature.CombatState is not HextechCombatState combatState
			|| combatState.RunState != context.RunState)
		{
			return;
		}

		int count = context.TierValue(Kind, 0, 1, 1);
		for (int i = 0; i < count; i++)
		{
			CardModel wither = combatState.CreateCard<Wither>(player);
			await HextechCardGeneration.AddGeneratedCardToCombat(
				wither,
				PileType.Hand,
				addedByPlayer: false);
		}
	}

	internal override Task AfterShuffle(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, Player shuffler)
	{
		if (shuffler.Creature.Side != CombatSide.Player
			|| shuffler.Creature.IsDead
			|| shuffler.Creature.CombatState is not HextechCombatState combatState
			|| shuffler.PlayerCombatState is not { } playerCombatState
			|| combatState.RunState != context.RunState)
		{
			return Task.CompletedTask;
		}

		// 升级"该玩家所有的凋萎"：原版 Wither.FakeUpgrade 每次 +3 回合结束伤害并在标题加 +N。
		// 同步纯状态变更，洗牌在两端确定性执行，MP 安全。
		foreach (CardModel card in playerCombatState.AllCards)
		{
			if (card is Wither wither)
			{
				wither.FakeUpgrade();
			}
		}

		return Task.CompletedTask;
	}
}
