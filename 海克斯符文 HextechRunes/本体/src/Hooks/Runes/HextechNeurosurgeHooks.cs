using HarmonyLib;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

// 升级：精神过载 —— 把 NeurosurgePower 每回合开始施加的灾厄(DoomPower)从「自身」重定向到「全体存活敌人」。
internal static class HextechNeurosurgeHooks
{

	internal static bool OwnsUpgradeRune(Creature? creature)
	{
		return creature?.Player?.GetRelic<NeurosurgeUpgradeRune>() != null;
	}


	internal static async Task RedirectDoomToEnemies(NeurosurgePower power, Creature owner)
	{
		if (owner.CombatState is not HextechCombatState combatState)
		{
			return;
		}

		ThrowingPlayerChoiceContext context = new();
		foreach (Creature enemy in HextechCombatCreatureHelper.GetAliveEnemies(combatState))
		{
			await PowerCmd.Apply<DoomPower>(context, enemy, power.Amount, owner, null);
		}
	}

}
