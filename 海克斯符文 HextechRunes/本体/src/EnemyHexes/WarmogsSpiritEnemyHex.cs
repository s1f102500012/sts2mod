namespace HextechRunes;

internal sealed class WarmogsSpiritEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.WarmogsSpirit;

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
		int cardsPerPlating = context.TierValue(Kind, 10, 8, 6);
		int cardsDrawn = context.Tracking.PlayerCardsDrawnThisCombat.GetValueOrDefault(playerId, 0) + 1;
		context.Tracking.PlayerCardsDrawnThisCombat[playerId] = cardsDrawn;
		if (cardsDrawn % cardsPerPlating != 0)
		{
			return;
		}

		HextechCombatState combatState = owner.Creature.CombatState;
		foreach (Creature enemy in context.GetAliveEnemies(combatState))
		{
			await HextechEnemyPowerScalingHooks.Apply<PlatingPower>(enemy, 1m, enemy, null);
		}
	}

	internal override Task AfterCardPlayedLate(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		return HextechPlayerContextHelper.IsNetworkMultiplayerRun() && cardPlay.Card.Owner?.Creature.CombatState is HextechCombatState combatState
			? ResolveDrawProgressFromHistory(context, combatState)
			: Task.CompletedTask;
	}

	internal override Task AfterPlayerTurnStartLate(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, Player player)
	{
		return HextechPlayerContextHelper.IsNetworkMultiplayerRun() && player.Creature.CombatState is HextechCombatState combatState
			? ResolveDrawProgressFromHistory(context, combatState)
			: Task.CompletedTask;
	}

#if !STS2_104_OR_NEWER
	internal override Task BeforePlayPhaseStart(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, Player player)
	{
		return HextechPlayerContextHelper.IsNetworkMultiplayerRun() && player.Creature.CombatState is HextechCombatState combatState
			? ResolveDrawProgressFromHistory(context, combatState)
			: Task.CompletedTask;
	}
#endif

	internal override Task BeforeTurnEnd(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, CombatSide side, CombatRoom? combatRoom)
	{
		return side == CombatSide.Player && combatRoom != null && HextechPlayerContextHelper.IsNetworkMultiplayerRun()
			? ResolveDrawProgressFromHistory(context, combatRoom.CombatState)
			: Task.CompletedTask;
	}

	private static async Task ResolveDrawProgressFromHistory(HextechEnemyHexContext context, HextechCombatState combatState)
	{
		if (combatState.RunState != context.RunState)
		{
			return;
		}

		int pendingPlating = 0;
		int cardsPerPlating = context.TierValue(MonsterHexKind.WarmogsSpirit, 10, 8, 6);
		foreach (Player player in combatState.Players.OrderBy(static player => player.NetId))
		{
			int drawnCards = CountPlayerDrawnCardsFromHistory(player);
			int previousDrawnCards = context.Tracking.PlayerCardsDrawnThisCombat.GetValueOrDefault(player.NetId, 0);
			if (drawnCards <= previousDrawnCards)
			{
				continue;
			}

			pendingPlating += drawnCards / cardsPerPlating - previousDrawnCards / cardsPerPlating;
			context.Tracking.PlayerCardsDrawnThisCombat[player.NetId] = drawnCards;
		}

		if (pendingPlating <= 0)
		{
			return;
		}

		foreach (Creature enemy in context.GetAliveEnemies(combatState))
		{
			await HextechEnemyPowerScalingHooks.Apply<PlatingPower>(enemy, pendingPlating, enemy, null);
		}
	}

	private static int CountPlayerDrawnCardsFromHistory(Player player)
	{
		return CombatManager.Instance.History.Entries
			.OfType<CardDrawnEntry>()
			.Count(entry => entry.Card.Owner?.NetId == player.NetId);
	}

}
