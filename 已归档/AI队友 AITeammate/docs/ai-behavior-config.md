# AI Behavior Config

This mod keeps one shared AI implementation and changes personality through per-character config files.

The release config surface now has five main areas:

- `combat`
- `cardRewards`
- `potions`
- `shop`
- `events`

## Where the files live

- Nexus/mod install location: `mods\sts2AITeammate\config\ai-behavior\`
- Source repo location: `config/ai-behavior/`

Files shipped right now:

- `default.aiconfig`
- `ironclad.aiconfig`
- `silent.aiconfig`
- `defect.aiconfig`
- `regent.aiconfig`
- `necrobinder.aiconfig`

## Selection and fallback

The mod resolves the current AI teammate character and loads that character file first.

Fallback order:

1. character file
2. `default.aiconfig`
3. built-in code defaults

If a file is missing, malformed, or only partially filled out, the loader logs a warning and keeps going with fallback values instead of crashing.

## JSON shape

```json
{
  "schemaVersion": 4,
  "characterId": "ironclad",
  "displayName": "Ironclad",
  "combat": {},
  "cardRewards": {},
  "potions": {},
  "shop": {},
  "events": {}
}
```

You do not need to fill every field. Missing fields inherit from `default.aiconfig`, and anything still missing inherits from built-in defaults.

## Combat, card rewards, and potions

These sections control the shared AI's core behavior:

- `combat` tunes shared combat scoring and risk posture
- `cardRewards` tunes card reward evaluation and deck discipline
- `potions` tunes potion usage, acquisition, and full-slot replacement behavior

## Shop config

`shop` controls how the shared shop planner values cards, relics, potions, removal, and leftover gold.

### `shop.offerPriorities`

These are the easiest broad personality knobs.

- `cardPurchaseBias`
  Pushes the AI toward or away from buying cards in general.
- `relicPurchaseBias`
  Pushes the AI toward or away from relics in general.
- `potionPurchaseBias`
  Pushes the AI toward or away from buying potions.
- `removalServiceBias`
  Pushes the AI toward or away from merchant card removal.
- `cardAboveThresholdBonus`
  Extra reward for a card that clears the shop buy threshold.
- `cardBelowThresholdPenalty`
  Extra penalty for a card that fails the threshold.
- `saleBonus`
  How much the AI cares about sale pricing.
- `colorlessPremiumPenalty`
  How cautious the AI is about colorless card pricing.
- `goldReserveValuePerGold`
  How much the planner values leaving the shop with gold still saved.

Higher `goldReserveValuePerGold` means more hoarding. Lower or negative values make the AI spend more aggressively when it sees value.

### `shop.relicWeights`

These tune broad relic valuation without exposing every relic as its own custom config entry.

- rarity baselines: `ancientBaseline`, `rareBaseline`, `uncommonBaseline`, `commonBaseline`, `fallbackBaseline`
- `costDivisor`
  How strongly relic price reduces value.
- `specialRelicBonusMultiplier`
  Scales the built-in bonus values for especially strong general-purpose relics already recognized by the code.
- `strikeDummyBaseBonus`
  Baseline bonus for Strike Dummy when the deck still has enough strikes.
- `strikeDummyBonusPerStrike`
  Extra Strike Dummy value per strike.
- `duplicateMembershipPenalty`
- `duplicateCourierPenalty`

### `shop.removalWeights`

These tune how strongly the AI values deck cleanup at merchants.

- `burdenMultiplier`
  Multiplies the burden score of the best removal target.
- `smallDeckBonus`, `mediumDeckBonus`, `largeDeckBonus`
  Extra value for thinning decks at different deck sizes.
- `basicCardBonusPerCard`
  Reward for cleaning up strikes/defends and similar basics.
- `heavyCurveConsistencyBonus`
  Extra removal value when the deck is clunky.
- `noZeroCostConsistencyBonus`
  Extra cleanup value when the deck lacks cheap flexibility.
- `costDivisor`
  How strongly removal cost reduces value.

## Event config

`events` controls how the shared event planner values rewards, HP loss, risk, curses, and general upside.

### `events.outcomeWeights`

These are the broad reward-preference knobs.

- `relicRewardMultiplier`
- `potionRewardMultiplier`
- `fixedCardRewardMultiplier`
- `cardRewardBaselinePerReward`
- `cardRewardMultiplier`
- `removalRewardMultiplier`
- `upgradeRewardMultiplier`
- `transformRewardMultiplier`

These let one character care more about relics, another about removals, another about transforms, and so on.

Other useful fields here:

- `transformRemovalValueMultiplier`
  How much the event transform score cares about the removed card being bad.
- `transformReplacementBaselinePerCard`
  Expected upside of the new transformed card.
- `enchantBaselinePerCard`
- `maxHpGainValuePerPoint`
- `healValuePerPoint`
- `goldValueDivisor`
  Lower values make gold more valuable.

Upgrade-specific fields are also configurable here:

- `upgradeSpecBaseValue`
- `upgradeCostOverrideValuePerEnergy`
- `upgradeCostReductionValuePerEnergy`
- `upgradePositiveEffectValuePerPoint`
- `upgradeRetainBonus`
- `upgradeRemoveExhaustBonus`
- `upgradeRemoveEtherealBonus`
- `upgradeReplayIncreaseBonus`
- `upgradeBasicCardBonus`
- `upgradePowerCardBonus`

### `events.relicWeights`

These tune general relic valuation inside events.

- rarity baselines: `ancientBaseline`, `rareBaseline`, `uncommonBaseline`, `commonBaseline`, `fallbackBaseline`
- `specialRelicBonusMultiplier`
- `duplicateOwnedPenalty`

### `events.riskProfile`

These tune safety, greed, and risk tolerance in events.

- `hpPenaltyCriticalPerPoint`
- `hpPenaltyLowPerPoint`
- `hpPenaltyMidPerPoint`
- `hpPenaltyHealthyPerPoint`

Higher values make the AI avoid HP-loss options more strongly, especially at the matching health range.

Also configurable:

- `maxHpLossPenaltyPerPoint`
- `cursePenaltyMultiplier`
- `randomRewardDiscount`
- `randomGenericDiscount`
- `startsCombatPenalty`
- `unsupportedPenalty`
- `unknownEffectsPenalty`
- `lethalOptionPenalty`

Together, these control how willing the AI is to trade life for value, accept curses, take high-variance event outcomes, or accept options that lead into combat.

## What is now configurable

Shop knobs now exposed:

- card vs relic vs potion vs removal preference
- sale sensitivity
- gold conservation bias
- colorless-card caution
- relic rarity and price valuation
- special relic bonus scaling
- removal priority and cleanup pressure

Event knobs now exposed:

- relic, potion, fixed-card, card-reward, removal, upgrade, and transform preference
- transform expected value
- heal, max HP gain, and gold value
- HP-loss tolerance by current-health band
- curse tolerance
- randomness tolerance
- combat-follow-up caution

## What still stays hardcoded

Some logic remains in code on purpose because it is structural or would be too brittle to expose cleanly for release.

Still hardcoded:

- shop search structure, beam/depth limits, and legal-action sequencing
- event normalization and per-event handler coverage
- the exact built-in list of special relic/potion string patterns the heuristics recognize
- removal target burden feature extraction
- unsupported-option guardrails and event safety plumbing

These stay hardcoded because they are implementation mechanics more than user-facing personality knobs.

## How character files differ from `default.aiconfig`

`default.aiconfig` stays closest to the old shared baseline.

Character profiles now also differ in shop and event behavior:

- `ironclad.aiconfig`
  Spends a bit more aggressively, values relics and removals more, and accepts a little more HP loss for long-term value.
- `silent.aiconfig`
  Values removals and potion buys more, keeps more gold in reserve, and prefers safer event outcomes.
- `defect.aiconfig`
  Leans more toward relics, flexible value purchases, upgrades, and transforms.
- `regent.aiconfig`
  Strongest removal/discipline profile, more conservative spending, and safer event choices.
- `necrobinder.aiconfig`
  Spends more freely, values relics/cards more, and tolerates more event risk and variance.

## Validation and clamping

The loader clamps values to conservative ranges instead of trusting extreme edits blindly.

Examples:

- multipliers are clamped to safe bounded ranges
- large bonuses and penalties are bounded
- missing fields inherit fallback values
- malformed files fall back to defaults

## Example edits

### Make one character prioritize removals more

```json
{
  "shop": {
    "offerPriorities": {
      "removalServiceBias": 3.0
    },
    "removalWeights": {
      "burdenMultiplier": 1.2,
      "largeDeckBonus": 9.0
    }
  },
  "events": {
    "outcomeWeights": {
      "removalRewardMultiplier": 1.2
    }
  }
}
```

### Make one character hoard gold more

```json
{
  "shop": {
    "offerPriorities": {
      "goldReserveValuePerGold": 0.12,
      "cardPurchaseBias": -0.5
    },
    "relicWeights": {
      "costDivisor": 10.5
    }
  }
}
```

### Make one character take safer event outcomes

```json
{
  "events": {
    "riskProfile": {
      "hpPenaltyLowPerPoint": 4.4,
      "hpPenaltyMidPerPoint": 3.4,
      "cursePenaltyMultiplier": 1.15,
      "randomGenericDiscount": 12.0,
      "startsCombatPenalty": 10.0
    }
  }
}
```

### Make one character accept more HP loss for long-term value

```json
{
  "events": {
    "outcomeWeights": {
      "relicRewardMultiplier": 1.1,
      "upgradeRewardMultiplier": 1.1,
      "transformRewardMultiplier": 1.1
    },
    "riskProfile": {
      "hpPenaltyLowPerPoint": 3.2,
      "hpPenaltyMidPerPoint": 2.4,
      "hpPenaltyHealthyPerPoint": 1.6,
      "randomRewardDiscount": 4.5
    }
  }
}
```

## Suggested workflow for modders

1. Edit one character file.
2. Launch the game and confirm the config logs look correct.
3. Test a few combats, rewards, shops, or events with that teammate.
4. Compare the AI's picks and choices before making another change.
5. Iterate with small changes.
