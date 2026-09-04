# AGENTS.md

This file gives repo-specific instructions to coding agents working on this project.

## Project context
This project is a Slay the Spire 2 mod prototype focused on building an AI teammate / AI-player feature.

The user is still learning:
- C#
- modding workflow
- game launch/debug workflow
- how to inspect decompiled game code

The goal is to maintain steady progress with small, verifiable steps.

---

## Main workflow rules
- Prefer small, incremental changes.
- Prefer changes that are easy to verify in-game.
- Each task should produce one visible or testable result.
- Do not perform large refactors unless clearly necessary.
- Preserve existing user code and comments unless clearly obsolete.

---


## Documentation maintenance
Always ensure a `docs/` folder exists.

### debugging-notes.md
Keep short, practical notes:
- launch commands
- flags
- log locations
- verification steps
- common issues

### modding-decisions.md
Record decisions and reasoning:
- chosen patch points
- UI approach
- workflow changes
- rejected approaches (if relevant)

---

## Code change guidance
- Favor minimal, targeted changes.
- Prefer patching/extending existing logic instead of rewriting systems.
- Add clear logging for new behavior:
  - mod loaded
  - menu injected
  - button clicked
- Use simple and readable code.

---

## Implementation approach
When implementing a feature:

1. Identify relevant classes/methods using the decompiled code.
2. Choose the smallest viable patch point.
3. Implement a minimal version first (placeholder behavior is fine).
4. Add logging for verification.
5. Ensure the result is visible/testable in-game.

Do not jump directly to full feature implementation.

---

## Communication guidance
When summarizing work:
- what was changed
- which class/method was used
- why that location was chosen
- how to test it
- expected result in-game
- what was added to docs

Separate facts from assumptions.

---

## Preferred mentality
The goal is not perfect understanding.

The goal is:
- keep moving
- use real game code as source of truth
- validate changes quickly in-game
- build confidence through small working steps