using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;

namespace StartingDeckTweaks;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
	private const string HarmonyId = "Natsuki.StartingDeckTweaks";
	private const string ModId = "StartingDeckTweaks";
	private const string TargetGameVersion = "0.107.1";

	private static Harmony? _harmony;
	private static bool _hooksInstalled;

	public static void Initialize()
	{
		Harmony harmony = _harmony ??= new Harmony(HarmonyId);
		InstallHooks(harmony);
		Log.Info($"[{ModId}] Loaded for Slay the Spire 2 {TargetGameVersion}.");
	}

	private static void InstallHooks(Harmony harmony)
	{
		if (_hooksInstalled)
		{
			return;
		}

		PatchStartingDeck<Ironclad>(harmony, nameof(IroncladStartingDeckPrefix));
		PatchStartingDeck<Silent>(harmony, nameof(SilentStartingDeckPrefix));
		PatchStartingDeck<Defect>(harmony, nameof(DefectStartingDeckPrefix));
		PatchStartingDeck<Regent>(harmony, nameof(RegentStartingDeckPrefix));
		PatchStartingDeck<Necrobinder>(harmony, nameof(NecrobinderStartingDeckPrefix));
		_hooksInstalled = true;
	}

	private static void PatchStartingDeck<TCharacter>(Harmony harmony, string prefixName) where TCharacter : CharacterModel
	{
		MethodInfo? getter = typeof(TCharacter)
			.GetProperty(nameof(CharacterModel.StartingDeck), BindingFlags.Instance | BindingFlags.Public)
			?.GetMethod;
		if (getter == null)
		{
			throw new InvalidOperationException($"Could not find {typeof(TCharacter).FullName}.StartingDeck getter.");
		}

		harmony.Patch(getter, prefix: new HarmonyMethod(typeof(ModEntry), prefixName));
	}

	private static bool IroncladStartingDeckPrefix(ref IEnumerable<CardModel> __result)
	{
		__result = new CardModel[]
		{
			ModelDb.Card<StrikeIronclad>(),
			ModelDb.Card<StrikeIronclad>(),
			ModelDb.Card<StrikeIronclad>(),
			ModelDb.Card<DefendIronclad>(),
			ModelDb.Card<DefendIronclad>(),
			ModelDb.Card<DefendIronclad>(),
			ModelDb.Card<Bash>(),
			ModelDb.Card<TwinStrike>()
		};
		return false;
	}

	private static bool SilentStartingDeckPrefix(ref IEnumerable<CardModel> __result)
	{
		__result = new CardModel[]
		{
			ModelDb.Card<StrikeSilent>(),
			ModelDb.Card<StrikeSilent>(),
			ModelDb.Card<StrikeSilent>(),
			ModelDb.Card<DefendSilent>(),
			ModelDb.Card<DefendSilent>(),
			ModelDb.Card<DefendSilent>(),
			ModelDb.Card<Neutralize>(),
			ModelDb.Card<Survivor>()
		};
		return false;
	}

	private static bool DefectStartingDeckPrefix(ref IEnumerable<CardModel> __result)
	{
		__result = new CardModel[]
		{
			ModelDb.Card<StrikeDefect>(),
			ModelDb.Card<StrikeDefect>(),
			ModelDb.Card<StrikeDefect>(),
			ModelDb.Card<DefendDefect>(),
			ModelDb.Card<DefendDefect>(),
			ModelDb.Card<DefendDefect>(),
			ModelDb.Card<Zap>(),
			ModelDb.Card<Dualcast>()
		};
		return false;
	}

	private static bool RegentStartingDeckPrefix(ref IEnumerable<CardModel> __result)
	{
		__result = new CardModel[]
		{
			ModelDb.Card<StrikeRegent>(),
			ModelDb.Card<StrikeRegent>(),
			ModelDb.Card<StrikeRegent>(),
			ModelDb.Card<DefendRegent>(),
			ModelDb.Card<DefendRegent>(),
			ModelDb.Card<DefendRegent>(),
			ModelDb.Card<Venerate>(),
			ModelDb.Card<FallingStar>()
		};
		return false;
	}

	private static bool NecrobinderStartingDeckPrefix(ref IEnumerable<CardModel> __result)
	{
		__result = new CardModel[]
		{
			ModelDb.Card<StrikeNecrobinder>(),
			ModelDb.Card<StrikeNecrobinder>(),
			ModelDb.Card<StrikeNecrobinder>(),
			ModelDb.Card<DefendNecrobinder>(),
			ModelDb.Card<DefendNecrobinder>(),
			ModelDb.Card<DefendNecrobinder>(),
			ModelDb.Card<Unleash>(),
			ModelDb.Card<Bodyguard>()
		};
		return false;
	}
}
