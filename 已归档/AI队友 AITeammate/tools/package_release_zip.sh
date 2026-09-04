#!/bin/zsh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
MOD_ID="sts2AITeammate"
DIST_DIR="$ROOT/dist"
PACKAGE_ROOT="$ROOT/.build/package"
PACKAGE_DIR="$PACKAGE_ROOT/$MOD_ID"
ZIP_PATH="${1:-$DIST_DIR/$MOD_ID.zip}"

clean_macos_metadata() {
  local target="$1"
  [[ -d "$target" ]] || return 0

  find "$target" -name "__MACOSX" -type d -prune -exec rm -rf {} +
  find "$target" -name ".DS_Store" -type f -delete
  find "$target" -name "._*" -type f -delete
}

for required in "$DIST_DIR/$MOD_ID.dll" "$DIST_DIR/$MOD_ID.json" "$DIST_DIR/$MOD_ID.pck"; do
  if [[ ! -f "$required" ]]; then
    print -u2 "Missing release artifact: $required"
    print -u2 "Run tools/build_and_deploy.sh before packaging."
    exit 1
  fi
done

rm -rf "$PACKAGE_ROOT"
mkdir -p "$PACKAGE_DIR"
cp "$DIST_DIR/$MOD_ID.dll" "$PACKAGE_DIR/$MOD_ID.dll"
cp "$DIST_DIR/$MOD_ID.json" "$PACKAGE_DIR/$MOD_ID.json"
cp "$DIST_DIR/$MOD_ID.pck" "$PACKAGE_DIR/$MOD_ID.pck"
clean_macos_metadata "$PACKAGE_ROOT"

mkdir -p "$(dirname "$ZIP_PATH")"
rm -f "$ZIP_PATH"
(
  cd "$PACKAGE_ROOT"
  COPYFILE_DISABLE=1 zip -r -X "$ZIP_PATH" "$MOD_ID" \
    -x "__MACOSX/*" "*/__MACOSX/*" ".DS_Store" "*/.DS_Store" "._*" "*/._*"
)

if unzip -l "$ZIP_PATH" | rg -q '(__MACOSX|/[.]_|[.]DS_Store)'; then
  print -u2 "Package still contains macOS metadata: $ZIP_PATH"
  exit 1
fi

expected_entries="$(mktemp)"
actual_entries="$(mktemp)"
trap 'rm -f "$expected_entries" "$actual_entries"' EXIT
printf "%s\n" \
  "$MOD_ID/$MOD_ID.dll" \
  "$MOD_ID/$MOD_ID.json" \
  "$MOD_ID/$MOD_ID.pck" \
  | LC_ALL=C sort > "$expected_entries"
unzip -Z1 "$ZIP_PATH" | rg -v '/$' | LC_ALL=C sort > "$actual_entries"

if ! diff -u "$expected_entries" "$actual_entries" >&2; then
  print -u2 "Package contains unexpected files: $ZIP_PATH"
  exit 1
fi

echo "Packaged clean release zip: $ZIP_PATH"
