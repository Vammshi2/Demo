# Vercel deployment

This repository contains two separate ASP.NET Core applications. Vercel should deploy both through `Dockerfile.vercel`; if the CLI says "No framework detected" and deploys output directory `.`, stop and redeploy after confirming `Dockerfile.vercel` exists.

- Main customer app: repository root, container file at `Dockerfile.vercel`
- License authority app: `LicenseAuthority/`, container file at `LicenseAuthority/Dockerfile.vercel`

Deploy them as two separate Vercel projects. Do not store production database passwords, Supabase keys, license keys, webhook secrets, or email tokens in `appsettings.json`.

## Install and authenticate

Vercel CLI requires Node.js 18 or newer. If your terminal shows Node 16, upgrade first:

```sh
nvm install 20
nvm use 20
nvm alias default 20
```

```sh
npm i -g vercel
vercel login
```

## Main customer app

Run these commands from the repository root:

```sh
vercel link
vercel env add ConnectionStrings__DefaultConnection production
vercel env add Licensing__ValidationUrl production
vercel env add Licensing__ManagementUrl production
vercel env add Licensing__LicenseKey production
vercel env add Licensing__InstallationId production
vercel env add Provisioning__SetupToken production
vercel env add PaymentGateway__WebhookSecret production
vercel env add Resend__ApiToken production
vercel env add Email__FromAddress production
vercel env add Email__AdminAddress production
vercel env add AllowedHosts production
vercel deploy --prod --force
```

Use these non-secret values unless your deployment needs different settings:

```text
Licensing__RequireLicense=true
Licensing__ProductCode=hostelpro
Licensing__InstallationId=<stable-guid-generated-once-for-this-customer-deployment>
Licensing__ValidationIntervalMinutes=1
Licensing__OfflineGraceMinutes=1
Storage__DataPath=/tmp/hostelpro-data
DataProtection__KeysPath=/tmp/hostelpro-data/DataProtectionKeys
AllowedHosts=*
```

The `Licensing__ValidationUrl` value should point to the deployed license authority API:

```text
https://<license-authority-domain>/api/v1/licenses/validate
```

## License authority app

Run these commands from `LicenseAuthority/`:

```sh
cd LicenseAuthority
vercel link
vercel env add ConnectionStrings__LicenseAuthority production
vercel env add AdminSetup__BootstrapToken production
vercel env add DataProtection__KeysPath production
vercel env add Authority__PublicUrl production
vercel env add AllowedHosts production
vercel deploy --prod --force
```

Use these non-secret values unless your deployment needs different settings:

```text
AuthorityDatabase__Provider=postgresql
DataProtection__KeysPath=/tmp/hostelpro-authority/DataProtectionKeys
AllowedHosts=*
```

For `ConnectionStrings__LicenseAuthority` on Vercel, use the Supabase **Session pooler** connection string, not the direct `db.<project>.supabase.co:5432` string, unless the Supabase IPv4 add-on is enabled. Vercel is IPv4-only for this database connection path.

After the authority deploys, open:

```text
https://<license-authority-domain>/setup
```

Use the `AdminSetup__BootstrapToken` value to create the first vendor administrator. Rotate or remove that bootstrap token after setup.

## Notes

- Vercel CLI prints the deployment URL after `vercel deploy --prod`.
- A correct container deployment log should mention building from `Dockerfile.vercel` and Fluid compute/container deployment. If it only says "No framework detected" with output directory `.`, that deployment is static and should not be used.
- The app writes runtime cache and KYC uploads to `/tmp` in the container. `/tmp` is not durable storage, so production file uploads should later move to Supabase Storage or another object store.
- Local SQLite files, uploaded data, build outputs, `.env` files, and development settings are excluded by `.dockerignore` and `.vercelignore`.
