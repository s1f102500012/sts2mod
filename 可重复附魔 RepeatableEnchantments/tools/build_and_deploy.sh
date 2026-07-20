#!/bin/zsh
set -euo pipefail

export PATH="/opt/homebrew/opt/dotnet/bin:/opt/homebrew/opt/dotnet@9/bin:/opt/homebrew/opt/dotnet@8/bin:/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin:${PATH:-}"

ROOT="/Users/iniad/sts2-mods/RepeatableEnchantments"
FILE_STEM="RepeatableEnchantments"
MANIFEST_SRC="$ROOT/assets/$FILE_STEM.json"
DOTNET_BIN="${STS2_DOTNET_BIN:-/opt/homebrew/bin/dotnet}"
GAME_APP="/Users/iniad/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app"
GAME_BIN="$GAME_APP/Contents/MacOS/Slay the Spire 2"
MOD_DIR="$GAME_APP/Contents/MacOS/mods/$FILE_STEM"
BUILD_OUT="$ROOT/src/bin/Release/net9.0"
PROJECT_PATH="$ROOT/src/$FILE_STEM.csproj"
VERIFY_SCRIPT="/Users/iniad/sts2-mods/tools/verify_headless_load.sh"
VERIFY_AFTER_DEPLOY="${STS2_VERIFY_AFTER_DEPLOY:-0}"

rm -rf "$ROOT/src/bin" "$ROOT/src/obj" "$ROOT/dist"

"$DOTNET_BIN" build "$PROJECT_PATH" -c Release

MAIN_DLL="$BUILD_OUT/$FILE_STEM.dll"
if [[ ! -f "$MAIN_DLL" ]]; then
  print -u2 "Missing main mod DLL after build: $MAIN_DLL"
  exit 1
fi

EXTRA_DLLS=()
for dll in "$BUILD_OUT"/*.dll(N); do
  base_name="$(basename "$dll")"
  if [[ "$base_name" != "$FILE_STEM.dll" ]]; then
    EXTRA_DLLS+=("$base_name")
  fi
done

if (( ${#EXTRA_DLLS[@]} > 0 )); then
  print -u2 "Unexpected dependency DLLs in build output: ${EXTRA_DLLS[*]}"
  print -u2 "RepeatableEnchantments must ship as a single-DLL mod; mark references Private=false or remove package dependencies."
  exit 1
fi

mkdir -p "$ROOT/dist"
rm -rf "$MOD_DIR"

cp "$MANIFEST_SRC" "$ROOT/dist/$FILE_STEM.json"

"$GAME_BIN" --headless \
  --path "$ROOT/tools" \
  -s res://pack_mod.gd -- \
  "$MANIFEST_SRC" \
  "$ROOT/dist/$FILE_STEM.pck"

cp "$MAIN_DLL" "$ROOT/dist/$FILE_STEM.dll"

mkdir -p "$MOD_DIR"
cp "$ROOT/dist/$FILE_STEM.json" "$MOD_DIR/$FILE_STEM.json"
cp "$ROOT/dist/$FILE_STEM.pck" "$MOD_DIR/$FILE_STEM.pck"

cp "$ROOT/dist/$FILE_STEM.dll" "$MOD_DIR/$FILE_STEM.dll"

echo "Deployed to $MOD_DIR"

if [[ "$VERIFY_AFTER_DEPLOY" == "1" && -x "$VERIFY_SCRIPT" ]]; then
  STS2_HEADLESS_LOG_FILE="$ROOT/dist/startup-check.log" "$VERIFY_SCRIPT" "$FILE_STEM"
fi
