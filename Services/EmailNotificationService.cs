using System.Net;
using HostelPro.Data;
using HostelPro.Models;
using Microsoft.Extensions.Options;
using Resend;

namespace HostelPro.Services;

public sealed class EmailDeliveryOptions
{
    public const string SectionName = "Email";

    public string FromAddress { get; set; } = string.Empty;
    public string AdminAddress { get; set; } = string.Empty;
}

public interface IEmailNotificationService
{
    Task<bool> SendRegistrationAsync(ApplicationUser user, CancellationToken cancellationToken = default);
    Task<bool> SendEnquiryReceivedAsync(ContactForm form, CancellationToken cancellationToken = default);
    Task<bool> SendInvoiceAsync(Bill bill, HostelSetting settings, CancellationToken cancellationToken = default);
    Task<bool> SendPaymentReceiptAsync(Payment payment, HostelSetting settings, CancellationToken cancellationToken = default);
}

public sealed class ResendEmailNotificationService(
    IResend resend,
    IOptions<EmailDeliveryOptions> options,
    IConfiguration configuration,
    IHostelSettingsReader settingsReader,
    IReceiptPdfService receiptPdfService,
    ILogger<ResendEmailNotificationService> logger) : IEmailNotificationService
{
    private readonly EmailDeliveryOptions email = options.Value;

    public async Task<bool> SendRegistrationAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return false;
        }

        var settings = await settingsReader.GetSettingsAsync(cancellationToken);
        var propertyName = string.IsNullOrWhiteSpace(settings.HostelName) ? "Your accommodation" : settings.HostelName;
        var safeName = Encode(user.FullName);
        return await SendAsync(new EmailMessage
        {
            From = email.FromAddress,
            To = user.Email,
            Subject = $"Welcome to {propertyName}",
            HtmlBody = $"<p>Hello {safeName},</p><p>Your {Encode(propertyName)} resident account has been created successfully.</p>"
        }, cancellationToken);
    }

    public async Task<bool> SendEnquiryReceivedAsync(
        ContactForm form,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email.AdminAddress))
        {
            return false;
        }

        var settings = await settingsReader.GetSettingsAsync(cancellationToken);
        return await SendAsync(new EmailMessage
        {
            From = email.FromAddress,
            To = email.AdminAddress,
            ReplyTo = form.Email,
            Subject = $"New {settings.HostelName} enquiry from {form.Name.Trim()}",
            HtmlBody = $"""
                <h2>New website enquiry</h2>
                <p><strong>Name:</strong> {Encode(form.Name)}</p>
                <p><strong>Email:</strong> {Encode(form.Email)}</p>
                <p><strong>Phone:</strong> {Encode(form.Phone)}</p>
                <p><strong>Message:</strong><br>{Encode(form.Message)}</p>
                """
        }, cancellationToken);
    }

    public Task<bool> SendPaymentReceiptAsync(
        Payment payment,
        HostelSetting settings,
        CancellationToken cancellationToken = default)
    {
        var recipient = payment.Tenant?.Email;
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return Task.FromResult(false);
        }

        var receipt = receiptPdfService.BuildReceipt(payment, settings);
        var message = new EmailMessage
        {
            From = email.FromAddress,
            To = recipient,
            Subject = $"{settings.HostelName} payment receipt {payment.ReceiptNumber}",
            HtmlBody = $"""
                <p>Hello {Encode(payment.Tenant?.FullName)},</p>
                <p>We received your payment of INR {payment.PaymentAmount:N2}.</p>
                <p><strong>Transaction:</strong> {Encode(payment.TransactionId)}<br>
                <strong>Receipt:</strong> {Encode(payment.ReceiptNumber)}</p>
                """,
            Attachments =
            [
                new EmailAttachment
                {
                    Filename = $"{payment.ReceiptNumber}.pdf",
                    Content = receipt,
                    ContentType = "application/pdf"
                }
            ]
        };

        return SendAsync(message, cancellationToken);
    }

    public Task<bool> SendInvoiceAsync(
        Bill bill,
        HostelSetting settings,
        CancellationToken cancellationToken = default)
    {
        var recipient = bill.Tenant?.Email;
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return Task.FromResult(false);
        }

        return SendAsync(new EmailMessage
        {
            From = email.FromAddress,
            To = recipient,
            Subject = $"{settings.HostelName} invoice {bill.InvoiceNumber}",
            HtmlBody = $"""
                <h2>Monthly rent invoice</h2>
                <p>Hello {Encode(bill.Tenant?.FullName)},</p>
                <p>Your invoice for {Encode(bill.BillMonth)} {bill.BillYear} is ready.</p>
                <p><strong>Invoice:</strong> {Encode(bill.InvoiceNumber)}<br>
                <strong>Total:</strong> INR {bill.TotalAmount:N2}<br>
                <strong>Paid:</strong> INR {bill.PaidAmount:N2}<br>
                <strong>Due:</strong> INR {Math.Max(bill.TotalAmount - bill.PaidAmount, 0):N2}<br>
                <strong>Due date:</strong> {bill.DueDate:dd MMM yyyy}</p>
                <p>Payment methods: UPI, cash, or bank transfer.</p>
                <p>{Encode(settings.ContactPhone)}<br>{Encode(settings.ContactEmail)}</p>
                """
        }, cancellationToken);
    }

    private async Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuration["Resend:ApiToken"])
            || string.IsNullOrWhiteSpace(email.FromAddress)
            || string.IsNullOrWhiteSpace(message.To?.ToString()))
        {
            logger.LogInformation("Email delivery skipped because Resend configuration is incomplete.");
            return false;
        }

        try
        {
            await resend.EmailSendAsync(message, cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Resend email delivery failed for subject {Subject}.", message.Subject);
            return false;
        }
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value?.Trim() ?? string.Empty);
}
