#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VARIANT_MANIFEST_NAME="hextech-runes-variants.manifest"
TEST_PROJECT="$ROOT/tests/HextechRunes.Tests/HextechRunes.Tests.csproj"
TEST_DLL="$ROOT/tests/HextechRunes.Tests/bin/Release/net9.0/HextechRunes.Tests.dll"

# 测试工程同时直接引用本体、并通过拓展包间接引用本体。默认并行 MSBuild
# 在这张菱形工程图上偶尔会卡在子节点 IPC 重连；固定单节点即可稳定构建。
BUILD_STABILITY_ARGS=(
  -m:1
  -nodeReuse:false
  -p:UseSharedCompilation=false
  -p:NuGetAudit=false
  -p:RestoreIgnoreFailedSources=true
)
export DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE="${DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE:-true}"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE="${DOTNET_SKIP_FIRST_TIME_EXPERIENCE:-1}"

grep -Fq '<AssemblyName>HextechRunes.Loader</AssemblyName>' "$ROOT/loader/HextechRunes.Loader.csproj"
grep -Fq '[ModInitializer(nameof(Initialize))]' "$ROOT/loader/LoaderBootstrap.cs"
grep -Fq 'LinuxNativeDependencyBootstrap.EnsureHarmonyRuntimeDependenciesVisible();' "$ROOT/loader/LoaderBootstrap.cs"
grep -Fq 'libgcc_s.so.1' "$ROOT/loader/LinuxNativeDependencyBootstrap.cs"
grep -Fq 'RtldGlobal' "$ROOT/loader/LinuxNativeDependencyBootstrap.cs"
grep -Fq "$VARIANT_MANIFEST_NAME" "$ROOT/loader/LoaderBootstrap.cs"
grep -Fq 'AssociateAssemblyWithMod' "$ROOT/loader/LoaderBootstrap.cs"
grep -Fq 'ReflectionHelperModTypesPostfix' "$ROOT/loader/LoaderBootstrap.cs"
grep -Fq 'ModManager.OnModDetected += OnLegacyModDetected' "$ROOT/loader/LoaderBootstrap.cs"
grep -Fq 'LegacyModAssemblyField?.SetValue(mod, _selectedVariantAssembly)' "$ROOT/loader/LoaderBootstrap.cs"
grep -Fq 'CompatTargetMetadataKey = "HextechCompatibilityTarget"' "$ROOT/loader/LoaderBootstrap.cs"
grep -Fq '<AssemblyMetadata Include="HextechCompatibilityTarget"' "$ROOT/src/HextechRunes.csproj"
grep -Fq 'TARGETS=(0.107.1 0.110.0 0.111.0)' "$ROOT/tools/build_and_deploy.sh"

SPONSOR_ROOT="$ROOT/../HextechRunesSponsorPack"
grep -Fq 'LinuxNativeDependencyBootstrap.EnsureHarmonyRuntimeDependenciesVisible();' "$SPONSOR_ROOT/loader/LoaderBootstrap.cs"
grep -Fq 'libgcc_s.so.1' "$SPONSOR_ROOT/loader/LinuxNativeDependencyBootstrap.cs"
grep -Fq 'RtldGlobal' "$SPONSOR_ROOT/loader/LinuxNativeDependencyBootstrap.cs"

# 默认对各发布目标各跑一遍(引用目录存在才跑);显式设 HEXTECH_STS2_TARGET 则只跑该目标。
if [[ -n "${HEXTECH_STS2_TARGET:-}" ]]; then
  TARGETS=("$HEXTECH_STS2_TARGET")
else
  TARGETS=()
  for candidate in 0.107.1 0.110.0 0.111.0; do
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
  echo "== Building tests against STS2 $TARGET =="
  dotnet build \
    "$TEST_PROJECT" \
    --configuration Release \
    --no-incremental \
    "${BUILD_STABILITY_ARGS[@]}" \
    -p:HextechSts2Target="$TARGET" \
    -p:HextechSponsorSts2Target="$TARGET" \
    -p:GameDataDir="$GAME_DATA_DIR"
  echo "== Running tests against STS2 $TARGET =="
  dotnet "$TEST_DLL"
done

dotnet build \
  "$ROOT/loader/HextechRunes.Loader.csproj" \
  --configuration Release \
  "${BUILD_STABILITY_ARGS[@]}" \
  -p:GameDataDir="$ROOT/versioned-dll-backups/0.107.1/game-refs"

if [[ -f "$ROOT/dist/$VARIANT_MANIFEST_NAME" ]]; then
  python3 \
    "$ROOT/tools/multi_version/validate_variant_bundle.py" \
    --dist "$ROOT/dist" \
    --mod-id "HextechRunes" \
    --manifest-name "$VARIANT_MANIFEST_NAME"
fi
