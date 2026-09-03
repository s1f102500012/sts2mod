using HextechRunes;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HextechRunesSponsorPack;

/// <summary>
/// 是胃炎(赞助者物品,棱彩):对当前生命值高于你的敌人造成伤害时,伤害提高 50%,
/// 且每次造成此类伤害后回复 2 点生命并抽 1 张牌。
/// 血量比较取各自结算时点的当前值(倍率在结算前判,回复/抽牌在结算后判)。
/// </summary>
public sealed class GastritisRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("DamagePercent", 50m),
		new DynamicVar("Heal", 2m),
		new CardsVar(1)
	];

	public override decimal ModifyDamageMultiplicativeCompat(
		Creature? target,
		decimal amount,
		ValueProp props,
		Creature? dealer,
		CardModel? cardSource)
	{
		if (!IsDamageFromOwnerToEnemyOrPreview(target, dealer, cardSource)
			|| Owner == null
			|| target == null
			|| target.CurrentHp <= Owner.Creature.CurrentHp)
		{
			return 1m;
		}

		return 1m + DynamicVars["DamagePercent"].BaseValue / 100m;
	}

	public override async Task AfterDamageGiven(
		PlayerChoiceContext choiceContext,
		Creature? dealer,
		DamageResult result,
		ValueProp props,
		Creature target,
		CardModel? cardSource)
	{
		if (Owner == null
			|| Owner.Creature.IsDead
			|| target.Side != CombatSide.Enemy
			|| result.TotalDamage <= 0
			|| target.CurrentHp <= Owner.Creature.CurrentHp
			|| !IsDamageFromOwner(dealer, cardSource))
		{
			return;
		}

		Flash();
		await CreatureCmd.Heal(Owner.Creature, DynamicVars["Heal"].BaseValue);
		await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner, fromHandDraw: false);
	}
}
