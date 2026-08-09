#!/bin/zsh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
FILE_STEM="IntegratedStrategyEvents"
VARIANT_MANIFEST_NAME="integrated-strategy-events-variants.manifest"
TARGETS=(0.107.1 0.110.1)

MANIFEST_SRC="$ROOT/assets/$FILE_STEM.json"
VARIANT_PROJECT="$ROOT/src/$FILE_STEM.csproj"
LOADER_PROJECT="$ROOT/loader/$FILE_STEM.Loader.csproj"
REFS_ROOT="$ROOT/../HextechRunes/versioned-dll-backups"
BUILD_ROOT="$ROOT/.build"
IMPORT_PROJECT="$BUILD_ROOT/import_project"
DIST="$ROOT/dist"
ROOT_COMPAT_SRC="$ROOT/assets_root_compat"

GAME_APP="${STS2_GAME_APP:-/Users/iniad/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app}"
GAME_BIN="$GAME_APP/Contents/MacOS/Slay the Spire 2"
GAME_RELEASE_INFO="$GAME_APP/Contents/Resources/release_info.json"
MOD_DIR="$GAME_APP/Contents/MacOS/mods/$FILE_STEM"
RITSULIB_WORKSHOP_ROOT="/Users/iniad/Library/Application Support/Steam/steamapps/workshop/content/2868840/3747602295"
RITSULIB_ROOT="${RITSULIB_ROOT:-$RITSULIB_WORKSHOP_ROOT}"
RITSULIB_REQUIRED_VERSION="0.5.10"
INTEGRATED_STRATEGY_DEPLOY="${INTEGRATED_STRATEGY_DEPLOY:-1}"

DEFAULT_GODOT_EDITOR="$ROOT/../.tools/godot-4.5.1/Godot_mono.app/Contents/MacOS/Godot"
if [[ -z "${GODOT_EDITOR:-}" && -x "$DEFAULT_GODOT_EDITOR" ]]; then
	GODOT_EDITOR="$DEFAULT_GODOT_EDITOR"
else
	GODOT_EDITOR="${GODOT_EDITOR:-/opt/homebrew/bin/godot}"
fi

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

clean_macos_metadata() {
	local target="$1"
	[[ -d "$target" ]] || return 0
	find "$target" -name "__MACOSX" -type d -prune -exec rm -rf {} +
	find "$target" -name ".DS_Store" -type f -delete
	find "$target" -name "._*" -type f -delete
}

manifest_has_version() {
	local manifest="$1"
	local version="$2"
	[[ -f "$manifest" ]] || return 1
	grep -q "\"version\"[[:space:]]*:[[:space:]]*\"$version\"" "$manifest"
}

require_references() {
	local target="$1"
	local refs="$REFS_ROOT/$target/game-refs"
	local ritsulib_target
	ritsulib_target="$(ritsulib_target_for_game_target "$target")"
	for reference in sts2.dll GodotSharp.dll 0Harmony.dll; do
		if [[ ! -f "$refs/$reference" ]]; then
			print -u2 "Missing reference for STS2 $target: $refs/$reference"
			exit 1
		fi
	done
	if [[ ! -f "$RITSULIB_ROOT/lib/$ritsulib_target/STS2-RitsuLib.dll" ]]; then
		print -u2 "Missing RitsuLib $RITSULIB_REQUIRED_VERSION variant $ritsulib_target for STS2 $target."
		exit 1
	fi
}

ritsulib_target_for_game_target() {
	local target="$1"
	if [[ "$target" == "0.110.1" ]]; then
		echo "0.110.0"
	else
		echo "$target"
	fi
}

validate_ritsulib() {
	if ! manifest_has_version "$RITSULIB_ROOT/mod_manifest.json" "$RITSULIB_REQUIRED_VERSION"; then
		print -u2 "RitsuLib $RITSULIB_REQUIRED_VERSION is required from Steam Workshop item 3747602295 at $RITSULIB_ROOT."
		exit 1
	fi
}

deploy_bundle_atomically() {
	local stage="$MOD_DIR.tmp.$$"
	local previous="$MOD_DIR.previous.$$"
	rm -rf "$stage" "$previous"
	mkdir -p "$stage"
	rsync -a --delete "$DIST/" "$stage/"
	if [[ -d "$MOD_DIR" ]]; then
		mv "$MOD_DIR" "$previous"
	fi
	mv "$stage" "$MOD_DIR"
	rm -rf "$previous"
}

major_minor_version() {
	sed -E 's/^([0-9]+[.][0-9]+).*/\1/' <<< "$1"
}

validate_ritsulib
for target in "${TARGETS[@]}"; do
	require_references "$target"
done

if [[ ! -x "$GAME_BIN" ]]; then
	print -u2 "Missing Slay the Spire 2 executable: $GAME_BIN"
	exit 1
fi
if [[ ! -x "$GODOT_EDITOR" ]]; then
	print -u2 "Missing Godot editor: $GODOT_EDITOR"
	exit 1
fi

"$ROOT/tools/validate_event_structure.sh"

clean_directory "$BUILD_ROOT"
clean_directory "$DIST"
rm -rf "$ROOT/src/bin" "$ROOT/src/obj" "$ROOT/loader/bin" "$ROOT/loader/obj"

for target in "${TARGETS[@]}"; do
	refs="$REFS_ROOT/$target/game-refs"
	ritsulib_target="$(ritsulib_target_for_game_target "$target")"
	output="$BUILD_ROOT/variants/$target"
	mkdir -p "$output"

	echo "Building $FILE_STEM implementation for STS2 $target with RitsuLib $RITSULIB_REQUIRED_VERSION"
	"$DOTNET_BIN" clean "$VARIANT_PROJECT" -c Release \
		-p:IntegratedStrategySts2Target="$target" \
		-p:GameDataPath="$refs" \
		-p:RitsuLibTarget="$ritsulib_target" \
		-p:RitsuLibRoot="$RITSULIB_ROOT" >/dev/null
	"$DOTNET_BIN" build "$VARIANT_PROJECT" -c Release \
		-p:IntegratedStrategySts2Target="$target" \
		-p:GameDataPath="$refs" \
		-p:RitsuLibTarget="$ritsulib_target" \
		-p:RitsuLibRoot="$RITSULIB_ROOT" \
		-o "$output"

	variant_dir="$DIST/lib/$target"
	mkdir -p "$variant_dir"
	cp "$output/$FILE_STEM.dll" "$variant_dir/$FILE_STEM.dll"
done

loader_refs="$REFS_ROOT/0.107.1/game-refs"
loader_output="$BUILD_ROOT/loader"
mkdir -p "$loader_output"
echo "Building stable loader against STS2 0.107.1 references"
"$DOTNET_BIN" clean "$LOADER_PROJECT" -c Release -p:GameDataDir="$loader_refs" >/dev/null
"$DOTNET_BIN" build "$LOADER_PROJECT" -c Release \
	-p:GameDataDir="$loader_refs" \
	-o "$loader_output"
cp "$loader_output/$FILE_STEM.Loader.dll" "$DIST/$FILE_STEM.dll"

python3 "$ROOT/tools/multi_version/generate_variant_manifest.py" \
	--dist "$DIST" \
	--mod-id "$FILE_STEM" \
	--manifest-name "$VARIANT_MANIFEST_NAME" \
	--target "0.107.1" \
	--target "0.110.1"

GAME_GODOT_VERSION="$("$GAME_BIN" --version 2>/dev/null | head -n 1)"
IMPORT_GODOT_VERSION="$("$GODOT_EDITOR" --version 2>/dev/null | head -n 1)"
if [[ -n "$GAME_GODOT_VERSION" && -n "$IMPORT_GODOT_VERSION" \
	&& "$(major_minor_version "$GAME_GODOT_VERSION")" != "$(major_minor_version "$IMPORT_GODOT_VERSION")" ]]; then
	print -u2 "Warning: import Godot $IMPORT_GODOT_VERSION differs from runtime $GAME_GODOT_VERSION."
fi

mkdir -p "$IMPORT_PROJECT/$FILE_STEM"
cp "$ROOT/tools/project.godot" "$IMPORT_PROJECT/project.godot"
rsync -a --exclude "$FILE_STEM.json" "$ROOT/assets/" "$IMPORT_PROJECT/$FILE_STEM/"
if [[ -d "$ROOT_COMPAT_SRC" ]]; then
	rsync -a "$ROOT_COMPAT_SRC/" "$IMPORT_PROJECT/_root_compat/"
fi
clean_macos_metadata "$IMPORT_PROJECT"
"$GODOT_EDITOR" --headless --path "$IMPORT_PROJECT" --import

cp "$MANIFEST_SRC" "$DIST/$FILE_STEM.json"
"$GAME_BIN" --headless \
	--log-file "$BUILD_ROOT/pack-godot.log" \
	--path "$ROOT/tools" \
	-s res://pack_mod.gd -- \
	"$MANIFEST_SRC" \
	"$DIST/$FILE_STEM.pck" \
	"$IMPORT_PROJECT"

clean_macos_metadata "$DIST"
python3 "$ROOT/tools/multi_version/validate_variant_bundle.py" \
	--dist "$DIST" \
	--mod-id "$FILE_STEM" \
	--manifest-name "$VARIANT_MANIFEST_NAME"
"$ROOT/tools/run_map_compatibility_probe.sh"

if [[ "$INTEGRATED_STRATEGY_DEPLOY" != "0" ]]; then
	deploy_bundle_atomically
	clean_macos_metadata "$MOD_DIR"
	echo "Deployed multi-version package to $MOD_DIR"
else
	echo "Built multi-version package in $DIST without deploying."
fi

if [[ -f "$GAME_RELEASE_INFO" ]]; then
	CURRENT_GAME_VERSION="$(sed -nE 's/.*"version"[[:space:]]*:[[:space:]]*"v([^\"]+)".*/\1/p' "$GAME_RELEASE_INFO" | head -n 1)"
	echo "Installed STS2 version: ${CURRENT_GAME_VERSION:-unknown}; loader selects the newest bundled target not newer than the host."
fi
