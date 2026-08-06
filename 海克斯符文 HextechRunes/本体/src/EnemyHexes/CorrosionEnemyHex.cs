namespace HextechRunes;

internal sealed class CorrosionEnemyHex : HextechEnemyHexEffect
{
	internal const int FrailAmount = 1;

	internal override MonsterHexKind Kind => MonsterHexKind.Corrosion;

	internal override async Task AfterEnemyDamageGivenImmediate(HextechEnemyHexContext context, Creature dealer, DamageResult result, Creature target, CardModel? cardSource)
	{
		if (!ShouldApplyFrail(result.UnblockedDamage, target.Player != null))
		{
			return;
		}

		await PowerCmd.Apply<FrailPower>(target, FrailAmount, dealer, cardSource);
	}

	internal static bool ShouldApplyFrail(decimal unblockedDamage, bool targetIsPlayer)
	{
		return unblockedDamage > 0m && targetIsPlayer;
	}
}
