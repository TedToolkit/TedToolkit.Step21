using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TedToolkit.Step21.PackedConsumer;

/// <summary>
/// Independently compares the complete physical DATA graph of the fixed package fixtures.
/// It normalizes whitespace and equivalent decimal spellings, not instance identities or references.
/// </summary>
internal static class Part21FixtureSignature
{
    private static readonly Regex Tokens = new(
        "(?<text>'(?:''|[^'])*'|\"[^\"]*\")|(?<reference>#[0-9]+)|"
        + @"(?<enumeration>\.[A-Za-z_][A-Za-z_0-9]*\.)|"
        + @"(?<number>[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[Ee][+-]?[0-9]+)?)|"
        + @"(?<name>[A-Za-z_][A-Za-z_0-9]*)|(?<punctuation>[(),=$*;])|"
        + @"(?<comment>/\*[\s\S]*?\*/)|(?<space>\s+)|(?<invalid>.)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    internal static string Create(string source)
    {
        var start = source.IndexOf("DATA;", StringComparison.OrdinalIgnoreCase);
        var end = start < 0 ? -1 : source.IndexOf("ENDSEC;", start, StringComparison.OrdinalIgnoreCase);
        if (start < 0 || end < 0)
        {
            throw new InvalidOperationException("The fixed fixture must contain a complete DATA section.");
        }

        var result = new StringBuilder();
        foreach (Match token in Tokens.Matches(source.Substring(start + 5, end - start - 5)))
        {
            if (token.Groups["space"].Success || token.Groups["comment"].Success)
            {
                continue;
            }

            if (token.Groups["invalid"].Success)
            {
                throw new InvalidOperationException($"Unsupported token in fixed fixture: {token.Value}");
            }

            var value = token.Groups["number"].Success
                ? decimal.Parse(token.Value, NumberStyles.Float, CultureInfo.InvariantCulture)
                    .ToString("G29", CultureInfo.InvariantCulture)
                : token.Value;
            result.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
        }

        return result.ToString();
    }
}
