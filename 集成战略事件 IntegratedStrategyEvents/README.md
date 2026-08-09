# 集成战略事件

RitsuLib-based event-content mod for adding Slay the Spire 2 events inspired by Arknights Integrated Strategies. 同一个模组包支持 STS2 0.107.1 与 0.110.1，并依赖 RitsuLib 0.5.10（Workshop 3747602295）。

## Structure

- `src/Events/IntegratedStrategyEventModel.cs`: shared BaseLib event identity, owner, localization, act, and spawn-rule integration.
- `src/Events/IntegratedStrategyEventModel.*.cs`: focused event-facing helpers for navigation, option previews, HP/gold, rewards, and deck operations.
- `src/Events/IntegratedStrategyEventDefinition.cs`: event portrait factory, localization, and layout definition contract.
- `src/Events/IntegratedStrategyEventLocalization.cs`: shared current-language selection for event definition localization.
- `src/Events/IntegratedStrategyEventEffects.cs`: shared event effect facade.
- `src/Events/IntegratedStrategyEventEffects.*.cs`: focused effect implementations for HP/gold, reward delivery, card grants, and deck operations.
- `src/Events/IntegratedStrategyEventRewards.cs`: shared random reward facade.
- `src/Events/IntegratedStrategyEventRewards.*.cs`: focused random reward factories for relics, potions, card reward screens, card rolls, and card pool construction.
- `src/Events/IntegratedStrategyEventSpawnRules.cs`: spawn-rule facade used by event models.
- `src/Events/IntegratedStrategyEventSpawnRules.Acts.cs`: act-specific event pool restrictions.
- `src/Events/IntegratedStrategyEventSpawnRules.Gates.cs`: run-state gates such as HP, gold, deck, or card-pool requirements.
- `src/Events/*Event.cs`: one event flow per file. These files should only describe choices, branching, and page transitions.
- `src/Events/*Event.*.cs`: event-specific partial helpers for branch tables or other private mechanics that would otherwise clutter the main event flow.
- `src/Events/Definitions/*Event.Definition.cs`: one definition partial per event. These files own portrait file names, layout profiles, and localization.
- `src/IntegratedStrategyContentCatalog.cs`: combined model registry facade.
- `src/IntegratedStrategyContentCatalog.Events.cs`: registered event flow types.
- `src/IntegratedStrategyContentCatalog.Relics.cs`: registered event relic types.
- `src/IntegratedStrategyContentCatalog.Encounters.cs`: registered encounter and encounter hook types.
- `src/IntegratedStrategyContent.cs`: compatibility facade exposing all registered model types to the initializer.
- `src/ModEntry.cs`: initializer and pool registration; event relic pool registration is sourced from `IntegratedStrategyContentCatalog.EventRelicTypes`.
- `src/Encounters/IntegratedStrategyEliteEncounter.cs`: shared base for event-only elite encounters that are not added to normal act pools.
- `src/Encounters/IntegratedStrategyEncounterHook.cs`: shared combat hook entry for event-only encounter setup.
- `src/Encounters/IntegratedStrategyTwoSidedEliteEncounter.cs`: shared Kaiser Crab-style scene, slots, camera, and monster generation for flanking encounters.
- `src/Encounters/IntegratedStrategyEncounterSetup.cs`: combat-start helpers for finding custom encounters, forcing first moves, flanking powers, and visual facing.
- `src/Encounters/*Encounter.cs`: one custom encounter per file. These files should describe only monster composition, special scenes, and encounter-specific monster state.
- `src/Encounters/*EncounterHook.cs`: optional combat-start hooks inheriting `IntegratedStrategyEncounterHook<TEncounter>` for first moves, powers, or custom setup that cannot be expressed in the encounter model alone.
- `src/Relics/*Relic.cs`: one event relic identity per file, with shared event-relic icon behavior in `IntegratedStrategyEventRelic`.
- `src/Relics/*Relic.*.cs`: focused partial helpers for multi-mechanic relic behavior such as pickup effects and combat hooks.
- `src/UI/*Patch.cs`: focused UI patches for full-screen portraits, event layout, and hover-tip alignment.
- `src/UI/IntegratedStrategyEventPortrait*.cs`: split event portrait implementation for patch entry, viewport fitting, and process-frame reapplication.
- `src/UI/IntegratedStrategyEventLayout*.cs`: split event layout implementation for patch entry, layout application, shared event access, baseline caching, geometry, and node width application.
- `assets/IntegratedStrategyEvents.json`: manifest.
- `assets/images/events/`: imported event portrait images. Use 16:9 PNGs when possible.
- `loader/`: stable root loader that selects a separately compiled implementation from `lib/<game-version>/`.
- `tools/build_and_deploy.sh`: builds both game-version variants, packs resources, validates the bundle, and deploys it.
- `tools/validate_event_structure.sh`: checks that event flows, definitions, and assets stay separated.

## Adding Events

1. Create a new `partial` event flow class under `src/Events/` that inherits `IntegratedStrategyEventModel`.
2. Create the matching `src/Events/Definitions/<EventName>.Definition.cs` partial with `IntegratedStrategyEventDefinition.ForEventPortrait(...)`.
3. Put localization in the definition file with `IntegratedStrategyEventLocalization.ForCurrentLanguage`, `EventLoc`, `EventPageLoc`, and `EventOptionLoc`.
4. Put the portrait under `assets/images/events/` and pass its file name to `ForEventPortrait(...)`.
5. Use `IntegratedStrategyEventLayoutProfile.Standard` or a named left-side layout profile from the definition file.
6. Implement `GenerateInitialOptions()` and event branches in the flow class using shared helpers such as `HpChoice`, `GoldChoice`, `RelicChoice`, `CardPreviewChoice`, `FightChoice<TEncounter>`, `ShowFightPage<TEncounter>`, `EnterEventCombat<TEncounter>`, `LoseHpAndGainMaxHp`, `OfferRegularCardReward`, `OfferRareCardReward`, `OfferColorlessCardReward`, `ObtainRandomRelic`, and `OfferRandomPotionReward`.
7. Add the new event type to `IntegratedStrategyContentCatalog.Events.cs`.
8. Add act restrictions in `IntegratedStrategyEventSpawnRules.Acts.cs` and resource/deck gates in `IntegratedStrategyEventSpawnRules.Gates.cs` when the event should not be globally eligible.
9. Keep `event_options.txt`, `event_descriptions.txt`, and `event_refresh_conditions.txt` in sync as human review summaries.
10. Run `tools/validate_event_structure.sh`, then `tools/build_and_deploy.sh`; both 0.107.1 and 0.110.1 variants must compile.

## Adding Event Relics

1. Create one `src/Relics/<RelicName>Relic.cs` file inheriting `IntegratedStrategyEventRelic` unless the relic needs special image behavior.
2. If the relic has several independent mechanisms, split them into `src/Relics/<RelicName>Relic.<Mechanism>.cs` partial files.
3. Add the relic type once to `IntegratedStrategyContentCatalog.Relics.cs`.
4. Do not add a second explicit `ModHelper.AddModelToPool<EventRelicPool, ...>()` line in `ModEntry`; pool registration reads from the catalog.

## Adding Encounters

1. Create the encounter and any matching hook/setup files under `src/Encounters/`.
2. Inherit `IntegratedStrategyEliteEncounter` for normal event-only elite encounters.
3. Inherit `IntegratedStrategyTwoSidedEliteEncounter<TMonster>` for Kaiser Crab-style flanking encounters, overriding `CreateLeftMonster()` or `CreateRightMonster()` only for monster-specific starting state.
4. Inherit `IntegratedStrategyEncounterHook<TEncounter>` for combat-start setup, and put shared setup operations in `IntegratedStrategyEncounterSetup` when several encounters need the same positioning, reward, first-move, power, or visual-facing pattern.
5. Add encounter and hook model types to `IntegratedStrategyContentCatalog.Encounters.cs`.
6. Keep event flow classes responsible only for routing into the encounter.
