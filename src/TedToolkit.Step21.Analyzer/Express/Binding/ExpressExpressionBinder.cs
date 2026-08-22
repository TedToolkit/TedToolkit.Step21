// -----------------------------------------------------------------------
// <copyright file="ExpressExpressionBinder.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Numerics;

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Converts neutral expression syntax into immutable expression nodes with explicit EXPRESS result types.
/// </summary>
internal sealed class ExpressExpressionBinder
{
    private static readonly ExpressExpressionType _unresolved = new(ExpressExpressionTypeKind.Unresolved);

    private static readonly ExpressExpressionType _indeterminate = new(
        ExpressExpressionTypeKind.Indeterminate,
        canBeIndeterminate: true);

    private static readonly ExpressExpressionType _binary = new(ExpressExpressionTypeKind.Binary);

    private static readonly ExpressExpressionType _boolean = new(ExpressExpressionTypeKind.Boolean);

    private static readonly ExpressExpressionType _integer = new(ExpressExpressionTypeKind.Integer);

    private static readonly ExpressExpressionType _logical = new(ExpressExpressionTypeKind.Logical);

    private static readonly ExpressExpressionType _number = new(ExpressExpressionTypeKind.Number);

    private static readonly ExpressExpressionType _real = new(ExpressExpressionTypeKind.Real);

    private static readonly ExpressExpressionType _string = new(ExpressExpressionTypeKind.String);

    private readonly IReadOnlyList<ExpressBoundNameReference> _references;

    private readonly IReadOnlyDictionary<ExpressBoundSymbol, ExpressBoundDeclaration> _declarations;

    private ExpressExpressionType? _selfType;

    private ExpressExpressionBinder(
        IReadOnlyList<ExpressBoundNameReference> references,
        IReadOnlyDictionary<ExpressBoundSymbol, ExpressBoundDeclaration> declarations)
    {
        _references = references;
        _declarations = declarations;
    }

    /// <summary>
    /// Binds each outermost expression retained by the supplied declarations.
    /// </summary>
    /// <param name="declarations">The resolved schema declarations.</param>
    /// <param name="references">The resolved lexical and schema name references.</param>
    /// <returns>The typed outermost expression trees in source order.</returns>
    internal static IReadOnlyList<ExpressBoundExpression> Bind(
        IReadOnlyList<ExpressBoundDeclaration> declarations,
        IReadOnlyList<ExpressBoundNameReference> references)
    {
        var bySymbol = declarations.ToDictionary(declaration => declaration.Symbol);
        var binder = new ExpressExpressionBinder(references, bySymbol);
        var expectedTypes = binder.FindExpectedTypes(declarations);
        var result = new List<ExpressBoundExpression>();
        foreach (var declaration in declarations)
        {
            binder._selfType = declaration switch
            {
                ExpressBoundEntity entity => binder.TypeOf(new ExpressBoundNamedType(entity.Symbol, entity.Syntax.Span)),
                ExpressBoundDefinedType type => binder.TypeOf(new ExpressBoundNamedType(type.Symbol, type.Syntax.Span)),
                _ => null,
            };
            foreach (var expression in ExpressionRoots(declaration.Syntax))
            {
                result.Add(binder.BindExpression(
                    expression,
                    EmptyLexicalTypes.Instance,
                    expectedTypes.TryGetValue(expression, out var expected) ? expected : null));
            }
        }

        binder._selfType = null;
        return result;
    }

    private Dictionary<ExpressRuleSyntax, ExpressExpressionType> FindExpectedTypes(
        IReadOnlyList<ExpressBoundDeclaration> declarations)
    {
        var result = new Dictionary<ExpressRuleSyntax, ExpressExpressionType>();
        foreach (var declaration in declarations.OfType<ExpressBoundOpaqueDeclaration>())
        {
            if (declaration.Kind == ExpressDeclarationKind.Constant
                && declaration.DeclaredType is not null
                && declaration.Syntax.ChildRules("expression").SingleOrDefault() is { } constantValue)
            {
                result[constantValue] = TypeOf(declaration.DeclaredType);
            }

            if (declaration.Kind == ExpressDeclarationKind.Function && declaration.DeclaredType is not null)
            {
                foreach (var returnStatement in DescendantsInAlgorithm(declaration.Syntax)
                             .Where(node => node.Production == "returnStmt"))
                {
                    if (returnStatement.ChildRules("expression").SingleOrDefault() is { } returnValue)
                    {
                        result[returnValue] = TypeOf(declaration.DeclaredType);
                    }
                }
            }
        }

        foreach (var syntax in declarations.SelectMany(declaration => declaration.Syntax.DescendantsAndSelf()))
        {
            if (syntax.Production == "assignmentStmt")
            {
                var target = FindReference(syntax.RequiredChild("generalRef").Span)?.Target;
                if (target?.Type is not null)
                {
                    result[syntax.RequiredChild("expression")] = TypeOf(target.Type);
                }
            }
            else if (syntax.Production == "localVariable"
                     && syntax.ChildRules("expression").SingleOrDefault() is { } initializer)
            {
                var declarationSpan = syntax.RequiredChild("variableId").Span;
                var target = _references.Select(reference => reference.Target)
                    .Where(candidate => candidate.Kind == ExpressBoundNameKind.Variable
                        && SameStart(candidate.Span, declarationSpan))
                    .Distinct()
                    .SingleOrDefault();
                if (target?.Type is not null)
                {
                    result[initializer] = TypeOf(target.Type);
                }
            }
        }

        return result;
    }

    private static IEnumerable<ExpressRuleSyntax> DescendantsInAlgorithm(ExpressRuleSyntax syntax)
    {
        yield return syntax;
        foreach (var child in syntax.ChildRules())
        {
            if (child.Production is "functionDecl" or "procedureDecl")
            {
                continue;
            }

            foreach (var descendant in DescendantsInAlgorithm(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool SameStart(ExpressSourceSpan left, ExpressSourceSpan right)
    {
        return string.Equals(left.Start.FilePath, right.Start.FilePath, StringComparison.Ordinal)
            && left.Start.Line == right.Start.Line
            && left.Start.Column == right.Start.Column;
    }

    private static IEnumerable<ExpressRuleSyntax> ExpressionRoots(ExpressRuleSyntax syntax)
    {
        if (syntax.Production == "expression")
        {
            yield return syntax;
            yield break;
        }

        foreach (var child in syntax.ChildRules())
        {
            foreach (var expression in ExpressionRoots(child))
            {
                yield return expression;
            }
        }
    }

    private ExpressBoundExpression BindExpression(
        ExpressRuleSyntax syntax,
        IReadOnlyDictionary<string, ExpressExpressionType> lexicalTypes,
        ExpressExpressionType? expectedType = null)
    {
        var simpleExpressions = syntax.ChildRules("simpleExpression").ToArray();
        var left = BindSimpleExpression(simpleExpressions[0], lexicalTypes);
        if (simpleExpressions.Length == 1)
        {
            return ApplyExpectedType(left, expectedType);
        }

        var operation = syntax.RequiredChild("relOpExtended").TokenText();
        var right = BindSimpleExpression(simpleExpressions[1], lexicalTypes);
        return ApplyExpectedType(Create(
            ExpressExpressionKind.Binary,
            _logical,
            syntax,
            operation,
            reference: null,
            [left, right,]), expectedType);
    }

    private ExpressBoundExpression ApplyExpectedType(
        ExpressBoundExpression expression,
        ExpressExpressionType? expectedType)
    {
        if (expression.Kind != ExpressExpressionKind.AggregateInitializer
            || expectedType?.DeclaredType is not ExpressBoundAggregateType aggregate)
        {
            return expression;
        }

        var elementType = FromBoundType(aggregate.ElementType);
        var children = expression.Children.Select(child => child.Kind == ExpressExpressionKind.Repetition
            ? new ExpressBoundExpression(
                child.Kind,
                elementType,
                child.SourceText,
                child.Operation,
                child.Reference,
                [ApplyElementType(child.Children[0], elementType), child.Children[1],],
                child.Span)
            : ApplyElementType(child, elementType));
        return new(
            expression.Kind,
            expectedType.WithIndeterminate(
                expression.Type.CanBeIndeterminate
                || expression.Children.Any(child => child.Kind == ExpressExpressionKind.Repetition
                    && !IsStaticallyValidRepetition(child.Children[1]))),
            expression.SourceText,
            expression.Operation,
            expression.Reference,
            children,
            expression.Span);
    }

    private static ExpressBoundExpression ApplyElementType(
        ExpressBoundExpression expression,
        ExpressExpressionType expectedType)
    {
        return expression.Kind == ExpressExpressionKind.Indeterminate
            ? new ExpressBoundExpression(
                expression.Kind,
                expectedType.WithIndeterminate(true),
                expression.SourceText,
                expression.Operation,
                expression.Reference,
                expression.Children,
                expression.Span)
            : expression;
    }

    private ExpressBoundExpression BindSimpleExpression(
        ExpressRuleSyntax syntax,
        IReadOnlyDictionary<string, ExpressExpressionType> lexicalTypes)
    {
        return BindLeftAssociative(
            syntax,
            "term",
            "addLikeOp",
            BindTerm,
            lexicalTypes);
    }

    private ExpressBoundExpression BindTerm(
        ExpressRuleSyntax syntax,
        IReadOnlyDictionary<string, ExpressExpressionType> lexicalTypes)
    {
        return BindLeftAssociative(
            syntax,
            "factor",
            "multiplicationLikeOp",
            BindFactor,
            lexicalTypes);
    }

    private static ExpressBoundExpression BindLeftAssociative(
        ExpressRuleSyntax syntax,
        string operandProduction,
        string operationProduction,
        Func<ExpressRuleSyntax, IReadOnlyDictionary<string, ExpressExpressionType>, ExpressBoundExpression> bindOperand,
        IReadOnlyDictionary<string, ExpressExpressionType> lexicalTypes)
    {
        var operands = syntax.ChildRules(operandProduction).ToArray();
        var operations = syntax.ChildRules(operationProduction).ToArray();
        var result = bindOperand(operands[0], lexicalTypes);
        for (var index = 0; index < operations.Length; index++)
        {
            var right = bindOperand(operands[index + 1], lexicalTypes);
            var operation = operations[index].TokenText();
            var type = BinaryResultType(operation, result.Type, right.Type);
            if (operation is not ("OR" or "XOR" or "AND")
                && (result.Type.CanBeIndeterminate || right.Type.CanBeIndeterminate))
            {
                type = type.WithIndeterminate(true);
            }

            result = Create(
                ExpressExpressionKind.Binary,
                type,
                SliceSpan(result.Span, right.Span),
                string.Concat(result.SourceText, operation, right.SourceText),
                operation,
                reference: null,
                [result, right,]);
        }

        return result;
    }

    private ExpressBoundExpression BindFactor(
        ExpressRuleSyntax syntax,
        IReadOnlyDictionary<string, ExpressExpressionType> lexicalTypes)
    {
        var operands = syntax.ChildRules("simpleFactor").ToArray();
        var left = BindSimpleFactor(operands[0], lexicalTypes);
        if (operands.Length == 1)
        {
            return left;
        }

        var right = BindSimpleFactor(operands[1], lexicalTypes);
        var type = NumericResultType(left.Type, right.Type)
            .WithIndeterminate(true);
        return Create(
            ExpressExpressionKind.Binary,
            type,
            syntax,
            "**",
            reference: null,
            [left, right,]);
    }

    private ExpressBoundExpression BindSimpleFactor(
        ExpressRuleSyntax syntax,
        IReadOnlyDictionary<string, ExpressExpressionType> lexicalTypes)
    {
        var structural = syntax.ChildRules().FirstOrDefault(child =>
            child.Production is "aggregateInitializer" or "interval" or "queryExpression");
        if (structural is not null)
        {
            return structural.Production switch
            {
                "aggregateInitializer" => BindAggregateInitializer(structural, lexicalTypes),
                "interval" => BindInterval(structural, lexicalTypes),
                "queryExpression" => BindQuery(structural, lexicalTypes),
                _ => throw new InvalidOperationException("Unexpected structural expression."),
            };
        }

        var value = syntax.ChildRules("expression").SingleOrDefault() is { } parenthesized
            ? BindExpression(parenthesized, lexicalTypes)
            : BindPrimary(syntax.RequiredChild("primary"), lexicalTypes);
        var unary = syntax.ChildRules("unaryOp").SingleOrDefault();
        if (unary is null)
        {
            return value;
        }

        var operation = unary.TokenText();
        var type = string.Equals(operation, "NOT", StringComparison.OrdinalIgnoreCase)
            ? _logical
            : value.Type;
        return Create(
            ExpressExpressionKind.Unary,
            type,
            syntax,
            operation,
            reference: null,
            [value,]);
    }

    private ExpressBoundExpression BindPrimary(
        ExpressRuleSyntax syntax,
        IReadOnlyDictionary<string, ExpressExpressionType> lexicalTypes)
    {
        var literal = syntax.ChildRules("literal").SingleOrDefault();
        if (literal is not null)
        {
            return BindLiteral(literal);
        }

        var named = syntax.ChildRules().First(child =>
            child.Production is "namedApplication" or "namedReference");
        var result = named.Production == "namedApplication"
            ? BindApplication(named, lexicalTypes)
            : BindReference(named, lexicalTypes);
        foreach (var qualifier in syntax.ChildRules("qualifier"))
        {
            result = BindQualifier(qualifier, result, lexicalTypes);
        }

        return result;
    }

    private static ExpressBoundExpression BindLiteral(ExpressRuleSyntax syntax)
    {
        var token = syntax.DescendantTokens().Single();
        var text = token.Text;
        var type = token.TokenName switch
        {
            "BinaryLiteral" => _binary,
            "IntegerLiteral" => _integer,
            "RealLiteral" => _real,
            "SimpleStringLiteral" or "EncodedStringLiteral" => _string,
            _ when string.Equals(text, "TRUE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "FALSE", StringComparison.OrdinalIgnoreCase) => _boolean,
            _ when string.Equals(text, "UNKNOWN", StringComparison.OrdinalIgnoreCase) => _logical,
            _ => _unresolved,
        };
        return Create(
            ExpressExpressionKind.Literal,
            type,
            syntax,
            operation: null,
            reference: null,
            []);
    }

    private ExpressBoundExpression BindReference(
        ExpressRuleSyntax syntax,
        IReadOnlyDictionary<string, ExpressExpressionType> lexicalTypes)
    {
        var text = syntax.TokenText();
        if (text == "?")
        {
            return Create(
                ExpressExpressionKind.Indeterminate,
                _indeterminate,
                syntax,
                text,
                reference: null,
                []);
        }

        if (string.Equals(text, "E", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "PI", StringComparison.OrdinalIgnoreCase))
        {
            return Create(
                ExpressExpressionKind.Reference,
                _real,
                syntax,
                text,
                reference: null,
                []);
        }

        if (string.Equals(text, "SELF", StringComparison.OrdinalIgnoreCase))
        {
            return Create(
                ExpressExpressionKind.Reference,
                _selfType ?? new(ExpressExpressionTypeKind.Entity),
                syntax,
                text,
                reference: null,
                []);
        }

        var reference = FindReference(syntax.Span);
        var type = reference is not null
            && lexicalTypes.TryGetValue(reference.Target.Name, out var lexicalType)
                ? lexicalType
                : TypeOf(reference?.Target.Type)
                    .WithIndeterminate(reference?.Target.IsOptional == true);
        return Create(
            ExpressExpressionKind.Reference,
            type,
            syntax,
            text,
            reference?.Target,
            []);
    }

    private ExpressBoundExpression BindApplication(
        ExpressRuleSyntax syntax,
        IReadOnlyDictionary<string, ExpressExpressionType> lexicalTypes)
    {
        var parameterList = syntax.ChildRules("actualParameterList").SingleOrDefault();
        var parameters = parameterList is null
            ? []
            : parameterList.ChildRules("parameter")
                .Select(parameter => BindExpression(parameter.RequiredChild("expression"), lexicalTypes))
                .ToArray();
        var builtin = syntax.ChildRules("builtInFunction").SingleOrDefault();
        var reference = builtin is null ? FindReference(syntax.Span) : null;
        var operation = builtin?.TokenText() ?? reference?.Target.Name ?? syntax.IdentifierToken().Text;
        var type = builtin is null
            ? TypeOf(reference?.Target.Type)
            : BuiltInResultType(operation, parameters);
        if (builtin is not null
            && operation is not ("EXISTS" or "NVL")
            && type.Kind is not (ExpressExpressionTypeKind.Boolean or ExpressExpressionTypeKind.Logical)
            && parameters.Any(parameter => parameter.Type.CanBeIndeterminate))
        {
            type = type.WithIndeterminate(true);
        }

        return Create(
            ExpressExpressionKind.Application,
            type,
            syntax,
            operation,
            reference?.Target,
            parameters);
    }

    private ExpressBoundExpression BindQualifier(
        ExpressRuleSyntax syntax,
        ExpressBoundExpression source,
        IReadOnlyDictionary<string, ExpressExpressionType> lexicalTypes)
    {
        var qualifier = syntax.ChildRules().Single();
        if (qualifier.Production == "attributeQualifier")
        {
            var target = FindReference(qualifier.Span)?.Target
                ?? ResolveAttribute(source.Type, qualifier);
            return Create(
                ExpressExpressionKind.AttributeQualifier,
                TypeOf(target?.Type)
                    .WithIndeterminate(source.Type.CanBeIndeterminate || target?.IsOptional == true),
                SliceSpan(source.Span, syntax.Span),
                string.Concat(source.SourceText, qualifier.TokenText()),
                qualifier.TokenText(),
                target,
                [source,]);
        }

        if (qualifier.Production == "groupQualifier")
        {
            var reference = FindReference(qualifier.RequiredChild("entityRef").Span);
            return Create(
                ExpressExpressionKind.GroupQualifier,
                TypeOf(reference?.Target.Type)
                    .WithIndeterminate(source.Type.CanBeIndeterminate),
                SliceSpan(source.Span, syntax.Span),
                string.Concat(source.SourceText, qualifier.TokenText()),
                qualifier.TokenText(),
                reference?.Target,
                [source,]);
        }

        var indices = qualifier.ChildRules()
            .SelectMany(child => child.DescendantsAndSelf())
            .Where(child => child.Production == "index")
            .Select(index => BindSimpleExpression(index.RequiredChild("numericExpression").RequiredChild("simpleExpression"), lexicalTypes))
            .ToArray();
        var isSlice = indices.Length == 2
            && source.Type.Kind is ExpressExpressionTypeKind.Binary or ExpressExpressionTypeKind.String;
        var resultType = (isSlice ? source.Type : IndexedType(source.Type))
            .WithIndeterminate(true);
        return Create(
            isSlice ? ExpressExpressionKind.SliceQualifier : ExpressExpressionKind.IndexQualifier,
            resultType,
            SliceSpan(source.Span, syntax.Span),
            string.Concat(source.SourceText, qualifier.TokenText()),
            qualifier.TokenText(),
            reference: null,
            [source, .. indices,]);
    }

    private ExpressBoundName? ResolveAttribute(
        ExpressExpressionType source,
        ExpressRuleSyntax qualifier)
    {
        if (source.DeclaredType is not ExpressBoundNamedType named
            || named.Declaration.Kind != ExpressDeclarationKind.Entity
            || !_declarations.TryGetValue(named.Declaration, out var declaration)
            || declaration is not ExpressBoundEntity entity)
        {
            return null;
        }

        var name = qualifier.RequiredChild("attributeRef").IdentifierToken().Text;
        var attributes = EnumerateAttributes(entity, new HashSet<ExpressBoundSymbol>())
            .Where(attribute => string.Equals(attribute.Name, name, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToArray();
        if (attributes.Length != 1)
        {
            return null;
        }

        var attribute = attributes[0];
        return new(
            attribute.Name,
            ExpressBoundNameKind.Attribute,
            attribute.Type,
            schemaDeclaration: null,
            attribute.Span,
            attribute.IsOptional,
            attribute);
    }

    private IEnumerable<ExpressBoundAttribute> EnumerateAttributes(
        ExpressBoundEntity entity,
        ISet<ExpressBoundSymbol> visited)
    {
        if (!visited.Add(entity.Symbol))
        {
            yield break;
        }

        foreach (var attribute in entity.Attributes)
        {
            yield return attribute;
        }

        foreach (var supertype in entity.DirectSupertypes)
        {
            if (_declarations.TryGetValue(supertype, out var declaration)
                && declaration is ExpressBoundEntity baseEntity)
            {
                foreach (var attribute in EnumerateAttributes(baseEntity, visited))
                {
                    yield return attribute;
                }
            }
        }
    }

    private ExpressBoundExpression BindAggregateInitializer(
        ExpressRuleSyntax syntax,
        IReadOnlyDictionary<string, ExpressExpressionType> lexicalTypes)
    {
        var children = new List<ExpressBoundExpression>();
        foreach (var element in syntax.ChildRules("element"))
        {
            var value = BindExpression(element.RequiredChild("expression"), lexicalTypes);
            var repetition = element.ChildRules("repetition").SingleOrDefault();
            if (repetition is null)
            {
                children.Add(value);
                continue;
            }

            var count = BindSimpleExpression(
                repetition.RequiredChild("numericExpression").RequiredChild("simpleExpression"),
                lexicalTypes);
            children.Add(Create(
                ExpressExpressionKind.Repetition,
                value.Type,
                element,
                ":",
                reference: null,
                [value, count,]));
        }

        var elementType = children.Count == 0 ? _unresolved : children[0].Type;
        var aggregate = new ExpressBoundAggregateType(
            ExpressAggregateKind.List,
            elementType.DeclaredType ?? ScalarType(elementType, syntax.Span),
            lowerBoundText: "0",
            upperBoundText: null,
            isOptional: false,
            isUnique: false,
            typeLabel: null,
            syntax.Span);
        var canBeIndeterminate = children.Any(child => child.Kind == ExpressExpressionKind.Repetition
            && !IsStaticallyValidRepetition(child.Children[1]));
        return Create(
            ExpressExpressionKind.AggregateInitializer,
            new ExpressExpressionType(
                ExpressExpressionTypeKind.Aggregate,
                aggregate,
                canBeIndeterminate),
            syntax,
            operation: null,
            reference: null,
            children);
    }

    private static bool IsStaticallyValidRepetition(ExpressBoundExpression count)
    {
        return count.Kind == ExpressExpressionKind.Literal
            && BigInteger.TryParse(count.SourceText, out var value)
            && value.Sign >= 0
            && value <= int.MaxValue;
    }

    private ExpressBoundExpression BindInterval(
        ExpressRuleSyntax syntax,
        IReadOnlyDictionary<string, ExpressExpressionType> lexicalTypes)
    {
        var children = new ExpressBoundExpression[]
        {
            BindSimpleExpression(syntax.RequiredChild("intervalLow").RequiredChild("simpleExpression"), lexicalTypes),
            BindSimpleExpression(syntax.RequiredChild("intervalItem").RequiredChild("simpleExpression"), lexicalTypes),
            BindSimpleExpression(syntax.RequiredChild("intervalHigh").RequiredChild("simpleExpression"), lexicalTypes),
        };
        return Create(
            ExpressExpressionKind.Interval,
            _logical,
            syntax,
            string.Join(",", syntax.ChildRules("intervalOp").Select(operation => operation.TokenText())),
            reference: null,
            children);
    }

    private ExpressBoundExpression BindQuery(
        ExpressRuleSyntax syntax,
        IReadOnlyDictionary<string, ExpressExpressionType> lexicalTypes)
    {
        var source = BindSimpleExpression(
            syntax.RequiredChild("aggregateSource").RequiredChild("simpleExpression"),
            lexicalTypes);
        var variableName = syntax.RequiredChild("variableId").IdentifierToken().Text;
        var queryTypes = new Dictionary<string, ExpressExpressionType>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in lexicalTypes)
        {
            queryTypes.Add(pair.Key, pair.Value);
        }

        queryTypes[variableName] = AggregateElementType(source.Type);
        var predicate = BindExpression(
            syntax.RequiredChild("logicalExpression").RequiredChild("expression"),
            queryTypes);
        return Create(
            ExpressExpressionKind.Query,
            source.Type.WithIndeterminate(source.Type.CanBeIndeterminate),
            syntax,
            variableName,
            reference: null,
            [source, predicate,]);
    }

    private ExpressBoundNameReference? FindReference(ExpressSourceSpan span)
    {
        return _references.SingleOrDefault(reference => ReferenceEquals(reference.Span, span));
    }

    private ExpressExpressionType TypeOf(ExpressBoundType? type)
    {
        return type switch
        {
            null => _unresolved,
            ExpressBoundScalarType scalar => ScalarType(scalar),
            ExpressBoundAggregateType => new ExpressExpressionType(ExpressExpressionTypeKind.Aggregate, type),
            ExpressBoundEnumerationType => new ExpressExpressionType(ExpressExpressionTypeKind.Enumeration, type),
            ExpressBoundSelectType => new ExpressExpressionType(ExpressExpressionTypeKind.Select, type),
            ExpressBoundGenericType => new ExpressExpressionType(ExpressExpressionTypeKind.Generic, type),
            ExpressBoundNamedType named when named.Declaration.Kind == ExpressDeclarationKind.Entity =>
                new ExpressExpressionType(ExpressExpressionTypeKind.Entity, type),
            ExpressBoundNamedType named when _declarations.TryGetValue(named.Declaration, out var declaration)
                && declaration is ExpressBoundDefinedType defined => DefinedType(named, defined),
            ExpressBoundNamedType => new ExpressExpressionType(ExpressExpressionTypeKind.Defined, type),
            _ => _unresolved,
        };
    }

    private ExpressExpressionType DefinedType(
        ExpressBoundNamedType named,
        ExpressBoundDefinedType declaration)
    {
        var underlying = TypeOf(declaration.UnderlyingType);
        var definesNominalValue = declaration.UnderlyingType is ExpressBoundEnumerationType or ExpressBoundSelectType;
        var enumerationValues = declaration.UnderlyingType is ExpressBoundEnumerationType enumeration
            ? enumeration.Values
            : underlying.EnumerationValues;
        return new(
            underlying.Kind,
            named,
            underlying.CanBeIndeterminate,
            definesNominalValue ? 0 : underlying.DefinedValueDepth + 1,
            enumerationValues);
    }

    private static ExpressExpressionType ScalarType(ExpressBoundScalarType scalar)
    {
        return new(
            scalar.Kind switch
            {
                ExpressScalarKind.Binary => ExpressExpressionTypeKind.Binary,
                ExpressScalarKind.Boolean => ExpressExpressionTypeKind.Boolean,
                ExpressScalarKind.Integer => ExpressExpressionTypeKind.Integer,
                ExpressScalarKind.Logical => ExpressExpressionTypeKind.Logical,
                ExpressScalarKind.Number => ExpressExpressionTypeKind.Number,
                ExpressScalarKind.Real => ExpressExpressionTypeKind.Real,
                ExpressScalarKind.String => ExpressExpressionTypeKind.String,
                _ => ExpressExpressionTypeKind.Unresolved,
            },
            scalar);
    }

    private static ExpressBoundScalarType ScalarType(ExpressExpressionType type, ExpressSourceSpan span)
    {
        var kind = type.Kind switch
        {
            ExpressExpressionTypeKind.Binary => ExpressScalarKind.Binary,
            ExpressExpressionTypeKind.Boolean => ExpressScalarKind.Boolean,
            ExpressExpressionTypeKind.Integer => ExpressScalarKind.Integer,
            ExpressExpressionTypeKind.Logical => ExpressScalarKind.Logical,
            ExpressExpressionTypeKind.Number => ExpressScalarKind.Number,
            ExpressExpressionTypeKind.Real => ExpressScalarKind.Real,
            ExpressExpressionTypeKind.String => ExpressScalarKind.String,
            _ => ExpressScalarKind.Number,
        };
        return new(kind, constraintText: null, isFixed: false, span);
    }

    private static ExpressExpressionType BinaryResultType(
        string operation,
        ExpressExpressionType left,
        ExpressExpressionType right)
    {
        if (operation is "OR" or "XOR" or "AND")
        {
            return _logical;
        }

        if (operation == "||")
        {
            if (left.Kind == ExpressExpressionTypeKind.Entity
                && right.Kind == ExpressExpressionTypeKind.Entity)
            {
                return new(ExpressExpressionTypeKind.Entity);
            }

            return left.Kind == ExpressExpressionTypeKind.Binary ? _binary : _string;
        }

        if (operation is "DIV" or "MOD")
        {
            return _integer.WithIndeterminate(true);
        }

        if (operation == "/")
        {
            return _real.WithIndeterminate(true);
        }

        if (operation == "*"
            && left.DeclaredType is ExpressBoundAggregateType leftAggregate
            && right.DeclaredType is ExpressBoundAggregateType rightAggregate)
        {
            var resultKind = leftAggregate.Kind == ExpressAggregateKind.Set
                || rightAggregate.Kind == ExpressAggregateKind.Set
                    ? ExpressAggregateKind.Set
                    : ExpressAggregateKind.Bag;
            return AggregateOperationType(leftAggregate, resultKind);
        }

        if (left.Kind == ExpressExpressionTypeKind.Aggregate)
        {
            return left;
        }

        if (operation == "+" && right.Kind == ExpressExpressionTypeKind.Aggregate)
        {
            return right;
        }

        return NumericResultType(left, right);
    }

    private static ExpressExpressionType AggregateOperationType(
        ExpressBoundAggregateType source,
        ExpressAggregateKind kind)
    {
        return new(
            ExpressExpressionTypeKind.Aggregate,
            new ExpressBoundAggregateType(
                kind,
                source.ElementType,
                lowerBoundText: "0",
                upperBoundText: null,
                isOptional: false,
                isUnique: false,
                typeLabel: null,
                source.Span));
    }

    private static ExpressExpressionType NumericResultType(
        ExpressExpressionType left,
        ExpressExpressionType right)
    {
        if (left.Kind == ExpressExpressionTypeKind.Real || right.Kind == ExpressExpressionTypeKind.Real)
        {
            return _real;
        }

        if (left.Kind == ExpressExpressionTypeKind.Number || right.Kind == ExpressExpressionTypeKind.Number)
        {
            return _number;
        }

        return _integer;
    }

    private ExpressExpressionType IndexedType(ExpressExpressionType source)
    {
        return source.Kind switch
        {
            ExpressExpressionTypeKind.Binary => _binary,
            ExpressExpressionTypeKind.String => _string,
            _ => AggregateElementType(source),
        };
    }

    private ExpressExpressionType AggregateElementType(ExpressExpressionType source)
    {
        return source.DeclaredType is ExpressBoundAggregateType aggregate
            ? FromBoundType(aggregate.ElementType)
            : _unresolved;
    }

    private ExpressExpressionType FromBoundType(ExpressBoundType type)
    {
        return type switch
        {
            ExpressBoundScalarType scalar => ScalarType(scalar),
            ExpressBoundAggregateType => new ExpressExpressionType(ExpressExpressionTypeKind.Aggregate, type),
            ExpressBoundGenericType => new ExpressExpressionType(ExpressExpressionTypeKind.Generic, type),
            ExpressBoundNamedType named => TypeOf(named),
            ExpressBoundEnumerationType => new ExpressExpressionType(ExpressExpressionTypeKind.Enumeration, type),
            ExpressBoundSelectType => new ExpressExpressionType(ExpressExpressionTypeKind.Select, type),
            _ => _unresolved,
        };
    }

    private static ExpressExpressionType BuiltInResultType(
        string operation,
        ExpressBoundExpression[] parameters)
    {
        return operation.ToUpperInvariant() switch
        {
            "ABS" => parameters.Length == 0 ? _unresolved : parameters[0].Type,
            "ACOS" or "ASIN" or "ATAN" or "COS" or "EXP" or "LOG" or "LOG2" or "LOG10"
                or "SIN" or "SQRT" or "TAN" => _real.WithIndeterminate(true),
            "VALUE" => _number.WithIndeterminate(true),
            "BLENGTH" or "HIINDEX" or "LENGTH" or "LOBOUND" or "LOINDEX"
                or "SIZEOF" => _integer,
            "HIBOUND" => HasUnboundedUpperLimit(parameters)
                ? _integer.WithIndeterminate(true)
                : _integer,
            "EXISTS" => _boolean,
            "ODD" or "VALUE_IN" or "VALUE_UNIQUE" => _logical,
            "FORMAT" => _string.WithIndeterminate(true),
            "NVL" => parameters.Length > 1 ? parameters[1].Type : _unresolved,
            "ROLESOF" or "TYPEOF" => BuiltInAggregateType(
                ExpressAggregateKind.Set,
                new ExpressBoundScalarType(
                    ExpressScalarKind.String,
                    constraintText: null,
                    isFixed: false,
                    parameters[0].Span)),
            "USEDIN" => BuiltInAggregateType(
                ExpressAggregateKind.Bag,
                new ExpressBoundGenericType(isEntity: true, typeLabel: null, parameters[0].Span)),
            _ => _unresolved,
        };
    }

    private static ExpressExpressionType BuiltInAggregateType(
        ExpressAggregateKind kind,
        ExpressBoundType elementType)
    {
        return new(
            ExpressExpressionTypeKind.Aggregate,
            new ExpressBoundAggregateType(
                kind,
                elementType,
                lowerBoundText: "0",
                upperBoundText: null,
                isOptional: false,
                isUnique: kind == ExpressAggregateKind.Set,
                typeLabel: null,
                elementType.Span));
    }

    private static bool HasUnboundedUpperLimit(ExpressBoundExpression[] parameters)
    {
        return parameters.Length == 0
            || parameters[0].Type.DeclaredType is ExpressBoundAggregateType
            {
                Kind: not ExpressAggregateKind.Array,
                UpperBoundText: null or "?",
            };
    }

    private static ExpressBoundExpression Create(
        ExpressExpressionKind kind,
        ExpressExpressionType type,
        ExpressRuleSyntax syntax,
        string? operation,
        ExpressBoundName? reference,
        IEnumerable<ExpressBoundExpression> children)
    {
        return Create(kind, type, syntax.Span, syntax.TokenText(), operation, reference, children);
    }

    private static ExpressBoundExpression Create(
        ExpressExpressionKind kind,
        ExpressExpressionType type,
        ExpressSourceSpan span,
        string sourceText,
        string? operation,
        ExpressBoundName? reference,
        IEnumerable<ExpressBoundExpression> children)
    {
        return new(kind, type, sourceText, operation, reference, children, span);
    }

    private static ExpressSourceSpan SliceSpan(
        ExpressSourceSpan start,
        ExpressSourceSpan end)
    {
        return new(start.Start, end.End);
    }

    private sealed class EmptyLexicalTypes : Dictionary<string, ExpressExpressionType>
    {
        internal static EmptyLexicalTypes Instance { get; } = [];

        private EmptyLexicalTypes()
            : base(StringComparer.OrdinalIgnoreCase)
        {
        }
    }
}