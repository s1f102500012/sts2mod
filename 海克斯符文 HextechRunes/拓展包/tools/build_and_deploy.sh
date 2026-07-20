#!/bin/zsh
set -euo pipefail

DOTNET="${DOTNET:-/opt/homebrew/bin/dotnet}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
FILE_STEM="HextechRunesSponsorPack"
MANIFEST_SRC="$ROOT/assets/$FILE_STEM.json"
GAME_APP="/Users/iniad/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app"
GAME_BIN="$GAME_APP/Contents/MacOS/Slay the Spire 2"
MOD_DIR="$GAME_APP/Contents/MacOS/mods/$FILE_STEM"
BUILD_OUT="$ROOT/src/bin/Release/net9.0"
PROJECT_PATH="$ROOT/src/$FILE_STEM.csproj"
PACK_TOOL_PROJECT="$ROOT/../HextechRunes/tools"
IMPORT_PROJECT="$ROOT/.build/import_project"
DEFAULT_GODOT_EDITOR="$ROOT/../.tools/godot-4.5.1/Godot_mono.app/Contents/MacOS/Godot"
if [[ -z "${GODOT_EDITOR:-}" && -x "$DEFAULT_GODOT_EDITOR" ]]; then
  GODOT_EDITOR="$DEFAULT_GODOT_EDITOR"
else
  GODOT_EDITOR="${GODOT_EDITOR:-/opt/homebrew/bin/godot}"
fi
REFS_ROOT="${HEXTECH_SPONSOR_REFS_ROOT:-$ROOT/../HextechRunes/versioned-dll-backups}"
REFS_1090="$REFS_ROOT/0.109.0/game-refs"
REFS_1080="$REFS_ROOT/0.108.0/game-refs"
REFS_1071="$REFS_ROOT/0.107.1/game-refs"
REFS_1070="$REFS_ROOT/0.107.0/game-refs"
REFS_106="$REFS_ROOT/0.106.1/game-refs"
REFS_105="$REFS_ROOT/0.105.1/game-refs"
REFS_104="$REFS_ROOT/0.104.0/game-refs"
GAME_RELEASE_INFO="$GAME_APP/Contents/Resources/release_info.json"
DEFAULT_STS2_TARGET="0.107.1"
HEXTECH_SPONSOR_DEPLOY="${HEXTECH_SPONSOR_DEPLOY:-1}"

major_minor_version() {
  sed -E 's/^([0-9]+[.][0-9]+).*/\1/' <<< "$1"
}

clean_macos_metadata() {
  local target="$1"
  [[ -d "$target" ]] || return 0

  find "$target" -name "__MACOSX" -type d -prune -exec rm -rf {} +
  find "$target" -name ".DS_Store" -type f -delete
  find "$target" -name "._*" -type f -delete
}

GAME_GODOT_VERSION="$("$GAME_BIN" --version 2>/dev/null | head -n 1)"
IMPORT_GODOT_VERSION="$("$GODOT_EDITOR" --version 2>/dev/null | head -n 1)"
if [[ -n "$GAME_GODOT_VERSION" && -n "$IMPORT_GODOT_VERSION" \
  && "$(major_minor_version "$GAME_GODOT_VERSION")" != "$(major_minor_version "$IMPORT_GODOT_VERSION")" ]]; then
  print -u2 "Warning: asset import Godot version ($IMPORT_GODOT_VERSION) differs from game runtime ($GAME_GODOT_VERSION)."
  print -u2 "Set GODOT_EDITOR to a matching 4.5.x editor if texture compatibility regresses."
fi

rm -rf "$ROOT/src/bin" "$ROOT/src/obj" "$ROOT/dist" "$ROOT/.build"

CURRENT_GAME_VERSION=""
if [[ -f "$GAME_RELEASE_INFO" ]]; then
  CURRENT_GAME_VERSION="$(sed -nE 's/.*"version"[[:space:]]*:[[:space:]]*"v([^"]+)".*/\1/p' "$GAME_RELEASE_INFO" | head -n 1)"
fi

# 级联回退:未显式给拓展包目标时,沿用主模组的 HEXTECH_STS2_TARGET(双分支发布流程里两个脚本
# 常连跑,只设主模组变量时拓展包静默编成默认 0.107.1 曾是漏错版本的通道)。
HEXTECH_SPONSOR_STS2_TARGET="${HEXTECH_SPONSOR_STS2_TARGET:-${HEXTECH_STS2_TARGET:-$DEFAULT_STS2_TARGET}}"
case "$HEXTECH_SPONSOR_STS2_TARGET" in
  0.109*)
    HEXTECH_SPONSOR_STS2_TARGET="0.109.0"
    TARGET_REFS="$REFS_1090"
    ;;
  0.108*)
    HEXTECH_SPONSOR_STS2_TARGET="0.108.0"
    TARGET_REFS="$REFS_1080"
    ;;
  0.107.1*)
    HEXTECH_SPONSOR_STS2_TARGET="0.107.1"
    TARGET_REFS="$REFS_1071"
    ;;
  0.107.0*)
    HEXTECH_SPONSOR_STS2_TARGET="0.107.0"
    TARGET_REFS="$REFS_1070"
    ;;
  0.107)
    HEXTECH_SPONSOR_STS2_TARGET="0.107.1"
    TARGET_REFS="$REFS_1071"
    ;;
  0.106*)
    HEXTECH_SPONSOR_STS2_TARGET="0.106.1"
    TARGET_REFS="$REFS_106"
    ;;
  0.105*)
    HEXTECH_SPONSOR_STS2_TARGET="0.105.1"
    TARGET_REFS="$REFS_105"
    ;;
  0.104*)
    HEXTECH_SPONSOR_STS2_TARGET="0.104.0"
    TARGET_REFS="$REFS_104"
    ;;
  *)
    print -u2 "Unsupported or unknown STS2 version '$HEXTECH_SPONSOR_STS2_TARGET'; using live game references without compatibility defines."
    TARGET_REFS="$GAME_APP/Contents/Resources/data_sts2_macos_arm64"
    ;;
esac

if [[ "$HEXTECH_SPONSOR_DEPLOY" != "0" ]]; then
  case "$HEXTECH_SPONSOR_STS2_TARGET:$CURRENT_GAME_VERSION" in
    0.104.0:0.104*|0.105.1:0.105*|0.106.1:0.106*|0.107.0:0.107.0*|0.107.1:0.107.1*|0.108.0:0.108*|0.109.0:0.109*|*:)
      ;;
    *)
      if [[ "${HEXTECH_SPONSOR_ALLOW_VERSION_MISMATCH:-0}" != "1" ]]; then
        print -u2 "Refusing to deploy $FILE_STEM built for STS2 $HEXTECH_SPONSOR_STS2_TARGET into installed STS2 $CURRENT_GAME_VERSION."
        print -u2 "Set HEXTECH_SPONSOR_DEPLOY=0 to package only, or HEXTECH_SPONSOR_ALLOW_VERSION_MISMATCH=1 if intentional."
        exit 1
      fi
      print -u2 "Warning: deploying STS2 $HEXTECH_SPONSOR_STS2_TARGET build into installed STS2 $CURRENT_GAME_VERSION because HEXTECH_SPONSOR_ALLOW_VERSION_MISMATCH=1."
      ;;
  esac
fi

for ref_dll in sts2.dll GodotSharp.dll 0Harmony.dll; do
  if [[ ! -f "$TARGET_REFS/$ref_dll" ]]; then
    print -u2 "Missing required reference for STS2 $HEXTECH_SPONSOR_STS2_TARGET: $TARGET_REFS/$ref_dll"
    exit 1
  fi
done

echo "Building $FILE_STEM for STS2 $HEXTECH_SPONSOR_STS2_TARGET using $TARGET_REFS"
"$DOTNET" build "$PROJECT_PATH" -c Release \
  -p:HextechSponsorSts2Target="$HEXTECH_SPONSOR_STS2_TARGET" \
  -p:HextechSts2Target="$HEXTECH_SPONSOR_STS2_TARGET" \
  -p:GameDataDir="$TARGET_REFS"

mkdir -p "$ROOT/dist"
mkdir -p "$IMPORT_PROJECT/$FILE_STEM"
cp "$PACK_TOOL_PROJECT/project.godot" "$IMPORT_PROJECT/project.godot"
rsync -a --exclude "$FILE_STEM.json" "$ROOT/assets/" "$IMPORT_PROJECT/$FILE_STEM/"
clean_macos_metadata "$IMPORT_PROJECT"

"$GODOT_EDITOR" --headless \
  --path "$IMPORT_PROJECT" \
  --import

cp "$MANIFEST_SRC" "$ROOT/dist/$FILE_STEM.json"

"$GAME_BIN" --headless \
  --path "$PACK_TOOL_PROJECT" \
  -s res://pack_mod.gd -- \
  "$MANIFEST_SRC" \
  "$ROOT/dist/$FILE_STEM.pck" \
  "$IMPORT_PROJECT"

MAIN_DLL="$BUILD_OUT/$FILE_STEM.dll"
if [[ ! -f "$MAIN_DLL" ]]; then
  print -u2 "Expected main DLL missing: $MAIN_DLL"
  exit 1
fi

EXTRA_DLLS=()
for dll in "$BUILD_OUT"/*.dll(N); do
  if [[ "$(basename "$dll")" != "$FILE_STEM.dll" ]]; then
    EXTRA_DLLS+=("$dll")
  fi
done

if (( ${#EXTRA_DLLS[@]} )); then
  print -u2 "Build output contains unexpected dependency DLLs:"
  printf '  %s\n' "${EXTRA_DLLS[@]}" >&2
  exit 1
fi

cp "$MAIN_DLL" "$ROOT/dist/$FILE_STEM.dll"
clean_macos_metadata "$ROOT/dist"

# mods 目录内用"临时文件 + mv"原子替换(与主模组脚本一致):cp 原地覆盖重写同一 inode,
# 游戏开着时其 PCK 偏移索引仍指向旧布局,读新内容会得到错乱资源(NOPE 贴图)。
deploy_atomic() {
  local src="$1" dst="$2"
  local tmp="$dst.tmp.$$"
  cp "$src" "$tmp"
  mv -f "$tmp" "$dst"
}

if [[ "$HEXTECH_SPONSOR_DEPLOY" != "0" ]]; then
  mkdir -p "$MOD_DIR"
  deploy_atomic "$ROOT/dist/$FILE_STEM.json" "$MOD_DIR/$FILE_STEM.json"
  deploy_atomic "$ROOT/dist/$FILE_STEM.pck" "$MOD_DIR/$FILE_STEM.pck"
  deploy_atomic "$ROOT/dist/$FILE_STEM.dll" "$MOD_DIR/$FILE_STEM.dll"
  clean_macos_metadata "$MOD_DIR"
  echo "Deployed to $MOD_DIR"
else
  echo "Built package artifacts in $ROOT/dist without deploying to the installed game."
fi
