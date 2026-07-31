using MegaCrit.Sts2.Core.Models.CardPools;

namespace HextechRunes;

public abstract class HextechOwnerPoolTokenCard : CardModel
{
	protected HextechOwnerPoolTokenCard(
		int cost,
		CardType type,
		CardRarity rarity,
		TargetType targetType,
		bool shouldShowInCardLibrary)
		: base(cost, type, rarity, targetType, shouldShowInCardLibrary)
	{
	}

	public sealed override CardPoolModel Pool => IsMutable && Owner != null
		? Owner.Character.CardPool
		: ModelDb.CardPool<TokenCardPool>();

	public sealed override CardPoolModel VisualCardPool => Pool;

	public abstract override string PortraitPath { get; }

	public sealed override IEnumerable<string> AllPortraitPaths => [PortraitPath];
}
