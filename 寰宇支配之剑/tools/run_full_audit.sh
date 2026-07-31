#!/bin/zsh
set -euo pipefail

SCRIPT_DIR="${0:A:h}"
ROOT="${SCRIPT_DIR:h}"
OUTPUT="$ROOT/audit/generated"
TEMP_ROOT="$(mktemp -d)"

cleanup() {
	find "$TEMP_ROOT" -mindepth 1 -depth -delete
	rmdir "$TEMP_ROOT"
}
trap cleanup EXIT

mkdir -p "$OUTPUT"

UDS_DEPLOY=0 "$ROOT/tools/build_and_deploy.sh" 2>&1 | tee "$TEMP_ROOT/build-matrix.txt"
"$ROOT/tools/run_tests.sh" 2>&1 | tee "$TEMP_ROOT/adversarial-tests.txt"

python3 "$ROOT/tools/generate_audit_report.py" \
	--root "$ROOT" \
	--dist "$ROOT/dist" \
	--output "$OUTPUT"

sed -e "s|$ROOT|<repo>|g" -e "s|$HOME|<home>|g" \
	"$TEMP_ROOT/adversarial-tests.txt" > "$OUTPUT/adversarial-tests.txt"
sed -e "s|$ROOT|<repo>|g" -e "s|$HOME|<home>|g" \
	"$TEMP_ROOT/build-matrix.txt" > "$OUTPUT/build-matrix.txt"

print "Full audit completed: $OUTPUT"
