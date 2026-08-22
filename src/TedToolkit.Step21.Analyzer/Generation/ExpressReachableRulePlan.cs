// -----------------------------------------------------------------------
// <copyright file="ExpressReachableRulePlan.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

using TedToolkit.Step21.Analyzer.Express;
using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Computes the private executable dependency closure rooted at generated validation rules.
/// </summary>
internal sealed class ExpressReachableRulePlan
{
    private readonly IReadOnlyDictionary<ExpressBoundSymbol, ExpressBoundDeclaration> _declarations;

    private ExpressReachableRulePlan(
        ExpressBoundSchema schema,
        IReadOnlyDictionary<ExpressBoundSymbol, ExpressBoundDeclaration> declarations,
        IEnumerable<ExpressBoundDeclaration> reachableDeclarations,
        IEnumerable<ExpressBoundAttribute> reachableDerivedAttributes,
        IEnumerable<ExpressRuleSyntax> reachableRules,
        IEnumerable<ExpressEntityGenerationFailure> failures)
    {
        Schema = schema;
        _declarations = declarations;
        ReachableDeclarations = new ReadOnlyCollection<ExpressBoundDeclaration>(
            reachableDeclarations.ToArray());
        ReachableDerivedAttributes = new ReadOnlyCollection<ExpressBoundAttribute>(
            reachableDerivedAttributes.ToArray());
        ReachableRules = new ReadOnlyCollection<ExpressRuleSyntax>(reachableRules.ToArray());
        Failures = new ReadOnlyCollection<ExpressEntityGenerationFailure>(failures.ToArray());
    }

    /// <summary>
    /// Gets the schema whose private rule closure is represented.
    /// </summary>
    internal ExpressBoundSchema Schema { get; }

    /// <summary>
    /// Gets reachable constant and function declarations in deterministic source order.
    /// </summary>
    internal IReadOnlyList<ExpressBoundDeclaration> ReachableDeclarations { get; }

    /// <summary>
    /// Gets reachable derived attributes in deterministic source order.
    /// </summary>
    internal IReadOnlyList<ExpressBoundAttribute> ReachableDerivedAttributes { get; }

    /// <summary>
    /// Gets the validation-rooted WHERE expressions in deterministic source order.
    /// </summary>
    internal IReadOnlyList<ExpressRuleSyntax> ReachableRules { get; }

    /// <summary>
    /// Gets source-located closure failures that make schema generation unsafe.
    /// </summary>
    internal IReadOnlyList<ExpressEntityGenerationFailure> Failures { get; }

    /// <summary>
    /// Creates the complete validation-rooted dependency plan for one schema.
    /// </summary>
    /// <param name="schema">The bound schema.</param>
    /// <param name="resolver">The generated value-type resolver.</param>
    /// <returns>The immutable rule plan.</returns>
    internal static ExpressReachableRulePlan Create(
        ExpressBoundSchema schema,
        ExpressGeneratedTypeResolver resolver)
    {
        var declarations = schema.Declarations.Concat(schema.NestedDeclarations)
            .ToDictionary(declaration => declaration.Symbol);
        var builder = new Builder(schema, declarations, resolver);
        return builder.Create();
    }

    /// <summary>
    /// Gets the declaration selected by one reachable schema name.
    /// </summary>
    /// <param name="symbol">The declaration identity.</param>
    /// <returns>The bound declaration.</returns>
    internal ExpressBoundDeclaration GetDeclaration(ExpressBoundSymbol symbol)
    {
        return _declarations[symbol];
    }

    /// <summary>
    /// Gets the bound expression corresponding exactly to retained expression syntax.
    /// </summary>
    /// <param name="syntax">The retained expression syntax.</param>
    /// <returns>The typed expression.</returns>
    internal ExpressBoundExpression GetExpression(ExpressRuleSyntax syntax)
    {
        return Schema.Expressions.Single(expression => SameSpan(expression.Span, syntax.Span));
    }

    /// <summary>
    /// Finds the entity that declares an exact attribute object.
    /// </summary>
    /// <param name="attribute">The bound attribute.</param>
    /// <returns>The declaring entity.</returns>
    internal ExpressBoundEntity GetAttributeOwner(ExpressBoundAttribute attribute)
    {
        return Schema.Declarations.OfType<ExpressBoundEntity>()
            .Concat(Schema.NestedDeclarations.OfType<ExpressBoundEntity>())
            .Single(entity => entity.Attributes.Contains(attribute));
    }

    /// <summary>
    /// Gets the initializer expression for a reachable derived attribute.
    /// </summary>
    /// <param name="attribute">The derived attribute.</param>
    /// <returns>The typed initializer expression.</returns>
    internal ExpressBoundExpression GetDerivedExpression(ExpressBoundAttribute attribute)
    {
        var owner = GetAttributeOwner(attribute);
        var syntax = owner.Syntax.DescendantsAndSelf()
            .Where(candidate => candidate.Production == "derivedAttr")
            .Single(candidate => SameStart(
                candidate.RequiredChild("attributeDecl").Span,
                attribute.Span));
        return GetExpression(syntax.RequiredChild("expression"));
    }

    /// <summary>
    /// Gets the exact attribute selected by one UNIQUE referenced-attribute syntax node.
    /// </summary>
    /// <param name="syntax">The referenced attribute syntax.</param>
    /// <returns>The bound attribute.</returns>
    internal ExpressBoundAttribute GetReferencedAttribute(ExpressRuleSyntax syntax)
    {
        return Schema.NameReferences
            .Where(reference => reference.Target.Attribute is not null
                && Contains(syntax.Span, reference.Span))
            .Select(reference => reference.Target.Attribute!)
            .Distinct()
            .Single();
    }

    private static bool Contains(ExpressSourceSpan outer, ExpressSourceSpan inner)
    {
        return string.Equals(outer.Start.FilePath, inner.Start.FilePath, StringComparison.Ordinal)
            && Compare(outer.Start, inner.Start) <= 0
            && Compare(outer.End, inner.End) >= 0;
    }

    private static int Compare(ExpressSourceLocation left, ExpressSourceLocation right)
    {
        var line = left.Line.CompareTo(right.Line);
        return line != 0 ? line : left.Column.CompareTo(right.Column);
    }

    private static bool SameStart(ExpressSourceSpan left, ExpressSourceSpan right)
    {
        return string.Equals(left.Start.FilePath, right.Start.FilePath, StringComparison.Ordinal)
            && left.Start.Line == right.Start.Line
            && left.Start.Column == right.Start.Column;
    }

    private static bool SameSpan(ExpressSourceSpan left, ExpressSourceSpan right)
    {
        return SameStart(left, right)
            && left.End.Line == right.End.Line
            && left.End.Column == right.End.Column;
    }

    private sealed class Builder
    {
        private readonly ExpressBoundSchema _schema;

        private readonly IReadOnlyDictionary<ExpressBoundSymbol, ExpressBoundDeclaration> _declarations;

        private readonly ExpressGeneratedTypeResolver _resolver;

        private readonly HashSet<ExpressBoundDeclaration> _reachableDeclarations = [];

        private readonly HashSet<ExpressBoundAttribute> _reachableDerivedAttributes = [];

        private readonly HashSet<ExpressRuleSyntax> _reachableRules = [];

        private readonly Dictionary<object, VisitState> _states = [];

        private readonly List<ExpressEntityGenerationFailure> _failures = [];

        internal Builder(
            ExpressBoundSchema schema,
            IReadOnlyDictionary<ExpressBoundSymbol, ExpressBoundDeclaration> declarations,
            ExpressGeneratedTypeResolver resolver)
        {
            _schema = schema;
            _declarations = declarations;
            _resolver = resolver;
        }

        internal ExpressReachableRulePlan Create()
        {
            var checkedAggregates = new HashSet<ExpressBoundAggregateType>();
            foreach (var attribute in _schema.Declarations.OfType<ExpressBoundEntity>()
                         .SelectMany(entity => entity.Attributes)
                         .Where(attribute => attribute.Kind == ExpressAttributeKind.Explicit))
            {
                ValidateAggregateBounds(
                    attribute.Type,
                    checkedAggregates,
                    new HashSet<ExpressBoundSymbol>());
                VisitTypeRules(attribute.Type, new HashSet<ExpressBoundSymbol>());
            }

            foreach (var declaration in _schema.Declarations.Where(declaration =>
                         declaration.Kind is ExpressDeclarationKind.Entity
                             or ExpressDeclarationKind.Rule))
            {
                if (declaration is ExpressBoundEntity entity)
                {
                    VisitUniqueDependencies(entity);
                }

                if (declaration.Kind == ExpressDeclarationKind.Rule)
                {
                    ValidateRuleShape(declaration);
                }

                foreach (var rule in declaration.Syntax.DescendantsAndSelf()
                             .Where(candidate => candidate.Production == "domainRule"))
                {
                    VisitRule(rule);
                }
            }

            var orderedDeclarations = _schema.Declarations.Concat(_schema.NestedDeclarations)
                .Where(_reachableDeclarations.Contains);
            var orderedAttributes = _schema.Declarations.Concat(_schema.NestedDeclarations)
                .OfType<ExpressBoundEntity>()
                .SelectMany(entity => entity.Attributes)
                .Where(_reachableDerivedAttributes.Contains);
            var orderedRules = _schema.Declarations.Concat(_schema.NestedDeclarations)
                .SelectMany(declaration => declaration.Syntax.DescendantsAndSelf())
                .Where(candidate => candidate.Production == "domainRule" && _reachableRules.Contains(candidate));
            return new(
                _schema,
                _declarations,
                orderedDeclarations,
                orderedAttributes,
                orderedRules,
                _failures);
        }

        private void VisitTypeRules(
            ExpressBoundType type,
            ISet<ExpressBoundSymbol> visitedTypes)
        {
            if (type is ExpressBoundAggregateType aggregate)
            {
                VisitTypeRules(aggregate.ElementType, visitedTypes);
                return;
            }

            if (type is not ExpressBoundNamedType named
                || named.Declaration.Kind == ExpressDeclarationKind.Entity
                || !visitedTypes.Add(named.Declaration))
            {
                return;
            }

            if (!_declarations.TryGetValue(named.Declaration, out var declaration)
                || declaration is not ExpressBoundDefinedType defined)
            {
                var imported = _resolver.GetDefinedType(named.Declaration);
                if (imported.Syntax.ChildRules("whereClause").Any())
                {
                    AddFailure(
                        named.Span,
                        $"Validation-reachable cross-schema type rule '{imported.Name}' requires "
                        + "the multi-schema execution boundary.");
                }

                return;
            }

            foreach (var rule in defined.Syntax.ChildRules("whereClause")
                         .SelectMany(clause => clause.ChildRules("domainRule")))
            {
                VisitRule(rule);
            }

            switch (defined.UnderlyingType)
            {
                case ExpressBoundSelectType select:
                    foreach (var alternative in _resolver.GetSelectAlternatives(select))
                    {
                        VisitTypeRules(new ExpressBoundNamedType(alternative, named.Span), visitedTypes);
                    }

                    break;

                case not ExpressBoundEnumerationType:
                    VisitTypeRules(defined.UnderlyingType, visitedTypes);
                    break;
            }
        }

        private void VisitRule(ExpressRuleSyntax rule)
        {
            _reachableRules.Add(rule);
            VisitExpression(GetExpression(rule.RequiredChild("expression")), root: null);
        }

        private void VisitUniqueDependencies(ExpressBoundEntity entity)
        {
            var uniqueClause = entity.Syntax.RequiredChild("entityBody")
                .ChildRules("uniqueClause")
                .SingleOrDefault();
            if (uniqueClause is null)
            {
                return;
            }

            foreach (var referencedAttribute in uniqueClause.DescendantsAndSelf()
                         .Where(candidate => candidate.Production == "referencedAttribute"))
            {
                var attribute = _schema.NameReferences
                    .Where(reference => reference.Target.Attribute is not null
                        && Contains(referencedAttribute.Span, reference.Span))
                    .Select(reference => reference.Target.Attribute!)
                    .Distinct()
                    .Single();
                if (attribute.Kind == ExpressAttributeKind.Derived)
                {
                    VisitDerived(attribute, parent: null);
                }
            }
        }

        private void ValidateAggregateBounds(
            ExpressBoundType type,
            ISet<ExpressBoundAggregateType> checkedAggregates,
            ISet<ExpressBoundSymbol> visitedTypes)
        {
            if (type is ExpressBoundAggregateType aggregate)
            {
                if (checkedAggregates.Add(aggregate))
                {
                    ValidateAggregateBound(
                        aggregate,
                        aggregate.LowerBoundText,
                        aggregate.ResolvedLowerBoundText ?? aggregate.LowerBoundText,
                        "lower");
                    ValidateAggregateBound(
                        aggregate,
                        aggregate.UpperBoundText,
                        aggregate.ResolvedUpperBoundText ?? aggregate.UpperBoundText,
                        "upper");
                }

                ValidateAggregateBounds(aggregate.ElementType, checkedAggregates, visitedTypes);
                return;
            }

            if (type is not ExpressBoundNamedType named
                || named.Declaration.Kind == ExpressDeclarationKind.Entity
                || !visitedTypes.Add(named.Declaration)
                || !_declarations.TryGetValue(named.Declaration, out var declaration)
                || declaration is not ExpressBoundDefinedType defined)
            {
                return;
            }

            ValidateAggregateBounds(defined.UnderlyingType, checkedAggregates, visitedTypes);
        }

        private void ValidateAggregateBound(
            ExpressBoundAggregateType aggregate,
            string? sourceText,
            string? resolvedText,
            string boundary)
        {
            if (resolvedText is null
                || resolvedText == "?"
                || int.TryParse(
                    resolvedText,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out _))
            {
                return;
            }

            _failures.Add(new(
                _schema,
                aggregate.Span.Start,
                $"Validation-reachable {boundary} aggregate bound '{sourceText}' cannot be statically represented."));
        }

        private void VisitExpression(ExpressBoundExpression expression, object? root)
        {
            foreach (var node in expression.DescendantsAndSelf())
            {
                var reference = node.Reference;
                if (reference?.Attribute is { Kind: ExpressAttributeKind.Inverse, } inverse)
                {
                    AddFailure(
                        inverse.Span,
                        $"Validation-reachable inverse attribute '{inverse.Name}' requires population navigation that cannot be skipped.");
                }

                if (node.Kind == ExpressExpressionKind.Application
                    && node.Operation is "TYPEOF" or "ROLESOF" or "USEDIN")
                {
                    AddFailure(
                        node.Span,
                        $"Validation-reachable model function '{node.Operation}' requires a generated model traversal callback.");
                }

                if (node.Kind == ExpressExpressionKind.Binary
                    && node.Operation == "||"
                    && node.Children.All(child => child.Type.Kind == ExpressExpressionTypeKind.Entity))
                {
                    AddFailure(
                        node.Span,
                        "Validation-reachable complex entity construction belongs to complex mapping generation.");
                }

                if (reference?.Attribute is { Kind: ExpressAttributeKind.Derived, } attribute)
                {
                    VisitDerived(attribute, root);
                }

                if (reference?.SchemaDeclaration is { } symbol
                    && symbol.Kind is ExpressDeclarationKind.Constant or ExpressDeclarationKind.Function
                    && _declarations.TryGetValue(symbol, out var declaration))
                {
                    VisitDeclaration(declaration, root);
                }
                else if (reference?.SchemaDeclaration is { } importedSymbol
                         && importedSymbol.Kind is ExpressDeclarationKind.Constant or ExpressDeclarationKind.Function)
                {
                    AddFailure(
                        node.Span,
                        $"Validation-reachable cross-schema dependency '{reference.Name}' requires "
                        + "the multi-schema execution boundary.");
                }
            }
        }

        private void VisitDerived(ExpressBoundAttribute attribute, object? parent)
        {
            var owner = _schema.Declarations.OfType<ExpressBoundEntity>()
                .Concat(_schema.NestedDeclarations.OfType<ExpressBoundEntity>())
                .SingleOrDefault(entity => entity.Attributes.Contains(attribute));
            if (owner is null)
            {
                AddFailure(
                    attribute.Span,
                    $"Validation-reachable cross-schema derived attribute '{attribute.Name}' requires "
                    + "the multi-schema execution boundary.");
                return;
            }

            if (!Begin(attribute, parent))
            {
                return;
            }

            _reachableDerivedAttributes.Add(attribute);
            var syntax = owner.Syntax.DescendantsAndSelf()
                .Where(candidate => candidate.Production == "derivedAttr")
                .Single(candidate => SameStart(
                    candidate.RequiredChild("attributeDecl").Span,
                    attribute.Span));
            var expression = GetExpression(syntax.RequiredChild("expression"));
            ValidateDependencyResult(attribute.Name, attribute.Type, expression);
            VisitExpression(expression, attribute);
            End(attribute);
        }

        private void VisitDeclaration(ExpressBoundDeclaration declaration, object? parent)
        {
            if (!Begin(declaration, parent))
            {
                return;
            }

            _reachableDeclarations.Add(declaration);
            if (declaration is ExpressBoundOpaqueDeclaration { DeclaredType: { } declaredType, } opaque)
            {
                if (!_resolver.IsSupported(declaredType))
                {
                    AddFailure(
                        declaration.Syntax.Span,
                        $"Reachable EXPRESS {declaration.Kind.ToString()} '{declaration.Name}' has no generated result type.");
                }

                foreach (var expression in DeclarationExpressions(declaration))
                {
                    if (declaration.Kind == ExpressDeclarationKind.Constant
                        || IsFunctionReturnExpression(opaque, expression))
                    {
                        ValidateDependencyResult(declaration.Name, declaredType, expression);
                    }
                }
            }

            if (declaration.Kind == ExpressDeclarationKind.Function)
            {
                ValidateFunctionShape(declaration);
            }

            foreach (var expression in DeclarationExpressions(declaration))
            {
                VisitExpression(expression, declaration);
            }

            End(declaration);
        }

        private bool Begin(object dependency, object? parent)
        {
            if (!_states.TryGetValue(dependency, out var state))
            {
                _states[dependency] = VisitState.Visiting;
                return true;
            }

            if (state != VisitState.Visiting)
            {
                return false;
            }

            var span = dependency switch
            {
                ExpressBoundDeclaration declaration => declaration.Syntax.Span,
                ExpressBoundAttribute attribute => attribute.Span,
                _ => _schema.Identity.Span,
            };
            _failures.Add(new(
                _schema,
                span.Start,
                $"Reachable EXPRESS dependency cycle includes '{DependencyName(dependency)}'"
                + (parent is null ? "." : $" from '{DependencyName(parent)}'.")));

            return false;
        }

        private void End(object dependency)
        {
            _states[dependency] = VisitState.Complete;
        }

        private IEnumerable<ExpressBoundExpression> DeclarationExpressions(
            ExpressBoundDeclaration declaration)
        {
            return declaration.Syntax.DescendantsAndSelf()
                .Where(candidate => candidate.Production == "expression")
                .Select(GetExpression);
        }

        private ExpressBoundExpression GetExpression(ExpressRuleSyntax syntax)
        {
            return _schema.Expressions.Single(expression => SameSpan(expression.Span, syntax.Span));
        }

        private void ValidateFunctionShape(ExpressBoundDeclaration declaration)
        {
            var statements = declaration.Syntax.ChildRules("stmt").ToArray();
            if (statements.Length == 1
                && statements[0].ChildRules("returnStmt").SingleOrDefault() is
                { } returnStatement
                && returnStatement.ChildRules("expression").Count() == 1
                && !declaration.Syntax.RequiredChild("algorithmHead")
                    .ChildRules()
                    .Any(child => child.Production is "constantDecl" or "localDecl"))
            {
                return;
            }

            _failures.Add(new(
                _schema,
                declaration.Syntax.Span.Start,
                $"Reachable EXPRESS function '{declaration.Name}' must currently contain exactly one value RETURN statement."));
        }

        private void ValidateDependencyResult(
            string name,
            ExpressBoundType declaredType,
            ExpressBoundExpression expression)
        {
            if (!_resolver.IsSupported(declaredType) || !expression.Type.CanBeIndeterminate)
            {
                return;
            }

            AddFailure(
                expression.Span,
                $"Reachable EXPRESS dependency '{name}' can evaluate to the indeterminate value but its generated result is mandatory.");
        }

        private static bool IsFunctionReturnExpression(
            ExpressBoundOpaqueDeclaration declaration,
            ExpressBoundExpression expression)
        {
            return declaration.Syntax.DescendantsAndSelf()
                .Where(candidate => candidate.Production == "returnStmt")
                .SelectMany(candidate => candidate.ChildRules("expression"))
                .Any(candidate => SameSpan(candidate.Span, expression.Span));
        }

        private void AddFailure(ExpressSourceSpan span, string message)
        {
            if (_failures.Any(failure => SameLocation(failure.Location, span.Start)
                    && string.Equals(failure.Message, message, StringComparison.Ordinal)))
            {
                return;
            }

            _failures.Add(new(_schema, span.Start, message));
        }

        private static bool SameLocation(
            ExpressSourceLocation left,
            ExpressSourceLocation right)
        {
            return string.Equals(left.FilePath, right.FilePath, StringComparison.Ordinal)
                && left.Line == right.Line
                && left.Column == right.Column;
        }

        private void ValidateRuleShape(ExpressBoundDeclaration declaration)
        {
            if (!declaration.Syntax.RequiredChild("algorithmHead").ChildRules().Any()
                && !declaration.Syntax.ChildRules("stmt").Any())
            {
                return;
            }

            _failures.Add(new(
                _schema,
                declaration.Syntax.Span.Start,
                $"Validation-root EXPRESS RULE '{declaration.Name}' contains algorithm statements that cannot be skipped."));
        }

        private static string DependencyName(object dependency)
        {
            return dependency switch
            {
                ExpressBoundDeclaration declaration => declaration.Name,
                ExpressBoundAttribute attribute => attribute.Name,
                _ => dependency.GetType().Name,
            };
        }

        private enum VisitState
        {
            Visiting = 0,

            Complete = 1,
        }
    }
}