using Godot;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Singleton;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.ValueProps;

namespace StS1Act4;

internal static class Act4Util
{
	public static int CurrentAscension => RunManager.Instance.DebugOnlyGetState()?.AscensionLevel ?? 0;

	public static bool HasAscension(int level)
	{
		return CurrentAscension >= level;
	}

	public static bool HasOrbSlot(Creature creature)
	{
		return creature.Player?.BaseOrbSlotCount > 0;
	}

	public static decimal GetMultiplayerHpScalingFactor(Creature creature)
	{
		if (creature.CombatState?.RunState is not RunState runState || runState.Players.Count <= 1)
		{
			return 1m;
		}

		return runState.Players.Count * MultiplayerScalingModel.GetMultiplayerScaling(creature.CombatState.Encounter, runState.CurrentActIndex);
	}
}

internal static class Act4SpineAnimUtil
{
	public static CreatureAnimator GenerateDefaultAnimator(MegaSprite controller)
	{
		string idleName = "idle_loop";
		string attackName = idleName;
		string hitName = idleName;
		string deathName = idleName;

		try
		{
			MegaSkeleton? skeleton = controller.GetSkeleton();
			MegaSkeletonDataResource? data = skeleton?.GetData();
			if (data != null)
			{
				idleName = FindFirst(data, "idle_loop", "Idle", "idle", "Idle_1", "Idle_2", "waving", "animation", "idle_closed", "idle_open", "eyefloat", "idle closed")
					?? idleName;
				attackName = FindFirst(data, "attack", "Attack", "Attack_1", "rally", "chomp", "halberd_slam", "finger_wiggle", "tailslam", "rear", "Sumon")
					?? idleName;
				hitName = FindFirst(data, "hurt", "Hit", "hit", "Hurt", "damage", "damaged")
					?? idleName;
				deathName = FindFirst(data, "die", "Die", "Death", "death", "explode")
					?? hitName;
			}
		}
		catch
		{
		}

		AnimState idle = new(idleName, isLooping: true);
		AnimState attack = new(attackName);
		AnimState cast = new(attackName);
		AnimState hit = new(hitName);
		AnimState die = new(deathName);
		attack.NextState = idle;
		cast.NextState = idle;
		hit.NextState = idle;

		CreatureAnimator animator = new(idle, controller);
		animator.AddAnyState(CreatureAnimator.idleTrigger, idle);
		animator.AddAnyState(CreatureAnimator.attackTrigger, attack);
		animator.AddAnyState(CreatureAnimator.castTrigger, cast);
		animator.AddAnyState(CreatureAnimator.hitTrigger, hit);
		animator.AddAnyState(CreatureAnimator.deathTrigger, die);
		return animator;
	}

	private static string? FindFirst(MegaSkeletonDataResource data, params string[] candidates)
	{
		foreach (string candidate in candidates)
		{
			if (data.FindAnimation(candidate) != null)
			{
				return candidate;
			}
		}

		return null;
	}
}

public sealed class Sts1Act4 : ActModel
{
	public override string ChestOpenSfx => "event:/sfx/ui/treasure/treasure_act3";

	public override IEnumerable<EncounterModel> BossDiscoveryOrder => new[] { ModelDb.Encounter<CorruptHeartEncounter>() };

	public override IEnumerable<AncientEventModel> AllAncients => Array.Empty<AncientEventModel>();

	public override IEnumerable<EventModel> AllEvents => Array.Empty<EventModel>();

	protected override int NumberOfWeakEncounters => 0;

	protected override int BaseNumberOfRooms => 1;

	public override string[] BgMusicOptions => new[] { "event:/music/act3_a1_v1" };

	public override string[] MusicBankPaths => new[] { "res://banks/desktop/act3_a1.bank" };

	public override string AmbientSfx => "event:/sfx/ambience/act3_ambience";

	public override string ChestSpineSkinNameNormal => "act3";

	public override string ChestSpineSkinNameStroke => "act3_stroke";

	public override Color MapTraveledColor => new("28231D");

	public override Color MapUntraveledColor => new("877256");

	public override Color MapBgColor => new("A78A67");

	public override IEnumerable<EncounterModel> GenerateAllEncounters()
	{
		return new EncounterModel[]
		{
			ModelDb.Encounter<SpireShieldAndSpear>(),
			ModelDb.Encounter<CorruptHeartEncounter>()
		};
	}

	public override IEnumerable<AncientEventModel> GetUnlockedAncients(UnlockState unlockState)
	{
		return Array.Empty<AncientEventModel>();
	}

	protected override void ApplyActDiscoveryOrderModifications(UnlockState unlockState)
	{
	}

	public override MapPointTypeCounts GetMapPointTypes(MegaCrit.Sts2.Core.Random.Rng mapRng)
	{
		// 0.103.2 changed MapPointTypeCounts to use fixed ctor inputs for shops/rests/unknowns,
		// while elite count and ignore rules remain init-only overrides.
		return new MapPointTypeCounts(0, 0)
		{
			NumOfElites = 1,
			PointTypesThatIgnoreRules = new HashSet<MapPointType> { MapPointType.Elite }
		};
	}
}

public sealed class FixedAct4Map : ActMap
{
	private readonly MapPoint?[,] _grid = new MapPoint[3, 2];

	public MapPoint ShopPoint { get; }

	public MapPoint ElitePoint { get; }

	public override MapPoint BossMapPoint { get; }

	public override MapPoint StartingMapPoint { get; }

	protected override MapPoint?[,] Grid => _grid;

	public FixedAct4Map()
	{
		ShopPoint = new MapPoint(1, 0)
		{
			PointType = MapPointType.Shop,
			CanBeModified = false
		};
		StartingMapPoint = ShopPoint;
		ElitePoint = new MapPoint(1, 1)
		{
			PointType = MapPointType.Elite,
			CanBeModified = false
		};
		BossMapPoint = new MapPoint(1, 2)
		{
			PointType = MapPointType.Boss,
			CanBeModified = false
		};

		StartingMapPoint.AddChildPoint(ElitePoint);
		ElitePoint.AddChildPoint(BossMapPoint);
		startMapPoints.Add(StartingMapPoint);
		_grid[1, 1] = ElitePoint;
	}
}

public sealed class SpireShieldAndSpear : EncounterModel
{
	public override RoomType RoomType => RoomType.Elite;

	public override bool HasScene => true;

	public override bool FullyCenterPlayers => true;

	public override IReadOnlyList<string> Slots => new[] { "shield", "spear" };

	public override IEnumerable<MonsterModel> AllPossibleMonsters => new MonsterModel[]
	{
		ModelDb.Monster<SpireShield>(),
		ModelDb.Monster<SpireSpear>()
	};

	public override float GetCameraScaling()
	{
		return 0.9f;
	}

	public override Vector2 GetCameraOffset()
	{
		return Vector2.Down * 40f;
	}

	protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
	{
		return new(MonsterModel, string?)[]
		{
			(ModelDb.Monster<SpireShield>().ToMutable(), "shield"),
			(ModelDb.Monster<SpireSpear>().ToMutable(), "spear")
		};
	}
}

public sealed class CorruptHeartEncounter : EncounterModel
{
	public override RoomType RoomType => RoomType.Boss;

	public override bool HasScene => true;

	public override IReadOnlyList<string> Slots => new[] { "heart" };

	public override string BossNodePath => "res://images/map/placeholder/corruptheartencounter_icon";

	public override MegaCrit.Sts2.Core.Bindings.MegaSpine.MegaSkeletonDataResource? BossNodeSpineResource => null;

	public override IEnumerable<MonsterModel> AllPossibleMonsters => new[] { ModelDb.Monster<CorruptHeart>() };

	public override float GetCameraScaling()
	{
		return 0.92f;
	}

	public override Vector2 GetCameraOffset()
	{
		return Vector2.Down * 10f;
	}

	protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
	{
		return new(MonsterModel, string?)[] { (ModelDb.Monster<CorruptHeart>().ToMutable(), "heart") };
	}
}

public sealed class SpireShield : MonsterModel
{
	private const string ShieldAttackSfx = "event:/sfx/enemy/enemy_attacks/flail_knight/flail_knight_ram";

	private const string BashMoveId = "BASH_MOVE";

	private const string FortifyMoveId = "FORTIFY_MOVE";

	private const string SmashMoveId = "SMASH_MOVE";

	private int _moveCount;

	private string? _lastMoveId;

	public override int MinInitialHp => Act4Util.HasAscension(8) ? 125 : 110;

	public override int MaxInitialHp => MinInitialHp;

	public override bool HasDeathSfx => false;

	public override bool HasHurtSfx => false;

	private int BashDamage => Act4Util.HasAscension(9) ? 14 : 12;

	private int SmashDamage => 38;

	public override CreatureAnimator GenerateAnimator(MegaSprite controller)
	{
		return Act4SpineAnimUtil.GenerateDefaultAnimator(controller);
	}

	public override async Task AfterAddedToRoom()
	{
		await base.AfterAddedToRoom();
		await PowerCmd.Apply<ArtifactPower>(Creature, Act4Util.HasAscension(18) ? 2m : 1m, Creature, null);
		await PowerCmd.Apply<BackAttackLeftPower>(Creature, 1m, Creature, null);
	}

	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		MoveState bash = new(BashMoveId, BashMove, new SingleAttackIntent(BashDamage), new DebuffIntent());
		MoveState fortify = new(FortifyMoveId, FortifyMove, new DefendIntent());
		MoveState smash = new(SmashMoveId, SmashMove, new SingleAttackIntent(SmashDamage), new DefendIntent());
		RandomBranchState opener = new("SPIRE_SHIELD_RANDOM");
		opener.AddBranch(bash, MoveRepeatType.CanRepeatForever);
		opener.AddBranch(fortify, MoveRepeatType.CanRepeatForever);

		ConditionalBranchState branch = new("SPIRE_SHIELD_BRANCH");
		branch.AddState(smash, () => _moveCount % 3 == 2);
		branch.AddState(fortify, () => _moveCount % 3 == 1 && _lastMoveId == BashMoveId);
		branch.AddState(bash, () => _moveCount % 3 == 1 && _lastMoveId != BashMoveId);
		branch.AddState(opener, () => _moveCount % 3 == 0);

		bash.FollowUpState = branch;
		fortify.FollowUpState = branch;
		smash.FollowUpState = branch;

		return new MonsterMoveStateMachine(new MonsterState[] { branch, opener, bash, fortify, smash }, branch);
	}

	private async Task BashMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(BashDamage)
			.FromMonster(this)
			.WithAttackerAnim("Attack", 0.2f)
			.WithAttackerFx(null, ShieldAttackSfx)
			.Execute(null);
		foreach (Creature target in targets.Where(static target => target.IsAlive))
		{
			if (Act4Util.HasOrbSlot(target) && CombatState.RunState.Rng.MonsterAi.NextBool())
			{
				await PowerCmd.Apply<FocusPower>(target, -1m, Creature, null);
			}
			else
			{
				await PowerCmd.Apply<StrengthPower>(target, -1m, Creature, null);
			}
		}

		_lastMoveId = BashMoveId;
		_moveCount++;
	}

	private async Task FortifyMove(IReadOnlyList<Creature> targets)
	{
		foreach (Creature creature in CombatState.Enemies.Where(static creature => creature.IsAlive))
		{
			await CreatureCmd.GainBlock(creature, 30m, ValueProp.Move, null);
		}

		_lastMoveId = FortifyMoveId;
		_moveCount++;
	}

	private async Task SmashMove(IReadOnlyList<Creature> targets)
	{
		AttackCommand attack = DamageCmd.Attack(SmashDamage)
			.FromMonster(this)
			.WithAttackerAnim("Attack", 0.2f)
			.WithAttackerFx(null, ShieldAttackSfx);
		await attack.Execute(null);
		int blockAmount = Act4Util.HasAscension(8)
			? 99
			: attack.Results.Sum(static result => result.UnblockedDamage);
		if (blockAmount > 0)
		{
			await CreatureCmd.GainBlock(Creature, blockAmount, ValueProp.Move, null);
		}

		_lastMoveId = SmashMoveId;
		_moveCount++;
	}
}

public sealed class SpireSpear : MonsterModel
{
	private const string SpearAttackSfx = "event:/sfx/enemy/enemy_attacks/lagavulin_matriarch/lagavulin_matriarch_attack_stab";

	private const string BurnStrikeMoveId = "BURN_STRIKE_MOVE";

	private const string PiercerMoveId = "PIERCER_MOVE";

	private const string SkewerMoveId = "SKEWER_MOVE";

	private int _moveCount;

	private string? _lastMoveId;

	public override int MinInitialHp => Act4Util.HasAscension(8) ? 180 : 160;

	public override int MaxInitialHp => MinInitialHp;

	public override bool HasDeathSfx => false;

	public override bool HasHurtSfx => false;

	private int BurnStrikeDamage => Act4Util.HasAscension(9) ? 6 : 5;

	private int SkewerDamage => 10;

	private int SkewerHits => Act4Util.HasAscension(9) ? 4 : 3;

	public override CreatureAnimator GenerateAnimator(MegaSprite controller)
	{
		return Act4SpineAnimUtil.GenerateDefaultAnimator(controller);
	}

	public override async Task AfterAddedToRoom()
	{
		await base.AfterAddedToRoom();
		await PowerCmd.Apply<ArtifactPower>(Creature, Act4Util.HasAscension(18) ? 2m : 1m, Creature, null);
		await PowerCmd.Apply<BackAttackRightPower>(Creature, 1m, Creature, null);
		await PowerCmd.Apply<SurroundedPower>(CombatState.GetOpponentsOf(Creature), 1m, Creature, null);
	}

	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		MoveState burnStrike = new(BurnStrikeMoveId, BurnStrikeMove, new MultiAttackIntent(BurnStrikeDamage, 2), new DebuffIntent());
		MoveState piercer = new(PiercerMoveId, PiercerMove, new BuffIntent());
		MoveState skewer = new(SkewerMoveId, SkewerMove, new MultiAttackIntent(SkewerDamage, SkewerHits));
		RandomBranchState randomAttack = new("SPIRE_SPEAR_RANDOM");
		randomAttack.AddBranch(burnStrike, MoveRepeatType.CanRepeatForever);
		randomAttack.AddBranch(piercer, MoveRepeatType.CanRepeatForever);

		ConditionalBranchState branch = new("SPIRE_SPEAR_BRANCH");
		branch.AddState(skewer, () => _moveCount % 3 == 1);
		branch.AddState(piercer, () => _moveCount % 3 == 0 && _lastMoveId == BurnStrikeMoveId);
		branch.AddState(burnStrike, () => _moveCount % 3 == 0 && _lastMoveId != BurnStrikeMoveId);
		branch.AddState(randomAttack, () => _moveCount % 3 == 2);

		burnStrike.FollowUpState = branch;
		piercer.FollowUpState = branch;
		skewer.FollowUpState = branch;

		return new MonsterMoveStateMachine(new MonsterState[] { branch, randomAttack, burnStrike, piercer, skewer }, branch);
	}

	private async Task BurnStrikeMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(BurnStrikeDamage)
			.FromMonster(this)
			.WithHitCount(2)
			.OnlyPlayAnimOnce()
			.WithAttackerAnim("Attack", 0.15f)
			.WithAttackerFx(null, SpearAttackSfx)
			.Execute(null);
		foreach (Creature target in targets.Where(static target => target.IsAlive))
		{
			if (Act4Util.HasAscension(9))
			{
				await CardPileCmd.AddToCombatAndPreview<Burn>(target, PileType.Draw, 2, addedByPlayer: false, CardPilePosition.Top);
			}
			else
			{
				await CardPileCmd.AddToCombatAndPreview<Burn>(target, PileType.Discard, 2, addedByPlayer: false);
			}
		}

		_lastMoveId = BurnStrikeMoveId;
		_moveCount++;
	}

	private async Task PiercerMove(IReadOnlyList<Creature> targets)
	{
		foreach (Creature creature in CombatState.Enemies.Where(static creature => creature.IsAlive))
		{
			await PowerCmd.Apply<StrengthPower>(creature, 2m, Creature, null);
		}

		_lastMoveId = PiercerMoveId;
		_moveCount++;
	}

	private async Task SkewerMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(SkewerDamage)
			.FromMonster(this)
			.WithHitCount(SkewerHits)
			.OnlyPlayAnimOnce()
			.WithAttackerAnim("Attack", 0.1f)
			.WithAttackerFx(null, SpearAttackSfx)
			.Execute(null);
		_lastMoveId = SkewerMoveId;
		_moveCount++;
	}
}

public sealed class CorruptHeart : MonsterModel
{
	private const string DebilitateMoveId = "DEBILITATE_MOVE";

	private const string BloodShotsMoveId = "BLOOD_SHOTS_MOVE";

	private const string EchoMoveId = "ECHO_MOVE";

	private const string BuffMoveId = "BUFF_MOVE";

	private int _moveCount;

	private int _buffCount;

	private bool _isFirstMove = true;

	private string? _lastMoveId;

	public override int MinInitialHp => Act4Util.HasAscension(9) ? 800 : 750;

	public override int MaxInitialHp => MinInitialHp;

	public override bool HasDeathSfx => false;

	public override bool HasHurtSfx => false;

	private int BloodShotsDamage => 2;

	private int BloodShotsHits => Act4Util.HasAscension(9) ? 15 : 12;

	private int InvincibleCap => Act4Util.HasAscension(8) ? 200 : 300;

	private int BeatOfDeath => Act4Util.HasAscension(9) ? 2 : 1;

	private const string PlayerImpactSfx = "event:/sfx/enemy/enemy_impact_enemy_size/enemy_impact_armor";

	private const string PlayerImpactBigSfx = "event:/sfx/enemy/enemy_impact_enemy_size/enemy_impact_armor_big";

	public override CreatureAnimator GenerateAnimator(MegaSprite controller)
	{
		return Act4SpineAnimUtil.GenerateDefaultAnimator(controller);
	}

	public override async Task AfterAddedToRoom()
	{
		await base.AfterAddedToRoom();
		await PowerCmd.Apply<Act4InvinciblePower>(Creature, GetScaledInvincibleCap(), Creature, null);
		await PowerCmd.Apply<Act4BeatOfDeathPower>(Creature, BeatOfDeath, Creature, null);
		TaskHelper.RunSafely(PlayAmbientHeartbeat());
	}

	private decimal GetScaledInvincibleCap()
	{
		return Math.Floor(InvincibleCap * Act4Util.GetMultiplayerHpScalingFactor(Creature));
	}

	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		MoveState debilitate = new(DebilitateMoveId, DebilitateMove, new DebuffIntent(strong: true));
		MoveState bloodShots = new(BloodShotsMoveId, BloodShotsMove, new MultiAttackIntent(BloodShotsDamage, BloodShotsHits));
		MoveState echo = new(EchoMoveId, EchoMove, new SingleAttackIntent(EchoDamage));
		MoveState buff = new(BuffMoveId, BuffMove, new BuffIntent());
		RandomBranchState randomAttack = new("CORRUPT_HEART_RANDOM");
		randomAttack.AddBranch(bloodShots, MoveRepeatType.CanRepeatForever);
		randomAttack.AddBranch(echo, MoveRepeatType.CanRepeatForever);

		ConditionalBranchState branch = new("CORRUPT_HEART_BRANCH");
		branch.AddState(debilitate, () => _isFirstMove);
		branch.AddState(buff, () => !_isFirstMove && _moveCount % 3 == 2);
		branch.AddState(bloodShots, () => !_isFirstMove && _moveCount % 3 == 1 && _lastMoveId == EchoMoveId);
		branch.AddState(echo, () => !_isFirstMove && _moveCount % 3 == 1 && _lastMoveId != EchoMoveId);
		branch.AddState(randomAttack, () => !_isFirstMove && _moveCount % 3 == 0);

		debilitate.FollowUpState = branch;
		bloodShots.FollowUpState = branch;
		echo.FollowUpState = branch;
		buff.FollowUpState = branch;

		return new MonsterMoveStateMachine(new MonsterState[] { branch, randomAttack, debilitate, bloodShots, echo, buff }, branch);
	}

	private int EchoDamage => Act4Util.HasAscension(9) ? 45 : 40;

	private async Task PlayAmbientHeartbeat()
	{
		// Keep the heartbeat pulse close to the observed idle_loop lurch cadence.
		await Cmd.Wait(0.68f);
		while (RunManager.Instance.IsInProgress && CombatManager.Instance.IsInProgress && Creature.IsAlive && Creature.CombatState != null)
		{
			NGame.Instance?.ScreenShake(ShakeStrength.Weak, ShakeDuration.Short);
			await Cmd.Wait(2.0f);
		}
	}

	private async Task DebilitateMove(IReadOnlyList<Creature> targets)
	{
		foreach (Creature target in targets.Where(static target => target.IsAlive))
		{
			await PowerCmd.Apply<VulnerablePower>(target, 2m, Creature, null);
			await PowerCmd.Apply<WeakPower>(target, 2m, Creature, null);
			await PowerCmd.Apply<FrailPower>(target, 2m, Creature, null);
			await AddDebilitateStatuses(target);
		}

		_isFirstMove = false;
		_lastMoveId = DebilitateMoveId;
		_moveCount++;
	}

	private static async Task AddDebilitateStatuses(Creature target)
	{
		Player player = target.Player ?? target.PetOwner
			?? throw new InvalidOperationException("Debilitate target did not resolve to a player owner.");
		CombatState combatState = target.CombatState
			?? throw new InvalidOperationException("Debilitate target had no combat state.");

		List<CardModel> cards = new()
		{
			combatState.CreateCard<Dazed>(player),
			combatState.CreateCard<Slimed>(player),
			combatState.CreateCard<Wound>(player),
			combatState.CreateCard<Burn>(player),
			combatState.CreateCard<MegaCrit.Sts2.Core.Models.Cards.Void>(player)
		};

		IReadOnlyList<CardPileAddResult> results = await CardPileCmd.AddGeneratedCardsToCombat(cards, PileType.Draw, addedByPlayer: false, CardPilePosition.Random);
		CardCmd.PreviewCardPileAdd(results, 0.35f);
		await Cmd.Wait(0.25f);
	}

	private async Task BloodShotsMove(IReadOnlyList<Creature> targets)
	{
		Act4HeartVfx.PlayBloodShots(Creature, targets, BloodShotsHits);
		await Cmd.Wait(0.06f);
		await DamageCmd.Attack(BloodShotsDamage)
			.FromMonster(this)
			.WithHitCount(BloodShotsHits)
			.OnlyPlayAnimOnce()
			.WithAttackerAnim("Attack", 0.15f)
			.WithWaitBeforeHit(0.035f, 0.035f)
			.BeforeDamage(() =>
			{
				SfxCmd.Play(PlayerImpactSfx, "EnemyImpact_Intensity", 2.6f, 1.05f);
				return Task.CompletedTask;
			})
			.Execute(null);
		_lastMoveId = BloodShotsMoveId;
		_moveCount++;
	}

	private async Task EchoMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(EchoDamage)
			.FromMonster(this)
			.WithAttackerAnim("Attack", 0.2f)
			.BeforeDamage(() =>
			{
				SfxCmd.Play(PlayerImpactBigSfx, "EnemyImpact_Intensity", 3.4f, 1.15f);
				return Task.CompletedTask;
			})
			.Execute(null);
		_lastMoveId = EchoMoveId;
		_moveCount++;
	}

	private async Task BuffMove(IReadOnlyList<Creature> targets)
	{
		Act4HeartVfx.PlayBuff(Creature);
		await PowerCmd.Apply<StrengthPower>(Creature, 2m, Creature, null);
		switch (_buffCount)
		{
			case 0:
				await PowerCmd.Apply<ArtifactPower>(Creature, 2m, Creature, null);
				break;
			case 1:
				await PowerCmd.Apply<Act4BeatOfDeathPower>(Creature, 1m, Creature, null);
				break;
			case 2:
				await PowerCmd.Apply<Act4PainfulStabsPower>(Creature, 1m, Creature, null);
				break;
			case 3:
				await PowerCmd.Apply<StrengthPower>(Creature, 10m, Creature, null);
				break;
			default:
				await PowerCmd.Apply<StrengthPower>(Creature, 50m, Creature, null);
				break;
		}

		_buffCount++;
		_lastMoveId = BuffMoveId;
		_moveCount++;
	}
}

public sealed class Act4BeatOfDeathPower : PowerModel
{
	private const string DamageVarKey = "Damage";

	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override int DisplayAmount
	{
		get
		{
			SyncDynamicVars();
			return base.DisplayAmount;
		}
	}

	protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DynamicVar(DamageVarKey, 0m) };

	public override Task AfterApplied(Creature? applier, CardModel? cardSource)
	{
		SyncDynamicVars();
		return Task.CompletedTask;
	}

	public override Task AfterPowerAmountChanged(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		if (power == this)
		{
			SyncDynamicVars();
		}

		return Task.CompletedTask;
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		Player owner = cardPlay.Card.Owner;
		if (owner?.Creature == null || owner.Creature.IsDead || !owner.Creature.IsPlayer)
		{
			return;
		}

		Flash();
		await CreatureCmd.Damage(context, owner.Creature, Amount, ValueProp.Unpowered | ValueProp.SkipHurtAnim, Owner, null);
	}

	private void SyncDynamicVars()
	{
		DynamicVars[DamageVarKey].BaseValue = Amount;
	}
}

public sealed class Act4PainfulStabsPower : PowerModel
{
	private const string WoundsVarKey = "Wounds";

	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override int DisplayAmount
	{
		get
		{
			SyncDynamicVars();
			return base.DisplayAmount;
		}
	}

	protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DynamicVar(WoundsVarKey, 0m) };

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromCard<Wound>() };

	public override Task AfterApplied(Creature? applier, CardModel? cardSource)
	{
		SyncDynamicVars();
		return Task.CompletedTask;
	}

	public override Task AfterPowerAmountChanged(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		if (power == this)
		{
			SyncDynamicVars();
		}

		return Task.CompletedTask;
	}

	public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
	{
		if (dealer != Owner || target.Player == null || result.UnblockedDamage <= 0 || !props.HasFlag(ValueProp.Move))
		{
			return;
		}

		Flash();
		await CardPileCmd.AddToCombatAndPreview<Wound>(target, PileType.Discard, Amount, addedByPlayer: false);
	}

	private void SyncDynamicVars()
	{
		DynamicVars[WoundsVarKey].BaseValue = Amount;
	}
}

public sealed class Act4InvinciblePower : PowerModel
{
	private const string CurrentCapVarKey = "CurrentCap";

	private sealed class Data
	{
		public int remainingThisTurn;
	}

	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override int DisplayAmount
	{
		get
		{
			int value = GetInternalData<Data>() is { } d ? d.remainingThisTurn : Amount;
			DynamicVars[CurrentCapVarKey].BaseValue = value;
			return value;
		}
	}

	protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DynamicVar(CurrentCapVarKey, 0m) };

	protected override object? InitInternalData()
	{
		return new Data();
	}

	public override Task AfterApplied(Creature? applier, CardModel? cardSource)
	{
		GetInternalData<Data>().remainingThisTurn = Amount;
		SyncDisplayedCap();
		return Task.CompletedTask;
	}

	public override Task AfterPowerAmountChanged(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		if (power == this)
		{
			GetInternalData<Data>().remainingThisTurn = Math.Min(GetInternalData<Data>().remainingThisTurn, Amount);
			SyncDisplayedCap();
		}

		return Task.CompletedTask;
	}

	public override decimal ModifyHpLostAfterOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (target != Owner || amount <= 0m)
		{
			return amount;
		}

		int remaining = GetInternalData<Data>().remainingThisTurn;
		if (remaining <= 0)
		{
			return 0m;
		}

		return Math.Min(amount, remaining);
	}

	public override decimal ModifyDamageCap(Creature? target, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (target != Owner)
		{
			return decimal.MaxValue;
		}

		return Math.Max(0, GetInternalData<Data>().remainingThisTurn);
	}

	public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (target != Owner || result.UnblockedDamage <= 0)
		{
			return Task.CompletedTask;
		}

		Data data = GetInternalData<Data>();
		int updated = Math.Max(0, data.remainingThisTurn - result.UnblockedDamage);
		if (updated != data.remainingThisTurn)
		{
			data.remainingThisTurn = updated;
			SyncDisplayedCap();
			Flash();
		}

		return Task.CompletedTask;
	}

	public override Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
	{
		if (side == CombatSide.Enemy)
		{
			Data data = GetInternalData<Data>();
			data.remainingThisTurn = Amount;
			SyncDisplayedCap();
		}

		return Task.CompletedTask;
	}

	private void SyncDisplayedCap()
	{
		DynamicVars[CurrentCapVarKey].BaseValue = DisplayAmount;
		InvokeDisplayAmountChanged();
	}
}
