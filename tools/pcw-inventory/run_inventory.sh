#!/usr/bin/env bash
set -euo pipefail
REPO="${1:-.}"
OUT="${2:-./_inventory_out}"
FRONT="${3:-}"
CMD="python3 ./tools/pcw-inventory/generate_inventory.py --repo \"$REPO\" --out \"$OUT\""
if [[ -n "$FRONT" ]]; then CMD="$CMD --frontend \"$FRONT\""; fi
echo "Running: $CMD"
eval "$CMD"
