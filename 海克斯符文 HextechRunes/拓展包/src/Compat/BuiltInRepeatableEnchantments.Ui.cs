using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace HextechRunesSponsorPack;

internal static partial class BuiltInRepeatableEnchantments
{
	private static void GetEnchantmentHoverTipsPostfix(EnchantmentModel __instance, ref IEnumerable<IHoverTip> __result)
	{
		if (IsExternalMultiEnchantmentProviderActive() || __instance is not SponsorCompositeEnchantment composite)
		{
			return;
		}

		List<IHoverTip> tips =
		[
			new HoverTip(composite.Title, composite.DynamicDescription, composite.Icon)
		];
		tips.AddRange(composite.InnerEnchantments.SelectMany(static enchantment => enchantment.HoverTips));
		__result = tips;
	}

	private static void GetDescriptionForPilePostfix(CardModel __instance, ref string __result)
	{
		if (!IsExternalMultiEnchantmentProviderActive())
		{
			__result = AppendCompositeExtraText(__result, __instance);
		}
	}

	private static void GetDescriptionForUpgradePreviewPostfix(CardModel __instance, ref string __result)
	{
		if (!IsExternalMultiEnchantmentProviderActive())
		{
			__result = AppendCompositeExtraText(__result, __instance);
		}
	}

	private static bool UpdateEnchantmentVisualsPrefix(NCard __instance)
	{
		if (IsExternalMultiEnchantmentProviderActive())
		{
			return true;
		}

		ClearExtraEnchantmentTabs(__instance);
		if (__instance.Model?.Enchantment is not SponsorCompositeEnchantment composite)
		{
			return true;
		}

		IReadOnlyList<EnchantmentModel> enchantments = composite.InnerEnchantments;
		EnchantmentModel? lead = enchantments.FirstOrDefault();
		Control enchantmentTab = __instance.EnchantmentTab;
		TextureRect enchantmentIcon = (TextureRect)NCardEnchantmentIconField.GetValue(__instance)!;
		MegaLabel enchantmentLabel = (MegaLabel)NCardEnchantmentLabelField.GetValue(__instance)!;
		Vector2 defaultPosition = (Vector2)NCardDefaultEnchantmentPositionField.GetValue(__instance)!;
		Vector2 basePosition = __instance.Model.HasStarCostX || __instance.Model.CurrentStarCost >= 0
			? defaultPosition
			: defaultPosition + Vector2.Up * 45f;
		float tabSpacing = MathF.Max(54f, (enchantmentTab.Size.Y > 0f ? enchantmentTab.Size.Y : 46f) + 6f);

		if (lead != null)
		{
			enchantmentTab.Visible = true;
			ConfigureEnchantmentTab(enchantmentTab, enchantmentIcon, enchantmentLabel, lead);
			enchantmentTab.Position = basePosition;
			for (int i = 1; i < enchantments.Count; i++)
			{
				if (CreateExtraEnchantmentTab(__instance, enchantmentTab, basePosition + Vector2.Down * (tabSpacing * i), enchantments[i], i) == null)
				{
					DebugLog("UI", $"Failed to create extra enchantment tab index={i} for {DescribeCard(__instance.Model)}.");
				}
			}
		}
		else
		{
			DebugLog("UI", "Composite enchantment had no lead enchantment during card refresh.");
			enchantmentTab.Visible = false;
		}

		return false;
	}

	private static bool EnchantPreviewInitPrefix(NEnchantPreview __instance, CardModel card, EnchantmentModel canonicalEnchantment, int amount)
	{
		if (IsExternalMultiEnchantmentProviderActive()
			|| (card.Enchantment is not SponsorCompositeEnchantment && !CanUseBuiltInRepeatableEnchantments(card)))
		{
			return true;
		}

		canonicalEnchantment.AssertCanonical();
		NEnchantPreviewRemoveExistingCardsMethod.Invoke(__instance, null);

		NCard beforeCardNode = NCard.Create(card) ?? throw new InvalidOperationException("Failed to create before-card preview node.");
		NPreviewCardHolder beforeHolder = NPreviewCardHolder.Create(beforeCardNode, showHoverTips: true, scaleOnHover: false)
			?? throw new InvalidOperationException("Failed to create before-card preview holder.");
		NCard beforePreviewCardNode = beforeHolder.CardNode ?? throw new InvalidOperationException("Before-card preview holder did not expose a card node.");
		Control before = (Control)NEnchantPreviewBeforeField.GetValue(__instance)!;
		Control after = (Control)NEnchantPreviewAfterField.GetValue(__instance)!;
		before.AddChildSafely(beforeHolder);
		beforePreviewCardNode.UpdateVisuals(card.Pile?.Type ?? PileType.None, CardPreviewMode.Normal);

		var cardScope = card.CardScope ?? throw new InvalidOperationException("Preview card had no CardScope.");
		CardModel previewCard = cardScope.CloneCard(card);
		previewCard.IsEnchantmentPreview = true;
		EnchantmentModel previewEnchantment = canonicalEnchantment.ToMutable();
		ApplyEnchantmentToCard(previewCard, previewEnchantment, amount, recordHistory: false);
		DebugLog("Preview", $"Built enchant preview card before={DescribeCard(card)} after={DescribeCard(previewCard)}.");

		NCard afterCardNode = NCard.Create(previewCard) ?? throw new InvalidOperationException("Failed to create after-card preview node.");
		NPreviewCardHolder afterHolder = NPreviewCardHolder.Create(afterCardNode, showHoverTips: true, scaleOnHover: false)
			?? throw new InvalidOperationException("Failed to create after-card preview holder.");
		NCard afterPreviewCardNode = afterHolder.CardNode ?? throw new InvalidOperationException("After-card preview holder did not expose a card node.");
		after.AddChildSafely(afterHolder);
		afterPreviewCardNode.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
		return false;
	}

	private static void CardEnchantVfxReadyPostfix(NCardEnchantVfx __instance)
	{
		if (IsExternalMultiEnchantmentProviderActive())
		{
			return;
		}

		CardModel card = (CardModel)NCardEnchantVfxCardModelField.GetValue(__instance)!;
		if (card.Enchantment is not SponsorCompositeEnchantment composite)
		{
			return;
		}

		NCard cardNode = (NCard)NCardEnchantVfxCardNodeField.GetValue(__instance)!;
		TextureRect icon = (TextureRect)NCardEnchantVfxIconField.GetValue(__instance)!;
		MegaLabel label = (MegaLabel)NCardEnchantVfxLabelField.GetValue(__instance)!;
		ClearExtraEnchantmentTabs(cardNode);

		EnchantmentModel? animatedEnchantment = composite.GetLeadEnchantment() ?? composite.InnerEnchantments.LastOrDefault();
		if (animatedEnchantment == null)
		{
			icon.Visible = false;
			label.Visible = false;
			return;
		}

		icon.Texture = animatedEnchantment.Icon;
		icon.Visible = true;
		label.SetTextAutoSize(animatedEnchantment.DisplayAmount.ToString());
		label.Visible = animatedEnchantment.ShowAmount;
		DebugLog("Vfx", $"Adjusted enchant VFX for {DescribeCard(card)} to animate {animatedEnchantment.Id.Entry}.");
	}

	private static string AppendCompositeExtraText(string baseDescription, CardModel card)
	{
		if (card.Enchantment is not SponsorCompositeEnchantment composite)
		{
			return baseDescription;
		}

		List<string> lines = [];
		if (!string.IsNullOrWhiteSpace(baseDescription))
		{
			lines.Add(baseDescription);
		}

		lines.AddRange(composite.GetVisibleExtraCardTextLines());
		return string.Join('\n', lines.Where(static line => !string.IsNullOrWhiteSpace(line)));
	}

	private static Control? CreateExtraEnchantmentTab(NCard cardNode, Control sourceTab, Vector2 position, EnchantmentModel enchantment, int index)
	{
		if (sourceTab.GetParent() is not Node parent)
		{
			return null;
		}

		if (sourceTab.Duplicate() is not Control duplicateTab)
		{
			return null;
		}

		duplicateTab.Name = $"{ExtraEnchantmentTabPrefix}{index}";
		duplicateTab.Material = duplicateTab.Material?.Duplicate() as Material;
		duplicateTab.Position = position;
		parent.AddChildSafely(duplicateTab);

		TextureRect? icon = duplicateTab.GetNodeOrNull<TextureRect>("Icon") ?? duplicateTab.FindChild("Icon", true, false) as TextureRect;
		MegaLabel? label = duplicateTab.GetNodeOrNull<MegaLabel>("Label") ?? duplicateTab.FindChild("Label", true, false) as MegaLabel;
		if (icon == null || label == null)
		{
			DebugLog("UI", $"Extra enchantment tab duplicate is missing Icon/Label nodes for {DescribeCard(cardNode.Model)}.");
			parent.RemoveChildSafely(duplicateTab);
			duplicateTab.QueueFreeSafely();
			return null;
		}

		ConfigureEnchantmentTab(duplicateTab, icon, label, enchantment);
		return duplicateTab;
	}

	private static void ClearExtraEnchantmentTabs(NCard cardNode)
	{
		Node? parent = cardNode.EnchantmentTab.GetParent();
		if (parent == null)
		{
			return;
		}

		foreach (Node child in parent.GetChildren())
		{
			if (!child.Name.ToString().StartsWith(ExtraEnchantmentTabPrefix, StringComparison.Ordinal))
			{
				continue;
			}

			parent.RemoveChildSafely(child);
			child.QueueFreeSafely();
		}
	}

	private static void ConfigureEnchantmentTab(Control tab, TextureRect icon, MegaLabel label, EnchantmentModel enchantment)
	{
		tab.Visible = true;
		icon.Texture = enchantment.Icon;
		label.SetTextAutoSize(enchantment.DisplayAmount.ToString());
		label.Visible = enchantment.ShowAmount;
		ApplyEnchantmentStatus(tab, icon, label, enchantment.Status);
	}

	private static void ApplyEnchantmentStatus(Control tab, TextureRect icon, MegaLabel label, EnchantmentStatus status)
	{
		if (status == EnchantmentStatus.Disabled)
		{
			tab.Modulate = new Color(1f, 1f, 1f, 0.9f);
			if (tab.Material is ShaderMaterial shaderMaterial)
			{
				shaderMaterial.SetShaderParameter(UiTintHue, 0.25);
				shaderMaterial.SetShaderParameter(UiTintSaturation, 0.1);
				shaderMaterial.SetShaderParameter(UiTintValue, 0.6);
			}

			icon.UseParentMaterial = true;
			label.SelfModulate = StsColors.gray;
			return;
		}

		tab.Modulate = Colors.White;
		if (tab.Material is ShaderMaterial shaderMaterial2)
		{
			shaderMaterial2.SetShaderParameter(UiTintHue, 0.25);
			shaderMaterial2.SetShaderParameter(UiTintSaturation, 0.4);
			shaderMaterial2.SetShaderParameter(UiTintValue, 0.6);
		}

		icon.UseParentMaterial = false;
		label.SelfModulate = Colors.White;
	}
}
