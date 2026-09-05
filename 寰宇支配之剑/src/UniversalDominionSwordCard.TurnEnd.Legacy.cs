#if STS2_107_1
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Hooks;

namespace UniversalDominionSword;

public sealed partial class UniversalDominionSwordCard
{
	// 0.107.1 的敌方回合结束分发钩子叫 Hook.AfterTurnEnd(状态, 阵营, 参与者);参与者与原版回合循环一样传全部敌人。
	private static Task ResolveEnemyTurnEnd(ICombatState combatState)
	{
		return Hook.AfterTurnEnd(combatState, CombatSide.Enemy, combatState.Enemies.ToList());
	}
}
#endif
