using HarmonyLib;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

// 梦魇 —— 黑暗充能球(DarkOrb)每次实际触发 Passive 后,对生命值最低的存活敌人造成等同于该球当前计数(EvokeVal)的伤害。
// 必须挂 DarkOrb.Passive 本体,不能只挂 BeforeTurnEndOrbTrigger:
// 漆黑等回合内效果会通过 OrbCmd.Passive 直接触发 Passive,不会经过回合结束入口。
internal static class HextechNightmareHooks
{

	internal static MethodInfo ResolvePassiveHookTarget()
	{
		return RequireMethod(
			typeof(DarkOrb),
			nameof(DarkOrb.Passive),
			BindingFlags.Instance | BindingFlags.Public,
			typeof(PlayerChoiceContext),
			typeof(Creature));
	}


	internal static async Task CompletePassiveThen(Task passiveTask, Func<Task> nightmareEffect)
	{
		await passiveTask;
		await nightmareEffect();
	}

	internal static async Task TriggerNightmare(DarkOrb orb, PlayerChoiceContext choiceContext, Player player)
	{
		if (player.Creature.IsDead || player.Creature.CombatState is not HextechCombatState combatState)
		{
			return;
		}

		IReadOnlyList<Creature> enemies = HextechCombatCreatureHelper.GetAliveEnemies(combatState);
		if (enemies.Count == 0)
		{
			return;
		}

		Creature weakest = enemies.MinBy(static creature => creature.CurrentHp)!;
		await CreatureCmd.Damage(choiceContext, weakest, orb.EvokeVal, ValueProp.Unpowered, player.Creature);
	}

}
