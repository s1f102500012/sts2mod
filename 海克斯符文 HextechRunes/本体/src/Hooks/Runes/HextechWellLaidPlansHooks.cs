using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

// 已退役海克斯「升级：计划妥当」的旧存档兼容实现。
// 效果恒为「回合结束时选择任意张手牌保留,未选中的正常弃掉」。
// 0.108-:原版 power 的 BeforeFlushLate 自带选牌(上限 Amount),prefix 整体替换为无上限版本。
// 0.109+:原版计划妥当重做成「整手牌全保留」(power 只剩 ShouldFlush 返回 false),不再有选牌;
//   改为 postfix 把 ShouldFlush 拉回 true 恢复正常弃牌流程,选牌搬到 rune 自身的 BeforeFlushLate
//   (AbstractModel 挂点仍在,见 WellLaidPlansUpgradeRune.BeforeFlushLate)。
internal static class HextechWellLaidPlansHooks
{
	public static void Install(Harmony harmony)
	{
#if STS2_109_OR_NEWER
		harmony.Patch(
			RequireMethod(typeof(WellLaidPlansPower), nameof(WellLaidPlansPower.ShouldFlush), BindingFlags.Instance | BindingFlags.Public, typeof(Player)),
			postfix: new HarmonyMethod(typeof(HextechWellLaidPlansHooks), nameof(ShouldFlushPostfix)));
#else
		harmony.Patch(
			RequireMethod(typeof(WellLaidPlansPower), nameof(WellLaidPlansPower.BeforeFlushLate), BindingFlags.Instance | BindingFlags.Public, typeof(PlayerChoiceContext), typeof(Player)),
			prefix: new HarmonyMethod(typeof(HextechWellLaidPlansHooks), nameof(BeforeFlushLatePrefix)));
#endif
		HextechLog.Info($"[{ModInfo.Id}][WellLaidPlans] Unlimited retain hook installed.");
	}

#if STS2_109_OR_NEWER
	private static void ShouldFlushPostfix(WellLaidPlansPower __instance, Player player, ref bool __result)
	{
		// 原版 0.109 对持有者返回 false(整手牌全保留);有 rune 时恢复弃牌流程,保留权交给选牌。
		if (!__result && __instance.Owner?.Player == player && player?.GetRelic<WellLaidPlansUpgradeRune>() != null)
		{
			__result = true;
		}
	}
#else
	private static bool BeforeFlushLatePrefix(WellLaidPlansPower __instance, PlayerChoiceContext choiceContext, Player player, ref Task __result)
	{
		if (__instance.Owner?.Player == player && player?.GetRelic<WellLaidPlansUpgradeRune>() != null)
		{
			__result = UnlimitedRetain(__instance, choiceContext, player);
			return false;
		}

		return true;
	}
#endif

	internal static async Task UnlimitedRetain(WellLaidPlansPower power, PlayerChoiceContext choiceContext, Player player)
	{
		if (!Hook.ShouldFlush(player.Creature.CombatState, player))
		{
			return;
		}

		int handCount = PileType.Hand.GetPile(player).Cards.Count();
		if (handCount <= 0)
		{
			return;
		}

		// SelectionScreenPrompt 在 PowerModel 上是 protected,用 Traverse 反射读取原版「保留」提示文案;
		// 0.109 起 power 侧不再有选牌,基属性可能为空,兜底用计划妥当卡牌自身的选择提示,防 NRE 断链。
		LocString? prompt = Traverse.Create(power).Property("SelectionScreenPrompt").GetValue<LocString>();
		prompt ??= Traverse.Create(ModelDb.Card<WellLaidPlans>()).Property("SelectionScreenPrompt").GetValue<LocString>();
		prompt ??= new LocString("relics", "WELL_LAID_PLANS_UPGRADE_RUNE.title");
		List<CardModel> selected = (await CardSelectCmd.FromHand(
			prefs: new CardSelectorPrefs(prompt, 0, handCount),
			context: choiceContext,
			player: player,
			filter: static card => !card.ShouldRetainThisTurn,
			source: power)).ToList();
		foreach (CardModel card in selected)
		{
			card.GiveSingleTurnRetain();
		}
	}
}
