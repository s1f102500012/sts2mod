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
IMPORT_PROJECT="$ROOT/.build/import_project"
DEFAULT_GODOT_EDITOR="$ROOT/../.tools/godot-4.5.1/Godot_mono.app/Contents/MacOS/Godot"
if [[ -z "${GODOT_EDITOR:-}" && -x "$DEFAULT_GODOT_EDITOR" ]]; then
  GODOT_EDITOR="$DEFAULT_GODOT_EDITOR"
else
  GODOT_EDITOR="${GODOT_EDITOR:-/opt/homebrew/bin/godot}"
fi

major_minor_version() {
  sed -E 's/^([0-9]+[.][0-9]+).*/\1/' <<< "$1"
}

clean_macos_metadata() {
  local target="$1"
  [[ -d "$target" ]] || return 0

  find "$target" -name "__MACOSX" -type d -prune -exec rm -rf {} +
  find "$target" -name ".DS_Store" -type f -delete
  find "$target" -name "._*" -type f -delete
}

GAME_GODOT_VERSION="$("$GAME_BIN" --version 2>/dev/null | head -n 1)"
IMPORT_GODOT_VERSION="$("$GODOT_EDITOR" --version 2>/dev/null | head -n 1)"
if [[ -n "$GAME_GODOT_VERSION" && -n "$IMPORT_GODOT_VERSION" \
  && "$(major_minor_version "$GAME_GODOT_VERSION")" != "$(major_minor_version "$IMPORT_GODOT_VERSION")" ]]; then
  print -u2 "Warning: asset import Godot version ($IMPORT_GODOT_VERSION) differs from game runtime ($GAME_GODOT_VERSION)."
  print -u2 "Set GODOT_EDITOR to a matching 4.5.x editor if mobile/runtime texture compatibility regresses."
fi

# 双版本构建：STS2_TARGET=0.107.1（公开分支，默认）或 0.108.0（beta 分支）。
STS2_TARGET="${STS2_TARGET:-0.107.1}"

rm -rf "$ROOT/src/bin" "$ROOT/src/obj" "$ROOT/dist" "$ROOT/.build"

"$DOTNET_BIN" build "$PROJECT_PATH" -c Release -p:EndlessSts2Target="$STS2_TARGET"

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
  print -u2 "EndlessMode must ship as a mobile-safe single-DLL mod; mark references Private=false or remove package dependencies."
  exit 1
fi

mkdir -p "$ROOT/dist"
rm -rf "$MOD_DIR"
mkdir -p "$IMPORT_PROJECT/$FILE_STEM"

cp "$MANIFEST_SRC" "$ROOT/dist/$FILE_STEM.json"
if [[ "$STS2_TARGET" != 0.107* ]]; then
  sed -i '' 's/"min_game_version": "[^"]*"/"min_game_version": "'"$STS2_TARGET"'"/' "$ROOT/dist/$FILE_STEM.json"
fi
cp "$ROOT/tools/project.godot" "$IMPORT_PROJECT/project.godot"
rsync -a --exclude "$FILE_STEM.json" "$ROOT/assets/" "$IMPORT_PROJECT/$FILE_STEM/"
clean_macos_metadata "$IMPORT_PROJECT"

"$GODOT_EDITOR" --headless \
  --path "$IMPORT_PROJECT" \
  --import

"$GAME_BIN" --headless \
  --path "$ROOT/tools" \
  -s res://pack_mod.gd -- \
  "$MANIFEST_SRC" \
  "$ROOT/dist/$FILE_STEM.pck" \
  "$IMPORT_PROJECT"

cp "$MAIN_DLL" "$ROOT/dist/$FILE_STEM.dll"

clean_macos_metadata "$ROOT/dist"

mkdir -p "$MOD_DIR"
cp "$ROOT/dist/$FILE_STEM.json" "$MOD_DIR/$FILE_STEM.json"
cp "$ROOT/dist/$FILE_STEM.pck" "$MOD_DIR/$FILE_STEM.pck"

cp "$ROOT/dist/$FILE_STEM.dll" "$MOD_DIR/$FILE_STEM.dll"

clean_macos_metadata "$MOD_DIR"

echo "Deployed to $MOD_DIR"
