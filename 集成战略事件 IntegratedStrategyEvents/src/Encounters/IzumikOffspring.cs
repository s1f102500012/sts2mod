using System.Collections.Concurrent;
using System.Reflection;
using Godot;
using IntegratedStrategyEvents.Powers;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;

namespace IntegratedStrategyEvents.Encounters;

public sealed class IzumikOffspring : MonsterModel
{
	public const string SummonMoveId = "SUMMON_MOVE";

	private const int InitialHp = 50;
	private const int MaxTransformTargetHp = 100;
	private const decimal IzumikStatGain = 1m;
	private const float SummonMoveDelay = 0.15f;
	private const float SummonDelay = 0.75f;
	private const string IllusionMoveId = "ILLUSION_MOVE";
	private const string SummonTrigger = "SummonTrigger";
	private const string GremlinMercId = "GREMLIN_MERC";

	private bool _hasTransformed;

	public override int MinInitialHp => InitialHp;

	public override int MaxInitialHp => InitialHp;

	public override bool HasDeathSfx => false;

	public override bool HasHurtSfx => false;

	public override bool ShouldFadeAfterDeath => true;

	public override float DeathAnimLengthOverride => 1.1f;

	public override DamageSfxType TakeDamageSfxType => DamageSfxType.Fur;

	protected override string VisualsPath => "res://IntegratedStrategyEvents/scenes/creature_visuals/izumik_offspring.tscn";

	public override Vector2 ExtraDeathVfxPadding => Vector2.One * 0.5f;

	private bool HasTransformed
	{
		get => _hasTransformed;
		set
		{
			AssertMutable();
			_hasTransformed = value;
		}
	}

	public override async Task AfterAddedToRoom()
	{
		await base.AfterAddedToRoom();
		await PowerCmd.Apply<IzumikTransformationPower>(Creature, 1m, Creature, null, silent: true);
	}

	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		MoveState summon = new(
			SummonMoveId,
			SummonMove,
			new SummonIntent());
		summon.FollowUpState = summon;
		return new MonsterMoveStateMachine([summon], summon);
	}

	private async Task SummonMove(IReadOnlyList<Creature> targets)
	{
		_ = targets;
		await Cmd.Wait(SummonMoveDelay);
	}

	public async Task TransformIntoRandomEnemy()
	{
		if (HasTransformed || CombatState == null || Creature.IsDead)
		{
			return;
		}

		ICombatState combatState = CombatState;
		MonsterModel? replacement = ChooseRandomSmallMonster();
		if (replacement == null)
		{
			return;
		}

		HasTransformed = true;
		FlashIzumikTransformationPower();
		await CreatureCmd.TriggerAnim(Creature, SummonTrigger, SummonDelay);
		await BuffIzumikEcologicalFountain();

		string? slotName = string.IsNullOrWhiteSpace(Creature.SlotName) ? null : Creature.SlotName;
		bool wasMinion = Creature.HasPower<MinionPower>();
		Vector2? originalGlobalPosition = NCombatRoom.Instance?.GetCreatureNode(Creature)?.GlobalPosition;
		await CreatureCmd.Escape(Creature);
		if (combatState == null)
		{
			return;
		}

		Creature transformed = await CreatureCmd.Add(replacement, combatState, CombatSide.Enemy, slotName);
		transformed.SlotName = slotName;
		if (slotName != null)
		{
			combatState.SortEnemiesBySlotName();
		}
		else if (originalGlobalPosition.HasValue)
		{
			PinTransformedCreatureToOriginalPosition(transformed, originalGlobalPosition.Value);
		}

		if (wasMinion && !transformed.HasPower<MinionPower>())
		{
			await PowerCmd.Apply<MinionPower>(transformed, 1m, transformed, null, silent: true);
		}
	}

	private static void PinTransformedCreatureToOriginalPosition(Creature transformed, Vector2 originalGlobalPosition)
	{
		NCreature? transformedNode = NCombatRoom.Instance?.GetCreatureNode(transformed);
		if (transformedNode != null)
		{
			transformedNode.GlobalPosition = originalGlobalPosition;
		}
	}

	// 只允许原版程序集的普通遭遇怪进入变身池：其他模组注册的怪可能依赖各自的
	// EncounterHook/场景初始化，被裸召唤后没有合法 move，会中断回合推进链；
	// 事件专属遭遇（*EventEncounter）的怪不属于正常战斗池。
	// （0.108 起原版移除了 EncounterModel.IsDebugEncounter，调试遭遇的排除仅靠程序集判定。）
	private static readonly Assembly VanillaAssembly = typeof(MonsterModel).Assembly;

	private MonsterModel? ChooseRandomSmallMonster()
	{
		List<MonsterModel> candidates = ModelDb.AllEncounters
			.Where(static encounter => encounter.RoomType == RoomType.Monster &&
				encounter.GetType().Assembly == VanillaAssembly &&
				!encounter.GetType().Name.EndsWith("EventEncounter", StringComparison.Ordinal))
			.SelectMany(static encounter => encounter.AllPossibleMonsters)
			.Where(static monster => monster.GetType().Assembly == VanillaAssembly &&
				!IsInvalidTransformTarget(monster))
			.GroupBy(static monster => monster.Id.Entry, StringComparer.Ordinal)
			.Select(static group => group.First())
			.OrderBy(static monster => monster.Id.Entry, StringComparer.Ordinal)
			.ToList();
		return candidates.Count == 0
			? null
			: (MonsterModel)candidates[RunRng.MonsterAi.NextInt(candidates.Count)].ToMutable();
	}

	private static bool IsInvalidTransformTarget(MonsterModel monster)
	{
		return monster.CanonicalInstance is IzumikOffspring ||
			IsKnownUnsafeTransformTarget(monster) ||
			HasIllusionPowerOnSpawn(monster) ||
			monster.MaxInitialHp > MaxTransformTargetHp ||
			HasUnsafeMoveStateMachine(monster);
	}

	private static bool IsKnownUnsafeTransformTarget(MonsterModel monster)
	{
		return monster.CanonicalInstance is GremlinMerc ||
			monster.Id.Entry.Equals(GremlinMercId, StringComparison.Ordinal);
	}

	private static bool HasIllusionPowerOnSpawn(MonsterModel monster)
	{
		return monster.CanonicalInstance is EyeWithTeeth or Parafright;
	}

	private static readonly ConcurrentDictionary<Type, bool> UnsafeMoveStateMachineByType = new();

	private static bool HasUnsafeMoveStateMachine(MonsterModel monster)
	{
		return IsUnsafeMoveStateMachine(monster.GetType(), () => ProbeMoveStateMachine(monster));
	}

	// 探测结果只取决于怪物类型，每个类型探一次；变身 roll 会对全部原版怪重复求值，
	// 不缓存就是每次重建几十台状态机。
	internal static bool IsUnsafeMoveStateMachine(Type monsterType, Func<bool> probe)
	{
		// 非原版类型一律判为不安全：绝不为了判定去调用第三方模组自己的方法。
		return monsterType.Assembly != VanillaAssembly ||
			UnsafeMoveStateMachineByType.GetOrAdd(monsterType, _ => probe());
	}

	private static bool ProbeMoveStateMachine(MonsterModel monster)
	{
		try
		{
			// MonsterModel.MoveStateMachine 只由 SetUpForCombat 赋值一次，模型库里的
			// canonical 实例上恒为 null，因此仍需调用生成方法；已构造好的直接读。
			MethodInfo? generateMoveStateMachine =
				IntegratedStrategyPrivateMembers.Method(typeof(MonsterModel), "GenerateMoveStateMachine");
			if ((monster.MoveStateMachine ??
				generateMoveStateMachine?.Invoke(monster, null)) is not MonsterMoveStateMachine stateMachine)
			{
				return true;
			}

			if (stateMachine.States.Values.Any(static state => state is ConditionalBranchState))
			{
				return true;
			}

			if (stateMachine.States.ContainsKey(IllusionMoveId))
			{
				return true;
			}

			return stateMachine.States.Values
				.OfType<MoveState>()
				.SelectMany(static state => state.Intents)
				.Any(static intent => intent is SummonIntent);
		}
		catch
		{
			return true;
		}
	}

	private void FlashIzumikTransformationPower()
	{
		Creature.GetPowerInstances<IzumikTransformationPower>().FirstOrDefault()?.Pulse();
	}

	private async Task BuffIzumikEcologicalFountain()
	{
		Creature? izumik = CombatState?.Enemies.FirstOrDefault(IsIzumikEcologicalFountain);
		if (izumik == null || !izumik.IsAlive)
		{
			return;
		}

		await PowerCmd.Apply<StrengthPower>(izumik, IzumikStatGain, Creature, null);
		await PowerCmd.Apply<DexterityPower>(izumik, IzumikStatGain, Creature, null);
	}

	private static bool IsIzumikEcologicalFountain(Creature creature)
	{
		MonsterModel? monster = creature.Monster;
		return creature.IsAlive &&
			monster != null &&
			(monster is IIzumikEcologicalFountain ||
				monster.Id.Entry.Equals("IZUMIK_ECOLOGICAL_FOUNTAIN", StringComparison.Ordinal));
	}

	public override CreatureAnimator GenerateAnimator(MegaSprite controller)
	{
		AnimState idle = new("Idle", isLooping: true);
		AnimState start = new("Start")
		{
			NextState = idle
		};
		AnimState die = new("Die");

		CreatureAnimator animator = new(start, controller);
		animator.AddAnyState(CreatureAnimator.idleTrigger, idle);
		animator.AddAnyState(SummonTrigger, die);
		animator.AddAnyState(CreatureAnimator.attackTrigger, die);
		animator.AddAnyState(CreatureAnimator.deathTrigger, die);
		return animator;
	}
}
