using MegaCrit.Sts2.Core.Localization;

namespace HextechRunes;

public sealed class OstyWishCard : HextechOwnerPoolTokenCard
{
	private const string WishBlockDisplayVar = "WishBlock";
	private const string WishDamageDisplayVar = "WishDamage";

	private bool _usePlaceholderDisplayValues;

	public override string PortraitPath => HextechAssets.OstyWishCardPortraitPath;

	public override IEnumerable<CardKeyword> CanonicalKeywords =>
	[
		CardKeyword.Exhaust
	];

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new BlockVar(0m, ValueProp.Move),
		new DamageVar(0m, ValueProp.Move)
	];

	public OstyWishCard()
		: base(0, CardType.Skill, CardRarity.Token, TargetType.AllEnemies, shouldShowInCardLibrary: true)
	{
	}

	internal static CardModel CreatePlaceholderPreview()
	{
		CardModel card = ModelDb.Card<OstyWishCard>().ToMutable();
		if (card is OstyWishCard wish)
		{
			wish._usePlaceholderDisplayValues = true;
		}

		return card;
	}

	internal static void SetWishAmount(CardModel card, decimal amount)
	{
		if (card is not OstyWishCard wish)
		{
			return;
		}

		decimal value = Math.Max(0m, amount);
		wish.DynamicVars.Block.BaseValue = value;
		wish.DynamicVars.Damage.BaseValue = value;
		wish._usePlaceholderDisplayValues = false;
	}

	protected override void AddExtraArgsToDescription(LocString description)
	{
		if (_usePlaceholderDisplayValues)
		{
			description.Add(WishBlockDisplayVar, "[blue]X[/blue]");
			description.Add(WishDamageDisplayVar, "[blue]X[/blue]");
			return;
		}

		description.Add(WishBlockDisplayVar, DynamicVars.Block.ToHighlightedString(inverse: false));
		description.Add(WishDamageDisplayVar, DynamicVars.Damage.ToHighlightedString(inverse: false));
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (Owner?.Creature.CombatState == null)
		{
			return;
		}

		await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

		IReadOnlyList<Creature> enemies = Owner.Creature.CombatState.HittableEnemies.ToList();
		foreach (Creature enemy in enemies)
		{
			await HextechGameApiCompat.Damage(choiceContext, enemy, DynamicVars.Damage, Owner.Creature, this);
		}
	}

	protected override void OnUpgrade()
	{
		AddKeyword(CardKeyword.Retain);
	}
}
