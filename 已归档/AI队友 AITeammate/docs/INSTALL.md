# Install

## For players

This mod install includes:

- one `.dll`
- one `.json`
- a `config\ai-behavior\` folder with the shipped AI behavior files

Place them in this folder:

```text
xxx\Steam\steamapps\common\Slay the Spire 2\mods\sts2AITeammate\
```

Example final folder structure:

```text
xxx\Steam\steamapps\common\Slay the Spire 2\mods\sts2AITeammate\
    sts2AITeammate.dll
    sts2AITeammate.json
    config\
        ai-behavior\
            default.aiconfig
            ironclad.aiconfig
            silent.aiconfig
            defect.aiconfig
            regent.aiconfig
            necrobinder.aiconfig
```

Install steps:

1. Install the mod loader and any dependencies required for Slay the Spire 2 mods.
2. Create the folder `xxx\Steam\steamapps\common\Slay the Spire 2\mods\sts2AITeammate\` if it does not already exist.
3. Copy the mod `.dll`, `.json`, and `config` folder into that folder.
4. If you want to use an existing non-modded save, copy your save files into the modded save folder before launching the game.
5. Launch the game. On first detection, the game should prompt you to load the mod.
6. Launch again with the mod enabled.

## How to verify it loaded

- Start the game with mods enabled.
- Look for the AI Teammate menu/setup entry added by the mod.
- Start an AI teammate run and confirm AI companions appear in the run setup and act on their own once the run begins.

## Save files

Slay the Spire 2 keeps vanilla and modded saves separately.

If you want to use your existing non-modded save while playing with mods, copy your existing profile folders into the `modded` folder under:

```text
C:\Users\[YourUserName]\AppData\Roaming\SlayTheSpire2\steam\[YourSteamID]\modded
```

Example source folders:

```text
C:\Users\[YourUserName]\AppData\Roaming\SlayTheSpire2\steam\[YourSteamID]\profile1
C:\Users\[YourUserName]\AppData\Roaming\SlayTheSpire2\steam\[YourSteamID]\profile2
C:\Users\[YourUserName]\AppData\Roaming\SlayTheSpire2\steam\[YourSteamID]\profile3
```

Copy those folders into:

```text
C:\Users\[YourUserName]\AppData\Roaming\SlayTheSpire2\steam\[YourSteamID]\modded
```

If the `modded` folder does not exist yet, create it first.

## How to uninstall

- Remove the `sts2AITeammate` folder from your game's `mods\` directory.

## For local builds

Before building, point the project at your Slay the Spire 2 install by setting either:

- the `STS2_DIR` environment variable
- or the `Sts2Dir` MSBuild property

Example PowerShell session:

```powershell
$env:STS2_DIR = "D:\Games\Slay the Spire 2 - ModDev"
dotnet build
```

The post-build step copies the mod output into `$(Sts2Dir)\mods\sts2AITeammate\`.
