namespace AITeammate.Scripts;

internal readonly record struct CombatActionScoreBreakdown(
    int ImmediateDamage,
    int ImmediateDefense,
    int EnemyDebuff,
    int SelfBuff,
    int ResourceSetup,
    int StatusCleanup,
    int KillPotential,
    int CharacterStrategy,
    int TeamCoordination,
    int NonMinionLethal,
    int LagavulinSleep,
    int NoBenefit,
    int EnergyEfficiency)
{
    public int WeightedTotal(AiCombatRiskProfile risk)
    {
        return risk.ApplyAttackWeight(ImmediateDamage) +
               risk.ApplyDefenseWeight(ImmediateDefense) +
               EnemyDebuff +
               SelfBuff +
               ResourceSetup +
               StatusCleanup +
               KillPotential +
               CharacterStrategy +
               TeamCoordination +
               NonMinionLethal +
               LagavulinSleep +
               NoBenefit +
               EnergyEfficiency;
    }

    public string Describe()
    {
        return $"damage={ImmediateDamage} defense={ImmediateDefense} debuff={EnemyDebuff} buff={SelfBuff} setup={ResourceSetup} cleanup={StatusCleanup} kill={KillPotential} character={CharacterStrategy} team={TeamCoordination} nonMinionLethal={NonMinionLethal} lagavulinSleep={LagavulinSleep} noBenefit={NoBenefit} energy={EnergyEfficiency}";
    }
}
