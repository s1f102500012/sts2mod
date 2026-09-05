#if STS2_108_OR_NEWER
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Hooks;

namespace UniversalDominionSword;

public sealed partial class UniversalDominionSwordCard
{
	// 0.108 起敌方回合结束的分发钩子是 Hook.AfterSideTurnEnd(状态, 阵营, 参与者);参与者与原版回合循环一样传全部敌人。
	private static Task ResolveEnemyTurnEnd(ICombatState combatState)
	{
		return Hook.AfterSideTurnEnd(combatState, CombatSide.Enemy, combatState.Enemies.ToList());
	}
}
#endif
