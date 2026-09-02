using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Relics;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static partial class HextechPlayerRuneHooks
{
	internal const string FinisherCalculatedHitsKey = "CalculatedHits";

	internal static PropertyInfo? KunaiAttacksPlayedThisTurnProperty;
	internal static PropertyInfo? ShurikenAttacksPlayedThisTurnProperty;
	internal static PropertyInfo? OrnamentalFanAttacksPlayedThisTurnProperty;
	internal static PropertyInfo? PenNibAttackToDoubleProperty;

	internal static MethodInfo? NunchakuDoActivateVisualsMethod;
	internal static MethodInfo? KunaiDoActivateVisualsMethod;
	internal static MethodInfo? ShurikenDoActivateVisualsMethod;
	internal static MethodInfo? OrnamentalFanDoActivateVisualsMethod;
	internal static bool? _illusoryWeaponReflectionReady;

	/// <summary>
	/// 幻影武器要改写五个原版遗物的私有计数与视觉方法;任一缺失就整组停用并把符文标为本运行时不可用。
	/// 七个补丁类共用这一次解析。
	/// </summary>
	internal static bool IllusoryWeaponReflectionReady
	{
		get
		{
			if (_illusoryWeaponReflectionReady is bool cached)
			{
				return cached;
			}

			try
			{
				KunaiAttacksPlayedThisTurnProperty = RequireProperty(typeof(Kunai), "AttacksPlayedThisTurn");
				ShurikenAttacksPlayedThisTurnProperty = RequireProperty(typeof(Shuriken), "AttacksPlayedThisTurn");
				OrnamentalFanAttacksPlayedThisTurnProperty = RequireProperty(typeof(OrnamentalFan), "AttacksPlayedThisTurn");
				PenNibAttackToDoubleProperty = RequireProperty(typeof(PenNib), "AttackToDouble");

				NunchakuDoActivateVisualsMethod = RequireMethod(typeof(Nunchaku), "DoActivateVisuals", BindingFlags.Instance | BindingFlags.NonPublic);
				KunaiDoActivateVisualsMethod = RequireMethod(typeof(Kunai), "DoActivateVisuals", BindingFlags.Instance | BindingFlags.NonPublic);
				ShurikenDoActivateVisualsMethod = RequireMethod(typeof(Shuriken), "DoActivateVisuals", BindingFlags.Instance | BindingFlags.NonPublic);
				OrnamentalFanDoActivateVisualsMethod = RequireMethod(typeof(OrnamentalFan), "DoActivateVisuals", BindingFlags.Instance | BindingFlags.NonPublic);
				_illusoryWeaponReflectionReady = true;
			}
			catch (Exception ex)
			{
				HextechRuntimeRuneCompatibility.MarkPlayerRuneHookFailed<IllusoryWeaponRune>("illusory weapon attack counters", ex);
				_illusoryWeaponReflectionReady = false;
			}

			return _illusoryWeaponReflectionReady.Value;
		}
	}

	internal static PropertyInfo RequireProperty(Type type, string name)
	{
		return type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException($"Could not find required property {type.FullName}.{name}.");
	}


	internal static decimal CountFinisherAttackCardsPlayedThisTurn(CardModel card, Creature? _)
	{
			return HextechCombatHistoryHelper.CountOwnedAttackCardsPlayedThisTurn(
				card.Owner,
				card.CombatState as CombatState,
				firstInSeriesOnly: false,
				includeAutoPlay: true);
	}


	internal static async Task ResolveIllusoryWeaponNunchaku(Nunchaku nunchaku)
	{
		nunchaku.AttacksPlayed++;
		int cardsNeeded = nunchaku.DynamicVars.Cards.IntValue;
		if (cardsNeeded <= 0 || !CombatManager.Instance.IsInProgress || nunchaku.AttacksPlayed % cardsNeeded != 0)
		{
			return;
		}

		_ = TaskHelper.RunSafely(InvokePrivateRelicVisuals(nunchaku, NunchakuDoActivateVisualsMethod, nameof(Nunchaku)));
		await PlayerCmd.GainEnergy(nunchaku.DynamicVars.Energy.BaseValue, nunchaku.Owner);
	}


	internal static async Task ResolveIllusoryWeaponKunai(Kunai kunai)
	{
		int attacksPlayed = IncrementIntProperty(kunai, KunaiAttacksPlayedThisTurnProperty);
		int cardsNeeded = kunai.DynamicVars.Cards.IntValue;
		if (cardsNeeded <= 0 || attacksPlayed % cardsNeeded != 0)
		{
			return;
		}

		_ = TaskHelper.RunSafely(InvokePrivateRelicVisuals(kunai, KunaiDoActivateVisualsMethod, nameof(Kunai)));
		await PowerCmd.Apply<DexterityPower>(kunai.Owner.Creature, kunai.DynamicVars.Dexterity.BaseValue, kunai.Owner.Creature, null);
	}


	internal static async Task ResolveIllusoryWeaponShuriken(Shuriken shuriken)
	{
		int attacksPlayed = IncrementIntProperty(shuriken, ShurikenAttacksPlayedThisTurnProperty);
		int cardsNeeded = shuriken.DynamicVars.Cards.IntValue;
		if (cardsNeeded <= 0 || attacksPlayed % cardsNeeded != 0)
		{
			return;
		}

		_ = TaskHelper.RunSafely(InvokePrivateRelicVisuals(shuriken, ShurikenDoActivateVisualsMethod, nameof(Shuriken)));
		await PowerCmd.Apply<StrengthPower>(shuriken.Owner.Creature, shuriken.DynamicVars.Strength.BaseValue, shuriken.Owner.Creature, null);
	}


	internal static async Task ResolveIllusoryWeaponOrnamentalFan(OrnamentalFan ornamentalFan)
	{
		int attacksPlayed = IncrementIntProperty(ornamentalFan, OrnamentalFanAttacksPlayedThisTurnProperty);
		int cardsNeeded = ornamentalFan.DynamicVars.Cards.IntValue;
		if (cardsNeeded <= 0 || attacksPlayed % cardsNeeded != 0)
		{
			return;
		}

		_ = TaskHelper.RunSafely(InvokePrivateRelicVisuals(ornamentalFan, OrnamentalFanDoActivateVisualsMethod, nameof(OrnamentalFan)));
		await CreatureCmd.GainBlock(ornamentalFan.Owner.Creature, ornamentalFan.DynamicVars.Block, null);
	}


	internal static void ClearIllusoryWeaponPendingPenNib(Player? owner, CardModel card)
	{
		PenNib? penNib = owner?.GetRelic<PenNib>();
		if (penNib == null || !IsPenNibTracking(penNib, card))
		{
			return;
		}

		SetPenNibAttackToDouble(penNib, null);
	}

	internal static bool ShouldHandleIllusoryWeaponSkill(CardPlay cardPlay, Player? owner)
	{
		return owner != null
			&& cardPlay.Card.Type != CardType.Attack
			&& cardPlay.Card.Owner == owner
			&& IllusoryWeaponRune.IsAttackForEffects(cardPlay.Card, owner);
	}

	internal static int IncrementIntProperty(object instance, PropertyInfo? property)
	{
		int value = property?.GetValue(instance) is int current ? current : 0;
		value++;
		property?.SetValue(instance, value);
		return value;
	}

	internal static bool IsPenNibTracking(PenNib penNib, CardModel card)
	{
		return ReferenceEquals(PenNibAttackToDoubleProperty?.GetValue(penNib), card);
	}

	internal static void SetPenNibAttackToDouble(PenNib penNib, CardModel? card)
	{
		PenNibAttackToDoubleProperty?.SetValue(penNib, card);
	}

	internal static Task InvokePrivateRelicVisuals(RelicModel relic, MethodInfo? method, string relicName)
	{
		if (method == null)
		{
			return Task.CompletedTask;
		}

		try
		{
			return method.Invoke(relic, null) as Task ?? Task.CompletedTask;
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][IllusoryWeapon] Failed to run {relicName} activation visuals: {ex.GetType().Name}: {ex.Message}");
			relic.Flash();
			return Task.CompletedTask;
		}
	}

}
