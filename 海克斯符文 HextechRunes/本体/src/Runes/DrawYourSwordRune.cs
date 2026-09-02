using HarmonyLib;
namespace HextechRunes;

public sealed class DrawYourSwordRune : AttributeConversionRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<FocusPower>(2m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.Static(StaticHoverTip.Evoke),
		HoverTipFactory.FromPower<StrengthPower>(),
		HoverTipFactory.FromPower<DexterityPower>(),
		HoverTipFactory.FromPower<FocusPower>()
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsDefectPlayer(player);
	}

	internal bool ShouldReplaceOrbEvoke(OrbModel orb)
	{
		return Owner != null
			&& !Owner.Creature.IsDead
			&& IsDefectOwner
			&& ReferenceEquals(orb.Owner, Owner)
			&& ReferenceEquals(Owner.GetRelic<DrawYourSwordRune>(), this);
	}

	internal async Task<IEnumerable<Creature>> ReplaceOrbEvoke()
	{
		Flash();
		await PowerCmd.Apply<FocusPower>(
			Owner.Creature,
			DynamicVars["FocusPower"].BaseValue,
			Owner.Creature,
			null);
		return Array.Empty<Creature>();
	}

	protected override bool ShouldConvert(PowerModel canonicalPower)
	{
		return IsDefectOwner && !HasConflictingFocusConverter && canonicalPower is FocusPower;
	}

	protected override bool ShouldConvertAppliedPower(PowerModel power)
	{
		return IsDefectOwner && !HasConflictingFocusConverter && power is FocusPower;
	}

	protected override async Task ApplyConvertedPower(decimal amount, Creature? applier, CardModel? cardSource)
	{
		await PowerCmd.Apply<StrengthPower>(Owner!.Creature, amount, applier, cardSource);
		await PowerCmd.Apply<DexterityPower>(Owner.Creature, amount, applier, cardSource);
	}

	protected override Task RevertOriginalPower(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		return PowerCmd.Apply<FocusPower>(Owner!.Creature, -amount, applier, cardSource);
	}

	private bool HasConflictingFocusConverter => Owner?.GetRelic<DexterityStrengthToFocusRune>() != null;

	[HextechPatch("rune.draw-your-sword.evoke", "亮出你的剑", Rune = typeof(DrawYourSwordRune))]
	private static class DrawYourSwordEvokePatch
	{
		private static void Apply(Harmony harmony)
		{
			Assembly coreAssembly = typeof(OrbModel).Assembly;
			HarmonyMethod prefix = new(typeof(HextechPlayerRuneHooks), nameof(HextechPlayerRuneHooks.OrbEvokePrefix))
			{
				priority = Priority.First
			};

			foreach (MethodInfo method in HextechPlayerRuneHooks.FindLoadedOrbEvokeMethods())
			{
				try
				{
					harmony.Patch(method, prefix: prefix);
				}
				catch (Exception ex) when (method.DeclaringType?.Assembly != coreAssembly)
				{
					Log.Warn($"[{ModInfo.Id}][Compat] Could not replace evoke for external Orb {method.DeclaringType?.FullName}: {ex.Message}");
				}
			}
		}
	}
}
