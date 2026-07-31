using MegaCrit.Sts2.Core.Entities.Orbs;
using MegaCrit.Sts2.Core.Nodes.Orbs;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HextechRunes;

public sealed class DrawYourSwordRune : AttributeConversionRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<FocusPower>(1m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromOrb<LightningOrb>(),
		HoverTipFactory.FromPower<StrengthPower>(),
		HoverTipFactory.FromPower<DexterityPower>(),
		HoverTipFactory.FromPower<FocusPower>()
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsDefectPlayer(player);
	}

	public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, HextechCombatState combatState)
	{
		if (side != CombatSide.Enemy
			|| Owner == null
			|| !IsDefectOwner
			|| Owner.Creature.IsDead
			|| Owner.Creature.CombatState != combatState
			|| CombatManager.Instance?.IsOverOrEnding == true
			|| Owner.PlayerCombatState == null)
		{
			return;
		}

		OrbQueue orbQueue = Owner.PlayerCombatState.OrbQueue;
		List<OrbModel> orbs = orbQueue.Orbs.ToList();
		if (orbs.Count == 0)
		{
			return;
		}

		NOrbManager? orbManager = NCombatRoom.Instance?.GetCreatureNode(Owner.Creature)?.OrbManager;
		int removedCount = 0;
		foreach (OrbModel orb in orbs)
		{
			if (!orbQueue.Remove(orb))
			{
				continue;
			}

			orb.RemoveInternal();
			removedCount++;
			try
			{
				orbManager?.EvokeOrbAnim(orb);
			}
			catch (Exception ex)
			{
				Log.Warn($"[{ModInfo.Id}][DrawYourSword] Orb removal animation failed: {ex.Message}");
			}
		}

		if (removedCount == 0)
		{
			return;
		}

		Flash();
		await PowerCmd.Apply<FocusPower>(
			Owner.Creature,
			removedCount * DynamicVars["FocusPower"].BaseValue,
			Owner.Creature,
			null);
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
}
