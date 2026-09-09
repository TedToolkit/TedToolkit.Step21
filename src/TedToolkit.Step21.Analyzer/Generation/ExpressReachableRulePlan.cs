// -----------------------------------------------------------------------
// <copyright file="ExpressReachableRulePlan.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

using TedToolkit.Step21.Analyzer.Express;
using TedToolkit.Step21.Analyzer.Express.Analysis;
using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Computes the private executable dependency closure rooted at generated validation rules.
/// </summary>
internal sealed class ExpressReachableRulePlan
{
    private readonly ReadOnlyDictionary<ExpressBoundSymbol, ExpressBoundDeclaration> _declarations;

    private ExpressReachableRulePlan(
        ExpressBoundSchema schema,
        ExpressAnalyzedCompilation compilation,
        ExpressGeneratedTypeResolver resolver,
        IReadOnlyDictionary<ExpressBoundSymbol, ExpressBoundDeclaration> declarations,
        IReadOnlyList<ExpressEntityProjection> entityProjections,
        IReadOnlyList<ExpressComplexEntityProjection> complexEntityProjections,
        IEnumerable<ExpressBoundDeclaration> reachableDeclarations,
        IEnumerable<ExpressBoundAttribute> reachableDerivedAttributes,
        IEnumerable<ExpressBoundAttribute> reachableSingularInverseAttributes,
        IEnumerable<ExpressSemanticRule> reachableRules,
        bool requiresEntityValueEquality,
        bool requiresNumberAssignmentIndex,
        bool requiresIntegerAssignmentIndex,
        IEnumerable<ExpressEntityGenerationFailure> failures)
    {
        Schema = schema;
        Compilation = compilation;
        Analysis = compilation.GetAnalysis(schema);
        Resolver = resolver;
        _declarations = new(
            declarations.ToDictionary(pair => pair.Key, pair => pair.Value));
        EntityProjections = new ReadOnlyCollection<ExpressEntityProjection>(entityProjections.ToArray());
        ComplexEntityProjections = new ReadOnlyCollection<ExpressComplexEntityProjection>(
            complexEntityProjections.ToArray());
        ReachableDeclarations = new ReadOnlyCollection<ExpressBoundDeclaration>(
            reachableDeclarations.ToArray());
        ReachableDerivedAttributes = new ReadOnlyCollection<ExpressBoundAttribute>(
            reachableDerivedAttributes.ToArray());
        ReachableSingularInverseAttributes = new ReadOnlyCollection<ExpressBoundAttribute>(
            reachableSingularInverseAttributes.ToArray());
        ReachableRules = new ReadOnlyCollection<ExpressSemanticRule>(reachableRules.ToArray());
        RequiresEntityValueEquality = requiresEntityValueEquality;
        RequiresNumberAssignmentIndex = requiresNumberAssignmentIndex;
        RequiresIntegerAssignmentIndex = requiresIntegerAssignmentIndex;
        Failures = new ReadOnlyCollection<ExpressEntityGenerationFailure>(failures.ToArray());
    }

    /// <summary>
    /// Gets the schema whose private rule closure is represented.
    /// </summary>
    internal ExpressBoundSchema Schema { get; }

    /// <summary>
    /// Gets syntax-detached semantic rule analysis for the schema.
    /// </summary>
    internal ExpressSchemaAnalysis Analysis { get; }

    private ExpressAnalyzedCompilation Compilation { get; }

    /// <summary>
    /// Gets the generated value-type resolver retained by this plan.
    /// </summary>
    internal ExpressGeneratedTypeResolver Resolver { get; }

    /// <summary>
    /// Gets supported defined types in stable closed-compilation order.
    /// </summary>
    internal IReadOnlyList<ExpressBoundDefinedType> SupportedDefinedTypes
    {
        get
        {
            return Resolver.GetSupportedDeclarations(Compilation);
        }
    }

    /// <summary>
    /// Gets the generated entity storage projections available to private rule lowering.
    /// </summary>
    internal IReadOnlyList<ExpressEntityProjection> EntityProjections { get; }

    /// <summary>
    /// Gets generated complex projections that can make sibling-derived overrides applicable.
    /// </summary>
    internal IReadOnlyList<ExpressComplexEntityProjection> ComplexEntityProjections { get; }

    /// <summary>
    /// Gets reachable constant and function declarations in deterministic source order.
    /// </summary>
    internal IReadOnlyList<ExpressBoundDeclaration> ReachableDeclarations { get; }

    /// <summary>
    /// Gets reachable derived attributes in deterministic source order.
    /// </summary>
    internal IReadOnlyList<ExpressBoundAttribute> ReachableDerivedAttributes { get; }

    /// <summary>
    /// Gets validation-reachable entity-valued inverse attributes in deterministic source order.
    /// </summary>
    internal IReadOnlyList<ExpressBoundAttribute> ReachableSingularInverseAttributes { get; }

    /// <summary>
    /// Gets the validation-rooted WHERE expressions in deterministic source order.
    /// </summary>
    internal IReadOnlyList<ExpressSemanticRule> ReachableRules { get; }

    /// <summary>
    /// Gets a value indicating whether the reachable closure requires generated entity value equality.
    /// </summary>
    internal bool RequiresEntityValueEquality { get; }

    /// <summary>
    /// Gets a value indicating whether the reachable closure requires exact NUMBER assignment indices.
    /// </summary>
    internal bool RequiresNumberAssignmentIndex { get; }

    /// <summary>
    /// Gets a value indicating whether the reachable closure requires nullable INTEGER assignment indices.
    /// </summary>
    internal bool RequiresIntegerAssignmentIndex { get; }

    /// <summary>
    /// Gets source-located closure failures that make schema generation unsafe.
    /// </summary>
    internal IReadOnlyList<ExpressEntityGenerationFailure> Failures { get; }

    /// <summary>
    /// Creates the complete validation-rooted dependency plan for one schema.
    /// </summary>
    /// <param name="schema">The bound schema.</param>
    /// <param name="compilation">The analyzed closed compilation.</param>
    /// <param name="resolver">The generated value-type resolver.</param>
    /// <param name="entityProjections">The generated entity storage projections.</param>
    /// <param name="complexEntityProjections">The generated complex entity projections.</param>
    /// <returns>The immutable rule plan.</returns>
    internal static ExpressReachableRulePlan Create(
        ExpressBoundSchema schema,
        ExpressAnalyzedCompilation compilation,
        ExpressGeneratedTypeResolver resolver,
        IReadOnlyList<ExpressEntityProjection> entityProjections,
        IReadOnlyList<ExpressComplexEntityProjection>? complexEntityProjections = null)
    {
        var analysis = compilation.GetAnalysis(schema);
        var declarations = schema.Declarations.Concat(schema.NestedDeclarations)
            .ToDictionary(declaration => declaration.Symbol);
        var builder = new Builder(
            schema,
            compilation,
            declarations,
            resolver,
            entityProjections,
            complexEntityProjections ?? []);
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
    /// Gets syntax-detached semantic structure for any declaration in the closed compilation.
    /// </summary>
    /// <param name="declaration">The analyzed declaration.</param>
    /// <returns>The declaration's semantic rule model.</returns>
    internal ExpressSemanticRule GetSemanticDeclaration(ExpressBoundDeclaration declaration)
    {
        var schema = Compilation.Schemas.Single(candidate =>
            ReferenceEquals(candidate.Identity, declaration.DeclaringSchema));
        return Compilation.GetAnalysis(schema).GetDeclaration(declaration);
    }

    /// <summary>
    /// Gets the bound expression corresponding exactly to retained expression syntax.
    /// </summary>
    /// <param name="syntax">The retained expression syntax.</param>
    /// <returns>The typed expression.</returns>
    internal ExpressBoundExpression GetExpression(ExpressSemanticRule syntax)
    {
        return Analysis.GetExpression(syntax.Span);
    }

    /// <summary>
    /// Resolves the most-specific explicitly supplied entity projection in a complex constructor.
    /// </summary>
    /// <param name="expression">The flattened evaluated-set union expression.</param>
    /// <returns>The unique projection that contains every supplied partial entity, or null.</returns>
    internal ExpressEntityProjection? GetComplexConstructionTarget(ExpressBoundExpression expression)
    {
        return GetComplexConstructionTarget(expression, EntityProjections);
    }

    private static ExpressEntityProjection? GetComplexConstructionTarget(
        ExpressBoundExpression expression,
        IReadOnlyList<ExpressEntityProjection> entityProjections)
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

        var supplied = components.Select(component =>
        {
            ExpressBoundSymbol? symbol;
            if (component.Kind == ExpressExpressionKind.Application
                && component.Reference?.Kind == ExpressBoundNameKind.Entity
                && component.Reference.SchemaDeclaration is { } applicationEntity)
            {
                symbol = applicationEntity;
            }
            else
            {
                symbol = (component.Type.DeclaredType as ExpressBoundNamedType)?.Declaration;
            }

            return symbol is null
                ? null
                : entityProjections.SingleOrDefault(candidate => ReferenceEquals(
                    candidate.Entity.Symbol,
                    symbol));
        })
            .ToArray();
        if (supplied.Any(component => component is null))
        {
            return null;
        }

        var candidates = supplied
            .Select(component => component!)
            .Distinct()
            .Where(candidate => supplied.All(component => candidate.PhysicalComponents.Any(physical =>
                ReferenceEquals(physical.Symbol, component!.Entity.Symbol))))
            .OrderByDescending(candidate => candidate.PhysicalComponents.Count)
            .ToArray();
        return candidates.Length == 1
            || (candidates.Length > 1
                && candidates[0].PhysicalComponents.Count > candidates[1].PhysicalComponents.Count)
            ? candidates[0]
            : null;
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
        var syntax = Analysis.GetDeclaration(owner).DescendantsAndSelf()
            .Where(candidate => candidate.Role == "derivedAttr")
            .Single(candidate => SameStart(
                candidate.RequiredChild("attributeDecl").Span,
                attribute.Span));
        return GetExpression(syntax.RequiredChild("expression"));
    }

    /// <summary>
    /// Determines whether a CASE covers every value of a closed enumeration selector.
    /// </summary>
    /// <param name="caseStatement">The CASE statement syntax.</param>
    /// <returns><see langword="true" /> when every enumeration value has a case label.</returns>
    internal bool IsExhaustiveCase(ExpressSemanticRule caseStatement)
    {
        return IsExhaustiveCase(Analysis, Resolver, caseStatement);
    }

    private static bool IsExhaustiveCase(
        ExpressSchemaAnalysis analysis,
        ExpressGeneratedTypeResolver resolver,
        ExpressSemanticRule caseStatement)
    {
        var selectorSyntax = caseStatement.RequiredChild("selector").RequiredChild("expression");
        ExpressBoundType? type = analysis.GetExpression(selectorSyntax.Span).Type.DeclaredType;
        while (type is ExpressBoundNamedType named
               && named.Declaration.Kind != ExpressDeclarationKind.Entity)
        {
            type = resolver.GetDefinedType(named.Declaration).UnderlyingType;
        }

        if (type is not ExpressBoundEnumerationType { IsExtensible: false, } enumeration)
        {
            return false;
        }

        var labels = new HashSet<string>(caseStatement.ChildRules("caseAction")
            .SelectMany(action => action.ChildRules("caseLabel"))
            .Select(label => label.RequiredChild("expression"))
            .Select(label => analysis.GetExpression(label.Span).Reference?.Name)
            .OfType<string>(), StringComparer.OrdinalIgnoreCase);
        return resolver.GetEnumerationValues(enumeration).All(labels.Contains);
    }

    /// <summary>
    /// Gets the exact attribute selected by one UNIQUE referenced-attribute syntax node.
    /// </summary>
    /// <param name="syntax">The referenced attribute syntax.</param>
    /// <returns>The bound attribute.</returns>
    internal ExpressBoundAttribute GetReferencedAttribute(ExpressSemanticRule syntax)
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

        private readonly ExpressSchemaAnalysis _analysis;

        private readonly ExpressAnalyzedCompilation _compilation;

        private readonly IReadOnlyDictionary<ExpressBoundSymbol, ExpressBoundDeclaration> _declarations;

        private readonly ExpressGeneratedTypeResolver _resolver;

        private readonly IReadOnlyList<ExpressEntityProjection> _entityProjections;

        private readonly IReadOnlyList<ExpressComplexEntityProjection> _complexEntityProjections;

        private readonly HashSet<ExpressBoundDeclaration> _reachableDeclarations = [];

        private readonly HashSet<ExpressBoundAttribute> _reachableDerivedAttributes = [];

        private readonly HashSet<ExpressBoundAttribute> _reachableSingularInverseAttributes = [];

        private readonly HashSet<ExpressSemanticRule> _reachableRules = [];

        private readonly Dictionary<object, VisitState> _states = [];

        private readonly List<object> _dependencyPath = [];

        private readonly List<ExpressEntityGenerationFailure> _failures = [];

        private bool _requiresEntityValueEquality;

        private bool _requiresNumberAssignmentIndex;

        private bool _requiresIntegerAssignmentIndex;

        internal Builder(
            ExpressBoundSchema schema,
            ExpressAnalyzedCompilation compilation,
            IReadOnlyDictionary<ExpressBoundSymbol, ExpressBoundDeclaration> declarations,
            ExpressGeneratedTypeResolver resolver,
            IReadOnlyList<ExpressEntityProjection> entityProjections,
            IReadOnlyList<ExpressComplexEntityProjection> complexEntityProjections)
        {
            _schema = schema;
            _compilation = compilation;
            _analysis = compilation.GetAnalysis(schema);
            _declarations = declarations;
            _resolver = resolver;
            _entityProjections = entityProjections;
            _complexEntityProjections = complexEntityProjections;
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
                    foreach (var expression in DeclarationExpressions(declaration))
                    {
                        VisitExpression(expression, root: null);
                    }
                }

                foreach (var rule in _analysis.GetDeclaration(declaration).DescendantsAndSelf()
                             .Where(candidate => candidate.Role == "domainRule"))
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
            var orderedSingularInverseAttributes = _schema.Declarations.Concat(_schema.NestedDeclarations)
                .OfType<ExpressBoundEntity>()
                .SelectMany(entity => entity.Attributes)
                .Where(_reachableSingularInverseAttributes.Contains);
            var orderedRules = _schema.Declarations.Concat(_schema.NestedDeclarations)
                .SelectMany(declaration => _analysis.GetDeclaration(declaration).DescendantsAndSelf())
                .Where(candidate => candidate.Role == "domainRule" && _reachableRules.Contains(candidate));
            return new(
                _schema,
                _compilation,
                _resolver,
                _declarations,
                _entityProjections,
                _complexEntityProjections,
                orderedDeclarations,
                orderedAttributes,
                orderedSingularInverseAttributes,
                orderedRules,
                _requiresEntityValueEquality,
                _requiresNumberAssignmentIndex,
                _requiresIntegerAssignmentIndex,
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
                if (AnalysisFor(imported).GetDeclaration(imported).ChildRules("whereClause").Any())
                {
                    AddFailure(
                        named.Span,
                        $"Validation-reachable cross-schema type rule '{imported.Name}' requires "
                        + "the multi-schema execution boundary.");
                }

                return;
            }

            foreach (var rule in _analysis.GetDeclaration(defined).ChildRules("whereClause")
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

        private ExpressSchemaAnalysis AnalysisFor(ExpressBoundDeclaration declaration)
        {
            var schema = _compilation.Schemas.Single(candidate =>
                ReferenceEquals(candidate.Identity, declaration.DeclaringSchema));
            return _compilation.GetAnalysis(schema);
        }

        private void VisitRule(ExpressSemanticRule rule)
        {
            _reachableRules.Add(rule);
            VisitExpression(GetExpression(rule.RequiredChild("expression")), root: null);
        }

        private void VisitUniqueDependencies(ExpressBoundEntity entity)
        {
            var uniqueClause = _analysis.GetDeclaration(entity).RequiredChild("entityBody")
                .ChildRules("uniqueClause")
                .SingleOrDefault();
            if (uniqueClause is null)
            {
                return;
            }

            foreach (var referencedAttribute in uniqueClause.DescendantsAndSelf()
                         .Where(candidate => candidate.Role == "referencedAttribute"))
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
                else if (attribute.Kind == ExpressAttributeKind.Inverse
                         && attribute.Type is not ExpressBoundAggregateType)
                {
                    _reachableSingularInverseAttributes.Add(attribute);
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
                    out _)
                || (boundary == "upper"
                    && _schema.NameReferences.Any(reference =>
                        Contains(aggregate.Span, reference.Span)
                        && reference.Target.Attribute is not null
                        && StringComparer.OrdinalIgnoreCase.Equals(
                            reference.Target.Name,
                            sourceText))))
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
                        _reachableSingularInverseAttributes.Add(inverse);
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

                    var target = GetComplexConstructionTarget(node, _entityProjections);
                    var suppliedSlots = new List<(ExpressBoundSymbol Entity, string Attribute)>();
                    var incompatible = target is not
                    {
                        Entity.IsAbstract: false,
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
                            var constructorAttributes = projection is null
                                ? []
                                : ExpressComplexEntityProjection.GetComponentAttributes(projection);
                            incompatible |= projection is null
                                || target?.PhysicalComponents.Any(entity => ReferenceEquals(
                                    entity.Symbol,
                                    componentSymbol)) != true
                                || constructorAttributes.Count != component.Children.Count;
                            if (projection is not null)
                            {
                                suppliedSlots.AddRange(constructorAttributes.Select(attribute =>
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
                                ExpressTypeAnalysis.RequiresSchemaValueEquality(child.Type)))
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

                foreach (var attribute in reference?.AttributeCandidates
                             .Where(candidate => candidate.Kind == ExpressAttributeKind.Explicit)
                         ?? [])
                {
                    foreach (var derived in FindDerivedOverrides(node, attribute))
                    {
                        if (ReferenceEquals(derived, root))
                        {
                            continue;
                        }

                        VisitDerived(derived, root);
                    }
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

        private IEnumerable<ExpressBoundAttribute> FindDerivedOverrides(
            ExpressBoundExpression expression,
            ExpressBoundAttribute attribute)
        {
            var source = expression.Children.Count == 0 ? null : expression.Children[0];
            while (source?.Kind == ExpressExpressionKind.GroupQualifier)
            {
                source = source.Children.Count == 0 ? null : source.Children[0];
            }

            var sourceProjections = GetSourceProjections(source?.Type.DeclaredType).ToArray();
            if (sourceProjections.Length == 0)
            {
                return [];
            }

            return _entityProjections
                .Where(projection => projection.DerivedRedeclaredAttributes.Any(candidate =>
                        ReferenceEquals(candidate.Attribute, attribute))
                    && sourceProjections.Any(sourceProjection =>
                        sourceProjection.PhysicalComponents.Contains(projection.Entity)
                        || _complexEntityProjections.Any(complex =>
                            complex.Leaves.Any(leaf =>
                                leaf.PhysicalComponents.Contains(sourceProjection.Entity))
                            && complex.Leaves.Any(leaf =>
                                leaf.PhysicalComponents.Contains(projection.Entity)))))
                .SelectMany(projection => projection.Entity.Attributes.Where(candidate =>
                    candidate.Kind == ExpressAttributeKind.Derived
                    && StringComparer.OrdinalIgnoreCase.Equals(candidate.Name, attribute.Name)))
                .Distinct();
        }

        private IEnumerable<ExpressEntityProjection> GetSourceProjections(ExpressBoundType? type)
        {
            var visited = new HashSet<ExpressBoundSymbol>();
            var pending = new Stack<ExpressBoundType>();
            if (type is not null)
            {
                pending.Push(type);
            }

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (current is ExpressBoundSelectType directSelect)
                {
                    foreach (var alternative in _resolver.GetSelectAlternatives(directSelect))
                    {
                        pending.Push(new ExpressBoundNamedType(alternative, current.Span));
                    }

                    continue;
                }

                if (current is not ExpressBoundNamedType named || !visited.Add(named.Declaration))
                {
                    continue;
                }

                if (named.Declaration.Kind == ExpressDeclarationKind.Entity)
                {
                    var projection = _entityProjections.SingleOrDefault(candidate =>
                        ReferenceEquals(candidate.Entity.Symbol, named.Declaration));
                    if (projection is not null)
                    {
                        yield return projection;
                    }

                    continue;
                }

                pending.Push(_resolver.GetDefinedType(named.Declaration).UnderlyingType);
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
            var syntax = _analysis.GetDeclaration(owner).DescendantsAndSelf()
                .Where(candidate => candidate.Role == "derivedAttr")
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
                    ? _analysis.GetDeclaration(declaration).RequiredChild("functionHead")
                    : null;
                var formalTypes = functionHead?.ChildRules("formalParameter")
                    .SelectMany(formal => formal.ChildRules("parameterId"))
                    .Select(parameter => _schema.LexicalNames
                        .Where(candidate => candidate.Kind == ExpressBoundNameKind.Parameter
                            && SameStart(candidate.Span, parameter.Span))
                        .Distinct()
                        .SingleOrDefault()?.Type)
                    .OfType<ExpressBoundType>()
                    .ToArray() ?? Array.Empty<ExpressBoundType>();
                var localTypes = declaration.Kind == ExpressDeclarationKind.Function
                    ? _analysis.GetDeclaration(declaration).RequiredChild("algorithmHead")
                        .ChildRules("localDecl")
                        .SelectMany(local => local.ChildRules("localVariable"))
                        .SelectMany(local => local.ChildRules("variableId"))
                        .Select(variable => _schema.LexicalNames
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
                    ? _analysis.GetDeclaration(declaration).DescendantsAndSelf()
                        .Where(node => node.Role is "genericType" or "genericEntityType")
                        .ToArray()
                    : [];
                var genericLabels = genericNodes
                    .SelectMany(node => node.ChildRules("typeLabel"))
                    .Select(node => node.Identifier!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var hasUnlabeledGeneric = genericNodes.Any(node => !node.ChildRules("typeLabel").Any());
                var hasConflictingGeneric = genericNodes
                    .Where(node => node.ChildRules("typeLabel").SingleOrDefault() is not null)
                    .GroupBy(
                        node => node.ChildRules("typeLabel").Single().Identifier!,
                        StringComparer.OrdinalIgnoreCase)
                    .Any(group => group.Select(node => node.Role).Distinct(StringComparer.Ordinal).Count() > 1);
                var resultLabels = ExpressTypeAnalysis.GenericTypeLabels([declaredType,]);
                var formalLabels = ExpressTypeAnalysis.GenericTypeLabels(formalTypes);
                var localLabels = ExpressTypeAnalysis.GenericTypeLabels(localTypes);
                var scopeLabels = ExpressTypeAnalysis.GenericTypeLabels(
                    formalTypes.Concat([declaredType,]).Concat(localTypes));
                var lexicalBounds = _schema.LexicalNames
                    .Where(candidate => candidate.Kind is ExpressBoundNameKind.Parameter or ExpressBoundNameKind.Variable
                        && Contains(_analysis.GetDeclaration(declaration).Span, candidate.Span)
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

                        var callLabels = ExpressTypeAnalysis.GenericTypeLabels([callType,]);
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
                                && Contains(AnalysisFor(candidate).GetDeclaration(candidate).Span, call.Span));
                        if (caller?.DeclaredType is null)
                        {
                            return false;
                        }

                        var callerRule = AnalysisFor(caller).GetDeclaration(caller);
                        var callerHead = callerRule.RequiredChild("functionHead");
                        var callerGenericNodes = callerRule.DescendantsAndSelf()
                            .Where(node => node.Role is "genericType" or "genericEntityType")
                            .ToArray();
                        if (callerGenericNodes.Any(node => !node.ChildRules("typeLabel").Any())
                            || callerGenericNodes
                                .Where(node => node.ChildRules("typeLabel").SingleOrDefault() is not null)
                                .GroupBy(
                                    node => node.ChildRules("typeLabel").Single().Identifier!,
                                    StringComparer.OrdinalIgnoreCase)
                                .Any(group => group.Select(node => node.Role)
                                    .Distinct(StringComparer.Ordinal)
                                    .Count() > 1))
                        {
                            return false;
                        }

                        var callerFormalTypes = callerHead.ChildRules("formalParameter")
                            .SelectMany(formal => formal.ChildRules("parameterId"))
                            .Select(parameter => _schema.LexicalNames
                                .Where(candidate => candidate.Kind == ExpressBoundNameKind.Parameter
                                    && SameStart(candidate.Span, parameter.Span))
                                .Distinct()
                                .SingleOrDefault()?.Type)
                            .OfType<ExpressBoundType>()
                            .ToArray();
                        var callerLocalTypes = callerRule.RequiredChild("algorithmHead")
                            .ChildRules("localDecl")
                            .SelectMany(local => local.ChildRules("localVariable"))
                            .SelectMany(local => local.ChildRules("variableId"))
                            .Select(variable => _schema.LexicalNames
                                .Where(candidate => candidate.Kind == ExpressBoundNameKind.Variable
                                    && SameStart(candidate.Span, variable.Span))
                                .Distinct()
                                .SingleOrDefault()?.Type)
                            .OfType<ExpressBoundType>()
                            .ToArray();
                        var callerFormalLabels = ExpressTypeAnalysis.GenericTypeLabels(callerFormalTypes);
                        var callerResultLabels = ExpressTypeAnalysis.GenericTypeLabels(
                            [caller.DeclaredType,]);
                        var callerLocalLabels = ExpressTypeAnalysis.GenericTypeLabels(callerLocalTypes);
                        var callerScopeLabels = ExpressTypeAnalysis.GenericTypeLabels(
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
                        _analysis.GetDeclaration(declaration).Span,
                        $"Reachable EXPRESS {declaration.Kind.ToString()} '{declaration.Name}' has no generated result type.");
                }

                foreach (var expression in DeclarationExpressions(declaration))
                {
                    if ((declaration.Kind == ExpressDeclarationKind.Constant
                            && IsConstantValueExpression(opaque, expression))
                        || (IsFunctionReturnExpression(opaque, expression)
                            && !_schema.IndeterminateFunctions.Contains(declaration.Symbol)))
                    {
                        ValidateDependencyResult(declaration.Name, declaredType, expression);
                    }
                }
            }

            if (declaration.Kind is ExpressDeclarationKind.Function or ExpressDeclarationKind.Procedure)
            {
                ValidateFunctionShape(declaration);
            }

            foreach (var expression in DeclarationExpressions(declaration))
            {
                VisitExpression(expression, declaration);
            }

            foreach (var procedureReference in AlgorithmOperations(_analysis.GetDeclaration(declaration))
                         .Where(operation => operation.Role == "procedureCallStmt")
                         .SelectMany(operation => operation.ChildRules("procedureRef")))
            {
                var procedure = _schema.NameReferences
                    .Where(reference => SameStart(reference.Span, procedureReference.Span))
                    .Select(reference => reference.Target.SchemaDeclaration)
                    .OfType<ExpressBoundSymbol>()
                    .Distinct()
                    .SingleOrDefault();
                if (procedure is not null && _declarations.TryGetValue(procedure, out var procedureDeclaration))
                {
                    VisitDeclaration(procedureDeclaration, declaration);
                }
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
            var cycle = repeatedIndex < 0
                ? []
                : _dependencyPath.Skip(repeatedIndex).ToArray();
            if (cycle.Any(candidate => candidate is ExpressBoundDeclaration
                { Kind: ExpressDeclarationKind.Function, })
                && cycle.All(candidate => candidate is ExpressBoundAttribute
                    or ExpressBoundDeclaration { Kind: ExpressDeclarationKind.Function, }))
            {
                return false;
            }

            var span = dependency switch
            {
                ExpressBoundDeclaration declaration => AnalysisFor(declaration).GetDeclaration(declaration).Span,
                ExpressBoundAttribute attribute => attribute.Span,
                _ => _schema.Identity.Span,
            };
            AddFailure(
                span,
                $"Reachable EXPRESS dependency cycle includes '{DependencyName(dependency)}'"
                + (parent is null ? "." : $" from '{DependencyName(parent)}'."));

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
                .Where(expression => Contains(AnalysisFor(declaration).GetDeclaration(declaration).Span, expression.Span));
        }

        private ExpressBoundExpression GetExpression(ExpressSemanticRule syntax)
        {
            return _analysis.GetExpression(syntax.Span);
        }

        private void ValidateFunctionShape(ExpressBoundDeclaration declaration)
        {
            var declarationRule = _analysis.GetDeclaration(declaration);
            var statements = declarationRule.ChildRules("stmt").ToArray();
            var operations = AlgorithmOperations(declarationRule).ToArray();
            var isProcedure = declaration.Kind == ExpressDeclarationKind.Procedure;
            if (!ValidateControlAndIndexTypes(operations))
            {
                return;
            }

            if (statements.Length > 0
                && operations.All(operation =>
                    operation.Role is "assignmentStmt" or "caseStmt" or "compoundStmt" or "ifStmt"
                        or "repeatStmt" or "returnStmt" or "escapeStmt" or "skipStmt"
                        or "aliasStmt" or "nullStmt"
                    || (operation.Role == "procedureCallStmt"
                        && (IsSupportedListProcedure(operation) || IsSupportedProcedureCall(operation))))
                && (!operations.Any(operation => operation.Role is "escapeStmt" or "skipStmt")
                    || HasValidLoopTransfers(declarationRule, false))
                && operations.Where(operation => operation.Role == "aliasStmt")
                    .All(alias => alias.ChildRules("qualifier").All(qualifier =>
                        qualifier.ChildRules().Single().Role is "attributeQualifier" or "groupQualifier"))
                && operations.Where(operation => operation.Role == "assignmentStmt")
                    .All(assignment =>
                    {
                        var qualifiers = assignment.ChildRules("qualifier").ToArray();
                        if (qualifiers.Any(qualifier =>
                                qualifier.ChildRules().Single().Role == "attributeQualifier"))
                        {
                            var targetSyntax = assignment.RequiredChild("generalRef");
                            var target = _schema.NameReferences
                                .Where(reference => SameStart(reference.Span, targetSyntax.Span))
                                .Select(reference => reference.Target)
                                .Single();
                            if (target.Kind is not (ExpressBoundNameKind.Variable or ExpressBoundNameKind.Parameter)
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
                            if (operation.Role == "indexQualifier")
                            {
                                return true;
                            }

                            if (operation.Role == "groupQualifier")
                            {
                                return _schema.NameReferences.Any(reference =>
                                    Contains(operation.Span, reference.Span)
                                    && reference.Target.Kind == ExpressBoundNameKind.Entity);
                            }

                            var attribute = _schema.NameReferences
                                .Where(reference => Contains(operation.Span, reference.Span))
                                .Select(reference => reference.Target.Attribute)
                                .SingleOrDefault(candidate => candidate is not null);
                            return operation.Role == "attributeQualifier"
                                && attribute?.Kind == ExpressAttributeKind.Explicit;
                        });
                    })
                && operations.Where(operation => operation.Role == "repeatStmt")
                    .All(repeat => repeat.RequiredChild("repeatControl") is { } control
                        && control.ChildRules("incrementControl").Count() <= 1
                        && control.ChildRules("whileControl").Count() <= 1
                        && control.ChildRules("untilControl").Count() <= 1)
                && operations.Where(operation => operation.Role == "caseStmt")
                    .All(caseStatement => caseStatement.ChildRules("caseAction")
                            .All(action => action.ChildRules("caseLabel").Any()
                                && action.ChildRules("stmt").Count() == 1)
                        && caseStatement.ChildRules("stmt").Count() <= 1)
                && operations.Where(operation => operation.Role == "returnStmt")
                    .All(returnStatement => returnStatement.ChildRules("expression").Count() == (isProcedure ? 0 : 1))
                && !declarationRule.RequiredChild("algorithmHead")
                    .ChildRules()
                    .Any(child => child.Role == "declaration"
                        && child.ChildRules().Single().Role is not ("functionDecl" or "procedureDecl"))
                && !HasLexicalCapture(declaration, declarationRule))
            {
                return;
            }

            _failures.Add(new(
                _schema,
                declarationRule.Span.Start,
                $"Reachable EXPRESS {(isProcedure ? "procedure" : "function")} '{declaration.Name}' uses an "
                    + "algorithm statement shape that has no static generator."));
        }

        private bool ValidateControlAndIndexTypes(IReadOnlyList<ExpressSemanticRule> operations)
        {
            var valid = true;
            foreach (var operation in operations)
            {
                if (!ValidateProcedureScalarArguments(operation))
                {
                    valid = false;
                }

                if (!ValidateAssignmentRanges(operation))
                {
                    valid = false;
                }

                foreach (var qualifier in operation.Role == "assignmentStmt"
                             ? operation.ChildRules("qualifier")
                             : [])
                {
                    if (qualifier.ChildRules("indexQualifier").SingleOrDefault() is not { } indexQualifier)
                    {
                        continue;
                    }

                    foreach (var indexSyntax in indexQualifier.ChildRules()
                                 .Where(index => index.Role is "index1" or "index2"))
                    {
                        var expression = GetExpression(indexSyntax.RequiredChild("index")
                            .RequiredChild("numericExpression"));
                        if (expression.Type.Kind == ExpressExpressionTypeKind.Number)
                        {
                            _requiresNumberAssignmentIndex = true;
                            continue;
                        }

                        if (expression.Type.Kind == ExpressExpressionTypeKind.Integer)
                        {
                            _requiresIntegerAssignmentIndex |= expression.Type.CanBeIndeterminate;
                            continue;
                        }

                        AddFailure(
                            expression.Span,
                            "Assignment index requires an integer value; received "
                                + $"{expression.Type.Kind.ToString()}.");
                        valid = false;
                    }
                }

                IEnumerable<(string Name, ExpressSemanticRule Control)> controls = operation.Role switch
                {
                    "ifStmt" => [("IF", operation.RequiredChild("logicalExpression")),],
                    "repeatStmt" => operation.RequiredChild("repeatControl")
                        .ChildRules()
                        .Where(control => control.Role is "whileControl" or "untilControl")
                        .Select(control => (
                            control.Role == "whileControl" ? "WHILE" : "UNTIL",
                            control.RequiredChild("logicalExpression"))),
                    _ => [],
                };
                foreach (var (name, control) in controls)
                {
                    var expression = GetExpression(control.RequiredChild("expression"));
                    if (expression.Type.Kind is ExpressExpressionTypeKind.Boolean
                        or ExpressExpressionTypeKind.Logical
                        or ExpressExpressionTypeKind.Indeterminate)
                    {
                        continue;
                    }

                    AddFailure(
                        expression.Span,
                        $"{name} control requires a BOOLEAN or LOGICAL value; received "
                            + $"{expression.Type.Kind.ToString()}.");
                    valid = false;
                }

                if (operation.Role != "repeatStmt"
                    || operation.RequiredChild("repeatControl")
                        .ChildRules("incrementControl")
                        .SingleOrDefault() is not { } increment)
                {
                    continue;
                }

                IEnumerable<(string Name, ExpressSemanticRule Control)> numericControls =
                new (string Name, ExpressSemanticRule Control)[]
                {
                    ("REPEAT lower bound", increment.RequiredChild("bound1").RequiredChild("numericExpression")),
                    ("REPEAT upper bound", increment.RequiredChild("bound2").RequiredChild("numericExpression")),
                };
                if (increment.ChildRules("increment").SingleOrDefault() is { } step)
                {
                    numericControls = numericControls.Append(
                        ("REPEAT increment", step.RequiredChild("numericExpression")));
                }

                foreach (var (name, control) in numericControls)
                {
                    var expression = GetExpression(control);
                    if (expression.Type.Kind is ExpressExpressionTypeKind.Integer
                        or ExpressExpressionTypeKind.Real
                        or ExpressExpressionTypeKind.Number
                        or ExpressExpressionTypeKind.Indeterminate)
                    {
                        continue;
                    }

                    AddFailure(
                        expression.Span,
                        $"{name} requires a numeric value; received {expression.Type.Kind.ToString()}.");
                    valid = false;
                }
            }

            return valid;
        }

        private bool ValidateAssignmentRanges(ExpressSemanticRule operation)
        {
            if (operation.Role != "assignmentStmt")
            {
                return true;
            }

            var ranges = operation.ChildRules("qualifier")
                .SelectMany(qualifier => qualifier.ChildRules("indexQualifier"))
                .Where(index => index.ChildRules("index2").Any())
                .ToArray();
            if (ranges.Length == 0)
            {
                return true;
            }

            var targetSyntax = operation.RequiredChild("generalRef");
            var targetType = _schema.NameReferences
                .Where(reference => SameStart(reference.Span, targetSyntax.Span))
                .Select(reference => reference.Target.Type)
                .SingleOrDefault(type => type is not null);
            if (ranges.Length == 1
                && operation.ChildRules("qualifier").Count() == 1
                && targetType is ExpressBoundScalarType
                { Kind: ExpressScalarKind.String or ExpressScalarKind.Binary, })
            {
                return true;
            }

            AddFailure(
                ranges[0].Span,
                "A range-qualified assignment requires a STRING or BINARY carrier "
                    + "and shall be the final qualifier.");
            return false;
        }

        private bool ValidateProcedureScalarArguments(ExpressSemanticRule operation)
        {
            if (operation.Role != "procedureCallStmt"
                || operation.ChildRules("procedureRef").SingleOrDefault() is not { } procedureReference)
            {
                return true;
            }

            var procedure = _schema.NameReferences
                .Where(reference => SameStart(reference.Span, procedureReference.Span))
                .Select(reference => reference.Target.SchemaDeclaration)
                .OfType<ExpressBoundSymbol>()
                .Distinct()
                .SingleOrDefault();
            if (procedure is null
                || !_declarations.TryGetValue(procedure, out var declaration)
                || declaration.Kind != ExpressDeclarationKind.Procedure)
            {
                return true;
            }

            var parameterSyntax = _analysis.GetDeclaration(declaration).RequiredChild("procedureHead")
                .ChildRules("formalParameter")
                .SelectMany(formal => formal.ChildRules("parameterId"))
                .ToArray();
            var arguments = operation.ChildRules("actualParameterList")
                .SelectMany(parameters => parameters.ChildRules("parameter"))
                .Select(parameter => GetExpression(parameter.RequiredChild("expression")))
                .ToArray();
            if (parameterSyntax.Length != arguments.Length)
            {
                return true;
            }

            var valid = true;
            for (var index = 0; index < arguments.Length; index++)
            {
                var parameter = _schema.LexicalNames
                    .Where(candidate => candidate.Kind == ExpressBoundNameKind.Parameter
                        && SameStart(candidate.Span, parameterSyntax[index].Span))
                    .Distinct()
                    .Single();
                if (parameter.Type is not ExpressBoundScalarType formal
                    || IsScalarAssignmentCompatible(formal.Kind, arguments[index].Type.Kind))
                {
                    continue;
                }

                AddFailure(
                    arguments[index].Span,
                    "Procedure argument "
                        + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + $" of '{procedure.Name}' is not assignment-compatible: "
                        + $"{arguments[index].Type.Kind.ToString()} cannot be assigned to {formal.Kind.ToString()}.");
                valid = false;
            }

            return valid;
        }

        private static bool IsScalarAssignmentCompatible(
            ExpressScalarKind formal,
            ExpressExpressionTypeKind actual)
        {
            return actual is ExpressExpressionTypeKind.Indeterminate
                    or ExpressExpressionTypeKind.Select
                    or ExpressExpressionTypeKind.Generic
                    or ExpressExpressionTypeKind.Defined
                    or ExpressExpressionTypeKind.Unresolved
                || formal switch
                {
                    ExpressScalarKind.Binary => actual == ExpressExpressionTypeKind.Binary,
                    ExpressScalarKind.Boolean => actual is ExpressExpressionTypeKind.Boolean
                        or ExpressExpressionTypeKind.Logical,
                    ExpressScalarKind.Integer => actual == ExpressExpressionTypeKind.Integer,
                    ExpressScalarKind.Logical => actual is ExpressExpressionTypeKind.Boolean
                        or ExpressExpressionTypeKind.Logical,
                    ExpressScalarKind.Number => actual is ExpressExpressionTypeKind.Integer
                        or ExpressExpressionTypeKind.Real
                        or ExpressExpressionTypeKind.Number,
                    ExpressScalarKind.Real => actual is ExpressExpressionTypeKind.Integer
                        or ExpressExpressionTypeKind.Real,
                    ExpressScalarKind.String => actual == ExpressExpressionTypeKind.String,
                    _ => true,
                };
        }

        private static IEnumerable<ExpressSemanticRule> AlgorithmOperations(ExpressSemanticRule rule)
        {
            foreach (var child in rule.ChildRules())
            {
                if (child.Role is "functionDecl" or "procedureDecl")
                {
                    continue;
                }

                if (child.Role == "stmt")
                {
                    var operation = child.ChildRules().Single();
                    yield return operation;
                    foreach (var nestedOperation in AlgorithmOperations(operation))
                    {
                        yield return nestedOperation;
                    }

                    continue;
                }

                foreach (var operation in AlgorithmOperations(child))
                {
                    yield return operation;
                }
            }
        }

        private static bool HasValidLoopTransfers(ExpressSemanticRule rule, bool insideRepeat)
        {
            if (rule.Role is "escapeStmt" or "skipStmt")
            {
                return insideRepeat;
            }

            if (rule.Role is "functionDecl" or "procedureDecl")
            {
                insideRepeat = false;
            }

            return rule.ChildRules().All(child => HasValidLoopTransfers(
                child, insideRepeat || rule.Role == "repeatStmt"));
        }

        private bool IsSupportedListProcedure(ExpressSemanticRule operation)
        {
            var name = operation.ChildRules("builtInProcedure").SingleOrDefault()?.SourceText.ToUpperInvariant();
            var arguments = operation.ChildRules("actualParameterList")
                .SelectMany(parameters => parameters.ChildRules("parameter"))
                .Select(parameter => GetExpression(parameter.RequiredChild("expression")))
                .ToArray();
            return name is "INSERT" or "REMOVE"
                && arguments.Length == (name == "INSERT" ? 3 : 2)
                && arguments[0].Kind == ExpressExpressionKind.Reference
                && arguments[0].Reference is
                {
                    Kind: ExpressBoundNameKind.Variable or ExpressBoundNameKind.Parameter,
                    Type: { } targetType,
                }

                && _resolver.GetAggregateType(targetType) is { Kind: ExpressAggregateKind.List, }
                && arguments[arguments.Length - 1].Type.Kind is ExpressExpressionTypeKind.Integer
                    or ExpressExpressionTypeKind.Number;
        }

        private bool IsSupportedProcedureCall(ExpressSemanticRule operation)
        {
            if (operation.ChildRules("procedureRef").SingleOrDefault() is not { } procedureReference)
            {
                return false;
            }

            var procedure = _schema.NameReferences
                .Where(reference => SameStart(reference.Span, procedureReference.Span))
                .Select(reference => reference.Target.SchemaDeclaration)
                .OfType<ExpressBoundSymbol>()
                .Distinct()
                .SingleOrDefault();
            if (procedure is null
                || !_declarations.TryGetValue(procedure, out var declaration)
                || declaration.Kind != ExpressDeclarationKind.Procedure)
            {
                return false;
            }

            var expected = _analysis.GetDeclaration(declaration).RequiredChild("procedureHead")
                .ChildRules("formalParameter")
                .Sum(formal => formal.ChildRules("parameterId").Count());
            var actual = operation.ChildRules("actualParameterList")
                .SelectMany(parameters => parameters.ChildRules("parameter"))
                .Count();
            return expected == actual;
        }

        private bool HasLexicalCapture(
            ExpressBoundDeclaration declaration,
            ExpressSemanticRule declarationRule)
        {
            return _schema.NestedDeclarations.Contains(declaration)
                && _schema.NameReferences.Any(reference =>
                    Contains(declarationRule.Span, reference.Span)
                    && (reference.Target.Kind is ExpressBoundNameKind.Parameter
                            or ExpressBoundNameKind.Variable
                            or ExpressBoundNameKind.RepeatVariable
                        || (reference.Target.Kind == ExpressBoundNameKind.Constant
                            && reference.Target.SchemaDeclaration is null))
                    && !Contains(declarationRule.Span, reference.Target.Span));
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

        private bool IsFunctionReturnExpression(
            ExpressBoundOpaqueDeclaration declaration,
            ExpressBoundExpression expression)
        {
            return _analysis.GetDeclaration(declaration).DescendantsAndSelf()
                .Where(candidate => candidate.Role == "returnStmt")
                .SelectMany(candidate => candidate.ChildRules("expression"))
                .Any(candidate => SameSpan(candidate.Span, expression.Span));
        }

        private bool IsConstantValueExpression(
            ExpressBoundOpaqueDeclaration declaration,
            ExpressBoundExpression expression)
        {
            return _analysis.GetDeclaration(declaration).ChildRules("expression")
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
            var declarationRule = _analysis.GetDeclaration(declaration);
            if (!declarationRule.RequiredChild("algorithmHead").ChildRules().Any()
                && !declarationRule.ChildRules("stmt").Any())
            {
                return;
            }

            var operations = AlgorithmOperations(declarationRule).ToArray();
            if (!ValidateControlAndIndexTypes(operations))
            {
                return;
            }

            if (operations.All(operation =>
                    operation.Role is "assignmentStmt" or "caseStmt" or "compoundStmt" or "ifStmt"
                        or "repeatStmt" or "escapeStmt" or "skipStmt" or "aliasStmt" or "nullStmt"
                    || (operation.Role == "procedureCallStmt"
                        && (IsSupportedListProcedure(operation) || IsSupportedProcedureCall(operation))))
                && (!operations.Any(operation => operation.Role is "escapeStmt" or "skipStmt")
                    || HasValidLoopTransfers(declarationRule, false))
                && !operations.Any(operation => operation.Role == "returnStmt")
                && operations.Where(operation => operation.Role == "aliasStmt")
                    .All(alias => alias.ChildRules("qualifier").All(qualifier =>
                        qualifier.ChildRules().Single().Role is "attributeQualifier" or "groupQualifier"))
                && operations.Where(operation => operation.Role == "assignmentStmt")
                    .All(assignment => assignment.ChildRules("qualifier").All(qualifier =>
                    {
                        var qualifierOperation = qualifier.ChildRules().Single();
                        return qualifierOperation.Role == "indexQualifier";
                    }))
                && operations.Where(operation => operation.Role == "repeatStmt")
                    .All(repeat => repeat.RequiredChild("repeatControl") is { } control
                        && control.ChildRules("incrementControl").Count() <= 1
                        && control.ChildRules("whileControl").Count() <= 1
                        && control.ChildRules("untilControl").Count() <= 1)
                && operations.Where(operation => operation.Role == "caseStmt")
                    .All(caseStatement => caseStatement.ChildRules("caseAction")
                            .All(action => action.ChildRules("caseLabel").Any()
                                && action.ChildRules("stmt").Count() == 1)
                        && caseStatement.ChildRules("stmt").Count() <= 1)
                && !declarationRule.RequiredChild("algorithmHead")
                    .ChildRules()
                    .Any(child => child.Role == "declaration"
                        && child.ChildRules().Single().Role is not ("functionDecl" or "procedureDecl")))
            {
                return;
            }

            _failures.Add(new(
                _schema,
                declarationRule.Span.Start,
                $"Validation-root EXPRESS RULE '{declaration.Name}' uses an algorithm statement shape that has no static generator."));
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