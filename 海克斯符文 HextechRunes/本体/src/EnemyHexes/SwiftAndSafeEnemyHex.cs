namespace HextechRunes;

internal sealed class SwiftAndSafeEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.SwiftAndSafe;

	internal override async Task AfterCardDrawn(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
	{
		if (card.Owner?.Creature.Side != CombatSide.Player
			|| card.Owner.Creature.CombatState?.RunState != context.RunState
			|| HextechPlayerContextHelper.IsNetworkMultiplayerRun())
		{
			return;
		}

		Player owner = card.Owner;
		ulong playerId = owner.NetId;
		int cardsPerArtifact = context.TierValue(Kind, 15, 12, 10);
		int cardsDrawn = context.Tracking.SwiftAndSafePlayerCardsDrawnThisCombat.GetValueOrDefault(playerId, 0) + 1;
		context.Tracking.SwiftAndSafePlayerCardsDrawnThisCombat[playerId] = cardsDrawn;
		if (cardsDrawn % cardsPerArtifact != 0)
		{
			return;
		}

		HextechCombatState combatState = owner.Creature.CombatState;
		foreach (Creature enemy in context.GetAliveEnemies(combatState))
		{
			await PowerCmd.Apply<ArtifactPower>(enemy, 1, enemy, null);
		}
	}

	internal override Task AfterCardPlayedLate(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		return HextechPlayerContextHelper.IsNetworkMultiplayerRun() && cardPlay.Card.Owner?.Creature.CombatState is HextechCombatState combatState
			? ResolvePlayerDraws(context, combatState)
			: Task.CompletedTask;
	}

	internal override Task AfterPlayerTurnStartLate(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, Player player)
	{
		return HextechPlayerContextHelper.IsNetworkMultiplayerRun() && player.Creature.CombatState is HextechCombatState combatState
			? ResolvePlayerDraws(context, combatState)
			: Task.CompletedTask;
	}


	internal override Task BeforeTurnEnd(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, CombatSide side, CombatRoom? combatRoom)
	{
		return side == CombatSide.Player && combatRoom != null && HextechPlayerContextHelper.IsNetworkMultiplayerRun()
			? ResolvePlayerDraws(context, combatRoom.CombatState)
			: Task.CompletedTask;
	}

	private static async Task ResolvePlayerDraws(HextechEnemyHexContext context, HextechCombatState combatState)
	{
		if (combatState.RunState != context.RunState)
		{
			return;
		}

		int cardsPerArtifact = context.TierValue(MonsterHexKind.SwiftAndSafe, 15, 12, 10);

		// 人工制品节奏按「单人」标定,不随联机人数放大:阈值随玩家数等比放大,改用全队合计抽牌数对
		// (阈值 × 玩家数)结算。等价于按全队「人均抽牌数」给层数,而非各玩家分别跨阈值后求和——后者
		// 会让层数随人数线性翻倍(5 人 ≈ 5 倍),正是玩家反馈的 bug。各玩家累计抽牌数仍精确存进 tracking,
		// 故跨越阈值的小数进度不会丢失。
		int playerCount = Math.Max(1, combatState.Players.Count);
		long threshold = (long)cardsPerArtifact * playerCount;

		long totalDrawnNow = 0;
		long totalDrawnPrev = 0;
		foreach (Player player in combatState.Players.OrderBy(static player => player.NetId))
		{
			int drawnCards = CountPlayerDrawnCardsFromHistory(player);
			int previousDrawnCards = context.Tracking.SwiftAndSafePlayerCardsDrawnThisCombat.GetValueOrDefault(player.NetId, 0);

			// 防御:历史计数不应回退;万一回退就按已记录值,避免负增量。
			if (drawnCards < previousDrawnCards)
			{
				drawnCards = previousDrawnCards;
			}

			totalDrawnNow += drawnCards;
			totalDrawnPrev += previousDrawnCards;
			context.Tracking.SwiftAndSafePlayerCardsDrawnThisCombat[player.NetId] = drawnCards;
		}

		int pendingArtifact = (int)(totalDrawnNow / threshold - totalDrawnPrev / threshold);
		if (pendingArtifact <= 0)
		{
			return;
		}

		foreach (Creature enemy in context.GetAliveEnemies(combatState))
		{
			await PowerCmd.Apply<ArtifactPower>(enemy, pendingArtifact, enemy, null);
		}
	}

	private static int CountPlayerDrawnCardsFromHistory(Player player)
	{
		return CombatManager.Instance.History.Entries
			.OfType<CardDrawnEntry>()
			.Count(entry => entry.Card.Owner?.NetId == player.NetId);
	}

}
