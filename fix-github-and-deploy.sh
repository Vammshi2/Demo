#!/bin/sh
set -eu

cd "$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"

if [ -d LicenseAuthority/.git ]; then
  backup="/tmp/LicenseAuthority.git.backup.$(date +%Y%m%d%H%M%S)"
  mv LicenseAuthority/.git "${backup}"
  echo "Moved nested LicenseAuthority Git metadata to ${backup}"
fi

if git ls-tree HEAD LicenseAuthority 2>/dev/null | grep -q '^160000 '; then
  git rm --cached LicenseAuthority
fi

git add vercel.json LicenseAuthority VERCEL_DEPLOYMENT.md

if git diff --cached --quiet; then
  echo "No Git changes to commit."
else
  git commit -m "Stabilize authority deployment"
fi

git push -u origin main

if [ -s "${HOME}/.nvm/nvm.sh" ]; then
  # shellcheck disable=SC1090
  . "${HOME}/.nvm/nvm.sh"
  nvm use 20 >/dev/null 2>&1 || true
fi

echo "Deploying authority app..."
(
  cd LicenseAuthority
  vercel deploy . --prod --force --logs \
    --project hostelpro-authority \
    --scope tvamshikrishna2-gmailcoms-projects
)

echo "Authority deployment complete. Test: https://hostelpro-authority.vercel.app/setup"
