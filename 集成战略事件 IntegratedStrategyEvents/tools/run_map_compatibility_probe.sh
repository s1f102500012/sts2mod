#!/bin/zsh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT="$ROOT/tests/MapCompatibilityProbe/MapCompatibilityProbe.csproj"
REFS_ROOT="$ROOT/../HextechRunes/versioned-dll-backups"
RITSULIB_ROOT="${RITSULIB_ROOT:-/Users/iniad/Library/Application Support/Steam/steamapps/workshop/content/2868840/3747602295}"
GAME_APP="${STS2_GAME_APP:-/Users/iniad/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app}"
GAME_RUNTIME_DATA="${STS2_GAME_RUNTIME_DATA:-$GAME_APP/Contents/Resources/data_sts2_macos_$(uname -m)}"
TARGETS=(0.107.1 0.110.1)

if (( ${+commands[dotnet]} )); then
	DOTNET_BIN="${commands[dotnet]}"
elif [[ -x "/opt/homebrew/bin/dotnet" ]]; then
	DOTNET_BIN="/opt/homebrew/bin/dotnet"
else
	print -u2 "Could not find a usable .NET 9 SDK."
	exit 1
fi

for target in "${TARGETS[@]}"; do
	ritsulib_target="$target"
	if [[ "$target" == "0.110.1" ]]; then
		ritsulib_target="0.110.0"
	fi
	output="$ROOT/.build/map-compatibility-probe/$target"
	"$DOTNET_BIN" run \
		--project "$PROJECT" \
		-c Release \
		-p:IntegratedStrategySts2Target="$target" \
		-p:GameDataPath="$REFS_ROOT/$target/game-refs" \
		-p:GameRuntimeDataPath="$GAME_RUNTIME_DATA" \
		-p:RitsuLibTarget="$ritsulib_target" \
		-p:RitsuLibRoot="$RITSULIB_ROOT" \
		-p:BaseOutputPath="$output/bin/" \
		-p:BaseIntermediateOutputPath="$output/obj/"
done
