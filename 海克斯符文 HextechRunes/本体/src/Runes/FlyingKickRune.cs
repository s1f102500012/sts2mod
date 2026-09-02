using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Models.Exceptions;

namespace HextechRunes;

public sealed class FlyingKickRune : HextechRelicBase
{
	private const string BaseExecutePercentVar = "BaseExecutePercent";
	private const string OwnerMaxHpToExecutePercentVar = "OwnerMaxHpToExecutePercent";
	private const string ExecutePercentVar = "ExecutePercent";

	private bool _executing;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar(BaseExecutePercentVar, 10m),
		new DynamicVar(OwnerMaxHpToExecutePercentVar, 8m),
		new DynamicVar(ExecutePercentVar, 10m),
		new HealVar(10m)
	];

	public void RefreshExecutePercentFromOwner()
	{
		Player? owner;
		try
		{
			owner = Owner;
		}
		catch (CanonicalModelException)
		{
			return;
		}

		if (owner == null)
		{
			return;
		}

		RefreshExecutePercent(owner.Creature.MaxHp);
	}

	public decimal RefreshExecutePercent(decimal ownerMaxHp)
	{
		decimal executePercent = DynamicVars[BaseExecutePercentVar].BaseValue
			+ ownerMaxHp * DynamicVars[OwnerMaxHpToExecutePercentVar].BaseValue / 100m;
		DynamicVars[ExecutePercentVar].BaseValue = executePercent;
		return executePercent;
	}

	public override async Task AfterDamageGiven(
		PlayerChoiceContext choiceContext,
		Creature? dealer,
		DamageResult result,
		ValueProp props,
		Creature target,
		CardModel? cardSource)
	{
		if (_executing
			|| Owner == null
			|| Owner.Creature.IsDead
			|| target.Side != CombatSide.Enemy
			|| result.UnblockedDamage <= 0m
			|| !IsDamageFromOwner(dealer, cardSource))
		{
			return;
		}

		if (result.WasTargetKilled)
		{
			await TriggerFlyingKick(choiceContext, target, killTarget: false);
			return;
		}

		if (!target.IsAlive)
		{
			return;
		}

		decimal executePercent = RefreshExecutePercent(Owner.Creature.MaxHp);
		decimal threshold = target.MaxHp * executePercent / 100m;
		if (target.CurrentHp >= threshold)
		{
			return;
		}

		await TriggerFlyingKick(choiceContext, target, killTarget: true);
	}

	private async Task TriggerFlyingKick(PlayerChoiceContext choiceContext, Creature target, bool killTarget)
	{
		if (_executing || Owner == null || Owner.Creature.IsDead)
		{
			return;
		}

		_executing = true;
		try
		{
			Flash([target]);
			// 处决表现:原版大斩击+本体色爆闪(即刻),半秒后治疗绿光弧线流回自身;
			// 尸体横飞由 FlyingKickCorpseLaunchDriver 在 Kill 内接管,三段时序互补。
			HextechCombatVfx.FlyingKickStrike(target, Owner.Creature);
			// 收集者共享处决计数:真死亡判定要在 Kill 之前算,Kill 后怪物已移出战斗、CombatState 为空。
			bool creditable = CollectorRune.IsCreditableDeath(target);
			if (killTarget)
			{
				FlyingKickCorpseLaunchDriver.MarkPending(target);
				await CreatureCmd.Kill(target);
			}
			else if (HextechMonsterInteractionPolicy.IsTrueCombatDeath(target))
			{
				FlyingKickCorpseLaunchDriver.MarkPendingUntilConsumed(target);
			}

			Owner.GetRelic<CollectorRune>()?.RecordExecution(target, creditable);
		}
		finally
		{
			if (killTarget)
			{
				FlyingKickCorpseLaunchDriver.ClearPending(target);
			}

			_executing = false;
		}

		if (Owner.Creature.IsAlive)
		{
			decimal heal = Math.Max(1m, decimal.Floor(Owner.Creature.MaxHp * DynamicVars.Heal.BaseValue / 100m));
			await CreatureCmd.Heal(Owner.Creature, heal);
		}
	}

	[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.DynamicDescription), MethodType.Getter)]
	[HextechPatch("rune.flying-kick.description", "飞踢", Rune = typeof(FlyingKickRune))]
	private static class FlyingKickDescriptionPatch
	{
		[HarmonyPrefix]
		private static void Prefix(RelicModel __instance)
		{
			if (__instance is FlyingKickRune flyingKickRune)
			{
				flyingKickRune.RefreshExecutePercentFromOwner();
			}
		}
	}

	[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim), typeof(bool))]
	[HextechPatch("rune.flying-kick.corpse-launch", "飞踢尸体击飞视觉")]
	private static class FlyingKickCorpseLaunchPatch
	{
		[HarmonyPrepare]
		private static bool Prepare()
		{
			if (HextechRuntimeRuneCompatibility.IsAndroidRuntime)
			{
				Log.Warn($"[{ModInfo.Id}][Mayhem][Compat] Flying Kick corpse launch visual hook skipped on Android runtime.");
				return false;
			}

			return true;
		}

		[HarmonyPostfix]
		private static void Postfix(NCreature __instance, bool shouldRemove)
		{
			if (!FlyingKickCorpseLaunchDriver.TryConsumePending(__instance.Entity))
			{
				return;
			}

			if (!shouldRemove
				|| __instance.Entity == null
				|| !HextechMonsterInteractionPolicy.IsTrueCombatDeath(__instance.Entity))
			{
				return;
			}

			FlyingKickCorpseLaunchDriver.TryAttach(__instance);
		}
	}
}
