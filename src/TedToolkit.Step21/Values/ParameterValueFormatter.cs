using System.Buffers;
using System.Globalization;

namespace TedToolkit.Step21;

/// <summary>Formats strong parameter values into deterministic ISO 10303-21 parameter spellings.</summary>
internal static class ParameterValueFormatter
{
    /// <summary>Formats one parameter using a model-owned entity-name lookup.</summary>
    /// <param name="value">The strong parameter value.</param>
    /// <param name="getEntityName">The lookup that resolves a retained entity to its structure-owned occurrence name.</param>
    /// <returns>The canonical parameter spelling.</returns>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    internal static string Format(ParameterValue value, Func<Entity, EntityInstanceName> getEntityName)
    {
        Guard.NotNull(value);
        Guard.NotNull(getEntityName);
        var builder = new Part21TextBuilder(int.MaxValue, static () => throw new OutOfMemoryException());
        Append(builder, value, getEntityName);
        return builder.ToString();
    }

    internal static void Append(
        Part21TextBuilder builder,
        ParameterValue value,
        Func<Entity, EntityInstanceName> getEntityName,
        Part21StringEncoding stringEncoding = Part21StringEncoding.Canonical)
    {
        Guard.NotNull(builder);
        Guard.NotNull(value);
        Guard.NotNull(getEntityName);
        switch (value.Kind)
        {
            case ParameterValueKind.Omitted:
                builder.Append('$');
                break;
            case ParameterValueKind.Derived:
                builder.Append('*');
                break;
            case ParameterValueKind.Integer:
                _ = value.TryGetInteger(out var integer);
                builder.AppendFormattable(integer);
                break;
            case ParameterValueKind.Real:
                AppendReal(builder, value);
                break;
            case ParameterValueKind.String:
                if (!value.TryGetString(out var text))
                    throw InconsistentValue(value);
                AppendString(builder, text, stringEncoding);
                break;
            case ParameterValueKind.Binary:
                AppendBinary(builder, value);
                break;
            case ParameterValueKind.Boolean:
                _ = value.TryGetBoolean(out var boolean);
                builder.Append(boolean ? ".T." : ".F.");
                break;
            case ParameterValueKind.Logical:
                AppendLogical(builder, value);
                break;
            case ParameterValueKind.Enumeration:
                if (!value.TryGetEnumeration(out var symbol))
                    throw InconsistentValue(value);
                builder.Append('.').Append(symbol).Append('.');
                break;
            case ParameterValueKind.Entity:
                if (!value.TryGetEntity(out var entity))
                    throw InconsistentValue(value);
                builder.Append('#').Append(getEntityName(entity).CanonicalDigits);
                break;
            case ParameterValueKind.EntityInstance:
                if (!value.TryGetEntityInstance(out var entityName))
                    throw InconsistentValue(value);
                builder.Append('#').Append(entityName.CanonicalDigits);
                break;
            case ParameterValueKind.ValueInstance:
                if (!value.TryGetValueInstance(out var valueName))
                    throw InconsistentValue(value);
                builder.Append('@').Append(valueName.CanonicalDigits);
                break;
            case ParameterValueKind.ConstantEntity:
                if (!value.TryGetConstantEntity(out var constantEntity))
                    throw InconsistentValue(value);
                builder.Append('#').Append(constantEntity.Value);
                break;
            case ParameterValueKind.ConstantValue:
                if (!value.TryGetConstantValue(out var constantValue))
                    throw InconsistentValue(value);
                builder.Append('@').Append(constantValue.Value);
                break;
            case ParameterValueKind.Resource:
                if (!value.TryGetResource(out var resource))
                    throw InconsistentValue(value);
                builder.Append('<').Append(resource.Value).Append('>');
                break;
            case ParameterValueKind.Aggregate:
                AppendAggregate(builder, value, getEntityName, stringEncoding);
                break;
            case ParameterValueKind.Typed:
                AppendTyped(builder, value, getEntityName, stringEncoding);
                break;
            default:
                throw new InvalidOperationException($"Unsupported parameter value kind '{value.Kind}'.");
        }
    }

    private static void AppendReal(Part21TextBuilder builder, ParameterValue value)
    {
        _ = value.TryGetReal(out var real);
        if (real.Significand.IsZero)
        {
            builder.Append("0.");
            return;
        }
        builder.AppendFormattable(real.Significand);
        builder.Append(".E");
        builder.AppendFormattable(real.Exponent);
    }

    internal static void AppendString(
        Part21TextBuilder builder,
        string text,
        Part21StringEncoding encoding)
    {
        Guard.NotNull(builder);
        Guard.NotNull(text);
        if (GetEncodedStringOctetCount(text, encoding) > Part21StringTokenLimits.MaximumStoredOctets)
            Part21StringTokenLimits.Throw();

        builder.Append('\'');
        var alphabet = 'A';
        var extended = ExtendedEncoding.None;
        var remaining = text.AsSpan();
        while (!remaining.IsEmpty)
        {
            if (UnicodeScalar.DecodeFromUtf16(remaining, out var rune, out var consumed) != OperationStatus.Done)
                Part21StringTokenLimits.ThrowInvalidUnicodeScalar();
            remaining = remaining[consumed..];

            var nextExtended = GetExtendedEncoding(rune, encoding);
            if (nextExtended != ExtendedEncoding.None)
            {
                if (extended != nextExtended)
                {
                    AppendExtendedEnd(builder, extended);
                    AppendExtendedStart(builder, nextExtended);
                    extended = nextExtended;
                }
                AppendExtendedValue(builder, rune, extended);
                continue;
            }

            AppendExtendedEnd(builder, extended);
            extended = ExtendedEncoding.None;
            switch (rune.Value)
            {
                case '\'':
                    builder.Append("''");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case >= 0x20 and <= 0x7E:
                    builder.Append((char)rune.Value);
                    break;
                default:
                    AppendEncodedRune(builder, rune, encoding, ref alphabet);
                    break;
            }
        }

        AppendExtendedEnd(builder, extended);
        builder.Append('\'');
    }

    private static int GetEncodedStringOctetCount(string text, Part21StringEncoding encoding)
    {
        var count = 2;
        var alphabet = 'A';
        var extended = ExtendedEncoding.None;
        var remaining = text.AsSpan();
        while (!remaining.IsEmpty)
        {
            if (UnicodeScalar.DecodeFromUtf16(remaining, out var rune, out var consumed) != OperationStatus.Done)
                Part21StringTokenLimits.ThrowInvalidUnicodeScalar();
            remaining = remaining[consumed..];

            var nextExtended = GetExtendedEncoding(rune, encoding);
            if (nextExtended != ExtendedEncoding.None)
            {
                if (extended != nextExtended)
                {
                    if (extended != ExtendedEncoding.None)
                        count += 4;
                    count += 4;
                    extended = nextExtended;
                }
                count += extended == ExtendedEncoding.X2 ? 4 * rune.Utf16SequenceLength : 8;
            }
            else
            {
                if (extended != ExtendedEncoding.None)
                {
                    count += 4;
                    extended = ExtendedEncoding.None;
                }
                count += rune.Value switch
                {
                    '\'' or '\\' => 2,
                    >= 0x20 and <= 0x7E => 1,
                    _ => GetEncodedRuneOctetCount(rune, encoding, ref alphabet),
                };
            }
            if (count > Part21StringTokenLimits.MaximumStoredOctets)
                return count;
        }
        if (extended != ExtendedEncoding.None)
            count += 4;
        return count;
    }

    private static int GetEncodedRuneOctetCount(UnicodeScalar rune, Part21StringEncoding encoding, ref char alphabet)
    {
        if (rune.Value is <= 0x1F or 0x7F)
            return 5;

        switch (encoding)
        {
            case Part21StringEncoding.Utf8 when rune.Value >= 0x20:
                return rune.Utf8SequenceLength;
            case Part21StringEncoding.X when rune.Value <= 0xFF:
                return 5;
            case Part21StringEncoding.Iso8859 when TryEncodeIso8859(rune, out var page, out _):
                var changesPage = alphabet != page;
                alphabet = page;
                return changesPage ? 8 : 4;
            case Part21StringEncoding.Iso8859 when rune.Value <= 0xFF:
                return 5;
            default:
                return 0;
        }
    }

    private static void AppendBinary(Part21TextBuilder builder, ParameterValue value)
    {
        if (!value.TryGetBinary(out var binary))
            throw InconsistentValue(value);

        var unusedBits = (4 - (binary.Length % 4)) % 4;
        builder.Append('\"');
        builder.AppendFormattable(unusedBits);
        var encodedLength = binary.Length + unusedBits;
        for (var encodedIndex = 0; encodedIndex < encodedLength; encodedIndex += 4)
        {
            var nibble = 0;
            for (var bit = 0; bit < 4; bit++)
            {
                var sourceIndex = encodedIndex + bit - unusedBits;
                nibble = nibble << 1 | (sourceIndex >= 0 && binary[sourceIndex] ? 1 : 0);
            }
            builder.Append("0123456789ABCDEF"[nibble]);
        }
        builder.Append('\"');
    }

    private static void AppendLogical(Part21TextBuilder builder, ParameterValue value)
    {
        _ = value.TryGetLogical(out var logical);
        builder.Append(logical switch
        {
            LogicalValue.False => ".F.",
            LogicalValue.Unknown => ".U.",
            LogicalValue.True => ".T.",
            _ => throw new InvalidOperationException($"Unsupported LOGICAL value '{logical}'."),
        });
    }

    private static void AppendAggregate(
        Part21TextBuilder builder,
        ParameterValue value,
        Func<Entity, EntityInstanceName> getEntityName,
        Part21StringEncoding stringEncoding)
    {
        if (!value.TryGetAggregate(out var elements))
            throw InconsistentValue(value);

        builder.Append('(');
        for (var index = 0; index < elements.Count; index++)
        {
            if (index > 0)
                builder.Append(',');
            Append(builder, elements[index], getEntityName, stringEncoding);
        }
        builder.Append(')');
    }

    private static void AppendTyped(
        Part21TextBuilder builder,
        ParameterValue value,
        Func<Entity, EntityInstanceName> getEntityName,
        Part21StringEncoding stringEncoding)
    {
        if (!value.TryGetTyped(out var typeName, out var inner))
            throw InconsistentValue(value);

        builder.Append(typeName).Append('(');
        Append(builder, inner, getEntityName, stringEncoding);
        builder.Append(')');
    }

    private static void AppendEncodedRune(
        Part21TextBuilder builder,
        UnicodeScalar rune,
        Part21StringEncoding encoding,
        ref char alphabet)
    {
        if (rune.Value is <= 0x1F or 0x7F)
        {
            AppendXEncodedRune(builder, rune);
            return;
        }

        switch (encoding)
        {
            case Part21StringEncoding.Utf8 when rune.Value >= 0x20:
                Span<char> direct = stackalloc char[2];
                builder.Append(direct[..rune.EncodeToUtf16(direct)]);
                return;
            case Part21StringEncoding.X when rune.Value <= 0xFF:
                builder.Append("\\X\\");
                builder.AppendFormattable(rune.Value, "X2");
                return;
            case Part21StringEncoding.Iso8859 when TryEncodeIso8859(rune, out var page, out var value):
                if (alphabet != page)
                {
                    builder.Append("\\P").Append(page).Append("\\");
                    alphabet = page;
                }
                builder.Append("\\S\\").Append((char)(value - 128));
                return;
            case Part21StringEncoding.Iso8859 when rune.Value <= 0xFF:
                AppendXEncodedRune(builder, rune);
                return;
            default:
                throw new ExchangeStructureCapabilityException([
                    new Step21Diagnostic(
                        "P21-CAP-STRING-ENCODING",
                        Step21DiagnosticSeverity.Error,
                        $"String encoding '{encoding}' cannot represent Unicode scalar U+{rune.Value:X4}."),
                ]);
        }
    }

    private static void AppendXEncodedRune(Part21TextBuilder builder, UnicodeScalar rune)
    {
        builder.Append("\\X\\");
        builder.AppendFormattable(rune.Value, "X2");
    }

    private static ExtendedEncoding GetExtendedEncoding(UnicodeScalar rune, Part21StringEncoding encoding)
    {
        if (rune.Value <= 0x7F)
            return ExtendedEncoding.None;

        return encoding switch
        {
            Part21StringEncoding.Canonical when rune.Value <= 0xFFFF => ExtendedEncoding.X2,
            Part21StringEncoding.Canonical => ExtendedEncoding.X4,
            Part21StringEncoding.X2 when rune.Value <= 0xFFFF => ExtendedEncoding.X2,
            Part21StringEncoding.X4 => ExtendedEncoding.X4,
            _ => ExtendedEncoding.None,
        };
    }

    private static void AppendExtendedStart(Part21TextBuilder builder, ExtendedEncoding encoding)
    {
        if (encoding == ExtendedEncoding.X2)
            builder.Append("\\X2\\");
        else if (encoding == ExtendedEncoding.X4)
            builder.Append("\\X4\\");
    }

    private static void AppendExtendedEnd(Part21TextBuilder builder, ExtendedEncoding encoding)
    {
        if (encoding != ExtendedEncoding.None)
            builder.Append("\\X0\\");
    }

    private static void AppendExtendedValue(
        Part21TextBuilder builder,
        UnicodeScalar rune,
        ExtendedEncoding encoding)
    {
        if (encoding == ExtendedEncoding.X4)
        {
            builder.AppendFormattable(rune.Value, "X8");
            return;
        }

        builder.AppendFormattable(rune.Value, "X4");
    }

    private enum ExtendedEncoding
    {
        None,
        X2,
        X4,
    }

    private static bool TryEncodeIso8859(UnicodeScalar rune, out char page, out byte value)
    {
        if (rune.Value is >= 0xA0 and <= 0xFE)
        {
            page = 'A';
            value = (byte)rune.Value;
            return true;
        }

        Span<char> characters = stackalloc char[2];
        var characterCount = rune.EncodeToUtf16(characters);
        Span<byte> bytes = stackalloc byte[4];
        for (var index = 1; index < 9; index++)
        {
            var encoding = Iso8859Encodings.Pages[index - 1];
            if (encoding is null)
                continue;
            try
            {
                var byteCount = EncodingCompat.GetBytes(encoding, characters[..characterCount], bytes);
                if (byteCount == 1 && bytes[0] is >= 0xA0 and <= 0xFE)
                {
                    page = (char)('A' + index);
                    value = bytes[0];
                    return true;
                }
            }
            catch (System.Text.EncoderFallbackException)
            {
            }
        }

        page = default;
        value = default;
        return false;
    }

    private static class Iso8859Encodings
    {
        internal static readonly System.Text.Encoding?[] Pages = Enumerable.Range(2, 8)
            .Select(page => System.Text.CodePagesEncodingProvider.Instance.GetEncoding(
                28590 + page,
                System.Text.EncoderFallback.ExceptionFallback,
                System.Text.DecoderFallback.ExceptionFallback))
            .ToArray();
    }

    private static InvalidOperationException InconsistentValue(ParameterValue value) =>
        new($"Parameter kind '{value.Kind}' does not contain its required payload.");
}
