namespace HextechRunes;

// 0.8.4 重做:回合结束时,把每个玩家消耗牌堆里的所有状态牌与诅咒牌移回其弃牌堆
// (原效果为"状态/诅咒牌不因打出或回合结束而消耗",实现已整体替换)。
internal sealed class ForgottenSoulEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.ForgottenSoul;

	internal override async Task BeforeTurnEnd(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, CombatSide side, CombatRoom? combatRoom)
	{
		if (side != CombatSide.Player || combatRoom == null)
		{
			return;
		}

		foreach (Creature playerCreature in context.GetAlivePlayerSideCreaturesTakingTurn(combatRoom.CombatState))
		{
			Player? player = playerCreature.Player;
			if (player == null)
			{
				continue;
			}

			List<CardModel> cards = PileType.Exhaust.GetPile(player).Cards
				.Where(static card => card.Type is CardType.Status or CardType.Curse)
				.ToList();
			if (cards.Count == 0)
			{
				continue;
			}

			await CardPileCmd.Add(cards, PileType.Discard);
		}
	}
}
