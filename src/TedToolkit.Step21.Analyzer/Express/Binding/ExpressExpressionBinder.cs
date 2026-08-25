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

    private readonly HashSet<ExpressBoundSymbol> _indeterminateFunctions = [];

    private readonly HashSet<ExpressBoundName> _indeterminateLocals = [];

    private readonly HashSet<ExpressBoundAttribute> _indeterminateDerivedAttributes = [];

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
    internal static (
        IReadOnlyList<ExpressBoundExpression> Expressions,
        HashSet<ExpressBoundSymbol> IndeterminateFunctions,
        HashSet<ExpressBoundName> IndeterminateLocals) Bind(
        IReadOnlyList<ExpressBoundDeclaration> declarations,
        IReadOnlyList<ExpressBoundNameReference> references)
    {
        var bySymbol = declarations.ToDictionary(declaration => declaration.Symbol);
        var binder = new ExpressExpressionBinder(references, bySymbol);
        var expectedTypes = binder.FindExpectedTypes(declarations);
        var assignmentExpressions = new HashSet<ExpressRuleSyntax>(declarations
            .SelectMany(declaration => declaration.Syntax.DescendantsAndSelf())
            .Where(candidate => candidate.Production == "assignmentStmt")
            .Select(candidate => candidate.RequiredChild("expression")));
        while (true)
        {
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
                        expectedTypes.TryGetValue(expression, out var expected) ? expected : null,
                        assignmentExpressions.Contains(expression)));
                }
            }

            var facts = binder.AnalyzeIndeterminateFunctionResults(declarations, result);
            if (facts.Functions.SetEquals(binder._indeterminateFunctions)
                && facts.Locals.SetEquals(binder._indeterminateLocals)
                && facts.DerivedAttributes.SetEquals(binder._indeterminateDerivedAttributes))
            {
                binder._selfType = null;
                return (result, facts.Functions, facts.Locals);
            }

            binder._indeterminateFunctions.UnionWith(facts.Functions);
            binder._indeterminateLocals.UnionWith(facts.Locals);
            binder._indeterminateDerivedAttributes.UnionWith(facts.DerivedAttributes);
        }
    }

    private (
        HashSet<ExpressBoundSymbol> Functions,
        HashSet<ExpressBoundName> Locals,
        HashSet<ExpressBoundAttribute> DerivedAttributes)
        AnalyzeIndeterminateFunctionResults(
            IReadOnlyList<ExpressBoundDeclaration> declarations,
            IReadOnlyList<ExpressBoundExpression> expressions)
    {
        var functions = new HashSet<ExpressBoundSymbol>(_indeterminateFunctions);
        var locals = new HashSet<ExpressBoundName>();
        foreach (var declaration in declarations.Where(candidate => candidate.Kind == ExpressDeclarationKind.Function))
        {
            var functionResultIsBoolean = declaration is ExpressBoundOpaqueDeclaration
            {
                DeclaredType: ExpressBoundScalarType { Kind: ExpressScalarKind.Boolean, },
            };
            var allAssignments = DescendantsInAlgorithm(declaration.Syntax)
                .Where(candidate => candidate.Production == "assignmentStmt")
                .ToArray();
            var assignments = allAssignments
                .Where(candidate => !candidate.ChildRules("qualifier").Any())
                .ToArray();
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var assignment in assignments)
                {
                    var target = FindReference(assignment.RequiredChild("generalRef").Span)?.Target;
                    var valueSyntax = assignment.RequiredChild("expression");
                    var value = expressions.Single(candidate => SameStart(candidate.Span, valueSyntax.Span));
                    if (target?.Kind == ExpressBoundNameKind.Variable
                        && (value.Type.CanBeIndeterminate
                            || (target.Type is ExpressBoundScalarType { Kind: ExpressScalarKind.Boolean, }
                                && value.Type.Kind == ExpressExpressionTypeKind.Logical)
                            || value.DescendantsAndSelf().Any(candidate =>
                                candidate.Reference is { } reference && locals.Contains(reference)))
                        && locals.Add(target))
                    {
                        changed = true;
                    }
                }
            }

            foreach (var assignment in allAssignments)
            {
                var target = FindReference(assignment.RequiredChild("generalRef").Span)?.Target;
                var targetType = target?.Type;
                var targetIsOptional = target?.IsOptional == true;
                foreach (var qualifier in assignment.ChildRules("qualifier"))
                {
                    var operation = qualifier.ChildRules().Single();
                    if (operation.Production == "attributeQualifier")
                    {
                        var attribute = FindReference(operation.Span)?.Target;
                        targetType = attribute?.Type;
                        targetIsOptional = attribute?.IsOptional == true;
                    }
                    else if (operation.Production == "indexQualifier"
                             && targetType is ExpressBoundAggregateType aggregate)
                    {
                        targetType = aggregate.ElementType;
                        targetIsOptional = aggregate.IsOptional;
                    }
                }

                var valueSyntax = assignment.RequiredChild("expression");
                var value = expressions.Single(candidate => SameStart(candidate.Span, valueSyntax.Span));
                if (!targetIsOptional && value.Type.CanBeIndeterminate)
                {
                    functions.Add(declaration.Symbol);
                    break;
                }
            }

            foreach (var returnStatement in DescendantsInAlgorithm(declaration.Syntax)
                         .Where(candidate => candidate.Production == "returnStmt"))
            {
                var returnSyntax = returnStatement.RequiredChild("expression");
                var value = expressions.Single(candidate => SameStart(candidate.Span, returnSyntax.Span));
                if (value.Type.CanBeIndeterminate
                    || (functionResultIsBoolean && value.Type.Kind == ExpressExpressionTypeKind.Logical)
                    || value.DescendantsAndSelf().Any(candidate =>
                        candidate.Reference is { } reference && locals.Contains(reference)))
                {
                    functions.Add(declaration.Symbol);
                    break;
                }
            }
        }

        var derivedAttributes = new HashSet<ExpressBoundAttribute>(_indeterminateDerivedAttributes);
        foreach (var entity in declarations.OfType<ExpressBoundEntity>())
        {
            foreach (var attribute in entity.Attributes.Where(candidate =>
                         candidate.Kind == ExpressAttributeKind.Derived))
            {
                var syntax = entity.Syntax.DescendantsAndSelf()
                    .Where(candidate => candidate.Production == "derivedAttr")
                    .Single(candidate => SameStart(
                        candidate.RequiredChild("attributeDecl").Span,
                        attribute.Span));
                var initializer = syntax.RequiredChild("expression");
                var expression = expressions.Single(candidate => SameStart(candidate.Span, initializer.Span));
                if (expression.Type.CanBeIndeterminate)
                {
                    derivedAttributes.Add(attribute);
                }
            }
        }

        return (functions, locals, derivedAttributes);
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
                var targetType = FindReference(syntax.RequiredChild("generalRef").Span)?.Target.Type;
                foreach (var qualifier in syntax.ChildRules("qualifier"))
                {
                    var operation = qualifier.ChildRules().Single();
                    if (operation.Production == "attributeQualifier")
                    {
                        targetType = FindReference(operation.Span)?.Target.Type;
                    }
                    else if (operation.Production == "indexQualifier"
                             && targetType is ExpressBoundAggregateType aggregate)
                    {
                        targetType = aggregate.ElementType;
                    }
                }

                if (targetType is not null)
                {
                    result[syntax.RequiredChild("expression")] = TypeOf(targetType);
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
        if (syntax.Production is "expression" or "numericExpression")
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
        ExpressExpressionType? expectedType = null,
        bool allowEntitySetNarrowing = false)
    {
        var simpleExpressions = syntax.ChildRules("simpleExpression").ToArray();
        var left = BindSimpleExpression(simpleExpressions[0], lexicalTypes);
        if (simpleExpressions.Length == 1)
        {
            var expression = ApplyExpectedType(left, expectedType, allowEntitySetNarrowing);
            return new(
                expression.Kind,
                expression.Type,
                syntax.TokenText(),
                expression.Operation,
                expression.Reference,
                expression.Children,
                syntax.Span);
        }

        var operation = syntax.RequiredChild("relOpExtended").TokenText();
        var right = BindSimpleExpression(simpleExpressions[1], lexicalTypes);
        if (operation is "=" or "<>")
        {
            if (left.Kind == ExpressExpressionKind.AggregateInitializer
                && right.Kind != ExpressExpressionKind.AggregateInitializer
                && right.Type.DeclaredType is ExpressBoundAggregateType)
            {
                left = ApplyExpectedType(left, right.Type);
            }
            else if (right.Kind == ExpressExpressionKind.AggregateInitializer
                     && left.Kind != ExpressExpressionKind.AggregateInitializer
                     && left.Type.DeclaredType is ExpressBoundAggregateType)
            {
                right = ApplyExpectedType(right, left.Type);
            }
        }

        return ApplyExpectedType(Create(
            ExpressExpressionKind.Binary,
            _logical,
            syntax,
            operation,
            reference: null,
            [left, right,]), expectedType, allowEntitySetNarrowing);
    }

    private ExpressBoundExpression ApplyExpectedType(
        ExpressBoundExpression expression,
        ExpressExpressionType? expectedType,
        bool allowEntitySetNarrowing = false)
    {
        if (expression.Kind == ExpressExpressionKind.Binary
            && expression.Operation == "||"
            && expression.Children.All(child => child.Type.Kind == ExpressExpressionTypeKind.Entity)
            && expectedType?.DeclaredType is ExpressBoundNamedType
            {
                Declaration.Kind: ExpressDeclarationKind.Entity,
            })
        {
            return new(
                expression.Kind,
                expectedType.WithIndeterminate(expression.Type.CanBeIndeterminate),
                expression.SourceText,
                expression.Operation,
                expression.Reference,
                expression.Children,
                expression.Span);
        }

        if (allowEntitySetNarrowing
            && expectedType?.DeclaredType is ExpressBoundAggregateType expectedSet
            && expectedSet.Kind == ExpressAggregateKind.Set
            && expectedSet.ElementType is ExpressBoundNamedType expectedElement
            && expectedElement.Declaration.Kind == ExpressDeclarationKind.Entity
            && expression.Type.DeclaredType is ExpressBoundAggregateType sourceSet
            && sourceSet.Kind == ExpressAggregateKind.Set)
        {
            var sourceCanContainTarget = sourceSet.ElementType is ExpressBoundGenericType { IsEntity: true, };
            if (sourceSet.ElementType is ExpressBoundNamedType
                { Declaration.Kind: ExpressDeclarationKind.Entity, } sourceElement
                && !ReferenceEquals(sourceElement.Declaration, expectedElement.Declaration))
            {
                var pendingEntities = new Queue<ExpressBoundSymbol>();
                var physicalClosure = new HashSet<ExpressBoundSymbol>();
                pendingEntities.Enqueue(expectedElement.Declaration);
                while (pendingEntities.Count > 0)
                {
                    var entity = pendingEntities.Dequeue();
                    if (!physicalClosure.Add(entity)
                        || !_declarations.TryGetValue(entity, out var declaration)
                        || declaration is not ExpressBoundEntity boundEntity)
                    {
                        continue;
                    }

                    foreach (var supertype in boundEntity.DirectSupertypes)
                    {
                        pendingEntities.Enqueue(supertype);
                    }
                }

                sourceCanContainTarget = physicalClosure.Contains(sourceElement.Declaration);
            }

            if (sourceCanContainTarget)
            {
                return new(
                    expression.Kind,
                    expression.Type.WithIndeterminate(true),
                    expression.SourceText,
                    expression.Operation,
                    expression.Reference,
                    expression.Children,
                    expression.Span);
            }
        }

        if (expression.Kind == ExpressExpressionKind.Query
            && expectedType?.DeclaredType is ExpressBoundAggregateType expectedQueryAggregate
            && expression.Type.DeclaredType is ExpressBoundAggregateType queryAggregate
            && expectedQueryAggregate.Kind == queryAggregate.Kind)
        {
            var expectedQueryElement = expectedQueryAggregate.ElementType;
            var expectedQueryElementType = expectedQueryElement;
            while (expectedQueryElementType is ExpressBoundNamedType expectedNamed
                   && expectedNamed.Declaration.Kind != ExpressDeclarationKind.Entity
                   && _declarations.TryGetValue(expectedNamed.Declaration, out var expectedDeclaration)
                   && expectedDeclaration is ExpressBoundDefinedType expectedDefined)
            {
                expectedQueryElementType = expectedDefined.UnderlyingType;
            }

            if (expectedQueryElementType is ExpressBoundSelectType)
            {
                var targetLeaves = new HashSet<ExpressBoundSymbol>();
                var pendingTargetTypes = new Stack<ExpressBoundType>();
                pendingTargetTypes.Push(expectedQueryElement);
                var visitedTargetDeclarations = new HashSet<ExpressBoundSymbol>();
                var isClosedEntitySelect = true;
                while (pendingTargetTypes.Count > 0 && isClosedEntitySelect)
                {
                    var pendingTarget = pendingTargetTypes.Pop();
                    if (pendingTarget is not ExpressBoundNamedType pendingNamedTarget)
                    {
                        isClosedEntitySelect = false;
                        break;
                    }

                    if (!visitedTargetDeclarations.Add(pendingNamedTarget.Declaration))
                    {
                        continue;
                    }

                    if (pendingNamedTarget.Declaration.Kind == ExpressDeclarationKind.Entity)
                    {
                        targetLeaves.Add(pendingNamedTarget.Declaration);
                        continue;
                    }

                    if (!_declarations.TryGetValue(pendingNamedTarget.Declaration, out var pendingDeclaration)
                        || pendingDeclaration is not ExpressBoundDefinedType pendingDefined
                        || pendingDefined.UnderlyingType is not ExpressBoundSelectType pendingSelect
                        || pendingSelect.IsExtensible)
                    {
                        isClosedEntitySelect = false;
                        break;
                    }

                    foreach (var alternative in pendingSelect.Alternatives)
                    {
                        pendingTargetTypes.Push(new ExpressBoundNamedType(alternative, pendingSelect.Span));
                    }

                    if (pendingSelect.BaseType is not null)
                    {
                        pendingTargetTypes.Push(new ExpressBoundNamedType(
                            pendingSelect.BaseType,
                            pendingSelect.Span));
                    }
                }

                ExpressBoundName? queryVariable = null;
                var predicateLeaves = new HashSet<ExpressBoundSymbol>();
                var pendingPredicates = new Stack<ExpressBoundExpression>();
                pendingPredicates.Push(expression.Children[1]);
                var isExhaustivePredicate = expression.Reference is null;
                var predicateTermCount = 0;
                while (pendingPredicates.Count > 0 && isExhaustivePredicate)
                {
                    var predicate = pendingPredicates.Pop();
                    if (predicate.Kind == ExpressExpressionKind.Binary
                        && predicate.Operation == "OR"
                        && predicate.Children.Count == 2)
                    {
                        pendingPredicates.Push(predicate.Children[1]);
                        pendingPredicates.Push(predicate.Children[0]);
                        continue;
                    }

                    if (predicate.Kind != ExpressExpressionKind.Binary
                        || predicate.Operation != "IN"
                        || predicate.Children.Count != 2
                        || predicate.Children[0].Kind != ExpressExpressionKind.Literal
                        || predicate.Children[0].Type.Kind != ExpressExpressionTypeKind.String
                        || predicate.Children[1].Kind != ExpressExpressionKind.Application
                        || predicate.Children[1].Operation != "TYPEOF"
                        || predicate.Children[1].Children.Count != 1
                        || predicate.Children[1].Children[0].Reference is not
                        { Kind: ExpressBoundNameKind.QueryVariable, } candidateVariable
                        || !string.Equals(candidateVariable.Name, expression.Operation, StringComparison.OrdinalIgnoreCase)
                        || (queryVariable is not null && !ReferenceEquals(queryVariable, candidateVariable)))
                    {
                        isExhaustivePredicate = false;
                        break;
                    }

                    queryVariable = candidateVariable;
                    var sourceText = predicate.Children[0].SourceText;
                    var qualifiedName = sourceText.Length >= 2
                        ? sourceText.Substring(1, sourceText.Length - 2)
                        : "";
                    var selected = _declarations.Keys.SingleOrDefault(candidate =>
                        candidate.Kind == ExpressDeclarationKind.Entity
                        && string.Equals(
                            candidate.DeclaringSchema.Name + "." + candidate.Name,
                            qualifiedName,
                            StringComparison.OrdinalIgnoreCase));
                    predicateTermCount++;
                    if (selected is null || !predicateLeaves.Add(selected))
                    {
                        isExhaustivePredicate = false;
                    }
                }

                if (!isClosedEntitySelect
                    || !isExhaustivePredicate
                    || queryVariable is null
                    || predicateTermCount != targetLeaves.Count
                    || !predicateLeaves.SetEquals(targetLeaves))
                {
                    return expression;
                }

                var specializedSource = ApplyExpectedType(expression.Children[0], expectedType);
                return new(
                    expression.Kind,
                    expectedType.WithIndeterminate(
                        expression.Type.CanBeIndeterminate || specializedSource.Type.CanBeIndeterminate),
                    expression.SourceText,
                    expression.Operation,
                    queryVariable,
                    [specializedSource, expression.Children[1],],
                    expression.Span);
            }

            var source = ApplyExpectedType(expression.Children[0], expectedType);
            return new(
                expression.Kind,
                expectedType.WithIndeterminate(
                    expression.Type.CanBeIndeterminate || source.Type.CanBeIndeterminate),
                expression.SourceText,
                expression.Operation,
                expression.Reference,
                [source, expression.Children[1],],
                expression.Span);
        }

        if (expression.Kind == ExpressExpressionKind.Application
            && expectedType?.DeclaredType is ExpressBoundAggregateType expectedAggregate
            && expression.Type.DeclaredType is ExpressBoundAggregateType resultAggregate
            && resultAggregate.Kind == expectedAggregate.Kind
            && expression.Reference?.SchemaDeclaration is { } functionSymbol
            && _declarations.TryGetValue(functionSymbol, out var boundDeclaration)
            && boundDeclaration is ExpressBoundOpaqueDeclaration opaque
            && opaque.Kind == ExpressDeclarationKind.Function
            && opaque.DeclaredType is ExpressBoundAggregateType declaredResultAggregate
            && opaque.Syntax.RequiredChild("functionHead")
                .ChildRules("formalParameter")
                .SelectMany(formal => formal.ChildRules("parameterId"))
                .FirstOrDefault() is { } firstParameter
            && _references.Select(candidate => candidate.Target)
                .Where(candidate => candidate.Kind == ExpressBoundNameKind.Parameter
                    && SameStart(candidate.Span, firstParameter.Span))
                .Distinct()
                .SingleOrDefault()?.Type is ExpressBoundAggregateType parameterAggregate
            && expression.Children.Count > 0
            && expression.Children[0] is { } firstArgument
            && firstArgument.Type.DeclaredType is ExpressBoundAggregateType actualAggregate
            && TrySpecializeGenericType(
                declaredResultAggregate,
                expectedAggregate,
                requiredLabel: null,
                requiredSubstitution: null,
                out var specializedResultType,
                out var resultLabel,
                out var substitution)
            && resultLabel is not null
            && substitution is not null
            && TrySpecializeGenericType(
                parameterAggregate,
                actualAggregate,
                resultLabel,
                substitution,
                out var specializedArgumentType,
                out _,
                out _)
            && specializedResultType is ExpressBoundAggregateType specializedResult
            && specializedArgumentType is ExpressBoundAggregateType specializedArgumentAggregate)
        {
            var specializedArgument = new ExpressBoundExpression(
                firstArgument.Kind,
                TypeOf(specializedArgumentAggregate)
                    .WithIndeterminate(firstArgument.Type.CanBeIndeterminate),
                firstArgument.SourceText,
                firstArgument.Operation,
                firstArgument.Reference,
                firstArgument.Children,
                firstArgument.Span);
            return new(
                expression.Kind,
                TypeOf(specializedResult).WithIndeterminate(expression.Type.CanBeIndeterminate),
                expression.SourceText,
                expression.Operation,
                expression.Reference,
                [specializedArgument, .. expression.Children.Skip(1),],
                expression.Span);
        }

        if (expression.Kind == ExpressExpressionKind.Indeterminate && expectedType is not null)
        {
            return new(
                expression.Kind,
                expectedType.WithIndeterminate(true),
                expression.SourceText,
                expression.Operation,
                expression.Reference,
                expression.Children,
                expression.Span);
        }

        if (expression.Kind != ExpressExpressionKind.AggregateInitializer
            || expectedType?.DeclaredType is not ExpressBoundAggregateType aggregate)
        {
            return expression;
        }

        var elementType = FromBoundType(aggregate.ElementType);
        var children = expression.Children.Select(child =>
        {
            if (child.Kind != ExpressExpressionKind.Repetition)
            {
                return ApplyElementType(child, elementType);
            }

            var repeatedValue = ApplyElementType(child.Children[0], elementType);
            return new ExpressBoundExpression(
                child.Kind,
                elementType.WithIndeterminate(repeatedValue.Type.CanBeIndeterminate),
                child.SourceText,
                child.Operation,
                child.Reference,
                [repeatedValue, child.Children[1],],
                child.Span);
        })
            .ToArray();
        var boundsCanBeIndeterminate = aggregate.Kind == ExpressAggregateKind.Array
            && (!int.TryParse(
                    aggregate.ResolvedLowerBoundText ?? aggregate.LowerBoundText,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var lower)
                || !int.TryParse(
                    aggregate.ResolvedUpperBoundText ?? aggregate.UpperBoundText,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var upper)
                || upper < lower);
        return new(
            expression.Kind,
            expectedType.WithIndeterminate(
                boundsCanBeIndeterminate
                || expression.Children.Any(child => child.Kind == ExpressExpressionKind.Repetition
                    && !IsStaticallyValidRepetition(child.Children[1]))
                || (!aggregate.IsOptional
                    && children.Any(child => child.Type.CanBeIndeterminate))),
            expression.SourceText,
            expression.Operation,
            expression.Reference,
            children,
            expression.Span);
    }

    private static bool TrySpecializeGenericType(
        ExpressBoundType template,
        ExpressBoundType actual,
        string? requiredLabel,
        ExpressBoundType? requiredSubstitution,
        out ExpressBoundType specialized,
        out string? label,
        out ExpressBoundType? substitution)
    {
        label = requiredLabel;
        substitution = requiredSubstitution;

        if (template is ExpressBoundAggregateType templateAggregate)
        {
            var templateLowerText = templateAggregate.ResolvedLowerBoundText
                ?? templateAggregate.LowerBoundText;
            var templateUpperText = templateAggregate.ResolvedUpperBoundText
                ?? templateAggregate.UpperBoundText;
            var actualLowerText = (actual as ExpressBoundAggregateType)?.ResolvedLowerBoundText
                ?? (actual as ExpressBoundAggregateType)?.LowerBoundText;
            var actualUpperText = (actual as ExpressBoundAggregateType)?.ResolvedUpperBoundText
                ?? (actual as ExpressBoundAggregateType)?.UpperBoundText;
            var hasTemplateLower = int.TryParse(
                templateLowerText,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var templateLower);
            var hasActualLower = int.TryParse(
                actualLowerText,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var actualLower);
            var hasTemplateUpper = int.TryParse(
                templateUpperText,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var templateUpper);
            var hasActualUpper = int.TryParse(
                actualUpperText,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var actualUpper);
            var boundsCompatible = templateAggregate.Kind == ExpressAggregateKind.Array
                ? (!hasTemplateLower || !hasActualLower || templateLower == actualLower)
                    && (!hasTemplateUpper || !hasActualUpper || templateUpper == actualUpper)
                : (templateLowerText is null
                        || (hasTemplateLower && hasActualLower && actualLower >= templateLower)
                        || string.Equals(templateLowerText, actualLowerText, StringComparison.OrdinalIgnoreCase))
                    && (templateUpperText is null
                        || templateUpperText == "?"
                        || (hasTemplateUpper && hasActualUpper && actualUpper <= templateUpper)
                        || string.Equals(templateUpperText, actualUpperText, StringComparison.OrdinalIgnoreCase));
            if (actual is not ExpressBoundAggregateType actualAggregate
                || templateAggregate.Kind != actualAggregate.Kind
                || !boundsCompatible
                || !TrySpecializeGenericType(
                    templateAggregate.ElementType,
                    actualAggregate.ElementType,
                    label,
                    substitution,
                    out var specializedElement,
                    out label,
                    out substitution))
            {
                specialized = template;
                return false;
            }

            specialized = new ExpressBoundAggregateType(
                templateAggregate.Kind,
                specializedElement,
                templateAggregate.LowerBoundText,
                templateAggregate.UpperBoundText,
                templateAggregate.IsOptional,
                templateAggregate.IsUnique,
                templateAggregate.TypeLabel,
                templateAggregate.Span,
                templateAggregate.ResolvedLowerBoundText,
                templateAggregate.ResolvedUpperBoundText);
            return true;
        }

        if (template is ExpressBoundGenericType templateGeneric
            && templateGeneric.TypeLabel is { } templateLabel)
        {
            if (label is not null
                && !string.Equals(label, templateLabel, StringComparison.OrdinalIgnoreCase))
            {
                specialized = template;
                return false;
            }

            label ??= templateLabel;
            if (actual is ExpressBoundGenericType actualGeneric)
            {
                if (actualGeneric.TypeLabel is null
                    || !string.Equals(actualGeneric.TypeLabel, templateLabel, StringComparison.OrdinalIgnoreCase)
                    || actualGeneric.IsEntity != templateGeneric.IsEntity)
                {
                    specialized = template;
                    return false;
                }

                substitution ??= actual;
                specialized = substitution;
                return true;
            }

            if (templateGeneric.IsEntity
                && actual is not ExpressBoundNamedType
                {
                    Declaration.Kind: ExpressDeclarationKind.Entity,
                })
            {
                specialized = template;
                return false;
            }

            if (substitution is null)
            {
                substitution = actual;
                specialized = actual;
                return true;
            }

            if (!TrySpecializeGenericType(
                substitution,
                actual,
                requiredLabel: null,
                requiredSubstitution: null,
                out _,
                out _,
                out _))
            {
                specialized = template;
                return false;
            }

            specialized = substitution;
            return true;
        }

        var compatible = template switch
        {
            ExpressBoundScalarType templateScalar when actual is ExpressBoundScalarType actualScalar =>
                templateScalar.Kind == actualScalar.Kind,
            ExpressBoundNamedType templateNamed when actual is ExpressBoundNamedType actualNamed =>
                ReferenceEquals(templateNamed.Declaration, actualNamed.Declaration),
            _ => template.GetType() == actual.GetType(),
        };
        specialized = template;
        return compatible;
    }

    private ExpressBoundExpression ApplyElementType(
        ExpressBoundExpression expression,
        ExpressExpressionType expectedType)
    {
        if (expression.Kind == ExpressExpressionKind.AggregateInitializer)
        {
            return ApplyExpectedType(expression, expectedType);
        }

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

    private ExpressBoundExpression BindLeftAssociative(
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

        var reference = FindReference(syntax.Span);
        if (reference is null
            && (string.Equals(text, "E", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "PI", StringComparison.OrdinalIgnoreCase)))
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

        var referencedFunctionIsIndeterminate = reference?.Target.SchemaDeclaration is { } referencedSymbol
            && _indeterminateFunctions.Contains(referencedSymbol);
        var referencedLocalIsIndeterminate = reference is not null
            && _indeterminateLocals.Contains(reference.Target);
        var referencedDerivedIsIndeterminate = reference?.Target.AttributeCandidates.Any(
            _indeterminateDerivedAttributes.Contains) == true;
        var type = reference is not null
            && lexicalTypes.TryGetValue(reference.Target.Name, out var lexicalType)
                ? lexicalType
                : TypeOf(reference?.Target.Type)
                    .WithIndeterminate(reference?.Target.IsOptional == true
                        || referencedFunctionIsIndeterminate
                        || referencedLocalIsIndeterminate
                        || referencedDerivedIsIndeterminate);
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
        if (builtin is null
            && reference?.Target.SchemaDeclaration is { } calledSymbol
            && _declarations.TryGetValue(calledSymbol, out var calledDeclaration)
            && calledDeclaration is ExpressBoundOpaqueDeclaration calledFunction
            && calledFunction.Kind == ExpressDeclarationKind.Function)
        {
            var formalTypes = calledFunction.Syntax.RequiredChild("functionHead")
                .ChildRules("formalParameter")
                .SelectMany(formal => formal.ChildRules("parameterId"))
                .Select(parameter => _references.Select(candidate => candidate.Target)
                    .Where(candidate => candidate.Kind == ExpressBoundNameKind.Parameter
                        && SameStart(candidate.Span, parameter.Span))
                    .Distinct()
                    .SingleOrDefault()?.Type)
                .ToArray();
            if (formalTypes.Length == parameters.Length)
            {
                for (var index = 0; index < parameters.Length; index++)
                {
                    var initializer = parameters[index];
                    if (initializer.Kind != ExpressExpressionKind.AggregateInitializer
                        || initializer.Children.Count > 1
                        || initializer.Children.Any(child => child.Kind == ExpressExpressionKind.Repetition)
                        || formalTypes[index] is not ExpressBoundAggregateType formalSet
                        || formalSet.Kind != ExpressAggregateKind.Set)
                    {
                        continue;
                    }

                    var cardinality = initializer.Children.Count;
                    var lowerText = formalSet.ResolvedLowerBoundText ?? formalSet.LowerBoundText;
                    var upperText = formalSet.ResolvedUpperBoundText ?? formalSet.UpperBoundText;
                    var lower = 0;
                    var upper = int.MaxValue;
                    var hasLower = lowerText is null
                        || int.TryParse(lowerText, out lower);
                    var hasUpper = upperText is null
                        || upperText == "?"
                        || int.TryParse(upperText, out upper);
                    if (!hasLower
                        || !hasUpper
                        || cardinality < lower
                        || cardinality > upper)
                    {
                        continue;
                    }

                    parameters[index] = ApplyExpectedType(initializer, TypeOf(formalSet));
                }
            }
        }

        var type = builtin is null
            ? TypeOf(reference?.Target.Type).WithIndeterminate(
                reference?.Target.SchemaDeclaration is { } referencedSymbol
                && _indeterminateFunctions.Contains(referencedSymbol))
            : BuiltInResultType(operation, parameters);
        if (builtin is not null
            && string.Equals(operation, "USEDIN", StringComparison.OrdinalIgnoreCase)
            && parameters.Length == 2)
        {
            var roleParts = parameters[1].DescendantsAndSelf().ToArray();
            var roleIsStatic = roleParts.All(part =>
                (part.Kind == ExpressExpressionKind.Binary && part.Operation == "+")
                || (part.Kind == ExpressExpressionKind.Literal
                    && part.Type.Kind == ExpressExpressionTypeKind.String
                    && part.SourceText.Length >= 2
                    && part.SourceText[0] == '\''
                    && part.SourceText[part.SourceText.Length - 1] == '\''));
            if (roleIsStatic)
            {
                var role = string.Concat(roleParts
                    .Where(part => part.Kind == ExpressExpressionKind.Literal)
                    .Select(part => part.SourceText.Substring(1, part.SourceText.Length - 2).Replace("''", "'")));
                var roleOwners = _declarations.Values.OfType<ExpressBoundEntity>()
                    .Where(entity => entity.Attributes.Any(attribute => string.Equals(
                            role,
                            $"{entity.Symbol.DeclaringSchema.Name}.{entity.Name}.{attribute.Name}",
                            StringComparison.OrdinalIgnoreCase)))
                    .ToArray();
                if (roleOwners.Length == 1)
                {
                    type = BuiltInAggregateType(
                        ExpressAggregateKind.Bag,
                        new ExpressBoundNamedType(roleOwners[0].Symbol, parameters[1].Span));
                }
            }
        }

        if (builtin is null
            && reference?.Target.SchemaDeclaration is { } scalarFunctionSymbol
            && _declarations.TryGetValue(scalarFunctionSymbol, out var scalarBoundDeclaration)
            && scalarBoundDeclaration is ExpressBoundOpaqueDeclaration scalarOpaque
            && scalarOpaque.Kind == ExpressDeclarationKind.Function
            && scalarOpaque.DeclaredType is ExpressBoundGenericType)
        {
            var formalTypes = scalarOpaque.Syntax.RequiredChild("functionHead")
                .ChildRules("formalParameter")
                .SelectMany(formal => formal.ChildRules("parameterId"))
                .Select(parameter => _references.Select(candidate => candidate.Target)
                    .Where(candidate => candidate.Kind == ExpressBoundNameKind.Parameter
                        && SameStart(candidate.Span, parameter.Span))
                    .Distinct()
                    .SingleOrDefault()?.Type)
                .ToArray();
            string? inferredLabel = null;
            ExpressBoundType? inferredSubstitution = null;
            var compatible = formalTypes.Length == parameters.Length
                && formalTypes.All(formal => formal is not null);
            for (var index = 0; compatible && index < formalTypes.Length; index++)
            {
                var actualType = parameters[index].Type.DeclaredType;
                if (actualType is null
                    && parameters[index].Type.Kind is ExpressExpressionTypeKind.Binary
                        or ExpressExpressionTypeKind.Boolean
                        or ExpressExpressionTypeKind.Integer
                        or ExpressExpressionTypeKind.Logical
                        or ExpressExpressionTypeKind.Number
                        or ExpressExpressionTypeKind.Real
                        or ExpressExpressionTypeKind.String)
                {
                    actualType = ScalarType(parameters[index].Type, parameters[index].Span);
                }

                if (actualType is null
                    || !TrySpecializeGenericType(
                        formalTypes[index]!,
                        actualType,
                        inferredLabel,
                        inferredSubstitution,
                        out _,
                        out inferredLabel,
                        out inferredSubstitution))
                {
                    compatible = false;
                }
            }

            if (compatible
                && inferredLabel is not null
                && inferredSubstitution is not null
                && TrySpecializeGenericType(
                    scalarOpaque.DeclaredType,
                    inferredSubstitution,
                    inferredLabel,
                    inferredSubstitution,
                    out var specializedResult,
                    out _,
                    out _))
            {
                type = TypeOf(specializedResult).WithIndeterminate(
                    type.CanBeIndeterminate || parameters.Any(parameter => parameter.Type.CanBeIndeterminate));
            }
        }

        if (builtin is null
            && reference?.Target.SchemaDeclaration is { } functionSymbol
            && _declarations.TryGetValue(functionSymbol, out var boundDeclaration)
            && boundDeclaration is ExpressBoundOpaqueDeclaration opaque
            && opaque.Kind == ExpressDeclarationKind.Function
            && opaque.DeclaredType is ExpressBoundAggregateType resultAggregate
            && resultAggregate.ElementType is ExpressBoundGenericType { TypeLabel: { } resultLabel, }
            && parameters.FirstOrDefault()?.Type.DeclaredType is ExpressBoundAggregateType
                actualAggregate
            && opaque.Syntax.RequiredChild("functionHead")
                .ChildRules("formalParameter")
                .SelectMany(formal => formal.ChildRules("parameterId"))
                .FirstOrDefault() is { } firstParameter
            && _references.Select(candidate => candidate.Target)
                .Where(candidate => candidate.Kind == ExpressBoundNameKind.Parameter
                    && SameStart(candidate.Span, firstParameter.Span))
                .Distinct()
                .SingleOrDefault()?.Type is ExpressBoundAggregateType parameterAggregate
            && parameterAggregate.ElementType is ExpressBoundGenericType { TypeLabel: { } parameterLabel, }
            && string.Equals(resultLabel, parameterLabel, StringComparison.OrdinalIgnoreCase)
            && (actualAggregate.ElementType is not ExpressBoundGenericType
                || actualAggregate.ElementType is ExpressBoundGenericType
                {
                    IsEntity: true,
                    TypeLabel: null,
                }))
        {
            type = TypeOf(new ExpressBoundAggregateType(
                resultAggregate.Kind,
                actualAggregate.ElementType,
                resultAggregate.LowerBoundText,
                resultAggregate.UpperBoundText,
                resultAggregate.IsOptional,
                resultAggregate.IsUnique,
                resultAggregate.TypeLabel,
                resultAggregate.Span,
                resultAggregate.ResolvedLowerBoundText,
                resultAggregate.ResolvedUpperBoundText));
        }

        if (builtin is null
            && reference?.Target.SchemaDeclaration is { } nestedFunctionSymbol
            && _declarations.TryGetValue(nestedFunctionSymbol, out var nestedBoundDeclaration)
            && nestedBoundDeclaration is ExpressBoundOpaqueDeclaration nestedOpaque
            && nestedOpaque.Kind == ExpressDeclarationKind.Function
            && nestedOpaque.DeclaredType is ExpressBoundAggregateType nestedResultAggregate
            && nestedResultAggregate.ElementType is ExpressBoundAggregateType
            && parameters.FirstOrDefault()?.Type.DeclaredType is ExpressBoundAggregateType nestedActualAggregate
            && nestedOpaque.Syntax.RequiredChild("functionHead")
                .ChildRules("formalParameter")
                .SelectMany(formal => formal.ChildRules("parameterId"))
                .FirstOrDefault() is { } nestedFirstParameter
            && _references.Select(candidate => candidate.Target)
                .Where(candidate => candidate.Kind == ExpressBoundNameKind.Parameter
                    && SameStart(candidate.Span, nestedFirstParameter.Span))
                .Distinct()
                .SingleOrDefault()?.Type is ExpressBoundAggregateType nestedParameterAggregate
            && TrySpecializeGenericType(
                nestedParameterAggregate,
                nestedActualAggregate,
                requiredLabel: null,
                requiredSubstitution: null,
                out _,
                out var nestedLabel,
                out var nestedSubstitution)
            && nestedLabel is not null
            && nestedSubstitution is not null
            && TrySpecializeGenericType(
                nestedResultAggregate,
                nestedResultAggregate,
                nestedLabel,
                nestedSubstitution,
                out var nestedSpecializedResult,
                out _,
                out _)
            && nestedSpecializedResult is ExpressBoundAggregateType nestedSpecializedResultAggregate)
        {
            type = TypeOf(nestedSpecializedResultAggregate).WithIndeterminate(type.CanBeIndeterminate);
        }

        if (builtin is null
            && reference?.Target.SchemaDeclaration is { } dynamicFunctionSymbol
            && _declarations.TryGetValue(dynamicFunctionSymbol, out var dynamicBoundDeclaration)
            && dynamicBoundDeclaration is ExpressBoundOpaqueDeclaration dynamicFunction
            && dynamicFunction.Kind == ExpressDeclarationKind.Function)
        {
            var formalTypes = dynamicFunction.Syntax.RequiredChild("functionHead")
                .ChildRules("formalParameter")
                .SelectMany(formal => formal.ChildRules("parameterId"))
                .Select(parameter => _references.Select(candidate => candidate.Target)
                    .Where(candidate => candidate.Kind == ExpressBoundNameKind.Parameter
                        && SameStart(candidate.Span, parameter.Span))
                    .Distinct()
                    .SingleOrDefault()?.Type)
                .ToArray();
            if (formalTypes.Length == parameters.Length
                && formalTypes.All(formal => formal is not null))
            {
                for (var index = 0; index < parameters.Length && !type.CanBeIndeterminate; index++)
                {
                    if (formalTypes[index] is ExpressBoundNamedType
                        { Declaration.Kind: ExpressDeclarationKind.Entity, } formalEntity
                        && parameters[index].Type.DeclaredType is ExpressBoundNamedType actualSelectName
                        && actualSelectName.Declaration.Kind != ExpressDeclarationKind.Entity
                        && _declarations.TryGetValue(actualSelectName.Declaration, out var actualDeclaration)
                        && actualDeclaration is ExpressBoundDefinedType
                        { UnderlyingType: ExpressBoundSelectType, })
                    {
                        var pendingActualTypes = new Stack<ExpressBoundType>();
                        pendingActualTypes.Push(actualSelectName);
                        var visitedActualTypes = new HashSet<ExpressBoundSymbol>();
                        var hasCompatibleActual = false;
                        var hasIncompatibleActual = false;
                        while (pendingActualTypes.Count > 0)
                        {
                            var pendingActual = pendingActualTypes.Pop();
                            if (pendingActual is ExpressBoundSelectType pendingActualSelect)
                            {
                                foreach (var alternative in pendingActualSelect.Alternatives)
                                {
                                    pendingActualTypes.Push(new ExpressBoundNamedType(
                                        alternative,
                                        pendingActualSelect.Span));
                                }

                                if (pendingActualSelect.BaseType is not null)
                                {
                                    pendingActualTypes.Push(new ExpressBoundNamedType(
                                        pendingActualSelect.BaseType,
                                        pendingActualSelect.Span));
                                }

                                continue;
                            }

                            if (pendingActual is not ExpressBoundNamedType pendingActualNamed)
                            {
                                hasIncompatibleActual = true;
                                continue;
                            }

                            if (!visitedActualTypes.Add(pendingActualNamed.Declaration))
                            {
                                continue;
                            }

                            if (pendingActualNamed.Declaration.Kind != ExpressDeclarationKind.Entity)
                            {
                                if (_declarations.TryGetValue(
                                        pendingActualNamed.Declaration,
                                        out var pendingActualDeclaration)
                                    && pendingActualDeclaration is ExpressBoundDefinedType pendingActualDefined)
                                {
                                    pendingActualTypes.Push(pendingActualDefined.UnderlyingType);
                                }

                                continue;
                            }

                            var pendingEntities = new Queue<ExpressBoundSymbol>();
                            var physicalClosure = new HashSet<ExpressBoundSymbol>();
                            pendingEntities.Enqueue(pendingActualNamed.Declaration);
                            while (pendingEntities.Count > 0)
                            {
                                var entity = pendingEntities.Dequeue();
                                if (!physicalClosure.Add(entity)
                                    || !_declarations.TryGetValue(entity, out var entityDeclaration)
                                    || entityDeclaration is not ExpressBoundEntity boundEntity)
                                {
                                    continue;
                                }

                                foreach (var supertype in boundEntity.DirectSupertypes)
                                {
                                    pendingEntities.Enqueue(supertype);
                                }
                            }

                            if (physicalClosure.Contains(formalEntity.Declaration))
                            {
                                hasCompatibleActual = true;
                            }
                            else
                            {
                                hasIncompatibleActual = true;
                            }
                        }

                        if (hasCompatibleActual && hasIncompatibleActual)
                        {
                            type = type.WithIndeterminate(true);
                        }

                        continue;
                    }

                    if (parameters[index].Type.DeclaredType is not ExpressBoundNamedType
                        { Declaration.Kind: ExpressDeclarationKind.Entity, } actualEntity)
                    {
                        continue;
                    }

                    var pendingTypes = new Stack<ExpressBoundType>();
                    pendingTypes.Push(formalTypes[index]!);
                    var visitedTypes = new HashSet<ExpressBoundSymbol>();
                    var matchingEntities = new List<(
                        ExpressBoundSymbol Leaf,
                        HashSet<ExpressBoundSymbol> PhysicalClosure)>();
                    while (pendingTypes.Count > 0)
                    {
                        var pendingType = pendingTypes.Pop();
                        if (pendingType is ExpressBoundSelectType pendingSelect)
                        {
                            foreach (var alternative in pendingSelect.Alternatives)
                            {
                                pendingTypes.Push(new ExpressBoundNamedType(alternative, pendingSelect.Span));
                            }

                            if (pendingSelect.BaseType is not null)
                            {
                                pendingTypes.Push(new ExpressBoundNamedType(
                                    pendingSelect.BaseType,
                                    pendingSelect.Span));
                            }

                            continue;
                        }

                        if (pendingType is not ExpressBoundNamedType pendingNamed
                            || !visitedTypes.Add(pendingNamed.Declaration))
                        {
                            continue;
                        }

                        if (pendingNamed.Declaration.Kind != ExpressDeclarationKind.Entity)
                        {
                            if (_declarations.TryGetValue(pendingNamed.Declaration, out var pendingDeclaration)
                                && pendingDeclaration is ExpressBoundDefinedType pendingDefined)
                            {
                                pendingTypes.Push(pendingDefined.UnderlyingType);
                            }

                            continue;
                        }

                        var physicalClosure = new HashSet<ExpressBoundSymbol>();
                        var pendingEntities = new Queue<ExpressBoundSymbol>();
                        pendingEntities.Enqueue(pendingNamed.Declaration);
                        while (pendingEntities.Count > 0)
                        {
                            var entity = pendingEntities.Dequeue();
                            if (!physicalClosure.Add(entity)
                                || !_declarations.TryGetValue(entity, out var entityDeclaration)
                                || entityDeclaration is not ExpressBoundEntity boundEntity)
                            {
                                continue;
                            }

                            foreach (var supertype in boundEntity.DirectSupertypes)
                            {
                                pendingEntities.Enqueue(supertype);
                            }
                        }

                        if (!ReferenceEquals(pendingNamed.Declaration, actualEntity.Declaration)
                            && physicalClosure.Contains(actualEntity.Declaration))
                        {
                            matchingEntities.Add((pendingNamed.Declaration, physicalClosure));
                        }
                    }

                    var hasOverlappingAlternatives = matchingEntities.SelectMany(
                            (left, leftIndex) => matchingEntities.Skip(leftIndex + 1)
                                .Select(right => (Left: left, Right: right)))
                        .Any(pair => pair.Left.PhysicalClosure.Contains(pair.Right.Leaf)
                            || pair.Right.PhysicalClosure.Contains(pair.Left.Leaf));
                    if (matchingEntities.Count > 0 && !hasOverlappingAlternatives)
                    {
                        type = type.WithIndeterminate(true);
                    }
                }
            }
        }

        if (parameters.Any(parameter => parameter.Type.CanBeIndeterminate)
            && (builtin is null || operation is not ("EXISTS" or "NVL")))
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
            var targetDerivedIsIndeterminate = target?.AttributeCandidates.Any(
                _indeterminateDerivedAttributes.Contains) == true;
            ExpressBoundType? sourceType = source.Type.DeclaredType;
            while (sourceType is ExpressBoundNamedType named
                   && named.Declaration.Kind != ExpressDeclarationKind.Entity
                   && _declarations.TryGetValue(named.Declaration, out var namedDeclaration)
                   && namedDeclaration is ExpressBoundDefinedType defined)
            {
                sourceType = defined.UnderlyingType;
            }

            var selectAttributeCanBeIndeterminate = false;
            if (sourceType is ExpressBoundSelectType select && target is not null)
            {
                var alternatives = new List<ExpressBoundSymbol>(select.Alternatives);
                var baseType = select.BaseType;
                while (baseType is not null
                       && _declarations.TryGetValue(baseType, out var baseDeclaration)
                       && baseDeclaration is ExpressBoundDefinedType
                       {
                           UnderlyingType: ExpressBoundSelectType baseSelect,
                       })
                {
                    alternatives.AddRange(baseSelect.Alternatives);
                    baseType = baseSelect.BaseType;
                }

                selectAttributeCanBeIndeterminate = select.IsExtensible
                    || alternatives.Any(alternative =>
                        !_declarations.TryGetValue(alternative, out var alternativeDeclaration)
                        || alternativeDeclaration is not ExpressBoundEntity alternativeEntity
                        || !EnumerateAttributes(alternativeEntity, new HashSet<ExpressBoundSymbol>())
                            .Any(target.AttributeCandidates.Contains));
            }

            return Create(
                ExpressExpressionKind.AttributeQualifier,
                TypeOf(target?.Type)
                    .WithIndeterminate(
                        source.Type.CanBeIndeterminate
                        || target?.IsOptional == true
                        || targetDerivedIsIndeterminate
                        || selectAttributeCanBeIndeterminate),
                SliceSpan(source.Span, syntax.Span),
                string.Concat(source.SourceText, qualifier.TokenText()),
                qualifier.TokenText(),
                target,
                [source,]);
        }

        if (qualifier.Production == "groupQualifier")
        {
            var reference = FindReference(qualifier.RequiredChild("entityRef").Span);
            var selectGroupCanBeIndeterminate = false;
            ExpressBoundType? sourceType = source.Type.DeclaredType;
            while (sourceType is ExpressBoundNamedType named
                   && named.Declaration.Kind != ExpressDeclarationKind.Entity
                   && _declarations.TryGetValue(named.Declaration, out var namedDeclaration)
                   && namedDeclaration is ExpressBoundDefinedType defined)
            {
                sourceType = defined.UnderlyingType;
            }

            if (sourceType is ExpressBoundSelectType select
                && reference?.Target.SchemaDeclaration is { } group)
            {
                var alternatives = new List<ExpressBoundSymbol>(select.Alternatives);
                var baseType = select.BaseType;
                while (baseType is not null
                       && _declarations.TryGetValue(baseType, out var baseDeclaration)
                       && baseDeclaration is ExpressBoundDefinedType
                       {
                           UnderlyingType: ExpressBoundSelectType baseSelect,
                       })
                {
                    alternatives.AddRange(baseSelect.Alternatives);
                    baseType = baseSelect.BaseType;
                }

                selectGroupCanBeIndeterminate = select.IsExtensible;
                foreach (var alternative in alternatives)
                {
                    var pending = new Queue<ExpressBoundSymbol>();
                    var visited = new HashSet<ExpressBoundSymbol>();
                    pending.Enqueue(alternative);
                    while (pending.Count > 0)
                    {
                        var candidate = pending.Dequeue();
                        if (!visited.Add(candidate)
                            || !_declarations.TryGetValue(candidate, out var candidateDeclaration)
                            || candidateDeclaration is not ExpressBoundEntity candidateEntity)
                        {
                            continue;
                        }

                        foreach (var supertype in candidateEntity.DirectSupertypes)
                        {
                            pending.Enqueue(supertype);
                        }
                    }

                    if (!visited.Contains(group))
                    {
                        selectGroupCanBeIndeterminate = true;
                        break;
                    }
                }
            }

            return Create(
                ExpressExpressionKind.GroupQualifier,
                TypeOf(reference?.Target.Type)
                    .WithIndeterminate(source.Type.CanBeIndeterminate || selectGroupCanBeIndeterminate),
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
        var isStaticallyPresent = false;
        if (!isSlice
            && indices.Length == 1
            && !source.Type.CanBeIndeterminate
            && source.Type.DeclaredType is ExpressBoundAggregateType aggregate
            && !aggregate.IsOptional
            && BigInteger.TryParse(
                indices[0].SourceText,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var index))
        {
            var lowerText = aggregate.ResolvedLowerBoundText ?? aggregate.LowerBoundText;
            var upperText = aggregate.ResolvedUpperBoundText ?? aggregate.UpperBoundText;
            if (aggregate.Kind == ExpressAggregateKind.Array
                && BigInteger.TryParse(
                    lowerText,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var lower)
                && BigInteger.TryParse(
                    upperText,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var upper))
            {
                isStaticallyPresent = index >= lower && index <= upper;
            }
            else if (aggregate.Kind == ExpressAggregateKind.List
                && BigInteger.TryParse(
                    lowerText,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var minimumCount))
            {
                isStaticallyPresent = index >= BigInteger.One && index <= minimumCount;
            }
        }

        var resultType = (isSlice ? source.Type : IndexedType(source.Type))
            .WithIndeterminate(!isStaticallyPresent);
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
        var canBeIndeterminate = children.Any(child =>
            (child.Kind == ExpressExpressionKind.Repetition
                && !IsStaticallyValidRepetition(child.Children[1]))
            || (child.Kind == ExpressExpressionKind.Repetition ? child.Children[0] : child)
                .Type.CanBeIndeterminate);
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
        ExpressBoundType? specializedElement = null;
        ExpressBoundName? queryVariable = null;
        var sourceAggregate = source.Type.DeclaredType as ExpressBoundAggregateType;
        if (sourceAggregate is not null
            && predicate.Kind == ExpressExpressionKind.Binary
            && predicate.Operation == "IN"
            && predicate.Children.Count == 2
            && predicate.Children[0].Kind == ExpressExpressionKind.Literal
            && predicate.Children[0].Type.Kind == ExpressExpressionTypeKind.String
            && predicate.Children[1].Kind == ExpressExpressionKind.Application
            && predicate.Children[1].Operation == "TYPEOF"
            && predicate.Children[1].Children.Count == 1
            && predicate.Children[1].Children[0].Reference is
            { Kind: ExpressBoundNameKind.QueryVariable, } candidateVariable
            && string.Equals(candidateVariable.Name, variableName, StringComparison.OrdinalIgnoreCase))
        {
            var sourceText = predicate.Children[0].SourceText;
            var qualifiedName = sourceText.Length >= 2
                ? sourceText.Substring(1, sourceText.Length - 2)
                : "";
            var selected = _declarations.Keys.SingleOrDefault(candidate =>
                candidate.Kind == ExpressDeclarationKind.Entity
                && string.Equals(
                    candidate.DeclaringSchema.Name + "." + candidate.Name,
                    qualifiedName,
                    StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                var selectedEntityClosure = new HashSet<ExpressBoundSymbol>();
                var pendingSelectedEntities = new Queue<ExpressBoundSymbol>();
                pendingSelectedEntities.Enqueue(selected);
                while (pendingSelectedEntities.Count > 0)
                {
                    var entity = pendingSelectedEntities.Dequeue();
                    if (!selectedEntityClosure.Add(entity)
                        || !_declarations.TryGetValue(entity, out var declaration)
                        || declaration is not ExpressBoundEntity boundEntity)
                    {
                        continue;
                    }

                    foreach (var supertype in boundEntity.DirectSupertypes)
                    {
                        pendingSelectedEntities.Enqueue(supertype);
                    }
                }

                var candidateType = sourceAggregate.ElementType;
                while (candidateType is ExpressBoundNamedType candidateNamed
                       && candidateNamed.Declaration.Kind != ExpressDeclarationKind.Entity
                       && _declarations.TryGetValue(candidateNamed.Declaration, out var candidateDeclaration)
                       && candidateDeclaration is ExpressBoundDefinedType candidateDefined)
                {
                    candidateType = candidateDefined.UnderlyingType;
                }

                var isUniqueCompatibleLeaf = false;
                if (candidateType is ExpressBoundNamedType
                    { Declaration.Kind: ExpressDeclarationKind.Entity, } entityCarrier)
                {
                    isUniqueCompatibleLeaf = selectedEntityClosure.Contains(entityCarrier.Declaration);
                }
                else if (candidateType is ExpressBoundGenericType { IsEntity: true, })
                {
                    isUniqueCompatibleLeaf = true;
                }
                else if (candidateType is ExpressBoundSelectType selectCarrier)
                {
                    var pendingTypes = new Stack<ExpressBoundType>();
                    var visitedTypes = new HashSet<ExpressBoundSymbol>();
                    foreach (var alternative in selectCarrier.Alternatives)
                    {
                        pendingTypes.Push(new ExpressBoundNamedType(alternative, selectCarrier.Span));
                    }

                    if (selectCarrier.BaseType is not null)
                    {
                        pendingTypes.Push(new ExpressBoundNamedType(selectCarrier.BaseType, selectCarrier.Span));
                    }

                    var matchingLeaves = 0;
                    while (pendingTypes.Count > 0)
                    {
                        var pendingType = pendingTypes.Pop();
                        if (pendingType is not ExpressBoundNamedType pendingNamed
                            || !visitedTypes.Add(pendingNamed.Declaration))
                        {
                            continue;
                        }

                        if (pendingNamed.Declaration.Kind == ExpressDeclarationKind.Entity
                            && selectedEntityClosure.Contains(pendingNamed.Declaration))
                        {
                            matchingLeaves++;
                            continue;
                        }

                        if (pendingNamed.Declaration.Kind != ExpressDeclarationKind.Entity
                            && _declarations.TryGetValue(pendingNamed.Declaration, out var pendingDeclaration)
                            && pendingDeclaration is ExpressBoundDefinedType pendingDefined)
                        {
                            var underlying = pendingDefined.UnderlyingType;
                            if (underlying is ExpressBoundSelectType pendingSelect)
                            {
                                foreach (var alternative in pendingSelect.Alternatives)
                                {
                                    pendingTypes.Push(new ExpressBoundNamedType(alternative, pendingSelect.Span));
                                }

                                if (pendingSelect.BaseType is not null)
                                {
                                    pendingTypes.Push(new ExpressBoundNamedType(
                                        pendingSelect.BaseType,
                                        pendingSelect.Span));
                                }
                            }
                            else
                            {
                                pendingTypes.Push(underlying);
                            }
                        }
                    }

                    isUniqueCompatibleLeaf = matchingLeaves == 1;
                }

                if (isUniqueCompatibleLeaf)
                {
                    specializedElement = new ExpressBoundNamedType(selected, predicate.Span);
                    queryVariable = candidateVariable;
                }
            }
        }

        var resultType = source.Type;
        if (specializedElement is not null && sourceAggregate is not null)
        {
            resultType = TypeOf(new ExpressBoundAggregateType(
                    sourceAggregate.Kind,
                    specializedElement,
                    sourceAggregate.LowerBoundText,
                    sourceAggregate.UpperBoundText,
                    sourceAggregate.IsOptional,
                    sourceAggregate.IsUnique,
                    sourceAggregate.TypeLabel,
                    sourceAggregate.Span,
                    sourceAggregate.ResolvedLowerBoundText,
                    sourceAggregate.ResolvedUpperBoundText))
                .WithIndeterminate(source.Type.CanBeIndeterminate);
        }

        return Create(
            ExpressExpressionKind.Query,
            resultType,
            syntax,
            variableName,
            queryVariable,
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

    private ExpressExpressionType BinaryResultType(
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

        if (operation == "+"
            && left.Kind == ExpressExpressionTypeKind.String
            && right.Kind == ExpressExpressionTypeKind.String)
        {
            return _string;
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

        var leftEntityAggregate = left.DeclaredType as ExpressBoundAggregateType;
        var rightEntityAggregate = right.DeclaredType as ExpressBoundAggregateType;
        if (operation == "+"
            && (leftEntityAggregate is not null || rightEntityAggregate is not null))
        {
            ExpressBoundType? commonElement = null;
            var sourceAggregate = leftEntityAggregate ?? rightEntityAggregate!;
            var leftElement = leftEntityAggregate?.ElementType ?? left.DeclaredType;
            var rightElement = rightEntityAggregate?.ElementType ?? right.DeclaredType;
            var leftIsGenericEntity = leftElement is ExpressBoundGenericType { IsEntity: true, };
            var rightIsGenericEntity = rightElement is ExpressBoundGenericType { IsEntity: true, };
            var leftEntity = leftElement as ExpressBoundNamedType;
            var rightEntity = rightElement as ExpressBoundNamedType;
            if ((leftIsGenericEntity || leftEntity?.Declaration.Kind == ExpressDeclarationKind.Entity)
                && (rightIsGenericEntity || rightEntity?.Declaration.Kind == ExpressDeclarationKind.Entity))
            {
                if (leftIsGenericEntity || rightIsGenericEntity)
                {
                    commonElement = new ExpressBoundGenericType(
                        isEntity: true,
                        typeLabel: null,
                        sourceAggregate.Span);
                }
                else
                {
                    var leftClosure = new HashSet<ExpressBoundSymbol>();
                    var pending = new Stack<ExpressBoundSymbol>();
                    pending.Push(leftEntity!.Declaration);
                    while (pending.Count > 0)
                    {
                        var candidate = pending.Pop();
                        if (!leftClosure.Add(candidate)
                            || !_declarations.TryGetValue(candidate, out var declaration)
                            || declaration is not ExpressBoundEntity entity)
                        {
                            continue;
                        }

                        foreach (var supertype in entity.DirectSupertypes)
                        {
                            pending.Push(supertype);
                        }
                    }

                    var rightClosure = new HashSet<ExpressBoundSymbol>();
                    pending.Push(rightEntity!.Declaration);
                    while (pending.Count > 0)
                    {
                        var candidate = pending.Pop();
                        if (!rightClosure.Add(candidate)
                            || !_declarations.TryGetValue(candidate, out var declaration)
                            || declaration is not ExpressBoundEntity entity)
                        {
                            continue;
                        }

                        foreach (var supertype in entity.DirectSupertypes)
                        {
                            pending.Push(supertype);
                        }
                    }

                    var common = leftClosure.Intersect(rightClosure).ToArray();
                    var mostSpecific = common.Where(candidate => !common.Any(other =>
                    {
                        if (ReferenceEquals(candidate, other))
                        {
                            return false;
                        }

                        var visited = new HashSet<ExpressBoundSymbol>();
                        var ancestors = new Stack<ExpressBoundSymbol>();
                        ancestors.Push(other);
                        while (ancestors.Count > 0)
                        {
                            var ancestor = ancestors.Pop();
                            if (!visited.Add(ancestor))
                            {
                                continue;
                            }

                            if (ReferenceEquals(ancestor, candidate))
                            {
                                return true;
                            }

                            if (_declarations.TryGetValue(ancestor, out var declaration)
                                && declaration is ExpressBoundEntity entity)
                            {
                                foreach (var supertype in entity.DirectSupertypes)
                                {
                                    ancestors.Push(supertype);
                                }
                            }
                        }

                        return false;
                    })).ToArray();
                    commonElement = mostSpecific.Length switch
                    {
                        0 => new ExpressBoundGenericType(
                            isEntity: true,
                            typeLabel: null,
                            sourceAggregate.Span),
                        1 => new ExpressBoundNamedType(mostSpecific[0], sourceAggregate.Span),
                        _ => null,
                    };
                }
            }

            if (commonElement is not null)
            {
                return AggregateOperationType(sourceAggregate, sourceAggregate.Kind, commonElement);
            }
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
        ExpressAggregateKind kind,
        ExpressBoundType? elementType = null)
    {
        return new(
            ExpressExpressionTypeKind.Aggregate,
            new ExpressBoundAggregateType(
                kind,
                elementType ?? source.ElementType,
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
        if (string.Equals(operation, "NVL", StringComparison.OrdinalIgnoreCase)
            && parameters.Length > 1)
        {
            var left = parameters[0].Type;
            var right = parameters[1].Type;
            var leftIsNumeric = left.Kind is ExpressExpressionTypeKind.Integer
                or ExpressExpressionTypeKind.Real
                or ExpressExpressionTypeKind.Number;
            var rightIsNumeric = right.Kind is ExpressExpressionTypeKind.Integer
                or ExpressExpressionTypeKind.Real
                or ExpressExpressionTypeKind.Number;
            if (leftIsNumeric && rightIsNumeric)
            {
                if (left.DeclaredType is ExpressBoundNamedType leftNamed
                    && right.DeclaredType is ExpressBoundNamedType rightNamed
                    && !ReferenceEquals(leftNamed.Declaration, rightNamed.Declaration))
                {
                    return _unresolved;
                }

                return NumericResultType(left, right).WithIndeterminate(
                    left.CanBeIndeterminate && right.CanBeIndeterminate);
            }

            if (leftIsNumeric || rightIsNumeric)
            {
                return _unresolved;
            }
        }

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