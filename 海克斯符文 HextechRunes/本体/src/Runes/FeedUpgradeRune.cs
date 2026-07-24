namespace HextechRunes;

// 升级：狂宴(仅战士) —— 用狂宴(Feed)斩杀敌人时,额外获得 15% 最大生命加成。
// 同一符文内按 15% 线性叠加；作为独立乘区与其他生命符文及生命锻造器相乘。
public sealed class FeedUpgradeRune : CardUpgradeRuneBase<Feed>, IHextechMaxHpScalingRune
{
	private const decimal BonusMaxHpPercent = 0.15m;

	private int _baseMaxHp;
	private int _bonusGranted;
	private int _stacks;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedBaseMaxHp
	{
		get => _baseMaxHp;
		set => _baseMaxHp = Math.Max(0, value);
	}

	public int BaseMaxHp
	{
		get => _baseMaxHp;
		set => _baseMaxHp = Math.Max(1, value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedFeedBonusGranted
	{
		get => _bonusGranted;
		set => _bonusGranted = Math.Max(0, value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedStacks
	{
		get => _stacks;
		set => _stacks = Math.Max(0, value);
	}

	public decimal MaxHpScale => 1m + _stacks * BonusMaxHpPercent;

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<Feed>()
	];

	protected override bool IsAvailableForCharacter(Player player) => IsIroncladPlayer(player);

	public override Task AfterRoomEntered(AbstractRoom room)
	{
		if (Owner != null)
		{
			MigrateLegacyStackCount();
			IHextechMaxHpBaseHolder primary = HextechMaxHpScaling.GetPrimary(Owner) ?? this;
			HextechMaxHpScaling.EnsureBaseInitialized(Owner, primary, assumeAlreadyScaled: true);
		}

		return Task.CompletedTask;
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (Owner == null
			|| Owner.Creature.IsDead
			|| cardPlay.Card.Owner != Owner
			|| cardPlay.Card is not Feed
			|| cardPlay.Target is not { IsDead: true })
		{
			return;
		}

		MigrateLegacyStackCount();
		IHextechMaxHpBaseHolder primary = HextechMaxHpScaling.GetPrimary(Owner) ?? this;
		HextechMaxHpScaling.EnsureBaseInitialized(Owner, primary, assumeAlreadyScaled: true);
		SavedStacks++;
		Flash();
		await HextechMaxHpScaling.ReapplyScale(Owner);
	}

	private void MigrateLegacyStackCount()
	{
		if (_stacks > 0 || _bonusGranted <= 0 || Owner == null)
		{
			return;
		}

		decimal legacyBaseMaxHp = Math.Max(1m, Owner.Creature.MaxHp - _bonusGranted);
		decimal legacyBonusPerStack = Math.Max(1m, Math.Floor(legacyBaseMaxHp * BonusMaxHpPercent));
		_stacks = Math.Max(1, (int)Math.Round(_bonusGranted / legacyBonusPerStack, MidpointRounding.AwayFromZero));
		if (HextechMaxHpScaling.GetPrimary(Owner) is { BaseMaxHp: > 0 } primary)
		{
			primary.BaseMaxHp = Math.Max(1, (int)Math.Floor(Owner.Creature.MaxHp / HextechMaxHpScaling.GetScale(Owner)));
		}
	}
}
