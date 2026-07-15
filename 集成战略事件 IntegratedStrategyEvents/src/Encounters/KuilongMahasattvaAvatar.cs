using Godot;
using IntegratedStrategyEvents.Powers;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace IntegratedStrategyEvents.Encounters;

public sealed class KuilongMahasattvaAvatar : MonsterModel
{
	private enum Phase
	{
		Meditation,
		Free,
		Reviving,
		Worryless
	}

	public const string BossSlot = "kuilong";
	public const string LotusLeftSlot = "kuilong_lotus_left";
	public const string LotusRightSlot = "kuilong_lotus_right";

	public const string ReviveToWorrylessMoveId = "REVIVE_TO_WORRYLESS_MOVE";
	public const string MeditationBearThreeBodiesMoveId = "MEDITATION_BEAR_THREE_BODIES_MOVE";
	public const string FreeWrathfulGazeMoveId = "FREE_WRATHFUL_GAZE_MOVE";
	public const string FreeRiseMoveId = "FREE_RISE_MOVE";
	public const string FreeClearFireMoveId = "FREE_CLEAR_FIRE_MOVE";
	public const string FreePunishFivePreceptsMoveId = "FREE_PUNISH_FIVE_PRECEPTS_MOVE";
	public const string WorrylessNiruFireMoveId = "WORRYLESS_NIRU_FIRE_MOVE";
	public const string WorrylessDoubleSlashMoveId = "WORRYLESS_DOUBLE_SLASH_MOVE";
	public const string WorrylessBearThreeBodiesMoveId = "WORRYLESS_BEAR_THREE_BODIES_MOVE";
	public const string WorrylessPunishFivePreceptsMoveId = "WORRYLESS_PUNISH_FIVE_PRECEPTS_MOVE";

	// 冥想阶段的"无限"生命值。联机时 CombatState 会按 人数×幕系数(至多4×1.3) 缩放怪物 HP，
	// 结果必须落在 int 范围内，否则 decimal→int 转换抛 OverflowException 导致进战斗黑屏。
	private const int InfiniteHp = 300000000;
	private const int PhaseHp = 500;
	private const int WrathfulGazeDamage = 30;
	private const int PunishFivePreceptsDamage = 10;
	private const int PunishFivePreceptsHits = 5;
	private const decimal SoarGain = 1m;
	private const decimal NiruFireStrength = 2m;
	private const int DoubleSlashDamage = 20;
	private const int DoubleSlashHits = 2;
	private const float SummonDelay = 0.8f;
	private const float PhaseTransitionDelay = 1.25f;
	private const float ReviveDelay = 1.35f;
	private const float SingleAttackDelay = 0.65f;
	private const float MultiAttackDelay = 0.45f;
	private const float BuffDelay = 0.7f;
	private const string EnterFreePhaseTrigger = "EnterFreePhase";
	private const string FreeRiseTrigger = "FreeRise";
	private const string FreeClearFireTrigger = "FreeClearFire";
	private const string FreePunishTrigger = "FreePunish";
	private const string ReviveEndTrigger = "ReviveEnd";
	private const string WorrylessPunishTrigger = "WorrylessPunish";
	private const string AttackSfxPath = "event:/sfx/enemy/enemy_attacks/spectral_knight/spectral_knight_soul_slash";
	private const string HeavyAttackSfxPath = "event:/sfx/enemy/enemy_attacks/soul_fysh/soul_fysh_wave";
	private const string BuffSfxPath = "event:/sfx/enemy/enemy_attacks/knowledge_demon/knowledge_demon_flame";

	private MoveState? _reviveState;
	private MoveState? _freeFirstState;
	private Phase _phase = Phase.Meditation;
	private bool _hasRevived;
	private bool _isReviving;

	public override int MinInitialHp => InfiniteHp;

	public override int MaxInitialHp => InfiniteHp;

	public override bool HasDeathSfx => false;

	public override bool HasHurtSfx => false;

	public override bool ShouldFadeAfterDeath => true;

	public override bool ShouldDisappearFromDoom => HasRevived;

	public override float DeathAnimLengthOverride => 1.45f;

	public override DamageSfxType TakeDamageSfxType => DamageSfxType.Magic;

	protected override string VisualsPath =>
		"res://IntegratedStrategyEvents/scenes/creature_visuals/kuilong_mahasattva_avatar.tscn";

	public override Vector2 ExtraDeathVfxPadding => Vector2.One * 0.9f;

	public bool HasRevived => _hasRevived;

	public bool IsMeditationPhase => _phase == Phase.Meditation;

	private bool IsFreePhase => _phase == Phase.Free;

	private bool IsWorrylessPhase => _phase == Phase.Worryless;

	public override async Task AfterAddedToRoom()
	{
		await base.AfterAddedToRoom();
		if (_phase == Phase.Meditation)
		{
			await CreatureCmd.SetMaxAndCurrentHp(Creature, InfiniteHp);
			Creature.HpDisplay = HpDisplay.InfiniteWithoutNumbers;
			await PowerCmd.Apply<NonAttachmentPower>(Creature, 1m, Creature, null, silent: true);
		}
	}

	public override Task AfterDeath(
		PlayerChoiceContext choiceContext,
		Creature creature,
		bool wasRemovalPrevented,
		float deathAnimLength)
	{
		_ = choiceContext;
		_ = wasRemovalPrevented;
		_ = deathAnimLength;
		if (creature != Creature || HasRevived || _isReviving || _phase != Phase.Free)
		{
			return Task.CompletedTask;
		}

		_isReviving = true;
		_phase = Phase.Reviving;
		TriggerReviveWaitingState();
		return Task.CompletedTask;
	}

	public override bool ShouldAllowHitting(Creature creature)
	{
		return creature != Creature || !_isReviving;
	}

	public override bool ShouldStopCombatFromEnding()
	{
		return Creature is { IsDead: true } && (_isReviving || (_phase == Phase.Free && !HasRevived));
	}

	public override bool ShouldCreatureBeRemovedFromCombatAfterDeath(Creature creature)
	{
		return creature != Creature || HasRevived;
	}

	public async Task EnterFreePhase()
	{
		if (_phase != Phase.Meditation || Creature.IsDead)
		{
			return;
		}

		if (_freeFirstState == null)
		{
			return;
		}

		_phase = Phase.Free;
		SetMoveImmediate(_freeFirstState, forceTransition: true);
		await RemoveAllPowers();
		await MonsterAnimationHelper.TriggerAnimWithFixedWait(Creature, EnterFreePhaseTrigger, PhaseTransitionDelay);
		Creature.HpDisplay = HpDisplay.Normal;
		await CreatureCmd.SetMaxAndCurrentHp(Creature, GetScaledPhaseHp());
	}

	private void TriggerReviveWaitingState()
	{
		if (_reviveState != null)
		{
			SetMoveImmediate(_reviveState, forceTransition: true);
		}
	}

	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		MoveState revive = new(
			ReviveToWorrylessMoveId,
			ReviveToWorrylessMove,
			new HealIntent(),
			new BuffIntent())
		{
			MustPerformOnceBeforeTransitioning = true
		};
		MoveState meditationBearThreeBodies = new(
			MeditationBearThreeBodiesMoveId,
			MeditationBearThreeBodiesMove,
			new SummonIntent());
		MoveState freeWrathfulGaze = new(
			FreeWrathfulGazeMoveId,
			FreeWrathfulGazeMove,
			new SingleAttackIntent(WrathfulGazeDamage));
		MoveState freeRise = new(
			FreeRiseMoveId,
			FreeRiseMove,
			new BuffIntent());
		MoveState freeClearFire = new(
			FreeClearFireMoveId,
			FreeClearFireMove,
			new BuffIntent());
		MoveState freePunishFivePrecepts = new(
			FreePunishFivePreceptsMoveId,
			FreePunishFivePreceptsMove,
			new MultiAttackIntent(PunishFivePreceptsDamage, PunishFivePreceptsHits));
		MoveState worrylessNiruFire = new(
			WorrylessNiruFireMoveId,
			WorrylessNiruFireMove,
			new BuffIntent());
		MoveState worrylessDoubleSlash = new(
			WorrylessDoubleSlashMoveId,
			WorrylessDoubleSlashMove,
			new MultiAttackIntent(DoubleSlashDamage, DoubleSlashHits));
		MoveState worrylessBearThreeBodies = new(
			WorrylessBearThreeBodiesMoveId,
			WorrylessBearThreeBodiesMove,
			new SummonIntent());
		MoveState worrylessPunishFivePrecepts = new(
			WorrylessPunishFivePreceptsMoveId,
			WorrylessPunishFivePreceptsMove,
			new MultiAttackIntent(PunishFivePreceptsDamage, PunishFivePreceptsHits));

		_reviveState = revive;
		_freeFirstState = freeWrathfulGaze;

		meditationBearThreeBodies.FollowUpState = meditationBearThreeBodies;

		freeWrathfulGaze.FollowUpState = freeRise;
		freeRise.FollowUpState = freeClearFire;
		freeClearFire.FollowUpState = freePunishFivePrecepts;
		freePunishFivePrecepts.FollowUpState = freeWrathfulGaze;

		revive.FollowUpState = worrylessNiruFire;
		worrylessNiruFire.FollowUpState = worrylessDoubleSlash;
		worrylessDoubleSlash.FollowUpState = worrylessBearThreeBodies;
		worrylessBearThreeBodies.FollowUpState = worrylessPunishFivePrecepts;
		worrylessPunishFivePrecepts.FollowUpState = worrylessNiruFire;

		return new MonsterMoveStateMachine(
			[
				revive,
				meditationBearThreeBodies,
				freeWrathfulGaze,
				freeRise,
				freeClearFire,
				freePunishFivePrecepts,
				worrylessNiruFire,
				worrylessDoubleSlash,
				worrylessBearThreeBodies,
				worrylessPunishFivePrecepts
			],
			meditationBearThreeBodies);
	}

	private async Task ReviveToWorrylessMove(IReadOnlyList<Creature> targets)
	{
		_ = targets;
		_phase = Phase.Worryless;
		_hasRevived = true;
		Creature.HpDisplay = HpDisplay.Normal;
		await MonsterAnimationHelper.TriggerAnimWithFixedWait(Creature, ReviveEndTrigger, ReviveDelay);
		await CreatureCmd.SetMaxAndCurrentHp(Creature, GetScaledPhaseHp());
		_isReviving = false;
	}

	private async Task MeditationBearThreeBodiesMove(IReadOnlyList<Creature> targets)
	{
		_ = targets;
		await MonsterAnimationHelper.TriggerAnimWithFixedWait(Creature, CreatureAnimator.attackTrigger, SummonDelay);
		await SummonLotusInFirstOpenSlot(LotusLeftSlot, LotusRightSlot);
	}

	private async Task FreeWrathfulGazeMove(IReadOnlyList<Creature> targets)
	{
		_ = targets;
		await DamageCmd.Attack(WrathfulGazeDamage)
			.FromMonster(this)
			.WithAttackerAnim(CreatureAnimator.attackTrigger, 0f)
			.WithWaitBeforeHit(SingleAttackDelay, SingleAttackDelay)
			.WithHitFx("vfx/vfx_attack_blunt", HeavyAttackSfxPath)
			.Execute(null);
	}

	private async Task FreeRiseMove(IReadOnlyList<Creature> targets)
	{
		_ = targets;
		SfxCmd.Play(BuffSfxPath);
		await MonsterAnimationHelper.TriggerAnimWithFixedWait(Creature, FreeRiseTrigger, BuffDelay);
		await PowerCmd.Apply<SoarPower>(Creature, SoarGain, Creature, null);
	}

	private async Task FreeClearFireMove(IReadOnlyList<Creature> targets)
	{
		_ = targets;
		SfxCmd.Play(BuffSfxPath);
		await MonsterAnimationHelper.TriggerAnimWithFixedWait(Creature, FreeClearFireTrigger, BuffDelay);
		await PowerCmd.Remove<SoarPower>(Creature);
		await RemoveDebuffs();
	}

	private async Task FreePunishFivePreceptsMove(IReadOnlyList<Creature> targets)
	{
		_ = targets;
		await PunishFivePrecepts(FreePunishTrigger);
	}

	private async Task WorrylessNiruFireMove(IReadOnlyList<Creature> targets)
	{
		_ = targets;
		SfxCmd.Play(BuffSfxPath);
		await MonsterAnimationHelper.TriggerAnimWithFixedWait(Creature, CreatureAnimator.attackTrigger, BuffDelay);
		await PowerCmd.Apply<StrengthPower>(Creature, NiruFireStrength, Creature, null);
	}

	private async Task WorrylessDoubleSlashMove(IReadOnlyList<Creature> targets)
	{
		_ = targets;
		await DamageCmd.Attack(DoubleSlashDamage)
			.FromMonster(this)
			.WithHitCount(DoubleSlashHits)
			.WithAttackerAnim(CreatureAnimator.attackTrigger, 0f)
			.WithWaitBeforeHit(SingleAttackDelay, SingleAttackDelay)
			.WithHitFx("vfx/vfx_attack_slash", AttackSfxPath)
			.OnlyPlayAnimOnce()
			.Execute(null);
	}

	private async Task WorrylessBearThreeBodiesMove(IReadOnlyList<Creature> targets)
	{
		_ = targets;
		await MonsterAnimationHelper.TriggerAnimWithFixedWait(Creature, CreatureAnimator.attackTrigger, SummonDelay);
		await SummonLotus(LotusLeftSlot);
		await SummonLotus(LotusRightSlot);
	}

	private async Task WorrylessPunishFivePreceptsMove(IReadOnlyList<Creature> targets)
	{
		_ = targets;
		await PunishFivePrecepts(WorrylessPunishTrigger);
	}

	private async Task PunishFivePrecepts(string triggerName)
	{
		await DamageCmd.Attack(PunishFivePreceptsDamage)
			.FromMonster(this)
			.WithHitCount(PunishFivePreceptsHits)
			.WithAttackerAnim(triggerName, 0f)
			.WithWaitBeforeHit(MultiAttackDelay, MultiAttackDelay)
			.WithHitFx("vfx/vfx_attack_slash", AttackSfxPath)
			.OnlyPlayAnimOnce()
			.Execute(null);
	}

	private async Task SummonLotus(string slotName)
	{
		if (!CanSummonLotus(slotName))
		{
			return;
		}

		Creature lotus = await CreatureCmd.Add<ReincarnationLotus>(CombatState, slotName);
		if (!lotus.HasPower<MinionPower>())
		{
			await PowerCmd.Apply<MinionPower>(lotus, 1m, Creature, null, silent: true);
		}
	}

	private async Task SummonLotusInFirstOpenSlot(params string[] slotNames)
	{
		foreach (string slotName in slotNames)
		{
			if (!CanSummonLotus(slotName))
			{
				continue;
			}

			await SummonLotus(slotName);
			return;
		}
	}

	private bool CanSummonLotus(string slotName)
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

	private async Task RemoveAllPowers()
	{
		PowerModel[] powers = Creature.Powers.ToArray();
		foreach (PowerModel power in powers)
		{
			await PowerCmd.Remove(power);
		}
	}

	private async Task RemoveDebuffs()
	{
		PowerModel[] debuffs = Creature.Powers
			.Where(static power => power.TypeForCurrentAmount == PowerType.Debuff)
			.ToArray();
		foreach (PowerModel debuff in debuffs)
		{
			await PowerCmd.Remove(debuff);
		}
	}

	private decimal GetScaledPhaseHp()
	{
		if (CombatState.RunState != null)
		{
			return Creature.ScaleHpForMultiplayer(
				PhaseHp,
				CombatState.Encounter,
				CombatState.RunState.Players.Count,
				Math.Clamp(CombatState.RunState.CurrentActIndex, 0, 2));
		}

		return PhaseHp;
	}

	public override CreatureAnimator GenerateAnimator(MegaSprite controller)
	{
		AnimState meditationIdle = new("A_Idle", isLooping: true);
		AnimState freeIdle = new("B_Idle", isLooping: true);
		AnimState worrylessIdle = new("C_Idle", isLooping: true);
		AnimState meditationSummon = new("A_Skill")
		{
			NextState = meditationIdle
		};
		AnimState enterFreePhase = new("A_Revive")
		{
			NextState = freeIdle
		};
		AnimState freeAttack = new("B_Attack")
		{
			NextState = freeIdle
		};
		AnimState freeRiseLoop = new("B_Skill_Loop", isLooping: true);
		AnimState freeRise = new("B_Skill_Begin")
		{
			NextState = freeRiseLoop
		};
		AnimState freeClearFire = new("B_Skill_End")
		{
			NextState = freeIdle
		};
		AnimState freePunish = new("B_Skill_2")
		{
			NextState = freeIdle
		};
		AnimState reviveLoop = new("B_Revive_Loop", isLooping: true);
		AnimState reviveBegin = new("B_Revive_Begin")
		{
			NextState = reviveLoop
		};
		AnimState reviveEnd = new("B_Revive_End")
		{
			NextState = worrylessIdle
		};
		AnimState worrylessAttack = new("C_Attack")
		{
			NextState = worrylessIdle
		};
		AnimState worrylessPunish = new("C_Skill_1")
		{
			NextState = worrylessIdle
		};
		AnimState worrylessDie = new("C_Die");

		CreatureAnimator animator = new(meditationIdle, controller);
		animator.AddAnyState(CreatureAnimator.idleTrigger, meditationIdle, () => IsMeditationPhase);
		animator.AddAnyState(CreatureAnimator.idleTrigger, freeIdle, () => IsFreePhase);
		animator.AddAnyState(CreatureAnimator.idleTrigger, worrylessIdle, () => IsWorrylessPhase);
		animator.AddAnyState(CreatureAnimator.attackTrigger, meditationSummon, () => IsMeditationPhase);
		animator.AddAnyState(CreatureAnimator.attackTrigger, freeAttack, () => IsFreePhase);
		animator.AddAnyState(CreatureAnimator.attackTrigger, worrylessAttack, () => IsWorrylessPhase);
		animator.AddAnyState(EnterFreePhaseTrigger, enterFreePhase);
		animator.AddAnyState(FreeRiseTrigger, freeRise);
		animator.AddAnyState(FreeClearFireTrigger, freeClearFire);
		animator.AddAnyState(FreePunishTrigger, freePunish);
		animator.AddAnyState(ReviveEndTrigger, reviveEnd);
		animator.AddAnyState(WorrylessPunishTrigger, worrylessPunish);
		animator.AddAnyState(CreatureAnimator.deathTrigger, reviveBegin, () => !HasRevived);
		animator.AddAnyState(CreatureAnimator.deathTrigger, worrylessDie, () => HasRevived);
		return animator;
	}
}
