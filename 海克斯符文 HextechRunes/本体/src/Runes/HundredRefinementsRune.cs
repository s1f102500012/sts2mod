using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Rewards;

namespace HextechRunes;

public sealed class HundredRefinementsRune : HextechRelicBase
{
	private const string BodyForgeOptionId = "HEXTECH_BODY_FORGE";
	private int _bodyForgeCount;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedBodyForgeCount
	{
		get => _bodyForgeCount;
		set
		{
			_bodyForgeCount = Math.Max(0, value);
			InvokeDisplayAmountChanged();
		}
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("BodyForges", 2m),
		new DynamicVar("ForgeCount", 1m)
	];

	public override bool ShowCounter => true;

	public override int DisplayAmount => !IsCanonical ? _bodyForgeCount % BodyForgeThreshold : 0;

	public override bool TryModifyCardRewardAlternatives(Player player, CardReward cardReward, List<CardRewardAlternative> alternatives)
	{
		if (player != Owner || Owner == null)
		{
			return false;
		}

		// 多个「百炼成钢」共用一个锻体按钮:仅由第一个实例添加,点击时同步推进所有实例的计数
		// (与佩尔之翼把多个牺牲按钮合并成一个同理),而不是每个海克斯各一个按钮、各自计数。
		if (GetActiveRunes(Owner).FirstOrDefault() != this)
		{
			return false;
		}

		alternatives.Add(new CardRewardAlternative(
			BodyForgeOptionId,
			ForgeBodyForAllInstances,
			PostAlternateCardRewardAction.EndSelectionAndCompleteReward));
		return true;
	}

	private async Task ForgeBodyForAllInstances()
	{
		if (Owner == null)
		{
			return;
		}

		foreach (HundredRefinementsRune rune in GetActiveRunes(Owner))
		{
			await rune.ForgeBodyOnce();
		}
	}

	private async Task ForgeBodyOnce()
	{
		if (Owner == null)
		{
			return;
		}

		_bodyForgeCount++;
		InvokeDisplayAmountChanged();
		Flash();
		if (_bodyForgeCount % BodyForgeThreshold != 0)
		{
			return;
		}

		await HextechForgeGrantHelper.ObtainRandomForges(Owner, Math.Max(0, DynamicVars["ForgeCount"].IntValue));
	}

	private static IReadOnlyList<HundredRefinementsRune> GetActiveRunes(Player player)
	{
		return player.Relics.OfType<HundredRefinementsRune>().Where(static rune => rune.Owner != null).ToList();
	}

	private int BodyForgeThreshold => Math.Max(1, DynamicVars["BodyForges"].IntValue);
}
