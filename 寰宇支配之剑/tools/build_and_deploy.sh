#!/bin/zsh
set -euo pipefail

SCRIPT_DIR="${0:A:h}"
ROOT="${SCRIPT_DIR:h}"
FILE_STEM="UniversalDominionSword"
VARIANT_MANIFEST_NAME="universal-dominion-sword-variants.manifest"
TARGETS=(0.107.1 0.110.0 0.111.0)

MANIFEST_SRC="$ROOT/assets/$FILE_STEM.json"
VARIANT_PROJECT="$ROOT/src/$FILE_STEM.csproj"
LOADER_PROJECT="$ROOT/loader/$FILE_STEM.Loader.csproj"
REFS_ROOT="${UDS_REFS_ROOT:-$ROOT/../HextechRunes/versioned-dll-backups}"
BUILD_ROOT="$ROOT/.build"
DIST="$ROOT/dist"
IMPORT_PROJECT="$BUILD_ROOT/import_project"
WORKSHOP_CONTENT="$ROOT/workshop/content"
UDS_DEPLOY="${UDS_DEPLOY:-1}"

GAME_APP="${STS2_GAME_APP:-$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app}"
GAME_BIN="$GAME_APP/Contents/MacOS/Slay the Spire 2"
MOD_DIR="$GAME_APP/Contents/MacOS/mods/$FILE_STEM"

# 资源导入用工作区的 Godot 4.5.x 编辑器;PNG 经 --import 生成 .import/.ctex 后,
# 原版 ResourceLoader.Load / PreloadManager.Cache 才能按 PackedIconPath / PortraitPath 直接取图。
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

for target in "${TARGETS[@]}"; do
	refs="$REFS_ROOT/$target/game-refs"
	for reference in sts2.dll GodotSharp.dll 0Harmony.dll; do
		if [[ ! -f "$refs/$reference" ]]; then
			print -u2 "Missing reference for STS2 $target: $refs/$reference"
			exit 1
		fi
	done
done
if [[ ! -x "$GODOT_EDITOR" ]]; then
	print -u2 "Missing Godot editor for asset import: $GODOT_EDITOR (set GODOT_EDITOR)."
	exit 1
fi
if [[ ! -x "$GAME_BIN" ]]; then
	print -u2 "Missing game binary for PCK packing: $GAME_BIN (set STS2_GAME_APP)."
	exit 1
fi

clean_directory "$BUILD_ROOT"
clean_directory "$DIST"
rm -rf "$ROOT/src/bin" "$ROOT/src/obj" "$ROOT/loader/bin" "$ROOT/loader/obj"

for target in "${TARGETS[@]}"; do
	refs="$REFS_ROOT/$target/game-refs"
	output="$BUILD_ROOT/variants/$target"
	mkdir -p "$output"

	print "Building $FILE_STEM implementation for STS2 $target using $refs"
	"$DOTNET_BIN" build "$VARIANT_PROJECT" -c Release --nologo \
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
print "Building stable $FILE_STEM loader against STS2 0.107.1 references"
"$DOTNET_BIN" build "$LOADER_PROJECT" -c Release --nologo \
	-p:GameDataDir="$loader_refs" \
	-o "$loader_output"
cp "$loader_output/$FILE_STEM.Loader.dll" "$DIST/$FILE_STEM.dll"

typeset -a variant_manifest_target_args
variant_manifest_target_args=()
for target in "${TARGETS[@]}"; do
	variant_manifest_target_args+=(--target "$target")
done
python3 "$ROOT/tools/multi_version/generate_variant_manifest.py" \
	--dist "$DIST" \
	--mod-id "$FILE_STEM" \
	--manifest-name "$VARIANT_MANIFEST_NAME" \
	"${variant_manifest_target_args[@]}" \
	--assembly "$FILE_STEM.dll"

mkdir -p "$IMPORT_PROJECT/$FILE_STEM"
cp "$ROOT/tools/project.godot" "$IMPORT_PROJECT/project.godot"
rsync -a --exclude "$FILE_STEM.json" --exclude "third_party" "$ROOT/assets/" "$IMPORT_PROJECT/$FILE_STEM/"
clean_macos_metadata "$IMPORT_PROJECT"

print "Importing assets with $GODOT_EDITOR"
"$GODOT_EDITOR" --headless --path "$IMPORT_PROJECT" --import

cp "$MANIFEST_SRC" "$DIST/$FILE_STEM.json"
"$GAME_BIN" --headless \
	--path "$ROOT/tools" \
	-s res://pack_mod.gd -- \
	"$MANIFEST_SRC" \
	"$DIST/$FILE_STEM.pck" \
	"$IMPORT_PROJECT"

clean_macos_metadata "$DIST"
find "$DIST" -name "*.pdb" -type f -delete

python3 "$ROOT/tools/multi_version/validate_variant_bundle.py" \
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
