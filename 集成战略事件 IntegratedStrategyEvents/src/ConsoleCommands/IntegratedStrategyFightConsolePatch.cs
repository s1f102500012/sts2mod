using HarmonyLib;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Models;

namespace IntegratedStrategyEvents.ConsoleCommands;

[HarmonyPatch(typeof(FightConsoleCmd), nameof(FightConsoleCmd.Process))]
[IntegratedStrategyPatch("IntegratedStrategyFightConsoleProcessPatch", "content", "本模组内容")]
internal static class IntegratedStrategyFightConsoleProcessPatch
{
	[HarmonyPrefix]
	private static void AddIntegratedStrategyPrefix(string[] args)
	{
		if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
		{
			return;
		}

		args[0] = IntegratedStrategyFightConsoleAliases.Normalize(args[0]);
	}
}

[HarmonyPatch(typeof(FightConsoleCmd), nameof(FightConsoleCmd.GetArgumentCompletions))]
[IntegratedStrategyPatch("IntegratedStrategyFightConsoleCompletionPatch", "content", "本模组内容")]
internal static class IntegratedStrategyFightConsoleCompletionPatch
{
	[HarmonyPostfix]
	private static void AddIntegratedStrategyEntries(string[] args, ref CompletionResult __result)
	{
		if (args.Length > 1)
		{
			return;
		}

		string partial = args.FirstOrDefault() ?? string.Empty;
		HashSet<string> candidates = new(__result.Candidates, StringComparer.OrdinalIgnoreCase);
		foreach (string entry in IntegratedStrategyFightConsoleAliases.GetCompletionEntries())
		{
			if (IsMatch(entry, partial))
			{
				candidates.Add(entry);
			}
		}

		__result.Candidates = candidates
			.OrderBy(entry => GetRelevance(entry, partial))
			.ThenBy(entry => entry, StringComparer.Ordinal)
			.ToList();
		__result.CommonPrefix = CalculateCommonCompletion(__result.Candidates, __result.CommandPrefix);
	}

	private static bool IsMatch(string entry, string partial)
	{
		return string.IsNullOrWhiteSpace(partial) ||
			entry.Contains(partial, StringComparison.OrdinalIgnoreCase);
	}

	private static int GetRelevance(string entry, string partial)
	{
		if (string.IsNullOrWhiteSpace(partial))
		{
			return 0;
		}

		if (entry.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
		{
			return 0;
		}

		string unprefixed = IntegratedStrategyFightConsoleAliases.RemoveModPrefix(entry);
		return unprefixed.StartsWith(partial, StringComparison.OrdinalIgnoreCase) ? 1 : 2;
	}

	private static string CalculateCommonCompletion(IReadOnlyList<string> entries, string prefix)
	{
		if (entries.Count == 0)
		{
			return string.Empty;
		}

		if (entries.Count == 1)
		{
			return prefix + entries[0] + " ";
		}

		int maxLength = entries.Min(entry => entry.Length);
		string first = entries[0];
		int matchingLength = 0;
		for (int i = 0; i < maxLength; i++)
		{
			char c = first[i];
			if (!entries.All(entry => char.ToUpperInvariant(entry[i]) == char.ToUpperInvariant(c)))
			{
				break;
			}

			matchingLength = i + 1;
		}

		return matchingLength == 0 ? string.Empty : prefix + first[..matchingLength];
	}
}

internal static class IntegratedStrategyFightConsoleAliases
{
	private const string ModPrefix = "INTEGRATEDSTRATEGYEVENTS-";
	private static readonly ModelId EncounterCategory = new(ModelId.SlugifyCategory<EncounterModel>(), string.Empty);
	private static readonly Dictionary<string, string> ExplicitAliases = new(StringComparer.OrdinalIgnoreCase)
	{
		["IZUMIK"] = "IZUMIK_ECOLOGICAL_FOUNTAIN_BOSS_ENCOUNTER",
		["IZUMIK_TEST"] = "IZUMIK_ECOLOGICAL_FOUNTAIN_BOSS_ENCOUNTER",
		["ECOLOGICAL_FOUNTAIN"] = "IZUMIK_ECOLOGICAL_FOUNTAIN_BOSS_ENCOUNTER",
		["IZUMIK_ECOLOGICAL_FOUNTAIN"] = "IZUMIK_ECOLOGICAL_FOUNTAIN_BOSS_ENCOUNTER",
		["IZUMIK_ECOLOGICAL_FOUNTAIN_TEST_ENCOUNTER"] = "IZUMIK_ECOLOGICAL_FOUNTAIN_BOSS_ENCOUNTER",
		["IZUMIK_OFFSPRING"] = "IZUMIK_OFFSPRING_TEST_ENCOUNTER",
		["IZUMIK_OFFSPRING_TEST"] = "IZUMIK_OFFSPRING_TEST_ENCOUNTER",
		["ISHARMLA"] = "ISHARMLA_CORRUPTED_HEART_BOSS_ENCOUNTER",
		["ISHARMLA_TEST"] = "ISHARMLA_CORRUPTED_HEART_BOSS_ENCOUNTER",
		["CORRUPTED_HEART"] = "ISHARMLA_CORRUPTED_HEART_BOSS_ENCOUNTER",
		["ISHARMLA_CORRUPTED_HEART"] = "ISHARMLA_CORRUPTED_HEART_BOSS_ENCOUNTER",
		["ISHARMLA_CORRUPTED_HEART_TEST_ENCOUNTER"] = "ISHARMLA_CORRUPTED_HEART_BOSS_ENCOUNTER",
		["FROSTNOVA"] = "FROST_NOVA_WINTER_SCAR_BOSS_ENCOUNTER",
		["FROSTNOVA_TEST"] = "FROST_NOVA_WINTER_SCAR_BOSS_ENCOUNTER",
		["FROST_NOVA"] = "FROST_NOVA_WINTER_SCAR_BOSS_ENCOUNTER",
		["FROST_NOVA_TEST"] = "FROST_NOVA_WINTER_SCAR_BOSS_ENCOUNTER",
		["FROST_NOVA_WINTER_SCAR"] = "FROST_NOVA_WINTER_SCAR_BOSS_ENCOUNTER",
		["FROST_NOVA_WINTER_SCAR_TEST_ENCOUNTER"] = "FROST_NOVA_WINTER_SCAR_BOSS_ENCOUNTER",
		["BOZHOKASTI_SAINTGUARD_GUNNER_TEST_ENCOUNTER"] = "BOZHOKASTI_SAINTGUARD_GUNNER_BOSS_ENCOUNTER",
		["KUILONG_MAHASATTVA_AVATAR_TEST_ENCOUNTER"] = "KUILONG_MAHASATTVA_AVATAR_BOSS_ENCOUNTER",
		["FURNACE_FINALE_AMIYA_TEST_ENCOUNTER"] = "FURNACE_FINALE_AMIYA_ENCOUNTER",
		["MIO"] = "MIO_TEST_ENCOUNTER",
		["MIO_TEST"] = "MIO_TEST_ENCOUNTER",
		["GOPNIK_TEST_ENCOUNTER"] = "BUSINESS_EMPIRE_GOPNIK_ENCOUNTER",
		["SCAVENGER_APOSTLES_TEST_ENCOUNTER"] = "NORTH_WIND_WITCH_SCAVENGER_APOSTLES_ENCOUNTER",
		["SARKAZ_DESCENDANT_HATRED_COLLECTORS_TEST_ENCOUNTER"] =
			"FUTURE_HUNTER_SARKAZ_DESCENDANT_HATRED_COLLECTORS_ENCOUNTER",
		["CALENDAR_KINGS_PINCER_ENCOUNTER"] = "CALENDAR_KINGS_PINCER_BOSS_ENCOUNTER",
		["SORROWFUL_LOCK"] = "SORROWFUL_LOCK_BOSS_ENCOUNTER",
		["LOCK"] = "SORROWFUL_LOCK_BOSS_ENCOUNTER",
		["SORROWFUL_LOCK_TEST_ENCOUNTER"] = "SORROWFUL_LOCK_BOSS_ENCOUNTER"
	};

	public static string Normalize(string entry)
	{
		string normalized = entry.ToUpperInvariant();
		if (ExplicitAliases.TryGetValue(normalized, out string? alias))
		{
			return ResolveExistingEncounterEntry(alias);
		}

		string existing = ResolveExistingEncounterEntry(normalized);
		if (existing != normalized)
		{
			return existing;
		}

		if (normalized.StartsWith(ModPrefix, StringComparison.Ordinal))
		{
			return normalized;
		}

		string prefixed = ModPrefix + normalized;
		return ModelDb.GetByIdOrNull<EncounterModel>(WithEntry(prefixed)) == null ? normalized : prefixed;
	}

	public static IEnumerable<string> GetCompletionEntries()
	{
		HashSet<string> entries = new(StringComparer.OrdinalIgnoreCase);
		foreach (Type type in IntegratedStrategyContentCatalog.EncounterTypes)
		{
			if (!type.IsSubclassOf(typeof(EncounterModel)))
			{
				continue;
			}

			string entry = ModelDb.GetId(type).Entry;
			if (entries.Add(entry))
			{
				yield return entry;
			}

			string unprefixed = RemoveModPrefix(entry);
			if (unprefixed != entry && entries.Add(unprefixed))
			{
				yield return unprefixed;
			}
		}

		foreach (string alias in ExplicitAliases.Keys)
		{
			if (entries.Add(alias))
			{
				yield return alias;
			}
		}
	}

	public static string RemoveModPrefix(string entry)
	{
		return entry.StartsWith(ModPrefix, StringComparison.OrdinalIgnoreCase)
			? entry[ModPrefix.Length..]
			: entry;
	}

	private static ModelId WithEntry(string entry)
	{
		return new ModelId(EncounterCategory.Category, entry);
	}

	private static string ResolveExistingEncounterEntry(string entry)
	{
		if (ModelDb.GetByIdOrNull<EncounterModel>(WithEntry(entry)) != null)
		{
			return entry;
		}

		if (entry.StartsWith(ModPrefix, StringComparison.Ordinal))
		{
			return entry;
		}

		string prefixed = ModPrefix + entry;
		return ModelDb.GetByIdOrNull<EncounterModel>(WithEntry(prefixed)) == null ? entry : prefixed;
	}
}
