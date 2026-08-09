using Godot;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace IntegratedStrategyEvents.Encounters;

public sealed class Mio : MonsterModel
{
	public const string DaggerMoveId = "DAGGER_MOVE";
	public const string ShortBladeMoveId = "SHORT_BLADE_MOVE";
	public const string DrawSwordMoveId = "DRAW_SWORD_MOVE";
	public const string ColdRadianceMoveId = "COLD_RADIANCE_MOVE";

	private const int InitialHp = 277;
	private const int StandardDamage = 12;
	private const int ColdRadianceDamage = 16;
	private const int AttackHits = 2;
	private const int StrengthGain = 2;
	private const int VulnerableAmount = 1;
	private const float AttackHitDelay = 0.55f;
	private const string SkillTrigger = "SkillTrigger";
	private const string AttackSfxPath =
		"event:/sfx/enemy/enemy_attacks/the_kin_minion/the_kin_minion_quick_slash";

	public override int MinInitialHp => InitialHp;

	public override int MaxInitialHp => InitialHp;

	public override bool HasDeathSfx => false;

	public override bool HasHurtSfx => false;

	public override bool ShouldFadeAfterDeath => true;

	public override float DeathAnimLengthOverride => 1.2f;

	public override DamageSfxType TakeDamageSfxType => DamageSfxType.Fur;

	protected override string VisualsPath => "res://IntegratedStrategyEvents/scenes/creature_visuals/mio.tscn";

	public override Vector2 ExtraDeathVfxPadding => Vector2.One * 0.6f;

	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		MoveState dagger = new(
			DaggerMoveId,
			DaggerMove,
			new MultiAttackIntent(StandardDamage, AttackHits));
		MoveState shortBlade = new(
			ShortBladeMoveId,
			ShortBladeMove,
			new MultiAttackIntent(StandardDamage, AttackHits),
			new BuffIntent());
		MoveState drawSword = new(
			DrawSwordMoveId,
			DrawSwordMove,
			new MultiAttackIntent(StandardDamage, AttackHits),
			new DebuffIntent());
		MoveState coldRadiance = new(
			ColdRadianceMoveId,
			ColdRadianceMove,
			new MultiAttackIntent(ColdRadianceDamage, AttackHits));

		dagger.FollowUpState = shortBlade;
		shortBlade.FollowUpState = drawSword;
		drawSword.FollowUpState = coldRadiance;
		coldRadiance.FollowUpState = dagger;
		return new MonsterMoveStateMachine([dagger, shortBlade, drawSword, coldRadiance], dagger);
	}

	private async Task DaggerMove(IReadOnlyList<Creature> targets)
	{
		_ = targets;
		await DoubleStrike(StandardDamage, CreatureAnimator.attackTrigger);
	}

	private async Task ShortBladeMove(IReadOnlyList<Creature> targets)
	{
		_ = targets;
		await DoubleStrike(StandardDamage, CreatureAnimator.attackTrigger);
		await PowerCmd.Apply<StrengthPower>(Creature, StrengthGain, Creature, null);
	}

	private async Task DrawSwordMove(IReadOnlyList<Creature> targets)
	{
		await DoubleStrike(StandardDamage, CreatureAnimator.attackTrigger);
		await PowerCmd.Apply<VulnerablePower>(targets, VulnerableAmount, Creature, null);
	}

	private async Task ColdRadianceMove(IReadOnlyList<Creature> targets)
	{
		_ = targets;
		await DoubleStrike(ColdRadianceDamage, SkillTrigger);
	}

	private async Task DoubleStrike(int damage, string triggerName)
	{
		await DamageCmd.Attack(damage)
			.FromMonster(this)
			.WithHitCount(AttackHits)
			.WithAttackerAnim(triggerName, 0f)
			.WithWaitBeforeHit(AttackHitDelay, AttackHitDelay)
			.WithHitFx("vfx/vfx_attack_slash", AttackSfxPath)
			.OnlyPlayAnimOnce()
			.Execute(null);
	}

	public override CreatureAnimator GenerateAnimator(MegaSprite controller)
	{
		AnimState idle = new("Idle", isLooping: true);
		AnimState attack = new("Attack")
		{
			NextState = idle
		};
		AnimState skill = new("Skill")
		{
			NextState = idle
		};
		AnimState die = new("Die");

		CreatureAnimator animator = new(idle, controller);
		animator.AddAnyState(CreatureAnimator.idleTrigger, idle);
		animator.AddAnyState(CreatureAnimator.attackTrigger, attack);
		animator.AddAnyState(SkillTrigger, skill);
		animator.AddAnyState(CreatureAnimator.deathTrigger, die);
		return animator;
	}
}
