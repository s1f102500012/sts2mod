# Debugging Notes

## AI teammate character setup UI

- Build check:
  `dotnet build sts2AITeammate.csproj -p:Sts2Dir='/Users/iniad/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS' -p:Sts2DataDir='/Users/iniad/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/Resources/data_sts2_macos_arm64' --no-restore`
- In-game verification path: main menu -> AI Teammate -> setup screen -> click a player slot -> picker screen.
- Expected result: setup and picker no longer use a large custom black content panel; slot controls sit in the stock `CharSelectButtons/ButtonContainer` region from `character_select_screen.tscn`.
- Expected result: slot and picker portrait controls use `res://scenes/screens/char_select/char_select_button.tscn` as the visible node, with the mod's `Button` acting only as the transparent hit target.
- Expected result: the setup ready control uses the visible stock node from `res://scenes/ui/confirm_button.tscn`, and the remote player list uses the duplicated stock `RemotePlayerContainer`.
- Expected result: Chinese UI strings do not insert spaces around `AI`, and remote lobby display names are localized through `AiTeammateLocalization`.
- Localization files should be copied by the project config into the deployed mod folder:
  `mods/sts2AITeammate/localization/eng/ai_teammate_ui.loc`
  `mods/sts2AITeammate/localization/zhs/ai_teammate_ui.loc`

## AI combat strategy validation

- Latest run review source: `~/Library/Application Support/SlayTheSpire2/logs/godot.log`.
- A boss combat can log `currentRoom=CombatRoom`; check `RoomType` before deciding whether potion scoring should receive elite/boss bonuses.
- Build and deploy check:
  `dotnet build sts2AITeammate.csproj /p:Sts2Dir="$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS" /p:Sts2DataDir="$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/Resources/data_sts2_macos_arm64"`
- Headless load check:
  `../tools/verify_headless_load.sh sts2AITeammate`
