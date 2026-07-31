#!/bin/zsh
set -euo pipefail

SCRIPT_DIR="${0:A:h}"
ROOT="${SCRIPT_DIR:h}"
INSPECT="${UDS_STS2_INSPECT:-$ROOT/../tools/sts2-inspect}"
REFS_ROOT="${UDS_REFS_ROOT:-$ROOT/../HextechRunes/versioned-dll-backups}"
TARGETS=(0.107.1 0.110.0)

EXTERNAL_ASSEMBLY=""
EXTERNAL_DEPENDENCY_DIR=""
typeset -a EXTERNAL_QUERIES
typeset -a EXTERNAL_SEARCHES
EXTERNAL_QUERIES=()
EXTERNAL_SEARCHES=()

usage() {
	print -r -- "Usage: ${0:t} [options]"
	print -r -- ""
	print -r -- "Validates the STS2 signatures required by the erasure implementation."
	print -r -- "With no options, only the versioned base-game assemblies are checked."
	print -r -- ""
	print -r -- "Options:"
	print -r -- "  --external-assembly PATH       Inspect an additional managed assembly."
	print -r -- "  --external-dependency-dir DIR  Dependency directory for that assembly."
	print -r -- "  --external-query TEXT          Repeatable sts2-inspect type query."
	print -r -- "  --external-search REGEX        Repeatable required regex in query output."
	print -r -- "  -h, --help                     Show this help."
}

fail() {
	print -u2 -r -- "Signature validation failed: $*"
	exit 1
}

require_file() {
	local path="$1"
	[[ -f "$path" ]] || fail "missing file: $path"
}

require_executable() {
	local path="$1"
	[[ -x "$path" ]] || fail "not executable: $path"
}

require_exact_line() {
	local dump="$1"
	local expected="$2"
	local context="$3"
	if ! grep -Fqx -- "$expected" "$dump"; then
		fail "$context: expected '$expected'"
	fi
}

require_regex() {
	local dump="$1"
	local expected="$2"
	local context="$3"
	if ! grep -Eq -- "$expected" "$dump"; then
		fail "$context: expected pattern '$expected'"
	fi
}

reject_regex() {
	local dump="$1"
	local unexpected="$2"
	local context="$3"
	if grep -Eq -- "$unexpected" "$dump"; then
		fail "$context: unexpected pattern '$unexpected'"
	fi
}

run_type_query() {
	local assembly="$1"
	local query="$2"
	local output="$3"
	local dependency_dir="${4:-}"
	local error_output="$output.stderr"

	if [[ -n "$dependency_dir" ]]; then
		if ! env STS2_GAME_DATA_DIR="$dependency_dir" \
			"$INSPECT" types "$query" --assembly "$assembly" \
			>"$output" 2>"$error_output"; then
			print -u2 -r -- "sts2-inspect error output:"
			command cat "$error_output" >&2
			fail "could not inspect '$query' in $assembly"
		fi
	elif ! "$INSPECT" types "$query" --assembly "$assembly" \
		>"$output" 2>"$error_output"; then
		print -u2 -r -- "sts2-inspect error output:"
		command cat "$error_output" >&2
		fail "could not inspect '$query' in $assembly"
	fi
}

run_decompile_query() {
	local assembly="$1"
	local query="$2"
	local output="$3"
	local dependency_dir="$4"
	local error_output="$output.stderr"

	if ! env STS2_GAME_DATA_DIR="$dependency_dir" \
		"$INSPECT" decompile "$query" --assembly "$assembly" \
		>"$output" 2>"$error_output"; then
		print -u2 -r -- "sts2-inspect error output:"
		command cat "$error_output" >&2
		fail "could not decompile '$query' in $assembly"
	fi
}

while (( $# > 0 )); do
	case "$1" in
		--external-assembly)
			(( $# >= 2 )) || fail "--external-assembly requires a path"
			EXTERNAL_ASSEMBLY="$2"
			shift 2
			;;
		--external-dependency-dir)
			(( $# >= 2 )) || fail "--external-dependency-dir requires a path"
			EXTERNAL_DEPENDENCY_DIR="$2"
			shift 2
			;;
		--external-query)
			(( $# >= 2 )) || fail "--external-query requires text"
			EXTERNAL_QUERIES+=("$2")
			shift 2
			;;
		--external-search)
			(( $# >= 2 )) || fail "--external-search requires a regex"
			EXTERNAL_SEARCHES+=("$2")
			shift 2
			;;
		-h|--help)
			usage
			exit 0
			;;
		*)
			fail "unknown option: $1"
			;;
	esac
done

require_executable "$INSPECT"

TEMP_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/uds-erasure-hooks.XXXXXX")"
cleanup() {
	rm -rf -- "$TEMP_ROOT"
}
trap cleanup EXIT

for target in "${TARGETS[@]}"; do
	refs="$REFS_ROOT/$target/game-refs"
	assembly="$refs/sts2.dll"
	require_file "$assembly"
	require_file "$refs/GodotSharp.dll"
	require_file "$refs/0Harmony.dll"

	creature_dump="$TEMP_ROOT/$target-creature.txt"
	state_dump="$TEMP_ROOT/$target-combat-state.txt"
	manager_dump="$TEMP_ROOT/$target-combat-manager.txt"
	room_dump="$TEMP_ROOT/$target-combat-room.txt"
	command_dump="$TEMP_ROOT/$target-creature-command.txt"
	hook_dump="$TEMP_ROOT/$target-hook.txt"
	monster_dump="$TEMP_ROOT/$target-monster-model.txt"
	encounter_dump="$TEMP_ROOT/$target-encounter-model.txt"
	creature_node_dump="$TEMP_ROOT/$target-creature-node.txt"
	callable_dump="$TEMP_ROOT/$target-callable.txt"
	action_executor_dump="$TEMP_ROOT/$target-action-executor.txt"
	manager_source="$TEMP_ROOT/$target-combat-manager.cs"
	hook_source="$TEMP_ROOT/$target-hook.cs"
	action_executor_source="$TEMP_ROOT/$target-action-executor.cs"

	run_type_query \
		"$assembly" \
		"MegaCrit.Sts2.Core.Entities.Creatures.Creature" \
		"$creature_dump"
	run_type_query \
		"$assembly" \
		"MegaCrit.Sts2.Core.Combat.CombatState" \
		"$state_dump"
	run_type_query \
		"$assembly" \
		"MegaCrit.Sts2.Core.Combat.CombatManager" \
		"$manager_dump"
	run_type_query \
		"$assembly" \
		"MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom" \
		"$room_dump"
	run_type_query \
		"$assembly" \
		"MegaCrit.Sts2.Core.Commands.CreatureCmd" \
		"$command_dump"
	run_type_query \
		"$assembly" \
		"MegaCrit.Sts2.Core.Hooks.Hook" \
		"$hook_dump"
	run_type_query \
		"$assembly" \
		"MegaCrit.Sts2.Core.Models.MonsterModel" \
		"$monster_dump"
	run_type_query \
		"$assembly" \
		"MegaCrit.Sts2.Core.Models.EncounterModel" \
		"$encounter_dump"
	run_type_query \
		"$assembly" \
		"MegaCrit.Sts2.Core.Nodes.Combat.NCreature" \
		"$creature_node_dump"
	run_type_query \
		"$refs/GodotSharp.dll" \
		"Godot.Callable" \
		"$callable_dump"
	run_type_query \
		"$assembly" \
		"MegaCrit.Sts2.Core.GameActions.ActionExecutor" \
		"$action_executor_dump"
	run_decompile_query \
		"$assembly" \
		"MegaCrit.Sts2.Core.Combat.CombatManager" \
		"$manager_source" \
		"$refs"
	run_decompile_query \
		"$assembly" \
		"MegaCrit.Sts2.Core.Hooks.Hook" \
		"$hook_source" \
		"$refs"
	run_decompile_query \
		"$assembly" \
		"MegaCrit.Sts2.Core.GameActions.ActionExecutor" \
		"$action_executor_source" \
		"$refs"

	context="STS2 $target Creature"
	require_exact_line "$creature_dump" \
		"  M Void HealInternal(Decimal amount)" "$context"
	require_exact_line "$creature_dump" \
		"  M Void SetCurrentHpInternal(Decimal amount)" "$context"
	require_exact_line "$creature_dump" \
		"  M Void set_CurrentHp(Int32 value)" "$context"
	require_exact_line "$creature_dump" \
		"  M Boolean get_IsAlive()" "$context"
	require_exact_line "$creature_dump" \
		"  M Boolean get_IsDead()" "$context"
	require_exact_line "$creature_dump" \
		"  F Int32 _currentHp" "$context"
	require_exact_line "$creature_dump" \
		"  F List<PowerModel> _powers" "$context"
	require_exact_line "$creature_dump" \
		"  F ICombatState <CombatState>k__BackingField" "$context"
	require_exact_line "$creature_dump" \
		"  M Void set_CombatState(ICombatState value)" "$context"
	require_exact_line "$creature_dump" \
		"  M Void InvokeDiedEvent()" "$context"

	context="STS2 $target CombatState"
	require_exact_line "$state_dump" \
		"  M Creature CreateCreature(MonsterModel monster, CombatSide side, String slot)" \
		"$context"
	require_exact_line "$state_dump" \
		"  M Void AttachCreature(Creature creature)" "$context"
	require_exact_line "$state_dump" \
		"  M Void AddCreature(Creature creature)" "$context"
	require_exact_line "$state_dump" \
		"  M Boolean ContainsCreature(Creature creature)" "$context"
	require_exact_line "$state_dump" \
		"  F List<Creature> _allies" "$context"
	require_exact_line "$state_dump" \
		"  F List<Creature> _enemies" "$context"
	require_exact_line "$state_dump" \
		"  F List<Creature> _escapedCreatures" "$context"
	require_exact_line "$state_dump" \
		"  F Action<ICombatState> CreaturesChanged" "$context"

	context="STS2 $target CombatManager"
	require_exact_line "$manager_dump" \
		"  M Void AddCreature(Creature creature)" "$context"
	require_exact_line "$manager_dump" \
		"  M Task AfterCreatureAdded(Creature creature)" "$context"
	require_exact_line "$manager_dump" \
		"  M Task<Boolean> CheckWinCondition()" "$context"
	require_exact_line "$manager_dump" \
		"  M Task EndCombatInternal()" "$context"
	require_exact_line "$manager_dump" \
		"  M CombatStateTracker get_StateTracker()" "$context"
	require_exact_line "$manager_dump" \
		"  F Action<CombatState> CreaturesChanged" "$context"
	require_regex "$manager_source" \
		'^[[:space:]]*public (async )?System\.Threading\.Tasks\.Task<bool> CheckWinCondition\(\)$' \
		"$context"
	require_regex "$manager_source" \
		'^[[:space:]]*public (async )?System\.Threading\.Tasks\.Task EndCombatInternal\(\)$' \
		"$context"

	case "$target" in
		0.107.1)
			require_exact_line "$manager_dump" \
				"  F CombatState _state" "$context"
			require_exact_line "$manager_dump" \
				"  F Boolean <IsInProgress>k__BackingField" "$context"
			require_exact_line "$manager_dump" \
				"  F Boolean <IsStarting>k__BackingField" "$context"
			require_exact_line "$manager_dump" \
				"  F PendingLossState _pendingLoss" "$context"
			reject_regex "$manager_dump" \
				'^  M Task<Boolean> CheckWinCondition\(CombatTurnState turnState\)$' \
				"$context"
			reject_regex "$manager_dump" \
				'^  M Task EndCombatInternal\(CombatTurnState turnState\)$' \
				"$context"
			;;
		0.110.0)
			turn_state_dump="$TEMP_ROOT/$target-combat-turn-state.txt"
			run_type_query \
				"$assembly" \
				"MegaCrit.Sts2.Core.Combat.CombatTurnState" \
				"$turn_state_dump"

			require_exact_line "$manager_dump" \
				"  F CombatTurnState _turnState" "$context"
			require_exact_line "$manager_dump" \
				"  M Task<Boolean> CheckWinCondition(CombatTurnState turnState)" \
				"$context"
			require_exact_line "$manager_dump" \
				"  M Task EndCombatInternal(CombatTurnState turnState)" \
				"$context"
			require_regex "$manager_source" \
				'^[[:space:]]*private (async )?System\.Threading\.Tasks\.Task<bool> CheckWinCondition\(CombatTurnState turnState\)$' \
				"$context"
			require_regex "$manager_source" \
				'^[[:space:]]*private (async )?System\.Threading\.Tasks\.Task EndCombatInternal\(CombatTurnState turnState\)$' \
				"$context"

			context="STS2 $target CombatTurnState"
			require_exact_line "$turn_state_dump" \
				"  M CombatState get_State()" "$context"
			require_exact_line "$turn_state_dump" \
				"  M Boolean get_IsInProgress()" "$context"
			require_exact_line "$turn_state_dump" \
				"  M Boolean get_IsStarting()" "$context"
			require_exact_line "$turn_state_dump" \
				"  M PendingLossState get_PendingLoss()" "$context"
			require_exact_line "$turn_state_dump" \
				"  F CombatState <State>k__BackingField" "$context"
			require_exact_line "$turn_state_dump" \
				"  F Boolean <IsInProgress>k__BackingField" "$context"
			require_exact_line "$turn_state_dump" \
				"  F Boolean <IsStarting>k__BackingField" "$context"
			require_exact_line "$turn_state_dump" \
				"  F PendingLossState <PendingLoss>k__BackingField" "$context"
			;;
		*)
			fail "unsupported base-game target: $target"
			;;
	esac

	context="STS2 $target NCombatRoom"
	require_exact_line "$room_dump" \
		"  M Void AddCreature(Creature creature)" "$context"
	require_exact_line "$room_dump" \
		"  M Void UpdateCreatureNavigation()" "$context"
	require_exact_line "$room_dump" \
		"  M IEnumerable<NCreature> get_CreatureNodes()" "$context"
	require_exact_line "$room_dump" \
		"  M IEnumerable<NCreature> get_RemovingCreatureNodes()" "$context"
	require_exact_line "$room_dump" \
		"  F List<NCreature> _creatureNodes" "$context"
	require_exact_line "$room_dump" \
		"  F List<NCreature> _removingCreatureNodes" "$context"

	context="STS2 $target NCreature"
	require_exact_line "$creature_node_dump" \
		"  M NCreature Create(Creature entity)" "$context"
	require_exact_line "$creature_node_dump" \
		"  M Creature get_Entity()" "$context"

	context="STS2 $target Godot Callable"
	require_exact_line "$callable_dump" \
		"  M Void CallDeferred(Variant[] args)" "$context"

	context="STS2 $target ActionExecutor"
	require_exact_line "$action_executor_dump" \
		"  M Void JustBeforeActionFinished(GameAction action)" "$context"
	require_regex "$action_executor_source" \
		'CombatManager\.Instance\.CheckWinCondition\(\)\.GetAwaiter\(\)' \
		"$context"
	require_regex "$action_executor_source" \
		'JustBeforeFinished -= actionExecutor\.JustBeforeActionFinished' \
		"$context"

	context="STS2 $target CreatureCmd"
	require_exact_line "$command_dump" \
		"  M Task Add(Creature creature)" "$context"

	context="STS2 $target Hook"
	require_exact_line "$hook_dump" \
		"  M Task AfterCreatureAddedToCombat(ICombatState combatState, Creature creature)" \
		"$context"
	require_exact_line "$hook_dump" \
		"  M Task BeforeDeath(IRunState runState, ICombatState combatState, Creature creature)" \
		"$context"
	require_exact_line "$hook_dump" \
		"  M Task AfterDeath(IRunState runState, ICombatState combatState, Creature creature, Boolean wasRemovalPrevented, Single deathAnimLength)" \
		"$context"
	require_exact_line "$hook_dump" \
		"  M Boolean ShouldStopCombatFromEnding(ICombatState combatState)" \
		"$context"
	require_regex "$hook_source" \
		'^[[:space:]]*public static bool ShouldStopCombatFromEnding\(ICombatState combatState\)$' \
		"$context"

	context="STS2 $target MonsterModel"
	require_exact_line "$monster_dump" \
		"  M Task PerformMove()" "$context"
	require_exact_line "$monster_dump" \
		"  F Boolean _isPerformingMove" "$context"
	require_exact_line "$monster_dump" \
		"  F MonsterMoveStateMachine _moveStateMachine" "$context"

	context="STS2 $target EncounterModel"
	require_exact_line "$encounter_dump" \
		"  M String GetNextSlot(ICombatState combatState)" "$context"

	print -r -- "Validated required erasure signatures for STS2 $target."
done

external_requested=0
if [[ -n "$EXTERNAL_ASSEMBLY" ]] \
	|| (( ${#EXTERNAL_QUERIES[@]} > 0 )) \
	|| (( ${#EXTERNAL_SEARCHES[@]} > 0 )) \
	|| [[ -n "$EXTERNAL_DEPENDENCY_DIR" ]]; then
	external_requested=1
fi

if (( external_requested )); then
	[[ -n "$EXTERNAL_ASSEMBLY" ]] \
		|| fail "--external-assembly is required for external checks"
	(( ${#EXTERNAL_QUERIES[@]} > 0 )) \
		|| fail "at least one --external-query is required"
	(( ${#EXTERNAL_SEARCHES[@]} > 0 )) \
		|| fail "at least one --external-search is required"
	require_file "$EXTERNAL_ASSEMBLY"
	if [[ -n "$EXTERNAL_DEPENDENCY_DIR" && ! -d "$EXTERNAL_DEPENDENCY_DIR" ]]; then
		fail "external dependency directory does not exist: $EXTERNAL_DEPENDENCY_DIR"
	fi

	external_dump="$TEMP_ROOT/external.txt"
	: >"$external_dump"
	query_index=0
	for query in "${EXTERNAL_QUERIES[@]}"; do
		(( query_index += 1 ))
		query_dump="$TEMP_ROOT/external-$query_index.txt"
		run_type_query \
			"$EXTERNAL_ASSEMBLY" \
			"$query" \
			"$query_dump" \
			"$EXTERNAL_DEPENDENCY_DIR"
		command cat "$query_dump" >>"$external_dump"
	done

	for pattern in "${EXTERNAL_SEARCHES[@]}"; do
		if ! grep -Eq -- "$pattern" "$external_dump"; then
			fail "external assembly output did not match regex: $pattern"
		fi
	done
	print -r -- \
		"Validated ${#EXTERNAL_SEARCHES[@]} external assembly signature patterns."
fi

print -r -- "Erasure hook signature checks passed."
