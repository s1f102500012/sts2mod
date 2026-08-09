#!/bin/zsh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
FILE_STEM="IntegratedStrategyEvents"
VARIANT_MANIFEST_NAME="integrated-strategy-events-variants.manifest"
DIST_DIR="$ROOT/dist"
PACKAGE_ROOT="$ROOT/.build/package"
PACKAGE_DIR="$PACKAGE_ROOT/$FILE_STEM"
ZIP_PATH="${1:-$DIST_DIR/$FILE_STEM.zip}"
EXPECTED_ENTRIES_FILE=""
ACTUAL_ENTRIES_FILE=""

if [[ "$ZIP_PATH" != /* ]]; then
	ZIP_PATH="$(pwd)/$ZIP_PATH"
fi

cleanup_temp_files() {
	[[ -z "$EXPECTED_ENTRIES_FILE" || ! -f "$EXPECTED_ENTRIES_FILE" ]] || rm -f "$EXPECTED_ENTRIES_FILE"
	[[ -z "$ACTUAL_ENTRIES_FILE" || ! -f "$ACTUAL_ENTRIES_FILE" ]] || rm -f "$ACTUAL_ENTRIES_FILE"
}
trap cleanup_temp_files EXIT

for required in \
	"$DIST_DIR/$FILE_STEM.json" \
	"$DIST_DIR/$FILE_STEM.pck" \
	"$DIST_DIR/$FILE_STEM.dll" \
	"$DIST_DIR/$VARIANT_MANIFEST_NAME" \
	"$DIST_DIR/lib/0.107.1/$FILE_STEM.dll" \
	"$DIST_DIR/lib/0.107.1/compat-target.txt" \
	"$DIST_DIR/lib/0.110.1/$FILE_STEM.dll" \
	"$DIST_DIR/lib/0.110.1/compat-target.txt"; do
	if [[ ! -f "$required" ]]; then
		print -u2 "Missing release artifact: $required"
		exit 1
	fi
done

rm -rf "$PACKAGE_ROOT"
mkdir -p "$PACKAGE_DIR"
rsync -a --exclude '*.zip' "$DIST_DIR/" "$PACKAGE_DIR/"
find "$PACKAGE_ROOT" -name "__MACOSX" -type d -prune -exec rm -rf {} +
find "$PACKAGE_ROOT" -name ".DS_Store" -type f -delete
find "$PACKAGE_ROOT" -name "._*" -type f -delete

mkdir -p "$(dirname "$ZIP_PATH")"
rm -f "$ZIP_PATH"
(
	cd "$PACKAGE_ROOT"
	COPYFILE_DISABLE=1 zip -r -X "$ZIP_PATH" "$FILE_STEM" \
		-x "__MACOSX/*" "*/__MACOSX/*" ".DS_Store" "*/.DS_Store" "._*" "*/._*"
)

if unzip -l "$ZIP_PATH" | rg -q '(__MACOSX|/[.]_|[.]DS_Store)'; then
	print -u2 "Package contains macOS metadata: $ZIP_PATH"
	exit 1
fi

EXPECTED_ENTRIES_FILE="$(mktemp)"
ACTUAL_ENTRIES_FILE="$(mktemp)"
printf "%s\n" \
	"$FILE_STEM/$FILE_STEM.dll" \
	"$FILE_STEM/$FILE_STEM.json" \
	"$FILE_STEM/$FILE_STEM.pck" \
	"$FILE_STEM/$VARIANT_MANIFEST_NAME" \
	"$FILE_STEM/lib/0.107.1/$FILE_STEM.dll" \
	"$FILE_STEM/lib/0.107.1/compat-target.txt" \
	"$FILE_STEM/lib/0.110.1/$FILE_STEM.dll" \
	"$FILE_STEM/lib/0.110.1/compat-target.txt" \
	| LC_ALL=C sort > "$EXPECTED_ENTRIES_FILE"
unzip -Z1 "$ZIP_PATH" | rg -v '/$' | LC_ALL=C sort > "$ACTUAL_ENTRIES_FILE"

if ! diff -u "$EXPECTED_ENTRIES_FILE" "$ACTUAL_ENTRIES_FILE" >&2; then
	print -u2 "Package contains unexpected files: $ZIP_PATH"
	exit 1
fi

echo "Packaged multi-version release zip: $ZIP_PATH"
