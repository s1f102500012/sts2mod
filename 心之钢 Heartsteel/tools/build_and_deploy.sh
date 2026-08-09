#!/bin/zsh
set -euo pipefail

SCRIPT_DIR="${0:A:h}"
ROOT="${SCRIPT_DIR:h}"
FILE_STEM="Heartsteel"
MOD_VERSION="0.2.0"
VARIANT_MANIFEST_NAME="heartsteel-variants.manifest"
TARGETS=(0.107.1 0.110.1)

MANIFEST_SRC="$ROOT/assets/$FILE_STEM.json"
VARIANT_PROJECT="$ROOT/src/$FILE_STEM.csproj"
LOADER_PROJECT="$ROOT/loader/$FILE_STEM.Loader.csproj"
REFS_ROOT="$ROOT/../HextechRunes/versioned-dll-backups"
BUILD_ROOT="$ROOT/.build"
IMPORT_PROJECT="$BUILD_ROOT/import_project"
DIST="$ROOT/dist"
RELEASE_DIR="$ROOT/releases/$MOD_VERSION"

GAME_APP="${STS2_GAME_APP:-/Users/iniad/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app}"
GAME_BIN="$GAME_APP/Contents/MacOS/Slay the Spire 2"
GAME_RELEASE_INFO="$GAME_APP/Contents/Resources/release_info.json"
MOD_DIR="$GAME_APP/Contents/MacOS/mods/$FILE_STEM"
RITSULIB_ROOT="${RITSULIB_ROOT:-/Users/iniad/Library/Application Support/Steam/steamapps/workshop/content/2868840/3747602295}"
RITSULIB_REQUIRED_VERSION="0.5.10"
HEARTSTEEL_DEPLOY="${HEARTSTEEL_DEPLOY:-1}"

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

require_file() {
	local path="$1"
	local description="$2"
	if [[ ! -f "$path" ]]; then
		print -u2 "Missing $description: $path"
		exit 1
	fi
}

ritsulib_target_for() {
	case "$1" in
		0.107.1) print "0.107.1" ;;
		0.110.1) print "0.110.0" ;;
		*) print -u2 "Unsupported STS2 target: $1"; return 1 ;;
	esac
}

major_minor_version() {
	sed -E 's/^([0-9]+[.][0-9]+).*/\1/' <<< "$1"
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

require_file "$RITSULIB_ROOT/mod_manifest.json" "RitsuLib Workshop manifest"
if ! grep -Eq '"version"[[:space:]]*:[[:space:]]*"0[.]5[.]10"' "$RITSULIB_ROOT/mod_manifest.json"; then
	print -u2 "RitsuLib $RITSULIB_REQUIRED_VERSION is required from Steam Workshop item 3747602295."
	exit 1
fi

for target in "${TARGETS[@]}"; do
	refs="$REFS_ROOT/$target/game-refs"
	for reference in sts2.dll GodotSharp.dll 0Harmony.dll; do
		require_file "$refs/$reference" "STS2 $target reference"
	done
	ritsu_target="$(ritsulib_target_for "$target")"
	require_file "$RITSULIB_ROOT/lib/$ritsu_target/STS2-RitsuLib.dll" "RitsuLib $RITSULIB_REQUIRED_VERSION implementation for STS2 $target"
done

if [[ ! -x "$GAME_BIN" ]]; then
	print -u2 "Missing Slay the Spire 2 executable: $GAME_BIN"
	exit 1
fi
if [[ ! -x "$GODOT_EDITOR" ]]; then
	print -u2 "Missing Godot editor: $GODOT_EDITOR"
	exit 1
fi

clean_directory "$BUILD_ROOT"
clean_directory "$DIST"
clean_directory "$RELEASE_DIR"
rm -rf "$ROOT/src/bin" "$ROOT/src/obj" "$ROOT/loader/bin" "$ROOT/loader/obj"

for target in "${TARGETS[@]}"; do
	refs="$REFS_ROOT/$target/game-refs"
	ritsu_target="$(ritsulib_target_for "$target")"
	output="$BUILD_ROOT/variants/$target"
	mkdir -p "$output"

	echo "Building $FILE_STEM implementation for STS2 $target with RitsuLib $RITSULIB_REQUIRED_VERSION ($ritsu_target API)"
	"$DOTNET_BIN" clean "$VARIANT_PROJECT" -c Release \
		-p:HeartsteelSts2Target="$target" \
		-p:GameDataPath="$refs" \
		-p:RitsuLibRoot="$RITSULIB_ROOT" \
		-p:RitsuLibCompatibilityTarget="$ritsu_target" >/dev/null
	"$DOTNET_BIN" build "$VARIANT_PROJECT" -c Release --nologo \
		-p:HeartsteelSts2Target="$target" \
		-p:GameDataPath="$refs" \
		-p:RitsuLibRoot="$RITSULIB_ROOT" \
		-p:RitsuLibCompatibilityTarget="$ritsu_target" \
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
"$DOTNET_BIN" build "$LOADER_PROJECT" -c Release --nologo \
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
clean_macos_metadata "$IMPORT_PROJECT"
"$GODOT_EDITOR" --headless --path "$IMPORT_PROJECT" --import

cp "$MANIFEST_SRC" "$DIST/$FILE_STEM.json"
"$GAME_BIN" --headless \
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

release_stage="$BUILD_ROOT/release/$FILE_STEM"
mkdir -p "$release_stage"
rsync -a --delete "$DIST/" "$release_stage/"
clean_macos_metadata "$release_stage"
ditto -c -k --norsrc --keepParent \
	"$release_stage" \
	"$RELEASE_DIR/$FILE_STEM-$MOD_VERSION-multi-version.zip"

if [[ "$HEARTSTEEL_DEPLOY" != "0" ]]; then
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

echo "Created release archive: $RELEASE_DIR/$FILE_STEM-$MOD_VERSION-multi-version.zip"
