#!/bin/zsh
set -euo pipefail

ROOT="/Users/iniad/sts2-mods/StS1Act4"
MOD_ID="StS1Act4"
MANIFEST_SRC="$ROOT/assets/$MOD_ID.json"
GAME_APP="/Users/iniad/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app"
GAME_BIN="$GAME_APP/Contents/MacOS/Slay the Spire 2"
MOD_DIR="$GAME_APP/Contents/MacOS/mods/$MOD_ID"
BUILD_OUT="$ROOT/src/bin/Release/net9.0"

rm -rf "$ROOT/src/bin" "$ROOT/src/obj" "$ROOT/dist"
dotnet build "$ROOT/src/$MOD_ID.csproj" -c Release

mkdir -p "$ROOT/dist" "$MOD_DIR"
rm -rf "$MOD_DIR"
mkdir -p "$MOD_DIR"

cp "$MANIFEST_SRC" "$ROOT/dist/$MOD_ID.json"

"$GAME_BIN" --headless \
  --path "$ROOT/tools" \
  -s res://pack_mod.gd -- \
  "$MANIFEST_SRC" \
  "$ROOT/dist/$MOD_ID.pck"

cp "$ROOT/dist/$MOD_ID.pck" "$MOD_DIR/$MOD_ID.pck"

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

cp "$ROOT/dist/$MOD_ID.json" "$MOD_DIR/$MOD_ID.json"

echo "Deployed to $MOD_DIR"
