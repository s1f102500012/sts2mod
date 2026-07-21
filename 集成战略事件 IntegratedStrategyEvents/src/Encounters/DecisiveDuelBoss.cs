using Godot;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace IntegratedStrategyEvents.Encounters;

public abstract class DecisiveDuelBoss : MonsterModel
{
	public const string VictoryTrigger = "VictoryTrigger";

	private const float AttackFollowUpHitDelay = 0.08f;
	private const string AttackHitSfxPath =
		"event:/sfx/enemy/enemy_attacks/the_insatiable/the_insatiable_thrash";
	private const string AttackVfxPath = "vfx/vfx_attack_slash";
	private const string CrashLandingVfxPath = "vfx/vfx_heavy_blunt";

	public override int MinInitialHp => InitialHp;

	public override int MaxInitialHp => InitialHp;

	public override bool HasDeathSfx => false;

	public override bool HasHurtSfx => false;

	public override bool ShouldFadeAfterDeath => true;

	public override float DeathAnimLengthOverride => 1.2f;

	public override DamageSfxType TakeDamageSfxType => DamageSfxType.Fur;

	public override Vector2 ExtraDeathVfxPadding => Vector2.One * 0.8f;

	protected abstract int InitialHp { get; }

	protected async Task AttackAllOtherUnits(
		int damage,
		string triggerName,
		float firstHitDelay,
		int hitCount = 1)
	{
		int vigorAmountAtStart = Creature.GetPower<VigorPower>()?.Amount ?? 0;
		await CreatureCmd.TriggerAnim(Creature, triggerName, 0f);
		await Cmd.CustomScaledWait(firstHitDelay, firstHitDelay);

		for (int i = 0; i < hitCount; i++)
		{
			if (i > 0)
			{
				await Cmd.CustomScaledWait(AttackFollowUpHitDelay, AttackFollowUpHitDelay);
			}

			await DamageAllOtherLivingUnits(damage);
		}

		await SpendVigorUsedByAttack(vigorAmountAtStart);
	}

	protected async Task AttackAllOtherUnitsWithCrashLandingVfx<TRivalMonster>(
		int damage,
		string triggerName,
		float firstHitDelay)
		where TRivalMonster : MonsterModel
	{
		int vigorAmountAtStart = Creature.GetPower<VigorPower>()?.Amount ?? 0;
		await CreatureCmd.TriggerAnim(Creature, triggerName, 0f);
		await Cmd.CustomScaledWait(firstHitDelay, firstHitDelay);

		Creature? vfxTarget = GetCrashLandingVfxTarget<TRivalMonster>();
		if (vfxTarget != null)
		{
			VfxCmd.PlayOnCreature(vfxTarget, CrashLandingVfxPath);
		}

		await DamageAllOtherLivingUnits(damage, shouldSpawnDefaultHitVfx: false);
		await SpendVigorUsedByAttack(vigorAmountAtStart);
	}

	protected async Task PrepareWithLoopingAnimation(string triggerName, float waitTime)
	{
		await TriggerAnimWithFixedWait(triggerName, waitTime);
		await PowerCmd.Apply<VigorPower>(Creature, 10m, Creature, null);
	}

	protected Task TriggerAnimWithFixedWait(string triggerName, float waitTime)
	{
		return MonsterAnimationHelper.TriggerAnimWithFixedWait(Creature, triggerName, waitTime);
	}

	protected async Task ApplyToOtherLivingUnits<TPower>(int amount)
		where TPower : PowerModel
	{
		await ApplyToOtherLivingUnits<TPower>(amount, amount);
	}

	protected async Task ApplyToOtherLivingUnits<TPower>(int playerAmount, int nonPlayerAmount)
		where TPower : PowerModel
	{
		List<Creature> playerTargets = [];
		List<Creature> nonPlayerTargets = [];
		foreach (Creature target in GetOtherLivingUnits())
		{
			if (target.IsPlayer)
			{
				playerTargets.Add(target);
			}
			else
			{
				nonPlayerTargets.Add(target);
			}
		}

		if (playerTargets.Count > 0)
		{
			await PowerCmd.Apply<TPower>(playerTargets, playerAmount, Creature, null);
		}

		if (nonPlayerTargets.Count > 0)
		{
			await PowerCmd.Apply<TPower>(nonPlayerTargets, nonPlayerAmount, Creature, null);
		}
	}

	private async Task DamageAllOtherLivingUnits(int damage, bool shouldSpawnDefaultHitVfx = true)
	{
		IReadOnlyList<Creature> targets = GetOtherLivingUnits();
		if (targets.Count == 0)
		{
			return;
		}

		List<Creature> playerTargets = [];
		List<Creature> nonPlayerTargets = [];
		foreach (Creature target in targets)
		{
			if (target.IsPlayer)
			{
				playerTargets.Add(target);
			}
			else
			{
				nonPlayerTargets.Add(target);
			}
		}

		if (playerTargets.Count > 0)
		{
			var playerAttack = DamageCmd.Attack(damage)
				.FromMonster(this)
				.WithNoAttackerAnim()
				.WithHitFx(shouldSpawnDefaultHitVfx ? AttackVfxPath : null, AttackHitSfxPath);
			if (shouldSpawnDefaultHitVfx)
			{
				playerAttack.SpawningHitVfxOnEachCreature();
			}

			await playerAttack.Execute(new BlockingPlayerChoiceContext());
		}

		if (nonPlayerTargets.Count > 0)
		{
			if (playerTargets.Count == 0)
			{
				SfxCmd.Play(AttackHitSfxPath);
			}

			if (shouldSpawnDefaultHitVfx)
			{
				VfxCmd.PlayOnCreatureCenters(nonPlayerTargets, AttackVfxPath);
			}

#if STS2_109_OR_NEWER
			await CreatureCmd.Damage(
				new BlockingPlayerChoiceContext(),
				nonPlayerTargets,
				damage,
				ValueProp.Move,
				Creature,
				null,
				null);
#else
			await CreatureCmd.Damage(
				new BlockingPlayerChoiceContext(),
				nonPlayerTargets,
				damage,
				ValueProp.Move,
				Creature,
				null);
#endif
		}
	}

	private IReadOnlyList<Creature> GetOtherLivingUnits()
	{
		Creature self = Creature;
		if (self.CombatState == null)
		{
			return [];
		}

		return self.CombatState.Creatures
			.Where(creature => creature.IsAlive && !ReferenceEquals(creature, self))
			.ToList();
	}

	private Creature? GetCrashLandingVfxTarget<TRivalMonster>()
		where TRivalMonster : MonsterModel
	{
		Creature self = Creature;
		if (self.CombatState == null)
		{
			return null;
		}

		Creature? livingRival = self.CombatState.Creatures.FirstOrDefault(creature =>
			creature.IsAlive &&
			!ReferenceEquals(creature, self) &&
			creature.Monster?.CanonicalInstance is TRivalMonster);
		if (livingRival != null)
		{
			return livingRival;
		}

		return self.CombatState.PlayerCreatures.FirstOrDefault(creature => creature.IsAlive);
	}

	private async Task SpendVigorUsedByAttack(int vigorAmountAtStart)
	{
		if (vigorAmountAtStart <= 0)
		{
			return;
		}

		VigorPower? vigor = Creature.GetPower<VigorPower>();
		if (vigor != null)
		{
			await PowerCmd.ModifyAmount(vigor, -vigorAmountAtStart, Creature, null);
		}
	}
}
