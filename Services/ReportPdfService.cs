using HostelPro.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HostelPro.Services;

public interface IReportPdfService
{
    byte[] BuildTenantList(IReadOnlyList<Tenant> tenants, HostelSetting setting);
    byte[] BuildPaymentList(IReadOnlyList<Payment> payments, HostelSetting setting);
    byte[] BuildUnpaidList(IReadOnlyList<Bill> bills, HostelSetting setting);
}

public sealed class ReportPdfService : IReportPdfService
{
    public byte[] BuildTenantList(IReadOnlyList<Tenant> tenants, HostelSetting setting) =>
        BuildDocument(setting, "Current Tenant List", tenants.Count, table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2.2f);
                columns.RelativeColumn(1.4f);
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(0.8f);
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(1.1f);
            });
            Header(table, "Tenant", "Phone", "Room", "Bed", "Monthly Rent", "Status");
            foreach (var tenant in tenants.OrderBy(item => item.Room?.RoomNumber).ThenBy(item => item.FullName))
            {
                Cell(table, tenant.FullName);
                Cell(table, tenant.Phone);
                Cell(table, tenant.Room?.RoomNumber ?? "-");
                Cell(table, tenant.Bed?.BedNumber ?? "-");
                Cell(table, $"INR {tenant.MonthlyRent:N2}");
                Cell(table, Label(tenant.Status));
            }
        });

    public byte[] BuildPaymentList(IReadOnlyList<Payment> payments, HostelSetting setting) =>
        BuildDocument(setting, "Paid Payment Report", payments.Count, table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(1.8f);
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(1.5f);
            });
            Header(table, "Date", "Tenant", "Receipt", "Method", "Amount", "Transaction");
            foreach (var payment in payments.Where(item => item.PaymentStatus == "success").OrderByDescending(item => item.CreatedUtc))
            {
                Cell(table, payment.CreatedUtc.ToLocalTime().ToString("dd MMM yyyy"));
                Cell(table, payment.Tenant?.FullName ?? "-");
                Cell(table, payment.ReceiptNumber);
                Cell(table, Label(payment.PaymentMethod));
                Cell(table, $"INR {payment.PaymentAmount:N2}");
                Cell(table, payment.TransactionId);
            }
        });

    public byte[] BuildUnpaidList(IReadOnlyList<Bill> bills, HostelSetting setting)
    {
        var unpaid = bills.Where(item => item.Status is "pending" or "overdue" or "partially_paid").ToList();
        return BuildDocument(setting, "Unpaid and Defaulter Report", unpaid.Count, table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.8f);
                columns.RelativeColumn(1f);
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(1f);
            });
            Header(table, "Tenant", "Room", "Invoice", "Balance", "Due Date", "Status");
            foreach (var bill in unpaid.OrderBy(item => item.DueDate))
            {
                Cell(table, bill.Tenant?.FullName ?? "-");
                Cell(table, bill.Tenant?.Room?.RoomNumber ?? "-");
                Cell(table, bill.InvoiceNumber);
                Cell(table, $"INR {Math.Max(bill.TotalAmount - bill.PaidAmount, 0):N2}");
                Cell(table, bill.DueDate.ToString("dd MMM yyyy"));
                Cell(table, Label(bill.Status));
            }
        });
    }

    private static byte[] BuildDocument(HostelSetting setting, string title, int recordCount, Action<TableDescriptor> content)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(28);
            page.DefaultTextStyle(style => style.FontSize(9).FontColor(Colors.Grey.Darken4));
            page.Header().Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text(setting.HostelName).FontSize(18).Bold().FontColor(Colors.Blue.Darken3);
                    column.Item().Text(string.IsNullOrWhiteSpace(setting.Address) ? setting.Tagline : setting.Address).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(260).AlignRight().Column(column =>
                {
                    column.Item().AlignRight().Text(title).FontSize(16).Bold();
                    column.Item().AlignRight().Text($"Generated {DateTime.Now:dd MMM yyyy, h:mm tt} · {recordCount} record(s)").FontColor(Colors.Grey.Darken1);
                });
            });
            page.Content().PaddingTop(22).Table(content);
            page.Footer().Row(row =>
            {
                row.RelativeItem().Text("Confidential administration report").FontColor(Colors.Grey.Medium);
                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        })).GeneratePdf();
    }

    private static void Header(TableDescriptor table, params string[] labels)
    {
        foreach (var label in labels)
        {
            table.Cell().Background(Colors.Blue.Darken3).Padding(7).Text(label).SemiBold().FontColor(Colors.White);
        }
    }

    private static void Cell(TableDescriptor table, string value) =>
        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(7).PaddingHorizontal(6).Text(value);

    private static string Label(string value) => value.Replace("_", " ");
}
