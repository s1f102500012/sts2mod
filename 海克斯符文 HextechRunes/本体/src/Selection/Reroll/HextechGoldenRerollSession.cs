namespace HextechRunes;

internal static class HextechGoldenRerollRules
{
	public static bool TryGetUpgradedRarity(
		HextechRarityTier rarity,
		out HextechRarityTier upgradedRarity)
	{
		switch (rarity)
		{
			case HextechRarityTier.Silver:
				upgradedRarity = HextechRarityTier.Gold;
				return true;
			case HextechRarityTier.Gold:
				upgradedRarity = HextechRarityTier.Prismatic;
				return true;
			default:
				upgradedRarity = rarity;
				return false;
		}
	}

	public static bool ShouldActivateForRoll(
		HextechRarityTier rarity,
		bool hasUpgradedCandidates,
		int percentRoll,
		int activationPercent)
	{
		int normalizedPercent = HextechRuneConfiguration.ClampGoldenRerollChancePercent(activationPercent);
		return hasUpgradedCandidates
			&& TryGetUpgradedRarity(rarity, out _)
			&& percentRoll >= 0
			&& percentRoll < normalizedPercent;
	}

	public static bool ShouldActivate(
		RunState runState,
		Player player,
		int actIndex,
		int choiceOrdinal,
		HextechRarityTier rarity,
		bool hasUpgradedCandidates,
		int activationPercent)
	{
		if (!hasUpgradedCandidates || !TryGetUpgradedRarity(rarity, out _))
		{
			return false;
		}

		return HextechStableRandom.PercentChance(
			runState,
			HextechRuneConfiguration.ClampGoldenRerollChancePercent(activationPercent),
			BuildSaltParts(
				actIndex,
				choiceOrdinal,
				HextechStableRandom.PlayerKey(player)));
	}

	internal static string[] BuildSaltParts(
		int actIndex,
		int choiceOrdinal,
		string playerKey)
	{
		return
		[
			"golden-reroll",
			"act",
			actIndex.ToString(),
			"choice",
			choiceOrdinal.ToString(),
			"player",
			playerKey
		];
	}
}

internal sealed class HextechGoldenRerollSession
{
	public bool CanActivate { get; }

	public bool IsActive { get; private set; }

	public HextechRarityTier UpgradedRarity { get; }

	private HextechGoldenRerollSession(
		bool canActivate,
		bool isActive,
		HextechRarityTier upgradedRarity)
	{
		CanActivate = canActivate;
		IsActive = isActive;
		UpgradedRarity = upgradedRarity;
	}

	public static HextechGoldenRerollSession Create(
		RunState runState,
		Player player,
		int actIndex,
		int choiceOrdinal,
		HextechRarityTier rarity,
		bool hasUpgradedCandidates,
		int activationPercent)
	{
		HextechRarityTier upgradedRarity = rarity;
		bool canActivate = hasUpgradedCandidates
			&& HextechGoldenRerollRules.TryGetUpgradedRarity(rarity, out upgradedRarity);
		if (!canActivate)
		{
			return new HextechGoldenRerollSession(
				canActivate: false,
				isActive: false,
				upgradedRarity: rarity);
		}

		bool forced = HextechGoldenRerollDebug.ConsumeNextEligibleForce();
		bool active = forced || HextechGoldenRerollRules.ShouldActivate(
			runState,
			player,
			actIndex,
			choiceOrdinal,
			rarity,
			hasUpgradedCandidates,
			activationPercent);
		return new HextechGoldenRerollSession(
			canActivate: true,
			isActive: active,
			upgradedRarity);
	}

	public bool ActivateForDebug()
	{
		if (!CanActivate)
		{
			return false;
		}

		IsActive = true;
		return true;
	}

	public void Consume()
	{
		IsActive = false;
	}
}

internal static class HextechGoldenRerollDebug
{
	private static WeakReference<HextechRuneSelectionScreen>? _currentScreen;
	private static bool _forceNextEligible;

	internal static void RegisterScreen(HextechRuneSelectionScreen screen)
	{
		_currentScreen = new WeakReference<HextechRuneSelectionScreen>(screen);
	}

	internal static void UnregisterScreen(HextechRuneSelectionScreen screen)
	{
		if (_currentScreen != null
			&& _currentScreen.TryGetTarget(out HextechRuneSelectionScreen? current)
			&& ReferenceEquals(current, screen))
		{
			_currentScreen = null;
		}
	}

	internal static bool ForceCurrentOrNext(out bool activatedCurrent)
	{
		if (_currentScreen != null
			&& _currentScreen.TryGetTarget(out HextechRuneSelectionScreen? screen)
			&& screen.ActivateGoldenRerollForDebug())
		{
			activatedCurrent = true;
			return true;
		}

		_forceNextEligible = true;
		activatedCurrent = false;
		return true;
	}

	internal static bool ConsumeNextEligibleForce()
	{
		if (!_forceNextEligible)
		{
			return false;
		}

		_forceNextEligible = false;
		return true;
	}

	internal static void Clear()
	{
		_forceNextEligible = false;
	}

	internal static bool IsNextEligibleForced => _forceNextEligible;

	internal static void ResetForTests()
	{
		_currentScreen = null;
		_forceNextEligible = false;
	}
}
