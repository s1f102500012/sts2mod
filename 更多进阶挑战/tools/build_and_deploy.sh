#!/bin/zsh
set -euo pipefail

ROOT="/Users/iniad/sts2-mods/MoreAscensionChallenge"
FILE_STEM="MoreAscensionChallenge"
MANIFEST_SRC="$ROOT/assets/$FILE_STEM.json"
GAME_APP="/Users/iniad/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app"
MOD_DIR="$GAME_APP/Contents/MacOS/mods/$FILE_STEM"
BUILD_OUT="$ROOT/src/bin/Release/net9.0"
PROJECT_PATH="$ROOT/src/$FILE_STEM.csproj"

rm -rf "$ROOT/src/bin" "$ROOT/src/obj" "$ROOT/dist"

dotnet build "$PROJECT_PATH" -c Release

mkdir -p "$ROOT/dist"
rm -rf "$MOD_DIR"
mkdir -p "$MOD_DIR"

cp "$MANIFEST_SRC" "$ROOT/dist/$FILE_STEM.json"
cp "$ROOT/dist/$FILE_STEM.json" "$MOD_DIR/$FILE_STEM.json"

for dll in "$BUILD_OUT"/*.dll; do
  base_name="$(basename "$dll")"
  case "$base_name" in
    sts2.dll|GodotSharp.dll)
      continue
      ;;
  esac

  cp "$dll" "$ROOT/dist/$base_name"
  cp "$dll" "$MOD_DIR/$base_name"
done

echo "Deployed to $MOD_DIR"
