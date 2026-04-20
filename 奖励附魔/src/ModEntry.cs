using System.Reflection;
using System.Runtime.Loader;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using DetourHook = MonoMod.RuntimeDetour.Hook;

namespace RewardEnchants;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
	private const decimal EnchantChancePerAct = 0.125m;
	private const decimal EnchantAmount = 1m;
	private const string VanillaEnchantmentNamespace = "MegaCrit.Sts2.Core.Models.Enchantments";
	private static readonly HashSet<Type> ExcludedRewardEnchantmentTypes = new()
	{
		typeof(Clone),
		typeof(DeprecatedEnchantment),
		typeof(TezcatarasEmber)
	};

	private static DetourHook? _tryModifyCardRewardOptionsHook;
	private static DetourHook? _merchantCardPopulateHook;
	private static IReadOnlyList<EnchantmentModel>? _vanillaEnchantments;

	private delegate bool OrigTryModifyCardRewardOptions(
		IRunState runState,
		Player player,
		List<CardCreationResult> cardRewardOptions,
		CardCreationOptions creationOptions,
		out List<AbstractModel> modifiers);
	private delegate void OrigMerchantCardPopulate(MerchantCardEntry self);

	public static void Initialize()
	{
		PreloadDependencyAssemblies();
		InstallHooks();
		Log.Info("[RewardEnchants] Loaded.");
	}

	private static void PreloadDependencyAssemblies()
	{
		Assembly assembly = Assembly.GetExecutingAssembly();
		string? modDirectory = Path.GetDirectoryName(assembly.Location);
		if (string.IsNullOrEmpty(modDirectory) || !Directory.Exists(modDirectory))
		{
			return;
		}

		string selfPath = assembly.Location;
		AssemblyLoadContext loadContext = AssemblyLoadContext.GetLoadContext(assembly) ?? AssemblyLoadContext.Default;
		foreach (string dllPath in Directory.GetFiles(modDirectory, "*.dll"))
		{
			if (string.Equals(dllPath, selfPath, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			loadContext.LoadFromAssemblyPath(dllPath);
		}
	}

	private static void InstallHooks()
	{
		_tryModifyCardRewardOptionsHook = new DetourHook(
			RequireMethod(
				typeof(MegaCrit.Sts2.Core.Hooks.Hook),
				nameof(MegaCrit.Sts2.Core.Hooks.Hook.TryModifyCardRewardOptions),
				BindingFlags.Static | BindingFlags.Public,
				typeof(IRunState),
				typeof(Player),
				typeof(List<CardCreationResult>),
				typeof(CardCreationOptions),
				typeof(List<AbstractModel>).MakeByRefType()),
			TryModifyCardRewardOptionsDetour);
		_merchantCardPopulateHook = new DetourHook(
			RequireMethod(typeof(MerchantCardEntry), nameof(MerchantCardEntry.Populate), BindingFlags.Instance | BindingFlags.Public),
			MerchantCardPopulateDetour);
	}

	private static bool TryModifyCardRewardOptionsDetour(
		OrigTryModifyCardRewardOptions orig,
		IRunState runState,
		Player player,
		List<CardCreationResult> cardRewardOptions,
		CardCreationOptions creationOptions,
		out List<AbstractModel> modifiers)
	{
		bool modified = orig(runState, player, cardRewardOptions, creationOptions, out modifiers);
		if (!ShouldProcessRewards(cardRewardOptions, creationOptions))
		{
			return modified;
		}

		bool enchantedAny = TryEnchantRewardCards(player, creationOptions, cardRewardOptions);
		return modified || enchantedAny;
	}

	private static void MerchantCardPopulateDetour(OrigMerchantCardPopulate orig, MerchantCardEntry self)
	{
		orig(self);

		CardCreationResult? creationResult = self.CreationResult;
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
		string derivedName = $"RewardEnchants.shop.{shopsRng.Counter}.{currentCard.Id.Entry}.{currentCard.CurrentUpgradeLevel}.{currentCard.Enchantment?.Id.Entry ?? "none"}";
		Rng localRng = new Rng(shopsRng.Seed, derivedName);
		return TryApplyRandomEnchantment(result, player, localRng, "merchant");
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
