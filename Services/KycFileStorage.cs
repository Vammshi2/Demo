using Microsoft.AspNetCore.Components.Forms;

namespace HostelPro.Services;

public interface IKycFileStorage
{
    Task<string> SaveAsync(IBrowserFile file, CancellationToken cancellationToken = default);
}

public sealed class KycFileStorage(IWebHostEnvironment environment, IConfiguration configuration) : IKycFileStorage
{
    private const long MaximumFileSize = 5 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string[]> AllowedTypes =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = ["application/pdf"],
            [".jpg"] = ["image/jpeg"],
            [".jpeg"] = ["image/jpeg"],
            [".png"] = ["image/png"]
        };

    public async Task<string> SaveAsync(IBrowserFile file, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(file.Name).ToLowerInvariant();
        if (!AllowedTypes.TryGetValue(extension, out var contentTypes)
            || !contentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Upload a PDF, JPG, or PNG file.");
        }

        if (file.Size <= 0 || file.Size > MaximumFileSize)
        {
            throw new InvalidDataException("The document must be 5 MB or smaller.");
        }

        await using var input = file.OpenReadStream(MaximumFileSize, cancellationToken);
        await using var buffer = new MemoryStream((int)file.Size);
        await input.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        if (!HasExpectedSignature(extension, bytes))
        {
            throw new InvalidDataException("The selected file content does not match its extension.");
        }

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var configuredDataDirectory = configuration["Storage:DataPath"];
        var dataDirectory = string.IsNullOrWhiteSpace(configuredDataDirectory)
            ? Path.Combine(environment.ContentRootPath, "App_Data")
            : configuredDataDirectory;
        var uploadPath = Path.Combine(dataDirectory, "Uploads", "Kyc");
        Directory.CreateDirectory(uploadPath);
        await File.WriteAllBytesAsync(Path.Combine(uploadPath, fileName), bytes, cancellationToken);
        return $"kyc/{fileName}";
    }

    private static bool HasExpectedSignature(string extension, byte[] bytes) => extension switch
    {
        ".pdf" => bytes.Length >= 5 && bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8),
        ".png" => bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        ".jpg" or ".jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        _ => false
    };
}
