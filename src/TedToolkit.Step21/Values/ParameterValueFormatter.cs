using System.Globalization;
using System.Text;

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
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(getEntityName);

        return value.Kind switch
        {
            ParameterValueKind.Omitted => "$",
            ParameterValueKind.Derived => "*",
            ParameterValueKind.Integer => FormatInteger(value),
            ParameterValueKind.Real => FormatReal(value),
            ParameterValueKind.String => FormatString(value),
            ParameterValueKind.Binary => FormatBinary(value),
            ParameterValueKind.Boolean => FormatBoolean(value),
            ParameterValueKind.Logical => FormatLogical(value),
            ParameterValueKind.Enumeration => FormatEnumeration(value),
            ParameterValueKind.Entity => FormatEntity(value, getEntityName),
            ParameterValueKind.EntityInstance => FormatEntityInstance(value),
            ParameterValueKind.ValueInstance => FormatValueInstance(value),
            ParameterValueKind.ConstantEntity => FormatConstantEntity(value),
            ParameterValueKind.ConstantValue => FormatConstantValue(value),
            ParameterValueKind.Resource => FormatResource(value),
            ParameterValueKind.Aggregate => FormatAggregate(value, getEntityName),
            ParameterValueKind.Typed => FormatTyped(value, getEntityName),
            _ => throw new InvalidOperationException($"Unsupported parameter value kind '{value.Kind}'."),
        };
    }

    private static string FormatInteger(ParameterValue value)
    {
        _ = value.TryGetInteger(out var integer);
        return integer.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatReal(ParameterValue value)
    {
        _ = value.TryGetReal(out var real);
        var significand = real.Significand.ToString(CultureInfo.InvariantCulture);
        if (real.Significand.IsZero)
            return "0.";

        var exponent = real.Exponent.ToString(CultureInfo.InvariantCulture);
        return string.Concat(significand, ".E", exponent);
    }

    private static string FormatString(ParameterValue value)
    {
        if (!value.TryGetString(out var text))
            throw InconsistentValue(value);

        var builder = new StringBuilder(text.Length + 2).Append('\'');
        foreach (var rune in text.EnumerateRunes())
        {
            switch (rune.Value)
            {
                case '\'':
                    _ = builder.Append("''");
                    break;
                case '\\':
                    _ = builder.Append("\\\\");
                    break;
                case >= 0x20 and <= 0x7E:
                    _ = builder.Append((char)rune.Value);
                    break;
                case <= 0xFFFF:
                    _ = builder.Append("\\X2\\")
                        .Append(rune.Value.ToString("X4", CultureInfo.InvariantCulture))
                        .Append("\\X0\\");
                    break;
                default:
                    _ = builder.Append("\\X4\\")
                        .Append(rune.Value.ToString("X8", CultureInfo.InvariantCulture))
                        .Append("\\X0\\");
                    break;
            }
        }

        return builder.Append('\'').ToString();
    }

    private static string FormatBinary(ParameterValue value)
    {
        if (!value.TryGetBinary(out var binary))
            throw InconsistentValue(value);

        var bits = binary.ToString();
        var unusedBits = (4 - (bits.Length % 4)) % 4;
        var paddedBits = bits.PadRight(bits.Length + unusedBits, '0');
        var builder = new StringBuilder((paddedBits.Length / 4) + 3)
            .Append('\"')
            .Append(unusedBits);
        for (var index = 0; index < paddedBits.Length; index += 4)
        {
            _ = builder.Append(Convert.ToInt32(paddedBits.Substring(index, 4), 2)
                .ToString("X", CultureInfo.InvariantCulture));
        }

        return builder.Append('\"').ToString();
    }

    private static string FormatBoolean(ParameterValue value)
    {
        _ = value.TryGetBoolean(out var boolean);
        return boolean ? ".T." : ".F.";
    }

    private static string FormatLogical(ParameterValue value)
    {
        _ = value.TryGetLogical(out var logical);
        return logical switch
        {
            LogicalValue.False => ".F.",
            LogicalValue.Unknown => ".U.",
            LogicalValue.True => ".T.",
            _ => throw new InvalidOperationException($"Unsupported LOGICAL value '{logical}'."),
        };
    }

    private static string FormatEnumeration(ParameterValue value)
    {
        _ = value.TryGetEnumeration(out var symbol);
        return string.Concat(".", symbol, ".");
    }

    private static string FormatEntity(
        ParameterValue value,
        Func<Entity, EntityInstanceName> getEntityName)
    {
        if (!value.TryGetEntity(out var entity))
            throw InconsistentValue(value);

        return getEntityName(entity).ToString();
    }

    private static string FormatAggregate(
        ParameterValue value,
        Func<Entity, EntityInstanceName> getEntityName)
    {
        if (!value.TryGetAggregate(out var elements))
            throw InconsistentValue(value);

        return string.Concat("(", string.Join(',', elements.Select(element => Format(element, getEntityName))), ")");
    }

    private static string FormatEntityInstance(ParameterValue value) =>
        value.TryGetEntityInstance(out var name) ? name.ToString() : throw InconsistentValue(value);

    private static string FormatValueInstance(ParameterValue value) =>
        value.TryGetValueInstance(out var name) ? name.ToString() : throw InconsistentValue(value);

    private static string FormatConstantEntity(ParameterValue value) =>
        value.TryGetConstantEntity(out var name) ? name.ToString() : throw InconsistentValue(value);

    private static string FormatConstantValue(ParameterValue value) =>
        value.TryGetConstantValue(out var name) ? name.ToString() : throw InconsistentValue(value);

    private static string FormatResource(ParameterValue value) =>
        value.TryGetResource(out var resource) ? resource.ToString() : throw InconsistentValue(value);

    private static string FormatTyped(
        ParameterValue value,
        Func<Entity, EntityInstanceName> getEntityName)
    {
        if (!value.TryGetTyped(out var typeName, out var inner))
            throw InconsistentValue(value);

        return string.Concat(typeName, "(", Format(inner, getEntityName), ")");
    }

    private static InvalidOperationException InconsistentValue(ParameterValue value) =>
        new($"Parameter kind '{value.Kind}' does not contain its required payload.");
}