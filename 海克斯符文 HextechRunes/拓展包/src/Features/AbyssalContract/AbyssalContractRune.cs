using HextechRunes;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HextechRunesSponsorPack;

// 枚举值参与 SavedContract 的持久化与联机同步,append-only,不要重排数值。
public enum AbyssalContractKind
{
	None = 0,
	Warrior = 1,
	Hunter = 2,
	Regent = 3,
	Necrobinder = 4,
	Automaton = 5
}

// 符文本身只做三件事:持有 [SavedProperty]、在获得时跑选择流程、把每个 hook 转发给对应契约策略。
// 五种契约的行为在 Features/AbyssalContract/ 下,每种一个无状态策略类。
public sealed class AbyssalContractRune : HextechRelicBase
{
	internal const int WarriorInitialStrength = 5;
	internal const int HunterSnakebiteCount = 3;
	internal const int HunterSkillInterval = 2;
	internal const int HunterCombatInterval = 3;
	internal const int HunterSnakebiteCostReduction = 1;
	internal const int NecrobinderDebuffApplications = 5;
	internal const int AutomatonOrbSlotBonus = 99;
	internal const int AutomatonDamagePerOrb = 1;

	private static readonly IReadOnlyDictionary<AbyssalContractKind, IAbyssalContract> Strategies =
		new Dictionary<AbyssalContractKind, IAbyssalContract>
		{
			[AbyssalContractKind.Warrior] = new WarriorContract(),
			[AbyssalContractKind.Hunter] = new HunterContract(),
			[AbyssalContractKind.Regent] = new RegentContract(),
			[AbyssalContractKind.Necrobinder] = new NecrobinderContract(),
			[AbyssalContractKind.Automaton] = new AutomatonContract()
		};

	private AbyssalContractKind _contract;
	private int _warriorEliteKills;
	private int _warriorStrengthBonuses;
	private int _hunterCompletedCombats;

	// 每场战斗的临时计数,不入存档;猎人契约读写,战斗开始与结束时无条件清零。
	internal int HunterSkillsPlayedThisCombat;
	internal bool AutoPlayingSnakebite;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedContract
	{
		get => (int)_contract;
		set => _contract = Enum.IsDefined(typeof(AbyssalContractKind), value)
			? (AbyssalContractKind)value
			: AbyssalContractKind.None;
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedWarriorEliteKills
	{
		get => _warriorEliteKills;
		set => _warriorEliteKills = Math.Max(0, value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedWarriorStrengthBonuses
	{
		get => _warriorStrengthBonuses;
		set => _warriorStrengthBonuses = Math.Max(0, value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedHunterCompletedCombats
	{
		get => _hunterCompletedCombats;
		set => _hunterCompletedCombats = Math.Max(0, value) % HunterCombatInterval;
	}

	public override bool HasUponPickupEffect => true;

	internal AbyssalContractKind Contract => _contract;

	private IAbyssalContract? Strategy =>
		Strategies.TryGetValue(_contract, out IAbyssalContract? strategy) ? strategy : null;

	// 未签约时把五种契约的提示全列出来,签约后只留自己那一条。
	protected override IEnumerable<IHoverTip> ExtraHoverTips => Strategy?.ExtraHoverTips
		??
		[
			.. HoverTipFactory.FromRelic<WarriorContractChoiceRelic>(),
			.. HoverTipFactory.FromRelic<HunterContractChoiceRelic>(),
			.. HoverTipFactory.FromRelic<RegentContractChoiceRelic>(),
			.. HoverTipFactory.FromRelic<NecrobinderContractChoiceRelic>(),
			.. HoverTipFactory.FromRelic<AutomatonContractChoiceRelic>()
		];

	public override async Task AfterObtained()
	{
		if (Owner == null || _contract != AbyssalContractKind.None)
		{
			return;
		}

		IReadOnlyList<RelicModel> choices =
		[
			ModelDb.Relic<WarriorContractChoiceRelic>(),
			ModelDb.Relic<HunterContractChoiceRelic>(),
			ModelDb.Relic<RegentContractChoiceRelic>(),
			ModelDb.Relic<NecrobinderContractChoiceRelic>(),
			ModelDb.Relic<AutomatonContractChoiceRelic>()
		];
		RelicModel? selected = await HextechRunesApi.SelectRelicOption(
			Owner,
			choices,
			"abyssal-contract-choice");
		AbyssalContractKind contract = GetContractKindForChoice(selected);
		if (contract == AbyssalContractKind.None)
		{
			return;
		}

		SavedContract = (int)contract;
		Flash();
		if (Strategy is IAbyssalContract strategy)
		{
			await strategy.ApplyInitialEffect(this);
		}
	}

	public override Task AfterRemoved()
	{
		if (Owner == null)
		{
			return Task.CompletedTask;
		}

		return Strategy?.AfterRemoved(this) ?? Task.CompletedTask;
	}

	public override Task BeforeCombatStart()
	{
		HunterSkillsPlayedThisCombat = 0;
		AutoPlayingSnakebite = false;
		if (Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		return Strategy?.BeforeCombatStart(this) ?? Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		HunterSkillsPlayedThisCombat = 0;
		AutoPlayingSnakebite = false;
		return Task.CompletedTask;
	}

	public override Task AfterCombatVictory(CombatRoom room)
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		return Strategy?.AfterCombatVictory(this, room) ?? Task.CompletedTask;
	}

	public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		return Strategy?.AfterCardPlayed(this, choiceContext, cardPlay) ?? Task.CompletedTask;
	}

	public override Task BeforeSideTurnStart(
		PlayerChoiceContext choiceContext,
		CombatSide side,
		HextechCombatState combatState)
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		return Strategy?.BeforeSideTurnStart(this, choiceContext, side, combatState) ?? Task.CompletedTask;
	}

	public override Task BeforeTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		return Strategy?.BeforeTurnEnd(this, choiceContext, side) ?? Task.CompletedTask;
	}

	public override bool ShouldAddToDeck(CardModel card)
	{
		return Strategy?.ShouldAddToDeck(this, card) ?? true;
	}

	public override Task AfterAddToDeckPrevented(CardModel card)
	{
		return Strategy?.AfterAddToDeckPrevented(this, card) ?? Task.CompletedTask;
	}

	public override bool TryModifyEnergyCostInCombat(
		CardModel card,
		decimal originalCost,
		out decimal modifiedCost)
	{
		modifiedCost = originalCost;
		IAbyssalContract? strategy = Strategy;
		return strategy != null
			&& strategy.TryModifyEnergyCostInCombat(this, card, originalCost, out modifiedCost);
	}

	internal bool HasContract(AbyssalContractKind contract)
	{
		return _contract == contract;
	}

	// 契约赠卡走本体的「战斗中入手、战斗外入牌组」通道;策略类看不到 protected 成员,由符文转发。
	internal Task AddContractCards<TCard>(int count, Action<CardModel>? configureCard = null)
		where TCard : CardModel
	{
		return AddCardCopiesToDeckOrHand<TCard>(count, configureCard);
	}

	internal static bool IsWarriorForbiddenCardType(CardType cardType)
	{
		return cardType is CardType.Skill or CardType.Power;
	}

	internal static bool AdvanceWarriorEliteProgress(ref int eliteKills, ref int strengthBonuses)
	{
		eliteKills = Math.Max(0, eliteKills) + 1;
		strengthBonuses = Math.Max(0, strengthBonuses);
		int requiredKills = 1 + strengthBonuses;
		if (eliteKills < requiredKills)
		{
			return false;
		}

		eliteKills -= requiredKills;
		strengthBonuses++;
		return true;
	}

	internal static AbyssalContractKind GetContractKindForChoice(RelicModel? selected)
	{
		return selected switch
		{
			WarriorContractChoiceRelic => AbyssalContractKind.Warrior,
			HunterContractChoiceRelic => AbyssalContractKind.Hunter,
			RegentContractChoiceRelic => AbyssalContractKind.Regent,
			NecrobinderContractChoiceRelic => AbyssalContractKind.Necrobinder,
			AutomatonContractChoiceRelic => AbyssalContractKind.Automaton,
			_ => AbyssalContractKind.None
		};
	}

	internal static Type? GetStarterUpgradeType(Type characterType)
	{
		if (characterType == typeof(Ironclad))
		{
			return typeof(BlackBlood);
		}
		if (characterType == typeof(Silent))
		{
			return typeof(RingOfTheDrake);
		}
		if (characterType == typeof(Regent))
		{
			return typeof(DivineDestiny);
		}
		if (characterType == typeof(Necrobinder))
		{
			return typeof(PhylacteryUnbound);
		}
		if (characterType == typeof(Defect))
		{
			return typeof(InfusedCore);
		}
		return null;
	}
}
