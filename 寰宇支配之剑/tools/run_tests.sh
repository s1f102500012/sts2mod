#!/bin/zsh
set -euo pipefail

SCRIPT_DIR="${0:A:h}"
ROOT="${SCRIPT_DIR:h}"
REFS_ROOT="${UDS_REFS_ROOT:-$ROOT/../HextechRunes/versioned-dll-backups}"
TEST_PROJECT="$ROOT/tests/UniversalDominionSword.Tests/UniversalDominionSword.Tests.csproj"
TEST_DLL="$ROOT/tests/UniversalDominionSword.Tests/bin/Release/net9.0/UniversalDominionSword.Tests.dll"

if (( ${+commands[dotnet]} )); then
	DOTNET_BIN="${commands[dotnet]}"
elif [[ -x "/opt/homebrew/bin/dotnet" ]]; then
	DOTNET_BIN="/opt/homebrew/bin/dotnet"
else
	print -u2 "Could not find a usable .NET 9 SDK."
	exit 1
fi

for json in \
	"$ROOT/assets/UniversalDominionSword.json" \
	"$ROOT/assets/localization/zhs/relics.json" \
	"$ROOT/assets/localization/zhs/cards.json" \
	"$ROOT/assets/localization/eng/relics.json" \
	"$ROOT/assets/localization/eng/cards.json" \
	"$ROOT/workshop/workshop.json"; do
	python3 -m json.tool "$json" >/dev/null
done

grep -Fq 'TARGETS=(0.107.1 0.110.0 0.111.0)' "$ROOT/tools/build_and_deploy.sh"

# 默认对各发布目标各跑一遍(引用目录存在才跑);显式设 UDS_STS2_TARGET 则只跑该目标。
typeset -a TARGETS
if [[ -n "${UDS_STS2_TARGET:-}" ]]; then
	TARGETS=("$UDS_STS2_TARGET")
else
	TARGETS=()
	for candidate in 0.107.1 0.110.0 0.111.0; do
		if [[ -f "$REFS_ROOT/$candidate/game-refs/sts2.dll" ]]; then
			TARGETS+=("$candidate")
		fi
	done
	if [[ ${#TARGETS[@]} -eq 0 ]]; then
		print -u2 "No STS2 reference directories found under $REFS_ROOT."
		exit 1
	fi
fi

for target in "${TARGETS[@]}"; do
	refs="$REFS_ROOT/$target/game-refs"
	print "== Building tests against STS2 $target =="
	"$DOTNET_BIN" build "$TEST_PROJECT" -c Release --nologo --no-incremental \
		-p:UniversalDominionSwordSts2Target="$target" \
		-p:GameDataDir="$refs"
	print "== Running tests against STS2 $target =="
	"$DOTNET_BIN" "$TEST_DLL"
done

"$DOTNET_BIN" build "$ROOT/loader/UniversalDominionSword.Loader.csproj" -c Release --nologo \
	-p:GameDataDir="$REFS_ROOT/0.107.1/game-refs"

if [[ -f "$ROOT/dist/universal-dominion-sword-variants.manifest" ]]; then
	python3 "$ROOT/tools/multi_version/validate_variant_bundle.py" \
		--dist "$ROOT/dist" \
		--mod-id "UniversalDominionSword" \
		--manifest-name "universal-dominion-sword-variants.manifest" \
		--assembly "UniversalDominionSword.dll"
fi

print "All checks passed."
