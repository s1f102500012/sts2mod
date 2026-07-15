using Godot;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using IntegratedStrategyEvents.Powers;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;

namespace IntegratedStrategyEvents.Encounters;

public sealed class SorrowfulLock : MonsterModel
{
	public const string BossSlot = "sorrowful_lock";
	public const string AssemblyLeftSlot = "sorrowful_lock_assembly_left";
	public const string AssemblyRightSlot = "sorrowful_lock_assembly_right";

	public const string SlapMoveId = "SLAP_MOVE";
	public const string ProtectiveBarrierMoveId = "PROTECTIVE_BARRIER_MOVE";
	public const string ManipulateMoveId = "MANIPULATE_MOVE";
	public const string FinishMoveId = "FINISH_MOVE";

	private const int InitialHp = 1000;
	private const decimal InitialMasterwork = MasterworkPower.HitsPerTrigger;
	private const decimal InitialBarricade = 1m;
	private const int SlapDamage = 10;
	private const int SlapHits = 2;
	private const decimal BarrierBlockRatio = 0.2m;
	private const decimal ManipulateStrengthGain = 2m;
	private const int FinishDamage = 40;
	private const decimal MoveHealRatio = 0.05m;
	private const float SlapHitDelay = 0.6f;
	private const float BarrierDelay = 0.95f;
	private const float ManipulateEndDelay = 0.8f;
	private const float SummonDelay = 0.75f;
	private const float FinishHitDelay = 0.85f;
	private const float HealDelay = 0.35f;
	private const string BarrierTrigger = "BarrierTrigger";
	private const string ManipulateTrigger = "ManipulateTrigger";
	private const string FinishTrigger = "FinishTrigger";
	private const string AttackSfxPath = "event:/sfx/enemy/enemy_attacks/spectral_knight/spectral_knight_soul_slash";
	private const string HeavyAttackSfxPath = "event:/sfx/enemy/enemy_attacks/soul_fysh/soul_fysh_wave";
	private const string BuffSfxPath = "event:/sfx/enemy/enemy_attacks/knowledge_demon/knowledge_demon_flame";

	private bool _inBarrierPose;

	public override int MinInitialHp => InitialHp;

	public override int MaxInitialHp => InitialHp;

	public override bool HasDeathSfx => false;

	public override bool HasHurtSfx => false;

	public override bool ShouldFadeAfterDeath => true;

	public override float DeathAnimLengthOverride => 1.35f;

	public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

	protected override string VisualsPath =>
		"res://IntegratedStrategyEvents/scenes/creature_visuals/sorrowful_lock.tscn";

	public override Vector2 ExtraDeathVfxPadding => Vector2.One * 0.8f;

	public override async Task AfterAddedToRoom()
	{
		await base.AfterAddedToRoom();
		await PowerCmd.Apply<MasterworkPower>(Creature, InitialMasterwork, Creature, null, silent: true);
		await PowerCmd.Apply<BarricadePower>(Creature, InitialBarricade, Creature, null, silent: true);
	}

	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		MoveState slap = new(
			SlapMoveId,
			SlapMove,
			new MultiAttackIntent(SlapDamage, SlapHits),
			new HealIntent());
		MoveState protectiveBarrier = new(
			ProtectiveBarrierMoveId,
			ProtectiveBarrierMove,
			new DefendIntent(),
			new HealIntent());
		MoveState manipulate = new(
			ManipulateMoveId,
			ManipulateMove,
			new SummonIntent(),
			new BuffIntent(),
			new HealIntent());
		MoveState finish = new(
			FinishMoveId,
			FinishMove,
			new SingleAttackIntent(FinishDamage),
			new HealIntent());

		slap.FollowUpState = protectiveBarrier;
		protectiveBarrier.FollowUpState = manipulate;
		manipulate.FollowUpState = finish;
		finish.FollowUpState = slap;

		return new MonsterMoveStateMachine(
			[slap, protectiveBarrier, manipulate, finish],
			slap);
	}

	private async Task SlapMove(IReadOnlyList<Creature> targets)
	{
		_ = targets;
		await DamageCmd.Attack(SlapDamage)
			.FromMonster(this)
			.WithHitCount(SlapHits)
			.WithAttackerAnim(CreatureAnimator.attackTrigger, 0f)
			.WithWaitBeforeHit(SlapHitDelay, SlapHitDelay)
			.WithHitFx("vfx/vfx_attack_blunt", AttackSfxPath)
			.OnlyPlayAnimOnce()
			.Execute(null);
		await ConsumeVigorAfterAttack();
		await HealAfterMove();
	}

	private async Task ProtectiveBarrierMove(IReadOnlyList<Creature> targets)
	{
		_ = targets;
		SfxCmd.Play(BuffSfxPath);
		_inBarrierPose = true;
		await MonsterAnimationHelper.TriggerAnimWithFixedWait(Creature, BarrierTrigger, BarrierDelay);
		await CreatureCmd.GainBlock(Creature, GetBarrierBlock(), ValueProp.Move, null);
		await HealAfterMove();
	}

	private async Task ManipulateMove(IReadOnlyList<Creature> targets)
	{
		_ = targets;
		_inBarrierPose = false;
		await MonsterAnimationHelper.TriggerAnimWithFixedWait(Creature, ManipulateTrigger, ManipulateEndDelay);
		// 先填靠近大锁的右槽，再填左槽。
		await SummonAssemblyInFirstOpenSlot(AssemblyRightSlot, AssemblyLeftSlot);
		SfxCmd.Play(BuffSfxPath);
		await PowerCmd.Apply<StrengthPower>(Creature, ManipulateStrengthGain, Creature, null);
		await HealAfterMove();
	}

	private async Task FinishMove(IReadOnlyList<Creature> targets)
	{
		_ = targets;
		await DamageCmd.Attack(FinishDamage)
			.FromMonster(this)
			.WithAttackerAnim(FinishTrigger, 0f)
			.WithWaitBeforeHit(FinishHitDelay, FinishHitDelay)
			.WithHitFx("vfx/vfx_attack_blunt", HeavyAttackSfxPath)
			.Execute(null);
		await ConsumeVigorAfterAttack();
		await HealAfterMove();
	}

	// 原版 VigorPower 只对卡牌来源的攻击登记消耗（BeforeAttack 里有 ModelSource is CardModel 守卫），
	// 怪物攻击会白吃加成且层数永不清空，因此攻击动作结束后手动消耗活力。
	private async Task ConsumeVigorAfterAttack()
	{
		if (Creature.IsDead || !Creature.HasPower<VigorPower>())
		{
			return;
		}

		await PowerCmd.Remove<VigorPower>(Creature);
	}

	private async Task HealAfterMove()
	{
		if (Creature.IsDead)
		{
			return;
		}

		await Cmd.Wait(HealDelay);
		await CreatureCmd.Heal(Creature, Math.Ceiling(Creature.MaxHp * MoveHealRatio));
	}

	private decimal GetBarrierBlock()
	{
		return Math.Ceiling(Creature.MaxHp * BarrierBlockRatio);
	}

	private async Task SummonAssemblyInFirstOpenSlot(params string[] slotNames)
	{
		foreach (string slotName in slotNames)
		{
			if (!CanSummonAssembly(slotName))
			{
				continue;
			}

			Creature assembly = await CreatureCmd.Add<TheaterAssembly>(CombatState, slotName);
			if (!assembly.HasPower<MinionPower>())
			{
				await PowerCmd.Apply<MinionPower>(assembly, 1m, Creature, null, silent: true);
			}

			await Cmd.Wait(SummonDelay);
			return;
		}
	}

	private bool CanSummonAssembly(string slotName)
	{
		return CombatState != null &&
			!Creature.IsDead &&
			CombatState.Encounter?.Slots.Contains(slotName) == true &&
			!IsSummonSlotOccupied(slotName);
	}

	private bool IsSummonSlotOccupied(string slotName)
	{
		return CombatState?.Enemies.Any(creature => creature.IsAlive && creature.SlotName == slotName) == true;
	}

	public override CreatureAnimator GenerateAnimator(MegaSprite controller)
	{
		AnimState idle = new("Idle", isLooping: true);
		AnimState attack = new("Attack")
		{
			NextState = idle
		};
		AnimState barrierLoop = new("Skill_1_Loop", isLooping: true);
		AnimState barrierStart = new("Skill_1_Start")
		{
			NextState = barrierLoop
		};
		AnimState manipulateAttack = new("Attack")
		{
			NextState = idle
		};
		AnimState manipulateEnd = new("Skill_1_End")
		{
			NextState = manipulateAttack
		};
		AnimState finish = new("Skill_3")
		{
			NextState = idle
		};
		AnimState die = new("Die");

		CreatureAnimator animator = new(idle, controller);
		// 保护屏障的姿态在解除前保持 Skill_1_Loop，不被常规 idle 触发打断。
		animator.AddAnyState(CreatureAnimator.idleTrigger, barrierLoop, () => _inBarrierPose);
		animator.AddAnyState(CreatureAnimator.idleTrigger, idle, () => !_inBarrierPose);
		animator.AddAnyState(CreatureAnimator.attackTrigger, attack);
		animator.AddAnyState(BarrierTrigger, barrierStart);
		animator.AddAnyState(ManipulateTrigger, manipulateEnd);
		animator.AddAnyState(FinishTrigger, finish);
		animator.AddAnyState(CreatureAnimator.deathTrigger, die);
		return animator;
	}
}
