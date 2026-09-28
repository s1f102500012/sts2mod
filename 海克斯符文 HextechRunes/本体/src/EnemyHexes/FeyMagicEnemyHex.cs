namespace HextechRunes;

internal sealed class FeyMagicEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.FeyMagic;

	internal override async Task BeforePlayerSideTurnStart(HextechEnemyHexContext context, HextechCombatState combatState, IReadOnlyList<Creature> players)
	{
		foreach (KeyValuePair<uint, uint> pending in context.Tracking.FeyMagicPendingNoDrawPlayers.ToList())
		{
			uint combatId = pending.Key;
			Creature? creature = combatState.GetCreature(combatId);
			Creature? source = combatState.GetCreature(pending.Value);
			// 队友的额外回合不结算别人的待处理项：无法抽牌要落在被击中者自己的下一个回合。
			if (creature is { IsAlive: true, Side: CombatSide.Player } && !players.Contains(creature))
			{
				continue;
			}

			context.Tracking.FeyMagicPendingNoDrawPlayers.Remove(combatId);
			if (creature == null || !creature.IsAlive || creature.Side != CombatSide.Player)
			{
				continue;
			}

			await PowerCmd.Apply<ShrinkPower>(creature, 1m, source, null);
			await PowerCmd.Apply<NoDrawPower>(creature, 1m, source, null);
		}
	}

	internal override Task AfterEnemyDamageGivenPlayerHit(HextechEnemyHexContext context, Creature dealer, Creature target)
	{
		if (target.CombatId != null
			&& dealer.CombatId != null
			&& !context.Tracking.FeyMagicPendingNoDrawPlayers.ContainsKey(target.CombatId.Value))
		{
			context.Tracking.FeyMagicPendingNoDrawPlayers[target.CombatId.Value] = dealer.CombatId.Value;
		}

		return Task.CompletedTask;
	}
}
