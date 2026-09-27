using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Models.Enchantments;

namespace HextechRunes;

public abstract class EnchantmentForgeBase<TEnchantment> : HextechForgeBase
	where TEnchantment : EnchantmentModel
{
	protected virtual int EnchantmentAmount => 1;

	// 选牌张数与附魔层数解耦:MomentumForge 要「选 1 张牌附魔动量3」,层数=3 但只选 1 张。
	protected virtual int EnchantmentCardCount => 1;

	public override bool HasUponPickupEffect => true;

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		.. HoverTipFactory.FromEnchantment<TEnchantment>(EnchantmentAmount)
	];

	// 泛型模型类的钩子只转发,不直接用类型参数(第三方批量补丁会把它写死,见 AutoPlayFormsAtCombatStartRuneBase)。
	public override Task AfterObtained()
	{
		return EnchantSelectedCardsAsync();
	}

	private async Task EnchantSelectedCardsAsync()
	{
		if (Owner == null)
		{
			return;
		}

		EnchantmentModel canonicalEnchantment = ModelDb.Enchantment<TEnchantment>();
		// FromDeckForEnchantment 的第三参是选牌界面的"附魔层数预览"(仅展示),不是选牌张数——
		// 误传张数(1)会让动量锻造器在选牌屏显示"动量1"(玩家实报);选牌张数由 prefs 第二参控制,
		// 实际附魔层数由下方 Enchant 决定(动量=3,与预览现已一致)。
		IEnumerable<CardModel> selectedCards = await CardSelectCmd.FromDeckForEnchantment(
			Owner,
			canonicalEnchantment,
			EnchantmentAmount,
			new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, EnchantmentCardCount));
		foreach (CardModel selected in selectedCards)
		{
			Flash();
			CardCmd.Enchant(canonicalEnchantment.ToMutable(), selected, EnchantmentAmount);
			CardCmd.Preview(selected);
		}
	}
}

public sealed class GlamForge : EnchantmentForgeBase<Glam>
{
}

public sealed class SwiftForge : EnchantmentForgeBase<Swift>
{
	protected override int EnchantmentAmount => 2;
}

public sealed class SoulsPowerForge : EnchantmentForgeBase<SoulsPower>
{
}

public sealed class MomentumForge : EnchantmentForgeBase<Momentum>
{
	protected override int EnchantmentAmount => 3;
}

public sealed class EmbersForge : EnchantmentForgeBase<TezcatarasEmber>
{
}

public sealed class SpiralForge : EnchantmentForgeBase<UniversalSpiral>
{
}
