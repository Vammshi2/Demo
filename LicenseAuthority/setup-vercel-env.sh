#!/bin/sh
set -eu

if [ -s "${HOME}/.nvm/nvm.sh" ]; then
  # shellcheck disable=SC1090
  . "${HOME}/.nvm/nvm.sh"
  if command -v nvm >/dev/null 2>&1; then
    nvm use 20 >/dev/null 2>&1 || true
  fi
fi

if ! command -v vercel >/dev/null 2>&1; then
  echo "Vercel CLI is required. Run: npm i -g vercel" >&2
  exit 1
fi

if ! command -v openssl >/dev/null 2>&1; then
  echo "openssl is required to generate the setup token." >&2
  exit 1
fi

script_dir="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
cd "${script_dir}"

token_file=".authority-bootstrap-token.txt"
if [ ! -s "${token_file}" ]; then
  token="hp_auth_setup_$(openssl rand -base64 32 | tr -dc A-Za-z0-9 | head -c 32)"
  printf "%s\n" "${token}" > "${token_file}"
  chmod 600 "${token_file}"
fi
bootstrap_token="$(cat "${token_file}")"

printf "Enter authority Supabase database password: "
stty -echo
IFS= read -r db_password
stty echo
printf "\n"

connection_string="Host=db.binfuseppdnakkguwaly.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=${db_password};SSL Mode=Require;Trust Server Certificate=true"

set_env() {
  name="$1"
  value="$2"
  vercel env rm "${name}" production --yes >/dev/null 2>&1 || true
  printf "%s\n" "${value}" | vercel env add "${name}" production
}

set_env ConnectionStrings__LicenseAuthority "${connection_string}"
set_env AuthorityDatabase__Provider "postgresql"
set_env AdminSetup__BootstrapToken "${bootstrap_token}"
set_env Authority__PublicUrl "https://hostelpro-authority.vercel.app"
set_env AllowedHosts "*"
set_env DataProtection__KeysPath "/tmp/hostelpro-authority/DataProtectionKeys"

vercel deploy --prod --force

printf "\nAuthority setup token file: %s/%s\n" "${script_dir}" "${token_file}"
printf "Open after deploy: https://hostelpro-authority.vercel.app/setup\n"
