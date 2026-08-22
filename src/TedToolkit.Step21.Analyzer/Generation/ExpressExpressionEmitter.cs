// -----------------------------------------------------------------------
// <copyright file="ExpressExpressionEmitter.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;
using System.Numerics;
using System.Text;

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
            static reference => throw new InvalidOperationException(
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
        return Emit(expression, new ExpressExpressionEmissionContext(resolveReference));
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
            return new(EmitCode(expression, context), TypeName(expression.Type));
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
        if (expression.Operation?.ToUpperInvariant() != "NOT" && operand.Type.CanBeIndeterminate)
        {
            return GuardIndeterminate(
                expression,
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
        var left = expression.Children[0];
        var right = expression.Children[1];
        var leftCode = EmitCode(left, context);
        var rightCode = EmitCode(right, context);
        var operation = expression.Operation?.ToUpperInvariant();
        if (operation is not ("AND" or "OR" or "XOR")
            && (left.Type.CanBeIndeterminate || right.Type.CanBeIndeterminate))
        {
            return GuardIndeterminate(
                expression,
                [left, right,],
                [leftCode, rightCode,],
                codes => EmitBinaryCore(expression, operation, left, codes[0], right, codes[1], context));
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
                EmitAggregateBinary(expression, operation, left, leftCode, right, rightCode),
            "+" or "-" or "*" or "/" => EmitNumericBinary(
                expression,
                operation,
                left,
                leftCode,
                right,
                rightCode),
            "DIV" => EmitIntegerDivision(expression, left, leftCode, right, rightCode, modulo: false),
            "MOD" => EmitIntegerDivision(expression, left, leftCode, right, rightCode, modulo: true),
            "**" when left.Type.Kind == ExpressExpressionTypeKind.Integer =>
                EmitIntegerPower(expression, leftCode, right, rightCode),
            "**" => RealMath(expression, "Pow", (left, leftCode), (right, rightCode)),
            "||" when left.Type.Kind == ExpressExpressionTypeKind.Binary =>
                $"new global::TedToolkit.Step21.BinaryValue(global::System.String.Concat(({leftCode}).ToString(), ({rightCode}).ToString()))",
            "||" when left.Type.Kind == ExpressExpressionTypeKind.Entity
                && right.Type.Kind == ExpressExpressionTypeKind.Entity
                && context.ResolveModelFunction is not null =>
                context.ResolveModelFunction("COMPLEX_CONSTRUCTOR", [leftCode, rightCode,]),
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
            ":=:" => LogicalComparison(InstanceEquality(expression, left, leftCode, right, rightCode, context)),
            ":<>:" => LogicalComparison(
                $"!({InstanceEquality(expression, left, leftCode, right, rightCode, context)})"),
            "<=" or ">=" when left.Type.Kind == ExpressExpressionTypeKind.Aggregate =>
                LogicalComparison(AggregateSubset(operation, left, leftCode, rightCode)),
            "<" or "<=" or ">" or ">=" => LogicalComparison(
                OrderedComparison(operation, left, leftCode, right, rightCode)),
            "IN" => EmitMembership(expression, leftCode, right, rightCode),
            "LIKE" => LogicalComparison(LikeMatch(leftCode, rightCode)),
            _ => throw new InvalidOperationException($"Operation '{expression.Operation}' is not statically generated."),
        };
    }

    private static string LikeMatch(string input, string pattern)
    {
        const string tokenPattern = "@\"!\\\\[\\s\\S]|![\\s\\S]|\\\\[\\s\\S]|[\\s\\S]\"";
        var tokens = $"global::System.Text.RegularExpressions.Regex.Matches(({pattern}), {tokenPattern})";
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
        var body = GuardIndeterminate(
            expression,
            expression.Children,
            variables,
            codes =>
            {
                var lower = LogicalComparison(OrderedComparison(
                    operations[0],
                    expression.Children[0],
                    codes[0],
                    expression.Children[1],
                    codes[1]));
                var upper = LogicalComparison(OrderedComparison(
                    operations[1],
                    expression.Children[1],
                    codes[1],
                    expression.Children[2],
                    codes[2]));
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
            return UnwrapDefined(expression.Type, context.ResolveReference(expression.Reference));
        }

        return expression.Operation?.ToUpperInvariant() switch
        {
            "E" => EmitReal("2.718281828459045"),
            "PI" => EmitReal("3.141592653589793"),
            "SELF" when context.SelfExpression is not null => context.SelfExpression,
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
                && expression.Children.Any(child => child.Type.CanBeIndeterminate))
            {
                return GuardIndeterminate(
                    expression,
                    expression.Children,
                    arguments,
                    codes => EmitBuiltIn(expression, codes, context));
            }

            return EmitBuiltIn(expression, arguments, context);
        }

        var target = expression.Reference.Kind == ExpressBoundNameKind.Entity
            && expression.Reference.SchemaDeclaration is { } entity
                ? $"new {GeneratedTypeName(entity, entityInterface: false)}"
                : context.ResolveReference(expression.Reference);
        if (expression.Children.Any(child => child.Type.CanBeIndeterminate))
        {
            return GuardIndeterminate(
                expression,
                expression.Children,
                arguments,
                codes => UnwrapDefined(expression.Type, $"{target}({string.Join(", ", codes)})"));
        }

        return UnwrapDefined(expression.Type, $"{target}({string.Join(", ", arguments)})");
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
        if (expression.Children[0].Type.CanBeIndeterminate)
        {
            return GuardIndeterminate(
                expression,
                expression.Children,
                [source,],
                codes => UnwrapDefined(
                    expression.Type,
                    $"({codes[0]}).{ExpressEntityProjection.ToPascalCase(reference.Name)}"));
        }

        return UnwrapDefined(
            expression.Type,
            $"({source}).{ExpressEntityProjection.ToPascalCase(reference.Name)}");
    }

    private static string EmitGroup(
        ExpressBoundExpression expression,
        ExpressExpressionEmissionContext context)
    {
        var reference = expression.Reference?.SchemaDeclaration
            ?? throw new InvalidOperationException(
                $"Group expression '{expression.SourceText}' has no resolved entity.");
        var source = EmitCode(expression.Children.Single(), context);
        if (expression.Children[0].Type.CanBeIndeterminate)
        {
            return GuardIndeterminate(
                expression,
                expression.Children,
                [source,],
                codes => $"(({GeneratedTypeName(reference, entityInterface: true)})({codes[0]}))");
        }

        return $"(({GeneratedTypeName(reference, entityInterface: true)})({source}))";
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
        return GuardIndeterminate(
            expression,
            expression.Children,
            codes,
            present => EmitIndexCore(expression, sourceExpression, present));
    }

    private static string EmitIndexCore(
        ExpressBoundExpression expression,
        ExpressBoundExpression sourceExpression,
        string[] codes)
    {
        var source = codes[0];
        var index = codes[1];
        var suffix = expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
            + "_"
            + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture);
        var value = "__expressIndexed_" + suffix;
        var position = "__expressIndex_" + suffix;
        var nullValue = $"({TypeName(expression.Type.WithIndeterminate(false))}?)null";
        if (sourceExpression.Type.Kind == ExpressExpressionTypeKind.Binary)
        {
            return $"(({source}), checked((int)({index}))) switch {{ "
                + $"var ({value}, {position}) when {position} >= 1 && {position} <= {value}.Length => "
                + $"new global::TedToolkit.Step21.BinaryValue({value}.ToString().Substring({position} - 1, 1)), "
                + $"_ => {nullValue} }}";
        }

        if (sourceExpression.Type.Kind == ExpressExpressionTypeKind.String)
        {
            return $"(({source}), checked((int)({index}))) switch {{ "
                + $"var ({value}, {position}) when {position} >= 1 && {position} <= {value}.Length => "
                + $"{value}.Substring({position} - 1, 1), _ => {nullValue} }}";
        }

        var aggregate = (ExpressBoundAggregateType)sourceExpression.Type.DeclaredType!;
        var samePosition = codes.Length == 3
            ? $" && {position} == checked((int)({codes[2]}))"
            : "";
        if (aggregate.Kind == ExpressAggregateKind.Array)
        {
            var item = "__expressItem_" + suffix;
            return $"(({source}), checked((int)({index}))) switch {{ "
                + $"var ({value}, {position}) when {position} >= {value}.LowerIndex "
                + $"&& {position} <= {value}.UpperIndex{samePosition} "
                + $"&& {value}.TryGetValue({position}, out var {item}) => "
                + $"{UnwrapDefined(expression.Type, item)}, _ => {nullValue} }}";
        }

        return $"(({source}), checked((int)({index}))) switch {{ "
            + $"var ({value}, {position}) when {position} >= 1 "
            + $"&& {position} <= global::System.Linq.Enumerable.Count({value}){samePosition} => "
            + $"{UnwrapDefined(expression.Type, $"global::System.Linq.Enumerable.ElementAt({value}, {position} - 1)")}, "
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

        var items = expression.Children.Select(child =>
        {
            if (child.Kind != ExpressExpressionKind.Repetition)
            {
                return ConvertAggregateElement(child, EmitCode(child, context), aggregate.ElementType);
            }

            var dynamicRepetition = dynamicRepetitions.SingleOrDefault(item => ReferenceEquals(item.Child, child));
            var count = dynamicRepetition.Child is null
                ? EmitCode(child.Children[1], context)
                : dynamicRepetition.Variable;
            return "..global::System.Linq.Enumerable.Repeat("
                + $"{ConvertAggregateElement(child.Children[0], EmitCode(child.Children[0], context), aggregate.ElementType)}, "
                + $"checked((int)({count})))";
        });
        var itemCodes = items.ToArray();
        var resultType = TypeName(expression.Type.WithIndeterminate(false));
        var aggregateCode = $"({resultType})[{string.Join(", ", itemCodes)}]";
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
        if (!int.TryParse(aggregate.LowerBoundText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lower)
            || !int.TryParse(aggregate.UpperBoundText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var upper))
        {
            throw GenerationError(
                expression,
                "ARRAY initializer generation requires statically integral declared bounds.");
        }

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
        if (dynamicRepetitions.Length == 0 && staticCardinality != upper - lower + 1)
        {
            throw GenerationError(
                expression,
                "ARRAY initializer cardinality does not match its declared index domain.");
        }

        var resultType = TypeName(expression.Type.WithIndeterminate(false));
        var suffix = expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
            + "_"
            + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture);
        var result = "__expressArray_" + suffix;
        var values = "__expressArrayValues_" + suffix;
        var index = "__expressArrayIndex_" + suffix;
        var elementType = BoundTypeName(aggregate.ElementType);
        var items = expression.Children.Select(child =>
        {
            var value = child.Kind == ExpressExpressionKind.Repetition ? child.Children[0] : child;
            var item = value.Kind == ExpressExpressionKind.Indeterminate
                ? $"(false, default({elementType})!)"
                : $"(true, {ConvertAggregateElement(value, EmitCode(value, context), aggregate.ElementType)})";
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
        var arrayCode = $"((global::System.Func<{resultType}{(expression.Type.CanBeIndeterminate ? "?" : "")}>)(() => {{ "
            + $"var {values} = ((bool IsSet, {elementType} Value)[])[{string.Join(", ", items)}]; "
            + (expression.Type.CanBeIndeterminate
                ? $"if ({values}.Length != {(upper - lower + 1).ToString(CultureInfo.InvariantCulture)}) return null; "
                : "")
            + $"var {result} = new {resultType}("
            + $"{lower.ToString(CultureInfo.InvariantCulture)}, {upper.ToString(CultureInfo.InvariantCulture)}, "
            + $"isOptional: {(aggregate.IsOptional ? "true" : "false")}, "
            + $"isUnique: {(aggregate.IsUnique ? "true" : "false")}); "
            + $"for (var {index} = 0; {index} < {values}.Length; {index}++) "
            + $"{{ if ({values}[{index}].IsSet) "
            + $"{result}[{lower.ToString(CultureInfo.InvariantCulture)} + {index}] = {values}[{index}].Value; }} "
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

    private static string ConvertAggregateElement(
        ExpressBoundExpression expression,
        string code,
        ExpressBoundType target)
    {
        return target switch
        {
            ExpressBoundScalarType { Kind: ExpressScalarKind.Number, } =>
                PromoteNumeric(expression, code, ExpressExpressionTypeKind.Number),
            ExpressBoundScalarType { Kind: ExpressScalarKind.Real, } =>
                PromoteNumeric(expression, code, ExpressExpressionTypeKind.Real),
            _ => code,
        };
    }

    private static string EmitQuery(
        ExpressBoundExpression expression,
        ExpressExpressionEmissionContext context)
    {
        var sourceExpression = expression.Children[0];
        var source = EmitCode(sourceExpression, context);
        var variableName = "__query_" + ExpressEntityProjection.ToPascalCase(expression.Operation!);
        var predicate = expression.Children[1];
        string ResolveQueryReference(ExpressBoundName reference)
        {
            return reference.Kind == ExpressBoundNameKind.QueryVariable
                && string.Equals(reference.Name, expression.Operation, StringComparison.OrdinalIgnoreCase)
                    ? variableName
                    : context.ResolveReference(reference);
        }

        var predicateCode = EmitCode(predicate, context.WithReferenceResolver(ResolveQueryReference));
        var condition = predicate.Type.Kind == ExpressExpressionTypeKind.Boolean
            ? predicateCode
            : $"({predicateCode}) == global::TedToolkit.Step21.LogicalValue.True";
        return GuardIndeterminate(
            expression,
            [sourceExpression,],
            [source,],
            codes => EmitQueryCore(expression, sourceExpression, codes[0], variableName, condition));
    }

    private static string EmitQueryCore(
        ExpressBoundExpression expression,
        ExpressBoundExpression sourceExpression,
        string source,
        string variableName,
        string condition)
    {
        var aggregate = (ExpressBoundAggregateType)sourceExpression.Type.DeclaredType!;
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
            ExpressAggregateKind.List or ExpressAggregateKind.Aggregate =>
                $"new {resultType}(0, {input}.UpperBound, {input}.IsUnique)",
            ExpressAggregateKind.Set => $"new {resultType}(0, {input}.UpperBound)",
            _ => throw new InvalidOperationException(
                $"Unknown query aggregate kind '{aggregate.Kind.ToString()}'."),
        };
        string body;
        if (aggregate.Kind == ExpressAggregateKind.Array)
        {
            var index = "__expressQueryIndex_" + suffix;
            body = $"for (var {index} = {input}.LowerIndex; {index} <= {input}.UpperIndex; {index}++) {{ "
                + $"if ({input}.TryGetValue({index}, out var {variableName}) && ({condition})) "
                + $"{{ {result}[{index}] = {variableName}; }} }}";
        }
        else
        {
            body = $"foreach (var {variableName} in {input}) {{ if ({condition}) "
                + $"{{ {result}.Add({variableName}); }} }}";
        }

        return $"((global::System.Func<{resultType}, {resultType}>)({input} => {{ "
            + $"var {result} = {factory}; {body} return {result}; }}))({source})";
    }

    private static string EmitBuiltIn(
        ExpressBoundExpression expression,
        string[] arguments,
        ExpressExpressionEmissionContext context)
    {
        var parameters = expression.Children;
        return expression.Operation!.ToUpperInvariant() switch
        {
            "ABS" when parameters[0].Type.Kind == ExpressExpressionTypeKind.Integer =>
                $"global::System.Numerics.BigInteger.Abs({arguments[0]})",
            "ABS" when parameters[0].Type.Kind == ExpressExpressionTypeKind.Number =>
                $"(({arguments[0]}) < global::TedToolkit.Step21.NumberValue.FromInteger(0) ? "
                + $"-({arguments[0]}) : ({arguments[0]}))",
            "ABS" => $"(({arguments[0]}).Significand.Sign < 0 ? -({arguments[0]}) : ({arguments[0]}))",
            "ACOS" => RealMath(expression, "Acos", (parameters[0], arguments[0])),
            "ASIN" => RealMath(expression, "Asin", (parameters[0], arguments[0])),
            "ATAN" when arguments.Length == 2 => RealMath(
                expression,
                "Atan2",
                (parameters[0], arguments[0]),
                (parameters[1], arguments[1])),
            "ATAN" => RealMath(expression, "Atan", (parameters[0], arguments[0])),
            "BLENGTH" or "LENGTH" => BigIntegerExpression($"({arguments[0]}).Length"),
            "EXISTS" => $"({arguments[0]}) is not null",
            "COS" => RealMath(expression, "Cos", (parameters[0], arguments[0])),
            "EXP" => RealMath(expression, "Exp", (parameters[0], arguments[0])),
            "FORMAT" => EmitFormat(expression, parameters[0], arguments),
            "HIBOUND" => HighBound(parameters[0], arguments[0]),
            "HIINDEX" => BigIntegerExpression(HighIndex(parameters[0], arguments[0])),
            "LOBOUND" => BigIntegerExpression(LowBound(parameters[0], arguments[0])),
            "LOINDEX" => BigIntegerExpression(LowIndex(parameters[0], arguments[0])),
            "SIZEOF" => BigIntegerExpression($"global::System.Linq.Enumerable.Count({arguments[0]})"),
            "ODD" => LogicalComparison($"!({arguments[0]}).IsEven"),
            "NVL" => $"((({TypeName(parameters[1].Type.WithIndeterminate(false))}?)({arguments[0]})) "
                + $"?? ({arguments[1]}))",
            "LOG" => RealMath(expression, "Log", (parameters[0], arguments[0])),
            "LOG2" => RealMath(expression, "Log2", (parameters[0], arguments[0])),
            "LOG10" => RealMath(expression, "Log10", (parameters[0], arguments[0])),
            "SIN" => RealMath(expression, "Sin", (parameters[0], arguments[0])),
            "SQRT" => RealMath(expression, "Sqrt", (parameters[0], arguments[0])),
            "TAN" => RealMath(expression, "Tan", (parameters[0], arguments[0])),
            "VALUE" => EmitValue(expression, arguments[0]),
            "VALUE_IN" => EmitValueIn(expression, arguments),
            "VALUE_UNIQUE" => EmitValueUnique(expression, arguments[0]),
            "ROLESOF" or "TYPEOF" or "USEDIN" when context.ResolveModelFunction is not null =>
                context.ResolveModelFunction(expression.Operation.ToUpperInvariant(), arguments),
            "ROLESOF" or "TYPEOF" or "USEDIN" => throw GenerationError(
                expression,
                $"{expression.Operation.ToUpperInvariant()} requires an enclosing generated model operation."),
            _ => throw GenerationError(
                expression,
                $"Built-in function '{expression.Operation}' has no static generator."),
        };
    }

    private static string EmitNumericBinary(
        ExpressBoundExpression expression,
        string operation,
        ExpressBoundExpression left,
        string leftCode,
        ExpressBoundExpression right,
        string rightCode)
    {
        var target = operation == "/" ? ExpressExpressionTypeKind.Real : expression.Type.Kind;
        var promotedLeft = PromoteNumeric(left, leftCode, target);
        var promotedRight = PromoteNumeric(right, rightCode, target);
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
            + $"return global::System.Double.IsFinite({quotient}) "
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
        string rightCode)
    {
        var resultType = TypeName(expression.Type);
        var resultAggregate = (ExpressBoundAggregateType)expression.Type.DeclaredType!;
        var elementType = BoundTypeName(resultAggregate.ElementType);
        var leftIsAggregate = left.Type.Kind == ExpressExpressionTypeKind.Aggregate;
        var rightIsAggregate = right.Type.Kind == ExpressExpressionTypeKind.Aggregate;
        string enumerable;
        if (operation == "+")
        {
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
                : BagIntersection(leftCode, rightCode, elementType);
        }
        else if (resultAggregate.Kind == ExpressAggregateKind.Set)
        {
            enumerable = rightIsAggregate
                ? $"global::System.Linq.Enumerable.Except(({leftCode}), ({rightCode}))"
                : $"global::System.Linq.Enumerable.Where(({leftCode}), __item => "
                    + $"!global::System.Collections.Generic.EqualityComparer<{elementType}>.Default.Equals("
                    + $"__item, ({rightCode})))";
        }
        else
        {
            enumerable = BagDifference(leftCode, rightCode, elementType, rightIsAggregate);
        }

        return $"({resultType})[..{enumerable}]";
    }

    private static string BagIntersection(string left, string right, string elementType)
    {
        return "global::System.Linq.Enumerable.SelectMany("
            + $"global::System.Linq.Enumerable.GroupBy(({left}), __item => __item), __group => "
            + "global::System.Linq.Enumerable.Repeat(__group.Key, global::System.Math.Min("
            + "global::System.Linq.Enumerable.Count(__group), "
            + $"global::System.Linq.Enumerable.Count(({right}), __candidate => "
            + $"global::System.Collections.Generic.EqualityComparer<{elementType}>.Default.Equals("
            + "__candidate, __group.Key)))))";
    }

    private static string BagDifference(
        string left,
        string right,
        string elementType,
        bool rightIsAggregate)
    {
        var removedCount = rightIsAggregate
            ? $"global::System.Linq.Enumerable.Count(({right}), __candidate => "
                + $"global::System.Collections.Generic.EqualityComparer<{elementType}>.Default.Equals("
                + "__candidate, __group.Key))"
            : $"(global::System.Collections.Generic.EqualityComparer<{elementType}>.Default.Equals("
                + $"({right}), __group.Key) ? 1 : 0)";
        return "global::System.Linq.Enumerable.SelectMany("
            + $"global::System.Linq.Enumerable.GroupBy(({left}), __item => __item), __group => "
            + "global::System.Linq.Enumerable.Repeat(__group.Key, global::System.Math.Max(0, "
            + $"global::System.Linq.Enumerable.Count(__group) - {removedCount})))";
    }

    private static string AggregateSubset(
        string operation,
        ExpressBoundExpression left,
        string leftCode,
        string rightCode)
    {
        var elementType = BoundTypeName(((ExpressBoundAggregateType)left.Type.DeclaredType!).ElementType);
        var subset = "global::System.Linq.Enumerable.All("
            + "global::System.Linq.Enumerable.GroupBy(({0}), __item => __item), __group => "
            + "global::System.Linq.Enumerable.Count(({1}), __candidate => "
            + $"global::System.Collections.Generic.EqualityComparer<{elementType}>.Default.Equals("
            + "__candidate, __group.Key)) >= global::System.Linq.Enumerable.Count(__group))";
        return operation == "<="
            ? string.Format(CultureInfo.InvariantCulture, subset, leftCode, rightCode)
            : string.Format(CultureInfo.InvariantCulture, subset, rightCode, leftCode);
    }

    private static string PromoteNumeric(
        ExpressBoundExpression expression,
        string code,
        ExpressExpressionTypeKind target)
    {
        if (expression.Type.Kind == target)
        {
            return code;
        }

        return target switch
        {
            ExpressExpressionTypeKind.Number when expression.Type.Kind == ExpressExpressionTypeKind.Integer =>
                $"global::TedToolkit.Step21.NumberValue.FromInteger({code})",
            ExpressExpressionTypeKind.Number when expression.Type.Kind == ExpressExpressionTypeKind.Real =>
                $"global::TedToolkit.Step21.NumberValue.FromReal({code})",
            ExpressExpressionTypeKind.Real when expression.Type.Kind == ExpressExpressionTypeKind.Integer =>
                $"new global::TedToolkit.Step21.RealValue(({code}), global::System.Numerics.BigInteger.Zero)",
            ExpressExpressionTypeKind.Real when expression.Type.Kind == ExpressExpressionTypeKind.Number =>
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
        var suffix = expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
            + "_"
            + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture);
        var dividend = "__expressDividend_" + suffix;
        var divisor = "__expressDivisor_" + suffix;
        var remainder = "__expressRemainder_" + suffix;
        var values = $"(({AsInteger(left, leftCode)}), ({AsInteger(right, rightCode)})) switch {{ "
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
            && !RequiresSchemaValueEquality(aggregate.ElementType))
        {
            return EmitOptionalArrayValueComparison(
                expression,
                aggregate,
                leftCode,
                rightCode,
                negated);
        }

        var equality = ValueEquality(expression, left, leftCode, right, rightCode, context);
        return LogicalComparison(negated ? $"!({equality})" : equality);
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
        string value,
        ExpressBoundExpression aggregateExpression,
        string aggregate)
    {
        var contains = $"global::System.Linq.Enumerable.Contains(({aggregate}), ({value}))";
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

    private static string ValueEquality(
        ExpressBoundExpression expression,
        ExpressBoundExpression left,
        string leftCode,
        ExpressBoundExpression right,
        string rightCode,
        ExpressExpressionEmissionContext context)
    {
        if (RequiresSchemaValueEquality(left.Type))
        {
            return context.ResolveValueEquality is null
                ? throw GenerationError(
                    expression,
                    "Entity-containing value equality requires an enclosing generated schema operation.")
                : context.ResolveValueEquality(left.Type, leftCode, rightCode);
        }

        return ValueEqualityCore(left, leftCode, right, rightCode);
    }

    private static string ValueEqualityCore(
        ExpressBoundExpression left,
        string leftCode,
        ExpressBoundExpression right,
        string rightCode)
    {
        if (left.Type.DeclaredType is ExpressBoundAggregateType leftAggregate)
        {
            if (leftAggregate.Kind is ExpressAggregateKind.Array or ExpressAggregateKind.List)
            {
                return $"global::System.Linq.Enumerable.SequenceEqual(({leftCode}), ({rightCode}))";
            }

            return $"({AggregateSubset("<=", left, leftCode, rightCode)}) && "
                + $"({AggregateSubset(">=", left, leftCode, rightCode)})";
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

        return $"global::System.Collections.Generic.EqualityComparer<{TypeName(left.Type)}>"
            + $".Default.Equals(({leftCode}), ({rightCode}))";
    }

    private static string InstanceEquality(
        ExpressBoundExpression expression,
        ExpressBoundExpression left,
        string leftCode,
        ExpressBoundExpression right,
        string rightCode,
        ExpressExpressionEmissionContext context)
    {
        if (left.Type.Kind == ExpressExpressionTypeKind.Entity)
        {
            return $"global::System.Object.ReferenceEquals(({leftCode}), ({rightCode}))";
        }

        if (left.Type.Kind == ExpressExpressionTypeKind.Select)
        {
            return $"global::System.Collections.Generic.EqualityComparer<{TypeName(left.Type)}>"
                + $".Default.Equals(({leftCode}), ({rightCode}))";
        }

        return left.Type.Kind == ExpressExpressionTypeKind.Aggregate
            ? ValueEqualityCore(left, leftCode, right, rightCode)
            : ValueEquality(expression, left, leftCode, right, rightCode, context);
    }

    private static bool RequiresSchemaValueEquality(ExpressExpressionType type)
    {
        if (type.Kind is ExpressExpressionTypeKind.Entity or ExpressExpressionTypeKind.Select)
        {
            return true;
        }

        return type.DeclaredType is ExpressBoundAggregateType aggregate
            && RequiresSchemaValueEquality(aggregate.ElementType);
    }

    private static bool RequiresSchemaValueEquality(ExpressBoundType type)
    {
        return type switch
        {
            ExpressBoundAggregateType aggregate => RequiresSchemaValueEquality(aggregate.ElementType),
            ExpressBoundGenericType { IsEntity: true, } => true,
            ExpressBoundNamedType named => named.Declaration.Kind == ExpressDeclarationKind.Entity,
            ExpressBoundSelectType => true,
            _ => false,
        };
    }

    private static string OrderedComparison(
        string operation,
        ExpressBoundExpression left,
        string leftCode,
        ExpressBoundExpression right,
        string rightCode)
    {
        if (IsNumeric(left.Type.Kind) && IsNumeric(right.Type.Kind))
        {
            var target = NumericComparisonType(left.Type.Kind, right.Type.Kind);
            return $"(({PromoteNumeric(left, leftCode, target)}) {operation} "
                + $"({PromoteNumeric(right, rightCode, target)}))";
        }

        if (left.Type.Kind == ExpressExpressionTypeKind.String)
        {
            return $"global::System.StringComparer.Ordinal.Compare(({leftCode}), ({rightCode})) "
                + $"{operation} 0";
        }

        if (left.Type.Kind == ExpressExpressionTypeKind.Binary)
        {
            return $"global::System.StringComparer.Ordinal.Compare(({leftCode}).ToString(), "
                + $"({rightCode}).ToString()) {operation} 0";
        }

        if (left.Type.Kind == ExpressExpressionTypeKind.Enumeration
            && left.Type.EnumerationValues is { } values)
        {
            var order = "new global::System.String[] { "
                + $"{string.Join(", ", values.Select(value => Literal(value.ToUpperInvariant())))} }}";
            return $"global::System.Array.IndexOf({order}, ({leftCode}).Value) {operation} "
                + $"global::System.Array.IndexOf({order}, ({rightCode}).Value)";
        }

        if (left.Type.Kind is ExpressExpressionTypeKind.Boolean or ExpressExpressionTypeKind.Logical)
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
        IReadOnlyList<ExpressBoundExpression> operands,
        string[] codes,
        Func<string[], string> emitPresent)
    {
        var presentCodes = codes.ToArray();
        var conditions = new List<string>();
        for (var index = 0; index < operands.Count; index++)
        {
            if (!operands[index].Type.CanBeIndeterminate)
            {
                continue;
            }

            var location = operands[index].Span.Start;
            var variable = "__expressPresent_"
                + location.Line.ToString(CultureInfo.InvariantCulture)
                + "_"
                + location.Column.ToString(CultureInfo.InvariantCulture)
                + "_"
                + index.ToString(CultureInfo.InvariantCulture);
            conditions.Add($"({codes[index]}) is {{ }} {variable}");
            presentCodes[index] = variable;
        }

        if (conditions.Count == 0)
        {
            return emitPresent(presentCodes);
        }

        var fallback = result.Type.Kind == ExpressExpressionTypeKind.Logical
            ? "global::TedToolkit.Step21.LogicalValue.Unknown"
            : $"({TypeName(result.Type.WithIndeterminate(false))}?)null";
        return $"({string.Join(" && ", conditions)} ? ({emitPresent(presentCodes)}) : {fallback})";
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
        return parameter.Type.DeclaredType is ExpressBoundAggregateType { Kind: ExpressAggregateKind.Array, }
            ? $"({argument}).UpperIndex"
            : $"global::System.Linq.Enumerable.Count({argument})";
    }

    private static string HighBound(ExpressBoundExpression parameter, string argument)
    {
        if (parameter.Type.DeclaredType is ExpressBoundAggregateType { Kind: ExpressAggregateKind.Array, })
        {
            return BigIntegerExpression($"({argument}).UpperIndex");
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
        return parameter.Type.DeclaredType is ExpressBoundAggregateType { Kind: ExpressAggregateKind.Array, }
            ? $"({argument}).LowerIndex"
            : $"({argument}).LowerBound";
    }

    private static string LowIndex(ExpressBoundExpression parameter, string argument)
    {
        return parameter.Type.DeclaredType is ExpressBoundAggregateType { Kind: ExpressAggregateKind.Array, }
            ? $"({argument}).LowerIndex"
            : "1";
    }

    private static string BigIntegerExpression(string code)
    {
        return $"new global::System.Numerics.BigInteger({code})";
    }

    private static string RealMath(
        ExpressBoundExpression expression,
        string operation,
        params (ExpressBoundExpression Expression, string Code)[] arguments)
    {
        var values = arguments.Select(argument => argument.Expression.Type.Kind switch
        {
            ExpressExpressionTypeKind.Integer => $"(double)({argument.Code})",
            ExpressExpressionTypeKind.Number or ExpressExpressionTypeKind.Real =>
                $"({argument.Code}).ToDouble()",
            _ => argument.Code,
        });
        var suffix = expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
            + "_"
            + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture);
        var result = "__expressMath_" + suffix;
        return $"(global::System.Math.{operation}({string.Join(", ", values)}) is var {result} "
            + $"&& global::System.Double.IsFinite({result}) ? "
            + $"global::TedToolkit.Step21.RealValue.FromDouble({result}) : "
            + "(global::TedToolkit.Step21.RealValue?)null)";
    }

    private static string AsLogical(ExpressBoundExpression expression, string code)
    {
        if (expression.Type.CanBeIndeterminate)
        {
            return expression.Type.Kind switch
            {
                ExpressExpressionTypeKind.Boolean => $"(({code}) switch {{ "
                    + "true => global::TedToolkit.Step21.LogicalValue.True, "
                    + "false => global::TedToolkit.Step21.LogicalValue.False, "
                    + "_ => global::TedToolkit.Step21.LogicalValue.Unknown })",
                ExpressExpressionTypeKind.Logical => $"(({code}) ?? "
                    + "global::TedToolkit.Step21.LogicalValue.Unknown)",
                _ => "global::TedToolkit.Step21.LogicalValue.Unknown",
            };
        }

        return expression.Type.Kind switch
        {
            ExpressExpressionTypeKind.Boolean => LogicalComparison(code),
            ExpressExpressionTypeKind.Indeterminate => "global::TedToolkit.Step21.LogicalValue.Unknown",
            _ => code,
        };
    }

    private static string LogicalComparison(string code)
    {
        return $"(({code}) ? global::TedToolkit.Step21.LogicalValue.True : "
            + "global::TedToolkit.Step21.LogicalValue.False)";
    }

    private static string UnwrapDefined(ExpressExpressionType type, string code)
    {
        for (var index = 0; index < type.DefinedValueDepth; index++)
        {
            code = $"({code}).Value";
        }

        return code;
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
        const string unknownValue = "global::TedToolkit.Step21.LogicalValue.Unknown";
        const string trueValue = "global::TedToolkit.Step21.LogicalValue.True";
        return operation switch
        {
            "AND" => $"((({left}), ({right})) switch {{ "
                + $"({falseValue}, _) or (_, {falseValue}) => {falseValue}, "
                + $"({trueValue}, {trueValue}) => {trueValue}, _ => {unknownValue} }})",
            "OR" => $"((({left}), ({right})) switch {{ "
                + $"({trueValue}, _) or (_, {trueValue}) => {trueValue}, "
                + $"({falseValue}, {falseValue}) => {falseValue}, _ => {unknownValue} }})",
            "XOR" => $"((({left}), ({right})) switch {{ "
                + $"({unknownValue}, _) or (_, {unknownValue}) => {unknownValue}, "
                + $"({trueValue}, {falseValue}) or ({falseValue}, {trueValue}) => {trueValue}, "
                + $"_ => {falseValue} }})",
            _ => throw new InvalidOperationException($"Unknown logical operation '{operation}'."),
        };
    }

    private static string TypeName(ExpressExpressionType type)
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
            ExpressExpressionTypeKind.Entity when type.DeclaredType is ExpressBoundNamedType entity =>
                GeneratedTypeName(entity.Declaration, entityInterface: true),
            ExpressExpressionTypeKind.Entity => "global::TedToolkit.Step21.Entity",
            ExpressExpressionTypeKind.Enumeration when type.DeclaredType is ExpressBoundNamedType enumeration =>
                GeneratedTypeName(enumeration.Declaration, entityInterface: false),
            ExpressExpressionTypeKind.Select when type.DeclaredType is ExpressBoundNamedType select =>
                GeneratedTypeName(select.Declaration, entityInterface: false),
            _ => throw new InvalidOperationException(
                $"Expression type '{type.Kind.ToString()}' requires a schema generation context."),
        };
        return type.CanBeIndeterminate ? name + "?" : name;
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

    private static string BoundTypeName(ExpressBoundType type)
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
        return $"global::TedToolkit.Step21.Generated.{schema}.{prefix}{name}";
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