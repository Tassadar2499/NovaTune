#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
UI_TESTS_DIR="${SCRIPT_DIR}/host"
UI_TESTS_NODE_MODULES="${UI_TESTS_DIR}/node_modules"
CLIENT_NODE_MODULES="${REPO_ROOT}/src/NovaTuneClient/node_modules"
PLAYER_PLAYWRIGHT_CLI="${REPO_ROOT}/src/NovaTuneClient/apps/player/node_modules/@playwright/test/cli.js"
WORKSPACE_PLAYWRIGHT_CLI="${CLIENT_NODE_MODULES}/.pnpm/@playwright+test@1.58.1/node_modules/@playwright/test/cli.js"
WORKSPACE_PLAYWRIGHT_PACKAGE="${REPO_ROOT}/src/NovaTuneClient/apps/player/node_modules/@playwright/test"
WORKSPACE_NODE_TYPES="${CLIENT_NODE_MODULES}/@types/node"
WORKSPACE_PLAYWRIGHT_RUNTIME="${CLIENT_NODE_MODULES}/.pnpm/playwright@1.58.1/node_modules/playwright"
WORKSPACE_PLAYWRIGHT_CORE="${CLIENT_NODE_MODULES}/.pnpm/playwright-core@1.58.1/node_modules/playwright-core"
CREATED_TEMP_NODE_MODULES=0

if command -v node >/dev/null 2>&1; then
  NODE_BIN="node"
else
  echo "node is required to run UI tests." >&2
  exit 1
fi

if [[ -f "${PLAYER_PLAYWRIGHT_CLI}" ]]; then
  PLAYWRIGHT_CLI="${PLAYER_PLAYWRIGHT_CLI}"
elif [[ -f "${WORKSPACE_PLAYWRIGHT_CLI}" ]]; then
  PLAYWRIGHT_CLI="${WORKSPACE_PLAYWRIGHT_CLI}"
else
  cat >&2 <<'EOF'
Could not find Playwright in the frontend workspace.
Run the frontend dependency install first, then retry:
  cd src/NovaTuneClient
  pnpm install
EOF
  exit 1
fi

create_temp_node_modules() {
  mkdir -p "${UI_TESTS_NODE_MODULES}/@playwright" "${UI_TESTS_NODE_MODULES}/@types"
  ln -s "${WORKSPACE_PLAYWRIGHT_PACKAGE}" "${UI_TESTS_NODE_MODULES}/@playwright/test"
  ln -s "${WORKSPACE_NODE_TYPES}" "${UI_TESTS_NODE_MODULES}/@types/node"
  ln -s "${WORKSPACE_PLAYWRIGHT_RUNTIME}" "${UI_TESTS_NODE_MODULES}/playwright"
  ln -s "${WORKSPACE_PLAYWRIGHT_CORE}" "${UI_TESTS_NODE_MODULES}/playwright-core"
  CREATED_TEMP_NODE_MODULES=1
}

cleanup() {
  if [[ "${CREATED_TEMP_NODE_MODULES}" -eq 1 ]]; then
    rm -rf "${UI_TESTS_NODE_MODULES}"
  fi
}

trap cleanup EXIT

if [[ ! -d "${UI_TESTS_NODE_MODULES}" ]]; then
  if [[ ! -d "${WORKSPACE_PLAYWRIGHT_PACKAGE}" || ! -d "${WORKSPACE_NODE_TYPES}" || ! -d "${WORKSPACE_PLAYWRIGHT_RUNTIME}" || ! -d "${WORKSPACE_PLAYWRIGHT_CORE}" ]]; then
    cat >&2 <<'EOF'
Could not build the temporary UI test node_modules bridge.
Run the frontend dependency install first, then retry:
  cd src/NovaTuneClient
  pnpm install
EOF
    exit 1
  fi

  create_temp_node_modules
fi

cd "${REPO_ROOT}"
exec "${NODE_BIN}" "${PLAYWRIGHT_CLI}" test -c "${UI_TESTS_DIR}/playwright.config.ts" "$@"
