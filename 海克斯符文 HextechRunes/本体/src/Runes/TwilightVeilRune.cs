namespace HextechRunes;

/// <summary>
/// 薄暮法衣(黄金,通用):敌人在战斗内获得增益效果时,你获得相同增益效果。
/// 只镜像"战斗内"获得:开场箭闸(首个玩家回合开始才武装)挡掉出生自带/战斗开始时/敌方海克斯开局增益;
/// BOSS 转阶段补发的开局类海克斯增益由 BeginMirrorSuppression 作用域另行压制。
/// 怪物专属机制/形态类 buff(惊惧/硬化甲壳等)不镜像——玩家模型上没有对应动画与脚本。
/// </summary>
public sealed class TwilightVeilRune : HextechRelicBase
{
	private static readonly AsyncLocal<bool> MirrorSuppressed = new();

	private bool _armed;
	private bool _mirroring;

	public override Task BeforeCombatStart()
	{
		_armed = false;
		_mirroring = false;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_armed = false;
		_mirroring = false;
		return Task.CompletedTask;
	}

	public override Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
	{
		// 任意玩家的首个回合开始后才开始镜像:此前的应用(出生自带/战斗开始时/开局海克斯)一律不偷。
		_armed = true;
		return Task.CompletedTask;
	}

	/// <summary>BOSS 转阶段等"开局类"增益补发窗口:期间敌方增益不触发镜像。</summary>
	internal static IDisposable BeginMirrorSuppression()
	{
		return new SuppressionScope();
	}

#if STS2_104_OR_NEWER
	public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
#else
	public override async Task AfterPowerAmountChanged(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
#endif
	{
		if (!_armed
			|| _mirroring
			|| MirrorSuppressed.Value
			|| Owner == null
			|| Owner.Creature.IsDead
			|| amount <= 0m
			|| power.Owner is not Creature enemy
			|| enemy.Side != CombatSide.Enemy
			|| enemy.CombatState == null
			|| enemy.CombatState != Owner.Creature.CombatState
			|| power.GetTypeForAmount(amount) != PowerType.Buff
			|| HextechMonsterInteractionPolicy.IsMonsterMechanismBuff(power))
		{
			return;
		}

		PowerModel mirrored = ModelDb.GetById<PowerModel>(power.Id).ToMutable();
		_mirroring = true;
		try
		{
			Flash();
			await PowerCmd.Apply(mirrored, Owner.Creature, amount, Owner.Creature, null);
		}
		finally
		{
			_mirroring = false;
		}
	}

	private sealed class SuppressionScope : IDisposable
	{
		private readonly bool _previous;

		public SuppressionScope()
		{
			_previous = MirrorSuppressed.Value;
			MirrorSuppressed.Value = true;
		}

		public void Dispose()
		{
			MirrorSuppressed.Value = _previous;
		}
	}
}
