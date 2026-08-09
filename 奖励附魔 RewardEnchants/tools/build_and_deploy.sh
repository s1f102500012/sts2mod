#!/bin/zsh
set -euo pipefail

ROOT="/Users/iniad/sts2-mods/RewardEnchants"
FILE_STEM="RewardEnchants"
MANIFEST_SRC="$ROOT/assets/$FILE_STEM.json"
VARIANT_MANIFEST="reward-enchants-variants.manifest"
GAME_APP="/Users/iniad/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app"
GAME_BIN="$GAME_APP/Contents/MacOS/Slay the Spire 2"
MOD_DIR="$GAME_APP/Contents/MacOS/mods/$FILE_STEM"
PROJECT_PATH="$ROOT/src/$FILE_STEM.csproj"
LOADER_PROJECT="$ROOT/loader/$FILE_STEM.Loader.csproj"
BUILD_ROOT="$ROOT/.build"
DIST="$ROOT/dist"
REFS_ROOT="/Users/iniad/sts2-mods/HextechRunes/versioned-dll-backups"
TARGETS=(0.107.1 0.110.1)
DEPLOY="${REWARD_ENCHANTS_DEPLOY:-1}"

rm -rf "$BUILD_ROOT" "$DIST"
mkdir -p "$BUILD_ROOT/implementation" "$BUILD_ROOT/loader" "$DIST/lib"

for target in "${TARGETS[@]}"; do
  refs="$REFS_ROOT/$target/game-refs"
  output="$BUILD_ROOT/implementation/$target"
  dotnet build "$PROJECT_PATH" \
    -c Release \
    -p:RewardEnchantsSts2Target="$target" \
    -p:GameDataDir="$refs" \
    -o "$output"
  mkdir -p "$DIST/lib/$target"
  cp "$output/$FILE_STEM.dll" "$DIST/lib/$target/$FILE_STEM.dll"
done

dotnet build "$LOADER_PROJECT" \
  -c Release \
  -p:GameDataDir="$REFS_ROOT/0.107.1/game-refs" \
  -o "$BUILD_ROOT/loader"

cp "$MANIFEST_SRC" "$DIST/$FILE_STEM.json"
cp "$BUILD_ROOT/loader/$FILE_STEM.Loader.dll" "$DIST/$FILE_STEM.dll"

"$GAME_BIN" --headless \
  --path "$ROOT/tools" \
  -s res://pack_mod.gd -- \
  "$MANIFEST_SRC" \
  "$DIST/$FILE_STEM.pck"

python3 "$ROOT/tools/multi_version_bundle.py" generate \
  --dist "$DIST" \
  --mod-id "$FILE_STEM" \
  --manifest-name "$VARIANT_MANIFEST" \
  --target 0.107.1 \
  --target 0.110.1

python3 "$ROOT/tools/multi_version_bundle.py" validate \
  --dist "$DIST" \
  --mod-id "$FILE_STEM" \
  --manifest-name "$VARIANT_MANIFEST"

if [[ "$DEPLOY" == "1" ]]; then
  mkdir -p "$MOD_DIR"
  rsync -a --delete "$DIST/" "$MOD_DIR/"
  echo "Deployed to $MOD_DIR"
else
  echo "Built bundle at $DIST (deployment disabled)"
fi
