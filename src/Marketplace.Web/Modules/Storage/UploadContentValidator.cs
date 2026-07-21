using System.Text;

namespace Marketplace.Web.Modules.Storage;

public static class UploadContentValidator
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static async Task<bool> MatchesDeclaredTypeAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        var sample = new byte[(int)Math.Min(file.Length, 4096)];
        await using var stream = file.OpenReadStream();
        var read = 0;
        while (read < sample.Length)
        {
            var count = await stream.ReadAsync(sample.AsMemory(read, sample.Length - read), cancellationToken);
            if (count == 0) break;
            read += count;
        }
        var bytes = sample.AsSpan(0, read);
        return file.ContentType.ToLowerInvariant() switch
        {
            "image/jpeg" => StartsWith(bytes, [0xFF, 0xD8, 0xFF]),
            "image/png" => StartsWith(bytes, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
            "image/webp" => bytes.Length >= 12 && StartsWith(bytes, "RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8),
            "application/pdf" => StartsWith(bytes, "%PDF-"u8),
            "text/plain" => IsPlainText(bytes),
            _ => false
        };
    }

    private static bool StartsWith(ReadOnlySpan<byte> value, ReadOnlySpan<byte> prefix) => value.Length >= prefix.Length && value[..prefix.Length].SequenceEqual(prefix);

    private static bool IsPlainText(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty || bytes.Contains((byte)0)) return false;
        try
        {
            var text = StrictUtf8.GetString(bytes);
            return text.All(character => character is '\r' or '\n' or '\t' || !char.IsControl(character));
        }
        catch (DecoderFallbackException) { return false; }
    }
}
