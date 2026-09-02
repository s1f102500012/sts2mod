using MegaCrit.Sts2.Core.Models.Exceptions;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HextechRunes;

/// <summary>
/// 升级：自动化——触发自动化效果(每抽 10 张的能量结算)时,额外抽 2 张牌。
/// 原版触发逻辑原样复刻(prefix 替换),仅在触发点追加抽牌;计数器读写走反射
/// (AutomationPower.Data.cardsLeft 为私有嵌套类型)。
/// </summary>
public sealed class AutomationUpgradeRune : CardUpgradeRuneBase<Automation>
{
	private const int TriggerThreshold = 10;

	private static readonly Type AutomationDataType = typeof(AutomationPower).GetNestedType("Data", BindingFlags.NonPublic)
		?? throw new InvalidOperationException("AutomationPower.Data was not found.");
	private static readonly MethodInfo PowerGetInternalDataMethod = typeof(PowerModel)
		.GetMethod("GetInternalData", BindingFlags.Instance | BindingFlags.NonPublic)
		?.MakeGenericMethod(AutomationDataType)
		?? throw new InvalidOperationException("PowerModel.GetInternalData was not found.");
	private static readonly MethodInfo PowerInvokeDisplayAmountChangedMethod = typeof(PowerModel)
		.GetMethod("InvokeDisplayAmountChanged", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("PowerModel.InvokeDisplayAmountChanged was not found.");
	private static readonly FieldInfo AutomationCardsLeftField = AutomationDataType.GetField("cardsLeft", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("AutomationPower.Data.cardsLeft was not found.");
	private static readonly MethodInfo PowerFlashMethod = typeof(PowerModel)
		.GetMethod("Flash", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, Type.EmptyTypes)
		?? throw new InvalidOperationException("PowerModel.Flash was not found.");

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar(2)
	];

	protected override bool IsAvailableForCharacter(Player player)
	{
		return true;
	}

	internal static bool ShouldUseUpgradedDraw(AutomationPower power, CardModel card)
	{
		Player? owner = power.Owner?.Player;
		return owner != null
			&& card.Owner == owner
			&& owner.GetRelic<AutomationUpgradeRune>() != null;
	}

	internal static async Task AfterCardDrawnUpgraded(PlayerChoiceContext choiceContext, AutomationPower power, CardModel card, bool fromHandDraw)
	{
		Player? owner = power.Owner.Player;
		if (owner == null)
		{
			return;
		}

		object data = PowerGetInternalDataMethod.Invoke(power, null)!;
		int cardsLeft = Math.Max(0, (int)AutomationCardsLeftField.GetValue(data)! - 1);
		AutomationCardsLeftField.SetValue(data, cardsLeft);
		PowerInvokeDisplayAmountChangedMethod.Invoke(power, null);
		if (cardsLeft > 0)
		{
			return;
		}

		// 原版触发:回能量并重置计数。
		PowerFlashMethod.Invoke(power, null);
		await PlayerCmd.GainEnergy(power.Amount, owner);
		AutomationCardsLeftField.SetValue(data, TriggerThreshold);
		PowerInvokeDisplayAmountChangedMethod.Invoke(power, null);

		// 符文追加:额外抽 2 张。
		if (owner.Creature.IsDead || owner.Creature.CombatState == null)
		{
			return;
		}

		AutomationUpgradeRune? rune = owner.GetRelic<AutomationUpgradeRune>();
		rune?.Flash();
		await CardPileCmd.Draw(choiceContext, rune?.DynamicVars.Cards.BaseValue ?? 2m, owner, fromHandDraw: false);
	}

	[HarmonyPatch(typeof(AutomationPower), nameof(AutomationPower.AfterCardDrawn), typeof(PlayerChoiceContext), typeof(CardModel), typeof(bool))]
	[HextechPatch("rune.automation", "升级自动化", Rune = typeof(AutomationUpgradeRune))]
	private static class AutomationPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(AutomationPower __instance, PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw, ref Task __result)
		{
			if (!AutomationUpgradeRune.ShouldUseUpgradedDraw(__instance, card))
			{
				return true;
			}

			__result = AutomationUpgradeRune.AfterCardDrawnUpgraded(choiceContext, __instance, card, fromHandDraw);
			return false;
		}
	}
}
