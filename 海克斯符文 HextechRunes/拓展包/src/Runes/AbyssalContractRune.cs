using HextechRunesSponsorPack;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HextechRunes;

public enum AbyssalContractKind
{
	None = 0,
	Warrior = 1,
	Hunter = 2,
	Regent = 3,
	Necrobinder = 4,
	Automaton = 5
}

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

	private AbyssalContractKind _contract;
	private int _warriorEliteKills;
	private int _warriorStrengthBonuses;
	private int _hunterCompletedCombats;
	private int _hunterSkillsPlayedThisCombat;
	private bool _autoPlayingSnakebite;

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

	protected override IEnumerable<IHoverTip> ExtraHoverTips => _contract switch
	{
		AbyssalContractKind.Warrior => HoverTipFactory.FromRelic<WarriorContractChoiceRelic>(),
		AbyssalContractKind.Hunter => HoverTipFactory.FromRelic<HunterContractChoiceRelic>(),
		AbyssalContractKind.Regent => HoverTipFactory.FromRelic<RegentContractChoiceRelic>(),
		AbyssalContractKind.Necrobinder => HoverTipFactory.FromRelic<NecrobinderContractChoiceRelic>(),
		AbyssalContractKind.Automaton => HoverTipFactory.FromRelic<AutomatonContractChoiceRelic>(),
		_ =>
		[
			.. HoverTipFactory.FromRelic<WarriorContractChoiceRelic>(),
			.. HoverTipFactory.FromRelic<HunterContractChoiceRelic>(),
			.. HoverTipFactory.FromRelic<RegentContractChoiceRelic>(),
			.. HoverTipFactory.FromRelic<NecrobinderContractChoiceRelic>(),
			.. HoverTipFactory.FromRelic<AutomatonContractChoiceRelic>()
		]
	};

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
		await ApplyInitialContractEffect();
	}

	public override async Task AfterRemoved()
	{
		if (Owner == null || _contract != AbyssalContractKind.Automaton)
		{
			return;
		}

		Owner.BaseOrbSlotCount = Math.Max(0, Owner.BaseOrbSlotCount - AutomatonOrbSlotBonus);
		if (Owner.PlayerCombatState != null && Owner.Creature.CombatState != null)
		{
			Owner.PlayerCombatState.OrbQueue.RemoveCapacity(AutomatonOrbSlotBonus);
		}
		await Task.CompletedTask;
	}

	public override async Task BeforeCombatStart()
	{
		_hunterSkillsPlayedThisCombat = 0;
		_autoPlayingSnakebite = false;
		if (Owner == null
			|| Owner.Creature.IsDead
			|| _contract != AbyssalContractKind.Warrior)
		{
			return;
		}

		Flash();
		await PowerCmd.Apply<StrengthPower>(
			Owner.Creature,
			WarriorInitialStrength + _warriorStrengthBonuses,
			Owner.Creature,
			null);
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_hunterSkillsPlayedThisCombat = 0;
		_autoPlayingSnakebite = false;
		return Task.CompletedTask;
	}

	public override async Task AfterCombatVictory(CombatRoom room)
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return;
		}

		if (_contract == AbyssalContractKind.Warrior && room.RoomType == RoomType.Elite)
		{
			int eliteKills = _warriorEliteKills;
			int strengthBonuses = _warriorStrengthBonuses;
			bool gainedStrength = AdvanceWarriorEliteProgress(ref eliteKills, ref strengthBonuses);
			SavedWarriorEliteKills = eliteKills;
			SavedWarriorStrengthBonuses = strengthBonuses;
			if (gainedStrength)
			{
				Flash();
			}
			return;
		}

		if (_contract != AbyssalContractKind.Hunter)
		{
			return;
		}

		SavedHunterCompletedCombats = _hunterCompletedCombats + 1;
		if (_hunterCompletedCombats != 0)
		{
			return;
		}

		await TransformRandomCardIntoSnakebite();
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (Owner == null
			|| Owner.Creature.IsDead
			|| _contract != AbyssalContractKind.Hunter
			|| _autoPlayingSnakebite
			|| cardPlay.Card.Owner != Owner
			|| cardPlay.Card.Type != CardType.Skill)
		{
			return;
		}

		_hunterSkillsPlayedThisCombat++;
		if (_hunterSkillsPlayedThisCombat < HunterSkillInterval)
		{
			return;
		}

		_hunterSkillsPlayedThisCombat -= HunterSkillInterval;
		Snakebite? snakebite = Owner.PlayerCombatState?.AllCards
			.OfType<Snakebite>()
			.FirstOrDefault(static card => card.Pile?.Type is PileType.Hand or PileType.Draw or PileType.Discard);
		if (snakebite == null)
		{
			return;
		}

		_autoPlayingSnakebite = true;
		try
		{
			Flash();
			await CardPileCmd.Add(snakebite, PileType.Play);
			await CardCmd.AutoPlay(choiceContext, snakebite, null);
		}
		finally
		{
			_autoPlayingSnakebite = false;
		}
	}

	public override async Task BeforeSideTurnStart(
		PlayerChoiceContext choiceContext,
		CombatSide side,
		HextechCombatState combatState)
	{
		if (Owner == null
			|| Owner.Creature.IsDead
			|| _contract != AbyssalContractKind.Necrobinder
			|| side != Owner.Creature.Side)
		{
			return;
		}

		IReadOnlyList<Creature> targets = combatState.Creatures
			.Where(static creature => creature.IsAlive && creature.CanReceivePowers)
			.ToArray();
		if (targets.Count == 0)
		{
			return;
		}

		Flash(targets);
		foreach (Creature target in targets)
		{
			for (int i = 0; i < NecrobinderDebuffApplications; i++)
			{
				await ApplyRandomDebuff(choiceContext, target);
			}
		}
	}

	public override async Task BeforeTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
	{
		if (Owner == null
			|| Owner.Creature.IsDead
			|| _contract != AbyssalContractKind.Automaton
			|| side != Owner.Creature.Side
			|| Owner.PlayerCombatState == null)
		{
			return;
		}

		int orbCount = Owner.PlayerCombatState.OrbQueue.Orbs.Count;
		if (orbCount <= 0)
		{
			return;
		}

		Flash([Owner.Creature]);
		await CreatureCmd.Damage(
			choiceContext,
			Owner.Creature,
			orbCount * AutomatonDamagePerOrb,
			ValueProp.Unpowered,
			Owner.Creature);
	}

	public override bool ShouldAddToDeck(CardModel card)
	{
		return _contract != AbyssalContractKind.Warrior
			|| card.Owner != Owner
			|| !IsWarriorForbiddenCardType(card.Type);
	}

	public override Task AfterAddToDeckPrevented(CardModel card)
	{
		if (_contract == AbyssalContractKind.Warrior
			&& card.Owner == Owner
			&& IsWarriorForbiddenCardType(card.Type))
		{
			Flash();
		}
		return Task.CompletedTask;
	}

	public override bool TryModifyEnergyCostInCombat(
		CardModel card,
		decimal originalCost,
		out decimal modifiedCost)
	{
		modifiedCost = originalCost;
		if (_contract != AbyssalContractKind.Hunter
			|| card.Owner != Owner
			|| card is not Snakebite
			|| card.EnergyCost.CostsX)
		{
			return false;
		}

		modifiedCost = Math.Max(0m, originalCost - HunterSnakebiteCostReduction);
		return true;
	}

	internal bool HasContract(AbyssalContractKind contract)
	{
		return _contract == contract;
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

	private async Task ApplyInitialContractEffect()
	{
		switch (_contract)
		{
			case AbyssalContractKind.Warrior:
				await RemoveWarriorForbiddenCards();
				await UpgradeCurrentStartingRelic();
				break;
			case AbyssalContractKind.Hunter:
				await AddCardCopiesToDeckOrHand<Snakebite>(HunterSnakebiteCount);
				break;
			case AbyssalContractKind.Regent:
				await ReplaceCurrentStartingRelicWithFencingManual();
				await AddCardCopiesToDeckOrHand<SwordSage>(1, ApplyImbuedEnchantment);
				await AddCardCopiesToDeckOrHand<Parry>(1, ApplyImbuedEnchantment);
				break;
			case AbyssalContractKind.Necrobinder:
				await AddCardCopiesToDeckOrHand<SleightOfFlesh>(1, ApplyImbuedEnchantment);
				break;
			case AbyssalContractKind.Automaton:
				ApplyAutomatonOrbSlots();
				await UpgradeCurrentStartingRelic();
				break;
		}
	}

	private async Task RemoveWarriorForbiddenCards()
	{
		if (Owner == null)
		{
			return;
		}

		IReadOnlyList<CardModel> cards = Owner.Deck.Cards
			.Where(static card => IsWarriorForbiddenCardType(card.Type))
			.ToArray();
		if (cards.Count > 0)
		{
			await CardPileCmd.RemoveFromDeck(cards, showPreview: true);
		}
	}

	private async Task TransformRandomCardIntoSnakebite()
	{
		if (Owner == null)
		{
			return;
		}

		IReadOnlyList<CardModel> nonSnakebites = Owner.Deck.Cards
			.Where(static card => card is not Snakebite)
			.ToArray();
		if (nonSnakebites.Count == 0)
		{
			return;
		}

		IReadOnlyList<CardModel> attacks = nonSnakebites
			.Where(static card => card.Type == CardType.Attack)
			.ToArray();
		IReadOnlyList<CardModel> candidates = attacks.Count > 0 ? attacks : nonSnakebites;
		CardModel? original = Owner.PlayerRng.Transformations.NextItem(candidates);
		if (original == null)
		{
			return;
		}
		CardModel replacement = Owner.RunState.CreateCard<Snakebite>(Owner);
		Flash();
		await CardCmd.Transform(original, replacement, CardPreviewStyle.HorizontalLayout);
	}

	private async Task ApplyRandomDebuff(PlayerChoiceContext choiceContext, Creature target)
	{
		if (Owner == null)
		{
			return;
		}

		switch (Owner.RunState.Rng.Niche.NextInt(5))
		{
			case 0:
				await PowerCmd.Apply<WeakPower>(choiceContext, target, 1m, Owner.Creature, null);
				break;
			case 1:
				await PowerCmd.Apply<VulnerablePower>(choiceContext, target, 1m, Owner.Creature, null);
				break;
			case 2:
				await PowerCmd.Apply<FrailPower>(choiceContext, target, 1m, Owner.Creature, null);
				break;
			case 3:
				await PowerCmd.Apply<DoomPower>(choiceContext, target, 1m, Owner.Creature, null);
				break;
			default:
				await PowerCmd.Apply<PoisonPower>(choiceContext, target, 1m, Owner.Creature, null);
				break;
		}
	}

	private async Task UpgradeCurrentStartingRelic()
	{
		if (Owner == null)
		{
			return;
		}

		(RelicModel? original, RelicModel? replacement) = Owner.Character switch
		{
			Ironclad => ((RelicModel?)Owner.GetRelic<BurningBlood>(), ModelDb.Relic<BlackBlood>().ToMutable()),
			Silent => ((RelicModel?)Owner.GetRelic<RingOfTheSnake>(), ModelDb.Relic<RingOfTheDrake>().ToMutable()),
			Regent => ((RelicModel?)Owner.GetRelic<DivineRight>(), ModelDb.Relic<DivineDestiny>().ToMutable()),
			Necrobinder => ((RelicModel?)Owner.GetRelic<BoundPhylactery>(), ModelDb.Relic<PhylacteryUnbound>().ToMutable()),
			Defect => ((RelicModel?)Owner.GetRelic<CrackedCore>(), ModelDb.Relic<InfusedCore>().ToMutable()),
			_ => (null, null)
		};
		if (original != null && replacement != null)
		{
			await RelicCmd.Replace(original, replacement);
		}
	}

	private async Task ReplaceCurrentStartingRelicWithFencingManual()
	{
		if (Owner == null || Owner.GetRelic<FencingManual>() != null)
		{
			return;
		}

		RelicModel? starter = Owner.Character switch
		{
			Ironclad => (RelicModel?)Owner.GetRelic<BurningBlood>() ?? Owner.GetRelic<BlackBlood>(),
			Silent => (RelicModel?)Owner.GetRelic<RingOfTheSnake>() ?? Owner.GetRelic<RingOfTheDrake>(),
			Regent => (RelicModel?)Owner.GetRelic<DivineRight>() ?? Owner.GetRelic<DivineDestiny>(),
			Necrobinder => (RelicModel?)Owner.GetRelic<BoundPhylactery>() ?? Owner.GetRelic<PhylacteryUnbound>(),
			Defect => (RelicModel?)Owner.GetRelic<CrackedCore>() ?? Owner.GetRelic<InfusedCore>(),
			_ => null
		};
		FencingManual replacement = (FencingManual)ModelDb.Relic<FencingManual>().ToMutable();
		if (starter != null)
		{
			await RelicCmd.Replace(starter, replacement);
		}
		else
		{
			await RelicCmd.Obtain(replacement, Owner);
		}
	}

	private void ApplyAutomatonOrbSlots()
	{
		if (Owner == null)
		{
			return;
		}

		Owner.BaseOrbSlotCount += AutomatonOrbSlotBonus;
		if (Owner.PlayerCombatState != null && Owner.Creature.CombatState != null)
		{
			Owner.PlayerCombatState.OrbQueue.AddCapacity(AutomatonOrbSlotBonus);
		}
	}

	private static void ApplyImbuedEnchantment(CardModel card)
	{
		Imbued imbued = (Imbued)ModelDb.Enchantment<Imbued>().ToMutable();
		card.EnchantInternal(imbued, 1m);
		imbued.ModifyCard();
		card.FinalizeUpgradeInternal();
	}
}
