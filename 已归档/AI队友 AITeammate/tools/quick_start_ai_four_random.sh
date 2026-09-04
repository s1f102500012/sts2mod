#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GAME_APP="${STS2_GAME_APP:-/Users/iniad/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app}"
GAME_BIN="$GAME_APP/Contents/MacOS/Slay the Spire 2"
DATA_DIR="$GAME_APP/Contents/Resources/data_sts2_macos_arm64"
STEAM_APPID_FILE="$GAME_APP/Contents/MacOS/steam_appid.txt"
STEAM_APP_ID="${STS2_STEAM_APP_ID:-2868840}"
MOD_ID="sts2AITeammate"
PROJECT_PATH="$ROOT/$MOD_ID.csproj"
BUILD_OUT="$ROOT/.godot/mono/temp/bin/Release"
MOD_DIR="$GAME_APP/Contents/MacOS/mods/$MOD_ID"
LOG_ROOT="$ROOT/logs/quickstart"

instances=1
seed=""
build=1
autopilot=1
test_map=0
headless=0
monitor=0
dry_run=0
quit_after=""
isolated_user_data=0

usage() {
  cat <<'USAGE'
Usage: tools/quick_start_ai_four_random.sh [options]

Options:
  --instances N      Launch N game processes, best-effort multi-open support.
  --seed SEED        Base run seed. Instance index is appended when N > 1.
  --no-build         Skip building/deploying the current mod DLL.
  --no-autopilot     Keep host AI takeover disabled after quick-start.
  --test-map         Enable the mod's test-map flag for the started run.
  --headless         Pass --headless to the game process.
  --quit-after MS    Pass --quit-after MS to the game process.
  --isolated-user-data
                    Use a per-instance Godot user data directory.
  --monitor          Tail launched process stdout logs after starting.
  --dry-run          Print the commands without launching.
  -h, --help         Show this help.
USAGE
}

while (($#)); do
  case "$1" in
    --instances)
      instances="${2:?missing value for --instances}"
      shift 2
      ;;
    --seed)
      seed="${2:?missing value for --seed}"
      shift 2
      ;;
    --no-build)
      build=0
      shift
      ;;
    --no-autopilot)
      autopilot=0
      shift
      ;;
    --test-map)
      test_map=1
      shift
      ;;
    --headless)
      headless=1
      shift
      ;;
    --quit-after)
      quit_after="${2:?missing value for --quit-after}"
      shift 2
      ;;
    --isolated-user-data)
      isolated_user_data=1
      shift
      ;;
    --monitor)
      monitor=1
      shift
      ;;
    --dry-run)
      dry_run=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if ! [[ "$instances" =~ ^[0-9]+$ ]] || (( instances < 1 )); then
  echo "--instances must be a positive integer." >&2
  exit 2
fi

if [[ ! -x "$GAME_BIN" ]]; then
  echo "Game binary not found or not executable: $GAME_BIN" >&2
  exit 1
fi

if [[ ! -f "$STEAM_APPID_FILE" ]]; then
  printf '%s\n' "$STEAM_APP_ID" > "$STEAM_APPID_FILE"
fi

find_dotnet() {
  if command -v dotnet >/dev/null 2>&1; then
    command -v dotnet
  elif [[ -x "/opt/homebrew/Cellar/dotnet/9.0.8/libexec/dotnet" ]]; then
    echo "/opt/homebrew/Cellar/dotnet/9.0.8/libexec/dotnet"
  else
    return 1
  fi
}

deploy_mod() {
  "$ROOT/tools/build_and_deploy.sh"
}

if (( build )); then
  deploy_mod
fi

run_id="$(date +%Y%m%d-%H%M%S)"
run_dir="$LOG_ROOT/$run_id"
if (( ! dry_run )); then
  mkdir -p "$run_dir"
fi

declare -a launched_pids=()
declare -a watchdog_pids=()
tail_pid=""

cleanup_background_helpers() {
  if [[ -n "$tail_pid" ]]; then
    kill "$tail_pid" 2>/dev/null || true
  fi
  if ((${#watchdog_pids[@]})); then
    for watchdog_pid in "${watchdog_pids[@]}"; do
      kill "$watchdog_pid" 2>/dev/null || true
    done
  fi
}

trap cleanup_background_helpers EXIT INT TERM

for ((i = 1; i <= instances; i++)); do
  instance_seed="$seed"
  if [[ -z "$instance_seed" ]]; then
    instance_seed="$run_id-$i"
  elif (( instances > 1 )); then
    instance_seed="$seed-$i"
  fi

  instance_dir="$run_dir/instance-$i"
  if (( ! dry_run )); then
    mkdir -p "$instance_dir"
  fi
  stdout_log="$instance_dir/stdout.log"
  pid_file="$instance_dir/pid"

  env_cmd=(
    env
    "STS2_AI_QUICKSTART=1"
    "STS2_AI_QUICKSTART_PLAYERS=4"
    "STS2_AI_QUICKSTART_SEED=$instance_seed"
    "STS2_AI_QUICKSTART_BEGIN=1"
    "STS2_AI_AUTOPILOT=$autopilot"
    "STS2_AI_TEST_MAP=$test_map"
    "STS2_AI_INSTANCE_INDEX=$i"
    "$GAME_BIN"
  )
  if (( headless )); then
    env_cmd+=(--headless)
  fi
  if [[ -n "$quit_after" ]]; then
    env_cmd+=(--quit-after "$quit_after")
  fi
  if (( isolated_user_data || instances > 1 )); then
    env_cmd+=(--user-data-dir "$instance_dir/userdata")
  fi

  printf 'Instance %d seed=%s log=%s\n' "$i" "$instance_seed" "$stdout_log"
  if (( dry_run )); then
    printf '  %q' "${env_cmd[@]}"
    printf '\n'
  else
    previous_dir="$PWD"
    cd "$GAME_APP/Contents/MacOS"
    if (( monitor )); then
      "${env_cmd[@]}" >"$stdout_log" 2>&1 &
    else
      nohup "${env_cmd[@]}" >"$stdout_log" 2>&1 </dev/null &
    fi
    game_pid=$!
    if (( ! monitor )); then
      disown "$game_pid" 2>/dev/null || true
    fi
    cd "$previous_dir"
    echo "$game_pid" >"$pid_file"
    launched_pids+=("$game_pid")
    if [[ -n "$quit_after" && "$quit_after" =~ ^[0-9]+$ && "$quit_after" -gt 0 ]]; then
      watchdog_seconds=$(( (quit_after + 999) / 1000 + 5 ))
      (
        sleep "$watchdog_seconds"
        if kill -0 "$game_pid" 2>/dev/null; then
          printf 'Quick-start watchdog stopping pid=%s after %ss\n' "$game_pid" "$watchdog_seconds" >>"$stdout_log"
          kill "$game_pid" 2>/dev/null || true
        fi
      ) &
      watchdog_pids+=("$!")
    fi
    sleep 2
  fi
done

echo "Quick-start logs: $run_dir"

if (( monitor && ! dry_run )); then
  tail -F "$run_dir"/instance-*/stdout.log &
  tail_pid=$!
  for game_pid in "${launched_pids[@]}"; do
    wait "$game_pid" || true
  done
  cleanup_background_helpers
  sleep 1
fi
