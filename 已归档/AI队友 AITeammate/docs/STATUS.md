# Project Status

This document is for repo readers who want to inspect, tune, or extend the project.

## What is implemented

The current public codebase includes:

- a working AI teammate run flow
- combat decision-making through a shared heuristic runtime
- deterministic handling for many reward and card-selection flows
- per-character behavior tuning
- configurable combat, card reward, potion, shop, and event valuation
- public config and preset documentation

## Current architecture shape

The AI is mostly one shared runtime with character-specific tuning layered on top.

That means:

- planner and scorer code is mostly shared
- character personality differences come from config files
- missing or malformed config values fall back safely instead of crashing

## Areas that are intentionally rough

These parts are still heuristic or conservative by design:

- event coverage is incomplete
- some event handlers are only partial and remain fallback-driven
- generic event handling prefers caution over ambitious bespoke logic
- some multiplayer-adjacent screens and room-end flows may still need careful testing after changes

For release safety:

- `CrystalSphere` is excluded from AI teammate event pools
- several other events still stay on fallback behavior rather than trying to drive complex custom flows

## Good contribution targets

The most useful improvements are likely to be:

- safer event-specific handling for currently partial events
- better reward and shop heuristics
- improved edge-case testing around multiplayer-adjacent flows
- clearer config docs, examples, and presets
- bug fixes that reduce UI or synchronization fragility

## Where to look first

- `Scripts/AI/Config/`
  Config models, merge behavior, fallback loading, and validation
- `Scripts/AI/Combat/`
  Shared combat scoring and line planning
- `Scripts/AI/CardSelection/`
  Card reward evaluation
- `Scripts/AI/Shop/`
  Shop valuation and purchase planning
- `Scripts/AI/Events/`
  Event snapshotting, valuation, and special handlers
- `Scripts/ActionLayer/`
  Live execution against the game's runtime systems

## Practical expectations

- Treat the codebase as a working experimental mod, not a polished framework.
- Expect heuristic logic and conservative safety checks.
- Test gameplay-facing changes in-game instead of assuming planner-side logic is enough.
