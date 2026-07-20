#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VARIANT_MANIFEST_NAME="hextech-runes-variants.manifest"

grep -Fq '<AssemblyName>HextechRunes.Loader</AssemblyName>' "$ROOT/loader/HextechRunes.Loader.csproj"
grep -Fq '[ModInitializer(nameof(Initialize))]' "$ROOT/loader/LoaderBootstrap.cs"
grep -Fq "$VARIANT_MANIFEST_NAME" "$ROOT/loader/LoaderBootstrap.cs"
grep -Fq 'AssociateAssemblyWithMod' "$ROOT/loader/LoaderBootstrap.cs"
grep -Fq 'ReflectionHelperModTypesPostfix' "$ROOT/loader/LoaderBootstrap.cs"
grep -Fq 'ModManager.OnModDetected += OnLegacyModDetected' "$ROOT/loader/LoaderBootstrap.cs"
grep -Fq 'LegacyModAssemblyField?.SetValue(mod, _selectedVariantAssembly)' "$ROOT/loader/LoaderBootstrap.cs"
grep -Fq 'CompatTargetMetadataKey = "HextechCompatibilityTarget"' "$ROOT/loader/LoaderBootstrap.cs"
grep -Fq '<AssemblyMetadata Include="HextechCompatibilityTarget"' "$ROOT/src/HextechRunes.csproj"
grep -Fq 'TARGETS=(0.107.1 0.109.0)' "$ROOT/tools/build_and_deploy.sh"
grep -Fq -- '--dll-path "$DIST/lib/$target/$FILE_STEM.dll"' "$ROOT/tools/build_and_deploy.sh"

# 默认对各发布目标各跑一遍(引用目录存在才跑);显式设 HEXTECH_STS2_TARGET 则只跑该目标。
if [[ -n "${HEXTECH_STS2_TARGET:-}" ]]; then
  TARGETS=("$HEXTECH_STS2_TARGET")
else
  TARGETS=()
  for candidate in 0.107.1 0.108.0 0.109.0; do
    if [[ -f "$ROOT/versioned-dll-backups/$candidate/game-refs/sts2.dll" ]]; then
      TARGETS+=("$candidate")
    fi
  done
  if [[ ${#TARGETS[@]} -eq 0 ]]; then
    TARGETS=(0.107.1)
  fi
fi

for TARGET in "${TARGETS[@]}"; do
  GAME_DATA_DIR="${HEXTECH_GAME_DATA_DIR:-"$ROOT/versioned-dll-backups/$TARGET/game-refs"}"
  echo "== Running tests against STS2 $TARGET =="
  dotnet run \
    --project "$ROOT/tests/HextechRunes.Tests/HextechRunes.Tests.csproj" \
    --configuration Release \
    -p:HextechSts2Target="$TARGET" \
    -p:GameDataDir="$GAME_DATA_DIR"
done

dotnet build \
  "$ROOT/loader/HextechRunes.Loader.csproj" \
  --configuration Release \
  -p:GameDataDir="$ROOT/versioned-dll-backups/0.107.1/game-refs"

if [[ -f "$ROOT/dist/$VARIANT_MANIFEST_NAME" ]]; then
  python3 \
    "$ROOT/tools/multi_version/validate_variant_bundle.py" \
    --dist "$ROOT/dist" \
    --mod-id "HextechRunes" \
    --manifest-name "$VARIANT_MANIFEST_NAME"
fi
