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

cat <<'INFO'
Enter the Authority Supabase pooler connection string.

Use Supabase Dashboard -> Connect -> Session pooler.
It should look like:
Host=aws-0-<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.binfuseppdnakkguwaly;Password=<password>;SSL Mode=Require;Trust Server Certificate=true

Do not use db.binfuseppdnakkguwaly.supabase.co:5432 on Vercel unless the Supabase IPv4 add-on is enabled.
INFO
printf "Authority pooler connection string: "
stty -echo
IFS= read -r connection_string
stty echo
printf "\n"

case "${connection_string}" in
  *pooler.supabase.com*)
    ;;
  *)
    echo "Warning: this does not look like a Supabase pooler connection string." >&2
    echo "Vercel may not reach the direct db.<project>.supabase.co IPv6 endpoint." >&2
    ;;
esac

set_env() {
  name="$1"
  value="$2"
  vercel env rm "${name}" production --yes \
    --project hostelpro-authority \
    --scope tvamshikrishna2-gmailcoms-projects >/dev/null 2>&1 || true
  printf "%s\n" "${value}" | vercel env add "${name}" production \
    --project hostelpro-authority \
    --scope tvamshikrishna2-gmailcoms-projects
}

set_env ConnectionStrings__LicenseAuthority "${connection_string}"
set_env AuthorityDatabase__Provider "postgresql"
set_env AuthorityDatabase__RunMigrationsOnStartup "false"
set_env AdminSetup__BootstrapToken "${bootstrap_token}"
set_env Authority__PublicUrl "https://hostelpro-authority.vercel.app"
set_env AllowedHosts "*"
set_env DataProtection__KeysPath "/tmp/hostelpro-authority/DataProtectionKeys"

vercel deploy . --prod --force \
  --project hostelpro-authority \
  --scope tvamshikrishna2-gmailcoms-projects

printf "\nAuthority setup token file: %s/%s\n" "${script_dir}" "${token_file}"
printf "Open after deploy: https://hostelpro-authority.vercel.app/setup\n"
