#!/bin/zsh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT="$ROOT/tests/MapCompatibilityProbe/MapCompatibilityProbe.csproj"
REFS_ROOT="$ROOT/../HextechRunes/versioned-dll-backups"
RITSULIB_ROOT="${RITSULIB_ROOT:-/Users/iniad/Library/Application Support/Steam/steamapps/workshop/content/2868840/3747602295}"
TARGETS=(0.107.1 0.109.0)

if (( ${+commands[dotnet]} )); then
	DOTNET_BIN="${commands[dotnet]}"
elif [[ -x "/opt/homebrew/bin/dotnet" ]]; then
	DOTNET_BIN="/opt/homebrew/bin/dotnet"
else
	print -u2 "Could not find a usable .NET 9 SDK."
	exit 1
fi

for target in "${TARGETS[@]}"; do
	output="$ROOT/.build/map-compatibility-probe/$target"
	"$DOTNET_BIN" run \
		--project "$PROJECT" \
		-c Release \
		-p:IntegratedStrategySts2Target="$target" \
		-p:GameDataPath="$REFS_ROOT/$target/game-refs" \
		-p:RitsuLibRoot="$RITSULIB_ROOT" \
		-p:BaseOutputPath="$output/bin/" \
		-p:BaseIntermediateOutputPath="$output/obj/"
done
