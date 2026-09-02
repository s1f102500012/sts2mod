using HarmonyLib;
namespace HextechRunes;

// 升级：精神过载(仅骨妹) —— 把 Neurosurge 每回合施加的灾厄(DoomPower)从「自身」改为「全体敌人」。
// 真正的重定向在 HextechNeurosurgeHooks(Harmony 改 NeurosurgePower.AfterSideTurnStart)。本类仅负责门控与 hover。
public sealed class NeurosurgeUpgradeRune : CardUpgradeRuneBase<Neurosurge>
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<Neurosurge>(),
		HoverTipFactory.FromPower<DoomPower>()
	];

	protected override bool IsAvailableForCharacter(Player player) => IsNecrobinderPlayer(player);

	[HarmonyPatch(typeof(NeurosurgePower), nameof(NeurosurgePower.AfterSideTurnStart), typeof(CombatSide), typeof(IReadOnlyList<Creature>), typeof(ICombatState))]
	[HextechPatch("rune.neurosurge.turn-start", "升级精神过载")]
	private static class AfterSideTurnStartPatch
	{
		[HarmonyPrefix]
		private static bool Prefix(NeurosurgePower __instance, IReadOnlyList<Creature> participants, ref Task __result)
		{
			Creature? owner = __instance.Owner;
			if (owner?.Player?.GetRelic<NeurosurgeUpgradeRune>() != null && participants.Contains(owner))
			{
				__result = HextechNeurosurgeHooks.RedirectDoomToEnemies(__instance, owner);
				return false;
			}

			return true;
		}
	}

	[HarmonyPatch(typeof(NeurosurgePower), nameof(NeurosurgePower.Type), MethodType.Getter)]
	[HextechPatch("rune.neurosurge.type", "升级精神过载")]
	private static class TypePatch
	{
		[HarmonyPostfix]
		private static void Postfix(NeurosurgePower __instance, ref PowerType __result)
		{
			if (__result == PowerType.Debuff && HextechNeurosurgeHooks.OwnsUpgradeRune(__instance.Owner))
			{
				__result = PowerType.Buff;
			}
		}
	}

	[HarmonyPatch(typeof(ArtifactPower), nameof(ArtifactPower.TryModifyPowerAmountReceived), new[] { typeof(PowerModel), typeof(Creature), typeof(decimal), typeof(Creature), typeof(decimal) }, new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out })]
	[HextechPatch("rune.neurosurge.artifact", "升级精神过载")]
	private static class ArtifactPatch
	{
		[HarmonyPrefix]
		private static bool Prefix(
			PowerModel canonicalPower,
			Creature target,
			decimal amount,
			ref decimal modifiedAmount,
			ref bool __result)
		{
			if (canonicalPower is NeurosurgePower && HextechNeurosurgeHooks.OwnsUpgradeRune(target))
			{
				modifiedAmount = amount;
				__result = false;
				return false;
			}

			return true;
		}
	}
}
