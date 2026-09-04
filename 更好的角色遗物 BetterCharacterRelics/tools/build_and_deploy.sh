#!/bin/zsh
set -euo pipefail

exec python3 "${0:A:h}/build_bundle.py" --deploy "$@"
