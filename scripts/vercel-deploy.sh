#!/bin/sh
set -eu

target="${1:-main}"

if [ -s "${HOME}/.nvm/nvm.sh" ]; then
  # shellcheck disable=SC1090
  . "${HOME}/.nvm/nvm.sh"
  if command -v nvm >/dev/null 2>&1; then
    nvm use 20 >/dev/null 2>&1 || true
  fi
fi

if ! command -v vercel >/dev/null 2>&1; then
  if ! command -v npm >/dev/null 2>&1; then
    echo "npm is required to install the Vercel CLI." >&2
    exit 1
  fi
  node_major="$(node -p 'Number(process.versions.node.split(".")[0])')"
  if [ "${node_major}" -lt 18 ]; then
    echo "Vercel CLI requires Node.js 18 or newer. Run: nvm install 20 && nvm use 20" >&2
    exit 1
  fi
  npm i -g vercel
fi

node_major="$(node -p 'Number(process.versions.node.split(".")[0])')"
if [ "${node_major}" -lt 18 ]; then
  echo "Vercel CLI requires Node.js 18 or newer. Run: nvm install 20 && nvm use 20" >&2
  exit 1
fi

case "${target}" in
  main)
    vercel deploy --prod
    ;;
  authority)
    cd LicenseAuthority
    vercel deploy --prod
    ;;
  *)
    echo "Usage: scripts/vercel-deploy.sh [main|authority]" >&2
    exit 2
    ;;
esac
