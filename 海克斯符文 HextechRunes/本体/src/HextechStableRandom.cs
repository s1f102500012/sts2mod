using MegaCrit.Sts2.Core.Models.Exceptions;

namespace HextechRunes;

internal static class HextechStableRandom
{
	private const ulong OffsetBasis = 14695981039346656037UL;
	private const ulong Prime = 1099511628211UL;

	public static int Index(RunState runState, int count, params string?[] saltParts)
	{
		if (count <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(count), count, "Cannot choose from an empty pool.");
		}

		return (int)(Hash(runState, saltParts) % (ulong)count);
	}

	public static bool PercentChance(RunState runState, int percent, params string?[] saltParts)
	{
		if (percent <= 0)
		{
			return false;
		}

		if (percent >= 100)
		{
			return true;
		}

		return Index(runState, 100, saltParts) < percent;
	}

	public static int IndexFromRawParts(int count, params string?[] parts)
	{
		if (count <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(count), count, "Cannot choose from an empty pool.");
		}

		return (int)(HashRaw(parts) % (ulong)count);
	}

	public static int PlayerCombatRoundIndex(RunState runState, Player player, int count, int roundNumber)
	{
		return IndexFromRawParts(
			count,
			runState.Rng.StringSeed,
			"|act:",
			runState.CurrentActIndex.ToString(),
			"|floor:",
			runState.TotalFloor.ToString(),
			"|round:",
			roundNumber.ToString(),
			"|player:",
			PlayerKey(player));
	}

	public static ulong HashRaw(params string?[] parts)
	{
		ulong hash = OffsetBasis;
		foreach (string? part in parts)
		{
			Add(ref hash, part);
		}

		return FinalizeHash(hash);
	}

	public static T Pick<T>(IEnumerable<T> candidates, RunState runState, Func<T, string> keySelector, params string?[] saltParts)
	{
		List<(T Item, string Key)> pool = candidates
			.Select(item => (Item: item, Key: keySelector(item)))
			.OrderBy(static item => item.Key, StringComparer.Ordinal)
			.ToList();
		if (pool.Count == 0)
		{
			throw new ArgumentException("Cannot choose from an empty pool.", nameof(candidates));
		}

		string poolKey = string.Join(",", pool.Select(static item => item.Key));
		int index = Index(runState, pool.Count, AppendSalt(saltParts, "pool", poolKey));
		return pool[index].Item;
	}

	public static List<T> PickDistinct<T>(IEnumerable<T> candidates, int count, RunState runState, Func<T, string> keySelector, params string?[] saltParts)
	{
		List<(T Item, string Key)> pool = candidates
			.Select(item => (Item: item, Key: keySelector(item)))
			.OrderBy(static item => item.Key, StringComparer.Ordinal)
			.ToList();
		List<T> selected = new(Math.Min(Math.Max(0, count), pool.Count));
		for (int i = 0; i < count && pool.Count > 0; i++)
		{
			string poolKey = string.Join(",", pool.Select(static item => item.Key));
			int index = Index(runState, pool.Count, AppendSalt(saltParts, "pick", i.ToString(), "pool", poolKey));
			selected.Add(pool[index].Item);
			pool.RemoveAt(index);
		}

		return selected;
	}

	public static CardModel CreateMinionCard(HextechCombatState combatState, Player owner, string source, int ordinal)
	{
		int index = Index((RunState)owner.RunState, 3,
			source,
			PlayerKey(owner),
			"round",
			combatState.RoundNumber.ToString(),
			"ordinal",
			ordinal.ToString());
		return index switch
		{
			0 => combatState.CreateCard<MinionStrike>(owner),
			1 => combatState.CreateCard<MinionDiveBomb>(owner),
			_ => combatState.CreateCard<MinionSacrifice>(owner)
		};
	}

	public static OrbModel CreateOrb(RunState runState, Player owner, string source, int ordinal, int roundNumber)
	{
		int index = Index(runState, 5,
			source,
			PlayerKey(owner),
			"round",
			roundNumber.ToString(),
			"ordinal",
			ordinal.ToString());
		return index switch
		{
			0 => ModelDb.Orb<LightningOrb>().ToMutable(),
			1 => ModelDb.Orb<FrostOrb>().ToMutable(),
			2 => ModelDb.Orb<DarkOrb>().ToMutable(),
			3 => ModelDb.Orb<PlasmaOrb>().ToMutable(),
			_ => ModelDb.Orb<GlassOrb>().ToMutable()
		};
	}

	public static string PlayerKey(Player player)
	{
		RunState runState = (RunState)player.RunState;
		int slot = runState.GetPlayerSlotIndex(player);
		return PlayerIdentityKey(slot, player.NetId);
	}

	internal static string PlayerIdentityKey(int slot, ulong netId)
	{
		return netId != 0UL
			? $"net:{netId}"
			: $"slot:{slot}";
	}

	public static string CardKey(CardModel card)
	{
		return card.Id.Entry;
	}

	public static string PotionKey(PotionModel potion)
	{
		return potion.Id.Entry;
	}

	public static string CardActionKey(CardModel? card)
	{
		if (card == null)
		{
			return "none";
		}

		string ownerKey;
		try
		{
			Player? owner = card.Owner;
			ownerKey = owner == null ? "owner:none" : PlayerKey(owner);
		}
		catch (CanonicalModelException)
		{
			ownerKey = "owner:canonical";
		}

		return string.Join(":",
			CardKey(card),
			ownerKey,
			"play",
			card.CurrentPlayIndex.ToString(),
			"target",
			card.CurrentTarget?.CombatId?.ToString() ?? "none",
			"pile",
			GetPileKey(card));
	}

	public static string TypeModelKey(Type type)
	{
		return ModelDb.GetId(type).Entry;
	}

	public static string CardPileKey(IEnumerable<CardModel> cards)
	{
		return string.Join(",", cards.Select(CardKey));
	}

	private static ulong Hash(RunState runState, IEnumerable<string?> saltParts)
	{
		ulong hash = OffsetBasis;
		Add(ref hash, runState.Rng.StringSeed);
		Add(ref hash, "|act:");
		Add(ref hash, runState.CurrentActIndex.ToString());
		Add(ref hash, "|floor:");
		Add(ref hash, runState.TotalFloor.ToString());
		foreach (string? part in saltParts)
		{
			Add(ref hash, "|");
			Add(ref hash, part ?? "");
		}

		return FinalizeHash(hash);
	}

	private static ulong FinalizeHash(ulong hash)
	{
		unchecked
		{
			hash ^= hash >> 33;
			hash *= 0xff51afd7ed558ccdUL;
			hash ^= hash >> 33;
			hash *= 0xc4ceb9fe1a85ec53UL;
			hash ^= hash >> 33;
			return hash;
		}
	}

	private static string?[] AppendSalt(string?[] saltParts, params string?[] extra)
	{
		string?[] result = new string?[saltParts.Length + extra.Length];
		Array.Copy(saltParts, result, saltParts.Length);
		Array.Copy(extra, 0, result, saltParts.Length, extra.Length);
		return result;
	}

	private static string GetPileKey(CardModel card)
	{
		CardPile? pile = card.Pile;
		if (pile == null)
		{
			return "none";
		}

		IReadOnlyList<CardModel> cards = pile.Cards;
		int index = -1;
		for (int i = 0; i < cards.Count; i++)
		{
			if (ReferenceEquals(cards[i], card))
			{
				index = i;
				break;
			}
		}

		return $"{pile.Type}:{index}";
	}

	private static void Add(ref ulong hash, string? value)
	{
		if (value == null)
		{
			return;
		}

		foreach (char ch in value)
		{
			hash ^= ch;
			hash *= Prime;
		}
	}
}
