# AI Behavior Preset Ideas

These are example value sets for modders. They are not auto-loaded by the mod. Copy the parts you want into a character JSON file.

## Safer Defender

```json
{
  "combat": {
    "riskProfile": {
      "survivalWeight": 1.15,
      "defenseWeight": 1.12,
      "damageTakenPenaltyPerPoint": 36,
      "perfectDefenseBonus": 72
    }
  },
  "cardRewards": {
    "disciplineWeights": {
      "rewardSkipThreshold": 13.5,
      "duplicatePenaltyPerCopy": 4.5
    }
  },
  "shop": {
    "offerPriorities": {
      "removalServiceBias": 2.5,
      "goldReserveValuePerGold": 0.08
    }
  },
  "events": {
    "riskProfile": {
      "hpPenaltyLowPerPoint": 4.3,
      "hpPenaltyMidPerPoint": 3.3,
      "cursePenaltyMultiplier": 1.15
    }
  }
}
```

## Spendier Relic Hunter

```json
{
  "shop": {
    "offerPriorities": {
      "relicPurchaseBias": 2.5,
      "goldReserveValuePerGold": -0.03
    },
    "relicWeights": {
      "rareBaseline": 23.0,
      "uncommonBaseline": 16.0,
      "specialRelicBonusMultiplier": 1.15
    }
  },
  "events": {
    "outcomeWeights": {
      "relicRewardMultiplier": 1.15
    }
  }
}
```

## Deck Cleanup

```json
{
  "shop": {
    "offerPriorities": {
      "removalServiceBias": 3.0
    },
    "removalWeights": {
      "burdenMultiplier": 1.2,
      "mediumDeckBonus": 5.0,
      "largeDeckBonus": 8.0
    }
  },
  "events": {
    "outcomeWeights": {
      "removalRewardMultiplier": 1.2,
      "upgradeRewardMultiplier": 1.1
    }
  }
}
```

## Gold Hoarder

```json
{
  "shop": {
    "offerPriorities": {
      "cardPurchaseBias": -0.5,
      "goldReserveValuePerGold": 0.12
    },
    "relicWeights": {
      "costDivisor": 10.0
    }
  },
  "events": {
    "outcomeWeights": {
      "goldValueDivisor": 10.5
    }
  }
}
```

## Risk-Taking Event Gambler

```json
{
  "events": {
    "outcomeWeights": {
      "relicRewardMultiplier": 1.1,
      "transformRewardMultiplier": 1.15
    },
    "riskProfile": {
      "hpPenaltyLowPerPoint": 3.1,
      "hpPenaltyMidPerPoint": 2.3,
      "cursePenaltyMultiplier": 0.85,
      "randomRewardDiscount": 4.5,
      "randomGenericDiscount": 8.0,
      "startsCombatPenalty": 6.0
    }
  }
}
```

## Selective Shopper

```json
{
  "cardRewards": {
    "disciplineWeights": {
      "shopSkipThresholdBase": 24.0,
      "shopSkipThresholdCostFactor": 0.12
    }
  },
  "shop": {
    "offerPriorities": {
      "cardPurchaseBias": -1.0,
      "goldReserveValuePerGold": 0.08
    }
  }
}
```
