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
        ExpressGeneratedTypeResolver resolver,
        IReadOnlyDictionary<ExpressBoundSymbol, ExpressBoundDeclaration> declarations,
        IReadOnlyList<ExpressEntityProjection> entityProjections,
        IEnumerable<ExpressBoundDeclaration> reachableDeclarations,
        IEnumerable<ExpressBoundAttribute> reachableDerivedAttributes,
        IEnumerable<ExpressRuleSyntax> reachableRules,
        bool requiresEntityValueEquality,
        IEnumerable<ExpressEntityGenerationFailure> failures)
    {
        Schema = schema;
        Resolver = resolver;
        _declarations = declarations;
        EntityProjections = entityProjections;
        ReachableDeclarations = new ReadOnlyCollection<ExpressBoundDeclaration>(
            reachableDeclarations.ToArray());
        ReachableDerivedAttributes = new ReadOnlyCollection<ExpressBoundAttribute>(
            reachableDerivedAttributes.ToArray());
        ReachableRules = new ReadOnlyCollection<ExpressRuleSyntax>(reachableRules.ToArray());
        RequiresEntityValueEquality = requiresEntityValueEquality;
        Failures = new ReadOnlyCollection<ExpressEntityGenerationFailure>(failures.ToArray());
    }

    /// <summary>
    /// Gets the schema whose private rule closure is represented.
    /// </summary>
    internal ExpressBoundSchema Schema { get; }

    /// <summary>
    /// Gets the generated value-type resolver retained by this plan.
    /// </summary>
    internal ExpressGeneratedTypeResolver Resolver { get; }

    /// <summary>
    /// Gets the generated entity storage projections available to private rule lowering.
    /// </summary>
    internal IReadOnlyList<ExpressEntityProjection> EntityProjections { get; }

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
    /// Gets a value indicating whether the reachable closure requires generated entity value equality.
    /// </summary>
    internal bool RequiresEntityValueEquality { get; }

    /// <summary>
    /// Gets source-located closure failures that make schema generation unsafe.
    /// </summary>
    internal IReadOnlyList<ExpressEntityGenerationFailure> Failures { get; }

    /// <summary>
    /// Creates the complete validation-rooted dependency plan for one schema.
    /// </summary>
    /// <param name="schema">The bound schema.</param>
    /// <param name="resolver">The generated value-type resolver.</param>
    /// <param name="entityProjections">The generated entity storage projections.</param>
    /// <returns>The immutable rule plan.</returns>
    internal static ExpressReachableRulePlan Create(
        ExpressBoundSchema schema,
        ExpressGeneratedTypeResolver resolver,
        IReadOnlyList<ExpressEntityProjection> entityProjections)
    {
        var declarations = schema.Declarations.Concat(schema.NestedDeclarations)
            .ToDictionary(declaration => declaration.Symbol);
        var builder = new Builder(schema, declarations, resolver, entityProjections);
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

        private readonly IReadOnlyList<ExpressEntityProjection> _entityProjections;

        private readonly HashSet<ExpressBoundDeclaration> _reachableDeclarations = [];

        private readonly HashSet<ExpressBoundAttribute> _reachableDerivedAttributes = [];

        private readonly HashSet<ExpressRuleSyntax> _reachableRules = [];

        private readonly Dictionary<object, VisitState> _states = [];

        private readonly List<object> _dependencyPath = [];

        private readonly List<ExpressEntityGenerationFailure> _failures = [];

        private bool _requiresEntityValueEquality;

        internal Builder(
            ExpressBoundSchema schema,
            IReadOnlyDictionary<ExpressBoundSymbol, ExpressBoundDeclaration> declarations,
            ExpressGeneratedTypeResolver resolver,
            IReadOnlyList<ExpressEntityProjection> entityProjections)
        {
            _schema = schema;
            _declarations = declarations;
            _resolver = resolver;
            _entityProjections = entityProjections;
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
                _resolver,
                _declarations,
                _entityProjections,
                orderedDeclarations,
                orderedAttributes,
                orderedRules,
                _requiresEntityValueEquality,
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
            var nodes = expression.DescendantsAndSelf().ToArray();
            foreach (var node in nodes)
            {
                var reference = node.Reference;
                foreach (var inverse in reference?.AttributeCandidates
                             .Where(candidate => candidate.Kind == ExpressAttributeKind.Inverse)
                         ?? [])
                {
                    if (inverse.Type is not ExpressBoundAggregateType)
                    {
                        AddFailure(
                            inverse.Span,
                            $"Validation-reachable singular inverse attribute '{inverse.Name}' cannot represent a missing or multiply populated forward role.");
                    }
                }

                if (node.Kind == ExpressExpressionKind.Binary
                    && node.Operation == "||"
                    && node.Children.All(child => child.Type.Kind == ExpressExpressionTypeKind.Entity)
                    && !nodes.Any(candidate => !ReferenceEquals(candidate, node)
                        && candidate.Kind == ExpressExpressionKind.Binary
                        && candidate.Operation == "||"
                        && candidate.DescendantsAndSelf().Skip(1).Any(descendant => ReferenceEquals(descendant, node))))
                {
                    var components = new List<ExpressBoundExpression>() { node, };
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

                    var target = node.Type.DeclaredType is ExpressBoundNamedType targetType
                        ? _entityProjections.SingleOrDefault(projection => ReferenceEquals(
                            projection.Entity.Symbol,
                            targetType.Declaration))
                        : null;
                    var suppliedSlots = new List<(ExpressBoundSymbol Entity, string Attribute)>();
                    var incompatible = target is not
                    {
                        Entity.IsAbstract: false,
                        HasDerivedRedeclaration: false,
                    };
                    foreach (var component in components)
                    {
                        ExpressEntityProjection? projection;
                        if (component.Kind == ExpressExpressionKind.Application
                            && component.Reference is
                            {
                                Kind: ExpressBoundNameKind.Entity,
                                SchemaDeclaration: { } componentSymbol,
                            })
                        {
                            projection = _entityProjections.SingleOrDefault(candidate => ReferenceEquals(
                                candidate.Entity.Symbol,
                                componentSymbol));
                            incompatible |= projection is null
                                || target?.PhysicalComponents.Any(entity => ReferenceEquals(
                                    entity.Symbol,
                                    componentSymbol)) != true
                                || projection.OwnAttributes.Count != component.Children.Count;
                            if (projection is not null)
                            {
                                suppliedSlots.AddRange(projection.OwnAttributes.Select(attribute =>
                                    (attribute.StorageEntity.Symbol, attribute.StorageAttributeName)));
                            }

                            continue;
                        }

                        projection = component.Type.DeclaredType is ExpressBoundNamedType sourceType
                            ? _entityProjections.SingleOrDefault(candidate => ReferenceEquals(
                                candidate.Entity.Symbol,
                                sourceType.Declaration))
                            : null;
                        incompatible |= projection is null
                            || target?.PhysicalComponents.Any(entity => ReferenceEquals(
                                entity.Symbol,
                                projection?.Entity.Symbol)) != true;
                        if (projection is not null)
                        {
                            suppliedSlots.AddRange(projection.EffectiveAttributes.Select(attribute =>
                                (attribute.StorageEntity.Symbol, attribute.StorageAttributeName)));
                        }
                    }

                    var duplicate = suppliedSlots.GroupBy(slot => slot).Any(group => group.Count() != 1);
                    var missing = target?.EffectiveAttributes.Any(attribute =>
                        !attribute.Attribute.IsOptional
                        && !suppliedSlots.Contains((
                            attribute.StorageEntity.Symbol,
                            attribute.StorageAttributeName))) != false;
                    if (incompatible || duplicate || missing)
                    {
                        var reason = "missing mandatory physical storage slots";
                        if (incompatible)
                        {
                            reason = "an incompatible component or result type";
                        }
                        else if (duplicate)
                        {
                            reason = "duplicate physical storage slots";
                        }

                        AddFailure(
                            node.Span,
                            $"Validation-reachable complex entity construction has {reason}.");
                    }
                }

                if (node.Kind == ExpressExpressionKind.Binary
                    && ((node.Operation is "=" or "<>"
                            && node.Children.Any(child =>
                                ExpressExpressionEmitter.RequiresSchemaValueEquality(child.Type)))
                        || (node.Operation == "IN"
                            && (node.Children[0].Type.Kind is ExpressExpressionTypeKind.Entity
                                or ExpressExpressionTypeKind.Select)
                            && node.Children[1].Type.DeclaredType is ExpressBoundAggregateType membershipAggregate
                            && (membershipAggregate.ElementType is ExpressBoundSelectType
                                || (membershipAggregate.ElementType is ExpressBoundNamedType namedElement
                                    && namedElement.Declaration.Kind != ExpressDeclarationKind.Entity
                                    && _resolver.GetDefinedType(namedElement.Declaration).UnderlyingType
                                        is ExpressBoundSelectType)))))
                {
                    _requiresEntityValueEquality = true;
                }

                foreach (var attribute in reference?.AttributeCandidates
                             .Where(candidate => candidate.Kind == ExpressAttributeKind.Derived)
                         ?? [])
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
                var functionHead = declaration.Kind == ExpressDeclarationKind.Function
                    ? declaration.Syntax.RequiredChild("functionHead")
                    : null;
                var formalTypes = functionHead?.ChildRules("formalParameter")
                    .SelectMany(formal => formal.ChildRules("parameterId"))
                    .Select(parameter => _schema.NameReferences.Select(reference => reference.Target)
                        .Where(candidate => candidate.Kind == ExpressBoundNameKind.Parameter
                            && SameStart(candidate.Span, parameter.Span))
                        .Distinct()
                        .SingleOrDefault()?.Type)
                    .OfType<ExpressBoundType>()
                    .ToArray() ?? Array.Empty<ExpressBoundType>();
                var localTypes = declaration.Kind == ExpressDeclarationKind.Function
                    ? declaration.Syntax.RequiredChild("algorithmHead")
                        .ChildRules("localDecl")
                        .SelectMany(local => local.ChildRules("localVariable"))
                        .SelectMany(local => local.ChildRules("variableId"))
                        .Select(variable => _schema.NameReferences.Select(reference => reference.Target)
                            .Where(candidate => candidate.Kind == ExpressBoundNameKind.Variable
                                && SameStart(candidate.Span, variable.Span))
                            .Distinct()
                            .SingleOrDefault()?.Type)
                        .OfType<ExpressBoundType>()
                        .ToArray()
                    : [];
                var genericCalls = _schema.Expressions
                    .SelectMany(expression => expression.DescendantsAndSelf())
                    .Where(expression => ReferenceEquals(
                        expression.Reference?.SchemaDeclaration,
                        declaration.Symbol))
                    .ToArray();
                var genericNodes = declaration.Kind == ExpressDeclarationKind.Function
                    ? declaration.Syntax.DescendantsAndSelf()
                        .Where(node => node.Production is "genericType" or "genericEntityType")
                        .ToArray()
                    : [];
                var genericLabels = genericNodes
                    .SelectMany(node => node.ChildRules("typeLabel"))
                    .Select(node => node.IdentifierToken().Text)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var hasUnlabeledGeneric = genericNodes.Any(node => !node.ChildRules("typeLabel").Any());
                var hasConflictingGeneric = genericNodes
                    .Where(node => node.ChildRules("typeLabel").SingleOrDefault() is not null)
                    .GroupBy(
                        node => node.ChildRules("typeLabel").Single().IdentifierToken().Text,
                        StringComparer.OrdinalIgnoreCase)
                    .Any(group => group.Select(node => node.Production).Distinct(StringComparer.Ordinal).Count() > 1);
                var resultLabels = ExpressExpressionEmitter.GenericTypeLabels([declaredType,]);
                var formalLabels = ExpressExpressionEmitter.GenericTypeLabels(formalTypes);
                var localLabels = ExpressExpressionEmitter.GenericTypeLabels(localTypes);
                var scopeLabels = ExpressExpressionEmitter.GenericTypeLabels(
                    formalTypes.Concat([declaredType,]).Concat(localTypes));
                var lexicalBounds = _schema.NameReferences
                    .Select(reference => reference.Target)
                    .Where(candidate => candidate.Kind is ExpressBoundNameKind.Parameter or ExpressBoundNameKind.Variable
                        && Contains(declaration.Syntax.Span, candidate.Span)
                        && candidate.Type is ExpressBoundScalarType
                        {
                            Kind: ExpressScalarKind.Integer or ExpressScalarKind.Number,
                        })
                    .Distinct()
                    .ToArray();
                var constructedArrays = DeclarationExpressions(declaration)
                    .Where(expression => expression.Kind == ExpressExpressionKind.AggregateInitializer
                        && expression.Type.DeclaredType is ExpressBoundAggregateType
                        {
                            Kind: ExpressAggregateKind.Array,
                        })
                    .Select(expression => (
                        Array: (ExpressBoundAggregateType)expression.Type.DeclaredType!,
                        expression.Type.CanBeIndeterminate))
                    .ToArray();
                var supportsInferredGenericResult = resultLabels.Count > 0
                    && !hasUnlabeledGeneric
                    && !hasConflictingGeneric
                    && resultLabels.All(label => formalLabels.Contains(label, StringComparer.OrdinalIgnoreCase))
                    && localLabels.All(label => formalLabels.Contains(label, StringComparer.OrdinalIgnoreCase))
                    && genericLabels.Length == scopeLabels.Count
                    && genericLabels.All(label => scopeLabels.Contains(label, StringComparer.OrdinalIgnoreCase))
                    && genericCalls.Length > 0
                    && genericCalls.All(call =>
                    {
                        if (call.Type.DeclaredType is not { } callType)
                        {
                            return false;
                        }

                        var callLabels = ExpressExpressionEmitter.GenericTypeLabels([callType,]);
                        var callElement = callType;
                        while (callElement is ExpressBoundAggregateType nestedCallAggregate)
                        {
                            callElement = nestedCallAggregate.ElementType;
                        }

                        if (callLabels.Count == 0)
                        {
                            return callElement is not ExpressBoundGenericType
                                || callElement is ExpressBoundGenericType { IsEntity: true, TypeLabel: null, };
                        }

                        if (!callLabels.All(label => resultLabels.Contains(
                                label,
                                StringComparer.OrdinalIgnoreCase)))
                        {
                            return false;
                        }

                        var caller = _schema.Declarations
                            .OfType<ExpressBoundOpaqueDeclaration>()
                            .SingleOrDefault(candidate => candidate.Kind == ExpressDeclarationKind.Function
                                && Contains(candidate.Syntax.Span, call.Span));
                        if (caller?.DeclaredType is null)
                        {
                            return false;
                        }

                        var callerHead = caller.Syntax.RequiredChild("functionHead");
                        var callerGenericNodes = caller.Syntax.DescendantsAndSelf()
                            .Where(node => node.Production is "genericType" or "genericEntityType")
                            .ToArray();
                        if (callerGenericNodes.Any(node => !node.ChildRules("typeLabel").Any())
                            || callerGenericNodes
                                .Where(node => node.ChildRules("typeLabel").SingleOrDefault() is not null)
                                .GroupBy(
                                    node => node.ChildRules("typeLabel").Single().IdentifierToken().Text,
                                    StringComparer.OrdinalIgnoreCase)
                                .Any(group => group.Select(node => node.Production)
                                    .Distinct(StringComparer.Ordinal)
                                    .Count() > 1))
                        {
                            return false;
                        }

                        var callerFormalTypes = callerHead.ChildRules("formalParameter")
                            .SelectMany(formal => formal.ChildRules("parameterId"))
                            .Select(parameter => _schema.NameReferences.Select(reference => reference.Target)
                                .Where(candidate => candidate.Kind == ExpressBoundNameKind.Parameter
                                    && SameStart(candidate.Span, parameter.Span))
                                .Distinct()
                                .SingleOrDefault()?.Type)
                            .OfType<ExpressBoundType>()
                            .ToArray();
                        var callerLocalTypes = caller.Syntax.RequiredChild("algorithmHead")
                            .ChildRules("localDecl")
                            .SelectMany(local => local.ChildRules("localVariable"))
                            .SelectMany(local => local.ChildRules("variableId"))
                            .Select(variable => _schema.NameReferences.Select(reference => reference.Target)
                                .Where(candidate => candidate.Kind == ExpressBoundNameKind.Variable
                                    && SameStart(candidate.Span, variable.Span))
                                .Distinct()
                                .SingleOrDefault()?.Type)
                            .OfType<ExpressBoundType>()
                            .ToArray();
                        var callerFormalLabels = ExpressExpressionEmitter.GenericTypeLabels(callerFormalTypes);
                        var callerResultLabels = ExpressExpressionEmitter.GenericTypeLabels(
                            [caller.DeclaredType,]);
                        var callerLocalLabels = ExpressExpressionEmitter.GenericTypeLabels(callerLocalTypes);
                        var callerScopeLabels = ExpressExpressionEmitter.GenericTypeLabels(
                            callerFormalTypes.Concat([caller.DeclaredType,]).Concat(callerLocalTypes));
                        return callLabels.All(label => callerScopeLabels.Contains(
                                label,
                                StringComparer.OrdinalIgnoreCase))
                            && callerResultLabels.All(label => callerFormalLabels.Contains(
                                label,
                                StringComparer.OrdinalIgnoreCase))
                            && callerLocalLabels.All(label => callerFormalLabels.Contains(
                                label,
                                StringComparer.OrdinalIgnoreCase));
                    })
                    && (declaredType is not ExpressBoundAggregateType { Kind: ExpressAggregateKind.Array, }
                        || (constructedArrays.Length > 0
                            && constructedArrays.All(construction =>
                            {
                                (string? Source, string? Resolved)[] bounds =
                                [
                                    (
                                        Source: construction.Array.LowerBoundText,
                                        Resolved: construction.Array.ResolvedLowerBoundText),
                                    (
                                        Source: construction.Array.UpperBoundText,
                                        Resolved: construction.Array.ResolvedUpperBoundText),
                                ];
                                return bounds.All(bound => int.TryParse(
                                        bound.Resolved ?? bound.Source,
                                        System.Globalization.NumberStyles.Integer,
                                        System.Globalization.CultureInfo.InvariantCulture,
                                        out _)
                                    || (bound.Source is not null
                                        && lexicalBounds.Any(candidate => string.Equals(
                                            candidate.Name,
                                            bound.Source,
                                            StringComparison.OrdinalIgnoreCase))))
                                    && (construction.CanBeIndeterminate
                                        || bounds.All(bound => int.TryParse(
                                            bound.Resolved ?? bound.Source,
                                            System.Globalization.NumberStyles.Integer,
                                            System.Globalization.CultureInfo.InvariantCulture,
                                            out _)));
                            })));
                if (!_resolver.IsSupported(declaredType) && !supportsInferredGenericResult)
                {
                    AddFailure(
                        declaration.Syntax.Span,
                        $"Reachable EXPRESS {declaration.Kind.ToString()} '{declaration.Name}' has no generated result type.");
                }

                foreach (var expression in DeclarationExpressions(declaration))
                {
                    if (declaration.Kind == ExpressDeclarationKind.Constant
                        || (IsFunctionReturnExpression(opaque, expression)
                            && !_schema.IndeterminateFunctions.Contains(declaration.Symbol)))
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
                _dependencyPath.Add(dependency);
                return true;
            }

            if (state != VisitState.Visiting)
            {
                return false;
            }

            var repeatedIndex = _dependencyPath.FindLastIndex(candidate => ReferenceEquals(candidate, dependency));
            if (repeatedIndex >= 0
                && _dependencyPath.Skip(repeatedIndex).All(candidate =>
                    candidate is ExpressBoundDeclaration { Kind: ExpressDeclarationKind.Function, }))
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
            if (_dependencyPath.Count == 0
                || !ReferenceEquals(_dependencyPath[_dependencyPath.Count - 1], dependency))
            {
                throw new InvalidOperationException("Reachable EXPRESS dependency traversal became unbalanced.");
            }

            _dependencyPath.RemoveAt(_dependencyPath.Count - 1);
            _states[dependency] = VisitState.Complete;
        }

        private IEnumerable<ExpressBoundExpression> DeclarationExpressions(
            ExpressBoundDeclaration declaration)
        {
            return _schema.Expressions
                .Where(expression => Contains(declaration.Syntax.Span, expression.Span));
        }

        private ExpressBoundExpression GetExpression(ExpressRuleSyntax syntax)
        {
            return _schema.Expressions.Single(expression => SameSpan(expression.Span, syntax.Span));
        }

        private void ValidateFunctionShape(ExpressBoundDeclaration declaration)
        {
            var statements = declaration.Syntax.ChildRules("stmt").ToArray();
            var operations = declaration.Syntax.DescendantsAndSelf()
                .Where(candidate => candidate.Production == "stmt")
                .Select(statement => statement.ChildRules().Single())
                .ToArray();
            if (statements.Length > 0
                && operations.All(operation =>
                    operation.Production is "assignmentStmt" or "caseStmt" or "compoundStmt" or "ifStmt" or "repeatStmt" or "returnStmt")
                && operations.Where(operation => operation.Production == "assignmentStmt")
                    .All(assignment =>
                    {
                        var qualifiers = assignment.ChildRules("qualifier").ToArray();
                        if (qualifiers.Any(qualifier =>
                                qualifier.ChildRules().Single().Production == "attributeQualifier"))
                        {
                            var targetSyntax = assignment.RequiredChild("generalRef");
                            var target = _schema.NameReferences
                                .Where(reference => SameStart(reference.Span, targetSyntax.Span))
                                .Select(reference => reference.Target)
                                .Single();
                            if (target.Kind != ExpressBoundNameKind.Variable
                                || target.Type is not ExpressBoundNamedType
                                {
                                    Declaration.Kind: ExpressDeclarationKind.Entity,
                                })
                            {
                                return false;
                            }
                        }

                        return qualifiers.All(qualifier =>
                        {
                            var operation = qualifier.ChildRules().Single();
                            if (operation.Production == "indexQualifier")
                            {
                                return !operation.ChildRules("index2").Any();
                            }

                            var attribute = _schema.NameReferences
                                .Where(reference => Contains(operation.Span, reference.Span))
                                .Select(reference => reference.Target.Attribute)
                                .SingleOrDefault(candidate => candidate is not null);
                            return operation.Production == "attributeQualifier"
                                && attribute?.Kind == ExpressAttributeKind.Explicit;
                        });
                    })
                && operations.Where(operation => operation.Production == "repeatStmt")
                    .All(repeat => repeat.RequiredChild("repeatControl") is { } control
                        && control.ChildRules("incrementControl").Count() == 1
                        && !control.ChildRules("whileControl").Any()
                        && !control.ChildRules("untilControl").Any())
                && operations.Where(operation => operation.Production == "caseStmt")
                    .All(caseStatement => caseStatement.ChildRules("caseAction")
                            .All(action => action.ChildRules("caseLabel").Any()
                                && action.ChildRules("stmt").Count() == 1)
                        && caseStatement.ChildRules("stmt").Count() <= 1)
                && operations.Where(operation => operation.Production == "returnStmt")
                    .All(returnStatement => returnStatement.ChildRules("expression").Count() == 1)
                && AlwaysReturns(statements.Last())
                && !declaration.Syntax.RequiredChild("algorithmHead")
                    .ChildRules()
                    .Any(child => child.Production is "constantDecl" or "declaration"))
            {
                return;
            }

            _failures.Add(new(
                _schema,
                declaration.Syntax.Span.Start,
                $"Reachable EXPRESS function '{declaration.Name}' uses an algorithm statement shape that has no static generator."));
        }

        private static bool AlwaysReturns(ExpressRuleSyntax statement)
        {
            var operation = statement.Production == "stmt"
                ? statement.ChildRules().Single()
                : statement;
            if (operation.Production == "returnStmt")
            {
                return operation.ChildRules("expression").Count() == 1;
            }

            if (operation.Production == "compoundStmt")
            {
                var statements = operation.ChildRules("stmt").ToArray();
                return statements.Length > 0 && AlwaysReturns(statements.Last());
            }

            if (operation.Production == "caseStmt")
            {
                var otherwise = operation.ChildRules("stmt").SingleOrDefault();
                return otherwise is not null
                    && AlwaysReturns(otherwise)
                    && operation.ChildRules("caseAction").All(action =>
                        AlwaysReturns(action.RequiredChild("stmt")));
            }

            if (operation.Production != "ifStmt" || !operation.HasDirectToken("ELSE"))
            {
                return false;
            }

            var thenStatements = new List<ExpressRuleSyntax>();
            var elseStatements = new List<ExpressRuleSyntax>();
            var inElse = false;
            foreach (var child in operation.Children)
            {
                if (child is ExpressTokenSyntax token
                    && string.Equals(token.Text, "ELSE", StringComparison.OrdinalIgnoreCase))
                {
                    inElse = true;
                }
                else if (child is ExpressRuleSyntax { Production: "stmt", } nested)
                {
                    (inElse ? elseStatements : thenStatements).Add(nested);
                }
            }

            return thenStatements.Count > 0
                && elseStatements.Count > 0
                && AlwaysReturns(thenStatements.Last())
                && AlwaysReturns(elseStatements.Last());
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