using System.Buffers;
using System.Text;

namespace TedToolkit.Step21;

/// <summary>Owns the normative maximum stored size of one Part 21 STRING token.</summary>
internal static class Part21StringTokenLimits
{
    internal const int MaximumStoredOctets = 32769;

    internal static void Throw(SourceLocation? sourceLocation = null) =>
        throw new ExchangeStructureCapabilityException([
            new Step21Diagnostic(
                "P21-STRING-STORED-LENGTH",
                Step21DiagnosticSeverity.Error,
                $"A stored STRING token cannot exceed {MaximumStoredOctets} octets including its apostrophes.",
                sourceLocation),
        ]);

    internal static bool ContainsOnlyUnicodeScalars(string value) =>
        !TryGetInvalidUnicodeScalarIndex(value, out _);

    internal static bool TryGetInvalidUnicodeScalarIndex(string value, out int invalidIndex)
    {
        var remaining = value.AsSpan();
        var index = 0;
        while (!remaining.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(remaining, out _, out var consumed) != OperationStatus.Done)
            {
                invalidIndex = index;
                return true;
            }
            remaining = remaining[consumed..];
            index += consumed;
        }

        invalidIndex = -1;
        return false;
    }

    internal static void ThrowInvalidUnicodeScalar(SourceLocation? sourceLocation = null) =>
        throw new ExchangeStructureCapabilityException([
            new Step21Diagnostic(
                "P21-STRING-UNICODE-SCALAR",
                Step21DiagnosticSeverity.Error,
                "A STRING value contains an unpaired UTF-16 surrogate and cannot be encoded as a UCS scalar.",
                sourceLocation),
        ]);
}
