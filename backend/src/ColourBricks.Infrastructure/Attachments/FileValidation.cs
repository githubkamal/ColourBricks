namespace ColourBricks.Infrastructure.Attachments;

/// <summary>
/// Extension allowlist and magic-byte check for uploads (plan.md P2-T08). A file
/// renamed from <c>.exe</c> to <c>.pdf</c> fails the signature check.
/// </summary>
internal static class FileValidation
{
    public static readonly IReadOnlySet<string> AllowedExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf", ".jpg", ".jpeg", ".png", ".webp", ".xlsx", ".csv" };

    public static bool IsAllowedExtension(string extension) => AllowedExtensions.Contains(extension);

    /// <summary>True when the leading bytes are consistent with the claimed extension.</summary>
    public static bool MatchesSignature(string extension, ReadOnlySpan<byte> head)
    {
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => StartsWith(head, "%PDF"u8),
            ".png" => StartsWith(head, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
            ".jpg" or ".jpeg" => StartsWith(head, [0xFF, 0xD8, 0xFF]),
            ".webp" => head.Length >= 12 && StartsWith(head, "RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8),
            ".xlsx" => StartsWith(head, [0x50, 0x4B, 0x03, 0x04]) || StartsWith(head, [0x50, 0x4B, 0x05, 0x06]),
            ".csv" => LooksLikeText(head),
            _ => false,
        };
    }

    private static bool StartsWith(ReadOnlySpan<byte> data, ReadOnlySpan<byte> prefix) =>
        data.Length >= prefix.Length && data[..prefix.Length].SequenceEqual(prefix);

    private static bool LooksLikeText(ReadOnlySpan<byte> head)
    {
        if (head.IsEmpty)
        {
            return false;
        }

        foreach (byte b in head)
        {
            // Reject NUL and other control bytes that never appear in CSV text.
            if (b == 0 || (b < 0x09) || (b > 0x0D && b < 0x20 && b != 0x1B))
            {
                return false;
            }
        }

        return true;
    }
}
