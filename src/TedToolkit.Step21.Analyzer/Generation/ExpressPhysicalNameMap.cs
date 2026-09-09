// -----------------------------------------------------------------------
// <copyright file="ExpressPhysicalNameMap.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;
using System.Globalization;

using TedToolkit.Step21.Analyzer.Express;
using TedToolkit.Step21.Analyzer.Express.Analysis;
using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Resolves explicit schema-supplied Part 21 physical names without inventing abbreviations.
/// </summary>
internal sealed class ExpressPhysicalNameMap
{
    private readonly IReadOnlyDictionary<ExpressBoundSymbol, string> _entityNames;

    private readonly IReadOnlyDictionary<ExpressBoundSymbol, string> _typeNames;

    private readonly IReadOnlyDictionary<string, string> _enumerationNames;

    private ExpressPhysicalNameMap(
        IReadOnlyDictionary<ExpressBoundSymbol, string> entityNames,
        IReadOnlyDictionary<ExpressBoundSymbol, string> typeNames,
        IReadOnlyDictionary<string, string> enumerationNames,
        IEnumerable<ExpressPhysicalNameFailure> failures)
    {
        _entityNames = entityNames;
        _typeNames = typeNames;
        _enumerationNames = enumerationNames;
        Failures = new ReadOnlyCollection<ExpressPhysicalNameFailure>(failures.ToArray());
    }

    /// <summary>
    /// Gets source-located invalid mapping declarations.
    /// </summary>
    internal IReadOnlyList<ExpressPhysicalNameFailure> Failures { get; }

    /// <summary>
    /// Creates and validates the complete mapping inventory.
    /// </summary>
    /// <param name="inputs">The Roslyn additional-file snapshots.</param>
    /// <param name="compilation">The analyzed EXPRESS compilation.</param>
    /// <param name="resolver">The generated type resolver.</param>
    /// <returns>The validated mapping inventory and its failures.</returns>
    internal static ExpressPhysicalNameMap Create(
        IEnumerable<ExpressGeneratorInput> inputs,
        ExpressAnalyzedCompilation compilation,
        ExpressGeneratedTypeResolver resolver)
    {
        var failures = new List<ExpressPhysicalNameFailure>();
        var rows = Parse(inputs, failures);
        var schemas = compilation.Schemas.ToDictionary(schema => schema.Name, StringComparer.OrdinalIgnoreCase);
        var entityNames = new Dictionary<ExpressBoundSymbol, string>();
        var typeNames = new Dictionary<ExpressBoundSymbol, string>();
        var enumerationNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in rows.GroupBy(row => row.SchemaName, StringComparer.OrdinalIgnoreCase))
        {
            var headers = group.Where(row => row.Kind == PhysicalNameKind.Schema).ToArray();
            var first = headers[0];
            if (headers.Length > 1)
            {
                var duplicate = headers[1];
                failures.Add(Failure(duplicate,
                    $"Schema '{group.Key}' already has a physical-name map at {first.Location.FilePath}:"
                    + first.Location.Line.ToString(CultureInfo.InvariantCulture) + "."));
                continue;
            }

            if (!schemas.TryGetValue(group.Key, out var schema))
            {
                failures.Add(Failure(first, $"Schema '{group.Key}' is not present in the supplied EXPRESS inputs."));
                continue;
            }

            var physicalTypeNames = new Dictionary<string, PhysicalNameRow>(StringComparer.OrdinalIgnoreCase);
            var declarations = schema.Declarations.ToDictionary(
                declaration => declaration.Name,
                StringComparer.OrdinalIgnoreCase);
            foreach (var row in group.Where(row => row.Kind != PhysicalNameKind.Schema))
            {
                if (!declarations.TryGetValue(row.LongName, out var declaration))
                {
                    failures.Add(Failure(row,
                        $"{row.KindName} name '{row.LongName}' is not declared by schema '{schema.Name}'."));
                    continue;
                }

                switch (row.Kind)
                {
                    case PhysicalNameKind.Entity:
                        if (declaration is not ExpressBoundEntity)
                        {
                            failures.Add(Failure(row, $"'{row.LongName}' is not an ENTITY declaration."));
                            continue;
                        }

                        AddPhysicalName(
                            row,
                            declaration.Symbol,
                            declarations.Values,
                            entityNames,
                            physicalTypeNames,
                            failures);
                        break;

                    case PhysicalNameKind.Type:
                        if (declaration is not ExpressBoundDefinedType physicalType)
                        {
                            failures.Add(Failure(row, $"'{row.LongName}' is not a TYPE declaration."));
                            continue;
                        }

                        var terminalType = ExpressDescriptorTypeSupport.GetTerminalType(
                            physicalType.UnderlyingType,
                            resolver);
                        if (terminalType is not ExpressBoundScalarType and not ExpressBoundEnumerationType)
                        {
                            failures.Add(Failure(row,
                                $"TYPE '{row.LongName}' is not a simple defined or enumeration type encoded as a "
                                + "typed-parameter keyword."));
                            continue;
                        }

                        AddPhysicalName(
                            row,
                            declaration.Symbol,
                            declarations.Values,
                            typeNames,
                            physicalTypeNames,
                            failures);
                        break;

                    case PhysicalNameKind.Enumeration:
                        if (declaration is not ExpressBoundDefinedType enumerationDeclaration
                            || enumerationDeclaration.UnderlyingType is not ExpressBoundEnumerationType enumeration)
                        {
                            failures.Add(Failure(row, $"'{row.LongName}' is not an enumeration TYPE declaration."));
                            continue;
                        }

                        var value = resolver.GetEnumerationValues(enumeration).FirstOrDefault(candidate =>
                            StringComparer.OrdinalIgnoreCase.Equals(candidate, row.EnumerationValue));
                        if (value is null)
                        {
                            failures.Add(Failure(row,
                                $"Enumeration value '{row.EnumerationValue}' is not declared by TYPE '{row.LongName}'."));
                            continue;
                        }

                        AddEnumerationName(
                            row,
                            enumerationDeclaration.Symbol,
                            value,
                            resolver.GetEnumerationValues(enumeration),
                            enumerationNames,
                            failures);
                        break;
                }
            }
        }

        return new(entityNames, typeNames, enumerationNames, failures);
    }

    /// <summary>
    /// Gets the canonical physical entity name.
    /// </summary>
    /// <param name="symbol">The bound entity symbol.</param>
    /// <returns>The explicit physical name or the canonical long name.</returns>
    internal string EntityName(ExpressBoundSymbol symbol)
    {
        return _entityNames.TryGetValue(symbol, out var physicalName)
            ? physicalName
            : symbol.Name.ToUpperInvariant();
    }

    /// <summary>
    /// Gets the canonical physical defined-type name.
    /// </summary>
    /// <param name="symbol">The bound defined-type symbol.</param>
    /// <returns>The explicit physical name or the canonical long name.</returns>
    internal string TypeName(ExpressBoundSymbol symbol)
    {
        return _typeNames.TryGetValue(symbol, out var physicalName)
            ? physicalName
            : symbol.Name.ToUpperInvariant();
    }

    /// <summary>
    /// Gets the canonical physical enumeration value.
    /// </summary>
    /// <param name="symbol">The bound enumeration type symbol.</param>
    /// <param name="value">The EXPRESS enumeration value.</param>
    /// <returns>The explicit physical name or the canonical long value.</returns>
    internal string EnumerationName(ExpressBoundSymbol symbol, string value)
    {
        return _enumerationNames.TryGetValue(EnumerationKey(symbol, value), out var physicalName)
            ? physicalName
            : value.ToUpperInvariant();
    }

    private static List<PhysicalNameRow> Parse(
        IEnumerable<ExpressGeneratorInput> inputs,
        List<ExpressPhysicalNameFailure> failures)
    {
        var rows = new List<PhysicalNameRow>();
        foreach (var input in inputs.Where(input => string.Equals(
                     Path.GetExtension(input.Path),
                     ".p21map",
                     StringComparison.OrdinalIgnoreCase)).OrderBy(input => input.Path, StringComparer.Ordinal))
        {
            if (input.Text is null)
            {
                failures.Add(new(input.Path, 1, 1, $"Roslyn could not read physical-name map '{input.Path}'."));
                continue;
            }

            ParseInput(input, rows, failures);
        }

        return rows;
    }

    private static void ParseInput(
        in ExpressGeneratorInput input,
        List<PhysicalNameRow> rows,
        List<ExpressPhysicalNameFailure> failures)
    {
        string? schemaName = null;
        var ended = false;
        var lines = input.Text!.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var content = lines[index].Split('#')[0].Trim();
            if (content.EndsWith(";", StringComparison.Ordinal))
            {
                content = content.Substring(0, content.Length - 1).TrimEnd();
            }

            if (content.Length == 0)
            {
                continue;
            }

            var line = index + 1;
            var tokens = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var keyword = tokens[0].ToUpperInvariant();
            if (ended)
            {
                failures.Add(new(input.Path, line, 1, "No declarations are permitted after END_SCHEMA."));
                continue;
            }

            if (keyword == "SCHEMA")
            {
                if (tokens.Length != 2 || schemaName is not null || !IsIdentifier(tokens.ElementAtOrDefault(1)))
                {
                    failures.Add(new(input.Path, line, 1, "Expected one 'SCHEMA <identifier>' header."));
                    continue;
                }

                schemaName = tokens[1];
                rows.Add(new(PhysicalNameKind.Schema, schemaName, schemaName, "", "",
                    new(input.Path, line, 1)));
                continue;
            }

            if (keyword == "END_SCHEMA")
            {
                if (tokens.Length != 1 || schemaName is null)
                {
                    failures.Add(new(input.Path, line, 1, "END_SCHEMA must close one physical-name map."));
                    continue;
                }

                ended = true;
                continue;
            }

            if (schemaName is null)
            {
                failures.Add(new(input.Path, line, 1, "A SCHEMA header must precede mapping declarations."));
                continue;
            }

            if (!TryParseRow(tokens, schemaName, input.Path, line, out var row, out var message))
            {
                failures.Add(new(input.Path, line, 1, message));
                continue;
            }

            rows.Add(row);
        }

        if (schemaName is null)
        {
            failures.Add(new(input.Path, 1, 1, "The physical-name map requires a SCHEMA header."));
        }
        else if (!ended)
        {
            failures.Add(new(input.Path, lines.Length, 1, "The physical-name map requires END_SCHEMA."));
        }
    }

    private static bool TryParseRow(
        string[] tokens,
        string schemaName,
        string path,
        int line,
        out PhysicalNameRow row,
        out string message)
    {
        var keyword = tokens[0].ToUpperInvariant();
        var kind = keyword switch
        {
            "ENTITY" => PhysicalNameKind.Entity,
            "TYPE" => PhysicalNameKind.Type,
            "ENUMERATION" => PhysicalNameKind.Enumeration,
            _ => PhysicalNameKind.Unknown,
        };
        var count = kind == PhysicalNameKind.Enumeration ? 4 : 3;
        if (kind == PhysicalNameKind.Unknown || tokens.Length != count
            || tokens.Skip(1).Any(token => !IsIdentifier(token)))
        {
            row = null!;
            message = "Expected 'ENTITY <long> <short>', 'TYPE <long> <short>', or "
                + "'ENUMERATION <type> <long-value> <short>'.";
            return false;
        }

        row = new(
            kind,
            schemaName,
            tokens[1],
            kind == PhysicalNameKind.Enumeration ? tokens[2] : "",
            tokens[count - 1].ToUpperInvariant(),
            new(path, line, 1));
        message = "";
        return true;
    }

    private static void AddPhysicalName(
        PhysicalNameRow row,
        ExpressBoundSymbol symbol,
        IEnumerable<ExpressBoundDeclaration> declarations,
        Dictionary<ExpressBoundSymbol, string> target,
        Dictionary<string, PhysicalNameRow> occupied,
        List<ExpressPhysicalNameFailure> failures)
    {
        if (target.ContainsKey(symbol))
        {
            failures.Add(Failure(row, $"'{row.LongName}' already has a physical-name mapping."));
            return;
        }

        var longName = symbol.Name.ToUpperInvariant();
        if (StringComparer.OrdinalIgnoreCase.Equals(longName, row.ShortName))
        {
            failures.Add(Failure(row, $"Physical name '{row.ShortName}' must differ from long name '{longName}'."));
            return;
        }

        if (declarations.Any(declaration => !ReferenceEquals(declaration.Symbol, symbol)
                && declaration is ExpressBoundEntity or ExpressBoundDefinedType
                && StringComparer.OrdinalIgnoreCase.Equals(declaration.Name, row.ShortName)))
        {
            failures.Add(Failure(row,
                $"Physical name '{row.ShortName}' collides with a declared ENTITY or TYPE long name."));
            return;
        }

        if (occupied.TryGetValue(row.ShortName, out var prior))
        {
            failures.Add(Failure(row,
                $"Physical name '{row.ShortName}' collides with '{prior.LongName}' at {prior.Location.FilePath}:"
                + prior.Location.Line.ToString(CultureInfo.InvariantCulture) + "."));
            return;
        }

        if (occupied.TryGetValue(longName, out prior))
        {
            failures.Add(Failure(row,
                $"Long name '{longName}' collides with physical name declared at {prior.Location.FilePath}:"
                + prior.Location.Line.ToString(CultureInfo.InvariantCulture) + "."));
            return;
        }

        occupied.Add(longName, row);
        occupied.Add(row.ShortName, row);
        target.Add(symbol, row.ShortName);
    }

    private static void AddEnumerationName(
        PhysicalNameRow row,
        ExpressBoundSymbol symbol,
        string value,
        IEnumerable<string> declaredValues,
        Dictionary<string, string> target,
        List<ExpressPhysicalNameFailure> failures)
    {
        var key = EnumerationKey(symbol, value);
        if (target.ContainsKey(key))
        {
            failures.Add(Failure(row,
                $"Enumeration value '{value}' already has a physical-name mapping for TYPE '{symbol.Name}'."));
            return;
        }

        var values = target.Where(pair => pair.Key.StartsWith(
                EnumerationPrefix(symbol),
                StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Value)
            .Concat(declaredValues);
        if (values.Any(candidate => StringComparer.OrdinalIgnoreCase.Equals(candidate, row.ShortName))
            || StringComparer.OrdinalIgnoreCase.Equals(value, row.ShortName))
        {
            failures.Add(Failure(row,
                $"Enumeration physical name '{row.ShortName}' is ambiguous within TYPE '{symbol.Name}'."));
            return;
        }

        target.Add(key, row.ShortName);
    }

    private static string EnumerationKey(ExpressBoundSymbol symbol, string value)
    {
        return EnumerationPrefix(symbol) + value;
    }

    private static string EnumerationPrefix(ExpressBoundSymbol symbol)
    {
        return symbol.DeclaringSchema.Name + "\0" + symbol.Name + "\0";
    }

    private static ExpressPhysicalNameFailure Failure(PhysicalNameRow row, string message)
    {
        return new(row.Location.FilePath, row.Location.Line, row.Location.Column, message);
    }

    private static bool IsIdentifier(string? value)
    {
        return value?.Length > 0
            && (IsAsciiLetter(value[0]) || value[0] == '_')
            && value.Skip(1).All(character => IsAsciiLetter(character)
                || character is >= '0' and <= '9'
                || character == '_');
    }

    private static bool IsAsciiLetter(char value)
    {
        return value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    }

    private enum PhysicalNameKind
    {
        Unknown = 0,

        Schema = 1,

        Entity = 2,

        Type = 3,

        Enumeration = 4,
    }

    private sealed class PhysicalNameRow
    {
        internal PhysicalNameRow(
            PhysicalNameKind kind,
            string schemaName,
            string longName,
            string enumerationValue,
            string shortName,
            ExpressSourceLocation location)
        {
            Kind = kind;
            SchemaName = schemaName;
            LongName = longName;
            EnumerationValue = enumerationValue;
            ShortName = shortName;
            Location = location;
        }

        internal PhysicalNameKind Kind { get; }

        internal string SchemaName { get; }

        internal string LongName { get; }

        internal string EnumerationValue { get; }

        internal string ShortName { get; }

        internal ExpressSourceLocation Location { get; }

        internal string KindName
        {
            get
            {
                return Kind switch
                {
                    PhysicalNameKind.Entity => "ENTITY",
                    PhysicalNameKind.Type => "TYPE",
                    PhysicalNameKind.Enumeration => "ENUMERATION",
                    _ => "MAPPING",
                };
            }
        }
    }
}