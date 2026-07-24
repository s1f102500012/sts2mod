#!/bin/zsh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
FILE_STEM="HextechRunes"
VARIANT_MANIFEST_NAME="hextech-runes-variants.manifest"
TARGETS=(0.107.1 0.109.0)

MANIFEST_SRC="$ROOT/assets/$FILE_STEM.json"
VARIANT_PROJECT="$ROOT/src/$FILE_STEM.csproj"
LOADER_PROJECT="$ROOT/loader/$FILE_STEM.Loader.csproj"
REFS_ROOT="$ROOT/versioned-dll-backups"
BUILD_ROOT="$ROOT/.build"
DIST="$ROOT/dist"

GAME_APP="/Users/iniad/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app"
GAME_BIN="$GAME_APP/Contents/MacOS/Slay the Spire 2"
GAME_RELEASE_INFO="$GAME_APP/Contents/Resources/release_info.json"
MOD_DIR="$GAME_APP/Contents/MacOS/mods/$FILE_STEM"
IMPORT_PROJECT="$BUILD_ROOT/import_project"
HEXTECH_DEPLOY="${HEXTECH_DEPLOY:-1}"

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

major_minor_version() {
	sed -E 's/^([0-9]+[.][0-9]+).*/\1/' <<< "$1"
}

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

require_references() {
	local target="$1"
	local refs="$REFS_ROOT/$target/game-refs"
	for reference in sts2.dll GodotSharp.dll 0Harmony.dll Steamworks.NET.dll; do
		if [[ ! -f "$refs/$reference" ]]; then
			print -u2 "Missing reference for STS2 $target: $refs/$reference"
			exit 1
		fi
	done
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

GAME_GODOT_VERSION="$("$GAME_BIN" --version 2>/dev/null | head -n 1)"
IMPORT_GODOT_VERSION="$("$GODOT_EDITOR" --version 2>/dev/null | head -n 1)"
if [[ -n "$GAME_GODOT_VERSION" && -n "$IMPORT_GODOT_VERSION" \
	&& "$(major_minor_version "$GAME_GODOT_VERSION")" != "$(major_minor_version "$IMPORT_GODOT_VERSION")" ]]; then
	print -u2 "Warning: asset import Godot version ($IMPORT_GODOT_VERSION) differs from game runtime ($GAME_GODOT_VERSION)."
	print -u2 "Set GODOT_EDITOR to a matching 4.5.x editor if mobile/runtime texture compatibility regresses."
fi

clean_directory "$BUILD_ROOT"
clean_directory "$DIST"
rm -rf "$ROOT/src/bin" "$ROOT/src/obj" "$ROOT/loader/bin" "$ROOT/loader/obj"

python3 "$ROOT/tools/validate_hextech_content.py"

for target in "${TARGETS[@]}"; do
	refs="$REFS_ROOT/$target/game-refs"
	output="$BUILD_ROOT/variants/$target"
	mkdir -p "$output"

	echo "Building $FILE_STEM implementation for STS2 $target using $refs"
	"$DOTNET_BIN" clean "$VARIANT_PROJECT" -c Release \
		-p:HextechSts2Target="$target" \
		-p:GameDataDir="$refs" >/dev/null
	"$DOTNET_BIN" build "$VARIANT_PROJECT" -c Release \
		-p:HextechSts2Target="$target" \
		-p:GameDataDir="$refs" \
		-o "$output"

	variant_dir="$DIST/lib/$target"
	mkdir -p "$variant_dir"
	cp "$output/$FILE_STEM.dll" "$variant_dir/$FILE_STEM.dll"
done

loader_refs="$REFS_ROOT/0.107.1/game-refs"
loader_output="$BUILD_ROOT/loader"
mkdir -p "$loader_output"
echo "Building stable $FILE_STEM loader against STS2 0.107.1 references"
"$DOTNET_BIN" clean "$LOADER_PROJECT" -c Release >/dev/null
"$DOTNET_BIN" build "$LOADER_PROJECT" -c Release \
	-p:GameDataDir="$loader_refs" \
	-o "$loader_output"
cp "$loader_output/$FILE_STEM.Loader.dll" "$DIST/$FILE_STEM.dll"

python3 \
	"$ROOT/tools/multi_version/generate_variant_manifest.py" \
	--dist "$DIST" \
	--mod-id "$FILE_STEM" \
	--manifest-name "$VARIANT_MANIFEST_NAME" \
	--target "0.107.1" \
	--target "0.109.0"

mkdir -p "$IMPORT_PROJECT/$FILE_STEM"
cp "$ROOT/tools/project.godot" "$IMPORT_PROJECT/project.godot"
rsync -a --exclude "$FILE_STEM.json" "$ROOT/assets/" "$IMPORT_PROJECT/$FILE_STEM/"
clean_macos_metadata "$IMPORT_PROJECT"

"$GODOT_EDITOR" --headless \
	--path "$IMPORT_PROJECT" \
	--import

cp "$MANIFEST_SRC" "$DIST/$FILE_STEM.json"
"$GAME_BIN" --headless \
	--path "$ROOT/tools" \
	-s res://pack_mod.gd -- \
	"$MANIFEST_SRC" \
	"$DIST/$FILE_STEM.pck" \
	"$IMPORT_PROJECT"

clean_macos_metadata "$DIST"
python3 \
	"$ROOT/tools/multi_version/validate_variant_bundle.py" \
	--dist "$DIST" \
	--mod-id "$FILE_STEM" \
	--manifest-name "$VARIANT_MANIFEST_NAME"

# 同包发布后，根 DLL 是稳定 loader；官方完整性指纹仍应记录实际执行的版本 DLL。
if [[ "${HEXTECH_UPDATE_LATEST:-1}" != "0" ]]; then
	for target in "${TARGETS[@]}"; do
		python3 "$ROOT/tools/update_latest_version_hashes.py" \
			--latest-json "$ROOT/server/hextech-telemetry/public/latest-version.json" \
			--dist "$DIST" \
			--mod-id "$FILE_STEM" \
			--server-name "海克斯大乱斗" \
			--server-identity "Natsuki.HextechRunes.official" \
			--game-version "$target" \
			--dll-path "$DIST/lib/$target/$FILE_STEM.dll" \
			--output-fingerprint "$BUILD_ROOT/fingerprints/build-fingerprint-$target.json"
	done
else
	echo "Skipped latest-version.json fingerprint update (HEXTECH_UPDATE_LATEST=0)."
fi

if [[ "$HEXTECH_DEPLOY" != "0" ]]; then
	deploy_bundle_atomically
	clean_macos_metadata "$MOD_DIR"
	echo "Deployed multi-version package to $MOD_DIR"
else
	echo "Built multi-version package in $DIST without deploying."
fi

if [[ -f "$GAME_RELEASE_INFO" ]]; then
	CURRENT_GAME_VERSION="$(sed -nE 's/.*"version"[[:space:]]*:[[:space:]]*"v([^"]+)".*/\1/p' "$GAME_RELEASE_INFO" | head -n 1)"
	echo "Installed STS2 version: ${CURRENT_GAME_VERSION:-unknown}; loader will choose the greatest bundled target not newer than the host."
fi
