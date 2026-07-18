using HostelPro.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HostelPro.Services;

public interface IReceiptPdfService
{
    byte[] BuildReceipt(Payment payment, HostelSetting setting);
}

public sealed class ReceiptPdfService : IReceiptPdfService
{
    public byte[] BuildReceipt(Payment payment, HostelSetting setting)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(style => style.FontSize(11));
                page.Header().Column(column =>
                {
                    column.Item().Text(setting.HostelName).FontSize(22).Bold();
                    column.Item().Text(setting.Address).FontColor(Colors.Grey.Darken1);
                    column.Item().PaddingTop(12).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingVertical(24).Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Text("Payment Receipt").FontSize(18).Bold();
                    column.Item().Text($"Receipt Number: {payment.ReceiptNumber}");
                    column.Item().Text($"Transaction ID: {payment.TransactionId}");
                    column.Item().Text($"Amount Paid: INR {payment.PaymentAmount:N2}").Bold();
                    column.Item().Text($"Payment Method: {payment.PaymentMethod.ToUpperInvariant()}");
                    column.Item().Text($"Payment Date: {payment.CreatedUtc:dd MMM yyyy HH:mm} UTC");
                    column.Item().PaddingTop(24).Text("Authorized Signature").FontColor(Colors.Grey.Darken2);
                });

                page.Footer().AlignCenter().Text($"Generated securely by {setting.HostelName}");
            });
        }).GeneratePdf();
    }
}
