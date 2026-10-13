namespace Shiranuix.Utilities;

/// <summary>
/// Natural ("human") string ordering, so <c>page2.png</c> sorts before
/// <c>page10.png</c>.
/// </summary>
/// <remarks>
/// <see cref="string"/> comparison orders <c>page10</c> before <c>page2</c>
/// because it compares lexicographically. This comparer instead splits each
/// string into runs of digits and non-digits and compares digit runs
/// numerically — which is what users expect from file names.
/// </remarks>
/// <example>
/// <code>
/// files.Sort(NaturalSort.Compare);
/// </code>
/// </example>
public static class NaturalSort
{
    /// <summary>
    /// Compares two strings naturally (numeric runs compared numerically).
    /// </summary>
    /// <param name="left">First string; may be <c>null</c>.</param>
    /// <param name="right">Second string; may be <c>null</c>.</param>
    /// <returns>
    /// Negative, zero, or positive as <paramref name="left"/> sorts before,
    /// the same as, or after <paramref name="right"/>.
    /// </returns>
    public static int Compare(string? left, string? right)
    {
        if (left is null || right is null)
        {
            // null sorts before any non-null value.
            return ReferenceEquals(left, right) ? 0 : (left is null ? -1 : 1);
        }

        var leftParts = SplitRuns(left);
        var rightParts = SplitRuns(right);

        int shared = Math.Min(leftParts.Length, rightParts.Length);
        for (int i = 0; i < shared; i++)
        {
            string a = leftParts[i];
            string b = rightParts[i];

            if (IsNumeric(a) && IsNumeric(b))
            {
                // Compare digit runs numerically: strip leading zeros, then the
                // run with more digits is larger; if equal length, ordinal order
                // matches numeric order.
                string strippedA = StripZeros(a);
                string strippedB = StripZeros(b);

                int byValue = strippedA.Length.CompareTo(strippedB.Length);
                if (byValue == 0)
                {
                    byValue = string.CompareOrdinal(strippedA, strippedB);
                }

                if (byValue != 0)
                {
                    return byValue;
                }

                // Same numeric value: prefer the shorter raw run so "1" comes
                // before "01" deterministically.
                int byLength = a.Length.CompareTo(b.Length);
                if (byLength != 0)
                {
                    return byLength;
                }
            }
            else
            {
                int byText = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
                if (byText != 0)
                {
                    return byText;
                }
            }
        }

        // All shared runs were equal; the shorter string sorts first.
        return leftParts.Length.CompareTo(rightParts.Length);
    }

    /// <summary>
    /// Returns an <see cref="IComparer{T}"/> that orders strings naturally,
    /// convenient for <c>OrderBy</c> and <c>List.Sort</c>.
    /// </summary>
    /// <returns>A natural-order comparer.</returns>
    public static IComparer<string> Comparer() =>
        System.Collections.Generic.Comparer<string>.Create(Compare);

    /// <summary>
    /// Splits a string into alternating runs of digits and non-digits.
    /// </summary>
    /// <param name="input">The string to split.</param>
    /// <returns>Array of runs; never empty.</returns>
    private static string[] SplitRuns(string input)
    {
        var runs = new List<string>();
        int i = 0;
        while (i < input.Length)
        {
            bool digit = char.IsAsciiDigit(input[i]);
            int start = i;
            while (i < input.Length && char.IsAsciiDigit(input[i]) == digit)
            {
                i++;
            }

            runs.Add(input[start..i]);
        }

        return runs.ToArray();
    }

    /// <summary>Returns whether every character in <paramref name="run"/> is a digit.</summary>
    private static bool IsNumeric(string run)
    {
        if (run.Length == 0)
        {
            return false;
        }

        foreach (char c in run)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Removes leading zeros, keeping a single zero for the value 0.</summary>
    private static string StripZeros(string digits)
    {
        int firstNonZero = 0;
        while (firstNonZero < digits.Length - 1 && digits[firstNonZero] == '0')
        {
            firstNonZero++;
        }

        return digits[firstNonZero..];
    }
}
