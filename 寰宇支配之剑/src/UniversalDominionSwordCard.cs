using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace UniversalDominionSword;

/// <summary>
/// 抹杀:0 费单体攻击。剥去目标的全部能力后造成上限伤害;目标挨了这一刀却仍未消散时,立即结算敌方回合结束并对
/// 所有敌人再挥一次,至多十七刀。每次打出,本局游戏中的基础耗能永久 +1。
/// </summary>
/// <remarks>
/// 伤害走原版命令。先剥能力,是因为原版里所有"死后不离场、轮到自己再站起来"的存活手段(重新接合、适应、寄生之类)
/// 都是挂在身上的能力,能力没了,死亡就是普通死亡,尸体会正常离场。"再挥一次"之前先结算敌方回合结束,是因为一切
/// "等回合结束再说"的存活手段(无实体倒计时、按回合分段的形态、回合末才推进的阶段)都在那一刻结算——抹杀连这点
/// 喘息也不给。普通敌人第一刀就消散,后面的循环不会跑。
/// </remarks>
public sealed partial class UniversalDominionSwordCard : CardModel
{
	// 原版 Creature.LoseHpInternal 对单次掉血的钳制上限,伤害数字也按它显示。
	private const int EraseDamage = 999_999_999;

	private const int MaximumEnergyCost = 999_999_999;

	// 循环封顶。首刀之后最多再挥十六次,远超任何按回合分段的存活机制所需。
	private const int MaximumStrikes = 17;

	// 与原版"失去生命"效果同一组标志:Unblockable 无视格挡,Unpowered 不吃力量/易伤等能力修正。
	private const ValueProp EraseProps = ValueProp.Unblockable | ValueProp.Unpowered;

	private int _permanentCostIncrease;

	public override CardPoolModel Pool => IsMutable && Owner != null
		? Owner.Character.CardPool
		: ModelDb.CardPool<TokenCardPool>();

	public override CardPoolModel VisualCardPool => Pool;

	public override string PortraitPath => ModInfo.CardPortraitPath;

	public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

	// 属性名集合决定联机 net-id 布局:改名/增删都是不兼容变更,须随版本号发布并写进更新日志。
	[SavedProperty]
	public int PermanentCostIncrease
	{
		get => _permanentCostIncrease;
		set
		{
			AssertMutable();
			_permanentCostIncrease = Math.Clamp(value, 0, MaximumEnergyCost);
			EnergyCost.SetCustomBaseCost(Math.Min(
				EnergyCost.Canonical + _permanentCostIncrease,
				MaximumEnergyCost));
		}
	}

	public UniversalDominionSwordCard()
		: base(0, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy, shouldShowInCardLibrary: true)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		Creature? target = cardPlay.Target;
		if (target == null)
		{
			Log.Warn($"[{ModInfo.Id}] Erasure was played without a target; nothing happens.");
			return;
		}

		await CreatureCmd.TriggerAnim(
			Owner.Creature,
			"Attack",
			Owner.Character.AttackAnimDelay);

		// 先写共享状态(耗能),再进伤害管线。
		IncreasePermanentCost();
		if (DeckVersion is UniversalDominionSwordCard deckVersion)
		{
			deckVersion.IncreasePermanentCost();
		}

		ICombatState? combatState = CombatState ?? target.CombatState;
		IReadOnlyList<Creature> victims = [target];
		for (int strike = 1; ; strike++)
		{
			foreach (Creature victim in victims)
			{
				await StripPowers(victim);
			}

			List<DamageResult> results = (await CreatureCmd.Damage(
				choiceContext,
				victims,
				EraseDamage,
				EraseProps,
				Owner.Creature)).ToList();

			bool erased = results.Any(result => ReferenceEquals(result.Receiver, target) && result.WasTargetKilled);
			int dealt = results.Sum(result => result.UnblockedDamage);
			if (erased || dealt <= 0 || strike >= MaximumStrikes || combatState == null)
			{
				break;
			}

			// 目标挨了一刀却还站着:它在等回合结束。那就现在结算敌方回合结束,再对场上所有敌人挥剑。
			await ResolveEnemyTurnEnd(combatState);
			victims = combatState.HittableEnemies.Where(creature => creature.IsAlive).ToList();
			if (victims.Count == 0)
			{
				break;
			}
		}
	}

	protected override void OnUpgrade()
	{
	}

	private void IncreasePermanentCost()
	{
		PermanentCostIncrease = Math.Min(
			PermanentCostIncrease + 1,
			MaximumEnergyCost);
	}

	// 逐个走原版移除命令;移除失败的能力保持原状。
	private static async Task StripPowers(Creature victim)
	{
		foreach (PowerModel power in victim.Powers.ToList())
		{
			await PowerCmd.Remove(power);
		}
	}
}
