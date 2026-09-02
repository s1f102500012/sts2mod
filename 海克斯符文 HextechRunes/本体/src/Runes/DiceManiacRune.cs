namespace HextechRunes;

public sealed class DiceManiacRune : HextechRelicBase, IHextechSharedCombatVictoryRune
{
	private const int SilverForgeWeight = 65;
	private const int GoldForgeWeight = 25;
	private const int PrismaticForgeWeight = 10;
	internal const int ForgeRarityMultiplier = 2;
	internal const int BaseDropChance = 50;
	internal const int DropChanceStep = 10;

	// 药水掉落式动态掉率:掉一次降 10%,没掉一次升 10%。只存相对 50% 的偏移,文案与图标不变。
	private int _dropChanceOffset;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	private int SavedDiceManiacForgeChanceOffset
	{
		get => _dropChanceOffset;
		set => _dropChanceOffset = HextechDynamicDropChance.ClampOffset(value, BaseDropChance);
	}

	internal int CurrentDropChance => HextechDynamicDropChance.CurrentChance(_dropChanceOffset, BaseDropChance);

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("DropChance", BaseDropChance),
		new DynamicVar("ForgeMultiplier", 2m)
	];

	public override Task AfterCombatVictory(CombatRoom room)
	{
		if (IsNetworkMultiplayer())
		{
			return Task.CompletedTask;
		}

		return ApplySharedCombatVictory(room);
	}

	public Task ApplySharedCombatVictory(CombatRoom room)
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		bool dropped = HextechStableRandom.PercentChance(
			(RunState)Owner.RunState,
			CurrentDropChance,
			"dice-maniac-forge-reward",
			HextechStableRandom.PlayerKey(Owner),
			Owner.Relics.Count.ToString());
		_dropChanceOffset = HextechDynamicDropChance.NextOffset(_dropChanceOffset, BaseDropChance, DropChanceStep, dropped);
		if (!dropped)
		{
			return Task.CompletedTask;
		}

		Flash(Array.Empty<Creature>());
		HextechForgeGrantHelper.AddWeightedRandomForgeReward(
			Owner,
			room,
			"dice-maniac-random-forge-reward",
			SilverForgeWeight,
			GoldForgeWeight,
			PrismaticForgeWeight);
		return Task.CompletedTask;
	}
}
