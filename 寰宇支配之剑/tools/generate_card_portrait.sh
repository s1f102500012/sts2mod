#!/bin/zsh
set -euo pipefail

SCRIPT_DIR="${0:A:h}"
ROOT="${SCRIPT_DIR:h}"
ASSETS="$ROOT/assets/images/relics"
OUTPUT="${1:-$ROOT/assets/images/cards/universal_dominion_sword_card.png}"

MAGICK="$(command -v magick)"
if [[ -z "$MAGICK" ]]; then
	print -u2 "ImageMagick is required to generate the card portrait."
	exit 1
fi

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

# Render a frozen frame by executing the same constants, sprite-frame timing,
# spherical projection, symbol sampling and blade-mask blend as the live shader.
python3 "$ROOT/tools/render_static_cosmic_frame.py" \
	--assets "$ASSETS" \
	--output "$WORK_DIR/sword.png" \
	--size 160 \
	--tick 0

# The portrait itself is the game's complete 250x190 artwork rectangle.
"$MAGICK" -size 250x190 radial-gradient:'#35145f-#03000b' \
	"$WORK_DIR/background.png"

"$MAGICK" "$WORK_DIR/background.png" "$WORK_DIR/sword.png" \
	-gravity center -compose over -composite \
	-alpha off -depth 8 -strip \
	"$OUTPUT"

print "Generated $OUTPUT (250x190)"
