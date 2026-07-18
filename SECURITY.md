# Security Model

HostelPro is designed so private business data is accessed through server-side C# services, not public JavaScript bundles.

## Access Boundaries

- Public users can view active rooms, published gallery images, and submit enquiries.
- Resident accounts can access the tenant portal only for the tenant record linked to their Identity account. Email matching is used only as a compatibility fallback for older records.
- Admin users can access management pages only when assigned the `Admin` role.

## Secrets

Do not store real passwords, UPI IDs, SMTP keys, database passwords, or API keys in source control.

Use one of these mechanisms:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."
dotnet user-secrets set "Licensing:ValidationUrl" "https://licenses.example/api/v1/licenses/validate"
dotnet user-secrets set "Licensing:LicenseKey" "..."
dotnet user-secrets set "Payment:UpiId" "your-upi-id@bank"
dotnet user-secrets set "Resend:ApiToken" "your-resend-api-token"
```

In production, use the host platform's encrypted secret manager.

## Data Protection

- Use HTTPS only.
- Keep Identity cookies HTTP-only and Secure.
- Use role checks on every admin component.
- Validate input with data annotations and service-level checks.
- Accept only signature-verified PDF/JPEG/PNG identity documents up to 5 MB.
- Accept only signature-verified JPG/PNG/WebP public images up to 5 MB, and restrict remote image URLs to `Security:AllowedImageHosts`.
- Keep uploaded identity documents outside public static directories.
- Serve uploaded identity documents only through authorized server endpoints.
- Log privileged mutations to `AuditLogs`.
- Keep the PostgreSQL connection, license key, UPI ID, Supabase secret key, and Resend token in the deployment secret manager; they must never be exposed through `wwwroot` or client-side markup.

## Subscription Boundary

- Customer installations send only product code, installation ID, application version, and host name to the vendor license authority.
- Property, tenant, KYC, billing, and payment data never leaves the customer database for license validation.
- License responses are cached with ASP.NET Core Data Protection and expire after the configured offline grace period.
- Suspended, unpaid, or expired licenses block the application and all privileged mutations.

## Payment Handling

UPI QR generation is server-side. Transaction IDs are stored after admin verification. For a real gateway, verify payment callbacks on the server and never trust browser-supplied payment status.
