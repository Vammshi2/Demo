# HostelPro License Authority

Vendor-controlled ASP.NET Core 8 service for issuing HostelPro customer licenses and validating installations. It uses PostgreSQL, stores only a key prefix and SHA-256 key hash, and protects all vendor pages with a secure cookie session.

This is a separate application. It does not share the hostel application's database, Identity tables, cookies, or data-protection keys. Development uses an isolated SQLite file at `App_Data/license-authority-dev.db`; production requires a separate PostgreSQL/Supabase database account.

## Configure

Keep secrets outside `appsettings.json`. For local development:

```bash
dotnet user-secrets set --project LicenseAuthority.csproj \
  "AdminSetup:BootstrapToken" \
  "REPLACE_WITH_A_LONG_RANDOM_TOKEN"
```

The development profile uses its own SQLite database and does not need the customer database password. To test against a separate PostgreSQL database instead, set `AuthorityDatabase:Provider` to `postgresql` and configure:

```bash
dotnet user-secrets set --project LicenseAuthority.csproj \
  "ConnectionStrings:LicenseAuthority" \
  "Host=localhost;Database=hostelpro_license_authority;Username=postgres;Password=<database-password>;SSL Mode=Require"
```

For deployment, set `ConnectionStrings__LicenseAuthority`, `AdminSetup__BootstrapToken`, and `DataProtection__KeysPath` through the hosting platform's secret store. Set `AllowedHosts` to the authority hostname and terminate HTTPS either in ASP.NET Core or at a trusted reverse proxy.

## Run

```bash
dotnet run --project LicenseAuthority.csproj
```

Startup applies EF Core migrations. Open `/setup` once, supply the configured bootstrap token, and create the first vendor administrator. Remove or rotate `AdminSetup__BootstrapToken` after setup; `/setup` also refuses further accounts once an administrator exists.

Development permits an HTTP loopback URL and uses a development cookie name. Production requires HTTPS and a secure `__Host-` cookie.

The protected Licenses area can issue, list, and update licenses, set `trial`, `active`, `unpaid`, or `suspended`, change paid-through dates and installation limits, and revoke registered installations. A newly issued plaintext key is displayed once and is never persisted. Customer leases default to one minute, and the customer application also revalidates open sessions every minute.

Each license can track non-secret deployment metadata: application URL, hosting provider, region, deployment status, and an external secret-store reference. The edit page generates the required environment-variable template. Supabase passwords, customer license keys, and hosting API tokens remain in the hosting provider or external vault and are never stored in the authority database.

After the application base URL is saved, the authority automatically generates the public application, administrator login, and tenant application links. Generate an owner activation token from the license page to receive a one-time activation link. Configure the same value as `Provisioning__SetupToken` in the customer deployment. License authentication is automatic after the deployment is configured with its issued key. The first owner chooses their administrator password through the tokenized setup link; after the first administrator exists, that setup route closes permanently.

## Customer configuration

Configure the customer HostelPro deployment with:

```text
Licensing__ValidationUrl=https://licenses.example.com/api/v1/licenses/validate
Licensing__LicenseKey=<the-key-shown-at-issuance>
Licensing__ProductCode=hostelpro
Provisioning__SetupToken=<the-owner-activation-token-shown-at-issuance>
```

`POST /api/v1/licenses/validate` requires `Authorization: Bearer <customer-license-key>` and accepts the existing HostelPro request contract:

```json
{
  "productCode": "hostelpro",
  "installationId": "00000000-0000-0000-0000-000000000000",
  "applicationVersion": "1.0.0",
  "hostName": "customer-host"
}
```

## Tests

```bash
dotnet test tests/LicenseAuthority.Tests/LicenseAuthority.Tests.csproj
```

The tests cover active/trial validation, suspended and unpaid decisions, expiry, and key hashing. Database integration should run against an isolated PostgreSQL instance in deployment CI.
