// -----------------------------------------------------------------------
// <copyright file="ExpressExpressionEmitter.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;
using System.Numerics;
using System.Text;

using TedToolkit.Step21.Analyzer.Express.Analysis;
using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Composes strongly typed static C# leaves from immutable bound EXPRESS expressions.
/// </summary>
internal static class ExpressExpressionEmitter
{
    /// <summary>
    /// Emits one expression that requires no schema declaration references.
    /// </summary>
    /// <param name="expression">The typed bound expression.</param>
    /// <returns>The generated C# expression and result type.</returns>
    internal static ExpressGeneratedExpression Emit(ExpressBoundExpression expression)
    {
        return Emit(expression, new ExpressExpressionEmissionContext(
            static (reference, _, _, _) => throw new InvalidOperationException(
                $"Reference '{reference.Name}' requires a schema generation context.")));
    }

    /// <summary>
    /// Emits one expression using a caller-owned static C# spelling for declaration references.
    /// </summary>
    /// <param name="expression">The typed bound expression.</param>
    /// <param name="resolveReference">Maps a resolved EXPRESS value or callable to static C#.</param>
    /// <returns>The generated C# expression and result type.</returns>
    internal static ExpressGeneratedExpression Emit(
        ExpressBoundExpression expression,
        Func<ExpressBoundName, string> resolveReference)
    {
        return Emit(expression, new ExpressExpressionEmissionContext(
            (reference, _, _, _) => resolveReference(reference)));
    }

    /// <summary>
    /// Emits one expression using all static spellings supplied by its enclosing generated operation.
    /// </summary>
    /// <param name="expression">The typed bound expression.</param>
    /// <param name="context">The immutable surrounding emission context.</param>
    /// <returns>The generated C# expression and result type.</returns>
    internal static ExpressGeneratedExpression Emit(
        ExpressBoundExpression expression,
        ExpressExpressionEmissionContext context)
    {
        try
        {
            var typeName = expression.Type.Kind == ExpressExpressionTypeKind.Generic
                && expression.Type.DeclaredType is ExpressBoundGenericType { TypeLabel: { } typeLabel, } generic
                && context.GenericTypeLabels.Contains(typeLabel, StringComparer.OrdinalIgnoreCase)
                    ? BoundTypeName(generic)
                    : TypeName(expression.Type, context);
            return new(EmitCode(expression, context), typeName);
        }
        catch (InvalidOperationException exception) when (!IsSourceLocated(expression, exception))
        {
            throw new InvalidOperationException(GenerationError(expression, exception.Message).Message, exception);
        }
    }

    private static bool IsSourceLocated(
        ExpressBoundExpression expression,
        InvalidOperationException exception)
    {
        var location = expression.Span.Start;
        var prefix = $"{location.FilePath}:{location.Line.ToString(CultureInfo.InvariantCulture)}:"
            + $"{location.Column.ToString(CultureInfo.InvariantCulture)}:";
        return exception.Message.StartsWith(prefix, StringComparison.Ordinal);
    }

    private static string EmitCode(
        ExpressBoundExpression expression,
        ExpressExpressionEmissionContext context)
    {
        return expression.Kind switch
        {
            ExpressExpressionKind.Literal => EmitLiteral(expression),
            ExpressExpressionKind.Indeterminate => "null",
            ExpressExpressionKind.Reference => EmitReference(expression, context),
            ExpressExpressionKind.Application => EmitApplication(expression, context),
            ExpressExpressionKind.Unary => EmitUnary(expression, context),
            ExpressExpressionKind.Binary => EmitBinary(expression, context),
            ExpressExpressionKind.AggregateInitializer => EmitAggregate(expression, context),
            ExpressExpressionKind.Repetition => throw new InvalidOperationException(
                "A repetition node is emitted only within its aggregate initializer."),
            ExpressExpressionKind.Interval => EmitInterval(expression, context),
            ExpressExpressionKind.Query => EmitQuery(expression, context),
            ExpressExpressionKind.AttributeQualifier => EmitAttribute(expression, context),
            ExpressExpressionKind.GroupQualifier => EmitGroup(expression, context),
            ExpressExpressionKind.IndexQualifier => EmitIndex(expression, context),
            ExpressExpressionKind.SliceQualifier => EmitSlice(expression, context),
            _ => throw new InvalidOperationException(
                $"Expression '{expression.SourceText}' requires a schema generation context."),
        };
    }

    private static string EmitLiteral(ExpressBoundExpression expression)
    {
        return expression.Type.Kind switch
        {
            ExpressExpressionTypeKind.Integer =>
                $"global::System.Numerics.BigInteger.Parse(\"{expression.SourceText}\", global::System.Globalization.CultureInfo.InvariantCulture)",
            ExpressExpressionTypeKind.Real => EmitReal(expression.SourceText),
            ExpressExpressionTypeKind.Boolean => string.Equals(
                expression.SourceText,
                "TRUE",
                StringComparison.OrdinalIgnoreCase) ? "true" : "false",
            ExpressExpressionTypeKind.Logical => "global::TedToolkit.Step21.LogicalValue.Unknown",
            ExpressExpressionTypeKind.String => Literal(DecodeString(expression)),
            ExpressExpressionTypeKind.Binary =>
                $"new global::TedToolkit.Step21.BinaryValue({Literal(expression.SourceText.Substring(1))})",
            _ => throw new InvalidOperationException(
                $"Literal '{expression.SourceText}' has no generated static representation."),
        };
    }

    private static string EmitReal(string source)
    {
        var exponentMarker = source.IndexOfAny(['E', 'e',]);
        var significandText = exponentMarker < 0 ? source : source.Substring(0, exponentMarker);
        var exponentText = exponentMarker < 0 ? "0" : source.Substring(exponentMarker + 1);
        var point = significandText.IndexOf('.');
        var fractionalDigits = point < 0 ? 0 : significandText.Length - point - 1;
        var digits = point < 0 ? significandText : significandText.Remove(point, 1);
        var significand = BigInteger.Parse(digits, CultureInfo.InvariantCulture);
        var exponent = BigInteger.Parse(exponentText, CultureInfo.InvariantCulture) - fractionalDigits;
        return "new global::TedToolkit.Step21.RealValue("
            + $"global::System.Numerics.BigInteger.Parse(\"{significand.ToString(CultureInfo.InvariantCulture)}\", "
            + "global::System.Globalization.CultureInfo.InvariantCulture), "
            + $"global::System.Numerics.BigInteger.Parse(\"{exponent.ToString(CultureInfo.InvariantCulture)}\", "
            + "global::System.Globalization.CultureInfo.InvariantCulture))";
    }

    private static string EmitUnary(
        ExpressBoundExpression expression,
        ExpressExpressionEmissionContext context)
    {
        var operand = expression.Children.Single();
        var code = EmitCode(operand, context);
        if (expression.Operation?.ToUpperInvariant() is "+" or "-"
            && RequiresSelectScalarProjection(operand, context)
            && context.ResolveModelFunction is not null)
        {
            if (operand.Type.CanBeIndeterminate)
            {
                return GuardIndeterminate(
                    expression,
                    context,
                    [operand,],
                    [code,],
                    codes => context.ResolveModelFunction(
                        "NUMERIC_SELECT_UNARY",
                        expression,
                        codes));
            }

            return context.ResolveModelFunction("NUMERIC_SELECT_UNARY", expression, [code,]);
        }

        if (expression.Operation?.ToUpperInvariant() != "NOT" && operand.Type.CanBeIndeterminate)
        {
            return GuardIndeterminate(
                expression,
                context,
                [operand,],
                [code,],
                codes => EmitUnaryCore(expression, operand, codes[0]));
        }

        return EmitUnaryCore(expression, operand, code);
    }

    private static string EmitUnaryCore(
        ExpressBoundExpression expression,
        ExpressBoundExpression operand,
        string code)
    {
        return expression.Operation?.ToUpperInvariant() switch
        {
            "+" => $"(+({code}))",
            "-" => $"(-({code}))",
            "NOT" => EmitNot(AsLogical(operand, code)),
            _ => throw new InvalidOperationException($"Unknown unary operation '{expression.Operation}'."),
        };
    }

    private static string EmitBinary(
        ExpressBoundExpression expression,
        ExpressExpressionEmissionContext context)
    {
        if (expression.Operation == "||"
            && expression.Children.All(child => child.Type.Kind == ExpressExpressionTypeKind.Entity)
            && context.ResolveModelFunction is not null)
        {
            var components = new List<ExpressBoundExpression>() { expression, };
            for (var index = 0; index < components.Count; index++)
            {
                var component = components[index];
                if (component.Kind == ExpressExpressionKind.Binary
                    && component.Operation == "||"
                    && component.Children.All(child => child.Type.Kind == ExpressExpressionTypeKind.Entity))
                {
                    components.RemoveAt(index);
                    components.InsertRange(index, component.Children);
                    index--;
                }
            }

            var componentArguments = components.SelectMany(component =>
                    component.Kind == ExpressExpressionKind.Application
                    && component.Reference?.Kind == ExpressBoundNameKind.Entity
                        ? component.Children
                        : [component,])
                .ToArray();
            var arguments = componentArguments
                .Select(argument => EmitCode(argument, context))
                .ToArray();
            return GuardIndeterminate(
                expression,
                context,
                componentArguments,
                arguments,
                codes => context.ResolveModelFunction("COMPLEX_CONSTRUCTOR", expression, codes));
        }

        var left = expression.Children[0];
        var right = expression.Children[1];
        var leftCode = EmitCode(left, context);
        var rightCode = EmitCode(right, context);
        var operation = expression.Operation?.ToUpperInvariant();
        if (operation is not ("AND" or "OR" or "XOR")
            && (MayEmitIndeterminate(left, context) || MayEmitIndeterminate(right, context)))
        {
            var guardedResultType = operation switch
            {
                "+" or "||" when left.Type.Kind == ExpressExpressionTypeKind.Binary =>
                    "global::TedToolkit.Step21.BinaryValue",
                "+" when (left.Type.Kind == ExpressExpressionTypeKind.String
                    || right.Type.Kind == ExpressExpressionTypeKind.String)
                    && left.Type.Kind != ExpressExpressionTypeKind.Aggregate
                    && right.Type.Kind != ExpressExpressionTypeKind.Aggregate =>
                    "global::System.String",
                "**" when left.Type.Kind != ExpressExpressionTypeKind.Integer =>
                    "global::TedToolkit.Step21.RealValue",
                _ => null,
            };
            return GuardIndeterminate(
                expression,
                context,
                [left, right,],
                [leftCode, rightCode,],
                codes => EmitBinaryCore(expression, operation, left, codes[0], right, codes[1], context),
                guardedResultType);
        }

        return EmitBinaryCore(expression, operation, left, leftCode, right, rightCode, context);
    }

    private static string EmitBinaryCore(
        ExpressBoundExpression expression,
        string? operation,
        ExpressBoundExpression left,
        string leftCode,
        ExpressBoundExpression right,
        string rightCode,
        ExpressExpressionEmissionContext context)
    {
        return operation switch
        {
            "+" when left.Type.Kind == ExpressExpressionTypeKind.Binary =>
                $"new global::TedToolkit.Step21.BinaryValue(global::System.String.Concat(({leftCode}).ToString(), ({rightCode}).ToString()))",
            "+" or "-" or "*" when left.Type.Kind == ExpressExpressionTypeKind.Aggregate
                || right.Type.Kind == ExpressExpressionTypeKind.Aggregate =>
                EmitAggregateBinary(expression, operation, left, leftCode, right, rightCode, context),
            "+" or "-" or "*" or "/" or "DIV" or "MOD"
                when RequiresSelectScalarProjection(left, context)
                    || RequiresSelectScalarProjection(right, context) =>
                context.ResolveModelFunction?.Invoke(
                    "NUMERIC_SELECT_BINARY",
                    expression,
                    [leftCode, rightCode,])
                ?? throw GenerationError(
                    expression,
                    "SELECT arithmetic requires an enclosing generated schema operation."),
            "+" or "-" or "*" or "/" => EmitNumericBinary(
                expression,
                operation,
                left,
                leftCode,
                right,
                rightCode,
                ResolveNarrowedScalarKind(left, context),
                ResolveNarrowedScalarKind(right, context)),
            "DIV" => EmitIntegerDivision(expression, left, leftCode, right, rightCode, modulo: false),
            "MOD" => EmitIntegerDivision(expression, left, leftCode, right, rightCode, modulo: true),
            "**" when left.Type.Kind == ExpressExpressionTypeKind.Integer =>
                EmitIntegerPower(expression, leftCode, right, rightCode),
            "**" => RealMath(expression, "Pow", context, (left, leftCode), (right, rightCode)),
            "||" when left.Type.Kind == ExpressExpressionTypeKind.Binary =>
                $"new global::TedToolkit.Step21.BinaryValue(global::System.String.Concat(({leftCode}).ToString(), ({rightCode}).ToString()))",
            "||" when left.Type.Kind == ExpressExpressionTypeKind.Entity
                && right.Type.Kind == ExpressExpressionTypeKind.Entity
                && context.ResolveModelFunction is not null =>
                context.ResolveModelFunction("COMPLEX_CONSTRUCTOR", expression, [leftCode, rightCode,]),
            "||" when left.Type.Kind == ExpressExpressionTypeKind.Entity
                && right.Type.Kind == ExpressExpressionTypeKind.Entity => throw GenerationError(
                    expression,
                    "Complex entity construction requires an enclosing generated model operation."),
            "||" => $"global::System.String.Concat(({leftCode}), ({rightCode}))",
            "AND" => EmitLogicalBinary("AND", AsLogical(left, leftCode), AsLogical(right, rightCode)),
            "OR" => EmitLogicalBinary("OR", AsLogical(left, leftCode), AsLogical(right, rightCode)),
            "XOR" => EmitLogicalBinary("XOR", AsLogical(left, leftCode), AsLogical(right, rightCode)),
            "=" => EmitValueComparison(expression, left, leftCode, right, rightCode, context, negated: false),
            "<>" => EmitValueComparison(expression, left, leftCode, right, rightCode, context, negated: true),
            ":=:" => LogicalComparison(InstanceEquality(
                expression,
                left,
                leftCode,
                right,
                rightCode,
                context)),
            ":<>:" => LogicalComparison(
                $"!({InstanceEquality(expression, left, leftCode, right, rightCode, context)})"),
            "<=" or ">=" when left.Type.Kind == ExpressExpressionTypeKind.Aggregate =>
                LogicalComparison(AggregateSubset(operation, leftCode, rightCode)),
            "<" or "<=" or ">" or ">="
                when RequiresSelectScalarProjection(left, context)
                    || RequiresSelectScalarProjection(right, context) =>
                context.ResolveModelFunction?.Invoke(
                    "ORDERED_SELECT_COMPARISON",
                    expression,
                    [leftCode, rightCode,])
                ?? throw GenerationError(
                    expression,
                    "SELECT ordering requires an enclosing generated schema operation."),
            "<" or "<=" or ">" or ">=" => LogicalComparison(
                OrderedComparison(operation, left, leftCode, right, rightCode, context)),
            "IN" => EmitMembership(expression, left, leftCode, right, rightCode, context),
            "LIKE" when RequiresSelectScalarProjection(left, context)
                || RequiresSelectScalarProjection(right, context) =>
                context.ResolveModelFunction?.Invoke(
                    "SELECT_LIKE",
                    expression,
                    [leftCode, rightCode,])
                ?? throw GenerationError(
                    expression,
                    "SELECT LIKE requires an enclosing generated schema operation."),
            "LIKE" => LogicalComparison(LikeMatch(leftCode, rightCode)),
            _ => throw new InvalidOperationException($"Operation '{expression.Operation}' is not statically generated."),
        };
    }

    private static bool RequiresSelectScalarProjection(
        ExpressBoundExpression expression,
        ExpressExpressionEmissionContext context)
    {
        var lexicalType = expression.Reference is { } resolvedReference
            ? context.ResolveLexicalBound?.Invoke(resolvedReference.Name)?.Type
            : null;
        var indexedElementType = expression.Kind == ExpressExpressionKind.IndexQualifier
            && expression.Children.Count > 0
            && expression.Children[0].Type.DeclaredType is { } indexedSource
                ? context.ResolveAggregateType?.Invoke(indexedSource)?.ElementType
                : null;
        var isSelect = expression.Type.Kind == ExpressExpressionTypeKind.Select
            || (expression.Type.DeclaredType is ExpressBoundNamedType named
                && named.Declaration.Kind != ExpressDeclarationKind.Entity
                && context.IsSelectValueType?.Invoke(named) == true)
            || (lexicalType is not null && context.IsSelectValueType?.Invoke(lexicalType) == true)
            || (indexedElementType is not null
                && context.IsSelectValueType?.Invoke(indexedElementType) == true);
        return isSelect
            && lexicalType is not ExpressBoundScalarType;
    }

    private static bool HasSelectedAggregateCarrier(
        ExpressBoundExpression expression,
        ExpressExpressionEmissionContext context)
    {
        ExpressBoundType?[] candidates =
        {
            expression.Type.DeclaredType,
            expression.Reference?.Attribute?.Type,
            expression.Reference?.Type,
        };
        return candidates.Any(candidate => candidate is not null
            && context.IsSelectValueType?.Invoke(candidate) == true);
    }

    private static ExpressExpressionTypeKind? ResolveNarrowedScalarKind(
        ExpressBoundExpression expression,
        ExpressExpressionEmissionContext context)
    {
        if (expression.Reference is not { } reference
            || context.ResolveLexicalBound?.Invoke(reference.Name)?.Type
                is not ExpressBoundScalarType scalar)
        {
            return null;
        }

        return scalar.Kind switch
        {
            ExpressScalarKind.Integer => ExpressExpressionTypeKind.Integer,
            ExpressScalarKind.Number => ExpressExpressionTypeKind.Number,
            ExpressScalarKind.Real => ExpressExpressionTypeKind.Real,
            _ => null,
        };
    }

    /// <summary>
    /// Emits the culture-invariant EXPRESS LIKE pattern match.
    /// </summary>
    /// <param name="input">The emitted input string.</param>
    /// <param name="pattern">The emitted EXPRESS pattern string.</param>
    /// <returns>The emitted Boolean pattern match.</returns>
    internal static string LikeMatch(string input, string pattern)
    {
        const string tokenPattern = "@\"!\\\\[\\s\\S]|![\\s\\S]|\\\\[\\s\\S]|[\\s\\S]\"";
        var tokens = "global::System.Linq.Enumerable.Cast<global::System.Text.RegularExpressions.Match>("
            + $"global::System.Text.RegularExpressions.Regex.Matches(({pattern}), {tokenPattern}))";
        var converted = "global::System.Linq.Enumerable.Select("
            + $"{tokens}, __token => __token.Value switch {{ "
            + "\"@\" => @\"\\p{L}\", \"^\" => @\"\\p{Lu}\", "
            + "\"?\" => @\"[\\s\\S]\", \"&\" or \"*\" => @\"[\\s\\S]*\", "
            + "\"#\" => @\"[0-9]\", \"$\" => @\"[^ ]*(?: |\\z)\", "
            + "var __value when __value.StartsWith(\"!\\\\\", global::System.StringComparison.Ordinal) => "
            + "@\"(?!\" + global::System.Text.RegularExpressions.Regex.Escape(__value.Substring(2)) "
            + "+ @\")[\\s\\S]\", "
            + "var __value when __value.StartsWith(\"!\", global::System.StringComparison.Ordinal) => "
            + "@\"(?!\" + (__value.Substring(1) switch { "
            + "\"@\" => @\"\\p{L}\", \"^\" => @\"\\p{Lu}\", \"?\" => @\"[\\s\\S]\", "
            + "\"&\" or \"*\" => @\"[\\s\\S]*\", \"#\" => @\"[0-9]\", "
            + "\"$\" => @\"[^ ]*(?: |\\z)\", var __literal => "
            + "global::System.Text.RegularExpressions.Regex.Escape(__literal) }) + @\")[\\s\\S]\", "
            + "var __value when __value.StartsWith(\"\\\\\", global::System.StringComparison.Ordinal) => "
            + "global::System.Text.RegularExpressions.Regex.Escape(__value.Substring(1)), "
            + "var __value => global::System.Text.RegularExpressions.Regex.Escape(__value) })";
        var regex = "global::System.String.Concat(@\"\\A\", "
            + $"global::System.String.Concat({converted}), @\"\\z\")";
        return "global::System.Text.RegularExpressions.Regex.IsMatch("
            + $"({input}), {regex}, global::System.Text.RegularExpressions.RegexOptions.CultureInvariant)";
    }

    private static string EmitInterval(
        ExpressBoundExpression expression,
        ExpressExpressionEmissionContext context)
    {
        var operations = expression.Operation!.Split(',');
        var low = EmitCode(expression.Children[0], context);
        var item = EmitCode(expression.Children[1], context);
        var high = EmitCode(expression.Children[2], context);
        var suffix = expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
            + "_"
            + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture);
        var variables = new string[]
        {
            "__expressLow_" + suffix,
            "__expressItem_" + suffix,
            "__expressHigh_" + suffix,
        };
        var requiresSelectProjection = expression.Children.Any(child =>
            RequiresSelectScalarProjection(child, context));
        var body = GuardIndeterminate(
            expression,
            context,
            expression.Children,
            variables,
            codes =>
            {
                if (requiresSelectProjection)
                {
                    return context.ResolveModelFunction?.Invoke(
                            "ORDERED_SELECT_COMPARISON",
                            expression,
                            codes)
                        ?? throw GenerationError(
                            expression,
                            "SELECT interval ordering requires an enclosing generated schema operation.");
                }

                var lower = LogicalComparison(OrderedComparison(
                    operations[0],
                    expression.Children[0],
                    codes[0],
                    expression.Children[1],
                    codes[1],
                    context));
                var upper = LogicalComparison(OrderedComparison(
                    operations[1],
                    expression.Children[1],
                    codes[1],
                    expression.Children[2],
                    codes[2],
                    context));
                return EmitLogicalBinary("AND", lower, upper);
            });
        return $"(({low}), ({item}), ({high})) switch {{ "
            + $"var ({string.Join(", ", variables)}) => {body} }}";
    }

    private static string EmitReference(
        ExpressBoundExpression expression,
        ExpressExpressionEmissionContext context)
    {
        if (expression.Reference is not null)
        {
            var narrowed = expression.Type.DeclaredType as ExpressBoundNamedType;
            if ((expression.Reference.Type is ExpressBoundGenericType { IsEntity: true, }
                    || expression.Reference.Kind == ExpressBoundNameKind.QueryVariable)
                && narrowed?.Declaration.Kind == ExpressDeclarationKind.Entity)
            {
                var carrier = context.ResolveReference(expression.Reference, null, null, null);
                return context.ResolveReference(
                    expression.Reference,
                    expression.Reference.Type,
                    carrier,
                    narrowed.Declaration);
            }

            var carrierType = expression.Reference.Attribute?.Type
                ?? (expression.Reference.AttributeCandidates.Count == 1
                    ? expression.Reference.AttributeCandidates[0].Type
                    : expression.Reference.Type);
            if (narrowed is not null
                && carrierType is not null
                && (carrierType is not ExpressBoundNamedType namedCarrier
                    || !ReferenceEquals(namedCarrier.Declaration, narrowed.Declaration)))
            {
                var carrier = narrowed.Declaration.Kind == ExpressDeclarationKind.Entity
                    ? context.ResolveReference(expression.Reference, null, null, null)
                    : null;
                return context.ResolveReference(
                    expression.Reference,
                    carrierType,
                    carrier,
                    narrowed.Declaration);
            }

            var source = context.ResolveReference(expression.Reference, null, null, null);
            var lexicalCarrierType = context.ResolveLexicalBound?.Invoke(expression.Reference.Name)?.Type;

            if (lexicalCarrierType is ExpressBoundScalarType)
            {
                return source;
            }

            carrierType = lexicalCarrierType ?? carrierType;

            if (carrierType is { } scalarCarrier
                && scalarCarrier is not ExpressBoundScalarType
                && CreateScalarType(expression.Type.Kind, scalarCarrier) is { } narrowedScalar
                && context.ResolveNarrowedScalarReference is not null
                && context.ResolveNarrowedScalarReference(
                    expression.Reference,
                    scalarCarrier,
                    source,
                    narrowedScalar) is { } narrowedSource)
            {
                return narrowedSource;
            }

            return UnwrapDefined(expression.Type, source, context);
        }

        return expression.Operation?.ToUpperInvariant() switch
        {
            "E" => EmitReal("2.718281828459045"),
            "PI" => EmitReal("3.141592653589793"),
            "SELF" when context.SelfExpression is not null => UnwrapDefined(
                expression.Type,
                context.SelfExpression,
                context),
            "SELF" => throw GenerationError(expression, "SELF requires an enclosing generated entity operation."),
            _ => throw new InvalidOperationException(
                $"Reference '{expression.SourceText}' has no resolved static target."),
        };
    }

    private static string EmitApplication(
        ExpressBoundExpression expression,
        ExpressExpressionEmissionContext context)
    {
        var arguments = expression.Children
            .Select(child => EmitCode(child, context))
            .ToArray();
        if (expression.Reference is null)
        {
            if (expression.Operation is not ("EXISTS" or "NVL")
                && expression.Children.Any(child => child.Type.CanBeIndeterminate
                    || context.MayReturnIndeterminate?.Invoke(child) == true))
            {
                return GuardIndeterminate(
                    expression,
                    context,
                    expression.Children,
                    arguments,
                    codes => EmitBuiltIn(expression, codes, context));
            }

            return EmitBuiltIn(expression, arguments, context);
        }

        var target = expression.Reference.Kind == ExpressBoundNameKind.Entity
            && expression.Reference.SchemaDeclaration is { } entity
                ? $"new {GeneratedTypeName(entity, entityInterface: false)}"
                : context.ResolveReference(expression.Reference, null, null, null);
        string Invoke(IReadOnlyList<string> codes)
        {
            return context.ResolveApplication is not null
                && expression.Reference.Kind is ExpressBoundNameKind.Function or ExpressBoundNameKind.Entity
                    ? context.ResolveApplication(expression, codes)
                    : $"{target}({string.Join(", ", codes)})";
        }

        if (expression.Children.Any(child => child.Type.CanBeIndeterminate
            || context.MayReturnIndeterminate?.Invoke(child) == true))
        {
            return GuardIndeterminate(
                expression,
                context,
                expression.Children,
                arguments,
                codes => UnwrapDefined(expression.Type, Invoke(codes), context));
        }

        return UnwrapDefined(expression.Type, Invoke(arguments), context);
    }

    private static string EmitAttribute(
        ExpressBoundExpression expression,
        ExpressExpressionEmissionContext context)
    {
        var reference = expression.Reference
            ?? throw new InvalidOperationException(
                $"Attribute expression '{expression.SourceText}' has no resolved member.");
        if (reference.Kind == ExpressBoundNameKind.Enumeration
            && reference.Type is ExpressBoundNamedType enumeration)
        {
            return $"{GeneratedTypeName(enumeration.Declaration, entityInterface: false)}."
                + ExpressEntityProjection.ToPascalCase(reference.Name);
        }

        var source = EmitCode(expression.Children.Single(), context);
        string Access(string candidate)
        {
            return context.ResolveAttribute is null
                ? $"({candidate}).{ExpressEntityProjection.ToPascalCase(reference.Name)}"
                : context.ResolveAttribute(expression.Children[0], reference, candidate);
        }

        string Result(string candidate)
        {
            var result = Access(candidate);
            var narrowed = expression.Type.DeclaredType as ExpressBoundNamedType;
            var declaredCarrier = reference.Attribute?.Type
                ?? (reference.AttributeCandidates.Count == 1
                    ? reference.AttributeCandidates[0].Type
                    : reference.Type);
            if (expression.Type.Kind == ExpressExpressionTypeKind.Aggregate
                && declaredCarrier is ExpressBoundNamedType declaredCarrierName
                && context.ResolveDefinedValueType?.Invoke(declaredCarrierName)
                    is ExpressBoundNamedType semanticCarrier
                && ReferenceEquals(
                    declaredCarrierName.Declaration,
                    semanticCarrier.Declaration))
            {
                return result;
            }

            if (declaredCarrier is { } carrier
                && narrowed?.Declaration.Kind == ExpressDeclarationKind.Entity
                && (carrier is not ExpressBoundNamedType carrierName
                    || !ReferenceEquals(carrierName.Declaration, narrowed.Declaration)))
            {
                return context.ResolveReference(
                    reference,
                    carrier,
                    result,
                    narrowed.Declaration);
            }

            if (declaredCarrier is { } scalarCarrier
                && scalarCarrier is not ExpressBoundScalarType
                && CreateScalarType(expression.Type.Kind, scalarCarrier) is { } narrowedScalar
                && context.ResolveNarrowedScalarReference is not null
                && context.ResolveNarrowedScalarReference(
                    reference,
                    scalarCarrier,
                    result,
                    narrowedScalar) is { } narrowedResult)
            {
                return narrowedResult;
            }

            return UnwrapDefined(expression.Type, result, context);
        }

        if (expression.Children[0].Type.CanBeIndeterminate)
        {
            return GuardIndeterminate(
                expression,
                context,
                expression.Children,
                [source,],
                codes => Result(codes[0]));
        }

        return Result(source);
    }

    private static ExpressBoundScalarType? CreateScalarType(
        ExpressExpressionTypeKind kind,
        ExpressBoundType carrier)
    {
        var scalarKind = kind switch
        {
            ExpressExpressionTypeKind.Binary => ExpressScalarKind.Binary,
            ExpressExpressionTypeKind.Boolean => ExpressScalarKind.Boolean,
            ExpressExpressionTypeKind.Integer => ExpressScalarKind.Integer,
            ExpressExpressionTypeKind.Logical => ExpressScalarKind.Logical,
            ExpressExpressionTypeKind.Number => ExpressScalarKind.Number,
            ExpressExpressionTypeKind.Real => ExpressScalarKind.Real,
            ExpressExpressionTypeKind.String => ExpressScalarKind.String,
            _ => (ExpressScalarKind?)null,
        };
        return scalarKind is null
            ? null
            : new ExpressBoundScalarType(scalarKind.Value, constraintText: null, isFixed: false, carrier.Span);
    }

    private static string EmitGroup(
        ExpressBoundExpression expression,
        ExpressExpressionEmissionContext context)
    {
        var reference = expression.Reference?.SchemaDeclaration
            ?? throw new InvalidOperationException(
                $"Group expression '{expression.SourceText}' has no resolved entity.");
        var sourceExpression = expression.Children.Single();
        var source = EmitCode(sourceExpression, context);
        string Access(string candidate)
        {
            return context.ResolveAttribute is not null
                ? context.ResolveAttribute(sourceExpression, expression.Reference!, candidate)
                : $"(({GeneratedTypeName(reference, entityInterface: true)})({candidate}))";
        }

        if (expression.Children[0].Type.CanBeIndeterminate)
        {
            return GuardIndeterminate(
                expression,
                context,
                expression.Children,
                [source,],
                codes => Access(codes[0]));
        }

        return Access(source);
    }

    private static string EmitIndex(
        ExpressBoundExpression expression,
        ExpressExpressionEmissionContext context)
    {
        var sourceExpression = expression.Children[0];
        var source = EmitCode(sourceExpression, context);
        var index = EmitCode(expression.Children[1], context);
        var codes = expression.Children.Count == 2
            ? new[] { source, index, }
            : new[] { source, index, EmitCode(expression.Children[2], context), };
        var isProvenPresent = sourceExpression.Type.DeclaredType is ExpressBoundAggregateType
        { Kind: ExpressAggregateKind.List or ExpressAggregateKind.Bag, }
            && (context.SafeIndices.Any(safeIndex =>
                    ReferenceEquals(sourceExpression.Reference, safeIndex.Key)
                    && ReferenceEquals(expression.Children[1].Reference, safeIndex.Value))
                || context.IsKnownDeterminate?.Invoke(expression) == true);
        return GuardIndeterminate(
            expression,
            context,
            expression.Children,
            codes,
            present => EmitIndexCore(expression, sourceExpression, present, isProvenPresent, context));
    }

    private static string EmitIndexCore(
        ExpressBoundExpression expression,
        ExpressBoundExpression sourceExpression,
        string[] codes,
        bool isProvenPresent,
        ExpressExpressionEmissionContext context)
    {
        var source = codes[0];
        var index = codes[1];
        var indexValue = expression.Children[1].Type.Kind == ExpressExpressionTypeKind.Number
            ? $"({index}).ToIntegerTruncated()"
            : index;
        var suffix = expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
            + "_"
            + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture);
        var value = "__expressIndexed_" + suffix;
        var position = "__expressIndex_" + suffix;
        var resultType = expression.Type.Kind == ExpressExpressionTypeKind.Generic
            && sourceExpression.Type.DeclaredType is ExpressBoundAggregateType genericSource
            ? BoundTypeName(genericSource.ElementType)
            : EmittedValueTypeName(expression.Type.WithIndeterminate(false), context);
        var nullValue = expression.Type.Kind == ExpressExpressionTypeKind.Generic
            ? $"default({resultType})"
            : $"({resultType}?)null";
        if (sourceExpression.Type.Kind == ExpressExpressionTypeKind.Binary)
        {
            return $"(({source}), checked((int)({indexValue}))) switch {{ "
                + $"var ({value}, {position}) when {position} >= 1 && {position} <= {value}.Length => "
                + $"new global::TedToolkit.Step21.BinaryValue({value}.ToString().Substring({position} - 1, 1)), "
                + $"_ => {nullValue} }}";
        }

        if (sourceExpression.Type.Kind == ExpressExpressionTypeKind.String)
        {
            return $"(({source}), checked((int)({indexValue}))) switch {{ "
                + $"var ({value}, {position}) when {position} >= 1 && {position} <= {value}.Length => "
                + $"{value}.Substring({position} - 1, 1), _ => {nullValue} }}";
        }

        if (sourceExpression.Type.Kind == ExpressExpressionTypeKind.Generic)
        {
            return context.ResolveModelFunction?.Invoke("GENERIC_INDEX", expression, [source, indexValue,])
                ?? throw GenerationError(
                    expression,
                    "Generic aggregate indexing requires an enclosing generated schema operation.");
        }

        if (context.ResolveSelectedAggregateIndex?.Invoke(expression, codes, resultType)
            is { } selectedAggregateIndex)
        {
            return selectedAggregateIndex;
        }

        var aggregate = sourceExpression.Type.DeclaredType as ExpressBoundAggregateType
            ?? context.ResolveAggregateType?.Invoke(sourceExpression.Type.DeclaredType!);
        if (aggregate is null)
        {
            return context.ResolveSelectedAggregateIndex?.Invoke(expression, codes, resultType)
                ?? throw GenerationError(expression, "Index access requires a resolved aggregate domain.");
        }

        string ResultValue(string candidate)
        {
            if (expression.Type.DeclaredType is ExpressBoundNamedType
                { Declaration.Kind: ExpressDeclarationKind.Entity, } narrowedEntity
                && aggregate.ElementType is ExpressBoundNamedType
                { Declaration.Kind: not ExpressDeclarationKind.Entity, }
                && context.ResolveNarrowedEntityCarrier is not null)
            {
                return context.ResolveNarrowedEntityCarrier(
                    aggregate.ElementType,
                    candidate,
                    narrowedEntity);
            }

            if (expression.Type.DeclaredType is ExpressBoundNamedType
                { Declaration.Kind: ExpressDeclarationKind.Entity, } narrowedEntityType
                && aggregate.ElementType is ExpressBoundNamedType
                { Declaration.Kind: ExpressDeclarationKind.Entity, } sourceEntityType
                && !ReferenceEquals(narrowedEntityType.Declaration, sourceEntityType.Declaration))
            {
                var narrowedName = TypeName(expression.Type.WithIndeterminate(false), context);
                var narrowedValue = "__expressNarrowedIndex_" + suffix;
                return $"({candidate}) is {narrowedName} {narrowedValue} ? {narrowedValue} : "
                    + $"({narrowedName}?)null";
            }

            return UnwrapDefined(expression.Type, candidate, context);
        }

        if (isProvenPresent)
        {
            var presentValue = aggregate.Kind is ExpressAggregateKind.Bag or ExpressAggregateKind.Set
                ? $"global::System.Linq.Enumerable.ElementAt({source}, checked((int)({indexValue})) - 1)"
                : $"({source})[checked((int)({indexValue})) - 1]";
            return ResultValue(presentValue);
        }

        var endIndexValue = "";
        if (codes.Length == 3)
        {
            endIndexValue = expression.Children[2].Type.Kind == ExpressExpressionTypeKind.Number
                ? $"({codes[2]}).ToIntegerTruncated()"
                : codes[2];
        }

        var samePosition = codes.Length == 3
            ? $" && {position} == checked((int)({endIndexValue}))"
            : "";
        if (aggregate.Kind == ExpressAggregateKind.Array)
        {
            var item = "__expressItem_" + suffix;
            return $"(({source}), checked((int)({indexValue}))) switch {{ "
                + $"var ({value}, {position}) when {position} >= {value}.LowerIndex "
                + $"&& {position} <= {value}.UpperIndex{samePosition} "
                + $"&& {value}.TryGetValue({position}, out var {item}) => "
                + $"{ResultValue(item)}, _ => {nullValue} }}";
        }

        return $"(({source}), checked((int)({indexValue}))) switch {{ "
            + $"var ({value}, {position}) when {position} >= 1 "
            + $"&& {position} <= global::System.Linq.Enumerable.Count({value}){samePosition} => "
            + $"{ResultValue($"global::System.Linq.Enumerable.ElementAt({value}, {position} - 1)")}, "
            + $"_ => {nullValue} }}";
    }

    private static string EmitSlice(
        ExpressBoundExpression expression,
        ExpressExpressionEmissionContext context)
    {
        var sourceExpression = expression.Children[0];
        var source = EmitCode(sourceExpression, context);
        var low = EmitCode(expression.Children[1], context);
        var high = EmitCode(expression.Children[2], context);
        return GuardIndeterminate(
            expression,
            context,
            expression.Children,
            [source, low, high,],
            codes => EmitSliceCore(expression, sourceExpression, codes));
    }

    private static string EmitSliceCore(
        ExpressBoundExpression expression,
        ExpressBoundExpression sourceExpression,
        string[] codes)
    {
        var suffix = expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
            + "_"
            + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture);
        var source = "__expressSlice_" + suffix;
        var low = "__expressLow_" + suffix;
        var high = "__expressHigh_" + suffix;
        var nullValue = $"({TypeName(expression.Type.WithIndeterminate(false))}?)null";
        var text = sourceExpression.Type.Kind == ExpressExpressionTypeKind.Binary
            ? $"{source}.ToString()"
            : source;
        var segment = $"{text}.Substring({low} - 1, {high} - {low} + 1)";
        var result = sourceExpression.Type.Kind == ExpressExpressionTypeKind.Binary
            ? $"new global::TedToolkit.Step21.BinaryValue({segment})"
            : segment;
        return $"(({codes[0]}), checked((int)({codes[1]})), checked((int)({codes[2]}))) switch {{ "
            + $"var ({source}, {low}, {high}) when {low} >= 1 && {low} <= {high} "
            + $"&& {high} <= {text}.Length => {result}, _ => {nullValue} }}";
    }

    private static string EmitAggregate(
        ExpressBoundExpression expression,
        ExpressExpressionEmissionContext context)
    {
        var aggregate = (ExpressBoundAggregateType)expression.Type.DeclaredType!;
        var dynamicRepetitions = expression.Children
            .Where(child => child.Kind == ExpressExpressionKind.Repetition
                && !IsStaticallyValidRepetition(child.Children[1]))
            .Select((child, index) => (
                Child: child,
                Code: EmitCode(child.Children[1], context),
                RawVariable: "__expressRepetitionRaw_"
                    + expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                    + "_"
                    + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                    + "_" + index.ToString(CultureInfo.InvariantCulture),
                Variable: "__expressRepetition_"
                    + expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                    + "_"
                    + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                    + "_" + index.ToString(CultureInfo.InvariantCulture)))
            .ToArray();
        if (aggregate.Kind == ExpressAggregateKind.Array)
        {
            return EmitArrayAggregate(expression, aggregate, context, dynamicRepetitions);
        }

        var elementExpressions = expression.Children
            .Select(child => child.Kind == ExpressExpressionKind.Repetition ? child.Children[0] : child)
            .ToArray();
        var elementCodes = elementExpressions
            .Select(child => EmitCode(child, context))
            .ToArray();
        var aggregateCode = GuardIndeterminate(
            expression,
            context,
            elementExpressions,
            elementCodes,
            presentCodes =>
            {
                var items = expression.Children.Select((child, index) =>
                {
                    if (child.Kind != ExpressExpressionKind.Repetition)
                    {
                        return ConvertAggregateElement(
                            child,
                            presentCodes[index],
                            aggregate.ElementType,
                            context,
                            sourceIsDeterminate: true);
                    }

                    var dynamicRepetition = dynamicRepetitions.SingleOrDefault(item =>
                        ReferenceEquals(item.Child, child));
                    var count = dynamicRepetition.Child is null
                        ? EmitCode(child.Children[1], context)
                        : dynamicRepetition.Variable;
                    return "..global::System.Linq.Enumerable.Repeat("
                        + $"{ConvertAggregateElement(
                             child.Children[0],
                             presentCodes[index],
                             aggregate.ElementType,
                             context,
                             sourceIsDeterminate: true)}, "
                        + $"checked((int)({count})))";
                });
                var resultType = TypeName(expression.Type.WithIndeterminate(false), context);
                return $"({resultType})[{string.Join(", ", items)}]";
            });
        var resultType = TypeName(expression.Type.WithIndeterminate(false), context);
        return GuardDynamicRepetitions(dynamicRepetitions, resultType, aggregateCode);
    }

    private static bool IsStaticallyValidRepetition(ExpressBoundExpression count)
    {
        return count.Kind == ExpressExpressionKind.Literal
            && BigInteger.TryParse(count.SourceText, out var value)
            && value.Sign >= 0
            && value <= int.MaxValue;
    }

    private static string EmitArrayAggregate(
        ExpressBoundExpression expression,
        ExpressBoundAggregateType aggregate,
        ExpressExpressionEmissionContext context,
        (ExpressBoundExpression Child, string Code, string RawVariable, string Variable)[] dynamicRepetitions)
    {
        var lowerText = aggregate.ResolvedLowerBoundText ?? aggregate.LowerBoundText;
        var upperText = aggregate.ResolvedUpperBoundText ?? aggregate.UpperBoundText;
        var hasStaticLower = int.TryParse(
            lowerText,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var lower);
        var hasStaticUpper = int.TryParse(
            upperText,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var upper);
        var lexicalLower = hasStaticLower ? null : context.ResolveLexicalBound?.Invoke(aggregate.LowerBoundText ?? "");
        var lexicalUpper = hasStaticUpper ? null : context.ResolveLexicalBound?.Invoke(aggregate.UpperBoundText ?? "");
        if ((!hasStaticLower && lexicalLower is null)
            || (!hasStaticUpper && lexicalUpper is null))
        {
            throw GenerationError(
                expression,
                "ARRAY initializer generation requires static or lexical numeric declared bounds.");
        }

        string lowerCode;
        if (hasStaticLower)
        {
            lowerCode = lower.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            lowerCode = lexicalLower!.Value.Type is ExpressBoundScalarType { Kind: ExpressScalarKind.Number, }
                ? $"checked((int)({lexicalLower.Value.Code}).ToIntegerTruncated())"
                : $"checked((int)({lexicalLower.Value.Code}))";
        }

        string upperCode;
        if (hasStaticUpper)
        {
            upperCode = upper.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            upperCode = lexicalUpper!.Value.Type is ExpressBoundScalarType { Kind: ExpressScalarKind.Number, }
                ? $"checked((int)({lexicalUpper.Value.Code}).ToIntegerTruncated())"
                : $"checked((int)({lexicalUpper.Value.Code}))";
        }

        var hasStaticBounds = hasStaticLower && hasStaticUpper;

        var staticCardinality = expression.Children.Aggregate(0, (count, child) =>
        {
            if (child.Kind != ExpressExpressionKind.Repetition)
            {
                return count + 1;
            }

            return IsStaticallyValidRepetition(child.Children[1])
                ? checked(count + int.Parse(child.Children[1].SourceText, CultureInfo.InvariantCulture))
                : count;
        });
        if (hasStaticBounds
            && dynamicRepetitions.Length == 0
            && staticCardinality != upper - lower + 1)
        {
            throw GenerationError(
                expression,
                "ARRAY initializer cardinality does not match its declared index domain.");
        }

        var resultType = TypeName(expression.Type.WithIndeterminate(false), context);
        var suffix = expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
            + "_"
            + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture);
        var result = "__expressArray_" + suffix;
        var values = "__expressArrayValues_" + suffix;
        var index = "__expressArrayIndex_" + suffix;
        var lowerIndex = "__expressArrayLower_" + suffix;
        var upperIndex = "__expressArrayUpper_" + suffix;
        var elementType = BoundTypeName(aggregate.ElementType);
        var guardedGenericIndices = hasStaticBounds
            ? []
            : expression.Children
                .Select(child => child.Kind == ExpressExpressionKind.Repetition ? child.Children[0] : child)
                .Where(value => value.Kind == ExpressExpressionKind.IndexQualifier
                    && value.Type.Kind == ExpressExpressionTypeKind.Generic
                    && value.Children.Count == 2
                    && value.Children[0].Kind == ExpressExpressionKind.Reference
                    && !value.Children[0].Type.CanBeIndeterminate
                    && value.Children[0].Type.DeclaredType is ExpressBoundAggregateType
                    {
                        Kind: ExpressAggregateKind.List,
                    })
                .Select(value =>
                {
                    var location = value.Span.Start;
                    var itemVariable = "__expressArrayItem_"
                        + location.Line.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + location.Column.ToString(CultureInfo.InvariantCulture);
                    var positionVariable = "__expressArrayItemIndex_"
                        + location.Line.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + location.Column.ToString(CultureInfo.InvariantCulture);
                    var sourceCode = EmitCode(value.Children[0], context);
                    var indexCode = EmitCode(value.Children[1], context);
                    var positionCode = value.Children[1].Type.Kind == ExpressExpressionTypeKind.Number
                        ? $"checked((int)({indexCode}).ToIntegerTruncated())"
                        : $"checked((int)({indexCode}))";
                    var guard = $"var {positionVariable} = {positionCode}; "
                        + $"if ({positionVariable} < 1 "
                        + $"|| {positionVariable} > global::System.Linq.Enumerable.Count({sourceCode}) "
                        + $"|| global::System.Linq.Enumerable.ElementAt({sourceCode}, {positionVariable} - 1) "
                        + $"is not {{ }} {itemVariable}) return null; ";
                    return (Value: value, Variable: itemVariable, Guard: guard);
                })
                .ToArray();
        var items = expression.Children.Select(child =>
        {
            var value = child.Kind == ExpressExpressionKind.Repetition ? child.Children[0] : child;
            var guarded = guardedGenericIndices.SingleOrDefault(candidate => ReferenceEquals(candidate.Value, value));
            string item;
            if (guarded.Value is not null)
            {
                item = $"(true, new {elementType}[] {{ {guarded.Variable} }})";
            }
            else if (value.Kind == ExpressExpressionKind.Indeterminate)
            {
                item = $"(false, global::System.Array.Empty<{elementType}>())";
            }
            else if (aggregate.IsOptional && value.Type.CanBeIndeterminate)
            {
                var presentValue = context.AllocateTemporaryName(
                    "__expressOptionalElement_"
                    + value.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                    + "_"
                    + value.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                    + "_");
                item = $"(({EmitCode(value, context)}) is {{ }} {presentValue} "
                    + $"? (true, new {elementType}[] {{ "
                    + $"{ConvertAggregateElement(
                        value,
                        presentValue,
                        aggregate.ElementType,
                        context,
                        sourceIsDeterminate: true)} }}) "
                    + $": (false, global::System.Array.Empty<{elementType}>()))";
            }
            else
            {
                item = $"(true, new {elementType}[] {{ "
                    + $"{ConvertAggregateElement(
                        value,
                        EmitCode(value, context),
                        aggregate.ElementType,
                        context)} }})";
            }

            if (child.Kind != ExpressExpressionKind.Repetition)
            {
                return item;
            }

            var dynamicRepetition = dynamicRepetitions.SingleOrDefault(candidate =>
                ReferenceEquals(candidate.Child, child));
            var count = dynamicRepetition.Child is null
                ? EmitCode(child.Children[1], context)
                : dynamicRepetition.Variable;
            return $"..global::System.Linq.Enumerable.Repeat({item}, checked((int)({count})))";
        });
        var generatedLower = hasStaticBounds ? lowerCode : lowerIndex;
        var generatedUpper = hasStaticBounds ? upperCode : upperIndex;
        var lexicalBounds = hasStaticBounds
            ? ""
            : $"var {lowerIndex} = {lowerCode}; var {upperIndex} = {upperCode}; "
                + $"if ({upperIndex} < {lowerIndex}) return null; "
                + string.Concat(guardedGenericIndices.Select(item => item.Guard));
        var cardinality = hasStaticBounds
            ? (upper - lower + 1).ToString(CultureInfo.InvariantCulture)
            : $"((long)({upperIndex}) - ({lowerIndex}) + 1L)";
        var arrayCode = $"((global::System.Func<{resultType}{(expression.Type.CanBeIndeterminate ? "?" : "")}>)(() => {{ "
            + lexicalBounds
            + $"var {values} = ((bool IsSet, {elementType}[] Values)[])[{string.Join(", ", items)}]; "
            + (expression.Type.CanBeIndeterminate
                ? $"if ({values}.LongLength != {cardinality}) return null; "
                : "")
            + $"var {result} = new {resultType}("
            + $"{generatedLower}, {generatedUpper}, "
            + $"isOptional: {(aggregate.IsOptional ? "true" : "false")}, "
            + $"isUnique: {(aggregate.IsUnique ? "true" : "false")}); "
            + $"for (var {index} = 0; {index} < {values}.Length; {index}++) "
            + $"{{ if ({values}[{index}].IsSet) "
            + $"{result}[checked({generatedLower} + {index})] = {values}[{index}].Values[0]; }} "
            + $"return {result}; }}))()";
        return GuardDynamicRepetitions(dynamicRepetitions, resultType, arrayCode);
    }

    private static string GuardDynamicRepetitions(
        (ExpressBoundExpression Child, string Code, string RawVariable, string Variable)[] repetitions,
        string resultType,
        string code)
    {
        if (repetitions.Length == 0)
        {
            return code;
        }

        var values = repetitions.Length == 1
            ? $"({repetitions[0].Code})"
            : $"({string.Join(", ", repetitions.Select(item => $"({item.Code})"))})";
        var pattern = repetitions.Length == 1
            ? $"var {repetitions[0].RawVariable}"
            : $"var ({string.Join(", ", repetitions.Select(item => item.RawVariable))})";
        var condition = string.Join(" && ", repetitions.Select(item =>
            $"{item.RawVariable} is {{ }} {item.Variable} && {item.Variable}.Sign >= 0 "
            + $"&& {item.Variable} <= global::System.Int32.MaxValue"));
        return $"{values} switch {{ {pattern} when {condition} => {code}, _ => ({resultType}?)null }}";
    }

    /// <summary>
    /// Adapts a present element to the aggregate's numeric or nominal value domain.
    /// </summary>
    /// <param name="expression">The element's bound expression.</param>
    /// <param name="code">The emitted determinate element.</param>
    /// <param name="target">The required element domain.</param>
    /// <param name="context">The shared schema emission context.</param>
    /// <param name="sourceIsDeterminate">Whether <paramref name="code"/> is already proven present.</param>
    /// <returns>The converted element expression.</returns>
    internal static string ConvertAggregateElement(
        ExpressBoundExpression expression,
        string code,
        ExpressBoundType target,
        ExpressExpressionEmissionContext context,
        bool sourceIsDeterminate = false)
    {
        if (target is ExpressBoundNamedType { Declaration.Kind: ExpressDeclarationKind.Entity, } entity
            && expression.Type.Kind == ExpressExpressionTypeKind.Select
            && expression.Type.DeclaredType is { } carrier
            && context.ResolveNarrowedEntityCarrier is not null)
        {
            return context.ResolveNarrowedEntityCarrier(carrier, code, entity);
        }

        var converted = target switch
        {
            ExpressBoundScalarType { Kind: ExpressScalarKind.Logical, }
                when expression.Type.Kind == ExpressExpressionTypeKind.Boolean =>
                AsLogical(expression, code),
            ExpressBoundScalarType { Kind: ExpressScalarKind.Number, } =>
                PromoteNumeric(expression, code, ExpressExpressionTypeKind.Number),
            ExpressBoundScalarType { Kind: ExpressScalarKind.Real, } =>
                PromoteNumeric(expression, code, ExpressExpressionTypeKind.Real),
            _ => code,
        };
        return context.ResolveAggregateElement?.Invoke(
            expression,
            converted,
            target,
            sourceIsDeterminate) ?? converted;
    }

    private static string EmitQuery(
        ExpressBoundExpression expression,
        ExpressExpressionEmissionContext context)
    {
        var sourceExpression = expression.Children[0];
        var source = EmitCode(sourceExpression, context);
        var variableName = "__query_" + ExpressEntityProjection.ToPascalCase(expression.Operation!);
        var predicate = expression.Children[1];
        var resultAggregate = expression.Type.DeclaredType as ExpressBoundAggregateType
            ?? throw new InvalidOperationException("A QUERY result must retain its aggregate type.");
        var sourceCarrierType = sourceExpression.Reference?.Attribute?.Type
            ?? (sourceExpression.Reference?.AttributeCandidates.Count == 1
                ? sourceExpression.Reference.AttributeCandidates[0].Type
                : sourceExpression.Reference?.Type)
            ?? sourceExpression.Type.DeclaredType!;
        var sourceAggregate = sourceExpression.Type.DeclaredType as ExpressBoundAggregateType
            ?? context.ResolveAggregateType?.Invoke(sourceCarrierType)
            ?? context.ResolveAggregateType?.Invoke(sourceExpression.Type.DeclaredType!)
            ?? resultAggregate;
        var resolvedSourceCarrier = context.ResolveDefinedValueType?.Invoke(sourceCarrierType);
        if (resolvedSourceCarrier is not null
            && resolvedSourceCarrier is not ExpressBoundAggregateType)
        {
            source = context.ResolveAggregateSource?.Invoke(
                sourceCarrierType,
                source,
                sourceAggregate)
                ?? throw GenerationError(expression, "QUERY requires a resolved aggregate source.");
        }

        string ResolveQueryReference(
            ExpressBoundName reference,
            ExpressBoundType? narrowedType,
            string? narrowedCode,
            ExpressBoundSymbol? narrowedAlternative)
        {
            if (reference.Kind == ExpressBoundNameKind.QueryVariable
                && string.Equals(reference.Name, expression.Operation, StringComparison.OrdinalIgnoreCase))
            {
                return narrowedAlternative is null
                    ? variableName
                    : context.ResolveReference(
                        reference,
                        narrowedType ?? sourceAggregate.ElementType,
                        narrowedCode ?? variableName,
                        narrowedAlternative);
            }

            return context.ResolveReference(reference, narrowedType, narrowedCode, narrowedAlternative);
        }

        var predicateCode = EmitCode(predicate, context.WithReferenceResolver(ResolveQueryReference));
        var condition = $"({AsLogical(predicate, predicateCode)}) "
            + "== global::TedToolkit.Step21.LogicalValue.True";
        var resultValue = variableName;
        if (!ReferenceEquals(sourceAggregate.ElementType, resultAggregate.ElementType)
            && resultAggregate.ElementType is ExpressBoundNamedType narrowedElement)
        {
            var queryVariable = expression.Reference
                ?? predicate.DescendantsAndSelf()
                    .Select(candidate => candidate.Reference)
                    .FirstOrDefault(reference => reference?.Kind == ExpressBoundNameKind.QueryVariable
                        && string.Equals(reference.Name, expression.Operation, StringComparison.OrdinalIgnoreCase));
            if (queryVariable is not null)
            {
                resultValue = context.ResolveReference(
                    queryVariable,
                    sourceAggregate.ElementType,
                    variableName,
                    narrowedElement.Declaration);
            }
        }

        return GuardIndeterminate(
            expression,
            context,
            [sourceExpression,],
            [source,],
            codes => EmitQueryCore(
                expression,
                sourceAggregate,
                codes[0],
                variableName,
                resultValue,
                condition));
    }

    private static string EmitQueryCore(
        ExpressBoundExpression expression,
        ExpressBoundAggregateType aggregate,
        string source,
        string variableName,
        string resultValue,
        string condition)
    {
        var sourceType = AggregateInterfaceTypeName(aggregate);
        var resultType = TypeName(expression.Type.WithIndeterminate(false));
        var elementType = BoundTypeName(aggregate.ElementType);
        var suffix = expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
            + "_"
            + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture);
        var input = "__expressQuerySource_" + suffix;
        var result = "__expressQueryResult_" + suffix;
        var factory = aggregate.Kind switch
        {
            ExpressAggregateKind.Array => $"new {resultType}({input}.LowerIndex, {input}.UpperIndex, "
                + $"isOptional: true, isUnique: {input}.IsUnique)",
            ExpressAggregateKind.Bag => $"new {resultType}(0, {input}.UpperBound)",
            ExpressAggregateKind.List =>
                $"new {resultType}(0, {input}.UpperBound, {input}.IsUnique)",
            ExpressAggregateKind.Aggregate => $"new {resultType}()",
            ExpressAggregateKind.Set => $"new {resultType}(0, {input}.UpperBound)",
            _ => throw new InvalidOperationException(
                $"Unknown query aggregate kind '{aggregate.Kind.ToString()}'."),
        };
        string body;
        if (aggregate.Kind == ExpressAggregateKind.Array)
        {
            var index = "__expressQueryIndex_" + suffix;
            body = $"for (var {index} = {input}.LowerIndex; {index} <= {input}.UpperIndex; {index}++) {{ "
                + $"if ({input}.IsSet({index})) {{ var {variableName} = {input}[{index}]; "
                + $"if ({condition}) {{ {result}[{index}] = {resultValue}; }} }} }}";
        }
        else
        {
            body = $"foreach (var {variableName} in {input}) {{ if ({condition}) "
                + $"{{ {result}.Add({resultValue}); }} }}";
        }

        return $"((global::System.Func<{sourceType}, {resultType}>)({input} => {{ "
            + $"var {result} = {factory}; {body} return {result}; }}))({source})";
    }

    private static string EmitBuiltIn(
        ExpressBoundExpression expression,
        string[] arguments,
        ExpressExpressionEmissionContext context)
    {
        var parameters = expression.Children;
        if (string.Equals(expression.Operation, "NVL", StringComparison.OrdinalIgnoreCase)
            && parameters.Count > 1
            && parameters[0].Type.DeclaredType is ExpressBoundNamedType sourceName
            && sourceName.Declaration.Kind != ExpressDeclarationKind.Entity
            && parameters[1].Type.DeclaredType is ExpressBoundNamedType
            { Declaration.Kind: ExpressDeclarationKind.Entity, } fallbackEntity
            && context.ResolveSelectToEntityValue is not null)
        {
            var projected = context.ResolveSelectToEntityValue(
                parameters[0],
                arguments[0],
                fallbackEntity);
            return $"(({projected}) ?? ({arguments[1]}))";
        }

        if (string.Equals(expression.Operation, "NVL", StringComparison.OrdinalIgnoreCase)
            && parameters.Count > 1
            && parameters[0].Type.Kind is ExpressExpressionTypeKind.Integer
                or ExpressExpressionTypeKind.Real
                or ExpressExpressionTypeKind.Number
            && parameters[1].Type.Kind is ExpressExpressionTypeKind.Integer
                or ExpressExpressionTypeKind.Real
                or ExpressExpressionTypeKind.Number)
        {
            var target = expression.Type.Kind;
            var fallback = PromoteNumeric(parameters[1], arguments[1], target);
            if (!parameters[0].Type.CanBeIndeterminate)
            {
                return PromoteNumeric(parameters[0], arguments[0], target);
            }

            var present = context.AllocateTemporaryName("__expressNvlValue");
            var promoted = PromoteNumeric(parameters[0], present, target);
            return $"(({arguments[0]}) is {{ }} {present} ? {promoted} : {fallback})";
        }

        if (expression.Operation?.ToUpperInvariant()
                is "ABS" or "BLENGTH" or "FORMAT" or "LENGTH" or "ODD" or "VALUE"
            && parameters.Any(parameter => RequiresSelectScalarProjection(parameter, context))
            && context.ResolveModelFunction is not null)
        {
            return context.ResolveModelFunction("SELECT_SCALAR_BUILTIN", expression, arguments);
        }

        return expression.Operation!.ToUpperInvariant() switch
        {
            "ABS" when parameters[0].Type.Kind == ExpressExpressionTypeKind.Integer =>
                $"global::System.Numerics.BigInteger.Abs({arguments[0]})",
            "ABS" when parameters[0].Type.Kind == ExpressExpressionTypeKind.Number =>
                $"(({arguments[0]}) < global::TedToolkit.Step21.NumberValue.FromInteger(0) ? "
                + $"-({arguments[0]}) : ({arguments[0]}))",
            "ABS" => $"(({arguments[0]}).Significand.Sign < 0 ? -({arguments[0]}) : ({arguments[0]}))",
            "ACOS" => RealMath(expression, "Acos", context, (parameters[0], arguments[0])),
            "ASIN" => RealMath(expression, "Asin", context, (parameters[0], arguments[0])),
            "ATAN" when arguments.Length == 2 => RealMath(
                expression,
                "Atan2",
                context,
                (parameters[0], arguments[0]),
                (parameters[1], arguments[1])),
            "ATAN" => RealMath(expression, "Atan", context, (parameters[0], arguments[0])),
            "BLENGTH" or "LENGTH" => BigIntegerExpression($"({arguments[0]}).Length"),
            "EXISTS" => MayEmitIndeterminate(parameters[0], context)
                ? $"({arguments[0]}) is not null"
                : "true",
            "COS" => RealMath(expression, "Cos", context, (parameters[0], arguments[0])),
            "EXP" => RealMath(expression, "Exp", context, (parameters[0], arguments[0])),
            "FORMAT" => EmitFormat(expression, parameters[0], arguments),
            "HIBOUND" => HighBound(parameters[0], arguments[0]),
            "HIINDEX" => BigIntegerExpression(HighIndex(parameters[0], arguments[0])),
            "LOBOUND" => BigIntegerExpression(LowBound(parameters[0], arguments[0])),
            "LOINDEX" => BigIntegerExpression(LowIndex(parameters[0], arguments[0])),
            "SIZEOF" when parameters[0].Type.Kind == ExpressExpressionTypeKind.Generic
                && context.ResolveModelFunction is not null =>
                context.ResolveModelFunction("GENERIC_SIZE", expression, arguments),
            "SIZEOF" when HasSelectedAggregateCarrier(parameters[0], context)
                && context.ResolveModelFunction is not null =>
                context.ResolveModelFunction("SELECT_AGGREGATE_SIZE", expression, arguments),
            "SIZEOF" => BigIntegerExpression($"global::System.Linq.Enumerable.Count({arguments[0]})"),
            "ODD" => LogicalComparison($"!({arguments[0]}).IsEven"),
            "NVL" => $"((({TypeName(parameters[1].Type.WithIndeterminate(false), context)}?)({arguments[0]})) "
                + $"?? ({arguments[1]}))",
            "LOG" => RealMath(expression, "Log", context, (parameters[0], arguments[0])),
            "LOG2" => RealMath(expression, "Log2", context, (parameters[0], arguments[0])),
            "LOG10" => RealMath(expression, "Log10", context, (parameters[0], arguments[0])),
            "SIN" => RealMath(expression, "Sin", context, (parameters[0], arguments[0])),
            "SQRT" => RealMath(expression, "Sqrt", context, (parameters[0], arguments[0])),
            "TAN" => RealMath(expression, "Tan", context, (parameters[0], arguments[0])),
            "VALUE" => EmitValue(expression, arguments[0]),
            "VALUE_IN" => EmitValueIn(expression, arguments),
            "VALUE_UNIQUE" => EmitValueUnique(expression, arguments[0]),
            "ROLESOF" or "TYPEOF" or "USEDIN" when context.ResolveModelFunction is not null =>
                context.ResolveModelFunction(expression.Operation.ToUpperInvariant(), expression, arguments),
            "ROLESOF" or "TYPEOF" or "USEDIN" => throw GenerationError(
                expression,
                $"{expression.Operation.ToUpperInvariant()} requires an enclosing generated model operation."),
            _ => throw GenerationError(
                expression,
                $"Built-in function '{expression.Operation}' has no static generator."),
        };
    }

    /// <summary>
    /// Emits one numeric binary operation after any schema-owned SELECT projection.
    /// </summary>
    /// <param name="expression">The bound binary expression.</param>
    /// <param name="operation">The normalized operator spelling.</param>
    /// <param name="left">The bound left operand.</param>
    /// <param name="leftCode">The projected left value.</param>
    /// <param name="right">The bound right operand.</param>
    /// <param name="rightCode">The projected right value.</param>
    /// <param name="leftType">The runtime-proven left scalar kind, when narrower than the bound type.</param>
    /// <param name="rightType">The runtime-proven right scalar kind, when narrower than the bound type.</param>
    /// <returns>The statically typed C# numeric operation.</returns>
    internal static string EmitNumericBinary(
        ExpressBoundExpression expression,
        string operation,
        ExpressBoundExpression left,
        string leftCode,
        ExpressBoundExpression right,
        string rightCode,
        ExpressExpressionTypeKind? leftType = null,
        ExpressExpressionTypeKind? rightType = null)
    {
        var target = expression.Type.Kind;
        if (operation == "/")
        {
            target = ExpressExpressionTypeKind.Real;
        }
        else if (operation is "DIV" or "MOD")
        {
            target = ExpressExpressionTypeKind.Integer;
        }

        var promotedLeft = PromoteNumeric(left, leftCode, target, leftType);
        var promotedRight = PromoteNumeric(right, rightCode, target, rightType);
        if (operation is "DIV" or "MOD")
        {
            return EmitIntegerDivisionCore(
                expression,
                promotedLeft,
                promotedRight,
                operation == "MOD");
        }

        if (operation != "/")
        {
            return $"(({promotedLeft}) {operation} ({promotedRight}))";
        }

        var suffix = expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
            + "_"
            + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture);
        var leftValue = "__expressDividend_" + suffix;
        var rightValue = "__expressDivisor_" + suffix;
        var quotient = "__expressQuotient_" + suffix;
        return "((global::System.Func<global::TedToolkit.Step21.RealValue, "
            + "global::TedToolkit.Step21.RealValue, global::TedToolkit.Step21.RealValue?>)("
            + $"({leftValue}, {rightValue}) => {{ if ({rightValue}.Significand.IsZero) {{ return null; }} "
            + $"var {quotient} = {leftValue}.ToDouble() / {rightValue}.ToDouble(); "
            + $"return !global::System.Double.IsNaN({quotient}) "
            + $"&& !global::System.Double.IsInfinity({quotient}) "
            + $"? global::TedToolkit.Step21.RealValue.FromDouble({quotient}) : null; }}))"
            + $"({promotedLeft}, {promotedRight})";
    }

    private static string EmitValueIn(ExpressBoundExpression expression, string[] arguments)
    {
        var contains = $"global::System.Linq.Enumerable.Contains(({arguments[0]}), ({arguments[1]}))";
        if (expression.Children[0].Type.DeclaredType is not ExpressBoundAggregateType
            {
                Kind: ExpressAggregateKind.Array,
                IsOptional: true,
            })
        {
            return LogicalComparison(contains);
        }

        var allSet = AllArraySlotsSet(expression, arguments[0]);
        return $"({contains} ? global::TedToolkit.Step21.LogicalValue.True : {allSet} "
            + "? global::TedToolkit.Step21.LogicalValue.False : global::TedToolkit.Step21.LogicalValue.Unknown)";
    }

    private static string EmitFormat(
        ExpressBoundExpression expression,
        ExpressBoundExpression number,
        string[] arguments)
    {
        var suffix = expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
            + "_"
            + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture);
        var value = "__expressFormatValue_" + suffix;
        var format = "__expressFormatText_" + suffix;
        return "((global::System.Func<global::TedToolkit.Step21.NumberValue, global::System.String, "
            + $"global::System.String?>)(({value}, {format}) => {{ try {{ return {value}.Format({format}); }} "
            + "catch (global::System.ArgumentException) { return null; } }))"
            + $"({PromoteNumeric(number, arguments[0], ExpressExpressionTypeKind.Number)}, {arguments[1]})";
    }

    private static string EmitValueUnique(ExpressBoundExpression expression, string argument)
    {
        var unique = "global::System.Linq.Enumerable.Count("
            + $"global::System.Linq.Enumerable.Distinct(({argument}))) "
            + $"== global::System.Linq.Enumerable.Count(({argument}))";
        if (expression.Children[0].Type.DeclaredType is not ExpressBoundAggregateType
            {
                Kind: ExpressAggregateKind.Array,
                IsOptional: true,
            })
        {
            return LogicalComparison(unique);
        }

        var allSet = AllArraySlotsSet(expression, argument);
        return $"(!({unique}) ? global::TedToolkit.Step21.LogicalValue.False : {allSet} "
            + "? global::TedToolkit.Step21.LogicalValue.True : global::TedToolkit.Step21.LogicalValue.Unknown)";
    }

    private static string AllArraySlotsSet(ExpressBoundExpression expression, string argument)
    {
        var suffix = expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
            + "_"
            + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture);
        var index = "__expressArrayIndex_" + suffix;
        return "global::System.Linq.Enumerable.All(global::System.Linq.Enumerable.Range("
            + $"({argument}).LowerIndex, ({argument}).Count), {index} => ({argument}).IsSet({index}))";
    }

    private static string EmitAggregateBinary(
        ExpressBoundExpression expression,
        string operation,
        ExpressBoundExpression left,
        string leftCode,
        ExpressBoundExpression right,
        string rightCode,
        ExpressExpressionEmissionContext context)
    {
        var resultType = TypeName(expression.Type.WithIndeterminate(false), context);
        var resultAggregate = expression.Type.DeclaredType as ExpressBoundAggregateType
            ?? (expression.Type.DeclaredType is { } declaredType
                ? context.ResolveAggregateType?.Invoke(declaredType)
                : null)
            ?? throw GenerationError(expression, "Aggregate operation has no resolved aggregate result domain.");
        var leftIsAggregate = left.Type.Kind == ExpressExpressionTypeKind.Aggregate
            || (left.Type.DeclaredType is { } leftDeclared
                && context.ResolveAggregateType?.Invoke(leftDeclared) is not null);
        var rightIsAggregate = right.Type.Kind == ExpressExpressionTypeKind.Aggregate
            || (right.Type.DeclaredType is { } rightDeclared
                && context.ResolveAggregateType?.Invoke(rightDeclared) is not null);
        string enumerable;
        if (operation == "+")
        {
            if ((leftIsAggregate || rightIsAggregate)
                && resultAggregate.Kind is ExpressAggregateKind.List
                    or ExpressAggregateKind.Bag
                    or ExpressAggregateKind.Set
                && !resultAggregate.IsOptional
                && left.Type.DeclaredType is not ExpressBoundAggregateType { IsOptional: true, }
                && right.Type.DeclaredType is not ExpressBoundAggregateType { IsOptional: true, }
                && (resultAggregate.ElementType is ExpressBoundNamedType
                    || resultAggregate.ElementType is ExpressBoundGenericType)
                && (!ReferenceEquals(
                        (left.Type.DeclaredType as ExpressBoundAggregateType)?.ElementType
                            ?? left.Type.DeclaredType,
                        resultAggregate.ElementType)
                    || !ReferenceEquals(
                        (right.Type.DeclaredType as ExpressBoundAggregateType)?.ElementType
                            ?? right.Type.DeclaredType,
                        resultAggregate.ElementType)))
            {
                return context.ResolveModelFunction is not null
                    ? context.ResolveModelFunction("AGGREGATE_UNION", expression, [leftCode, rightCode,])
                    : throw GenerationError(
                        expression,
                        "A sole-alternative SELECT aggregate union requires an enclosing generated model operation.");
            }

            enumerable = (leftIsAggregate, rightIsAggregate) switch
            {
                (true, true) => $"global::System.Linq.Enumerable.Concat(({leftCode}), ({rightCode}))",
                (true, false) => $"global::System.Linq.Enumerable.Append(({leftCode}), ({rightCode}))",
                (false, true) => $"global::System.Linq.Enumerable.Prepend(({rightCode}), ({leftCode}))",
                _ => throw new InvalidOperationException("Aggregate union requires at least one aggregate operand."),
            };
            if (resultAggregate.Kind == ExpressAggregateKind.Set)
            {
                enumerable = $"global::System.Linq.Enumerable.Distinct({enumerable})";
            }
        }
        else if (operation == "*")
        {
            enumerable = resultAggregate.Kind == ExpressAggregateKind.Set
                ? $"global::System.Linq.Enumerable.Intersect(({leftCode}), ({rightCode}))"
                : BagIntersection(leftCode, rightCode);
        }
        else if (resultAggregate.Kind == ExpressAggregateKind.Set)
        {
            enumerable = rightIsAggregate
                ? $"global::System.Linq.Enumerable.Except(({leftCode}), ({rightCode}))"
                : $"global::System.Linq.Enumerable.Where(({leftCode}), __item => "
                    + "!global::System.Object.Equals("
                    + $"__item, ({rightCode})))";
        }
        else
        {
            enumerable = BagDifference(leftCode, rightCode, rightIsAggregate);
        }

        return $"({resultType})[..{enumerable}]";
    }

    private static string BagIntersection(string left, string right)
    {
        return "global::System.Linq.Enumerable.SelectMany("
            + $"global::System.Linq.Enumerable.GroupBy(({left}), __item => __item), __group => "
            + "global::System.Linq.Enumerable.Repeat(__group.Key, global::System.Math.Min("
            + "global::System.Linq.Enumerable.Count(__group), "
            + $"global::System.Linq.Enumerable.Count(({right}), __candidate => "
            + "global::System.Object.Equals("
            + "__candidate, __group.Key)))))";
    }

    private static string BagDifference(
        string left,
        string right,
        bool rightIsAggregate)
    {
        var removedCount = rightIsAggregate
            ? $"global::System.Linq.Enumerable.Count(({right}), __candidate => "
                + "global::System.Object.Equals("
                + "__candidate, __group.Key))"
            : "(global::System.Object.Equals("
                + $"({right}), __group.Key) ? 1 : 0)";
        return "global::System.Linq.Enumerable.SelectMany("
            + $"global::System.Linq.Enumerable.GroupBy(({left}), __item => __item), __group => "
            + "global::System.Linq.Enumerable.Repeat(__group.Key, global::System.Math.Max(0, "
            + $"global::System.Linq.Enumerable.Count(__group) - {removedCount})))";
    }

    private static string AggregateSubset(
        string operation,
        string leftCode,
        string rightCode)
    {
        const string subset = "global::System.Linq.Enumerable.All("
            + "global::System.Linq.Enumerable.GroupBy(({0}), __item => __item), __group => "
            + "global::System.Linq.Enumerable.Count(({1}), __candidate => "
            + "global::System.Object.Equals("
            + "__candidate, __group.Key)) >= global::System.Linq.Enumerable.Count(__group))";
        return operation == "<="
            ? string.Format(CultureInfo.InvariantCulture, subset, leftCode, rightCode)
            : string.Format(CultureInfo.InvariantCulture, subset, rightCode, leftCode);
    }

    /// <summary>
    /// Promotes one emitted numeric value to a compatible wider numeric representation.
    /// </summary>
    /// <param name="expression">The statically bound source value.</param>
    /// <param name="code">The emitted source value expression.</param>
    /// <param name="target">The wider numeric result kind.</param>
    /// <param name="sourceType">The branch-proven scalar source kind, when narrower than the bound expression.</param>
    /// <returns>The promoted value expression, or the original expression when no promotion applies.</returns>
    internal static string PromoteNumeric(
        ExpressBoundExpression expression,
        string code,
        ExpressExpressionTypeKind target,
        ExpressExpressionTypeKind? sourceType = null)
    {
        var source = sourceType ?? expression.Type.Kind;
        if (source == target)
        {
            return code;
        }

        return target switch
        {
            ExpressExpressionTypeKind.Number when source == ExpressExpressionTypeKind.Integer =>
                $"global::TedToolkit.Step21.NumberValue.FromInteger({code})",
            ExpressExpressionTypeKind.Number when source == ExpressExpressionTypeKind.Real =>
                $"global::TedToolkit.Step21.NumberValue.FromReal({code})",
            ExpressExpressionTypeKind.Real when source == ExpressExpressionTypeKind.Integer =>
                $"new global::TedToolkit.Step21.RealValue(({code}), global::System.Numerics.BigInteger.Zero)",
            ExpressExpressionTypeKind.Real when source == ExpressExpressionTypeKind.Number =>
                $"({code}).ToReal()",
            _ => code,
        };
    }

    private static string AsInteger(ExpressBoundExpression expression, string code)
    {
        return expression.Type.Kind switch
        {
            ExpressExpressionTypeKind.Integer => code,
            ExpressExpressionTypeKind.Real =>
                $"global::TedToolkit.Step21.NumberValue.FromReal({code}).ToIntegerTruncated()",
            ExpressExpressionTypeKind.Number => $"({code}).ToIntegerTruncated()",
            _ => code,
        };
    }

    private static string EmitIntegerDivision(
        ExpressBoundExpression expression,
        ExpressBoundExpression left,
        string leftCode,
        ExpressBoundExpression right,
        string rightCode,
        bool modulo)
    {
        return EmitIntegerDivisionCore(
            expression,
            AsInteger(left, leftCode),
            AsInteger(right, rightCode),
            modulo);
    }

    private static string EmitIntegerDivisionCore(
        ExpressBoundExpression expression,
        string leftCode,
        string rightCode,
        bool modulo)
    {
        var suffix = expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
            + "_"
            + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture);
        var dividend = "__expressDividend_" + suffix;
        var divisor = "__expressDivisor_" + suffix;
        var remainder = "__expressRemainder_" + suffix;
        var values = $"(({leftCode}), ({rightCode})) switch {{ "
            + $"var ({dividend}, {divisor}) => ";
        if (modulo)
        {
            return values
                + $"{divisor}.IsZero ? (global::System.Numerics.BigInteger?)null : "
                + $"global::System.Numerics.BigInteger.Remainder({dividend}, {divisor}) is var {remainder} "
                + $"? ({remainder}.IsZero || {remainder}.Sign == {divisor}.Sign "
                + $"? {remainder} : {remainder} + {divisor}) : default }}";
        }

        return values
            + $"{divisor}.IsZero ? (global::System.Numerics.BigInteger?)null : "
            + $"global::System.Numerics.BigInteger.DivRem({dividend}, {divisor}, out var {remainder}) "
            + $"- (!{remainder}.IsZero && {remainder}.Sign != {divisor}.Sign "
            + "? global::System.Numerics.BigInteger.One : global::System.Numerics.BigInteger.Zero) }";
    }

    private static string EmitIntegerPower(
        ExpressBoundExpression expression,
        string leftCode,
        ExpressBoundExpression right,
        string rightCode)
    {
        var suffix = expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
            + "_"
            + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture);
        var exponent = "__expressExponent_" + suffix;
        return $"(({AsInteger(right, rightCode)}) is var {exponent} && {exponent}.Sign >= 0 "
            + $"&& {exponent} <= global::System.Int32.MaxValue ? "
            + $"global::System.Numerics.BigInteger.Pow(({leftCode}), (int){exponent}) : "
            + "(global::System.Numerics.BigInteger?)null)";
    }

    private static string EmitValueComparison(
        ExpressBoundExpression expression,
        ExpressBoundExpression left,
        string leftCode,
        ExpressBoundExpression right,
        string rightCode,
        ExpressExpressionEmissionContext context,
        bool negated)
    {
        if (left.Type.DeclaredType is ExpressBoundAggregateType aggregate
            && aggregate.Kind == ExpressAggregateKind.Array
            && aggregate.IsOptional
            && !ExpressTypeAnalysis.RequiresSchemaValueEquality(aggregate.ElementType))
        {
            return EmitOptionalArrayValueComparison(
                expression,
                aggregate,
                leftCode,
                rightCode,
                negated);
        }

        if (ExpressTypeAnalysis.RequiresSchemaValueEquality(left.Type)
            || ExpressTypeAnalysis.RequiresSchemaValueEquality(right.Type))
        {
            var equality = context.ResolveValueEquality is null
                ? throw GenerationError(
                    expression,
                    "Entity-containing value equality requires an enclosing generated schema operation.")
                : context.ResolveValueEquality(left, leftCode, right, rightCode, null);
            return negated ? EmitNot(equality) : equality;
        }

        var valueEquality = ValueEqualityCore(left, leftCode, right, rightCode, context);
        return LogicalComparison(negated ? $"!({valueEquality})" : valueEquality);
    }

    private static string EmitOptionalArrayValueComparison(
        ExpressBoundExpression expression,
        ExpressBoundAggregateType aggregate,
        string leftCode,
        string rightCode,
        bool negated)
    {
        var suffix = expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
            + "_"
            + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture);
        var left = "__expressLeftArray_" + suffix;
        var right = "__expressRightArray_" + suffix;
        var index = "__expressArrayIndex_" + suffix;
        var elementType = BoundTypeName(aggregate.ElementType);
        var sameBounds = $"{left}.LowerIndex == {right}.LowerIndex && {left}.UpperIndex == {right}.UpperIndex";
        var mismatch = "global::System.Linq.Enumerable.Any(global::System.Linq.Enumerable.Range("
            + $"{left}.LowerIndex, {left}.Count), {index} => {left}.IsSet({index}) != {right}.IsSet({index}) "
            + $"|| ({left}.TryGetValue({index}, out var __expressLeftItem_{suffix}) "
            + $"&& {right}.TryGetValue({index}, out var __expressRightItem_{suffix}) "
            + $"&& !global::System.Collections.Generic.EqualityComparer<{elementType}>.Default.Equals("
            + $"__expressLeftItem_{suffix}, __expressRightItem_{suffix})))";
        var allSet = "global::System.Linq.Enumerable.All(global::System.Linq.Enumerable.Range("
            + $"{left}.LowerIndex, {left}.Count), {index} => {left}.IsSet({index}) && {right}.IsSet({index}))";
        var equalResult = negated
            ? "global::TedToolkit.Step21.LogicalValue.False"
            : "global::TedToolkit.Step21.LogicalValue.True";
        var unequalResult = negated
            ? "global::TedToolkit.Step21.LogicalValue.True"
            : "global::TedToolkit.Step21.LogicalValue.False";
        return $"(({leftCode}), ({rightCode})) switch {{ var ({left}, {right}) => "
            + $"!({sameBounds}) || {mismatch} ? {unequalResult} : {allSet} ? {equalResult} : "
            + "global::TedToolkit.Step21.LogicalValue.Unknown }";
    }

    private static string EmitMembership(
        ExpressBoundExpression expression,
        ExpressBoundExpression valueExpression,
        string value,
        ExpressBoundExpression aggregateExpression,
        string aggregate,
        ExpressExpressionEmissionContext context)
    {
        if (aggregateExpression.Type.DeclaredType is ExpressBoundAggregateType aggregateType
            && valueExpression.Type.Kind is ExpressExpressionTypeKind.Entity or ExpressExpressionTypeKind.Select
            && aggregateType.ElementType is ExpressBoundSelectType
                or ExpressBoundNamedType
            {
                Declaration.Kind: not ExpressDeclarationKind.Entity,
            })
        {
            var resolve = context.ResolveValueEquality
                ?? throw GenerationError(
                    expression,
                    "SELECT membership requires an enclosing generated schema operation.");
            var suffix = expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                + "_"
                + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture);
            var element = "__expressMember_" + suffix;
            var state = "__expressMembershipState_" + suffix;
            var match = "__expressMembershipMatch_" + suffix;
            var equality = resolve(
                aggregateExpression,
                element,
                valueExpression,
                value,
                aggregateType.ElementType);
            return "global::System.Linq.Enumerable.Aggregate("
                + $"global::System.Linq.Enumerable.Select(({aggregate}), {element} => {equality}), "
                + "global::TedToolkit.Step21.LogicalValue.False, "
                + $"({state}, {match}) => {state} == global::TedToolkit.Step21.LogicalValue.True "
                + $"|| {match} == global::TedToolkit.Step21.LogicalValue.True "
                + "? global::TedToolkit.Step21.LogicalValue.True : "
                + $"{state} == global::TedToolkit.Step21.LogicalValue.Unknown "
                + $"|| {match} == global::TedToolkit.Step21.LogicalValue.Unknown "
                + "? global::TedToolkit.Step21.LogicalValue.Unknown "
                + ": global::TedToolkit.Step21.LogicalValue.False)";
        }

        var contains = "global::System.Linq.Enumerable.Any("
            + $"({aggregate}), __candidate => global::System.Object.Equals(__candidate, ({value})))";
        if (aggregateExpression.Type.DeclaredType is not ExpressBoundAggregateType
            {
                Kind: ExpressAggregateKind.Array,
                IsOptional: true,
            })
        {
            return LogicalComparison(contains);
        }

        var allSet = AllArraySlotsSet(expression, aggregate);
        return $"({contains} ? global::TedToolkit.Step21.LogicalValue.True : {allSet} "
            + "? global::TedToolkit.Step21.LogicalValue.False : global::TedToolkit.Step21.LogicalValue.Unknown)";
    }

    /// <summary>
    /// Emits statically typed EXPRESS value equality for two bound operands.
    /// </summary>
    /// <param name="left">The left bound operand.</param>
    /// <param name="leftCode">The generated left operand.</param>
    /// <param name="right">The right bound operand.</param>
    /// <param name="rightCode">The generated right operand.</param>
    /// <param name="context">The schema context for nominal aggregate operands, when available.</param>
    /// <returns>The generated Boolean equality expression.</returns>
    internal static string ValueEqualityCore(
        ExpressBoundExpression left,
        string leftCode,
        ExpressBoundExpression right,
        string rightCode,
        ExpressExpressionEmissionContext? context = null)
    {
        var leftAggregate = left.Type.DeclaredType as ExpressBoundAggregateType
            ?? (left.Type.DeclaredType is { } declared ? context?.ResolveAggregateType?.Invoke(declared) : null);
        if (leftAggregate is not null)
        {
            if (leftAggregate.Kind is ExpressAggregateKind.Array or ExpressAggregateKind.List)
            {
                return $"global::System.Linq.Enumerable.SequenceEqual(({leftCode}), ({rightCode}))";
            }

            return $"({AggregateSubset("<=", leftCode, rightCode)}) && "
                + $"({AggregateSubset(">=", leftCode, rightCode)})";
        }

        if (IsNumeric(left.Type.Kind) && IsNumeric(right.Type.Kind))
        {
            var target = NumericComparisonType(left.Type.Kind, right.Type.Kind);
            var promotedLeft = PromoteNumeric(left, leftCode, target);
            var promotedRight = PromoteNumeric(right, rightCode, target);
            return target == ExpressExpressionTypeKind.Number
                ? $"({promotedLeft}).CompareTo({promotedRight}) == 0"
                : $"({promotedLeft}) == ({promotedRight})";
        }

        if (left.Type.Kind is ExpressExpressionTypeKind.Boolean or ExpressExpressionTypeKind.Logical
            && right.Type.Kind is ExpressExpressionTypeKind.Boolean or ExpressExpressionTypeKind.Logical)
        {
            return $"({AsLogical(left, leftCode)}) == ({AsLogical(right, rightCode)})";
        }

        var comparisonType = left.Type.DefinedValueDepth > 0
            && left.Type.DeclaredType is { } declaredType
            && context?.ResolveDefinedValueType?.Invoke(declaredType) is { } resolvedType
                ? BoundTypeName(resolvedType)
                : TypeName(left.Type, context);
        return $"global::System.Collections.Generic.EqualityComparer<{comparisonType}>"
            + $".Default.Equals(({leftCode}), ({rightCode}))";
    }

    /// <summary>
    /// Emits the TRUE-only value-equality predicate required when selecting an EXPRESS CASE label.
    /// </summary>
    /// <param name="selector">The CASE selector.</param>
    /// <param name="selectorCode">The generated, once-evaluated selector value.</param>
    /// <param name="label">The candidate CASE label.</param>
    /// <param name="labelCode">The generated label value.</param>
    /// <param name="context">The enclosing schema emission context.</param>
    /// <returns>A Boolean predicate which is true only for EXPRESS TRUE equality.</returns>
    /// <exception cref="InvalidOperationException">Schema value equality has no static schema context.</exception>
    internal static string CaseValueMatches(
        ExpressBoundExpression selector,
        string selectorCode,
        ExpressBoundExpression label,
        string labelCode,
        ExpressExpressionEmissionContext context)
    {
        if (selector.Kind == ExpressExpressionKind.Indeterminate
            || label.Kind == ExpressExpressionKind.Indeterminate)
        {
            return "false";
        }

        var selectorValue = selectorCode;
        var labelValue = labelCode;
        var conditions = new List<string>();
        if (MayEmitIndeterminate(selector, context))
        {
            var presentSelector = context.AllocateTemporaryName("__expressCaseSelector");
            conditions.Add($"({selectorCode}) is {{ }} {presentSelector}");
            selectorValue = presentSelector;
        }

        if (MayEmitIndeterminate(label, context))
        {
            var presentLabel = context.AllocateTemporaryName("__expressCaseLabel");
            conditions.Add($"({labelCode}) is {{ }} {presentLabel}");
            labelValue = presentLabel;
        }

        string match;
        if (ExpressTypeAnalysis.RequiresSchemaValueEquality(selector.Type)
            || ExpressTypeAnalysis.RequiresSchemaValueEquality(label.Type))
        {
            var equality = context.ResolveValueEquality is null
                ? throw GenerationError(
                    selector,
                    "Entity-containing CASE equality requires an enclosing generated schema operation.")
                : context.ResolveValueEquality(selector, selectorValue, label, labelValue, null);
            match = $"({equality}) == global::TedToolkit.Step21.LogicalValue.True";
        }
        else
        {
            match = ValueEqualityCore(selector, selectorValue, label, labelValue, context);
        }

        return conditions.Count == 0
            ? match
            : $"{string.Join(" && ", conditions)} && ({match})";
    }

    private static string InstanceEquality(
        ExpressBoundExpression expression,
        ExpressBoundExpression left,
        string leftCode,
        ExpressBoundExpression right,
        string rightCode,
        ExpressExpressionEmissionContext context)
    {
        if (left.Type.Kind == ExpressExpressionTypeKind.Select
            || right.Type.Kind == ExpressExpressionTypeKind.Select)
        {
            return context.ResolveModelFunction is not null
                ? context.ResolveModelFunction("INSTANCE_EQUALITY", expression, [leftCode, rightCode,])
                : throw GenerationError(
                    expression,
                    "SELECT instance equality requires an enclosing generated model operation.");
        }

        if (left.Type.Kind == ExpressExpressionTypeKind.Entity
            || left.Type.DeclaredType is ExpressBoundGenericType { IsEntity: true, })
        {
            return $"global::System.Object.ReferenceEquals(({leftCode}), ({rightCode}))";
        }

        return left.Type.Kind == ExpressExpressionTypeKind.Aggregate
            ? ValueEqualityCore(left, leftCode, right, rightCode)
            : ValueEqualityCore(left, leftCode, right, rightCode);
    }

    private static string OrderedComparison(
        string operation,
        ExpressBoundExpression left,
        string leftCode,
        ExpressBoundExpression right,
        string rightCode,
        ExpressExpressionEmissionContext context)
    {
        var leftKind = left.Reference is { } leftReference
            && context.ResolveLexicalBound?.Invoke(leftReference.Name)?.Type is ExpressBoundScalarType leftScalar
                ? leftScalar.Kind switch
                {
                    ExpressScalarKind.Integer => ExpressExpressionTypeKind.Integer,
                    ExpressScalarKind.Number => ExpressExpressionTypeKind.Number,
                    ExpressScalarKind.Real => ExpressExpressionTypeKind.Real,
                    _ => left.Type.Kind,
                }
                : left.Type.Kind;
        var rightKind = right.Reference is { } rightReference
            && context.ResolveLexicalBound?.Invoke(rightReference.Name)?.Type is ExpressBoundScalarType rightScalar
                ? rightScalar.Kind switch
                {
                    ExpressScalarKind.Integer => ExpressExpressionTypeKind.Integer,
                    ExpressScalarKind.Number => ExpressExpressionTypeKind.Number,
                    ExpressScalarKind.Real => ExpressExpressionTypeKind.Real,
                    _ => right.Type.Kind,
                }
                : right.Type.Kind;
        if (IsNumeric(leftKind) && IsNumeric(rightKind))
        {
            var target = NumericComparisonType(leftKind, rightKind);
            return $"(({PromoteNumeric(left, leftCode, target, leftKind)}) {operation} "
                + $"({PromoteNumeric(right, rightCode, target, rightKind)}))";
        }

        if (leftKind == ExpressExpressionTypeKind.String)
        {
            return $"global::System.StringComparer.Ordinal.Compare(({leftCode}), ({rightCode})) "
                + $"{operation} 0";
        }

        if (leftKind == ExpressExpressionTypeKind.Binary)
        {
            return $"global::System.StringComparer.Ordinal.Compare(({leftCode}).ToString(), "
                + $"({rightCode}).ToString()) {operation} 0";
        }

        if (leftKind == ExpressExpressionTypeKind.Enumeration
            && left.Type.EnumerationValues is { } values)
        {
            var order = "new global::System.String[] { "
                + $"{string.Join(", ", values.Select(value => Literal(value.ToUpperInvariant())))} }}";
            return $"global::System.Array.IndexOf({order}, ({leftCode}).Value) {operation} "
                + $"global::System.Array.IndexOf({order}, ({rightCode}).Value)";
        }

        if (leftKind is ExpressExpressionTypeKind.Boolean or ExpressExpressionTypeKind.Logical)
        {
            return $"global::System.Convert.ToInt32({AsLogical(left, leftCode)}) {operation} "
                + $"global::System.Convert.ToInt32({AsLogical(right, rightCode)})";
        }

        return $"(({leftCode}) {operation} ({rightCode}))";
    }

    private static ExpressExpressionTypeKind NumericComparisonType(
        ExpressExpressionTypeKind left,
        ExpressExpressionTypeKind right)
    {
        if (left == ExpressExpressionTypeKind.Number || right == ExpressExpressionTypeKind.Number)
        {
            return ExpressExpressionTypeKind.Number;
        }

        return left == ExpressExpressionTypeKind.Real || right == ExpressExpressionTypeKind.Real
            ? ExpressExpressionTypeKind.Real
            : ExpressExpressionTypeKind.Integer;
    }

    private static bool IsNumeric(ExpressExpressionTypeKind kind)
    {
        return kind is ExpressExpressionTypeKind.Integer
            or ExpressExpressionTypeKind.Number
            or ExpressExpressionTypeKind.Real;
    }

    private static string GuardIndeterminate(
        ExpressBoundExpression result,
        ExpressExpressionEmissionContext context,
        IReadOnlyList<ExpressBoundExpression> operands,
        string[] codes,
        Func<string[], string> emitPresent,
        string? determinateResultTypeName = null)
    {
        var determinateResultType = result.Type.WithIndeterminate(false);
        var fallbackType = determinateResultTypeName ?? (result.Type.DeclaredType is ExpressBoundGenericType generic
            ? BoundTypeName(generic)
            : EmittedValueTypeName(determinateResultType, context));
        var fallback = result.Type.Kind switch
        {
            ExpressExpressionTypeKind.Logical => "global::TedToolkit.Step21.LogicalValue.Unknown",
            ExpressExpressionTypeKind.Generic => $"default({fallbackType})",
            _ => $"({fallbackType}?)null",
        };
        if (operands.Any(operand => operand.Kind == ExpressExpressionKind.Indeterminate))
        {
            return fallback;
        }

        var presentCodes = codes.ToArray();
        var conditions = new List<string>();
        for (var index = 0; index < operands.Count; index++)
        {
            if (!MayEmitIndeterminate(operands[index], context))
            {
                continue;
            }

            var location = operands[index].Span.Start;
            var variablePrefix = "__expressPresent_"
                + location.Line.ToString(CultureInfo.InvariantCulture)
                + "_"
                + location.Column.ToString(CultureInfo.InvariantCulture)
                + "_"
                + index.ToString(CultureInfo.InvariantCulture)
                + "_";
            var variable = context.AllocateTemporaryName(variablePrefix);
            conditions.Add($"({codes[index]}) is {{ }} {variable}");
            presentCodes[index] = variable;
        }

        if (conditions.Count == 0)
        {
            return emitPresent(presentCodes);
        }

        return $"({string.Join(" && ", conditions)} ? ({emitPresent(presentCodes)}) : {fallback})";
    }

    private static bool MayEmitIndeterminate(
        ExpressBoundExpression expression,
        ExpressExpressionEmissionContext context)
    {
        if (context.IsKnownDeterminate?.Invoke(expression) == true)
        {
            return false;
        }

        if (expression.Kind == ExpressExpressionKind.IndexQualifier
            && expression.Children.Count >= 2
            && expression.Children[0].Reference is { } aggregateReference
            && expression.Children[1].Reference is { } indexReference
            && context.SafeIndices.Any(safeIndex =>
                ReferenceEquals(safeIndex.Key, aggregateReference)
                && ReferenceEquals(safeIndex.Value, indexReference)))
        {
            return false;
        }

        return expression.Kind is ExpressExpressionKind.Indeterminate
                or ExpressExpressionKind.IndexQualifier
            || expression.Type.CanBeIndeterminate
            || context.MayReturnIndeterminate?.Invoke(expression) == true
            || expression.Children.Any(child => MayEmitIndeterminate(child, context));
    }

    private static string EmitValue(ExpressBoundExpression expression, string argument)
    {
        var variable = "__expressNumber_"
            + expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
            + "_"
            + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture);
        return "(global::TedToolkit.Step21.NumberValue.TryParse("
            + $"({argument}), out var {variable}) ? {variable} : "
            + "(global::TedToolkit.Step21.NumberValue?)null)";
    }

    private static string HighIndex(ExpressBoundExpression parameter, string argument)
    {
        return parameter.Type.DeclaredType is ExpressBoundAggregateType { } aggregate
            ? aggregate.Kind switch
            {
                ExpressAggregateKind.Array => $"({argument}).UpperIndex",
                ExpressAggregateKind.Aggregate => GeneralAggregateHighIndex(aggregate, argument),
                _ => $"global::System.Linq.Enumerable.Count({argument})",
            }
            : $"global::System.Linq.Enumerable.Count({argument})";
    }

    private static string HighBound(ExpressBoundExpression parameter, string argument)
    {
        if (parameter.Type.DeclaredType is ExpressBoundAggregateType { Kind: ExpressAggregateKind.Array, })
        {
            return BigIntegerExpression($"({argument}).UpperIndex");
        }

        if (parameter.Type.DeclaredType is ExpressBoundAggregateType
            { Kind: ExpressAggregateKind.Aggregate, } aggregate)
        {
            return GeneralAggregateHighBound(aggregate, argument);
        }

        if (parameter.Type.DeclaredType is ExpressBoundAggregateType
            {
                UpperBoundText: not (null or "?"),
            })
        {
            return BigIntegerExpression($"({argument}).UpperBound.GetValueOrDefault()");
        }

        return $"(({argument}).UpperBound.HasValue ? "
            + $"new global::System.Numerics.BigInteger(({argument}).UpperBound.Value) : "
            + "(global::System.Numerics.BigInteger?)null)";
    }

    private static string LowBound(ExpressBoundExpression parameter, string argument)
    {
        return parameter.Type.DeclaredType is ExpressBoundAggregateType { } aggregate
            ? aggregate.Kind switch
            {
                ExpressAggregateKind.Array => $"({argument}).LowerIndex",
                ExpressAggregateKind.Aggregate => GeneralAggregateLowBound(aggregate, argument),
                _ => $"({argument}).LowerBound",
            }
            : $"({argument}).LowerBound";
    }

    private static string LowIndex(ExpressBoundExpression parameter, string argument)
    {
        return parameter.Type.DeclaredType is ExpressBoundAggregateType { } aggregate
            ? aggregate.Kind switch
            {
                ExpressAggregateKind.Array => $"({argument}).LowerIndex",
                ExpressAggregateKind.Aggregate => GeneralAggregateLowIndex(aggregate, argument),
                _ => "1",
            }
            : "1";
    }

    private static string BigIntegerExpression(string code)
    {
        return $"new global::System.Numerics.BigInteger({code})";
    }

    private static string GeneralAggregateHighBound(ExpressBoundAggregateType aggregate, string argument)
    {
        var elementType = BoundTypeName(aggregate.ElementType);
        var array = $"global::TedToolkit.Step21.IExpressArray<{elementType}>";
        var bag = $"global::TedToolkit.Step21.IExpressBag<{elementType}>";
        var list = $"global::TedToolkit.Step21.IExpressList<{elementType}>";
        var set = $"global::TedToolkit.Step21.IExpressSet<{elementType}>";
        return $"({argument}) switch {{ "
            + $"{array} __array => new global::System.Numerics.BigInteger(__array.UpperIndex), "
            + $"{bag} __bag => __bag.UpperBound.HasValue ? "
            + "new global::System.Numerics.BigInteger(__bag.UpperBound.Value) : "
            + "(global::System.Numerics.BigInteger?)null, "
            + $"{list} __list => __list.UpperBound.HasValue ? "
            + "new global::System.Numerics.BigInteger(__list.UpperBound.Value) : "
            + "(global::System.Numerics.BigInteger?)null, "
            + $"{set} __set => __set.UpperBound.HasValue ? "
            + "new global::System.Numerics.BigInteger(__set.UpperBound.Value) : "
            + "(global::System.Numerics.BigInteger?)null, "
            + "_ => (global::System.Numerics.BigInteger?)null }";
    }

    private static string GeneralAggregateHighIndex(ExpressBoundAggregateType aggregate, string argument)
    {
        var elementType = BoundTypeName(aggregate.ElementType);
        return $"({argument}) is global::TedToolkit.Step21.IExpressArray<{elementType}> __highIndexArray "
            + $"? __highIndexArray.UpperIndex : global::System.Linq.Enumerable.Count<{elementType}>({argument})";
    }

    private static string GeneralAggregateLowBound(ExpressBoundAggregateType aggregate, string argument)
    {
        var elementType = BoundTypeName(aggregate.ElementType);
        return $"({argument}) switch {{ "
            + $"global::TedToolkit.Step21.IExpressArray<{elementType}> __array => __array.LowerIndex, "
            + $"global::TedToolkit.Step21.IExpressBag<{elementType}> __bag => __bag.LowerBound, "
            + $"global::TedToolkit.Step21.IExpressList<{elementType}> __list => __list.LowerBound, "
            + $"global::TedToolkit.Step21.IExpressSet<{elementType}> __set => __set.LowerBound, "
            + "_ => 0 }";
    }

    private static string GeneralAggregateLowIndex(ExpressBoundAggregateType aggregate, string argument)
    {
        var elementType = BoundTypeName(aggregate.ElementType);
        return $"({argument}) is global::TedToolkit.Step21.IExpressArray<{elementType}> __lowIndexArray "
            + "? __lowIndexArray.LowerIndex : 1";
    }

    private static string RealMath(
        ExpressBoundExpression expression,
        string operation,
        ExpressExpressionEmissionContext context,
        params (ExpressBoundExpression Expression, string Code)[] arguments)
    {
        var values = arguments.Select(argument => RealMathValue(
            argument.Expression,
            argument.Code,
            context));
        var suffix = expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
            + "_"
            + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture);
        var result = "__expressMath_" + suffix;
        var invocation = operation == "Log2"
            ? $"global::System.Math.Log({string.Join(", ", values)}, 2.0)"
            : $"global::System.Math.{operation}({string.Join(", ", values)})";
        return $"({invocation} is var {result} "
            + $"&& !global::System.Double.IsNaN({result}) "
            + $"&& !global::System.Double.IsInfinity({result}) ? "
            + $"global::TedToolkit.Step21.RealValue.FromDouble({result}) : "
            + "(global::TedToolkit.Step21.RealValue?)null)";
    }

    private static string RealMathValue(
        ExpressBoundExpression expression,
        string code,
        ExpressExpressionEmissionContext context)
    {
        if (expression.Type.Kind == ExpressExpressionTypeKind.Integer)
        {
            return $"(double)({code})";
        }

        if (expression.Type.Kind is ExpressExpressionTypeKind.Number or ExpressExpressionTypeKind.Real)
        {
            return $"({code}).ToDouble()";
        }

        if (expression.Reference is { } reference
            && context.ResolveLexicalBound?.Invoke(reference.Name)?.Type is { } carrierType
            && carrierType is not ExpressBoundScalarType
            && context.ResolveNarrowedScalarReference is not null)
        {
            var carrier = context.ResolveReference(reference, null, null, null);
            var target = new ExpressBoundScalarType(
                ExpressScalarKind.Real,
                constraintText: null,
                isFixed: false,
                carrierType.Span);
            if (context.ResolveNarrowedScalarReference(reference, carrierType, carrier, target)
                is { } projected)
            {
                return $"({projected}).ToDouble()";
            }
        }

        var resolved = expression.Type.DeclaredType is { } declared
            ? context.ResolveDefinedValueType?.Invoke(declared)
            : null;
        return resolved is ExpressBoundScalarType scalar
            ? scalar.Kind switch
            {
                ExpressScalarKind.Integer => $"(double)({code})",
                ExpressScalarKind.Number or ExpressScalarKind.Real => $"({code}).ToDouble()",
                _ => NumericToDouble(code),
            }
            : NumericToDouble(code);
    }

    private static string NumericToDouble(string code)
    {
        return $"((object)({code})) switch {{ "
            + "global::System.Numerics.BigInteger __expressInteger => (double)__expressInteger, "
            + "global::TedToolkit.Step21.NumberValue __expressNumber => __expressNumber.ToDouble(), "
            + "global::TedToolkit.Step21.RealValue __expressReal => __expressReal.ToDouble(), "
            + "_ => throw new global::System.InvalidOperationException() }";
    }

    /// <summary>
    /// Converts a BOOLEAN or LOGICAL expression to its generated LOGICAL representation.
    /// </summary>
    /// <param name="expression">The bound expression that supplies determinacy facts.</param>
    /// <param name="code">The generated expression code.</param>
    /// <returns>The generated LOGICAL expression.</returns>
    internal static string AsLogical(ExpressBoundExpression expression, string code)
    {
        if (expression.Type.CanBeIndeterminate)
        {
            return expression.Type.Kind switch
            {
                ExpressExpressionTypeKind.Boolean => $"(((global::System.Boolean?)({code})) switch {{ "
                    + "true => global::TedToolkit.Step21.LogicalValue.True, "
                    + "false => global::TedToolkit.Step21.LogicalValue.False, "
                    + "_ => global::TedToolkit.Step21.LogicalValue.Unknown })",
                ExpressExpressionTypeKind.Logical => $"(({code}) switch {{ "
                    + "global::TedToolkit.Step21.LogicalValue.True => "
                    + "global::TedToolkit.Step21.LogicalValue.True, "
                    + "global::TedToolkit.Step21.LogicalValue.False => "
                    + "global::TedToolkit.Step21.LogicalValue.False, "
                    + "_ => global::TedToolkit.Step21.LogicalValue.Unknown })",
                _ => "global::TedToolkit.Step21.LogicalValue.Unknown",
            };
        }

        return expression.Type.Kind switch
        {
            ExpressExpressionTypeKind.Boolean => $"(((global::System.Boolean?)({code})) switch {{ "
                + "true => global::TedToolkit.Step21.LogicalValue.True, "
                + "false => global::TedToolkit.Step21.LogicalValue.False, "
                + "_ => global::TedToolkit.Step21.LogicalValue.Unknown })",
            ExpressExpressionTypeKind.Indeterminate => "global::TedToolkit.Step21.LogicalValue.Unknown",
            _ => code,
        };
    }

    private static string LogicalComparison(string code)
    {
        return $"(({code}) ? global::TedToolkit.Step21.LogicalValue.True : "
            + "global::TedToolkit.Step21.LogicalValue.False)";
    }

    private static string UnwrapDefined(
        ExpressExpressionType type,
        string code,
        ExpressExpressionEmissionContext context)
    {
        if (type.Kind == ExpressExpressionTypeKind.Entity
            || type.DeclaredType is ExpressBoundNamedType
            { Declaration.Kind: ExpressDeclarationKind.Entity, })
        {
            return code;
        }

        if (type.DeclaredType is ExpressBoundNamedType declared
            && context.ResolveDefinedValueType?.Invoke(declared) is ExpressBoundNamedType semantic
            && ReferenceEquals(declared.Declaration, semantic.Declaration))
        {
            return code;
        }

        if (!type.CanBeIndeterminate)
        {
            for (var index = 0; index < type.DefinedValueDepth; index++)
            {
                code = $"({code}).Value";
            }

            return code;
        }

        var presencePatterns = new List<string>(type.DefinedValueDepth);
        for (var index = 0; index < type.DefinedValueDepth; index++)
        {
            var value = context.AllocateTemporaryName("__expressDefinedValue");
            presencePatterns.Add($"({code}) is {{ }} {value}");
            code = $"({value}).Value";
        }

        return presencePatterns.Count == 0
            ? code
            : $"(({string.Join(" && ", presencePatterns)}) ? {code} : "
                + $"({EmittedValueTypeName(type.WithIndeterminate(false), context)}?)null)";
    }

    private static string EmitNot(string operand)
    {
        return $"(({operand}) switch {{ "
            + "global::TedToolkit.Step21.LogicalValue.False => global::TedToolkit.Step21.LogicalValue.True, "
            + "global::TedToolkit.Step21.LogicalValue.True => global::TedToolkit.Step21.LogicalValue.False, "
            + "_ => global::TedToolkit.Step21.LogicalValue.Unknown })";
    }

    private static string EmitLogicalBinary(string operation, string left, string right)
    {
        const string falseValue = "global::TedToolkit.Step21.LogicalValue.False";
        const string logicalType = "global::TedToolkit.Step21.LogicalValue";
        const string unknownValue = "global::TedToolkit.Step21.LogicalValue.Unknown";
        const string trueValue = "global::TedToolkit.Step21.LogicalValue.True";
        var typedRight = $"(({logicalType})({right}))";
        return operation switch
        {
            "AND" => $"(({left}) switch {{ "
                + $"{falseValue} => {falseValue}, {trueValue} => {typedRight}, "
                + $"_ => {typedRight} switch {{ {falseValue} => {falseValue}, _ => {unknownValue} }} }})",
            "OR" => $"(({left}) switch {{ "
                + $"{trueValue} => {trueValue}, {falseValue} => {typedRight}, "
                + $"_ => {typedRight} switch {{ {trueValue} => {trueValue}, _ => {unknownValue} }} }})",
            "XOR" => $"((({left}), ({right})) switch {{ "
                + $"({unknownValue}, _) or (_, {unknownValue}) => {unknownValue}, "
                + $"({trueValue}, {falseValue}) or ({falseValue}, {trueValue}) => {trueValue}, "
                + $"_ => {falseValue} }})",
            _ => throw new InvalidOperationException($"Unknown logical operation '{operation}'."),
        };
    }

    private static string TypeName(
        ExpressExpressionType type,
        ExpressExpressionEmissionContext? context = null)
    {
        var name = type.Kind switch
        {
            ExpressExpressionTypeKind.Binary => "global::TedToolkit.Step21.BinaryValue",
            ExpressExpressionTypeKind.Boolean => "global::System.Boolean",
            ExpressExpressionTypeKind.Integer => "global::System.Numerics.BigInteger",
            ExpressExpressionTypeKind.Logical => "global::TedToolkit.Step21.LogicalValue",
            ExpressExpressionTypeKind.Number => "global::TedToolkit.Step21.NumberValue",
            ExpressExpressionTypeKind.Real => "global::TedToolkit.Step21.RealValue",
            ExpressExpressionTypeKind.String => "global::System.String",
            ExpressExpressionTypeKind.Aggregate when type.DeclaredType is ExpressBoundAggregateType aggregate =>
                AggregateTypeName(aggregate),
            ExpressExpressionTypeKind.Aggregate when type.DeclaredType is { } declared
                && context?.ResolveAggregateType?.Invoke(declared) is { } underlying =>
                AggregateTypeName(underlying),
            ExpressExpressionTypeKind.Entity when type.DeclaredType is ExpressBoundNamedType entity =>
                GeneratedTypeName(entity.Declaration, entityInterface: true),
            ExpressExpressionTypeKind.Entity => "global::TedToolkit.Step21.Entity",
            ExpressExpressionTypeKind.Enumeration when type.DeclaredType is ExpressBoundNamedType enumeration =>
                GeneratedTypeName(enumeration.Declaration, entityInterface: false),
            ExpressExpressionTypeKind.Select when type.DeclaredType is ExpressBoundNamedType select =>
                GeneratedTypeName(select.Declaration, entityInterface: false),
            ExpressExpressionTypeKind.Generic when type.DeclaredType is ExpressBoundGenericType
            { TypeLabel: { } typeLabel, } generic
                && context?.GenericTypeLabels.Contains(typeLabel, StringComparer.OrdinalIgnoreCase) == true =>
                BoundTypeName(generic),
            ExpressExpressionTypeKind.Generic => "global::System.Object",
            _ => throw new InvalidOperationException(
                $"Expression type '{type.Kind.ToString()}' requires a schema generation context."),
        };
        return type.CanBeIndeterminate ? name + "?" : name;
    }

    private static string EmittedValueTypeName(
        ExpressExpressionType type,
        ExpressExpressionEmissionContext? context)
    {
        if (type.DefinedValueDepth > 0
            && type.DeclaredType is { } declared
            && context?.ResolveDefinedValueType?.Invoke(declared) is { } semanticType)
        {
            var semanticName = BoundTypeName(semanticType);
            return type.CanBeIndeterminate ? semanticName + "?" : semanticName;
        }

        return TypeName(type, context);
    }

    private static string AggregateTypeName(ExpressBoundAggregateType aggregate)
    {
        var definition = aggregate.Kind switch
        {
            ExpressAggregateKind.Array => "ExpressArray",
            ExpressAggregateKind.Bag => "ExpressBag",
            ExpressAggregateKind.List or ExpressAggregateKind.Aggregate => "ExpressList",
            ExpressAggregateKind.Set => "ExpressSet",
            _ => throw new InvalidOperationException(
                $"Unknown aggregate kind '{aggregate.Kind.ToString()}'."),
        };
        return $"global::TedToolkit.Step21.{definition}<{BoundTypeName(aggregate.ElementType)}>";
    }

    /// <summary>
    /// Gets the covariant read-only interface spelling for a bound aggregate type.
    /// </summary>
    /// <param name="aggregate">The bound aggregate type.</param>
    /// <returns>The fully qualified aggregate interface spelling.</returns>
    /// <exception cref="InvalidOperationException">The aggregate kind is unknown.</exception>
    internal static string AggregateInterfaceTypeName(ExpressBoundAggregateType aggregate)
    {
        if (aggregate.Kind == ExpressAggregateKind.Aggregate)
        {
            return "global::System.Collections.Generic.IEnumerable<"
                + BoundTypeName(aggregate.ElementType)
                + ">";
        }

        var definition = aggregate.Kind switch
        {
            ExpressAggregateKind.Array => "IExpressArray",
            ExpressAggregateKind.Bag => "IExpressBag",
            ExpressAggregateKind.List => "IExpressList",
            ExpressAggregateKind.Set => "IExpressSet",
            _ => throw new InvalidOperationException(
                $"Unknown aggregate kind '{aggregate.Kind.ToString()}'."),
        };
        return $"global::TedToolkit.Step21.{definition}<{BoundTypeName(aggregate.ElementType)}>";
    }

    /// <summary>
    /// Gets the generated C# spelling for a bound expression value type.
    /// </summary>
    /// <param name="type">The bound type.</param>
    /// <returns>The fully qualified generated type spelling.</returns>
    /// <exception cref="InvalidOperationException">The bound type has no static generated representation.</exception>
    internal static string BoundTypeName(ExpressBoundType type)
    {
        return type switch
        {
            ExpressBoundScalarType { Kind: ExpressScalarKind.Binary, } =>
                "global::TedToolkit.Step21.BinaryValue",
            ExpressBoundScalarType { Kind: ExpressScalarKind.Boolean, } => "global::System.Boolean",
            ExpressBoundScalarType { Kind: ExpressScalarKind.Integer, } => "global::System.Numerics.BigInteger",
            ExpressBoundScalarType { Kind: ExpressScalarKind.Logical, } =>
                "global::TedToolkit.Step21.LogicalValue",
            ExpressBoundScalarType { Kind: ExpressScalarKind.Number, } =>
                "global::TedToolkit.Step21.NumberValue",
            ExpressBoundScalarType { Kind: ExpressScalarKind.Real, } => "global::TedToolkit.Step21.RealValue",
            ExpressBoundScalarType { Kind: ExpressScalarKind.String, } => "global::System.String",
            ExpressBoundAggregateType aggregate => AggregateTypeName(aggregate),
            ExpressBoundGenericType { IsEntity: true, } => "global::TedToolkit.Step21.Entity",
            ExpressBoundGenericType { TypeLabel: { } label, } =>
                "T" + ExpressEntityProjection.ToPascalCase(label),
            ExpressBoundGenericType => "global::System.Object",
            ExpressBoundNamedType named => GeneratedTypeName(
                named.Declaration,
                named.Declaration.Kind == ExpressDeclarationKind.Entity),
            _ => throw new InvalidOperationException(
                $"Bound type '{type.GetType().Name}' has no static C# expression type."),
        };
    }

    private static string GeneratedTypeName(ExpressBoundSymbol symbol, bool entityInterface)
    {
        var schema = ExpressEntityProjection.ToPascalCase(symbol.DeclaringSchema.Name);
        var name = ExpressEntityProjection.ToPascalCase(symbol.Name);
        var prefix = entityInterface ? "I" : "";
        return $"global::TedToolkit.Step21.Schemas.{schema}.{prefix}{name}";
    }

    private static string DecodeString(ExpressBoundExpression expression)
    {
        var source = expression.SourceText;
        if (source.Length >= 2 && source[0] == '"' && source[source.Length - 1] == '"')
        {
            var encoded = source.Substring(1, source.Length - 2);
            var builder = new StringBuilder(encoded.Length / 2);
            for (var index = 0; index < encoded.Length; index += 8)
            {
                var scalar = int.Parse(
                    encoded.Substring(index, 8),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture);
                try
                {
                    _ = builder.Append(char.ConvertFromUtf32(scalar));
                }
                catch (ArgumentOutOfRangeException exception)
                {
                    throw new InvalidOperationException(
                        $"Encoded STRING at {expression.Span.Start.FilePath}:"
                        + $"{expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)}:"
                        + $"{expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture)} "
                        + $"contains invalid Unicode scalar U+{scalar.ToString("X8", CultureInfo.InvariantCulture)}.",
                        exception);
                }
            }

            return builder.ToString();
        }

        return source.Length >= 2 && source[0] == '\'' && source[source.Length - 1] == '\''
            ? source.Substring(1, source.Length - 2).Replace("''", "'")
            : source;
    }

    private static string Literal(string value)
    {
        var builder = new StringBuilder(value.Length + 2).Append('"');
        foreach (var character in value)
        {
            _ = character switch
            {
                '"' => builder.Append("\\\""),
                '\\' => builder.Append("\\\\"),
                '\r' => builder.Append("\\r"),
                '\n' => builder.Append("\\n"),
                '\t' => builder.Append("\\t"),
                _ => builder.Append(character),
            };
        }

        return builder.Append('"').ToString();
    }

    private static InvalidOperationException GenerationError(
        ExpressBoundExpression expression,
        string message)
    {
        var location = expression.Span.Start;
        return new(
            $"{location.FilePath}:{location.Line.ToString(CultureInfo.InvariantCulture)}:"
            + $"{location.Column.ToString(CultureInfo.InvariantCulture)}: {message}");
    }
}