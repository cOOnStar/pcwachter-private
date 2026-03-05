#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT_DIR="${ROOT_DIR}/docs/audit/_generated"

mkdir -p "${OUT_DIR}"

if ! command -v rg >/dev/null 2>&1; then
  echo "ERROR: ripgrep (rg) is required."
  exit 1
fi

echo "[audit] root=${ROOT_DIR}"
echo "[audit] out=${OUT_DIR}"

{
  echo "file,prefix,tags"
  rg -n "APIRouter\\(" "${ROOT_DIR}/server/api/app/routers" -g "*.py" | \
    sed -E 's#^(.+):([0-9]+):.*prefix=\"([^\"]*)\".*tags=\\[\"([^\"]*)\"\\].*#\1,\3,\4#g'
} > "${OUT_DIR}/routers_quick.csv"

{
  echo "route,file"
  rg -n "<Route path=" "${ROOT_DIR}/server/console/src/App.tsx" | \
    sed -E 's#^(.+):([0-9]+):.*path=\"([^\"]+)\".*#\3,\1#g'
} > "${OUT_DIR}/console_routes_quick.csv"

{
  echo "route,file"
  find "${ROOT_DIR}/server/home/src/app" -type f -name "page.tsx" | \
    sed "s#${ROOT_DIR}/server/home/src/app##; s#/page.tsx##; s#^$#/#" | \
    awk '{print $0 ",server/home/src/app" $0 "/page.tsx"}'
} > "${OUT_DIR}/home_routes_quick.csv"

{
  echo "revision,file"
  rg -n "^revision\\s*=\\s*\"" "${ROOT_DIR}/server/api/alembic/versions" -g "*.py" | \
    sed -E 's#^(.+):([0-9]+):revision = \"([^\"]+)\"#\3,\1#g'
} > "${OUT_DIR}/migrations_quick.csv"

{
  echo "class,table,line,file"
  rg -n "^class\\s+.+\\(Base\\):|__tablename__\\s*=\\s*\"" "${ROOT_DIR}/server/api/app/models.py"
} > "${OUT_DIR}/models_quick.txt"

echo "[audit] done"
