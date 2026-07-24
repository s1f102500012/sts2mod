using System.Globalization;
using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static partial class HextechCombatHooks
{
	private const string KnowledgeDemonCurseMoveId = "CURSE_OF_KNOWLEDGE_MOVE";
	private const int FinalKnowledgeDemonCurseIndex = 2;
	private const string TestSubjectRespawnMoveId = "RESPAWN_MOVE";
	private const string IllusionReviveMoveId = "REVIVE_MOVE";
	private const string TheInsatiableOpeningMoveId = "LIQUIFY_GROUND_MOVE";

	private static readonly FieldInfo MoveStateIntentsField =
		RequireField(typeof(MoveState), "<Intents>k__BackingField");
	private static readonly FieldInfo MonsterIsPerformingMoveField =
		RequireField(typeof(MonsterModel), "_isPerformingMove");
	private static readonly FieldInfo KnowledgeDemonCurseCounterField =
		RequireField(typeof(KnowledgeDemon), "_curseOfKnowledgeCounter");

	private static int _jeweledGauntletFailureLogs;

	private static void InstallJeweledGauntletHooks(Harmony harmony)
	{
		try
		{
			harmony.Patch(
				RequireMethod(typeof(MonsterModel), nameof(MonsterModel.PerformMove), BindingFlags.Instance | BindingFlags.Public),
				prefix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(JeweledGauntletPerformMovePrefix)),
				postfix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(JeweledGauntletPerformMovePostfix)));

			harmony.Patch(
				RequireMethod(
					typeof(NCreature),
					nameof(NCreature.UpdateIntent),
					BindingFlags.Instance | BindingFlags.Public,
					typeof(IEnumerable<Creature>)),
				prefix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(JeweledGauntletUpdateIntentPrefix)),
				postfix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(JeweledGauntletUpdateIntentPostfix)),
				finalizer: new HarmonyMethod(typeof(HextechCombatHooks), nameof(JeweledGauntletUpdateIntentFinalizer)));
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] 珠光护手 hook 安装失败,行动重复或双重意图可能不会生效: {ex.GetType().Name}: {ex.Message}");
		}
	}

	private static void JeweledGauntletPerformMovePrefix(
		MonsterModel __instance,
		out JeweledGauntletMoveRepeatState? __state)
	{
		__state = null;
		try
		{
			if (ShouldRepeatJeweledGauntletMove(__instance, out MoveState? move) && move != null)
			{
				__state = new JeweledGauntletMoveRepeatState(__instance, move);
			}
		}
		catch (Exception ex)
		{
			LogJeweledGauntletFailure(nameof(JeweledGauntletPerformMovePrefix), ex);
		}
	}

	private static void JeweledGauntletPerformMovePostfix(
		ref Task __result,
		JeweledGauntletMoveRepeatState? __state)
	{
		if (__state != null)
		{
			__result = RepeatJeweledGauntletMoveAfterOriginal(__result, __state);
		}
	}

	private static async Task RepeatJeweledGauntletMoveAfterOriginal(
		Task originalTask,
		JeweledGauntletMoveRepeatState repeatState)
	{
		await originalTask;

		MonsterModel monster = repeatState.Monster;
		Creature creature = monster.Creature;
		HextechCombatState? combatState = creature.CombatState;
		if (!CanPerformJeweledGauntletRepeat(monster, repeatState.Move, combatState))
		{
			return;
		}

		await Cmd.CustomScaledWait(0.1f, 0.2f);
		MonsterIsPerformingMoveField.SetValue(monster, true);
		IReadOnlyList<Creature> targets = combatState!.PlayerCreatures.ToArray();
		try
		{
			Log.Info($"Monster {monster.Id.Entry} repeating move {repeatState.Move.Id} via enemy Jeweled Gauntlet");
			await repeatState.Move.PerformMove(targets);
			CombatManager.Instance.History.MonsterPerformedMove(combatState, monster, repeatState.Move, targets);
		}
		finally
		{
			MonsterIsPerformingMoveField.SetValue(monster, false);
		}

		if (creature.IsDead && Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(combatState, creature))
		{
			combatState.RemoveCreature(creature);
		}

		await Cmd.CustomScaledWait(0.1f, 0.4f);
	}

	private static bool CanPerformJeweledGauntletRepeat(
		MonsterModel monster,
		MoveState capturedMove,
		HextechCombatState? combatState)
	{
		if (!CombatManager.Instance.IsInProgress
			|| combatState == null
			|| monster.Creature.IsDead
			|| !combatState.Creatures.Contains(monster.Creature))
		{
			return false;
		}

		// 部分 Boss 阶段转换、逃跑或特殊动作会在第一次执行期间强制改写 NextMove。
		// 即使旧动作的意图看似普通，也不能在状态机已前进后再执行其捕获委托。
		return ReferenceEquals(monster.NextMove, capturedMove);
	}

	private static void JeweledGauntletUpdateIntentPrefix(
		NCreature __instance,
		out JeweledGauntletIntentPatchState? __state)
	{
		__state = null;
		try
		{
			MonsterModel? monster = __instance.Entity?.Monster;
			if (monster == null
				|| !ShouldRepeatJeweledGauntletMove(monster, out MoveState? move)
				|| move == null)
			{
				return;
			}

			IReadOnlyList<AbstractIntent> originalIntents = move.Intents;
			IReadOnlyList<AbstractIntent> displayedIntents = DuplicateJeweledGauntletIntentGroup(originalIntents);
			MoveStateIntentsField.SetValue(move, displayedIntents);
			__state = new JeweledGauntletIntentPatchState(move, originalIntents, displayedIntents);
		}
		catch (Exception ex)
		{
			LogJeweledGauntletFailure(nameof(JeweledGauntletUpdateIntentPrefix), ex);
		}
	}

	private static void JeweledGauntletUpdateIntentPostfix(JeweledGauntletIntentPatchState? __state)
	{
		RestoreJeweledGauntletIntents(__state);
	}

	private static Exception? JeweledGauntletUpdateIntentFinalizer(
		Exception? __exception,
		JeweledGauntletIntentPatchState? __state)
	{
		RestoreJeweledGauntletIntents(__state);
		return __exception;
	}

	private static void RestoreJeweledGauntletIntents(JeweledGauntletIntentPatchState? state)
	{
		if (state == null)
		{
			return;
		}

		try
		{
			// 只撤销自己临时安装的列表；若别的逻辑在 UpdateIntent 期间确实改了行动，则不覆盖它。
			if (ReferenceEquals(MoveStateIntentsField.GetValue(state.Move), state.DisplayedIntents))
			{
				MoveStateIntentsField.SetValue(state.Move, state.OriginalIntents);
			}
		}
		catch (Exception ex)
		{
			LogJeweledGauntletFailure(nameof(RestoreJeweledGauntletIntents), ex);
		}
	}

	private static bool ShouldRepeatJeweledGauntletMove(
		MonsterModel monster,
		out MoveState? move)
	{
		move = null;
		Creature creature = monster.Creature;
		if (creature.Side != CombatSide.Enemy
			|| creature.CombatId is not uint combatId
			|| creature.CombatState is not { } combatState
			|| combatState.RunState is not RunState runState
			|| GetMayhemModifier(runState) is not { } modifier
			|| !modifier.HasActiveMonsterHex(MonsterHexKind.JeweledGauntlet))
		{
			return false;
		}

		move = monster.NextMove;
		if (ShouldSuppressJeweledGauntletRepeat(monster, move))
		{
			return false;
		}

		if (!AreJeweledGauntletIntentsRepeatable(move.Intents))
		{
			return false;
		}

		int chance = GetJeweledGauntletRepeatPercent(modifier.GetMonsterHexStrengthTier(MonsterHexKind.JeweledGauntlet));
		return HextechStableRandom.PercentChance(
			runState,
			chance,
			"enemy-jeweled-gauntlet-repeat",
			combatId.ToString(CultureInfo.InvariantCulture),
			combatState.RoundNumber.ToString(CultureInfo.InvariantCulture),
			move.Id);
	}

	private static bool ShouldSuppressJeweledGauntletRepeat(
		MonsterModel monster,
		MoveState move)
	{
		if (IsMonsterRevivalMove(move.Id))
		{
			return true;
		}

		if (monster is TheInsatiable && IsTheInsatiableOpeningMove(move.Id))
		{
			return true;
		}

		if (monster is not KnowledgeDemon)
		{
			return false;
		}

		int curseCounter = (int)(KnowledgeDemonCurseCounterField.GetValue(monster) ?? 0);
		return WouldRepeatFinalKnowledgeDemonCurse(move.Id, curseCounter);
	}

	internal static bool WouldRepeatFinalKnowledgeDemonCurse(string moveId, int curseCounter)
	{
		// 原行动会先消费当前阶段，珠光护手的重复行动随后消费下一阶段。
		// 因此计数为 1 时就必须阻止重复，否则第二阶段的暴击会直接执行第三阶段。
		return curseCounter + 1 >= FinalKnowledgeDemonCurseIndex
			&& string.Equals(moveId, KnowledgeDemonCurseMoveId, StringComparison.Ordinal);
	}

	internal static bool IsTheInsatiableOpeningMove(string moveId)
	{
		return string.Equals(moveId, TheInsatiableOpeningMoveId, StringComparison.Ordinal);
	}

	internal static bool IsMonsterRevivalMove(string moveId)
	{
		return string.Equals(moveId, TestSubjectRespawnMoveId, StringComparison.Ordinal)
			|| string.Equals(moveId, IllusionReviveMoveId, StringComparison.Ordinal);
	}

	internal static int GetJeweledGauntletRepeatPercent(int strengthTier)
	{
		return strengthTier switch
		{
			<= 1 => 10,
			2 => 20,
			_ => 30
		};
	}

	internal static bool AreJeweledGauntletIntentsRepeatable(IReadOnlyList<AbstractIntent> intents)
	{
		return intents.Count > 0 && intents.All(static intent => IsJeweledGauntletIntentTypeRepeatable(intent.IntentType));
	}

	internal static bool IsJeweledGauntletIntentTypeRepeatable(IntentType intentType)
	{
		return intentType is IntentType.Attack
			or IntentType.Buff
			or IntentType.CardDebuff
			or IntentType.Debuff
			or IntentType.DebuffStrong
			or IntentType.Defend
			or IntentType.Heal
			or IntentType.StatusCard;
	}

	internal static IReadOnlyList<AbstractIntent> DuplicateJeweledGauntletIntentGroup(
		IReadOnlyList<AbstractIntent> intents)
	{
		AbstractIntent[] duplicated = new AbstractIntent[intents.Count * 2];
		for (int i = 0; i < intents.Count; i++)
		{
			duplicated[i] = intents[i];
			duplicated[i + intents.Count] = intents[i];
		}

		return duplicated;
	}

	private static void LogJeweledGauntletFailure(string hook, Exception ex)
	{
		if (_jeweledGauntletFailureLogs++ < 10)
		{
			Log.Error($"[{ModInfo.Id}][Mayhem] {hook} failed; enemy Jeweled Gauntlet fell back to one action: {ex}");
		}
	}

	private sealed record JeweledGauntletMoveRepeatState(MonsterModel Monster, MoveState Move);

	private sealed record JeweledGauntletIntentPatchState(
		MoveState Move,
		IReadOnlyList<AbstractIntent> OriginalIntents,
		IReadOnlyList<AbstractIntent> DisplayedIntents);
}
