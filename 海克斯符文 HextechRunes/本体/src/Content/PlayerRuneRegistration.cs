namespace HextechRunes;

[Flags]
public enum PlayerRuneFlags
{
	None = 0,
	Disabled = 1,
	AttributeConversionExclusive = 2,
	FirstActExcluded = 4,
	ThirdActExcluded = 8,
	SelectionExcluded = 16
}

public enum PlayerRuneCharacterPool
{
	Ironclad,
	Silent,
	Regent,
	Defect,
	Necrobinder
}

public readonly record struct PlayerRuneRegistration(
	Type Type,
	HextechRarityTier Rarity,
	PlayerRuneFlags Flags = PlayerRuneFlags.None,
	PlayerRuneCharacterPool? CharacterPool = null,
	int CharacterOrder = 0,
	string TagKey = "COMPREHENSIVE");
