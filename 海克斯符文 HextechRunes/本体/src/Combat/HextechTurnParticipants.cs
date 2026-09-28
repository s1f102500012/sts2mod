namespace HextechRunes;

// 原版回合钩子的 participants 是"本次真正开始/结束回合的生物"：队友的额外回合只带那名玩家重入，
// 此时阵营检查照样通过、回合数也不推进，只有 participants 能区分"持有者这回合没动"。
// 原版传入的集合不统一：普通回合开始含宠物，额外回合开始和玩家侧回合结束只含玩家本人，
// 所以宠物一律按主人判定。
internal static class HextechTurnParticipants
{
	internal static bool Includes(IEnumerable<Creature> participants, Creature creature)
	{
		Creature subject = creature.PetOwner?.Creature ?? creature;
		return participants.Contains(subject) || participants.Contains(creature);
	}

	internal static bool Includes(IEnumerable<Creature> participants, Player player)
	{
		return participants.Contains(player.Creature);
	}
}
