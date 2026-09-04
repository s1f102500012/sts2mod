#!/bin/zsh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
MOD_ID="sts2AITeammate"
GAME_APP="${STS2_GAME_APP:-/Users/iniad/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app}"
GAME_ROOT="$(cd "$GAME_APP/.." && pwd)"
GAME_BIN="$GAME_APP/Contents/MacOS/Slay the Spire 2"
DATA_DIR="$GAME_APP/Contents/Resources/data_sts2_macos_arm64"
PROJECT_PATH="$ROOT/$MOD_ID.csproj"
BUILD_OUT="$ROOT/.godot/mono/temp/bin/Release"
DIST_DIR="$ROOT/dist"
PACK_ASSETS="$ROOT/.build/pack_assets"
DEPLOY="${STS2_AI_DEPLOY:-1}"

find_dotnet() {
  if command -v dotnet >/dev/null 2>&1; then
    command -v dotnet
  elif [[ -x "/opt/homebrew/Cellar/dotnet/9.0.8/libexec/dotnet" ]]; then
    echo "/opt/homebrew/Cellar/dotnet/9.0.8/libexec/dotnet"
  elif [[ -x "/opt/homebrew/bin/dotnet" ]]; then
    echo "/opt/homebrew/bin/dotnet"
  else
    return 1
  fi
}

clean_macos_metadata() {
  local target="$1"
  [[ -d "$target" ]] || return 0

  find "$target" -name "__MACOSX" -type d -prune -exec rm -rf {} +
  find "$target" -name ".DS_Store" -type f -delete
  find "$target" -name "._*" -type f -delete
}

if [[ ! -x "$GAME_BIN" ]]; then
  print -u2 "Game binary not found or not executable: $GAME_BIN"
  exit 1
fi

dotnet_bin="$(find_dotnet)" || {
  print -u2 "Could not find dotnet. Install .NET 9 SDK or add dotnet to PATH."
  exit 1
}
dotnet_root="$(cd "$(dirname "$dotnet_bin")" && pwd)"

rm -rf "$ROOT/dist" "$ROOT/.build"
mkdir -p "$DIST_DIR" "$PACK_ASSETS"

DOTNET_ROOT="$dotnet_root" "$dotnet_bin" build "$PROJECT_PATH" -c Release \
  -p:Sts2DataDir="$DATA_DIR"

cp "$ROOT/$MOD_ID.json" "$DIST_DIR/$MOD_ID.json"
cp "$BUILD_OUT/$MOD_ID.dll" "$DIST_DIR/$MOD_ID.dll"

cp "$ROOT/$MOD_ID.json" "$PACK_ASSETS/$MOD_ID.json"
if [[ -d "$ROOT/config" ]]; then
  rsync -a --delete "$ROOT/config/" "$PACK_ASSETS/config/"
fi
if [[ -d "$ROOT/config/localization" ]]; then
  mkdir -p "$PACK_ASSETS/localization"
  rsync -a --delete "$ROOT/config/localization/" "$PACK_ASSETS/localization/"
fi
clean_macos_metadata "$PACK_ASSETS"

"$GAME_BIN" --headless \
  --path "$ROOT" \
  -s res://tools/pack_mod.gd -- \
  "$PACK_ASSETS/$MOD_ID.json" \
  "$DIST_DIR/$MOD_ID.pck"

clean_macos_metadata "$DIST_DIR"

if [[ "$DEPLOY" != "0" ]]; then
  deploy_dirs=(
    "$GAME_APP/Contents/MacOS/mods/$MOD_ID"
    "$GAME_APP/Contents/Resources/mods/$MOD_ID"
    "$GAME_ROOT/mods/$MOD_ID"
  )

  for mod_dir in "${deploy_dirs[@]}"; do
    rm -rf "$mod_dir"
    mkdir -p "$mod_dir"
    cp "$DIST_DIR/$MOD_ID.dll" "$mod_dir/$MOD_ID.dll"
    cp "$DIST_DIR/$MOD_ID.json" "$mod_dir/$MOD_ID.json"
    cp "$DIST_DIR/$MOD_ID.pck" "$mod_dir/$MOD_ID.pck"
    clean_macos_metadata "$mod_dir"
    echo "Deployed $MOD_ID to $mod_dir"
  done
else
  echo "Built release artifacts in $DIST_DIR without deploying."
fi
