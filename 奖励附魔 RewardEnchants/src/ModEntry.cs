using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using CoreHook = MegaCrit.Sts2.Core.Hooks.Hook;

namespace RewardEnchants;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
	private const string HarmonyId = "Natsuki.RewardEnchants";
	private const decimal EnchantChancePerAct = 0.125m;
	private const string VanillaEnchantmentNamespace = "MegaCrit.Sts2.Core.Models.Enchantments";
	private const string ShopsRngName = "shops";
	private static readonly HashSet<Type> ExcludedRewardEnchantmentTypes = new()
	{
		typeof(Clone),
		typeof(DeprecatedEnchantment),
		typeof(TezcatarasEmber)
	};

	private static Harmony? _harmony;
	private static bool _hooksInstalled;
	private static IReadOnlyList<EnchantmentModel>? _vanillaEnchantments;

	public static void Initialize()
	{
		Harmony harmony = _harmony ??= new Harmony(HarmonyId);
		InstallHooks(harmony);
		Log.Info("[RewardEnchants] Loaded.");
	}

	private static void InstallHooks(Harmony harmony)
	{
		if (_hooksInstalled)
		{
			return;
		}

		harmony.Patch(
			RequireMethod(
				typeof(CoreHook),
				nameof(CoreHook.TryModifyCardRewardOptions),
				BindingFlags.Static | BindingFlags.Public,
				typeof(IRunState),
				typeof(Player),
				typeof(List<CardCreationResult>),
				typeof(CardCreationOptions),
				typeof(List<AbstractModel>).MakeByRefType()),
			postfix: new HarmonyMethod(typeof(ModEntry), nameof(TryModifyCardRewardOptionsPostfix)));
		harmony.Patch(
			RequireMethod(typeof(MerchantCardEntry), nameof(MerchantCardEntry.Populate), BindingFlags.Instance | BindingFlags.Public),
			postfix: new HarmonyMethod(typeof(ModEntry), nameof(MerchantCardPopulatePostfix)));
		_hooksInstalled = true;
	}

	private static void TryModifyCardRewardOptionsPostfix(
		Player player,
		List<CardCreationResult> cardRewardOptions,
		CardCreationOptions creationOptions,
		ref bool __result)
	{
		if (!ShouldProcessRewards(cardRewardOptions, creationOptions))
		{
			return;
		}

		bool enchantedAny = TryEnchantRewardCards(player, creationOptions, cardRewardOptions);
		__result = __result || enchantedAny;
	}

	private static void MerchantCardPopulatePostfix(MerchantCardEntry __instance)
	{
		CardCreationResult? creationResult = __instance.CreationResult;
		if (creationResult == null)
		{
			return;
		}

		TryApplyMerchantEnchantment(creationResult, creationResult.Card.Owner);
	}

	private static bool ShouldProcessRewards(List<CardCreationResult> cardRewardOptions, CardCreationOptions creationOptions)
	{
		if (cardRewardOptions.Count == 0)
		{
			return false;
		}

		if (creationOptions.Source != CardCreationSource.Encounter)
		{
			return false;
		}

		if (creationOptions.Flags.HasFlag(CardCreationFlags.NoModifyHooks))
		{
			return false;
		}

		if (creationOptions.Flags.HasFlag(CardCreationFlags.NoCardModelModifications))
		{
			return false;
		}

		return true;
	}

	private static bool TryEnchantRewardCards(Player player, CardCreationOptions creationOptions, List<CardCreationResult> cardRewardOptions)
	{
		bool enchantedAny = false;
		Rng rng = creationOptions.RngOverride ?? player.PlayerRng.Rewards;

		foreach (CardCreationResult reward in cardRewardOptions)
		{
			enchantedAny = TryApplyRandomEnchantment(reward, player, rng, "reward") || enchantedAny;
		}

		return enchantedAny;
	}

	private static bool TryApplyMerchantEnchantment(CardCreationResult result, Player player)
	{
		Rng shopsRng = player.PlayerRng.Shops;
		CardModel currentCard = result.Card;
		Rng localRng = CreateMerchantEnchantmentRng(shopsRng, currentCard, player);
		return TryApplyRandomEnchantment(result, player, localRng, "merchant");
	}

	private static Rng CreateMerchantEnchantmentRng(Rng shopsRng, CardModel card, Player player)
	{
#if STS2_110_OR_NEWER
		int counter = shopsRng.ToSerializable().counter;
		ulong shopSeed = unchecked(player.PlayerRng.Seed + StringHelper.GetDeterministicHashCode(ShopsRngName));
#else
		int counter = shopsRng.Counter;
		uint shopSeed = shopsRng.Seed;
#endif
		string derivedName = $"RewardEnchants.shop.{counter}.{card.Id.Entry}.{card.CurrentUpgradeLevel}.{card.Enchantment?.Id.Entry ?? "none"}";
		return new Rng(shopSeed, derivedName);
	}

	private static bool TryApplyRandomEnchantment(CardCreationResult result, Player player, Rng rng, string sourceLabel)
	{
		CardModel currentCard = result.Card;
		List<EnchantmentModel> candidates = GetEligibleEnchantments(currentCard);
		if (candidates.Count == 0)
		{
			return false;
		}

		if ((decimal)rng.NextFloat() > GetEnchantChance(player.RunState.CurrentActIndex))
		{
			return false;
		}

		EnchantmentModel? selected = rng.NextItem(candidates);
		if (selected == null)
		{
			return false;
		}

		CardModel enchantedCard = player.RunState.CloneCard(currentCard);
		decimal enchantAmount = GetEnchantAmount(player.RunState.CurrentActIndex);
		CardCmd.Enchant(selected.ToMutable(), enchantedCard, enchantAmount);
		result.ModifyCard(enchantedCard);

		Log.Info($"[RewardEnchants] Added {selected.Id.Entry} x{enchantAmount} to {sourceLabel} card {enchantedCard.Id.Entry}.");
		return true;
	}

	private static decimal GetEnchantChance(int currentActIndex)
	{
		return Math.Clamp((currentActIndex + 1) * EnchantChancePerAct, 0m, 1m);
	}

	private static decimal GetEnchantAmount(int currentActIndex)
	{
		return currentActIndex + 1;
	}

	private static List<EnchantmentModel> GetEligibleEnchantments(CardModel card)
	{
		return GetVanillaEnchantments()
			.Where((EnchantmentModel enchantment) => IsEligibleRewardEnchantment(card, enchantment))
			.ToList();
	}

	private static IReadOnlyList<EnchantmentModel> GetVanillaEnchantments()
	{
		return _vanillaEnchantments ??= ModelDb.DebugEnchantments
			.Where(IsVanillaRewardEnchantment)
			.OrderBy((EnchantmentModel enchantment) => enchantment.Id.Entry, StringComparer.Ordinal)
			.ToList();
	}

	private static bool IsVanillaRewardEnchantment(EnchantmentModel enchantment)
	{
		Type type = enchantment.GetType();
		if (type.Namespace != VanillaEnchantmentNamespace)
		{
			return false;
		}

		return true;
	}

	private static bool IsEligibleRewardEnchantment(CardModel card, EnchantmentModel enchantment)
	{
		Type type = enchantment.GetType();
		if (ExcludedRewardEnchantmentTypes.Contains(type))
		{
			return false;
		}

		if (!enchantment.CanEnchant(card))
		{
			return false;
		}

		if (enchantment is Inky && !IsInkyCompatibleRewardCard(card))
		{
			return false;
		}

		return true;
	}

	private static bool IsInkyCompatibleRewardCard(CardModel card)
	{
		return card.Type == CardType.Attack
			&& card.TargetType is TargetType.AnyEnemy or TargetType.AllEnemies or TargetType.RandomEnemy;
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
}
