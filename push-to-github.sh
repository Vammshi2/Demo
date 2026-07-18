#!/bin/sh
set -eu

repo_url="https://github.com/Vammshi2/Demo.git"

cd "$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"

if [ ! -d .git ]; then
  git init
fi

git branch -M main

if git remote get-url origin >/dev/null 2>&1; then
  git remote set-url origin "${repo_url}"
else
  git remote add origin "${repo_url}"
fi

for ignored_path in \
  .env.local \
  LicenseAuthority/.env.local \
  LicenseAuthority/.authority-bootstrap-token.txt \
  App_Data/license-cache.dat \
  LicenseAuthority/App_Data/license-authority-dev.db
do
  if ! git check-ignore -q "${ignored_path}"; then
    echo "Refusing to continue because ${ignored_path} is not ignored." >&2
    exit 1
  fi
done

git add .

if git diff --cached --quiet; then
  echo "No changes to commit."
else
  git commit -m "first commit"
fi

git push -u origin main
