using HarmonyLib;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static partial class HextechCombatHooks
{
	public static void Install(Harmony harmony)
	{
		InstallDrawHooks(harmony);
		InstallHealingHooks(harmony);
		InstallCardPlayHooks(harmony);
		InstallMaxHpHooks(harmony);
		InstallPowerCompatibilityHooks(harmony);
		if (InstallDamageCommandHooks(harmony))
		{
			InstallDualWieldIntentHooks(harmony);
		}
		InstallJeweledGauntletHooks(harmony);
		TryInstallRuneHook<NearDeathFeastRune>("near-death feast", () => InstallNearDeathFeastHooks(harmony));
		HextechPlayerRuneHooks.Install(harmony);
	}

	private static void TryInstallRuneHook<TRune>(string label, Action install)
		where TRune : RelicModel
	{
		try
		{
			install();
		}
		catch (Exception ex)
		{
			HextechRuntimeRuneCompatibility.MarkPlayerRuneHookFailed<TRune>(label, ex);
		}
	}

	private static void InstallDrawHooks(Harmony harmony)
	{
		harmony.Patch(
			RequireMethod(typeof(CardPileCmd), nameof(CardPileCmd.Draw), BindingFlags.Public | BindingFlags.Static, typeof(PlayerChoiceContext), typeof(decimal), typeof(Player), typeof(bool)),
			prefix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(DrawPrefix)));
	}

	private static void InstallHealingHooks(Harmony harmony)
	{
		harmony.Patch(
			RequireMethod(typeof(CreatureCmd), nameof(CreatureCmd.Heal), BindingFlags.Public | BindingFlags.Static, typeof(Creature), typeof(decimal), typeof(bool)),
			prefix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(HealPrefix)),
			postfix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(HealPostfix)));
	}

	private static void InstallCardPlayHooks(Harmony harmony)
	{
		HarmonyMethod canPlayAllowanceWithReasonPostfix = new(
			typeof(HextechCombatHooks),
			nameof(CardCanPlayAllowanceWithReasonPostfix))
		{
			priority = Priority.First
		};
		HarmonyMethod canPlayBlockerPostfix = new(typeof(HextechCombatHooks), nameof(CardCanPlayBlockerPostfix))
		{
			priority = Priority.Last
		};
		HarmonyMethod canPlayBlockerWithReasonPostfix = new(
			typeof(HextechCombatHooks),
			nameof(CardCanPlayBlockerWithReasonPostfix))
		{
			priority = Priority.Last
		};

		MethodInfo canPlay = RequireMethod(typeof(CardModel), nameof(CardModel.CanPlay), BindingFlags.Instance | BindingFlags.Public);
		MethodInfo canPlayWithReason = RequireMethod(
			typeof(CardModel),
			nameof(CardModel.CanPlay),
			BindingFlags.Instance | BindingFlags.Public,
			typeof(UnplayableReason).MakeByRefType(),
			typeof(AbstractModel).MakeByRefType());
		harmony.Patch(
			canPlay,
			postfix: canPlayBlockerPostfix);
		harmony.Patch(
			canPlayWithReason,
			postfix: canPlayAllowanceWithReasonPostfix);
		harmony.Patch(
			canPlayWithReason,
			postfix: canPlayBlockerWithReasonPostfix);
		harmony.Patch(
			RequireMethod(typeof(CardModel), nameof(CardModel.SpendResources), BindingFlags.Instance | BindingFlags.Public),
			prefix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(CardSpendResourcesPrefix)));
		harmony.Patch(
			RequireMethod(typeof(CardModel), nameof(CardModel.OnPlayWrapper), BindingFlags.Instance | BindingFlags.Public, typeof(PlayerChoiceContext), typeof(Creature), typeof(bool), typeof(ResourceInfo), typeof(bool)),
			prefix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(CardOnPlayWrapperPrefix)),
			postfix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(CardOnPlayWrapperPostfix)));
	}

	private static void InstallMaxHpHooks(Harmony harmony)
	{
		harmony.Patch(
			RequireMethod(typeof(CreatureCmd), nameof(CreatureCmd.GainMaxHp), BindingFlags.Public | BindingFlags.Static, typeof(Creature), typeof(decimal)),
			prefix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(GainMaxHpPrefix)),
			postfix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(ResetGoliathTaskPostfix)));
		harmony.Patch(
			RequireMethod(typeof(CreatureCmd), nameof(CreatureCmd.LoseMaxHp), BindingFlags.Public | BindingFlags.Static, typeof(PlayerChoiceContext), typeof(Creature), typeof(decimal), typeof(bool)),
			prefix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(LoseMaxHpPrefix)),
			postfix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(ResetGoliathTaskPostfix)));
		MethodInfo setMaxHpMethod = RequireMethod(typeof(CreatureCmd), nameof(CreatureCmd.SetMaxHp), BindingFlags.Public | BindingFlags.Static, typeof(Creature), typeof(decimal));
		harmony.Patch(
			setMaxHpMethod,
			prefix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(SetMaxHpPrefix)),
			postfix: new HarmonyMethod(
				typeof(HextechCombatHooks),
				setMaxHpMethod.ReturnType == typeof(Task<decimal>)
					? nameof(ResetGoliathDecimalTaskPostfix)
					: nameof(ResetGoliathTaskPostfix)));
	}

	private static void InstallPowerCompatibilityHooks(Harmony harmony)
	{
		InstallShrinkPowerCompatibilityHooks(harmony);
		harmony.Patch(
			RequireMethod(typeof(PowerModel), nameof(PowerModel.GetTypeForAmount), BindingFlags.Public | BindingFlags.Instance, typeof(decimal)),
			prefix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(PowerModelGetTypeForAmountPrefix)));
		harmony.Patch(
			RequireMethod(typeof(StormPower), nameof(StormPower.BeforeCardPlayed), BindingFlags.Public | BindingFlags.Instance, typeof(CardPlay)),
			prefix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(StormBeforeCardPlayedPrefix)));
		harmony.Patch(
			RequireMethod(typeof(StormPower), nameof(StormPower.AfterCardPlayed), BindingFlags.Public | BindingFlags.Instance, typeof(PlayerChoiceContext), typeof(CardPlay)),
			prefix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(StormAfterCardPlayedPrefix)));
		harmony.Patch(
			RequireMethod(typeof(EntropyPower), nameof(EntropyPower.AfterPlayerTurnStart), BindingFlags.Public | BindingFlags.Instance, typeof(PlayerChoiceContext), typeof(Player)),
			prefix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(EntropyAfterPlayerTurnStartPrefix)));
#if STS2_110_OR_NEWER
		harmony.Patch(
			RequireMethod(
				typeof(Outbreak),
				"OnPlay",
				BindingFlags.Instance | BindingFlags.NonPublic,
				typeof(PlayerChoiceContext),
				typeof(CardPlay)),
			prefix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(OutbreakOnPlayPrefix)),
			postfix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(OutbreakOnPlayPostfix)));
#else
		TryPatchAfterPowerAmountChanged(
			harmony,
			typeof(OutbreakPower),
			nameof(OutbreakPower),
			nameof(OutbreakPowerAfterPowerAmountChangedPrefix),
			nameof(OutbreakPowerAfterPowerAmountChangedPostfix));
#endif
		TryPatchAfterPowerAmountChanged(
			harmony,
			typeof(SleightOfFleshPower),
			nameof(SleightOfFleshPower),
			nameof(SleightOfFleshPowerAfterPowerAmountChangedPrefix),
			nameof(SleightOfFleshPowerAfterPowerAmountChangedPostfix));
	}

	private static void TryPatchAfterPowerAmountChanged(
		Harmony harmony,
		Type powerType,
		string label,
		string prefixName,
		string postfixName)
	{
		int patchedCount = 0;
		if (TryPatchAfterPowerAmountChangedOverload(
			harmony,
			powerType,
			label,
			prefixName,
			postfixName,
			typeof(PlayerChoiceContext),
			typeof(PowerModel),
			typeof(decimal),
			typeof(Creature),
			typeof(CardModel)))
		{
			patchedCount++;
		}

		if (TryPatchAfterPowerAmountChangedOverload(
			harmony,
			powerType,
			label,
			prefixName,
			postfixName,
			typeof(PowerModel),
			typeof(decimal),
			typeof(Creature),
			typeof(CardModel)))
		{
			patchedCount++;
		}

		if (patchedCount == 0)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Optional power compatibility hook skipped: {label}.AfterPowerAmountChanged overload not found.");
		}
	}

	private static bool TryPatchAfterPowerAmountChangedOverload(
		Harmony harmony,
		Type powerType,
		string label,
		string prefixName,
		string postfixName,
		params Type[] parameterTypes)
	{
		MethodInfo? target = AccessTools.Method(powerType, "AfterPowerAmountChanged", parameterTypes);
		if (target == null)
		{
			return false;
		}

		try
		{
			harmony.Patch(
				target,
				prefix: new HarmonyMethod(typeof(HextechCombatHooks), prefixName),
				postfix: new HarmonyMethod(typeof(HextechCombatHooks), postfixName));
			return true;
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Optional power compatibility hook failed: {label}.{target.Name}: {ex.GetType().Name}: {ex.Message}");
			return false;
		}
	}

	private static bool InstallDamageCommandHooks(Harmony harmony)
	{
		HarmonyMethod? dualWieldPrefix = TryCreateDualWieldAttackCommandPrefix();
		harmony.Patch(
			RequireMethod(typeof(AttackCommand), nameof(AttackCommand.Execute), BindingFlags.Instance | BindingFlags.Public, typeof(PlayerChoiceContext)),
			prefix: dualWieldPrefix,
			postfix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(AttackCommandExecutePostfix))
			{
				priority = Priority.Last
			});
		harmony.Patch(
			RequireMethod(
				typeof(CreatureCmd),
				nameof(CreatureCmd.Damage),
				BindingFlags.Public | BindingFlags.Static,
				typeof(PlayerChoiceContext),
				typeof(IEnumerable<Creature>),
				typeof(decimal),
				typeof(ValueProp),
				typeof(Creature),
#if STS2_108_OR_NEWER
				// 0.108.0 起该重载追加 CardPlay 参数。
				typeof(CardModel),
				typeof(CardPlay)),
#else
				typeof(CardModel)),
#endif
			prefix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(ActualDamageCommandPrefix)),
			postfix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(ActualDamageCommandPostfix)));
		harmony.Patch(
			RequireMethod(
				typeof(SlipperyPower),
				nameof(SlipperyPower.ModifyHpLostAfterOsty),
				BindingFlags.Instance | BindingFlags.Public,
				typeof(Creature),
				typeof(decimal),
				typeof(ValueProp),
				typeof(Creature),
				typeof(CardModel)),
			postfix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(SlipperyModifyHpLostAfterOstyPostfix)));
		harmony.Patch(
			RequireMethod(
				typeof(SlipperyPower),
				nameof(SlipperyPower.AfterDamageReceived),
				BindingFlags.Instance | BindingFlags.Public,
				typeof(PlayerChoiceContext),
				typeof(Creature),
				typeof(DamageResult),
				typeof(ValueProp),
				typeof(Creature),
				typeof(CardModel)),
			postfix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(SlipperyAfterDamageReceivedPostfix)));
		harmony.Patch(
			RequireMethod(
				typeof(Creature),
				nameof(Creature.DamageBlockInternal),
				BindingFlags.Instance | BindingFlags.Public,
				typeof(decimal),
				typeof(ValueProp)),
			prefix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(PiercingThreadDamageBlockPrefix))
			{
				priority = Priority.First
			});
		harmony.Patch(
			RequireMethod(
				typeof(DieForYouPower),
				nameof(DieForYouPower.ModifyUnblockedDamageTarget),
				BindingFlags.Instance | BindingFlags.Public,
				typeof(Creature),
				typeof(decimal),
				typeof(ValueProp),
				typeof(Creature)),
			postfix: new HarmonyMethod(typeof(HextechCombatHooks), nameof(DieForYouModifyUnblockedDamageTargetPostfix)));
		return dualWieldPrefix != null;
	}
}
