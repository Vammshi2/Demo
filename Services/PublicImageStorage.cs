using Microsoft.AspNetCore.Components.Forms;

namespace HostelPro.Services;

public interface IPublicImageStorage
{
    Task<string> SaveAsync(IBrowserFile file, string collection, CancellationToken cancellationToken = default);
}

public sealed class PublicImageStorage(IWebHostEnvironment environment) : IPublicImageStorage
{
    private const long MaximumFileSize = 5 * 1024 * 1024;
    private static readonly string[] AllowedCollections = ["rooms", "gallery"];

    public async Task<string> SaveAsync(IBrowserFile file, string collection, CancellationToken cancellationToken = default)
    {
        if (!AllowedCollections.Contains(collection, StringComparer.Ordinal))
        {
            throw new InvalidDataException("The image destination is invalid.");
        }

        var extension = Path.GetExtension(file.Name).ToLowerInvariant();
        var validType = extension switch
        {
            ".jpg" or ".jpeg" => file.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase),
            ".png" => file.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase),
            ".webp" => file.ContentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
        if (!validType)
        {
            throw new InvalidDataException("Upload a JPG, PNG, or WebP image.");
        }

        if (file.Size <= 0 || file.Size > MaximumFileSize)
        {
            throw new InvalidDataException("The image must be 5 MB or smaller.");
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
        var path = Path.Combine(environment.WebRootPath, "images", collection);
        Directory.CreateDirectory(path);
        await File.WriteAllBytesAsync(Path.Combine(path, fileName), bytes, cancellationToken);
        return $"/images/{collection}/{fileName}";
    }

    private static bool HasExpectedSignature(string extension, byte[] bytes) => extension switch
    {
        ".jpg" or ".jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        ".png" => bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        ".webp" => bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };
}
