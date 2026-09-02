namespace HextechRunes;

internal static partial class HextechCombatHooks
{
	// 记录每次伤害命令里,哪些玩家滑溜「减伤生效了」(在 ModifyHpLostAfterOsty 把 >=1 的伤害压到 1)。
	// 原版滑溜的消耗只看 result.UnblockedDamage>=1,但钨合金棍等会在滑溜之后继续 ModifyHpLostAfterOsty
	// 把这 1 点伤害再减到 0,导致 UnblockedDamage=0 → 滑溜「免了伤却不掉层」→ 永久无敌。
	// 这里追踪滑溜是否真减伤,在它减伤却没被原版消耗时补一次消耗。
	private static readonly Dictionary<long, HashSet<SlipperyPower>> SlipperyReductionsByCommand = new();


	private static async Task AppendSlipperyConsumption(Task original, SlipperyPower power)
	{
		await original;
		await PowerCmd.Decrement(power);
	}

	// Bug2:亡灵 Osty 的「替死」(DieForYouPower)把对主人(玩家)的攻击改派给 Osty 承受,玩家滑溜会
	// 全程 bypass(伤害目标是 Osty 不是玩家)、永不消耗 → Osty 替死 + 滑溜永久储备 = 双重无敌。
	// 记录本次伤害命令里 Osty 替了哪些玩家的死,在命令结束时为这些玩家的滑溜各消耗 1 层。
	private static readonly Dictionary<long, HashSet<SlipperyPower>> OstyRedirectSlipperyByCommand = new();


	private static async Task ConsumeOstyRedirectedSlippery(long commandId)
	{
		if (!OstyRedirectSlipperyByCommand.Remove(commandId, out HashSet<SlipperyPower>? pending))
		{
			return;
		}

		foreach (SlipperyPower slippery in pending)
		{
			if (slippery.Amount > 0m)
			{
				await PowerCmd.Decrement(slippery);
			}
		}
	}

	private static void ClearSlipperyReductions(long commandId)
	{
		SlipperyReductionsByCommand.Remove(commandId);
		OstyRedirectSlipperyByCommand.Remove(commandId);
	}

	[HarmonyPatch(typeof(SlipperyPower), nameof(SlipperyPower.ModifyHpLostAfterOsty), typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel))]
	[HextechPatch("combat.slippery.hp-lost", "滑溜能力兼容")]
	private static class SlipperyHpLostPatch
	{
		[HarmonyPostfix]
		private static void Postfix(SlipperyPower __instance, Creature target, decimal amount, ref decimal __result)
		{
			// target!=Owner(如伤害分摊给 Osty)或伤害本就 <1、或滑溜没把它压低,都不算「滑溜减伤」。
			if (target != __instance.Owner || amount < 1m || __result >= amount)
			{
				return;
			}

			long commandId = CurrentActualDamageCommandId;
			if (commandId == 0L)
			{
				return;
			}

			if (!SlipperyReductionsByCommand.TryGetValue(commandId, out HashSet<SlipperyPower>? reduced))
			{
				reduced = [];
				SlipperyReductionsByCommand[commandId] = reduced;
			}

			reduced.Add(__instance);
		}
	}

	[HarmonyPatch(typeof(SlipperyPower), nameof(SlipperyPower.AfterDamageReceived), typeof(PlayerChoiceContext), typeof(Creature), typeof(DamageResult), typeof(ValueProp), typeof(Creature), typeof(CardModel))]
	[HextechPatch("combat.slippery.damage-received", "滑溜能力兼容")]
	private static class SlipperyDamageReceivedPatch
	{
		[HarmonyPostfix]
		private static void Postfix(SlipperyPower __instance, Creature target, DamageResult result, ref Task __result)
		{
			// 原版在 result.UnblockedDamage>=1 时已自行消耗;这里只补「减伤生效但最终伤害被压到 <1」的漏网情形。
			if (target != __instance.Owner || result.UnblockedDamage >= 1)
			{
				return;
			}

			long commandId = CurrentActualDamageCommandId;
			if (commandId == 0L
				|| !SlipperyReductionsByCommand.TryGetValue(commandId, out HashSet<SlipperyPower>? reduced)
				|| !reduced.Remove(__instance))
			{
				return;
			}

			__result = AppendSlipperyConsumption(__result, __instance);
		}
	}

	[HarmonyPatch(typeof(DieForYouPower), nameof(DieForYouPower.ModifyUnblockedDamageTarget), typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature))]
	[HextechPatch("combat.die-for-you.target", "为你而死")]
	private static class DieForYouTargetPatch
	{
		[HarmonyPostfix]
		private static void Postfix(DieForYouPower __instance, Creature target, Creature __result)
		{
			// 替死生效 = 把原本指向主人(玩家)的伤害目标改成了 Osty 自己(__instance.Owner)。
			Creature? petOwnerCreature = __instance.Owner.PetOwner?.Creature;
			if (petOwnerCreature == null || __result != __instance.Owner || target != petOwnerCreature)
			{
				return;
			}

			if (petOwnerCreature.GetPower<SlipperyPower>() is not SlipperyPower slippery || slippery.Amount <= 0m)
			{
				return;
			}

			long commandId = CurrentActualDamageCommandId;
			if (commandId == 0L)
			{
				return;
			}

			if (!OstyRedirectSlipperyByCommand.TryGetValue(commandId, out HashSet<SlipperyPower>? pending))
			{
				pending = [];
				OstyRedirectSlipperyByCommand[commandId] = pending;
			}

			pending.Add(slippery);
		}
	}
}
