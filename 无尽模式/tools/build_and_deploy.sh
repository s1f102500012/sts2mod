#!/bin/zsh
set -euo pipefail

ROOT="/Users/iniad/sts2-mods/EndlessMode"
FILE_STEM="EndlessMode"
MANIFEST_SRC="$ROOT/assets/$FILE_STEM.json"
GAME_APP="/Users/iniad/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app"
GAME_BIN="$GAME_APP/Contents/MacOS/Slay the Spire 2"
MOD_DIR="$GAME_APP/Contents/MacOS/mods/$FILE_STEM"
BUILD_OUT="$ROOT/src/bin/Release/net9.0"
PROJECT_PATH="$ROOT/src/$FILE_STEM.csproj"
DOTNET_BIN="/opt/homebrew/bin/dotnet"

rm -rf "$ROOT/src/bin" "$ROOT/src/obj" "$ROOT/dist"

"$DOTNET_BIN" build "$PROJECT_PATH" -c Release

mkdir -p "$ROOT/dist"
rm -rf "$MOD_DIR"

cp "$MANIFEST_SRC" "$ROOT/dist/$FILE_STEM.json"

"$GAME_BIN" --headless \
  --path "$ROOT/tools" \
  -s res://pack_mod.gd -- \
  "$MANIFEST_SRC" \
  "$ROOT/dist/$FILE_STEM.pck"

for dll in "$BUILD_OUT"/*.dll; do
  base_name="$(basename "$dll")"
  case "$base_name" in
    sts2.dll|GodotSharp.dll)
      continue
      ;;
  esac

  cp "$dll" "$ROOT/dist/$base_name"
done

mkdir -p "$MOD_DIR"
cp "$ROOT/dist/$FILE_STEM.json" "$MOD_DIR/$FILE_STEM.json"
cp "$ROOT/dist/$FILE_STEM.pck" "$MOD_DIR/$FILE_STEM.pck"

for dll in "$ROOT/dist"/*.dll; do
  cp "$dll" "$MOD_DIR/$(basename "$dll")"
done

echo "Deployed to $MOD_DIR"
