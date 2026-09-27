namespace HextechRunes;

/// <summary>
/// 「升级：某张卡」系列符文的共用基类:每次打出指定的那张卡(<typeparamref name="TCard"/>)之后,让这张卡在本局游戏里
/// 永久提升自己的伤害(以及可选的格挡),逐张卡实例独立累加、持久存档。具体累加逻辑见 <see cref="HextechSelfUpgradeCardStore"/>。
/// </summary>
public abstract class SelfUpgradeOnPlayRuneBase<TCard> : CardUpgradeRuneBase<TCard>
	where TCard : CardModel
{
	// 这两个哑 [SavedProperty] 属性唯一作用:把 HextechSelfUpgradeCardStore 写进卡牌 Props 的两个键名注册进
	// SavedPropertiesTypeCache。符文是 AbstractModel、会被 InjectModelType 自动注入,这条注册路径被 BaseLib 支持。
	// 属性本身恒为 0、写入忽略,不承载任何数据(真正的逐卡计数写在被打出那张卡自己的 Props 里)。
	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	protected int HextechSelfUpgradeDamageBonus
	{
		get => 0;
		set { }
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	protected int HextechSelfUpgradeBlockBonus
	{
		get => 0;
		set { }
	}

	/// <summary>每次打出本卡后,这张卡永久增加的伤害白值。</summary>
	protected abstract int DamagePerPlay { get; }

	/// <summary>每次打出本卡后,这张卡永久增加的格挡白值(默认 0,不加格挡)。</summary>
	protected virtual int BlockPerPlay => 0;

	// 泛型模型类的钩子只转发,不直接用类型参数(第三方批量补丁会把它写死,见 AutoPlayFormsAtCombatStartRuneBase)。
	public sealed override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		return RecordSelfUpgradeOnPlay(cardPlay);
	}

	private Task RecordSelfUpgradeOnPlay(CardPlay cardPlay)
	{
		if (Owner == null || cardPlay.Card.Owner != Owner || cardPlay.Card is not TCard)
		{
			return Task.CompletedTask;
		}

		Flash();
		if (DamagePerPlay != 0)
		{
			HextechSelfUpgradeCardStore.AddDamageOnPlay(cardPlay.Card, DamagePerPlay);
		}

		if (BlockPerPlay != 0)
		{
			HextechSelfUpgradeCardStore.AddBlockOnPlay(cardPlay.Card, BlockPerPlay);
		}

		return Task.CompletedTask;
	}
}
