using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace UniversalDominionSword;

internal static partial class ErasureKill
{
	private const int MaximumSettlementPasses = 8;
	private const int MaximumStabilizationFrames = 128;
	private const int StableFramesToCloseContinuationLease = 8;
	private const int ErasurePatchPriority = 10_000;

	private static readonly FieldInfo PowersField = RequireField(
		typeof(Creature),
		"_powers");

	private static readonly FieldInfo CurrentHpField = RequireField(
		typeof(Creature),
		"_currentHp");

	private static readonly FieldInfo CombatStateBackingField = RequireField(
		typeof(Creature),
		"<CombatState>k__BackingField");

	private static readonly FieldInfo MonsterMoveStateMachineField =
		RequireField(typeof(MonsterModel), "_moveStateMachine");

	private static readonly FieldInfo MonsterIsPerformingMoveField =
		RequireField(typeof(MonsterModel), "_isPerformingMove");

	private static readonly FieldInfo EnemiesField = RequireField(
		typeof(CombatState),
		"_enemies");

	private static readonly FieldInfo AlliesField = RequireField(
		typeof(CombatState),
		"_allies");

	private static readonly FieldInfo EscapedCreaturesField = RequireField(
		typeof(CombatState),
		"_escapedCreatures");

	private static readonly FieldInfo CombatStateChangedField = RequireField(
		typeof(CombatState),
		"CreaturesChanged");

	private static readonly FieldInfo ManagerCreaturesChangedField = RequireField(
		typeof(CombatManager),
		"CreaturesChanged");

	private static readonly FieldInfo ActiveNodesField = RequireField(
		typeof(NCombatRoom),
		"_creatureNodes");

	private static readonly FieldInfo RemovingNodesField = RequireField(
		typeof(NCombatRoom),
		"_removingCreatureNodes");

	private static readonly MethodInfo UpdateCreatureNavigationMethod =
		AccessTools.DeclaredMethod(
			typeof(NCombatRoom),
			"UpdateCreatureNavigation")
		?? throw new MissingMethodException(
			typeof(NCombatRoom).FullName,
			"UpdateCreatureNavigation");

	private static readonly ConditionalWeakTable<ICombatState, CombatLedger>
		Ledgers = new();

	private static readonly ConditionalWeakTable<Creature, LineageBinding>
		Bindings = new();

	private static readonly ConditionalWeakTable<Creature, GenericSlotOrigin>
		GenericSlotOrigins = new();

	private static readonly IEqualityComparer<Creature>
		CreatureReferenceComparer = ReferenceEqualityComparer.Instance;

	private static readonly AsyncLocal<CausalScope?> ActiveScope = new();

	private static readonly AsyncLocal<SlotAllocationTicket?>
		ActiveSlotAllocation = new();

	private static readonly AsyncLocal<bool>
		IsSchedulingDeferredContinuation = new();

	public static async Task Execute(
		Creature target,
		ICombatState combatState)
	{
		if (target.Side != CombatSide.Enemy || target.Monster == null)
		{
			throw new InvalidOperationException(
				"Erasure can only target an enemy monster.");
		}

		if (TryGetBinding(target, out LineageBinding? existing)
			&& ReferenceEquals(existing.Ledger.CombatState, combatState))
		{
			await RestabilizeLineage(existing);
			return;
		}

		CombatLedger ledger = Ledgers.GetValue(
			combatState,
			state => new CombatLedger(state));
		LineageBinding root;
		lock (ledger.Gate)
		{
			ErasureEvidence evidence = CaptureEvidence(target);
			ErasureEvidence[] preexisting = combatState.Allies
				.Concat(combatState.Enemies)
				.Distinct(CreatureReferenceComparer)
				.Select(creature => CaptureEvidence(creature))
				.ToArray();
			ErasureLineage lineage = new(
				++ledger.NextOperationSequence,
				evidence,
				preexisting);
			ledger.Lineages.Add(lineage);
			root = BindMember(
				ledger,
				lineage,
				lineage.Root,
				target);
		}

		Log.Info(
			$"[{ModInfo.Id}] Erasing selected creature {target.ModelId}; " +
			$"combatId={target.CombatId?.ToString() ?? "<none>"}; " +
			$"slot={target.SlotName ?? "<none>"}; " +
			$"operation={root.Lineage.OperationSequence}.");

		await RestabilizeLineage(root);
	}

	private static ICombatState? ReadAttachedCombat(Creature creature)
	{
		return CombatStateBackingField.GetValue(creature) as ICombatState;
	}

	private static string SafeModelId(Creature creature)
	{
		try
		{
			return creature.ModelId.ToString();
		}
		catch
		{
			return creature.Monster?.GetType().FullName
				?? creature.GetType().FullName
				?? "<unknown>";
		}
	}

	private static FieldInfo RequireField(Type type, string name)
	{
		return AccessTools.Field(type, name)
			?? throw new MissingFieldException(type.FullName, name);
	}
}
