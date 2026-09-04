namespace AITeammate.Scripts;

internal enum EventOptionKind
{
    Unknown,
    Leave,
    Proceed,
    GainRelic,
    GainPotion,
    GainGold,
    SpendGold,
    LoseHp,
    LoseMaxHp,
    Heal,
    GainMaxHp,
    CardReward,
    AddFixedCard,
    RemoveCard,
    UpgradeCard,
    TransformCard,
    EnchantCard,
    AddCurse,
    EnterCombat,
    Randomized,
    MultiStep,
    Unsupported
}
