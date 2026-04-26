using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MonoMod.RuntimeDetour;

namespace HextechRunes;

internal static class HextechCombatHooks
{
	private static Hook? _drawHook;
	private static Hook? _healHook;
	private static Hook? _cardCanPlayHook;
	private static Hook? _cardCanPlayWithReasonHook;
	private static Hook? _gainMaxHpHook;
	private static Hook? _loseMaxHpHook;
	private static Hook? _setMaxHpHook;

	private static bool _handlingGoliathMaxHp;

	private delegate Task<IEnumerable<CardModel>> OrigDraw(PlayerChoiceContext choiceContext, decimal count, Player player, bool fromHandDraw);

	private delegate Task OrigHeal(Creature creature, decimal amount, bool playAnim);

	private delegate bool OrigCardCanPlay(CardModel self);

	private delegate bool OrigCardCanPlayWithReason(CardModel self, out UnplayableReason reason, out AbstractModel preventer);

	private delegate Task OrigGainMaxHp(Creature creature, decimal amount);

	private delegate Task OrigLoseMaxHp(PlayerChoiceContext choiceContext, Creature creature, decimal amount, bool isFromCard);

	private delegate Task<decimal> OrigSetMaxHp(Creature creature, decimal amount);

	public static void Install()
	{
		_drawHook = new Hook(
			RequireMethod(typeof(CardPileCmd), nameof(CardPileCmd.Draw), BindingFlags.Public | BindingFlags.Static, typeof(PlayerChoiceContext), typeof(decimal), typeof(Player), typeof(bool)),
			DrawDetour);
		_healHook = new Hook(
			RequireMethod(typeof(CreatureCmd), nameof(CreatureCmd.Heal), BindingFlags.Public | BindingFlags.Static, typeof(Creature), typeof(decimal), typeof(bool)),
			HealDetour);
		_cardCanPlayHook = new Hook(
			RequireMethod(typeof(CardModel), nameof(CardModel.CanPlay), BindingFlags.Instance | BindingFlags.Public),
			CardCanPlayDetour);
		_cardCanPlayWithReasonHook = new Hook(
			RequireMethod(typeof(CardModel), nameof(CardModel.CanPlay), BindingFlags.Instance | BindingFlags.Public, typeof(UnplayableReason).MakeByRefType(), typeof(AbstractModel).MakeByRefType()),
			CardCanPlayWithReasonDetour);
		_gainMaxHpHook = new Hook(
			RequireMethod(typeof(CreatureCmd), nameof(CreatureCmd.GainMaxHp), BindingFlags.Public | BindingFlags.Static, typeof(Creature), typeof(decimal)),
			GainMaxHpDetour);
		_loseMaxHpHook = new Hook(
			RequireMethod(typeof(CreatureCmd), nameof(CreatureCmd.LoseMaxHp), BindingFlags.Public | BindingFlags.Static, typeof(PlayerChoiceContext), typeof(Creature), typeof(decimal), typeof(bool)),
			LoseMaxHpDetour);
		_setMaxHpHook = new Hook(
			RequireMethod(typeof(CreatureCmd), nameof(CreatureCmd.SetMaxHp), BindingFlags.Public | BindingFlags.Static, typeof(Creature), typeof(decimal)),
			SetMaxHpDetour);
	}

	private static async Task<IEnumerable<CardModel>> DrawDetour(OrigDraw orig, PlayerChoiceContext choiceContext, decimal count, Player player, bool fromHandDraw)
	{
		NoNonsenseRune? noNonsenseRune = player.GetRelic<NoNonsenseRune>();
		if (noNonsenseRune == null || fromHandDraw || count <= 0m || player.Creature.CombatState == null)
		{
			return await orig(choiceContext, count, player, fromHandDraw);
		}

		int drawsPrevented = (int)Math.Ceiling(count);
		if (drawsPrevented <= 0)
		{
			return Array.Empty<CardModel>();
		}

		await noNonsenseRune.HandlePreventedNonHandDraw(drawsPrevented);
		await PowerCmd.Apply<StrengthPower>(player.Creature, drawsPrevented, player.Creature, null);
		return Array.Empty<CardModel>();
	}

	private static async Task HealDetour(OrigHeal orig, Creature creature, decimal amount, bool playAnim)
	{
		Player? player = creature.Player;
		if (player != null && creature == player.Creature)
		{
			if (player.GetRelic<OverflowRune>() != null)
			{
				amount *= 2m;
			}

			if (player.GetRelic<FirstAidKitRune>() != null)
			{
				amount *= 1.2m;
			}

			if (player.GetRelic<BackToBasicsRune>() != null)
			{
				amount *= 1.4m;
			}

			if (player.GetRelic<GoliathRune>() != null)
			{
				amount *= 1.2m;
			}

			if (player.GetRelic<ProteinShakeRune>() is ProteinShakeRune proteinShakeRune)
			{
				amount *= proteinShakeRune.SustainMultiplier;
			}
		}

		if (player?.GetRelic<GlassCannonRune>() is GlassCannonRune glassCannonRune && creature == player.Creature)
		{
			int healCap = (int)Math.Floor(creature.MaxHp * glassCannonRune.HealCapPercent);
			amount = Math.Min(amount, Math.Max(0, healCap - creature.CurrentHp));
			if (amount <= 0m)
			{
				return;
			}
		}

		if (creature.Side == CombatSide.Enemy
			&& creature.CombatState?.RunState is RunState runState
			&& GetMayhemModifier(runState) is HextechMayhemModifier modifier)
		{
			amount = modifier.ModifyEnemyHealAmount(creature, amount);
			if (amount <= 0m)
			{
				return;
			}
		}

		if (amount <= 0m)
		{
			return;
		}

		await orig(creature, amount, playAnim);

		if (player?.GetRelic<HolyFireRune>() != null
			&& creature == player.Creature
			&& creature.CombatState != null)
		{
			List<Creature> enemies = creature.CombatState.Enemies.Where(static enemy => enemy.IsAlive).ToList();
			int burnAmount = (int)Math.Floor(amount);
			if (enemies.Count > 0 && burnAmount > 0)
			{
				Creature target = enemies[player.RunState.Rng.Niche.NextInt(enemies.Count)];
				await PowerCmd.Apply<HextechBurnPower>(target, burnAmount, player.Creature, null);
			}
		}

		if (player?.GetRelic<CircleOfDeathRune>() is CircleOfDeathRune circleOfDeathRune
			&& creature == player.Creature
			&& creature.CombatState != null)
		{
			await circleOfDeathRune.HandleSustainGained(amount);
		}
	}

	private static bool CardCanPlayDetour(OrigCardCanPlay orig, CardModel self)
	{
		return orig(self) && !IsBlockedByBackToBasics(self);
	}

	private static bool CardCanPlayWithReasonDetour(OrigCardCanPlayWithReason orig, CardModel self, out UnplayableReason reason, out AbstractModel preventer)
	{
		bool canPlay = orig(self, out reason, out preventer);
		if (!canPlay)
		{
			return false;
		}

		if (!IsBlockedByBackToBasics(self, out AbstractModel? backToBasicsPreventer))
		{
			return true;
		}

		reason = default;
		preventer = backToBasicsPreventer!;
		return false;
	}

	private static Task GainMaxHpDetour(OrigGainMaxHp orig, Creature creature, decimal amount)
	{
		if (_handlingGoliathMaxHp || creature.Player?.GetRelic<GoliathRune>() is not GoliathRune rune)
		{
			return orig(creature, amount);
		}

		rune.EnsureBaseMaxHpInitialized();
		int oldActual = creature.MaxHp;
		rune.BaseMaxHp += (int)amount;
		int newActual = rune.GetScaledMaxHp();
		int delta = Math.Max(0, newActual - oldActual);
		if (delta == 0)
		{
			return Task.CompletedTask;
		}

		_handlingGoliathMaxHp = true;
		return CompleteWithReset(orig(creature, delta));
	}

	private static Task LoseMaxHpDetour(OrigLoseMaxHp orig, PlayerChoiceContext choiceContext, Creature creature, decimal amount, bool isFromCard)
	{
		if (_handlingGoliathMaxHp || creature.Player?.GetRelic<GoliathRune>() is not GoliathRune rune)
		{
			return orig(choiceContext, creature, amount, isFromCard);
		}

		rune.EnsureBaseMaxHpInitialized();
		int oldActual = creature.MaxHp;
		rune.BaseMaxHp -= (int)amount;
		int newActual = rune.GetScaledMaxHp();
		int loss = Math.Max(0, oldActual - newActual);
		if (loss == 0)
		{
			return Task.CompletedTask;
		}

		_handlingGoliathMaxHp = true;
		return CompleteWithReset(orig(choiceContext, creature, loss, isFromCard));
	}

	private static Task<decimal> SetMaxHpDetour(OrigSetMaxHp orig, Creature creature, decimal amount)
	{
		if (_handlingGoliathMaxHp || creature.Player?.GetRelic<GoliathRune>() is not GoliathRune rune)
		{
			return orig(creature, amount);
		}

		rune.BaseMaxHp = (int)Math.Max(1m, amount);
		_handlingGoliathMaxHp = true;
		return CompleteWithReset(orig(creature, rune.GetScaledMaxHp()));
	}

	private static async Task CompleteWithReset(Task task)
	{
		try
		{
			await task;
		}
		finally
		{
			_handlingGoliathMaxHp = false;
		}
	}

	private static async Task<decimal> CompleteWithReset(Task<decimal> task)
	{
		try
		{
			return await task;
		}
		finally
		{
			_handlingGoliathMaxHp = false;
		}
	}

	private static bool IsBlockedByBackToBasics(CardModel card)
	{
		return IsBlockedByBackToBasics(card, out _);
	}

	private static bool IsBlockedByBackToBasics(CardModel card, out AbstractModel? preventer)
	{
		preventer = null;
		if (card.Owner == null)
		{
			return false;
		}

		if (card.EnergyCost.CostsX || card.EnergyCost.GetAmountToSpend() < 3m)
		{
			return false;
		}

		BackToBasicsRune? rune = card.Owner.GetRelic<BackToBasicsRune>();
		if (rune != null)
		{
			preventer = rune;
			return true;
		}

		if (card.Owner.Creature.CombatState?.RunState is RunState runState
			&& card.Owner.Creature.Side == CombatSide.Player
			&& GetMayhemModifier(runState) is HextechMayhemModifier modifier
			&& modifier.HasActiveMonsterHex(MonsterHexKind.BackToBasics))
		{
			preventer = modifier;
			return true;
		}

		return false;
	}

	private static HextechMayhemModifier? GetMayhemModifier(RunState runState)
	{
		return runState.Modifiers.OfType<HextechMayhemModifier>().LastOrDefault();
	}

	private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags, params Type[] parameters)
	{
		return type.GetMethod(name, flags, binder: null, parameters, modifiers: null)
			?? throw new InvalidOperationException($"Could not find required method {type.FullName}.{name}.");
	}
}
