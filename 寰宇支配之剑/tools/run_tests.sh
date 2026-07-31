#!/bin/zsh
set -euo pipefail

SCRIPT_DIR="${0:A:h}"
ROOT="${SCRIPT_DIR:h}"
ERASURE_SOURCES=("$ROOT"/src/ErasureKill*.cs)
PRODUCTION_SOURCES=("$ROOT"/src/*.cs)

reject_fixed_pattern() {
	local pattern="$1"
	shift
	if grep -Fq -- "$pattern" "$@"; then
		print -u2 -r -- "Unexpected source pattern: $pattern"
		return 1
	fi
	return 0
}

require_regex_pattern() {
	local pattern="$1"
	shift
	if ! grep -Eq -- "$pattern" "$@"; then
		print -u2 -r -- "Missing source pattern: $pattern"
		return 1
	fi
	return 0
}

require_method_pattern() {
	local file="$1"
	local signature="$2"
	local pattern="$3"
	if ! awk -v signature="$signature" -v pattern="$pattern" '
		index($0, signature) { in_method = 1 }
		in_method && index($0, pattern) { found = 1 }
		in_method && $0 == "\t}" { exit }
		END { exit found ? 0 : 1 }
	' "$file"; then
		print -u2 -r -- \
			"Method '$signature' is missing source pattern: $pattern"
		return 1
	fi
	return 0
}

reject_method_pattern() {
	local file="$1"
	local signature="$2"
	local pattern="$3"
	if awk -v signature="$signature" -v pattern="$pattern" '
		index($0, signature) { in_method = 1 }
		in_method && index($0, pattern) { found = 1 }
		in_method && $0 == "\t}" { exit }
		END { exit found ? 0 : 1 }
	' "$file"; then
		print -u2 -r -- \
			"Method '$signature' contains forbidden source pattern: $pattern"
		return 1
	fi
	return 0
}

python3 -m json.tool "$ROOT/assets/UniversalDominionSword.json" >/dev/null
python3 -m json.tool "$ROOT/assets/localization/zhs/relics.json" >/dev/null
python3 -m json.tool "$ROOT/assets/localization/zhs/cards.json" >/dev/null
python3 -m json.tool "$ROOT/assets/localization/eng/relics.json" >/dev/null
python3 -m json.tool "$ROOT/assets/localization/eng/cards.json" >/dev/null

for image in \
	"$ROOT/assets/images/relics/infinity_sword_layer_0.png" \
	"$ROOT/assets/images/relics/infinity_sword_layer_1.png" \
	"$ROOT/assets/images/relics/infinity_sword_mask.png" \
	"$ROOT/assets/images/relics/universal_dominion_sword.png" \
	"$ROOT"/assets/images/relics/cosmic_<0-9>.png \
	"$ROOT/assets/images/cards/universal_dominion_sword_card.png"; do
	file "$image" | grep -q "PNG image data"
done

CARD_WIDTH="$(sips -g pixelWidth "$ROOT/assets/images/cards/universal_dominion_sword_card.png" | awk '/pixelWidth/ { print $2 }')"
CARD_HEIGHT="$(sips -g pixelHeight "$ROOT/assets/images/cards/universal_dominion_sword_card.png" | awk '/pixelHeight/ { print $2 }')"
[[ "$CARD_WIDTH" == "250" && "$CARD_HEIGHT" == "190" ]]
grep -Fq "render_static_cosmic_frame.py" "$ROOT/tools/generate_card_portrait.sh"
grep -Fq "radial-gradient:'#35145f-#03000b'" "$ROOT/tools/generate_card_portrait.sh"

grep -q '"author": "Natsuki"' "$ROOT/assets/UniversalDominionSword.json"
grep -q '"version": "0.2.4"' "$ROOT/assets/UniversalDominionSword.json"
grep -q '"min_game_version": "0.107.1"' "$ROOT/assets/UniversalDominionSword.json"
grep -Fq '<AssemblyName>UniversalDominionSword.Loader</AssemblyName>' "$ROOT/loader/UniversalDominionSword.Loader.csproj"
grep -Fq '<DebugType>none</DebugType>' "$ROOT/loader/UniversalDominionSword.Loader.csproj"
grep -Fq '<DebugType>none</DebugType>' "$ROOT/src/UniversalDominionSword.csproj"
grep -Fq 'Erasure.AuditContractVersion' "$ROOT/src/ErasurePatchContract.cs"
grep -Fq 'Erasure.KnownRisk' "$ROOT/src/ErasurePatchContract.cs"
grep -Fq 'ErasureBoundaryAttribute' "$ROOT/src/ErasurePatchContract.cs"
grep -Fq 'ErasurePatchContract.RuntimeSummary' "$ROOT/src/ModEntry.cs"
if grep -Eq '\.Unpatch(All)?[[:space:]]*\(' "${PRODUCTION_SOURCES[@]}"; then
	print -u2 -r -- "Production source must not unpatch Harmony patches."
	exit 1
fi
if grep -Eq '\.GetPatchInfo[[:space:]]*\(' "${PRODUCTION_SOURCES[@]}"; then
	print -u2 -r -- "Production source must not enumerate third-party patches."
	exit 1
fi
if grep -Eq 'Priority\.(First|Last|VeryHigh|VeryLow|High|Low)|(^|[^A-Za-z])(prefix|postfix|finalizer|transpiler)Priority:|(^|[^A-Za-z])(priority|before|after)[[:space:]]*=|Harmony(Priority|Before|After)' "${PRODUCTION_SOURCES[@]}"; then
	print -u2 -r -- "Production source must not override Harmony priority."
	exit 1
fi
grep -Fq '[ModInitializer(nameof(Initialize))]' "$ROOT/loader/LoaderBootstrap.cs"
grep -Fq 'universal-dominion-sword-variants.manifest' "$ROOT/loader/LoaderBootstrap.cs"
grep -Fq 'AssociateAssemblyWithMod' "$ROOT/loader/LoaderBootstrap.cs"
grep -Fq 'ReflectionHelperModTypesPostfix' "$ROOT/loader/LoaderBootstrap.cs"
grep -Fq 'ModManager.OnModDetected += OnLegacyModDetected' "$ROOT/loader/LoaderBootstrap.cs"
grep -Fq 'LegacyModAssemblyField?.SetValue(mod, _selectedVariantAssembly)' "$ROOT/loader/LoaderBootstrap.cs"
grep -Fq 'manifest?.Schema != 1' "$ROOT/loader/LoaderBootstrap.cs"
grep -Fq 'Duplicate compatibility' "$ROOT/loader/LoaderBootstrap.cs"
grep -Fq 'VariantSelectionPolicy.PickCompatibleVersion' "$ROOT/loader/LoaderBootstrap.cs"
grep -Fq 'ordered.LastOrDefault(version => version <= host)' "$ROOT/loader/VariantSelectionPolicy.cs"
grep -Fq 'TARGETS=(0.107.1 0.110.0)' "$ROOT/tools/build_and_deploy.sh"
grep -Fq 'UniversalDominionSwordSts2Target="$target"' "$ROOT/tools/build_and_deploy.sh"
grep -Fq 'variant_manifest_target_args+=(--target "$target")' "$ROOT/tools/build_and_deploy.sh"
grep -Fq 'RelicRarity.Ancient' "$ROOT/src/UniversalDominionSwordRelic.cs"
grep -Fq 'HoverTipFactory.FromCardWithCardHoverTips<UniversalDominionSwordCard>()' "$ROOT/src/UniversalDominionSwordRelic.cs"
grep -q 'public override async Task AfterObtained()' "$ROOT/src/UniversalDominionSwordRelic.cs"
grep -q 'PileType.Deck' "$ROOT/src/UniversalDominionSwordRelic.cs"
grep -q 'ModelDb.Card<UniversalDominionSwordCard>()' "$ROOT/src/UniversalDominionSwordRelic.cs"
grep -Fq 'AddModelToPool<EventRelicPool, UniversalDominionSwordRelic>()' "$ROOT/src/ModEntry.cs"
reject_fixed_pattern \
	'AddModelToPool<SharedRelicPool, UniversalDominionSwordRelic>()' \
	"$ROOT/src/ModEntry.cs"
grep -Fq 'NeowFourthOption.Install(harmony)' "$ROOT/src/ModEntry.cs"
grep -Fq 'AccessTools.DeclaredMethod(typeof(Neow), "GenerateInitialOptions")' "$ROOT/src/NeowFourthOption.cs"
grep -Fq 'options.Add(CreateSwordOption(__instance))' "$ROOT/src/NeowFourthOption.cs"
grep -Fq 'NEOW.pages.DONE.POSITIVE.description' "$ROOT/src/NeowFourthOption.cs"
grep -q '"UNIVERSAL_DOMINION_SWORD_CARD.title": "抹杀"' "$ROOT/assets/localization/zhs/cards.json"
grep -Fq '"UNIVERSAL_DOMINION_SWORD_CARD.description": "造成[rainbow freq=0.3 sat=0.8 val=1]无限[/rainbow]点伤害。\n这张牌在本局游戏中的耗能永久增加1。"' "$ROOT/assets/localization/zhs/cards.json"
grep -Fq '"UNIVERSAL_DOMINION_SWORD_CARD.description": "Deal [rainbow freq=0.3 sat=0.8 val=1]infinite[/rainbow] damage.\nThis card permanently costs 1 more Energy this run."' "$ROOT/assets/localization/eng/cards.json"
grep -q '"UNIVERSAL_DOMINION_SWORD_RELIC.flavor": "汝掌心中者，寰宇之力也。"' "$ROOT/assets/localization/zhs/relics.json"
grep -Fq '[SavedProperty]' "$ROOT/src/UniversalDominionSwordCard.cs"
grep -Fq 'TargetType.AnyEnemy' "$ROOT/src/UniversalDominionSwordCard.cs"
grep -Fq 'deckVersion.IncreasePermanentCost()' "$ROOT/src/UniversalDominionSwordCard.cs"
grep -Fq 'Owner.Character.AttackAnimDelay' "$ROOT/src/UniversalDominionSwordCard.cs"
grep -Fq 'await InvokeOriginalKillWithoutCheckingWinCondition(' \
	"$ROOT/src/ErasureKill.Stabilization.cs"
grep -Fq 'CreateReversePatcher(' \
	"$ROOT/src/ErasureKill.Patches.cs"
grep -Fq 'HarmonyReversePatchType.Original' \
	"$ROOT/src/ErasureKill.Patches.cs"
grep -Fq 'PatchCanonicalSettlementEntry(harmony);' \
	"$ROOT/src/ErasureKill.Patches.cs"
grep -Fq 'AssemblyBuilder.DefineDynamicAssembly(' \
	"$ROOT/src/ErasureKill.SettlementPipeline.cs"
grep -Fq 'Patch(HarmonyReversePatchType.Original)' \
	"$ROOT/src/ErasureKill.SettlementPipeline.cs"
grep -Fq 'nameof(RemoveCreatureNodeForErasure)' \
	"$ROOT/src/ErasureKill.DeathPipeline.cs"
grep -Fq 'nameof(CombatManagerRemoveCreatureForErasure)' \
	"$ROOT/src/ErasureKill.DeathPipeline.cs"
grep -Fq 'nameof(CombatStateRemoveCreatureForErasure)' \
	"$ROOT/src/ErasureKill.DeathPipeline.cs"
grep -Fq 'nameof(InvokeOriginalRemoveCreatureNode)' \
	"$ROOT/src/ErasureKill.Patches.cs"
grep -Fq 'nameof(InvokeOriginalCombatManagerRemoveCreature)' \
	"$ROOT/src/ErasureKill.Patches.cs"
grep -Fq 'nameof(InvokeOriginalCombatStateRemoveCreature)' \
	"$ROOT/src/ErasureKill.Patches.cs"
reject_fixed_pattern \
	'await CreatureCmd.Kill(seed.Creature' \
	"$ROOT/src/ErasureKill.Stabilization.cs"
reject_fixed_pattern \
	'CombatManager.Instance.RemoveCreature' \
	"${ERASURE_SOURCES[@]}"
reject_fixed_pattern '.RemoveCreature(creature)' "${ERASURE_SOURCES[@]}"
reject_fixed_pattern 'ErasurePatchPriority' "${ERASURE_SOURCES[@]}"
reject_fixed_pattern 'HarmonyPriority' "${ERASURE_SOURCES[@]}"
reject_fixed_pattern 'prefixPriority:' "${ERASURE_SOURCES[@]}"
reject_fixed_pattern 'postfixPriority:' "${ERASURE_SOURCES[@]}"
reject_fixed_pattern 'finalizerPriority:' "${ERASURE_SOURCES[@]}"
grep -Fq 'CurrentHpField.SetValue(creature, 0)' "${ERASURE_SOURCES[@]}"
grep -Fq 'PowersField.GetValue(creature) is IList powers' "${ERASURE_SOURCES[@]}"
grep -Fq 'CombatStateBackingField.SetValue(creature, null)' "${ERASURE_SOURCES[@]}"
reject_fixed_pattern 'DebugOnlyGetState()' "${ERASURE_SOURCES[@]}"
require_method_pattern \
	"$ROOT/src/ErasureKill.ManagerState.cs" \
	'private static ICombatState? ReadManagerCombatState(' \
	'LegacyManagerStateField.GetValue(manager)'
require_method_pattern \
	"$ROOT/src/ErasureKill.ManagerState.cs" \
	'private static ICombatState? ReadManagerCombatState(' \
	'ManagerTurnStateField.GetValue(manager)'
require_method_pattern \
	"$ROOT/src/ErasureKill.ManagerState.cs" \
	'private static ICombatState? ReadManagerCombatState(' \
	'TurnStateCombatStateField.GetValue('
grep -Fq 'EscapedCreaturesField' "$ROOT/src/ErasureKill.cs"
grep -Fq 'GetRequiredList(EscapedCreaturesField, concrete)' \
	"$ROOT/src/ErasureKill.Convergence.cs"
grep -Fq 'StateTracker.Unsubscribe(creature)' "${ERASURE_SOURCES[@]}"
grep -Fq 'MonsterMoveStateMachineField.SetValue(monster, null)' "${ERASURE_SOURCES[@]}"
grep -Fq 'MonsterIsPerformingMoveField.SetValue(monster, false)' "${ERASURE_SOURCES[@]}"
grep -Fq 'nameof(MonsterModel.PerformMove)' "${ERASURE_SOURCES[@]}"
grep -Fq 'UpdateCreatureNavigationMethod.Invoke(room, null)' "${ERASURE_SOURCES[@]}"
grep -Fq 'node.QueueFreeSafely()' "${ERASURE_SOURCES[@]}"
grep -Fq 'node.IsQueuedForDeletion()' "${ERASURE_SOURCES[@]}"
grep -Fq 'ErasureVisualExitPolicy.ShouldPreserve(' \
	"$ROOT/src/ErasureKill.Convergence.cs"
grep -Fq 'EnsureCanonicalVisualExit(ledger, seed.Creature);' \
	"$ROOT/src/ErasureKill.Stabilization.cs"
grep -Fq 'ReserveCanonicalVisualExit(ledger, seed.Creature);' \
	"$ROOT/src/ErasureKill.Stabilization.cs"
grep -Fq 'node.StartDeathAnim(shouldRemove: true);' \
	"$ROOT/src/ErasureKill.Convergence.cs"
grep -Fq 'room.RemoveCreatureNode(node);' \
	"$ROOT/src/ErasureKill.Convergence.cs"
grep -Fq 'node.DeathAnimationTask is { IsCompleted: false }' \
	"$ROOT/src/ErasureKill.Convergence.cs"
grep -Fq 'public HashSet<NCreature> VisualExitNodes' \
	"$ROOT/src/ErasureKill.Tracking.cs"
grep -Fq 'ManagerCreaturesChangedField' "${ERASURE_SOURCES[@]}"
grep -Fq 'InvokeHandlers(' "${ERASURE_SOURCES[@]}"
grep -Fq 'ErasureLineage' "${ERASURE_SOURCES[@]}"
grep -Fq 'RunTerminationTransaction' "${ERASURE_SOURCES[@]}"
grep -Fq 'TryBeginCanonicalTermination()' "${ERASURE_SOURCES[@]}"
grep -Fq 'RequestImmediateCombatCompletion' "${ERASURE_SOURCES[@]}"
reject_fixed_pattern 'MaximumStabilizationFrames' "${ERASURE_SOURCES[@]}"
reject_fixed_pattern 'StableFramesToCloseContinuationLease' "${ERASURE_SOURCES[@]}"
reject_fixed_pattern 'AcquireContinuationLease()' "${ERASURE_SOURCES[@]}"
reject_fixed_pattern 'ReleaseContinuationLease()' "${ERASURE_SOURCES[@]}"
grep -Fq 'internal static partial class ErasureKill' "${ERASURE_SOURCES[@]}"
grep -Fq 'Bindings.Remove(creature)' "${ERASURE_SOURCES[@]}"
grep -Fq 'binding.Ledger.CombatState' "${ERASURE_SOURCES[@]}"
grep -Fq 'PreexistingCollision' "$ROOT/src/ErasureLineage.cs"
grep -Fq 'MaximumGeneration = 64' "$ROOT/src/ErasureLineage.cs"
grep -Fq 'MaximumContinuationClaims = 256' "$ROOT/src/ErasureLineage.cs"
grep -Fq 'Kind == ErasureAdmissionKind.LimitReached' \
	"$ROOT/src/ErasureLineage.cs"
grep -Fq 'IsCausalOverflow: true' \
	"$ROOT/src/ErasureKill.Tracking.cs"
grep -Fq 'ErasureContinuationToken' "$ROOT/src/ErasureLineage.cs"
grep -Fq 'ErasureAdmissionKind.CausalToken' "$ROOT/src/ErasureLineage.cs"
grep -Fq 'ErasureAdmissionKind.TerminalTransaction' \
	"$ROOT/src/ErasureLineage.cs"
grep -Fq 'WasSoleLivingPrimaryEnemyAtStart' \
	"$ROOT/src/ErasureLineage.cs"
grep -Fq 'ActiveTerminationLineages' \
	"$ROOT/src/ErasureKill.Tracking.cs"
reject_fixed_pattern 'usedGenericSlotAllocator' "$ROOT/src/ErasureLineage.cs"
grep -Fq 'ErasureMutationJournal' "$ROOT/src/ErasureMutationJournal.cs"
grep -Fq 'MaximumRecordedMutations = 512' \
	"$ROOT/src/ErasureMutationJournal.cs"
reject_fixed_pattern 'nameof(EncounterModel.GetNextSlot)' \
	"$ROOT/src/ErasureKill.Patches.cs"
grep -Fq 'GetKillStateMachineMoveNext()' \
	"$ROOT/src/ErasureKill.Patches.cs"
grep -Fq 'transpilerName: nameof(ErasureDeathPipelineTranspiler)' \
	"$ROOT/src/ErasureKill.Patches.cs"
grep -Fq 'nameof(Hook.BeforeDeath)' \
	"$ROOT/src/ErasureKill.DeathPipeline.cs"
grep -Fq 'nameof(Hook.ShouldDie)' \
	"$ROOT/src/ErasureKill.DeathPipeline.cs"
grep -Fq 'nameof(Hook.AfterDeath)' \
	"$ROOT/src/ErasureKill.DeathPipeline.cs"
grep -Fq 'nameof(Hook.ShouldCreatureBeRemovedFromCombatAfterDeath)' \
	"$ROOT/src/ErasureKill.DeathPipeline.cs"
grep -Fq 'nameof(Creature.InvokeDiedEvent)' \
	"$ROOT/src/ErasureKill.DeathPipeline.cs"
grep -Fq 'nameof(Creature.RemoveAllPowersAfterDeath)' \
	"$ROOT/src/ErasureKill.DeathPipeline.cs"
grep -Fq '$"get_{nameof(Creature.IsPrimaryEnemy)}"' \
	"$ROOT/src/ErasureKill.DeathPipeline.cs"
grep -Fq 'OrderObservedCreatures' \
	"$ROOT/src/ErasureKill.Convergence.cs"
grep -Fq 'nameof(CombatState.CreateCreature)' "${ERASURE_SOURCES[@]}"
grep -Fq 'nameof(Callable.CallDeferred)' "${ERASURE_SOURCES[@]}"
grep -Fq 'prefixName: nameof(DeferredCallablePrefix)' \
	"$ROOT/src/ErasureKill.Patches.cs"
require_method_pattern \
	"$ROOT/src/ErasureKill.Patches.cs" \
	'private static bool DeferredCallablePrefix(' \
	'InvokeCausalCallback(original, capturedArgs, scope)'
grep -Fq 'private static void InvokeCausalCallback(' \
	"$ROOT/src/ErasureKill.Patches.cs"
grep -Fq 'ActiveScope.Value = scope;' \
	"$ROOT/src/ErasureKill.Patches.cs"
grep -Fq 'IsUnsupportedTaskReturnConversion(exception)' \
	"$ROOT/src/ErasureKill.Patches.cs"
grep -Fq 'ShouldExecuteCausalCallback(scope)' \
	"$ROOT/src/ErasureKill.Patches.cs"
grep -Fq 'ErasureDeferredCallbackPolicy.Evaluate(snapshot)' \
	"$ROOT/src/ErasureKill.Patches.cs"
grep -Fq 'IsCompletionFlightRunning: completionRunning' \
	"$ROOT/src/ErasureKill.Patches.cs"
grep -Fq 'IsLineageCertified: lineageCertified' \
	"$ROOT/src/ErasureKill.Patches.cs"
grep -Fq 'DiscardCommittedLineage' \
	"$ROOT/src/ErasureDeferredCallbackPolicy.cs"
reject_fixed_pattern 'InvokeDeferredContinuation' \
	"$ROOT/src/ErasureKill.Patches.cs"
grep -Fq 'private static bool TryTrackCandidate(' \
	"$ROOT/src/ErasureKill.Tracking.cs"
grep -Fq 'private static void SettleLineage(' \
	"$ROOT/src/ErasureKill.Convergence.cs"
grep -Fq 'nameof(NCreature.Create)' "${ERASURE_SOURCES[@]}"
grep -Fq 'postfixName: nameof(NCreatureCreatePostfix)' \
	"$ROOT/src/ErasureKill.Patches.cs"
require_regex_pattern \
	'nameof\\(CombatState\\.AttachCreature\\)|"AttachCreature"' \
	"${ERASURE_SOURCES[@]}"
grep -Fq 'postfixName: nameof(CombatStateAttachPostfix)' \
	"$ROOT/src/ErasureKill.Patches.cs"
grep -Fq 'nameof(Creature.CombatState)' \
	"$ROOT/src/ErasureKill.Patches.cs"
grep -Fq 'postfixName: nameof(CombatStateSetterPostfix)' \
	"$ROOT/src/ErasureKill.Patches.cs"
grep -Fq 'nameof(CombatState.AddCreature)' "${ERASURE_SOURCES[@]}"
grep -Fq 'nameof(CombatManager.AddCreature)' "${ERASURE_SOURCES[@]}"
grep -Fq 'nameof(NCombatRoom.AddCreature)' "${ERASURE_SOURCES[@]}"
grep -Fq 'nameof(CombatManager.AfterCreatureAdded)' "${ERASURE_SOURCES[@]}"
grep -Fq 'nameof(Hook.AfterCreatureAddedToCombat)' "${ERASURE_SOURCES[@]}"
grep -Fq 'nameof(CombatManager.CheckWinCondition)' "${ERASURE_SOURCES[@]}"
grep -Fq 'foreach (MethodInfo checkWinMethod in GetCheckWinMethods())' \
	"$ROOT/src/ErasureKill.Patches.cs"
grep -Fq 'Type.EmptyTypes' "$ROOT/src/ErasureKill.Patches.cs"
grep -Fq '.GetDeclaredMethods(typeof(CombatManager))' \
	"$ROOT/src/ErasureKill.Patches.cs"
grep -Fq 'prefixName: nameof(CheckWinCapturePrefix)' \
	"$ROOT/src/ErasureKill.Patches.cs"
grep -Fq 'finalizerName: nameof(CheckWinConditionFinalizer)' \
	"$ROOT/src/ErasureKill.Patches.cs"
grep -Fq 'bool __runOriginal' "$ROOT/src/ErasureKill.Patches.cs"
grep -Fq 'ref Task<bool> __result' "$ROOT/src/ErasureKill.Patches.cs"
require_method_pattern \
	"$ROOT/src/ErasureKill.Patches.cs" \
	'private static void CheckWinCapturePrefix(' \
	'CombatManager __instance,'
require_method_pattern \
	"$ROOT/src/ErasureKill.Patches.cs" \
	'private static void CheckWinCapturePrefix(' \
	'out CheckWinInvocation __state'
reject_method_pattern \
	"$ROOT/src/ErasureKill.Patches.cs" \
	'private static void CheckWinCapturePrefix(' \
	'object[] __args'
reject_method_pattern \
	"$ROOT/src/ErasureKill.Patches.cs" \
	'private static void CheckWinCapturePrefix(' \
	'ref Task'
reject_method_pattern \
	"$ROOT/src/ErasureKill.Patches.cs" \
	'private static void CheckWinCapturePrefix(' \
	'SettleLedger('
reject_method_pattern \
	"$ROOT/src/ErasureKill.Patches.cs" \
	'private static void CheckWinCapturePrefix(' \
	'ScheduleUncertifiedLineages('
require_method_pattern \
	"$ROOT/src/ErasureKill.Patches.cs" \
	'private static bool AddCommandPrefix(' \
	'__result = Task.CompletedTask;'
require_method_pattern \
	"$ROOT/src/ErasureKill.Patches.cs" \
	'private static bool AddCommandPrefix(' \
	'SettleLineage(binding);'
require_method_pattern \
	"$ROOT/src/ErasureKill.Patches.cs" \
	'private static bool AddCommandPrefix(' \
	'ScheduleRestabilization(binding);'
reject_method_pattern \
	"$ROOT/src/ErasureKill.Patches.cs" \
	'private static bool AddCommandPrefix(' \
	'__result = RestabilizeLineage(binding);'
grep -Fq 'EnumerateRoomCreatureNodes' \
	"$ROOT/src/ErasureKill.Convergence.cs"
grep -Fq 'GetChildrenRecursive<NCreature>()' \
	"$ROOT/src/ErasureKill.Convergence.cs"
grep -Fq 'ReferenceEquals(node.Entity, creature)' \
	"$ROOT/src/ErasureKill.Convergence.cs"
grep -Fq 'ContainsExact(' "$ROOT/src/ErasureKill.Convergence.cs"
grep -Fq 'RemoveExact(' "$ROOT/src/ErasureKill.Convergence.cs"
grep -Fq 'CoordinateCombatCompletion(' \
	"$ROOT/src/ErasureKill.CombatCompletion.cs"
grep -Fq 'CompletionFlight' "$ROOT/src/ErasureKill.CombatCompletion.cs"
reject_fixed_pattern 'ShouldSuppressDeferredContinuation' \
	"${ERASURE_SOURCES[@]}"
reject_fixed_pattern 'ScheduleCompletionCheck' "${ERASURE_SOURCES[@]}"
reject_fixed_pattern 'DeferredContinuationSnapshot' \
	"$ROOT/src/ErasureCompletionPolicy.cs"
grep -Fq 'await InvokeOriginalCombatSettlement(' \
	"$ROOT/src/ErasureKill.CombatCompletion.cs"
grep -Fq 'public bool CompletionArmed { get; set; }' \
	"$ROOT/src/ErasureKill.Tracking.cs"
grep -Fq 'public bool TerminalSealed { get; set; }' \
	"$ROOT/src/ErasureKill.Tracking.cs"
grep -Fq 'public HashSet<Creature> TerminalBaselineEnemies' \
	"$ROOT/src/ErasureKill.Tracking.cs"
grep -Fq 'CommitTerminalCombat(ledger, terminalBaseline);' \
	"$ROOT/src/ErasureKill.CombatCompletion.cs"
grep -Fq 'SweepTerminalIngresses(ledger);' \
	"$ROOT/src/ErasureKill.CombatCompletion.cs"
grep -Fq 'ErasureTerminalIngressPolicy.Evaluate(snapshot)' \
	"$ROOT/src/ErasureKill.TerminalIngress.cs"
grep -Fq 'TryQuarantineTerminalIngress(__instance, __result)' \
	"$ROOT/src/ErasureKill.Patches.cs"
require_method_pattern \
	"$ROOT/src/ErasureKill.Patches.cs" \
	'private static bool AddCommandPrefix(' \
	'__result = CreateTerminalIngressCancellation();'
grep -Fq 'SealTerminalCombat(ledger);' \
	"$ROOT/src/ErasureKill.CombatCompletion.cs"
grep -Fq 'persistence.Commit()' \
	"$ROOT/src/UniversalDominionSwordCard.cs"
grep -Fq 'LineageCompletionCertificate' \
	"$ROOT/src/ErasureLineage.Completion.cs"
grep -Fq 'TryIssueCompletionCertificate(' \
	"$ROOT/src/ErasureLineage.Completion.cs"
grep -Fq 'TryGetCompletionCertificate(' \
	"$ROOT/src/ErasureLineage.Completion.cs"
grep -Fq 'IsCompletionArmed' "$ROOT/src/ErasureCompletionPolicy.cs"
grep -Fq 'AreAllLineagesCertified' "$ROOT/src/ErasureCompletionPolicy.cs"
grep -Fq 'HasLivingUntrackedPrimaryEnemy' \
	"$ROOT/src/ErasureCompletionPolicy.cs"
grep -Fq 'IsBlockedByCombatEndHook' \
	"$ROOT/src/ErasureCompletionPolicy.cs"
grep -Fq 'SingleCreatureTargeting' "$ROOT/src/ErasureTargeting.cs"
grep -Fq 'nameof(Creature.IsHittable)' "$ROOT/src/ErasureTargeting.cs"
grep -Fq 'ErasureTargeting.Install(harmony)' "$ROOT/src/ModEntry.cs"
grep -Fq 'float tick = floor(TIME * 20.0);' "$ROOT/src/AvaritiaCosmicShader.cs"
grep -Fq 'for (int i = 0; i < 16; i++)' "$ROOT/src/AvaritiaCosmicShader.cs"
grep -Fq '* 101.0' "$ROOT/src/AvaritiaCosmicShader.cs"
grep -Fq 'sample_cosmic(' "$ROOT/src/AvaritiaCosmicShader.cs"
grep -Fq 'InspectRelicUpdatePostfix' "$ROOT/src/DynamicRelicIcon.cs"
grep -Fq 'nameof(NEventOptionButton._Ready)' "$ROOT/src/DynamicRelicIcon.cs"
grep -Fq 'EventOptionReadyPostfix' "$ROOT/src/DynamicRelicIcon.cs"
grep -Fq 'GetNode<TextureRect>("%RelicIcon")' "$ROOT/src/DynamicRelicIcon.cs"

"$ROOT/tools/validate_erasure_hooks.sh"
dotnet run \
	--project "$ROOT/tests/ErasureAdversarialTests/ErasureAdversarialTests.csproj" \
	-c Release
dotnet run \
	--project "$ROOT/tests/HarmonyAdversarialTests/HarmonyAdversarialTests.csproj" \
	-c Release

if [[ -f "$ROOT/dist/universal-dominion-sword-variants.manifest" ]]; then
	python3 - "$ROOT/dist" <<'PY'
import hashlib
import json
import pathlib
import sys

root = pathlib.Path(sys.argv[1])
with (root / "universal-dominion-sword-variants.manifest").open(
    encoding="utf-8"
) as handle:
    manifest = json.load(handle)

assert manifest["schema"] == 1
assert [entry["compatTarget"] for entry in manifest["variants"]] == [
    "0.107.1",
    "0.110.0",
]
for entry in manifest["variants"]:
    directory = root / entry["directory"]
    assert directory.resolve().is_relative_to((root / "lib").resolve())
    assert (directory / "compat-target.txt").read_text().strip() == entry["compatTarget"]
    dll = directory / entry["assembly"]
    assert dll.is_file()
    assert hashlib.sha256(dll.read_bytes()).hexdigest() == entry["sha256"]

assert (root / "UniversalDominionSword.dll").is_file()
assert (root / "UniversalDominionSword.json").is_file()
assert (root / "UniversalDominionSword.pck").is_file()
assert not list(root.rglob("*.pdb"))
PY
fi

print "Source and asset checks passed."
