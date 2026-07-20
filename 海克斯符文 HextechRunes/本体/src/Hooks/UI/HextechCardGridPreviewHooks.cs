using System.Collections;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

/// <summary>
/// 牌组界面"查看升级"取消勾选的兜底还原:原版 NCardGrid 的还原链路没有为
/// "已升级但仍可继续升级"的卡(升级:打击/防御的无限升级)设计过——原版不存在
/// MaxUpgradeLevel&gt;1 的卡(已扫全部41个覆写),取消勾选后部分格子停留在 +1 预览
/// (玩家实报)。这里在开关切到 false 时强制把每个格子还原到基础卡并清预览旗标,
/// 纯表现层,不触碰任何卡牌数据。
/// </summary>
internal static class HextechCardGridPreviewHooks
{
	private static readonly FieldInfo? CardRowsField = typeof(NCardGrid).GetField("_cardRows", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly FieldInfo? PreviewFlagField = typeof(NGridCardHolder).GetField("_isPreviewingUpgrade", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly FieldInfo? BaseCardField = typeof(NGridCardHolder).GetField("_baseCard", BindingFlags.Instance | BindingFlags.NonPublic);

	public static void Install(Harmony harmony)
	{
		if (CardRowsField == null || PreviewFlagField == null || BaseCardField == null)
		{
			throw new MissingFieldException("NCardGrid/NGridCardHolder preview fields not found in this runtime.");
		}

		harmony.Patch(
			RequireMethod(typeof(NCardGrid), "set_IsShowingUpgrades", BindingFlags.Instance | BindingFlags.Public, typeof(bool)),
			postfix: new HarmonyMethod(typeof(HextechCardGridPreviewHooks), nameof(SetIsShowingUpgradesPostfix)));
	}

	private static void SetIsShowingUpgradesPostfix(NCardGrid __instance, bool value)
	{
		if (value || CardRowsField!.GetValue(__instance) is not IEnumerable rows)
		{
			return;
		}

		foreach (object? rowObj in rows)
		{
			if (rowObj is not IEnumerable row)
			{
				continue;
			}

			foreach (NGridCardHolder holder in row.OfType<NGridCardHolder>())
			{
				if (PreviewFlagField!.GetValue(holder) is not true
					|| BaseCardField!.GetValue(holder) is not CardModel baseCard
					|| holder.CardNode == null)
				{
					continue;
				}

				holder.CardNode.Model = baseCard;
				holder.CardNode.UpdateVisuals(holder.CardNode.DisplayingPile, CardPreviewMode.Normal);
				PreviewFlagField.SetValue(holder, false);
			}
		}
	}
}
