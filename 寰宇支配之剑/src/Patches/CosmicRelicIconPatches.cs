using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.InspectScreens;

namespace UniversalDominionSword.Patches;

/// <summary>
/// 遗物图标的星空材质只在三个纯表现节点上后缀:顶栏/奖励遗物节点、遗物检视大图、涅奥事件选项按钮。
/// 静态贴图本身走 <c>PackedIconPath</c> / <c>BigIconPath</c> 虚属性,不碰任何取图 getter。
/// </summary>
[SwordPatch("visual.relic-node", "遗物图标星空材质(遗物节点)", Optional = true)]
[HarmonyPatch(typeof(NRelic), "Reload")]
internal static class RelicNodeReloadPatch
{
	[HarmonyPrepare]
	private static bool Prepare() => VanillaMembers.NRelicModel != null;

	[HarmonyPostfix]
	private static void Postfix(NRelic __instance)
	{
		if (!__instance.IsNodeReady()
			|| VanillaMembers.NRelicModel!.GetValue(__instance) is not UniversalDominionSwordRelic)
		{
			return;
		}

		if (CosmicMaterial.TryApply(__instance.Icon))
		{
			__instance.Outline.Visible = false;
		}
	}
}

[SwordPatch("visual.relic-inspect", "遗物图标星空材质(检视界面)", Optional = true)]
[HarmonyPatch(typeof(NInspectRelicScreen), "UpdateRelicDisplay")]
internal static class RelicInspectDisplayPatch
{
	// 检视界面翻页复用同一张 TextureRect:记住原材质,翻到别的遗物时还回去。
	private static readonly Dictionary<ulong, Material?> OriginalMaterials = new();

	[HarmonyPrepare]
	private static bool Prepare() =>
		VanillaMembers.InspectRelics != null
		&& VanillaMembers.InspectIndex != null
		&& VanillaMembers.InspectImage != null;

	[HarmonyPostfix]
	private static void Postfix(NInspectRelicScreen __instance)
	{
		if (VanillaMembers.InspectImage!.GetValue(__instance) is not TextureRect image
			|| VanillaMembers.InspectRelics!.GetValue(__instance) is not IReadOnlyList<RelicModel> relics
			|| VanillaMembers.InspectIndex!.GetValue(__instance) is not int index
			|| index < 0
			|| index >= relics.Count)
		{
			return;
		}

		ulong imageId = image.GetInstanceId();
		OriginalMaterials.TryAdd(imageId, image.Material);
		if (relics[index] is not UniversalDominionSwordRelic)
		{
			if (CosmicMaterial.IsApplied(image))
			{
				image.Material = OriginalMaterials[imageId];
				image.TextureFilter = CanvasItem.TextureFilterEnum.ParentNode;
			}

			return;
		}

		CosmicMaterial.TryApply(image);
	}
}

[SwordPatch("visual.relic-event-option", "遗物图标星空材质(涅奥选项按钮)", Optional = true)]
[HarmonyPatch(typeof(NEventOptionButton), nameof(NEventOptionButton._Ready))]
internal static class RelicEventOptionReadyPatch
{
	[HarmonyPostfix]
	private static void Postfix(NEventOptionButton __instance)
	{
		if (__instance.Option?.Relic is not UniversalDominionSwordRelic)
		{
			return;
		}

		TextureRect? icon = __instance.GetNodeOrNull<TextureRect>("%RelicIcon");
		if (icon == null || !CosmicMaterial.TryApply(icon))
		{
			return;
		}

		TextureRect? outline = icon.GetNodeOrNull<TextureRect>("%Outline");
		if (outline != null)
		{
			outline.Visible = false;
		}
	}
}
