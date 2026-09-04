# AI Teammate Mod

AI Teammate is an experimental Slay the Spire 2 mod that adds AI-controlled teammates to a local fake-multiplayer run.

This repository is the public codebase for the mod. It is intended for players who want to use the release, and for tinkerers who want to inspect, tune, fork, or extend the project.

## What the mod currently does

- Adds AI-controlled teammates to a local fake-multiplayer run
- Lets AI teammates participate in combat, rewards, potion usage, shops, and many event flows
- Uses one mostly shared AI runtime rather than separate per-character planner implementations
- Ships with per-character behavior tuning files so different teammates can behave differently without duplicating the whole AI stack

The AI is functional, but it is still heuristic and experimental. It can make awkward or poor decisions, especially in edge cases.

## Current scope

The current public release is focused on:

- stable gameplay flow over maximum intelligence
- configurable behavior over large planner rewrites
- conservative handling of risky content

Current release-safety notes:

- `CrystalSphere` is excluded from AI teammate event pools for release safety
- several other events still rely on conservative fallback behavior instead of bespoke event intelligence

## High-level architecture

At a high level, the mod uses:

- a mostly shared AI runtime for combat, rewards, shops, potions, and events
- per-character config files to adjust personality and heuristic weights
- fallback/default loading so missing or malformed config fields do not crash the mod

Character differences come from config, not from fully separate planner classes.

## Behavior config

Shipped behavior config files live under:

- repo path: `config/ai-behavior/`
- installed mod path: `mods/sts2AITeammate/config/ai-behavior/`

The mod loads:

1. the current character file
2. `default.aiconfig`
3. built-in defaults

The current config surface covers:

- combat behavior
- card reward scoring
- potion heuristics
- shop valuation
- event valuation

See:

- [AI behavior config](docs/ai-behavior-config.md)
- [AI behavior preset ideas](docs/ai-behavior-presets.md)

## Build and run basics

For players:

- [Install guide](docs/INSTALL.md)
- [Current limitations](docs/LIMITATIONS.md)

For local builds:

- the project reads the game install path from the `Sts2Dir` MSBuild property
- by default, `sts2AITeammate.csproj` sets `Sts2Dir` to `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2 - ModDev`
- if your game lives somewhere else, you can either:
  - edit the default `Sts2Dir` value in `sts2AITeammate.csproj`
  - set the `STS2_DIR` environment variable
  - pass `Sts2Dir` directly on the command line
- then run `dotnet build`

Examples:

```powershell
$env:STS2_DIR = "D:\Games\Slay the Spire 2 - ModDev"
dotnet build
```

```powershell
dotnet build -p:Sts2Dir="D:\Games\Slay the Spire 2 - ModDev"
```

The project copies the built mod into the game's `mods/` folder after a successful build.

## Launching and capturing logs

For local testing, this repo assumes a mod-dev game folder that includes:

- `launch_opengl_moddev_debug.bat`
- `steam_appid.txt`

Example contents:

`launch_opengl_moddev_debug.bat`

```bat
@echo off
"%~dp0SlayTheSpire2.exe" --log --verbose -log Generic Debug --rendering-driver opengl3 %*
```

`steam_appid.txt`

```text
2868840
```

To launch the game and capture the output into the mod folder:

```powershell
./launch_opengl_moddev_debug.bat > ./mods/sts2AITeammate/aiteammate_launch_debug.log 2>&1
```

That log file is useful when testing AI behavior, room flow, and release packaging.

## Known limitations

- The AI is not a high-level strategic bot.
- Some event support is still generic or fallback-driven.
- Multiplayer-adjacent flows are more fragile than ordinary combat loops.
- The project should be treated as experimental rather than complete.

See:

- [Public limitations](docs/LIMITATIONS.md)
- [Project status for tinkerers](docs/STATUS.md)
- [Support and maintenance expectations](docs/SUPPORT.md)
- [Player-facing Nexus description draft](docs/NEXUS_DESCRIPTION.md)

## For contributors and tinkerers

If you want to inspect or extend the project, the best places to start are:

- `Scripts/AI/` for scoring, planners, config loading, and evaluation logic
- `Scripts/ActionLayer/` for live AI execution and game action wiring
- `config/ai-behavior/` for shipped character personalities
- `docs/` for public-facing usage, limits, and config notes

The implementation was built through iterative AI-assisted development and experimentation. If you plan to fork or extend it, inspect the code carefully and test changes in-game.

## License

This repository is licensed under the MIT License. See [LICENSE](LICENSE).
