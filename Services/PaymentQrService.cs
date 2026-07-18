using System.Globalization;
using QRCoder;

namespace HostelPro.Services;

public interface IPaymentQrService
{
    string CreateUpiQrDataUri(string upiId, string payeeName, decimal amount, string reference);
    string CreateUpiIntent(string upiId, string payeeName, decimal amount, string reference);
}

public sealed class PaymentQrService : IPaymentQrService
{
    public string CreateUpiIntent(string upiId, string payeeName, decimal amount, string reference)
    {
        var formattedAmount = amount.ToString("0.00", CultureInfo.InvariantCulture);
        return $"upi://pay?pa={Uri.EscapeDataString(upiId)}&pn={Uri.EscapeDataString(payeeName)}&am={formattedAmount}&cu=INR&tn={Uri.EscapeDataString(reference)}";
    }

    public string CreateUpiQrDataUri(string upiId, string payeeName, decimal amount, string reference)
    {
        var payload = CreateUpiIntent(upiId, payeeName, amount, reference);
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var qr = new PngByteQRCode(data);
        return $"data:image/png;base64,{Convert.ToBase64String(qr.GetGraphic(8))}";
    }
}
