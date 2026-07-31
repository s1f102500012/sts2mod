namespace HextechRunes;

internal static class HextechLegacyEnemyMaxHpMigration
{
	private const int MaxExactTankStackCount = 1024;

	internal static int ResolveBaseMaxHp(
		int currentMaxHp,
		int? rawMonsterMaxHp,
		IReadOnlyList<decimal> appliedFixedBonusFractions,
		decimal madScientistLossFraction,
		int tankEngineStacks)
	{
		int normalizedCurrentMaxHp = Math.Max(1, currentMaxHp);
		int beforeTankEngine = ReverseTankEngine(
			normalizedCurrentMaxHp,
			Math.Max(0, tankEngineStacks));
		int? normalizedRawMaxHp = NormalizeRawMaxHp(rawMonsterMaxHp);
		if (normalizedRawMaxHp == null)
		{
			return ReverseRawlessPersistentEffects(
				beforeTankEngine,
				appliedFixedBonusFractions,
				madScientistLossFraction);
		}

		int? fixedBonusTarget = ResolveFixedBonusTarget(rawMonsterMaxHp, appliedFixedBonusFractions);
		int beforeMadScientist = ReverseMadScientist(
			beforeTankEngine,
			madScientistLossFraction,
			fixedBonusTarget ?? normalizedRawMaxHp);

		if (fixedBonusTarget is int target && beforeMadScientist <= target)
		{
			// 旧固定加成会把所有不高于 target 的初始值压成同一个结果，无法从存档唯一反解。
			// 原始怪物生命是唯一有据可依的下界；多人缩放若高于 target，则会走下面的唯一解分支。
			return normalizedRawMaxHp.Value;
		}

		return Math.Max(1, beforeMadScientist);
	}

	private static int ReverseRawlessPersistentEffects(
		int currentMaxHp,
		IReadOnlyList<decimal> fixedBonusFractions,
		decimal madScientistLossFraction)
	{
		int firstCandidate = FindFirstRawlessInputAtLeast(
			currentMaxHp,
			fixedBonusFractions,
			madScientistLossFraction);
		if (ApplyLegacyRawlessPersistentEffects(
			firstCandidate,
			fixedBonusFractions,
			madScientistLossFraction) != currentMaxHp)
		{
			int lowerCandidate = Math.Max(1, firstCandidate - 1);
			long lowerDistance = Math.Abs(
				(long)ApplyLegacyRawlessPersistentEffects(
					lowerCandidate,
					fixedBonusFractions,
					madScientistLossFraction) - currentMaxHp);
			long upperDistance = Math.Abs(
				(long)ApplyLegacyRawlessPersistentEffects(
					firstCandidate,
					fixedBonusFractions,
					madScientistLossFraction) - currentMaxHp);
			return lowerDistance <= upperDistance ? lowerCandidate : firstCandidate;
		}

		return FindLastRawlessInputAtMost(
			currentMaxHp,
			fixedBonusFractions,
			madScientistLossFraction);
	}

	private static int FindFirstRawlessInputAtLeast(
		int output,
		IReadOnlyList<decimal> fixedBonusFractions,
		decimal madScientistLossFraction)
	{
		int low = 1;
		int high = int.MaxValue;
		while (low < high)
		{
			int middle = low + (int)(((long)high - low) / 2);
			if (ApplyLegacyRawlessPersistentEffects(
				middle,
				fixedBonusFractions,
				madScientistLossFraction) >= output)
			{
				high = middle;
			}
			else
			{
				low = middle + 1;
			}
		}

		return low;
	}

	private static int FindLastRawlessInputAtMost(
		int output,
		IReadOnlyList<decimal> fixedBonusFractions,
		decimal madScientistLossFraction)
	{
		int low = 1;
		int high = int.MaxValue;
		while (low < high)
		{
			int middle = low + (int)(((long)high - low + 1) / 2);
			if (ApplyLegacyRawlessPersistentEffects(
				middle,
				fixedBonusFractions,
				madScientistLossFraction) <= output)
			{
				low = middle;
			}
			else
			{
				high = middle - 1;
			}
		}

		return low;
	}

	private static int ApplyLegacyRawlessPersistentEffects(
		int baseMaxHp,
		IReadOnlyList<decimal> fixedBonusFractions,
		decimal madScientistLossFraction)
	{
		int maxHp = Math.Max(1, baseMaxHp);
		foreach (decimal bonusFraction in fixedBonusFractions)
		{
			if (bonusFraction > 0m)
			{
				maxHp = ApplyLegacyFixedBonus(maxHp, bonusFraction);
			}
		}

		return madScientistLossFraction > 0m
			? ApplyLegacyMadScientist(maxHp, madScientistLossFraction)
			: maxHp;
	}

	private static int ApplyLegacyFixedBonus(int maxHp, decimal bonusFraction)
	{
		decimal result = maxHp + Math.Floor(maxHp * bonusFraction);
		return (int)Math.Clamp(result, 1m, int.MaxValue);
	}

	private static int ReverseTankEngine(int currentMaxHp, int stackCount)
	{
		int beforeStack = currentMaxHp;
		int exactStackCount = Math.Min(stackCount, MaxExactTankStackCount);
		for (int i = 0; i < exactStackCount; i++)
		{
			beforeStack = ReverseSingleTankEngineStack(beforeStack);
		}

		// 从至少 1 点生命开始，合法旧算法不可能在 int 范围内承受 1024 层仍不溢出。
		// 超出上限说明追踪数据已损坏；继续取最小可解释基准，避免按攻击者提供的层数长循环。
		return stackCount > MaxExactTankStackCount ? 1 : beforeStack;
	}

	private static int ReverseSingleTankEngineStack(int currentMaxHp)
	{
		if (currentMaxHp <= 1)
		{
			return 1;
		}

		if (currentMaxHp <= 20)
		{
			return currentMaxHp - 1;
		}

		int quotient = currentMaxHp / 21;
		int remainder = currentMaxHp % 21;
		if (remainder <= 19)
		{
			return Math.Max(1, quotient * 20 + remainder);
		}

		// x + floor(x / 20) 在每个 20 点边界会跳过一个整数。非旧算法产出的值取较低前驱，
		// 避免迁移凭空增加敌人生命。
		return Math.Max(1, quotient * 20 + 19);
	}

	private static int ReverseMadScientist(int currentMaxHp, decimal lossFraction, int? preferredBeforeLoss)
	{
		if (lossFraction <= 0m)
		{
			return currentMaxHp;
		}

		if (lossFraction >= 1m)
		{
			return Math.Max(1, preferredBeforeLoss ?? currentMaxHp);
		}

		int firstCandidate = FindFirstMadScientistInputAtLeast(currentMaxHp, lossFraction);
		if (ApplyLegacyMadScientist(firstCandidate, lossFraction) != currentMaxHp)
		{
			int lowerCandidate = Math.Max(1, firstCandidate - 1);
			long lowerDistance = Math.Abs((long)ApplyLegacyMadScientist(lowerCandidate, lossFraction) - currentMaxHp);
			long upperDistance = Math.Abs((long)ApplyLegacyMadScientist(firstCandidate, lossFraction) - currentMaxHp);
			return upperDistance <= lowerDistance ? firstCandidate : lowerCandidate;
		}

		int lastCandidate = FindLastMadScientistInputAtMost(currentMaxHp, lossFraction);
		if (preferredBeforeLoss is int preferred
			&& preferred >= firstCandidate
			&& preferred <= lastCandidate)
		{
			return preferred;
		}

		// MadScientist 的 floor 会让相邻基准偶尔落到同一个结果。无法区分时取较高解，
		// 防止迁移把多人缩放或外部生命加成向下抹掉。
		return lastCandidate;
	}

	private static int FindFirstMadScientistInputAtLeast(int output, decimal lossFraction)
	{
		int low = 1;
		int high = int.MaxValue;
		while (low < high)
		{
			int middle = low + (int)(((long)high - low) / 2);
			if (ApplyLegacyMadScientist(middle, lossFraction) >= output)
			{
				high = middle;
			}
			else
			{
				low = middle + 1;
			}
		}

		return low;
	}

	private static int FindLastMadScientistInputAtMost(int output, decimal lossFraction)
	{
		int low = 1;
		int high = int.MaxValue;
		while (low < high)
		{
			int middle = low + (int)(((long)high - low + 1) / 2);
			if (ApplyLegacyMadScientist(middle, lossFraction) <= output)
			{
				low = middle;
			}
			else
			{
				high = middle - 1;
			}
		}

		return low;
	}

	private static int ApplyLegacyMadScientist(int maxHp, decimal lossFraction)
	{
		int loss = Math.Max(1, (int)Math.Floor(maxHp * lossFraction));
		return Math.Max(1, maxHp - loss);
	}

	private static int? ResolveFixedBonusTarget(int? rawMonsterMaxHp, IReadOnlyList<decimal> bonusFractions)
	{
		int? normalizedRawMaxHp = NormalizeRawMaxHp(rawMonsterMaxHp);
		if (normalizedRawMaxHp is not int rawMaxHp || bonusFractions.Count == 0)
		{
			return null;
		}

		decimal maxBonusFraction = bonusFractions.Count == 0
			? 0m
			: bonusFractions.Max();
		if (maxBonusFraction <= 0m)
		{
			return null;
		}

		decimal target = rawMaxHp + Math.Floor(rawMaxHp * maxBonusFraction);
		return (int)Math.Clamp(target, 1m, int.MaxValue);
	}

	private static int? NormalizeRawMaxHp(int? rawMonsterMaxHp)
	{
		return rawMonsterMaxHp is > 0 ? rawMonsterMaxHp : null;
	}
}
