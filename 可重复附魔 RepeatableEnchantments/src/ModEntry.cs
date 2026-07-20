using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.addons.mega_text;
using Godot;

namespace RepeatableEnchantments;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
	private const string LogPrefix = "[RepeatableEnchantments]";
	private const string HarmonyId = "Natsuki.RepeatableEnchantments";
	private const string TargetGameVersion = "0.107.1";

	private static readonly HashSet<Type> LayeredEnchantmentTypes = new()
	{
		typeof(Adroit),
		typeof(Goopy),
		typeof(Momentum),
		typeof(Nimble),
		typeof(Sharp),
		typeof(Sown),
		typeof(Swift),
		typeof(Vigorous)
	};

	private static readonly HashSet<Type> RefreshConsumedStackTypes = new()
	{
		typeof(Sown),
		typeof(Swift),
		typeof(Vigorous)
	};
	private static readonly StringName UiTintHue = new("h");
	private static readonly StringName UiTintSaturation = new("s");
	private static readonly StringName UiTintValue = new("v");
	private const string ExtraEnchantmentTabPrefix = "RepeatableEnchantmentsExtraTab";

	private static Harmony? _harmony;
	private static bool _initialized;

	private static readonly FieldInfo NCardEnchantmentIconField = RequireField(typeof(NCard), "_enchantmentIcon");
	private static readonly FieldInfo NCardEnchantmentLabelField = RequireField(typeof(NCard), "_enchantmentLabel");
	private static readonly FieldInfo NCardDefaultEnchantmentPositionField = RequireField(typeof(NCard), "_defaultEnchantmentPosition");
	private static readonly FieldInfo NEnchantPreviewBeforeField = RequireField(typeof(NEnchantPreview), "_before");
	private static readonly FieldInfo NEnchantPreviewAfterField = RequireField(typeof(NEnchantPreview), "_after");
	private static readonly MethodInfo NEnchantPreviewRemoveExistingCardsMethod = RequireMethod(typeof(NEnchantPreview), "RemoveExistingCards", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly FieldInfo NCardEnchantVfxCardNodeField = RequireField(typeof(NCardEnchantVfx), "_cardNode");
	private static readonly FieldInfo NCardEnchantVfxIconField = RequireField(typeof(NCardEnchantVfx), "_enchantmentIcon");
	private static readonly FieldInfo NCardEnchantVfxLabelField = RequireField(typeof(NCardEnchantVfx), "_enchantmentLabel");
	private static readonly FieldInfo NCardEnchantVfxCardModelField = RequireField(typeof(NCardEnchantVfx), "_cardModel");
	private static readonly PropertyInfo RestSiteOptionOwnerProperty = RequireProperty(
		typeof(RestSiteOption),
		"Owner",
		BindingFlags.Instance | BindingFlags.NonPublic);

	public static void Initialize()
	{
		if (_initialized)
		{
			Log.Info($"{LogPrefix} Initialize skipped; hooks are already installed.");
			return;
		}

		_initialized = true;
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(RepeatableCompositeEnchantment));
		PatchThievingHopperStealPriorities();
		Harmony harmony = _harmony ??= new Harmony(HarmonyId);
		InstallHooks(harmony);
		Log.Info($"{LogPrefix} Loaded for Slay the Spire 2 {TargetGameVersion}. Composite model id={ModelDb.GetId<RepeatableCompositeEnchantment>().Entry}.");
	}

	private static void InstallHooks(Harmony harmony)
	{
		harmony.Patch(
			RequireMethod(typeof(EnchantmentModel), nameof(EnchantmentModel.CanEnchant), BindingFlags.Instance | BindingFlags.Public, typeof(CardModel)),
			prefix: new HarmonyMethod(typeof(ModEntry), nameof(CanEnchantPrefix)));
		harmony.Patch(
			RequireMethod(typeof(MegaCrit.Sts2.Core.Commands.CardCmd), nameof(MegaCrit.Sts2.Core.Commands.CardCmd.Enchant), BindingFlags.Static | BindingFlags.Public, typeof(EnchantmentModel), typeof(CardModel), typeof(decimal)),
			prefix: new HarmonyMethod(typeof(ModEntry), nameof(EnchantPrefix)));
		harmony.Patch(
			RequireMethod(typeof(EnchantmentModel), "get_HoverTips", BindingFlags.Instance | BindingFlags.Public),
			postfix: new HarmonyMethod(typeof(ModEntry), nameof(GetEnchantmentHoverTipsPostfix)));
		harmony.Patch(
			RequireMethod(typeof(CardModel), nameof(CardModel.GetDescriptionForPile), BindingFlags.Instance | BindingFlags.Public, typeof(PileType), typeof(Creature)),
			postfix: new HarmonyMethod(typeof(ModEntry), nameof(GetDescriptionForPilePostfix)));
		harmony.Patch(
			RequireMethod(typeof(CardModel), nameof(CardModel.GetDescriptionForUpgradePreview), BindingFlags.Instance | BindingFlags.Public),
			postfix: new HarmonyMethod(typeof(ModEntry), nameof(GetDescriptionForUpgradePreviewPostfix)));
		harmony.Patch(
			RequireMethod(typeof(NCard), "UpdateEnchantmentVisuals", BindingFlags.Instance | BindingFlags.NonPublic),
			prefix: new HarmonyMethod(typeof(ModEntry), nameof(UpdateEnchantmentVisualsPrefix)));
		harmony.Patch(
			RequireMethod(typeof(NEnchantPreview), nameof(NEnchantPreview.Init), BindingFlags.Instance | BindingFlags.Public, typeof(CardModel), typeof(EnchantmentModel), typeof(int)),
			prefix: new HarmonyMethod(typeof(ModEntry), nameof(EnchantPreviewInitPrefix)));
		harmony.Patch(
			RequireMethod(typeof(NCardEnchantVfx), nameof(NCardEnchantVfx._Ready), BindingFlags.Instance | BindingFlags.Public),
			postfix: new HarmonyMethod(typeof(ModEntry), nameof(CardEnchantVfxReadyPostfix)));
		harmony.Patch(
			RequireMethod(typeof(CloneRestSiteOption), nameof(CloneRestSiteOption.OnSelect), BindingFlags.Instance | BindingFlags.Public),
			prefix: new HarmonyMethod(typeof(ModEntry), nameof(CloneRestSiteOnSelectPrefix)));
	}

	private static void PatchThievingHopperStealPriorities()
	{
		FieldInfo field = RequireField(typeof(ThievingHopper), "_stealPriorities");
		if (field.GetValue(null) is not Func<CardModel, bool>[] priorities || priorities.Length != 4)
		{
			throw new InvalidOperationException("Could not read ThievingHopper steal priorities.");
		}

		priorities[0] = static card => !HasEnchantmentType(card, typeof(Imbued)) && card.Rarity == CardRarity.Uncommon;
		priorities[1] = static card => !HasEnchantmentType(card, typeof(Imbued)) && card.Rarity is CardRarity.Common or CardRarity.Rare or CardRarity.Event;
		priorities[2] = static card => !HasEnchantmentType(card, typeof(Imbued)) && card.Rarity is CardRarity.Basic or CardRarity.Quest;
		priorities[3] = static card => card.Rarity == CardRarity.Ancient || HasEnchantmentType(card, typeof(Imbued));
	}

	private static bool CanEnchantPrefix(EnchantmentModel __instance, CardModel card, ref bool __result)
	{
		if (__instance is RepeatableCompositeEnchantment)
		{
			__result = false;
			return false;
		}

		CardType type = card.Type;
		if ((uint)(type - 4) <= 2u)
		{
			__result = false;
			return false;
		}

		if (!__instance.CanEnchantCardType(card.Type))
		{
			__result = false;
			return false;
		}

		CardPile? pile = card.Pile;
		if (pile != null && pile.Type == PileType.Deck && card.Keywords.Contains(CardKeyword.Unplayable))
		{
			__result = false;
			return false;
		}

		__result = card.Enchantment == null || CanAttachEnchantment(card, __instance.GetType());
		return false;
	}

	private static bool EnchantPrefix(EnchantmentModel enchantment, CardModel card, decimal amount, ref EnchantmentModel? __result)
	{
		enchantment.AssertMutable();
		Type enchantmentType = enchantment.GetType();
		DebugLog("Enchant", $"Request card={DescribeCard(card)} existing={DescribeEnchantment(card.Enchantment)} new={enchantment.Id.Entry} amount={amount}.");
		if (!enchantment.CanEnchant(card))
		{
			if (HasEnchantmentType(card, enchantmentType) && !IsLayeredEnchantmentType(enchantmentType))
			{
				DebugLog("Enchant", $"Skipping duplicate non-layered enchantment {enchantment.Id.Entry} on {DescribeCard(card)}.");
				__result = FindExistingEnchantment(card, enchantmentType);
				return false;
			}

			throw new InvalidOperationException($"Cannot enchant {card.Id} with {enchantment.Id}.");
		}

		__result = ApplyEnchantmentToCard(card, enchantment, amount, recordHistory: true);
		DebugLog("Enchant", $"Enchant complete for {DescribeCard(card)}. Result={DescribeEnchantment(card.Enchantment)}.");
		return false;
	}

	private static void GetEnchantmentHoverTipsPostfix(EnchantmentModel __instance, ref IEnumerable<IHoverTip> __result)
	{
		if (__instance is not RepeatableCompositeEnchantment composite)
		{
			return;
		}

		List<IHoverTip> tips = new()
		{
			new HoverTip(composite.Title, composite.DynamicDescription)
		};
		tips.AddRange(composite.InnerEnchantments.SelectMany(static enchantment => enchantment.HoverTips));
		__result = tips;
	}

	private static void GetDescriptionForPilePostfix(CardModel __instance, ref string __result)
	{
		__result = AppendCompositeExtraText(__result, __instance);
	}

	private static void GetDescriptionForUpgradePreviewPostfix(CardModel __instance, ref string __result)
	{
		__result = AppendCompositeExtraText(__result, __instance);
	}

	private static bool UpdateEnchantmentVisualsPrefix(NCard __instance)
	{
		ClearExtraEnchantmentTabs(__instance);
		if (__instance.Model?.Enchantment is not RepeatableCompositeEnchantment composite)
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
		CardModel card = (CardModel)NCardEnchantVfxCardModelField.GetValue(__instance)!;
		if (card.Enchantment is not RepeatableCompositeEnchantment composite)
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

	private static bool CloneRestSiteOnSelectPrefix(CloneRestSiteOption __instance, ref Task<bool> __result)
	{
		Player owner = (Player)(RestSiteOptionOwnerProperty.GetValue(__instance)
			?? throw new InvalidOperationException("Could not read CloneRestSiteOption owner."));
		bool hasCompositeClone = owner.Deck.Cards.Any(static card =>
			card.Enchantment is RepeatableCompositeEnchantment composite && composite.ContainsEnchantmentType(typeof(Clone)));
		if (!hasCompositeClone)
		{
			return true;
		}

		__result = CloneRestSiteOnSelectReplacement(owner);
		return false;
	}

	private static async Task<bool> CloneRestSiteOnSelectReplacement(Player owner)
	{
		IEnumerable<CardModel> sourceCards = owner.Deck.Cards.Where(card => HasEnchantmentType(card, typeof(Clone))).ToList();
		DebugLog("Clone", $"Rest site clone option matched {sourceCards.Count()} cards for owner {owner.NetId}.");

		List<CardPileAddResult> results = new();
		foreach (CardModel source in sourceCards)
		{
			CardModel clone = owner.RunState.CloneCard(source);
			results.Add(await MegaCrit.Sts2.Core.Commands.CardPileCmd.Add(clone, PileType.Deck));
		}

		MegaCrit.Sts2.Core.Commands.CardCmd.PreviewCardPileAdd(results, 1.2f, CardPreviewStyle.MessyLayout);
		return true;
	}

	private static RepeatableCompositeEnchantment ConvertToComposite(CardModel card, EnchantmentModel existing)
	{
		RepeatableCompositeEnchantment composite = (RepeatableCompositeEnchantment)ModelDb.Enchantment<RepeatableCompositeEnchantment>().ToMutable();
		DebugLog("Composite", $"Before conversion card={DescribeCard(card)} existing={DescribeEnchantment(existing)} hasCard={existing.HasCard}.");
		card.ClearEnchantmentInternal();
		card.EnchantInternal(composite, 1m);
		composite.ImportExistingEnchantment(existing);
		DebugLog("Composite", $"After conversion card={DescribeCard(card)} current={DescribeEnchantment(card.Enchantment)}.");
		return composite;
	}

	private static EnchantmentModel ApplyEnchantmentToCard(CardModel card, EnchantmentModel enchantment, decimal amount, bool recordHistory)
	{
		Type enchantmentType = enchantment.GetType();
		if (card.Enchantment == null)
		{
			card.EnchantInternal(enchantment, amount);
			enchantment.ModifyCard();
			card.FinalizeUpgradeInternal();
			if (recordHistory)
			{
				RecordEnchantmentHistory(card, enchantment.Id);
			}

			return card.Enchantment!;
		}

		if (card.Enchantment is RepeatableCompositeEnchantment composite)
		{
			EnchantmentModel result = composite.AddOrStackEnchantment(enchantment, amount, RefreshConsumedStackTypes.Contains(enchantmentType));
			card.FinalizeUpgradeInternal();
			if (recordHistory)
			{
				RecordEnchantmentHistory(card, enchantment.Id);
			}

			return result;
		}

		EnchantmentModel existing = card.Enchantment;
		if (existing.GetType() == enchantmentType)
		{
			existing.Amount += (int)amount;
			if (RefreshConsumedStackTypes.Contains(enchantmentType) && existing.Status == EnchantmentStatus.Disabled)
			{
				existing.Status = EnchantmentStatus.Normal;
			}

			existing.RecalculateValues();
			card.DynamicVars.RecalculateForUpgradeOrEnchant();
			card.FinalizeUpgradeInternal();
			if (recordHistory)
			{
				RecordEnchantmentHistory(card, enchantment.Id);
			}

			return existing;
		}

		RepeatableCompositeEnchantment compositeEnchantment = ConvertToComposite(card, existing);
		EnchantmentModel applied = compositeEnchantment.AddOrStackEnchantment(enchantment, amount, RefreshConsumedStackTypes.Contains(enchantmentType));
		card.FinalizeUpgradeInternal();
		if (recordHistory)
		{
			RecordEnchantmentHistory(card, enchantment.Id);
		}

		return applied;
	}

	private static bool CanAttachEnchantment(CardModel card, Type enchantmentType)
	{
		if (!HasEnchantmentType(card, enchantmentType))
		{
			return true;
		}

		return IsLayeredEnchantmentType(enchantmentType);
	}

	private static bool HasEnchantmentType(CardModel card, Type enchantmentType)
	{
		if (card.Enchantment == null)
		{
			return false;
		}

		if (card.Enchantment is RepeatableCompositeEnchantment composite)
		{
			return composite.ContainsEnchantmentType(enchantmentType);
		}

		return card.Enchantment.GetType() == enchantmentType;
	}

	private static EnchantmentModel? FindExistingEnchantment(CardModel card, Type enchantmentType)
	{
		if (card.Enchantment == null)
		{
			return null;
		}

		if (card.Enchantment is RepeatableCompositeEnchantment composite)
		{
			return composite.FindEnchantment(enchantmentType);
		}

		return card.Enchantment.GetType() == enchantmentType ? card.Enchantment : null;
	}

	private static bool IsLayeredEnchantmentType(Type enchantmentType)
	{
		return LayeredEnchantmentTypes.Contains(enchantmentType);
	}

	private static string AppendCompositeExtraText(string baseDescription, CardModel card)
	{
		if (card.Enchantment is not RepeatableCompositeEnchantment composite)
		{
			return baseDescription;
		}

		List<string> lines = new();
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

	private static void RecordEnchantmentHistory(CardModel card, ModelId enchantmentId)
	{
		if (card.Pile != null)
		{
			card.Owner.RunState.CurrentMapPointHistoryEntry?.GetEntry(card.Owner.NetId).CardsEnchanted.Add(
				new MegaCrit.Sts2.Core.Runs.History.CardEnchantmentHistoryEntry(card, enchantmentId));
		}
	}

	private static FieldInfo RequireField(Type type, string name)
	{
		FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
		if (field == null)
		{
			throw new InvalidOperationException($"Could not find required field {type.FullName}.{name}.");
		}

		return field;
	}

	private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags, params Type[] parameters)
	{
		MethodInfo? method = type.GetMethod(name, flags, binder: null, parameters, modifiers: null);
		if (method == null)
		{
			throw new InvalidOperationException($"Could not find required method {type.FullName}.{name}.");
		}

		return method;
	}

	private static PropertyInfo RequireProperty(Type type, string name, BindingFlags flags)
	{
		PropertyInfo? property = type.GetProperty(name, flags);
		if (property == null)
		{
			throw new InvalidOperationException($"Could not find required property {type.FullName}.{name}.");
		}

		return property;
	}

	internal static void DebugLog(string area, string message)
	{
		Log.Info($"{LogPrefix}[{area}] {message}");
	}

	internal static string DescribeCard(CardModel? card)
	{
		if (card == null)
		{
			return "<null-card>";
		}

		string enchantment = card.Enchantment == null ? "none" : DescribeEnchantment(card.Enchantment);
		return $"{card.Id.Entry}+{card.CurrentUpgradeLevel}[{enchantment}]";
	}

	private static string DescribeEnchantment(EnchantmentModel? enchantment)
	{
		if (enchantment == null)
		{
			return "none";
		}

		if (enchantment is RepeatableCompositeEnchantment composite)
		{
			return $"composite:{string.Join("+", composite.InnerEnchantments.Select(inner => $"{inner.Id.Entry}x{inner.Amount}"))}";
		}

		return $"{enchantment.Id.Entry}x{enchantment.Amount}";
	}
}
