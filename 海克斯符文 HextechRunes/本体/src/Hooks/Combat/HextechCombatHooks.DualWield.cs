using HarmonyLib;
using System.Runtime.CompilerServices;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static partial class HextechCombatHooks
{
	private static FieldInfo DualWieldDamagePerHitField = null!;
	private static FieldInfo DualWieldHitCountField = null!;
	private static readonly ConditionalWeakTable<AttackCommand, object> DualWieldProcessedCommands = new();
	private static readonly object DualWieldProcessedMarker = new();

	private static HarmonyMethod? TryCreateDualWieldAttackCommandPrefix()
	{
		try
		{
			DualWieldDamagePerHitField = RequireField(typeof(AttackCommand), "_damagePerHit");
			DualWieldHitCountField = RequireField(typeof(AttackCommand), "_hitCount");
			return new HarmonyMethod(typeof(HextechCombatHooks), nameof(DualWieldAttackCommandExecutePrefix));
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Dual Wield attack hook disabled because required AttackCommand fields are unavailable: {ex.GetType().Name}: {ex.Message}");
			return null;
		}
	}

	// 敌方「双刀流」:敌人攻击伤害白值减半(向上取整)、段数加倍。直接改写 AttackCommand 的
	// _damagePerHit(白值)与 _hitCount(段数),不碰伤害系数——力量等加成仍在减半后的白值上叠加。
	private static void DualWieldAttackCommandExecutePrefix(AttackCommand __instance)
	{
		Creature? attacker = __instance.Attacker;
		if (attacker?.Side != CombatSide.Enemy
			|| attacker.CombatState?.RunState is not RunState runState
			|| HextechMayhemModifier.FindIn(runState) is not { } modifier
			|| !modifier.HasActiveMonsterHex(MonsterHexKind.DualWield))
		{
			return;
		}

		// 同一攻击命令只处理一次,避免重入/重复执行时反复减半加段。
		if (DualWieldProcessedCommands.TryGetValue(__instance, out _))
		{
			return;
		}

		DualWieldProcessedCommands.Add(__instance, DualWieldProcessedMarker);

		// 计算型伤害(_damagePerHit < 0,改用 _calculatedDamageVar)不在此减半,只加倍段数。
		if (DualWieldDamagePerHitField.GetValue(__instance) is decimal damagePerHit && damagePerHit >= 1m)
		{
			DualWieldDamagePerHitField.SetValue(__instance, Math.Ceiling(damagePerHit / 2m));
		}

		if (DualWieldHitCountField.GetValue(__instance) is int hitCount && hitCount >= 1)
		{
			DualWieldHitCountField.SetValue(__instance, hitCount * 2);
		}
	}
}
