using System.Reflection;
using Godot;
using HarmonyLib;
using HextechRunes;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HextechRunesSponsorPack;

internal static partial class BuiltInRepeatableEnchantments
{
	private const string LogPrefix = "[HextechRunesSponsorPack][RepeatableEnchantments]";
	private const string HarmonyId = "Natsuki.HextechRunesSponsorPack.RepeatableEnchantments";
	private const string LegacyRepeatableAssemblyName = "RepeatableEnchantments";
	private const string MultiEnchantmentAssemblyName = "MultiEnchantmentMod";
	private static readonly bool VerboseLog = false;

	private static readonly HashSet<Type> LayeredEnchantmentTypes =
	[
		typeof(Adroit),
		typeof(Goopy),
		typeof(Momentum),
		typeof(Nimble),
		typeof(Sharp),
		typeof(Sown),
		typeof(Swift),
		typeof(Vigorous)
	];

	private static readonly HashSet<Type> RefreshConsumedStackTypes =
	[
		typeof(Sown),
		typeof(Swift),
		typeof(Vigorous)
	];

	private static readonly StringName UiTintHue = new("h");
	private static readonly StringName UiTintSaturation = new("s");
	private static readonly StringName UiTintValue = new("v");
	private const string ExtraEnchantmentTabPrefix = "HextechSponsorPackExtraEnchantmentTab";

	private static readonly object InitializeLock = new();
	private static Harmony? _harmony;
	private static bool _initialized;
#if !STS2_109_OR_NEWER
	private static bool _savedPropertiesRegistered;
#endif

	private static FieldInfo? _nCardEnchantmentIconField;
	private static FieldInfo? _nCardEnchantmentLabelField;
	private static FieldInfo? _nCardDefaultEnchantmentPositionField;
	private static FieldInfo? _nEnchantPreviewBeforeField;
	private static FieldInfo? _nEnchantPreviewAfterField;
	private static MethodInfo? _nEnchantPreviewRemoveExistingCardsMethod;
	private static FieldInfo? _nCardEnchantVfxCardNodeField;
	private static FieldInfo? _nCardEnchantVfxIconField;
	private static FieldInfo? _nCardEnchantVfxLabelField;
	private static FieldInfo? _nCardEnchantVfxCardModelField;
	private static PropertyInfo? _restSiteOptionOwnerProperty;

	private static FieldInfo NCardEnchantmentIconField => _nCardEnchantmentIconField ?? throw PrivateMemberNotInitialized();
	private static FieldInfo NCardEnchantmentLabelField => _nCardEnchantmentLabelField ?? throw PrivateMemberNotInitialized();
	private static FieldInfo NCardDefaultEnchantmentPositionField => _nCardDefaultEnchantmentPositionField ?? throw PrivateMemberNotInitialized();
	private static FieldInfo NEnchantPreviewBeforeField => _nEnchantPreviewBeforeField ?? throw PrivateMemberNotInitialized();
	private static FieldInfo NEnchantPreviewAfterField => _nEnchantPreviewAfterField ?? throw PrivateMemberNotInitialized();
	private static MethodInfo NEnchantPreviewRemoveExistingCardsMethod => _nEnchantPreviewRemoveExistingCardsMethod ?? throw PrivateMemberNotInitialized();
	private static FieldInfo NCardEnchantVfxCardNodeField => _nCardEnchantVfxCardNodeField ?? throw PrivateMemberNotInitialized();
	private static FieldInfo NCardEnchantVfxIconField => _nCardEnchantVfxIconField ?? throw PrivateMemberNotInitialized();
	private static FieldInfo NCardEnchantVfxLabelField => _nCardEnchantVfxLabelField ?? throw PrivateMemberNotInitialized();
	private static FieldInfo NCardEnchantVfxCardModelField => _nCardEnchantVfxCardModelField ?? throw PrivateMemberNotInitialized();
	private static PropertyInfo RestSiteOptionOwnerProperty => _restSiteOptionOwnerProperty ?? throw PrivateMemberNotInitialized();

	internal static void Initialize()
	{
		lock (InitializeLock)
		{
#if !STS2_109_OR_NEWER
			if (!_savedPropertiesRegistered)
			{
				// 0.109.0 起注入入口移除:游戏在 ExecuteEssential 的 ModelIdSerializationCache.Init()
				// 里从 ModelDb.All 自动收录全部载体(本类型经模组程序集注册进 ModelDb,无需手动注入)。
				SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(SponsorCompositeEnchantment));
				_savedPropertiesRegistered = true;
			}
#endif
			if (_initialized)
			{
				return;
			}

			Harmony harmony = _harmony ??= new Harmony(HarmonyId);
			try
			{
				InstallCoreHooks(harmony);
			}
			catch
			{
				harmony.UnpatchAll(HarmonyId);
				_harmony = null;
				throw;
			}

			TryInstallUiHooks();
			TryInstallRestSiteHook();
			TryPatchThievingHopperStealPriorities();
			_initialized = true;
			Log.Info($"{LogPrefix} Built-in repeatable enchantment hooks installed. Composite model id={ModelDb.GetId<SponsorCompositeEnchantment>().Entry}.");
		}
	}

	private static void InstallCoreHooks(Harmony harmony)
	{
		MethodInfo canEnchant = RequireMethod(typeof(EnchantmentModel), nameof(EnchantmentModel.CanEnchant), BindingFlags.Instance | BindingFlags.Public, typeof(CardModel));
		MethodInfo enchant = RequireMethod(typeof(CardCmd), nameof(CardCmd.Enchant), BindingFlags.Static | BindingFlags.Public, typeof(EnchantmentModel), typeof(CardModel), typeof(decimal));
		MethodInfo getHoverTips = RequireMethod(typeof(EnchantmentModel), "get_HoverTips", BindingFlags.Instance | BindingFlags.Public);
		MethodInfo getDescriptionForPile = RequireMethod(typeof(CardModel), nameof(CardModel.GetDescriptionForPile), BindingFlags.Instance | BindingFlags.Public, typeof(PileType), typeof(Creature));
		MethodInfo getDescriptionForUpgradePreview = RequireMethod(typeof(CardModel), nameof(CardModel.GetDescriptionForUpgradePreview), BindingFlags.Instance | BindingFlags.Public);

		harmony.Patch(canEnchant,
			prefix: new HarmonyMethod(typeof(BuiltInRepeatableEnchantments), nameof(CanEnchantPrefix)));
		harmony.Patch(enchant,
			prefix: new HarmonyMethod(typeof(BuiltInRepeatableEnchantments), nameof(EnchantPrefix)));
		harmony.Patch(getHoverTips,
			postfix: new HarmonyMethod(typeof(BuiltInRepeatableEnchantments), nameof(GetEnchantmentHoverTipsPostfix)));
		harmony.Patch(getDescriptionForPile,
			postfix: new HarmonyMethod(typeof(BuiltInRepeatableEnchantments), nameof(GetDescriptionForPilePostfix)));
		harmony.Patch(getDescriptionForUpgradePreview,
			postfix: new HarmonyMethod(typeof(BuiltInRepeatableEnchantments), nameof(GetDescriptionForUpgradePreviewPostfix)));
		InstallHookListenerExpansion(harmony);
	}

	private static void TryInstallUiHooks()
	{
		const string uiHarmonyId = HarmonyId + ".Ui";
		Harmony harmony = new(uiHarmonyId);
		try
		{
			_nCardEnchantmentIconField = RequireField(typeof(NCard), "_enchantmentIcon");
			_nCardEnchantmentLabelField = RequireField(typeof(NCard), "_enchantmentLabel");
			_nCardDefaultEnchantmentPositionField = RequireField(typeof(NCard), "_defaultEnchantmentPosition");
			_nEnchantPreviewBeforeField = RequireField(typeof(NEnchantPreview), "_before");
			_nEnchantPreviewAfterField = RequireField(typeof(NEnchantPreview), "_after");
			_nEnchantPreviewRemoveExistingCardsMethod = RequireMethod(typeof(NEnchantPreview), "RemoveExistingCards", BindingFlags.Instance | BindingFlags.NonPublic);
			_nCardEnchantVfxCardNodeField = RequireField(typeof(NCardEnchantVfx), "_cardNode");
			_nCardEnchantVfxIconField = RequireField(typeof(NCardEnchantVfx), "_enchantmentIcon");
			_nCardEnchantVfxLabelField = RequireField(typeof(NCardEnchantVfx), "_enchantmentLabel");
			_nCardEnchantVfxCardModelField = RequireField(typeof(NCardEnchantVfx), "_cardModel");

			MethodInfo updateVisuals = RequireMethod(typeof(NCard), "UpdateEnchantmentVisuals", BindingFlags.Instance | BindingFlags.NonPublic);
			MethodInfo initPreview = RequireMethod(typeof(NEnchantPreview), nameof(NEnchantPreview.Init), BindingFlags.Instance | BindingFlags.Public, typeof(CardModel), typeof(EnchantmentModel), typeof(int));
			MethodInfo readyVfx = RequireMethod(typeof(NCardEnchantVfx), nameof(NCardEnchantVfx._Ready), BindingFlags.Instance | BindingFlags.Public);
			harmony.Patch(updateVisuals, prefix: new HarmonyMethod(typeof(BuiltInRepeatableEnchantments), nameof(UpdateEnchantmentVisualsPrefix)));
			harmony.Patch(initPreview, prefix: new HarmonyMethod(typeof(BuiltInRepeatableEnchantments), nameof(EnchantPreviewInitPrefix)));
			harmony.Patch(readyVfx, postfix: new HarmonyMethod(typeof(BuiltInRepeatableEnchantments), nameof(CardEnchantVfxReadyPostfix)));
		}
		catch (Exception ex)
		{
			harmony.UnpatchAll(uiHarmonyId);
			Log.Warn($"{LogPrefix} Repeatable-enchantment UI hooks were disabled: {ex.GetType().Name}: {ex.Message}", 2);
		}
	}

	private static void TryInstallRestSiteHook()
	{
		const string restSiteHarmonyId = HarmonyId + ".RestSite";
		Harmony harmony = new(restSiteHarmonyId);
		try
		{
			_restSiteOptionOwnerProperty = RequireProperty(typeof(RestSiteOption), "Owner", BindingFlags.Instance | BindingFlags.NonPublic);
			MethodInfo onSelect = RequireMethod(typeof(CloneRestSiteOption), nameof(CloneRestSiteOption.OnSelect), BindingFlags.Instance | BindingFlags.Public);
			harmony.Patch(onSelect, prefix: new HarmonyMethod(typeof(BuiltInRepeatableEnchantments), nameof(CloneRestSiteOnSelectPrefix)));
		}
		catch (Exception ex)
		{
			harmony.UnpatchAll(restSiteHarmonyId);
			Log.Warn($"{LogPrefix} Clone rest-site compatibility was disabled: {ex.GetType().Name}: {ex.Message}", 2);
		}
	}

	private static void TryPatchThievingHopperStealPriorities()
	{
		try
		{
			PatchThievingHopperStealPriorities();
		}
		catch (Exception ex)
		{
			Log.Warn($"{LogPrefix} Thieving Hopper compatibility was disabled: {ex.GetType().Name}: {ex.Message}", 2);
		}
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
		if (IsExternalMultiEnchantmentProviderActive())
		{
			return true;
		}

		if (__instance is SponsorCompositeEnchantment)
		{
			__result = false;
			return false;
		}

		if (!CanUseBuiltInRepeatableEnchantments(card))
		{
			return true;
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
		if (IsExternalMultiEnchantmentProviderActive())
		{
			return true;
		}

		enchantment.AssertMutable();
		if (enchantment is SponsorCompositeEnchantment incomingComposite && card.Enchantment == null)
		{
			card.EnchantInternal(incomingComposite, amount);
			incomingComposite.ModifyCard();
			card.FinalizeUpgradeInternal();
			RecordEnchantmentHistory(card, incomingComposite.Id);
			__result = incomingComposite;
			return false;
		}
		if (!CanUseBuiltInRepeatableEnchantments(card))
		{
			return true;
		}

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

	private static bool CloneRestSiteOnSelectPrefix(CloneRestSiteOption __instance, ref Task<bool> __result)
	{
		if (IsExternalMultiEnchantmentProviderActive())
		{
			return true;
		}

		Player owner = (Player)(RestSiteOptionOwnerProperty.GetValue(__instance)
			?? throw new InvalidOperationException("Could not read CloneRestSiteOption owner."));
		bool hasCompositeClone = owner.Deck.Cards.Any(static card =>
			card.Enchantment is SponsorCompositeEnchantment composite && composite.ContainsEnchantmentType(typeof(Clone)));
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

		List<CardPileAddResult> results = [];
		foreach (CardModel source in sourceCards)
		{
			CardModel clone = owner.RunState.CloneCard(source);
			results.Add(await CardPileCmd.Add(clone, PileType.Deck));
		}

		CardCmd.PreviewCardPileAdd(results, 1.2f, CardPreviewStyle.MessyLayout);
		return true;
	}

	private static SponsorCompositeEnchantment ConvertToComposite(CardModel card, EnchantmentModel existing)
	{
		SponsorCompositeEnchantment composite = (SponsorCompositeEnchantment)ModelDb.Enchantment<SponsorCompositeEnchantment>().ToMutable();
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

		if (card.Enchantment is SponsorCompositeEnchantment composite)
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

		SponsorCompositeEnchantment compositeEnchantment = ConvertToComposite(card, existing);
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

	internal static bool HasEnchantmentType(CardModel card, Type enchantmentType)
	{
		return EnchantmentCompositionAdapter.Contains(card, enchantmentType);
	}

	private static EnchantmentModel? FindExistingEnchantment(CardModel card, Type enchantmentType)
	{
		return EnchantmentCompositionAdapter.Find(card, enchantmentType);
	}

	private static bool IsLayeredEnchantmentType(Type enchantmentType)
	{
		return LayeredEnchantmentTypes.Contains(enchantmentType);
	}

	private static bool CanUseBuiltInRepeatableEnchantments(CardModel card)
	{
		return RepeatableEnchantmentAccessPolicy.IsEnabledFor(card.Owner);
	}

	private static void RecordEnchantmentHistory(CardModel card, ModelId enchantmentId)
	{
		if (card.Pile != null)
		{
			card.Owner.RunState.CurrentMapPointHistoryEntry?.GetEntry(card.Owner.NetId).CardsEnchanted.Add(
				new CardEnchantmentHistoryEntry(card, enchantmentId));
		}
	}

	private static bool IsExternalMultiEnchantmentProviderActive()
	{
		return AppDomain.CurrentDomain.GetAssemblies().Any(static assembly =>
			string.Equals(assembly.GetName().Name, LegacyRepeatableAssemblyName, StringComparison.Ordinal)
			|| string.Equals(assembly.GetName().Name, MultiEnchantmentAssemblyName, StringComparison.Ordinal));
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

	private static InvalidOperationException PrivateMemberNotInitialized()
	{
		return new InvalidOperationException("Repeatable-enchantment compatibility private members were not initialized.");
	}

	internal static void DebugLog(string area, string message)
	{
		if (VerboseLog)
		{
			Log.Info($"{LogPrefix}[{area}] {message}");
		}
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

		if (enchantment is SponsorCompositeEnchantment composite)
		{
			return $"composite:{string.Join("+", composite.InnerEnchantments.Select(inner => $"{inner.Id.Entry}x{inner.Amount}"))}";
		}

		return $"{enchantment.Id.Entry}x{enchantment.Amount}";
	}
}
