using System.Text;

namespace ColourBricks.Domain.Services;

/// <summary>
/// Normalises master-record names for duplicate detection (plan.md §6): lowercase,
/// punctuation stripped, whitespace collapsed. Shared by parties and items.
/// </summary>
public static class NameNormalizer
{
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(name.Length);
        bool lastWasSpace = true; // trims leading whitespace

        foreach (char ch in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastWasSpace = false;
            }
            else if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace)
                {
                    builder.Append(' ');
                    lastWasSpace = true;
                }
            }
            // punctuation: dropped
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// True when two already-normalised names are close enough to warn about
    /// (small edit distance, or one contained in the other).
    /// </summary>
    public static bool AreNearDuplicates(string normalizedA, string normalizedB)
    {
        if (normalizedA.Length == 0 || normalizedB.Length == 0 || normalizedA == normalizedB)
        {
            return normalizedA == normalizedB && normalizedA.Length > 0;
        }

        if (normalizedA.Contains(normalizedB, StringComparison.Ordinal)
            || normalizedB.Contains(normalizedA, StringComparison.Ordinal))
        {
            return Math.Abs(normalizedA.Length - normalizedB.Length) <= 4;
        }

        int threshold = Math.Max(1, Math.Min(normalizedA.Length, normalizedB.Length) / 5);
        return LevenshteinDistance(normalizedA, normalizedB) <= threshold;
    }

    public static int LevenshteinDistance(string a, string b)
    {
        int[] previous = new int[b.Length + 1];
        int[] current = new int[b.Length + 1];

        for (int j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
