#!/bin/zsh
set -euo pipefail

ROOT="/Users/iniad/sts2-mods/CustomDifficulty"
FILE_STEM="CustomDifficulty"
MANIFEST_SRC="$ROOT/assets/$FILE_STEM.json"
GAME_APP="/Users/iniad/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app"
GAME_BIN="$GAME_APP/Contents/MacOS/Slay the Spire 2"
MOD_DIR="$GAME_APP/Contents/MacOS/mods/$FILE_STEM"
BUILD_OUT="$ROOT/src/bin/Release/net9.0"
PROJECT_PATH="$ROOT/src/$FILE_STEM.csproj"

if (( ${+commands[dotnet]} )); then
  DOTNET_BIN="${commands[dotnet]}"
elif [[ -x "/opt/homebrew/bin/dotnet" ]]; then
  DOTNET_BIN="/opt/homebrew/bin/dotnet"
else
  print -u2 "Could not find a usable dotnet executable. Install .NET 9 SDK or add dotnet to PATH."
  exit 1
fi

DOTNET_ROOT="$(cd "$(dirname "$DOTNET_BIN")" && pwd)"

clean_macos_metadata() {
  local target="$1"
  [[ -d "$target" ]] || return 0

  find "$target" -name "__MACOSX" -type d -prune -exec rm -rf {} +
  find "$target" -name ".DS_Store" -type f -delete
  find "$target" -name "._*" -type f -delete
}

# 双版本构建：STS2_TARGET=0.107.1（公开分支，默认）或 0.108.0（beta 分支）。
STS2_TARGET="${STS2_TARGET:-0.107.1}"

rm -rf "$ROOT/src/bin" "$ROOT/src/obj" "$ROOT/dist"

DOTNET_ROOT="$DOTNET_ROOT" "$DOTNET_BIN" build "$PROJECT_PATH" -c Release -p:CustomDiffSts2Target="$STS2_TARGET"

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
  print -u2 "CustomDifficulty must ship as a standard single-DLL mod; mark references Private=false or remove package dependencies."
  exit 1
fi

mkdir -p "$ROOT/dist"
rm -rf "$MOD_DIR"

cp "$MANIFEST_SRC" "$ROOT/dist/$FILE_STEM.json"
if [[ "$STS2_TARGET" == 0.108* ]]; then
  sed -i '' 's/"min_game_version": "[^"]*"/"min_game_version": "0.108.0"/' "$ROOT/dist/$FILE_STEM.json"
fi

"$GAME_BIN" --headless \
  --path "$ROOT/tools" \
  -s res://pack_mod.gd -- \
  "$MANIFEST_SRC" \
  "$ROOT/dist/$FILE_STEM.pck"

cp "$MAIN_DLL" "$ROOT/dist/$FILE_STEM.dll"
clean_macos_metadata "$ROOT/dist"

mkdir -p "$MOD_DIR"
cp "$ROOT/dist/$FILE_STEM.json" "$MOD_DIR/$FILE_STEM.json"
cp "$ROOT/dist/$FILE_STEM.pck" "$MOD_DIR/$FILE_STEM.pck"
cp "$ROOT/dist/$FILE_STEM.dll" "$MOD_DIR/$FILE_STEM.dll"
clean_macos_metadata "$MOD_DIR"

echo "Deployed to $MOD_DIR"
