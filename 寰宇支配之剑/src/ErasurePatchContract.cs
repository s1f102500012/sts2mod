using System.Reflection;

[assembly: AssemblyMetadata(
	"Erasure.AuditContractVersion",
	"1")]
[assembly: AssemblyMetadata(
	"Erasure.Scope",
	"Selected creature and causally certified continuation lineage in the same combat only")]
[assembly: AssemblyMetadata(
	"Erasure.Interoperability",
	"Does not enumerate, unpatch, disable, rename, or reprioritize third-party Harmony patches")]
[assembly: AssemblyMetadata(
	"Erasure.Identity",
	"Exact object identity or transaction-bound causal evidence; model type and slot alone are insufficient")]
[assembly: AssemblyMetadata(
	"Erasure.FailureMode",
	"Required member or IL-shape mismatch aborts patch initialization")]
[assembly: AssemblyMetadata(
	"Erasure.KnownRisk",
	"Version-specific private members and async-state-machine IL require validation for every supported game update")]

#if STS2_107_1
[assembly: AssemblyMetadata("Erasure.TargetGameVersion", "0.107.1")]
#elif STS2_110_0
[assembly: AssemblyMetadata("Erasure.TargetGameVersion", "0.110.0")]
#endif

namespace UniversalDominionSword;

[AttributeUsage(
	AttributeTargets.Class | AttributeTargets.Method,
	AllowMultiple = true,
	Inherited = false)]
public sealed class ErasureBoundaryAttribute(
	string scope,
	string enforcement) : Attribute
{
	public string Scope { get; } = scope;

	public string Enforcement { get; } = enforcement;
}

public static class ErasurePatchContract
{
	public const string SelectedLineageScope =
		"Selected creature plus causally certified direct continuation lineage " +
		"in the same combat.";

	public const string ThirdPartyInteroperability =
		"Never enumerate, unpatch, disable, rename, or reprioritize " +
		"third-party Harmony patches.";

	public const string IdentityAdmission =
		"Admission requires exact object identity or transaction-bound causal " +
		"evidence; model type and slot alone are insufficient.";

	public const string CanonicalFirst =
		"Canonical death, removal, and settlement primitives run before the " +
		"exact-object convergence fallback.";

	public const string FailClosedCompatibility =
		"Required member or IL-shape mismatch aborts patch initialization.";

	public const string KnownCompatibilityRisk =
		"Version-specific private members and async-state-machine IL require " +
		"validation for every supported game update.";

	public const string ValidationBoundary =
		"Compilation, static hook validation, and headless initialization are " +
		"not gameplay or multiplayer proof.";

	public const string RuntimeSummary =
		"scope=selected-lineage-only; third-party-unpatch=none; " +
		"explicit-priority-override=none; compatibility=fail-closed";

	private static readonly string[] InvariantValues =
	[
		SelectedLineageScope,
		ThirdPartyInteroperability,
		IdentityAdmission,
		CanonicalFirst,
		FailClosedCompatibility,
		KnownCompatibilityRisk,
		ValidationBoundary
	];

	public static IReadOnlyList<string> Invariants => InvariantValues;
}
