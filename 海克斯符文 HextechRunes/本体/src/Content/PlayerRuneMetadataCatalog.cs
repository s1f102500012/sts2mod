namespace HextechRunes;

internal sealed class PlayerRuneMetadataCatalog
{
	private readonly IReadOnlyList<PlayerRuneRegistration> _registrations;
	private readonly IReadOnlyDictionary<Type, PlayerRuneRegistration> _registrationByType;

	public PlayerRuneMetadataCatalog(IReadOnlyList<PlayerRuneRegistration> registrations)
	{
		_registrations = registrations.ToArray();
		_registrationByType = _registrations.ToDictionary(static registration => registration.Type);
		TypesByRarity = Enum.GetValues<HextechRarityTier>()
			.ToDictionary(static rarity => rarity, GetTypesForRarity);
		TypesByCharacter = Enum.GetValues<PlayerRuneCharacterPool>()
			.ToDictionary(static characterPool => characterPool, GetTypesForCharacter);
		TypesByFlag = Enum.GetValues<PlayerRuneFlags>()
			.Where(static flag => flag != PlayerRuneFlags.None)
			.ToDictionary(static flag => flag, GetTypesWithFlag);
		TagKeys = _registrations.ToDictionary(static registration => registration.Type, static registration => registration.TagKey);
		AllTypes = Enum.GetValues<HextechRarityTier>()
			.SelectMany(rarity => TypesByRarity[rarity])
			.Distinct()
			.ToArray();
	}

	public IReadOnlyList<PlayerRuneRegistration> Registrations => _registrations;

	public IReadOnlyList<Type> AllTypes { get; }

	public IReadOnlyDictionary<HextechRarityTier, IReadOnlyList<Type>> TypesByRarity { get; }

	public IReadOnlyDictionary<PlayerRuneCharacterPool, IReadOnlyList<Type>> TypesByCharacter { get; }

	public IReadOnlyDictionary<PlayerRuneFlags, IReadOnlyList<Type>> TypesByFlag { get; }

	public IReadOnlyDictionary<Type, string> TagKeys { get; }

	public bool IsRegistered(Type runeType)
	{
		return _registrationByType.ContainsKey(runeType);
	}

	public bool TryGetRegistration(Type runeType, out PlayerRuneRegistration registration)
	{
		return _registrationByType.TryGetValue(runeType, out registration);
	}

	public PlayerRuneRegistration GetRegistration(Type runeType)
	{
		return _registrationByType[runeType];
	}

	public bool IsVisible(Type runeType)
	{
		return IsRegistered(runeType)
			&& !HasFlag(runeType, PlayerRuneFlags.Disabled);
	}

	public bool IsSelectable(Type runeType)
	{
		return IsVisible(runeType) && !HasFlag(runeType, PlayerRuneFlags.SelectionExcluded);
	}

	public bool IsConfigurable(Type runeType)
	{
		return IsRegistered(runeType)
			&& !HasFlag(runeType, PlayerRuneFlags.SelectionExcluded);
	}

	public bool HasFlag(Type runeType, PlayerRuneFlags flag)
	{
		return _registrationByType.TryGetValue(runeType, out PlayerRuneRegistration registration)
			&& (registration.Flags & flag) != 0;
	}

	public bool TryGetRarity(Type runeType, out HextechRarityTier rarity)
	{
		if (_registrationByType.TryGetValue(runeType, out PlayerRuneRegistration registration))
		{
			rarity = registration.Rarity;
			return true;
		}

		rarity = default;
		return false;
	}

	public int GetRaritySortOrder(Type runeType)
	{
		return TryGetRarity(runeType, out HextechRarityTier rarity)
			? rarity switch
			{
				HextechRarityTier.Silver => 0,
				HextechRarityTier.Gold => 1,
				HextechRarityTier.Prismatic => 2,
				_ => 3
			}
			: 3;
	}

	public string GetTagKey(Type runeType)
	{
		return _registrationByType.TryGetValue(runeType, out PlayerRuneRegistration registration)
			? registration.TagKey
			: HextechPlayerRuneRegistry.DefaultTagKey;
	}

	public IReadOnlyList<Type> GetTypesForRarity(HextechRarityTier rarity)
	{
		return _registrations
			.Where(registration => registration.Rarity == rarity)
			.Select(static registration => registration.Type)
			.ToArray();
	}

	public IReadOnlyList<Type> GetSelectableTypesForRarity(HextechRarityTier rarity)
	{
		return TypesByRarity.TryGetValue(rarity, out IReadOnlyList<Type>? runeTypes)
			? runeTypes.Where(IsSelectable).ToArray()
			: Array.Empty<Type>();
	}

	public IReadOnlyList<Type> GetConfigurableTypesForRarity(HextechRarityTier rarity)
	{
		return TypesByRarity.TryGetValue(rarity, out IReadOnlyList<Type>? runeTypes)
			? runeTypes.Where(IsConfigurable).ToArray()
			: Array.Empty<Type>();
	}

	public IReadOnlyList<Type> GetTypesForCharacter(PlayerRuneCharacterPool characterPool)
	{
		return _registrations
			.Where(registration => registration.CharacterPool == characterPool)
			.OrderBy(static registration => registration.CharacterOrder)
			.Select(static registration => registration.Type)
			.ToArray();
	}

	public IReadOnlyList<Type> GetTypesWithFlag(PlayerRuneFlags flag)
	{
		return _registrations
			.Where(registration => (registration.Flags & flag) != 0)
			.Select(static registration => registration.Type)
			.ToArray();
	}

	public IReadOnlySet<Type> GetCharacterSpecificTypes()
	{
		return _registrations
			.Where(static registration => registration.CharacterPool.HasValue)
			.Select(static registration => registration.Type)
			.ToHashSet();
	}
}
