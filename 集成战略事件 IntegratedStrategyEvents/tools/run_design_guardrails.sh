#!/bin/zsh
set -euo pipefail
ROOT="${0:A:h:h}"
for target in 0.107.1 0.110.1 0.111.0; do
  dotnet run --project "$ROOT/tests/DesignGuardrails/DesignGuardrails.csproj" \
    -p:IntegratedStrategySts2Target="$target" \
    -- "$ROOT/tests/snapshots/$target" "$@"
done
