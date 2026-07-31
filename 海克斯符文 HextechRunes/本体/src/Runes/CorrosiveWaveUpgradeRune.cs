namespace HextechRunes;

public sealed class CorrosiveWaveUpgradeRune : CardUpgradeRuneBase<CorrosiveWave>
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<CorrosiveWave>(),
		HoverTipFactory.FromPower<CorrosiveWavePower>()
	];

	protected override bool IsAvailableForCharacter(Player player) => IsSilentPlayer(player);
}
