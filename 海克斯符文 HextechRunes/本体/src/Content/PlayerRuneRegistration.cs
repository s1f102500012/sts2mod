namespace HextechRunes;

[Flags]
public enum PlayerRuneFlags
{
	None = 0,
	Disabled = 1,
	AttributeConversionExclusive = 2,
	FirstActExcluded = 4,
	ThirdActExcluded = 8,
	SelectionExcluded = 16,
	// 已从当前内容中删除，但仍注册模型以兼容旧存档；不可见、不可选择、不可配置。
	Retired = 32
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
