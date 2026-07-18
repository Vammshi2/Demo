# HostelPro .NET Website

HostelPro is a configurable ASP.NET Core Blazor Server property-management product. Each customer deployment has its own database, branding, owner accounts, resident accounts, and vendor-controlled subscription license.

Planned product enhancements are tracked in [`ROADMAP.md`](ROADMAP.md).

## Tech Stack

- C# / ASP.NET Core 8
- Blazor Server interactive components
- ASP.NET Core Identity with Admin and User roles
- Entity Framework Core with PostgreSQL/Supabase and startup migrations
- QuestPDF for server-side receipt PDF generation
- QRCoder for server-side UPI QR generation
- Resend for server-side registration, enquiry, invoice, and payment-receipt email delivery
- Vendor license validation with an encrypted offline cache and configurable grace period

## Page Components

Public components:

- `Components/Pages/Home.razor`
- `Components/Pages/Rooms.razor`
- `Components/Pages/Gallery.razor`
- `Components/Pages/About.razor`
- `Components/Pages/Contact.razor`
- `Components/Pages/Apply.razor`
- `Components/Pages/Pay.razor`

Account components:

- `Components/Pages/Account/Login.razor`
- `Components/Pages/Account/Register.razor`
- `Components/Pages/Account/ForgotPassword.razor`
- `Components/Pages/Account/ResetPassword.razor`
- `Components/Pages/TenantPortal.razor`

Admin components:

- `Components/Pages/Admin/Dashboard.razor`
- `Components/Pages/Admin/RoomManagement.razor`
- `Components/Pages/Admin/BedManagement.razor`
- `Components/Pages/Admin/TenantManagement.razor`
- `Components/Pages/Admin/TenantApplications.razor`
- `Components/Pages/Admin/BillManagement.razor`
- `Components/Pages/Admin/PaymentManagement.razor`
- `Components/Pages/Admin/ExpenseManagement.razor`
- `Components/Pages/Admin/KycManagement.razor`
- `Components/Pages/Admin/MaintenanceManagement.razor`
- `Components/Pages/Admin/MessManagement.razor`
- `Components/Pages/Admin/EnquiryManagement.razor`
- `Components/Pages/Admin/Reports.razor`
- `Components/Pages/Admin/Settings.razor`
- `Components/Pages/Admin/UserManagement.razor`
- `Components/Pages/Admin/WebsiteManagement.razor`

## Implemented Management Features

- Monthly bill generation with invoice numbers, invoice email action, partial payment tracking, overdue status, and late-fee application.
- Expense tracker with monthly expense and net-profit dashboard/reporting.
- Tenant KYC/document workflow with secure server-side storage under `App_Data` and admin-only download endpoints.
- Tenant portal for bill history, receipts, KYC uploads, maintenance tickets, and mess opt-in/opt-out.
- Maintenance ticketing with priority, assignment, status, and resolution tracking.
- Food/mess management with daily menus and opt-in/opt-out counts.
- Dashboard analytics for expected revenue, monthly collection, monthly expenses, net profit, defaulters, open tickets, and pending KYC.
- Public resident applications with an admin review queue and controlled room/bed allocation.
- Expiring, revocable payment links whose plaintext tokens are never stored in the database.
- UPI reference verification, admin cash/bank/UPI entry, and an HMAC-signed provider webhook for automatic payment updates.
- Printable PDF reports for current tenants, successful payments, and unpaid/defaulter balances.
- Property-aware PG/co-living and men/women/mixed branding with resident eligibility validation.

## Local Setup

Install the .NET 8 SDK. Store secrets in .NET user-secrets for local development:

```bash
dotnet restore
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=aws-0-<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<database-password>;SSL Mode=Require;Trust Server Certificate=true"
dotnet user-secrets set "Licensing:ValidationUrl" "https://licenses.your-domain.example/api/v1/licenses/validate"
dotnet user-secrets set "Licensing:LicenseKey" "<customer-license-key>"
dotnet user-secrets set "Payment:UpiId" "your-upi-id@bank"
dotnet user-secrets set "PaymentGateway:WebhookSecret" "<random-webhook-signing-secret>"
dotnet user-secrets set "Resend:ApiToken" "your-resend-api-token"
dotnet user-secrets set "Email:FromAddress" "Property Team <noreply@your-domain.example>"
dotnet user-secrets set "Email:AdminAddress" "admin@example.com"
dotnet run
```

For local development only, license validation is bypassed when no remote license configuration exists. Open `/Setup` to create the first owner account. That route closes automatically after the first admin is created.

## appsettings.json

[`appsettings.json`](appsettings.json) contains the complete configuration shape, including PostgreSQL, optional Supabase API slots, licensing, payment, and email keys. Blank values are intentional. Never commit a database password, Supabase secret key, Resend token, or customer license key.

The customer application currently connects to Supabase through PostgreSQL/EF Core. Therefore only `ConnectionStrings:DefaultConnection` is required for data access. `Supabase:PublishableKey` and `Supabase:SecretKey` are reserved for future server-side Supabase API integrations and are not used by the current application.

## Vendor Subscription Control

The separate [License Authority](LicenseAuthority/README.md) is deployed and operated only by the software vendor. It issues customer keys, tracks installations, renews paid-through dates, and immediately blocks licenses marked unpaid or suspended. Customer deployments receive only their own license key and the public validation endpoint; they never receive vendor administrator credentials or the authority database connection.

## Supabase Postgres Setup

This app does not need `NEXT_PUBLIC_*` variables because it is not a Next.js client app. For an IPv4 development network, use the Supavisor session pooler connection shown in Supabase's **Connect** dialog:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=aws-0-<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<database-password>;SSL Mode=Require;Trust Server Certificate=true"
```

For deployed hosting, use:

```bash
ConnectionStrings__DefaultConnection="Host=aws-0-<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<database-password>;SSL Mode=Require;Trust Server Certificate=true"
Licensing__ValidationUrl="https://licenses.your-domain.example/api/v1/licenses/validate"
Licensing__LicenseKey="<customer-license-key>"
Resend__ApiToken="your-resend-api-token"
Email__FromAddress="Property Team <noreply@your-domain.example>"
Email__AdminAddress="admin@example.com"
Payment__UpiId="your-upi-id@bank"
PaymentGateway__WebhookSecret="<random-webhook-signing-secret>"
```

Startup applies pending EF Core migrations and seeds only required roles, an empty property profile, and the standard amenity list. Demo operational data is only seeded when `SeedDemoData` is explicitly enabled in Development. Keep database, licensing, Supabase, and Resend credentials out of source control.

## Security Notes

- Admin pages require the `Admin` role through `[Authorize(Roles = AppRoles.Admin)]`.
- Cookies are HTTP-only, `Secure`, `SameSite=Strict`, and use the `__Host-` prefix.
- Login and registration forms use ASP.NET Core antiforgery tokens.
- Admin credentials are never committed or seeded. The first owner creates them through the one-time `/Setup` flow.
- Public application and payment routes use server-side validation. Applications enter a pending queue, and payment data is accessible only through a random bearer token stored as a SHA-256 hash.
- Gateway callbacks require an HMAC-SHA256 signature in `X-HostelPro-Signature`; transaction identifiers are unique to prevent replayed payments.
- Management queries and mutations stay inside Blazor Server components and repository services.
- Security headers include frame blocking, no-sniff, referrer policy, permissions policy, and CSP.
- Audit logs are written for room, bed, tenant, bill, payment, expense, KYC, maintenance, mess, enquiry, gallery, and settings changes.
- Uploaded KYC documents are limited to verified PDF/JPEG/PNG files up to 5 MB, stored outside `wwwroot`, and streamed through an admin-only licensed endpoint.
- Supabase secrets must stay server-side. Do not place secret keys in `wwwroot`, Razor markup, public JavaScript, or committed config files.

## Production Checklist

- Use PostgreSQL/Supabase or another managed database for production.
- Set `ConnectionStrings:DefaultConnection` in the hosting environment.
- Turn on confirmed email before public launch if email delivery is configured.
- Verify a sending domain in Resend before sending to arbitrary tenant addresses. Resend's onboarding sender is intended only for initial account testing.
- Store uploaded documents outside `wwwroot` or behind authenticated download endpoints.
- Review and apply generated EF migrations before each production deployment.
- Configure and test vendor licensing before handing a deployment to a customer.
- Keep payment and email secrets in a server-side secret store only.

## UI Direction

The UI follows the supplied HostelHub structure: light public pages, card-based room browsing, a dark navy admin sidebar, blue primary actions, orange accent styling, searchable management tables, status badges, and compact dashboard metrics.
