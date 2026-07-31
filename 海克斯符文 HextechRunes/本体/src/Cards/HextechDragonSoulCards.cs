namespace HextechRunes;

public abstract class DragonSoulCardBase : HextechOwnerPoolTokenCard
{
	protected DragonSoulCardBase(int cost)
		: base(cost, CardType.Power, CardRarity.Token, TargetType.Self, shouldShowInCardLibrary: true)
	{
	}

	protected override void OnUpgrade()
	{
		AddKeyword(CardKeyword.Innate);
	}
}

public sealed class OceanDragonSoulCard : DragonSoulCardBase
{
	public override string PortraitPath => HextechAssets.OceanDragonSoulCardPortraitPath;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new HealVar(3m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<HextechOceanDragonSoulPower>()
	];

	public OceanDragonSoulCard()
		: base(0)
	{
	}

	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		return PowerCmd.Apply<HextechOceanDragonSoulPower>(Owner.Creature, DynamicVars.Heal.BaseValue, Owner.Creature, this);
	}
}

public sealed class InfernalDragonSoulCard : DragonSoulCardBase
{
	public override string PortraitPath => HextechAssets.InfernalDragonSoulCardPortraitPath;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("BurnPower", 6m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<HextechInfernalDragonSoulPower>(),
		HoverTipFactory.FromPower<HextechBurnPower>()
	];

	public InfernalDragonSoulCard()
		: base(0)
	{
	}

	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		return PowerCmd.Apply<HextechInfernalDragonSoulPower>(Owner.Creature, DynamicVars["BurnPower"].BaseValue, Owner.Creature, this);
	}
}

public sealed class HextechDragonSoulCard : DragonSoulCardBase
{
	public override string PortraitPath => HextechAssets.HextechDragonSoulCardPortraitPath;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new EnergyVar(1)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<HextechDragonSoulPower>()
	];

	public HextechDragonSoulCard()
		: base(0)
	{
	}

	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		return PowerCmd.Apply<HextechDragonSoulPower>(Owner.Creature, DynamicVars.Energy.BaseValue, Owner.Creature, this);
	}
}

public sealed class MountainDragonSoulCard : DragonSoulCardBase
{
	public override string PortraitPath => HextechAssets.MountainDragonSoulCardPortraitPath;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<PlatingPower>(2m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<HextechMountainDragonSoulPower>(),
		HoverTipFactory.FromPower<PlatingPower>()
	];

	public MountainDragonSoulCard()
		: base(0)
	{
	}

	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		return PowerCmd.Apply<HextechMountainDragonSoulPower>(Owner.Creature, DynamicVars["PlatingPower"].BaseValue, Owner.Creature, this);
	}
}

public sealed class ChemtechDragonSoulCard : DragonSoulCardBase
{
	public override string PortraitPath => HextechAssets.ChemtechDragonSoulCardPortraitPath;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("PotionCount", 1m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<HextechChemtechDragonSoulPower>()
	];

	public ChemtechDragonSoulCard()
		: base(1)
	{
	}

	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		return PowerCmd.Apply<HextechChemtechDragonSoulPower>(Owner.Creature, DynamicVars["PotionCount"].BaseValue, Owner.Creature, this);
	}
}

public sealed class CloudDragonSoulCard : DragonSoulCardBase
{
	public override string PortraitPath => HextechAssets.CloudDragonSoulCardPortraitPath;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar(2)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<HextechCloudDragonSoulPower>()
	];

	public CloudDragonSoulCard()
		: base(0)
	{
	}

	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		return PowerCmd.Apply<HextechCloudDragonSoulPower>(Owner.Creature, DynamicVars.Cards.BaseValue, Owner.Creature, this);
	}
}
