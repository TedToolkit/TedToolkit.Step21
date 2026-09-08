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
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(getEntityName);
        var builder = new Part21TextBuilder(int.MaxValue, static () => throw new OutOfMemoryException());
        Append(builder, value, getEntityName);
        return builder.ToString();
    }

    internal static void Append(
        Part21TextBuilder builder,
        ParameterValue value,
        Func<Entity, EntityInstanceName> getEntityName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(getEntityName);
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
                AppendString(builder, value);
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
                builder.Append(getEntityName(entity).ToString());
                break;
            case ParameterValueKind.EntityInstance:
                builder.Append(value.TryGetEntityInstance(out var entityName)
                    ? entityName.ToString() : throw InconsistentValue(value));
                break;
            case ParameterValueKind.ValueInstance:
                builder.Append(value.TryGetValueInstance(out var valueName)
                    ? valueName.ToString() : throw InconsistentValue(value));
                break;
            case ParameterValueKind.ConstantEntity:
                builder.Append(value.TryGetConstantEntity(out var constantEntity)
                    ? constantEntity.ToString() : throw InconsistentValue(value));
                break;
            case ParameterValueKind.ConstantValue:
                builder.Append(value.TryGetConstantValue(out var constantValue)
                    ? constantValue.ToString() : throw InconsistentValue(value));
                break;
            case ParameterValueKind.Resource:
                builder.Append(value.TryGetResource(out var resource)
                    ? resource.ToString() : throw InconsistentValue(value));
                break;
            case ParameterValueKind.Aggregate:
                AppendAggregate(builder, value, getEntityName);
                break;
            case ParameterValueKind.Typed:
                AppendTyped(builder, value, getEntityName);
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

    private static void AppendString(Part21TextBuilder builder, ParameterValue value)
    {
        if (!value.TryGetString(out var text))
            throw InconsistentValue(value);

        builder.Append('\'');
        foreach (var rune in text.EnumerateRunes())
        {
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
                case <= 0xFFFF:
                    builder.Append("\\X2\\");
                    builder.AppendFormattable(rune.Value, "X4");
                    builder.Append("\\X0\\");
                    break;
                default:
                    builder.Append("\\X4\\");
                    builder.AppendFormattable(rune.Value, "X8");
                    builder.Append("\\X0\\");
                    break;
            }
        }

        builder.Append('\'');
    }

    private static void AppendBinary(Part21TextBuilder builder, ParameterValue value)
    {
        if (!value.TryGetBinary(out var binary))
            throw InconsistentValue(value);

        var unusedBits = (4 - (binary.Length % 4)) % 4;
        builder.Append('\"');
        builder.AppendFormattable(unusedBits);
        for (var index = 0; index < binary.Length; index += 4)
        {
            var nibble = 0;
            for (var bit = 0; bit < 4; bit++)
                nibble = nibble << 1 | (index + bit < binary.Length && binary[index + bit] ? 1 : 0);
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
        Func<Entity, EntityInstanceName> getEntityName)
    {
        if (!value.TryGetAggregate(out var elements))
            throw InconsistentValue(value);

        builder.Append('(');
        for (var index = 0; index < elements.Count; index++)
        {
            if (index > 0)
                builder.Append(',');
            Append(builder, elements[index], getEntityName);
        }
        builder.Append(')');
    }

    private static void AppendTyped(
        Part21TextBuilder builder,
        ParameterValue value,
        Func<Entity, EntityInstanceName> getEntityName)
    {
        if (!value.TryGetTyped(out var typeName, out var inner))
            throw InconsistentValue(value);

        builder.Append(typeName).Append('(');
        Append(builder, inner, getEntityName);
        builder.Append(')');
    }

    private static InvalidOperationException InconsistentValue(ParameterValue value) =>
        new($"Parameter kind '{value.Kind}' does not contain its required payload.");
}
