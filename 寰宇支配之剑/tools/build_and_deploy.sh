#!/bin/zsh
set -euo pipefail

SCRIPT_DIR="${0:A:h}"
ROOT="${SCRIPT_DIR:h}"
FILE_STEM="UniversalDominionSword"
MANIFEST_SRC="$ROOT/assets/$FILE_STEM.json"
VARIANT_MANIFEST_NAME="universal-dominion-sword-variants.manifest"
GAME_APP="${STS2_GAME_APP:-$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app}"
GAME_BIN="$GAME_APP/Contents/MacOS/Slay the Spire 2"
MOD_DIR="$GAME_APP/Contents/MacOS/mods/$FILE_STEM"
WORKSHOP_CONTENT="$ROOT/workshop/content"
REFS_ROOT="${UDS_REFS_ROOT:-$ROOT/../HextechRunes/versioned-dll-backups}"
BUILD_ROOT="$ROOT/.build"
DIST="$ROOT/dist"
VARIANT_PROJECT="$ROOT/src/$FILE_STEM.csproj"
LOADER_PROJECT="$ROOT/loader/$FILE_STEM.Loader.csproj"
UDS_DEPLOY="${UDS_DEPLOY:-1}"
TARGETS=(0.107.1 0.110.0)
MULTI_VERSION_SKILL_ROOT="${UDS_MULTI_VERSION_SKILL_ROOT:-$HOME/.codex/skills/sts2-build-multi-version-bundle}"
VARIANT_MANIFEST_GENERATOR="$MULTI_VERSION_SKILL_ROOT/scripts/generate_variant_manifest.py"
VARIANT_BUNDLE_VALIDATOR="$MULTI_VERSION_SKILL_ROOT/scripts/validate_variant_bundle.py"

if (( ${+commands[dotnet]} )); then
	DOTNET_BIN="${commands[dotnet]}"
elif [[ -x "/opt/homebrew/bin/dotnet" ]]; then
	DOTNET_BIN="/opt/homebrew/bin/dotnet"
else
	print -u2 "Could not find a usable .NET 9 SDK."
	exit 1
fi

clean_directory() {
	local directory="$1"
	mkdir -p "$directory"
	find "$directory" -mindepth 1 -depth -delete
}

for target in "${TARGETS[@]}"; do
	refs="$REFS_ROOT/$target/game-refs"
	for reference in sts2.dll GodotSharp.dll 0Harmony.dll; do
		if [[ ! -f "$refs/$reference" ]]; then
			print -u2 "Missing reference for STS2 $target: $refs/$reference"
			exit 1
		fi
	done
done
for helper in "$VARIANT_MANIFEST_GENERATOR" "$VARIANT_BUNDLE_VALIDATOR"; do
	if [[ ! -f "$helper" ]]; then
		print -u2 "Missing multi-version bundle helper: $helper"
		exit 1
	fi
done

clean_directory "$BUILD_ROOT"
clean_directory "$DIST"

for target in "${TARGETS[@]}"; do
	refs="$REFS_ROOT/$target/game-refs"
	output="$BUILD_ROOT/variants/$target"
	mkdir -p "$output"

	"$DOTNET_BIN" clean "$VARIANT_PROJECT" -c Release >/dev/null
	"$DOTNET_BIN" build "$VARIANT_PROJECT" -c Release \
		-p:UniversalDominionSwordSts2Target="$target" \
		-p:GameDataDir="$refs" \
		-o "$output"

	variant_dir="$DIST/lib/$target"
	mkdir -p "$variant_dir"
	cp "$output/$FILE_STEM.dll" "$variant_dir/$FILE_STEM.dll"
done

loader_refs="$REFS_ROOT/0.107.1/game-refs"
loader_output="$BUILD_ROOT/loader"
mkdir -p "$loader_output"
"$DOTNET_BIN" clean "$LOADER_PROJECT" -c Release >/dev/null
"$DOTNET_BIN" build "$LOADER_PROJECT" -c Release \
	-p:GameDataDir="$loader_refs" \
	-o "$loader_output"
cp "$loader_output/$FILE_STEM.Loader.dll" "$DIST/$FILE_STEM.dll"

typeset -a variant_manifest_target_args
variant_manifest_target_args=()
for target in "${TARGETS[@]}"; do
	variant_manifest_target_args+=(--target "$target")
done

python3 "$VARIANT_MANIFEST_GENERATOR" \
	--dist "$DIST" \
	--mod-id "$FILE_STEM" \
	--manifest-name "$VARIANT_MANIFEST_NAME" \
	"${variant_manifest_target_args[@]}" \
	--assembly "$FILE_STEM.dll"

cp "$MANIFEST_SRC" "$DIST/$FILE_STEM.json"
"$GAME_BIN" --headless \
	--path "$ROOT/tools" \
	-s res://pack_mod.gd -- \
	"$MANIFEST_SRC" \
	"$DIST/$FILE_STEM.pck"

find "$DIST" -name ".DS_Store" -type f -delete
find "$DIST" -name "._*" -type f -delete
find "$DIST" -name "*.pdb" -type f -delete

python3 "$VARIANT_BUNDLE_VALIDATOR" \
	--dist "$DIST" \
	--mod-id "$FILE_STEM" \
	--manifest-name "$VARIANT_MANIFEST_NAME" \
	--assembly "$FILE_STEM.dll"

mkdir -p "$WORKSHOP_CONTENT"
rsync -a --delete "$DIST/" "$WORKSHOP_CONTENT/"
print "Prepared local Workshop content in $WORKSHOP_CONTENT"

if [[ "$UDS_DEPLOY" != "0" ]]; then
	mkdir -p "$MOD_DIR"
	rsync -a --delete "$DIST/" "$MOD_DIR/"
	print "Deployed multi-version package to $MOD_DIR"
else
	print "Built multi-version package in $DIST without deploying."
fi
