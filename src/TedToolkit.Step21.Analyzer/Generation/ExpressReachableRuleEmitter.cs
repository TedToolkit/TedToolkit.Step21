// -----------------------------------------------------------------------
// <copyright file="ExpressReachableRuleEmitter.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;

using TedToolkit.RoslynHelper;
using TedToolkit.RoslynHelper.Syntaxes;

using TedToolkit.Step21.Analyzer.Express;
using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Emits the validated private constant, function, and derived-attribute rule closure.
/// </summary>
internal static class ExpressReachableRuleEmitter
{
    private const string ACTIVE_PAIR_LIST =
        "new global::System.Collections.Generic.List<global::System.Collections.Generic.KeyValuePair<"
        + "global::TedToolkit.Step21.Entity, global::TedToolkit.Step21.Entity>>()";

    private const string POPULATION_TYPE =
        "global::System.Collections.Generic.IReadOnlyList<global::System.Collections.Generic.KeyValuePair<global::System.String, global::TedToolkit.Step21.Entity>>";

    /// <summary>
    /// Creates private static helpers for every reachable declaration dependency.
    /// </summary>
    /// <param name="plan">The validated reachability plan.</param>
    /// <param name="resolver">The generated value-type resolver.</param>
    /// <param name="entities">The generated entity projections available to private model operations.</param>
    /// <param name="complexEntities">The generated complex entity projections available to value equality.</param>
    /// <returns>The helper methods in deterministic declaration order.</returns>
    /// <exception cref="InvalidOperationException">A reachable plan contains a non-executable declaration kind.</exception>
    internal static IReadOnlyList<Method> CreateDependencyMethods(
        ExpressReachableRulePlan plan,
        ExpressGeneratedTypeResolver resolver,
        IReadOnlyList<ExpressEntityProjection> entities,
        IReadOnlyList<ExpressComplexEntityProjection> complexEntities)
    {
        var result = new List<Method>();
        foreach (var declaration in plan.ReachableDeclarations)
        {
            if (declaration is not ExpressBoundOpaqueDeclaration opaque
                || opaque.DeclaredType is null)
            {
                continue;
            }

            result.Add(declaration.Kind switch
            {
                ExpressDeclarationKind.Constant => CreateConstantMethod(plan, opaque, resolver),
                ExpressDeclarationKind.Function => CreateFunctionMethod(plan, opaque, resolver),
                _ => throw new InvalidOperationException(
                    $"Reachable declaration '{declaration.Name}' is not a constant or function."),
            });
        }

        result.AddRange(plan.ReachableDerivedAttributes.Select(attribute =>
            CreateDerivedMethod(plan, attribute, resolver)));
        result.AddRange(CreateModelMethods(plan, resolver, entities));
        if (plan.RequiresEntityValueEquality)
        {
            result.Add(CreateEntityValueEqualsMethod(plan, resolver, entities, complexEntities));
            result.Add(CreateOrderedValueEqualsMethod());
            result.Add(CreateArrayValueEqualsMethod());
            result.Add(CreateUnorderedValueEqualsMethod());
            result.Add(CreateHasPerfectMatchMethod());
            result.Add(CreateTryMatchMethod());
        }

        return result;
    }

    /// <summary>
    /// Creates an expression-emission context for a generated validation or helper method.
    /// </summary>
    /// <param name="plan">The validated reachability plan.</param>
    /// <param name="selfExpression">The enclosing EXPRESS SELF expression.</param>
    /// <param name="populationExpression">The generated spelling for the current validation population.</param>
    /// <param name="lexicalNames">Generated spellings for formal or local names.</param>
    /// <param name="safeIndices">Aggregate and REPEAT-variable pairs whose index access is proven present.</param>
    /// <param name="allocateTemporaryName">Allocates deterministic private names within the generated method.</param>
    /// <param name="selectNarrowings">Maps lexical SELECT values to branch-proven entity alternatives.</param>
    /// <param name="pathNarrowings">Maps qualified reference paths to branch-proven entity alternatives.</param>
    /// <param name="determinateLexicals">Identifies branch-proven determinate lexical values.</param>
    /// <param name="safeIndexPaths">Qualified aggregate paths proven present at a REPEAT index.</param>
    /// <param name="scalarNarrowings">Branch-local scalar projections over stable lexical storage.</param>
    /// <returns>The immutable emission context.</returns>
    internal static ExpressExpressionEmissionContext CreateContext(
        ExpressReachableRulePlan plan,
        string? selfExpression,
        string populationExpression,
        IReadOnlyDictionary<string, (string Code, ExpressBoundType Type)>? lexicalNames = null,
        IReadOnlyCollection<KeyValuePair<ExpressBoundName, ExpressBoundName>>? safeIndices = null,
        Func<string, string>? allocateTemporaryName = null,
        IReadOnlyDictionary<ExpressBoundName, ExpressBoundSymbol>? selectNarrowings = null,
        IReadOnlyList<KeyValuePair<ExpressBoundExpression, ExpressBoundSymbol>>? pathNarrowings = null,
        ISet<ExpressBoundName>? determinateLexicals = null,
        IReadOnlyList<KeyValuePair<ExpressBoundExpression, ExpressBoundName>>? safeIndexPaths = null,
        IReadOnlyDictionary<string, (string StorageCode, string Code, ExpressBoundType Type)>?
            scalarNarrowings = null)
    {
        string ResolveBoundReference(
            ExpressBoundName reference,
            ExpressBoundType? narrowedType,
            string? narrowedCode,
            ExpressBoundSymbol? narrowedAlternative)
        {
            if (scalarNarrowings?.TryGetValue(reference.Name, out var scalarNarrowing) == true
                && lexicalNames?.TryGetValue(reference.Name, out var scalarStorage) == true
                && string.Equals(
                    scalarStorage.Code,
                    scalarNarrowing.StorageCode,
                    StringComparison.Ordinal))
            {
                return scalarNarrowing.Code;
            }

            if (reference.Kind == ExpressBoundNameKind.Variable
                && reference.Type is ExpressBoundScalarType scalarType
                && scalarType.Kind is ExpressScalarKind.Integer or ExpressScalarKind.Number or ExpressScalarKind.Real
                && plan.Schema.IndeterminateLocals.Contains(reference)
                && determinateLexicals?.Contains(reference) == true
                && lexicalNames?.TryGetValue(reference.Name, out var determinateLexical) == true)
            {
                return $"({determinateLexical.Code}).Value";
            }

            return ResolveReference(
                plan,
                reference,
                selfExpression,
                populationExpression,
                lexicalNames,
                selectNarrowings,
                narrowedType,
                narrowedCode,
                narrowedAlternative);
        }

        (string Code, ExpressBoundType Type)? ResolveLexicalBound(string name)
        {
            if (scalarNarrowings?.TryGetValue(name, out var scalarNarrowing) == true
                && lexicalNames?.TryGetValue(name, out var scalarStorage) == true
                && string.Equals(
                    scalarStorage.Code,
                    scalarNarrowing.StorageCode,
                    StringComparison.Ordinal))
            {
                return (scalarNarrowing.Code, scalarNarrowing.Type);
            }

            return lexicalNames is not null && lexicalNames.TryGetValue(name, out var lexical)
                ? lexical
                : null;
        }

        return new(
            ResolveBoundReference,
            selfExpression,
            resolveModelFunction: (operation, expression, arguments) =>
                ResolveModelFunction(plan, operation, expression, arguments, populationExpression),
            resolveValueEquality: (left, leftCode, right, rightCode, leftTypeOverride) =>
                ResolveValueEquality(plan, left, leftCode, right, rightCode, leftTypeOverride),
            resolveAttribute: (sourceExpression, reference, source) =>
                ResolveAttribute(
                    plan,
                    sourceExpression,
                    reference,
                    source,
                    populationExpression,
                    selectNarrowings),
            resolveApplication: (expression, arguments) =>
                ResolveApplication(
                    plan,
                    expression,
                    arguments,
                    populationExpression,
                    selfExpression,
                    lexicalNames,
                    selectNarrowings,
                    pathNarrowings),
            safeIndices: safeIndices,
            resolveLexicalBound: ResolveLexicalBound,
            genericTypeLabels: ExpressExpressionEmitter.GenericTypeLabels(
                lexicalNames?.Values.Select(lexical => lexical.Type) ?? []),
            allocateTemporaryName: allocateTemporaryName,
            isKnownDeterminate: expression => (expression.Kind == ExpressExpressionKind.Reference
                && expression.Reference is { } lexicalReference
                && determinateLexicals?.Contains(lexicalReference) == true)
                || (expression.Kind == ExpressExpressionKind.IndexQualifier
                    && expression.Children.Count >= 2
                    && expression.Children[1].Reference is { } indexReference
                    && safeIndexPaths?.Any(pair => ReferenceEquals(pair.Value, indexReference)
                        && SameDirectReferencePath(pair.Key, expression.Children[0])) == true)
                || (expression.Kind == ExpressExpressionKind.AttributeQualifier
                && expression.Reference is { IsOptional: false, } attributeReference
                && attributeReference.AttributeCandidates.All(attribute =>
                    attribute.Kind == ExpressAttributeKind.Explicit)
                && ((expression.Children[0].Reference is { } sourceReference
                    && selectNarrowings?.TryGetValue(sourceReference, out var alternative) == true
                    && alternative.Kind == ExpressDeclarationKind.Entity
                    && plan.EntityProjections.Single(projection => projection.Entity.Symbol == alternative)
                        .PhysicalComponents.Any(component =>
                            attributeReference.AttributeCandidates.Any(attribute =>
                                ReferenceEquals(component, plan.GetAttributeOwner(attribute)))))
                || (expression.Children[0].Kind == ExpressExpressionKind.GroupQualifier
                    && expression.Children[0].Children[0].Reference is { } groupedSourceReference
                    && selectNarrowings?.TryGetValue(groupedSourceReference, out var groupedAlternative) == true
                    && groupedAlternative.Kind == ExpressDeclarationKind.Entity
                    && plan.EntityProjections.Single(projection =>
                            projection.Entity.Symbol == groupedAlternative)
                        .PhysicalComponents.Any(component =>
                            attributeReference.AttributeCandidates.Any(attribute =>
                                ReferenceEquals(component, plan.GetAttributeOwner(attribute)))))))
                || (expression.Kind == ExpressExpressionKind.GroupQualifier
                && expression.Reference?.SchemaDeclaration is { } group
                && expression.Children[0].Reference is { } groupSourceReference
                && selectNarrowings?.TryGetValue(groupSourceReference, out var groupAlternative) == true
                && plan.EntityProjections.Single(projection =>
                        projection.Entity.Symbol == groupAlternative)
                    .PhysicalComponents.Any(component => component.Symbol == group)));
    }

    private static string ResolveValueEquality(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression left,
        string leftCode,
        ExpressBoundExpression right,
        string rightCode,
        ExpressBoundType? leftTypeOverride,
        bool instanceEquality = false)
    {
        if (leftTypeOverride is null
            && left.Type.DeclaredType is ExpressBoundAggregateType leftAggregate)
        {
            return CreateBoundValueEquality(
                plan,
                plan.Resolver,
                leftAggregate,
                leftCode,
                rightCode,
                ACTIVE_PAIR_LIST);
        }

        if (leftTypeOverride is null && left.Type.Kind != ExpressExpressionTypeKind.Select)
        {
            if (right.Type.Kind == ExpressExpressionTypeKind.Select)
            {
                return ResolveValueEquality(
                    plan,
                    right,
                    rightCode,
                    left,
                    leftCode,
                    null,
                    instanceEquality);
            }

            return instanceEquality
                ? $"global::System.Object.ReferenceEquals(({leftCode}), ({rightCode}))"
                : "__ExpressEntityValueEquals("
                    + $"(global::TedToolkit.Step21.Entity)({leftCode}), "
                    + $"(global::TedToolkit.Step21.Entity)({rightCode}), "
                    + ACTIVE_PAIR_LIST
                    + ")";
        }

        ExpressBoundType? selectType = leftTypeOverride ?? left.Type.DeclaredType;
        while (selectType is ExpressBoundNamedType namedSelect)
        {
            selectType = plan.Resolver.GetDefinedType(namedSelect.Declaration).UnderlyingType;
        }

        if (selectType is not ExpressBoundSelectType select)
        {
            throw new InvalidOperationException(
                "SELECT value equality requires a statically resolved generated SELECT type.");
        }

        if (right.Type.Kind == ExpressExpressionTypeKind.Select)
        {
            return CreateSelectValueEquality(
                plan,
                plan.Resolver,
                select,
                leftCode,
                rightCode,
                ACTIVE_PAIR_LIST,
                instanceEquality,
                right.Type.DeclaredType);
        }

        ExpressBoundSymbol? rightNominal = right.Type.DeclaredType is ExpressBoundNamedType namedRight
            ? namedRight.Declaration
            : null;
        while (rightNominal is not null
               && rightNominal.Kind != ExpressDeclarationKind.Entity
               && plan.Resolver.GetDefinedType(rightNominal).UnderlyingType is ExpressBoundNamedType nestedRight)
        {
            rightNominal = nestedRight.Declaration;
        }

        var branches = new List<string>();
        var alternatives = plan.Resolver.GetSelectAlternatives(select);
        for (var index = 0; index < alternatives.Count; index++)
        {
            var alternative = alternatives[index];
            var variable = "__expressSelected_"
                + left.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                + "_"
                + left.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                + "_"
                + index.ToString(CultureInfo.InvariantCulture);
            var value = variable;
            var selected = alternative;
            while (selected.Kind != ExpressDeclarationKind.Entity
                   && plan.Resolver.GetDefinedType(selected).UnderlyingType is ExpressBoundNamedType nested)
            {
                value = $"({value}).Value";
                selected = nested.Declaration;
            }

            string equality;
            var selectedType = selected.Kind == ExpressDeclarationKind.Entity
                ? null
                : plan.Resolver.GetDefinedType(selected).UnderlyingType;
            if (selectedType is ExpressBoundSelectType nestedSelect)
            {
                equality = CreateSelectValueEquality(
                    plan,
                    plan.Resolver,
                    nestedSelect,
                    value,
                    rightCode,
                    ACTIVE_PAIR_LIST,
                    instanceEquality,
                    right.Type.DeclaredType);
            }
            else if (selected.Kind == ExpressDeclarationKind.Entity)
            {
                if (right.Type.Kind != ExpressExpressionTypeKind.Entity)
                {
                    equality = instanceEquality
                        ? "false"
                        : "global::TedToolkit.Step21.LogicalValue.False";
                }
                else if (instanceEquality)
                {
                    equality = $"global::System.Object.ReferenceEquals(({value}), ({rightCode}))";
                }
                else
                {
                    equality = "__ExpressEntityValueEquals("
                        + $"(global::TedToolkit.Step21.Entity)({value}), "
                        + $"(global::TedToolkit.Step21.Entity)({rightCode}), "
                        + "new global::System.Collections.Generic.List<"
                        + "global::System.Collections.Generic.KeyValuePair<"
                        + "global::TedToolkit.Step21.Entity, global::TedToolkit.Step21.Entity>>())";
                }
            }
            else if (instanceEquality)
            {
                equality = "false";
            }
            else
            {
                var underlying = selectedType!;
                var booleanEquality = underlying switch
                {
                    ExpressBoundScalarType scalar when scalar.Kind switch
                    {
                        ExpressScalarKind.Binary => right.Type.Kind == ExpressExpressionTypeKind.Binary,
                        ExpressScalarKind.Boolean => right.Type.Kind == ExpressExpressionTypeKind.Boolean,
                        ExpressScalarKind.Integer => right.Type.Kind == ExpressExpressionTypeKind.Integer,
                        ExpressScalarKind.Logical => right.Type.Kind == ExpressExpressionTypeKind.Logical,
                        ExpressScalarKind.Number => right.Type.Kind == ExpressExpressionTypeKind.Number,
                        ExpressScalarKind.Real => right.Type.Kind == ExpressExpressionTypeKind.Real,
                        ExpressScalarKind.String => right.Type.Kind == ExpressExpressionTypeKind.String,
                        _ => false,
                    }

                        => ExpressExpressionEmitter.ValueEqualityCore(
                        right,
                        $"({value}).Value",
                        right,
                        rightCode),
                    ExpressBoundEnumerationType when ReferenceEquals(selected, rightNominal) =>
                        $"global::System.StringComparer.Ordinal.Equals(({value}).Value, ({rightCode}).Value)",
                    _ => "false",
                };
                equality = $"(({booleanEquality}) ? global::TedToolkit.Step21.LogicalValue.True : "
                    + "global::TedToolkit.Step21.LogicalValue.False)";
            }

            branches.Add($"{variable} => {equality}");
        }

        return $"({leftCode}).Match({string.Join(", ", branches)})";
    }

    private static Method CreateConstantMethod(
        ExpressReachableRulePlan plan,
        ExpressBoundOpaqueDeclaration declaration,
        ExpressGeneratedTypeResolver resolver)
    {
        var method = CreateMethod(
            ConstantMethodName(declaration.Symbol),
            resolver.Resolve(plan.Schema.Identity, declaration.DeclaredType!).DataType);
        var temporaryOrdinal = 0;
        Func<string, string> allocateTemporaryName = prefix => prefix
            + (temporaryOrdinal++).ToString(CultureInfo.InvariantCulture);
        AddPopulationParameter(method);
        var expression = plan.GetExpression(declaration.Syntax.RequiredChild("expression"));
        var generated = ExpressExpressionEmitter.Emit(
            expression,
            CreateContext(
                plan,
                selfExpression: null,
                "entities",
                allocateTemporaryName: allocateTemporaryName));
        method.AddStatement(new CustomExpression(generated.Code).Return);
        AddSummary(method, $"Evaluates reachable EXPRESS constant {declaration.Name}.");
        return method;
    }

    private static Method CreateFunctionMethod(
        ExpressReachableRulePlan plan,
        ExpressBoundOpaqueDeclaration declaration,
        ExpressGeneratedTypeResolver resolver)
    {
        var resultGenericLabels = ExpressExpressionEmitter.GenericTypeLabels([declaration.DeclaredType!,]);
        var returnType = resultGenericLabels.Count == 0
            ? resolver.Resolve(plan.Schema.Identity, declaration.DeclaredType!).DataType
            : new DataType(ExpressExpressionEmitter.BoundTypeName(declaration.DeclaredType!));
        var canReturnIndeterminate = plan.Schema.IndeterminateFunctions.Contains(declaration.Symbol);
        if (canReturnIndeterminate)
        {
            returnType = returnType.Null;
        }

        var method = CreateMethod(
            FunctionMethodName(declaration.Symbol),
            returnType);
        var temporaryOrdinal = 0;
        Func<string, string> allocateTemporaryName = prefix => prefix
            + (temporaryOrdinal++).ToString(CultureInfo.InvariantCulture);
        var lexicalNames = new Dictionary<string, (string Code, ExpressBoundType Type)>(
            StringComparer.OrdinalIgnoreCase);
        var formalTypes = new List<ExpressBoundType>();
        var head = declaration.Syntax.RequiredChild("functionHead");
        foreach (var formal in head.ChildRules("formalParameter"))
        {
            foreach (var parameter in formal.ChildRules("parameterId"))
            {
                var name = parameter.IdentifierToken().Text;
                var boundName = plan.Schema.NameReferences
                    .Select(reference => reference.Target)
                    .Where(candidate => candidate.Kind == ExpressBoundNameKind.Parameter
                        && SameStart(candidate.Span, parameter.Span))
                    .Distinct()
                    .Single();
                var generatedName = ParameterName(name);
                lexicalNames.Add(name, (generatedName, boundName.Type!));
                formalTypes.Add(boundName.Type!);
                var parameterType = resolver.IsSupported(boundName.Type!)
                    ? resolver.Resolve(plan.Schema.Identity, boundName.Type!).DataType
                    : new DataType(ExpressExpressionEmitter.BoundTypeName(boundName.Type!));
                method.AddParameter(SourceComposer.Parameter(parameterType, generatedName));
            }
        }

        AddPopulationParameter(method);
        var determinateLexicals = new HashSet<ExpressBoundName>();
        var localTypes = new List<ExpressBoundType>();
        var algorithmHead = declaration.Syntax.RequiredChild("algorithmHead");
        foreach (var local in algorithmHead.ChildRules("localDecl")
                     .SelectMany(localDeclaration => localDeclaration.ChildRules("localVariable")))
        {
            foreach (var variable in local.ChildRules("variableId"))
            {
                var boundName = plan.Schema.NameReferences
                    .Select(reference => reference.Target)
                    .Where(candidate => candidate.Kind == ExpressBoundNameKind.Variable
                        && SameStart(candidate.Span, variable.Span))
                    .Distinct()
                    .SingleOrDefault();
                if (boundName?.Type is null)
                {
                    continue;
                }

                var generatedName = $"__local_{ExpressEntityProjection.ToPascalCase(boundName.Name)}";
                lexicalNames.Add(boundName.Name, (generatedName, boundName.Type));
                localTypes.Add(boundName.Type);
                var variableType = new DataType(
                    ExpressExpressionEmitter.BoundTypeName(boundName.Type)
                    + (plan.Schema.IndeterminateLocals.Contains(boundName) ? "?" : ""));
                var variableExpression = new VariableExpression(variableType, generatedName);
                if (local.ChildRules("expression").SingleOrDefault() is { } initializer)
                {
                    var initializerExpression = plan.GetExpression(initializer);
                    var generated = ExpressExpressionEmitter.Emit(
                        initializerExpression,
                        CreateContext(
                            plan,
                            selfExpression: null,
                            "entities",
                            lexicalNames,
                            allocateTemporaryName: allocateTemporaryName));
                    variableExpression.AddDefault(new CustomExpression(generated.Code));
                    if (!boundName.IsOptional && !initializerExpression.Type.CanBeIndeterminate)
                    {
                        determinateLexicals.Add(boundName);
                    }
                }
                else if (plan.Schema.IndeterminateLocals.Contains(boundName))
                {
                    variableExpression.AddDefault(new CustomExpression("null"));
                }

                method.AddStatement(variableExpression);
            }
        }

        foreach (var label in ExpressExpressionEmitter.GenericTypeLabels(
                     formalTypes.Concat([declaration.DeclaredType!,]).Concat(localTypes)))
        {
            method.AddTypeParameter(SourceComposer.TypeParameter(
                "T" + ExpressEntityProjection.ToPascalCase(label)));
        }

        var sizeAliases = new Dictionary<ExpressBoundName, ExpressBoundName>();
        var scalarNarrowings = new Dictionary<
            string,
            (string StorageCode, string Code, ExpressBoundType Type)>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var statement in declaration.Syntax.ChildRules("stmt"))
        {
            if (!EmitFunctionStatement(
                plan,
                statement,
                declaration.DeclaredType!,
                canReturnIndeterminate,
                method,
                lexicalNames,
                allocateTemporaryName,
                sizeAliases: sizeAliases,
                determinateLexicals: determinateLexicals,
                scalarNarrowings: scalarNarrowings))
            {
                break;
            }
        }

        AddSummary(method, $"Evaluates reachable EXPRESS function {declaration.Name}.");
        return method;
    }

    private static bool EmitFunctionStatement(
        ExpressReachableRulePlan plan,
        ExpressRuleSyntax statement,
        ExpressBoundType functionResultType,
        bool canReturnIndeterminate,
        IStatementOwner owner,
        Dictionary<string, (string Code, ExpressBoundType Type)> lexicalNames,
        Func<string, string> allocateTemporaryName,
        List<KeyValuePair<ExpressBoundName, ExpressBoundName>>? safeIndices = null,
        IDictionary<ExpressBoundName, ExpressBoundName>? sizeAliases = null,
        Dictionary<ExpressBoundName, ExpressBoundSymbol>? selectNarrowings = null,
        List<KeyValuePair<ExpressBoundExpression, ExpressBoundSymbol>>? pathNarrowings = null,
        ISet<ExpressBoundName>? determinateLexicals = null,
        List<KeyValuePair<ExpressBoundExpression, ExpressBoundName>>? safeIndexPaths = null,
        Dictionary<string, (string StorageCode, string Code, ExpressBoundType Type)>?
            scalarNarrowings = null)
    {
        var operation = statement.Production == "stmt"
            ? statement.ChildRules().Single()
            : statement;
        if (operation.Production == "assignmentStmt")
        {
            var targetSyntax = operation.RequiredChild("generalRef");
            var target = plan.Schema.NameReferences
                .Where(reference => SameStart(reference.Span, targetSyntax.Span))
                .Select(reference => reference.Target)
                .Single();
            var hasQualifier = operation.ChildRules("qualifier").Any();
            var targetWasDeterminate = !hasQualifier
                && determinateLexicals?.Contains(target) == true;
            var canEstablishDeterminacy = target.Type is ExpressBoundScalarType
                || targetWasDeterminate;
            var targetCode = lexicalNames[target.Name].Code;
            var targetType = target.Type!;
            var targetIsOptional = target.IsOptional;
            string? optionalUnsetCode = null;
            foreach (var qualifier in operation.ChildRules("qualifier"))
            {
                if (qualifier.ChildRules("attributeQualifier").SingleOrDefault() is { } attributeSyntax)
                {
                    var sourceEntity = (ExpressBoundNamedType)targetType;
                    var attribute = plan.GetReferencedAttribute(attributeSyntax);
                    var concreteType = "global::TedToolkit.Step21.Generated."
                        + ExpressEntityProjection.ToPascalCase(sourceEntity.Declaration.DeclaringSchema.Name)
                        + "."
                        + ExpressEntityProjection.ToPascalCase(sourceEntity.Declaration.Name);
                    targetCode = $"(({concreteType})({targetCode}))."
                        + ExpressEntityProjection.ToPascalCase(attribute.Name);
                    targetType = attribute.Type;
                    targetIsOptional = attribute.IsOptional;
                    optionalUnsetCode = null;
                    continue;
                }

                var indexSyntax = qualifier.RequiredChild("indexQualifier")
                    .RequiredChild("index1")
                    .RequiredChild("index")
                    .RequiredChild("numericExpression");
                var index = plan.GetExpression(indexSyntax);
                var emittedIndex = ExpressExpressionEmitter.Emit(
                    index,
                    CreateContext(
                        plan,
                        selfExpression: null,
                        "entities",
                        lexicalNames,
                        safeIndices,
                        allocateTemporaryName,
                        selectNarrowings,
                        pathNarrowings,
                        determinateLexicals,
                        safeIndexPaths,
                        scalarNarrowings));
                var indexCode = index.Type.Kind == ExpressExpressionTypeKind.Number
                    ? $"checked((int)({emittedIndex.Code}).ToIntegerTruncated())"
                    : $"checked((int)({emittedIndex.Code}))";
                var aggregate = (ExpressBoundAggregateType)targetType;
                var aggregateCode = targetCode;
                targetCode += aggregate.Kind == ExpressAggregateKind.Array
                    ? $"[{indexCode}]"
                    : $"[{indexCode} - 1]";
                targetType = aggregate.ElementType;
                targetIsOptional = aggregate.IsOptional;
                optionalUnsetCode = aggregate.Kind == ExpressAggregateKind.Array && aggregate.IsOptional
                    ? $"{aggregateCode}.Unset({indexCode})"
                    : null;
            }

            var valueExpression = plan.GetExpression(operation.RequiredChild("expression"));
            var valueIsKnownDeterminate = !valueExpression.Type.CanBeIndeterminate
                || (valueExpression.Kind == ExpressExpressionKind.Reference
                    && valueExpression.Reference is { } valueReference
                    && ((ReferenceEquals(valueReference, target) && targetWasDeterminate)
                        || determinateLexicals?.Contains(valueReference) == true));
            var value = ExpressExpressionEmitter.Emit(
                valueExpression,
                CreateContext(
                    plan,
                    selfExpression: null,
                    "entities",
                    lexicalNames,
                    safeIndices,
                    allocateTemporaryName,
                    selectNarrowings,
                    pathNarrowings,
                    determinateLexicals,
                    safeIndexPaths,
                    scalarNarrowings));
            safeIndexPaths?.Clear();
            if (!hasQualifier)
            {
                determinateLexicals?.Remove(target);
                scalarNarrowings?.Remove(target.Name);

                if (selectNarrowings?.ContainsKey(target) == true
                    && target.Type is ExpressBoundNamedType invalidatedSelect
                    && invalidatedSelect.Declaration.Kind != ExpressDeclarationKind.Entity
                    && plan.Resolver.GetDefinedType(invalidatedSelect.Declaration).UnderlyingType
                        is ExpressBoundSelectType)
                {
                    selectNarrowings[target] = invalidatedSelect.Declaration;
                }
                else
                {
                    selectNarrowings?.Remove(target);
                }

                safeIndices?.RemoveAll(pair => ReferenceEquals(pair.Key, target)
                    || ReferenceEquals(pair.Value, target));
            }

            if (sizeAliases is not null)
            {
                foreach (var alias in sizeAliases
                             .Where(alias => ReferenceEquals(alias.Key, target) || ReferenceEquals(alias.Value, target))
                             .ToArray())
                {
                    sizeAliases.Remove(alias.Key);
                }
            }

            if (hasQualifier)
            {
                if (pathNarrowings is not null)
                {
                    for (var index = pathNarrowings.Count - 1; index >= 0; index--)
                    {
                        var narrowing = pathNarrowings[index];
                        if (narrowing.Key.Type.DeclaredType is ExpressBoundNamedType invalidatedSelect
                            && invalidatedSelect.Declaration.Kind != ExpressDeclarationKind.Entity
                            && plan.Resolver.GetDefinedType(invalidatedSelect.Declaration).UnderlyingType
                                is ExpressBoundSelectType)
                        {
                            pathNarrowings[index] = new(narrowing.Key, invalidatedSelect.Declaration);
                        }
                        else
                        {
                            pathNarrowings.RemoveAt(index);
                        }
                    }
                }
            }
            else if (pathNarrowings is not null)
            {
                for (var index = pathNarrowings.Count - 1; index >= 0; index--)
                {
                    var narrowing = pathNarrowings[index];
                    if (!narrowing.Key.DescendantsAndSelf()
                        .Any(expression => ReferenceEquals(expression.Reference, target)))
                    {
                        continue;
                    }

                    if (narrowing.Key.Type.DeclaredType is ExpressBoundNamedType invalidatedSelect
                        && invalidatedSelect.Declaration.Kind != ExpressDeclarationKind.Entity
                        && plan.Resolver.GetDefinedType(invalidatedSelect.Declaration).UnderlyingType
                            is ExpressBoundSelectType)
                    {
                        pathNarrowings[index] = new(narrowing.Key, invalidatedSelect.Declaration);
                    }
                    else
                    {
                        pathNarrowings.RemoveAt(index);
                    }
                }
            }

            if (sizeAliases is not null
                && !operation.ChildRules("qualifier").Any()
                && target.Kind == ExpressBoundNameKind.Variable
                && valueExpression.Operation == "SIZEOF"
                && valueExpression.Children.Count == 1
                && valueExpression.Children[0].Reference is { Type: ExpressBoundAggregateType, } sourceAggregate)
            {
                sizeAliases.Clear();
                sizeAliases.Add(target, sourceAggregate);
            }

            var valueCode = value.Code;
            if (targetType is ExpressBoundAggregateType targetSet
                && targetSet.Kind == ExpressAggregateKind.Set
                && targetSet.ElementType is ExpressBoundNamedType targetEntity
                && targetEntity.Declaration.Kind == ExpressDeclarationKind.Entity
                && valueExpression.Type.DeclaredType is ExpressBoundAggregateType sourceSet
                && sourceSet.Kind == ExpressAggregateKind.Set)
            {
                var sourceCanContainTarget = sourceSet.ElementType
                    is ExpressBoundGenericType { IsEntity: true, };
                if (sourceSet.ElementType is ExpressBoundNamedType
                    { Declaration.Kind: ExpressDeclarationKind.Entity, } sourceEntity
                    && !ReferenceEquals(sourceEntity.Declaration, targetEntity.Declaration))
                {
                    sourceCanContainTarget = plan.EntityProjections.Single(projection =>
                            ReferenceEquals(projection.Entity.Symbol, targetEntity.Declaration))
                        .PhysicalComponents.Any(component =>
                            ReferenceEquals(component.Symbol, sourceEntity.Declaration));
                }

                if (sourceCanContainTarget)
                {
                    var source = allocateTemporaryName("__expressAssignedSet");
                    var testedItem = allocateTemporaryName("__expressTestedSetItem");
                    var selectedItem = allocateTemporaryName("__expressSelectedSetItem");
                    var typedItem = allocateTemporaryName("__expressTypedSetItem");
                    var targetSetName = ExpressExpressionEmitter.BoundTypeName(targetSet);
                    var targetEntityName = ExpressExpressionEmitter.BoundTypeName(targetEntity);
                    valueCode = $"(({valueCode}) is {{ }} {source} && "
                        + "global::System.Linq.Enumerable.All("
                        + $"{source}, {testedItem} => {testedItem} is {targetEntityName}) ? "
                        + $"({targetSetName})[..global::System.Linq.Enumerable.Select("
                        + $"{source}, {selectedItem} => {selectedItem} switch {{ "
                        + $"{targetEntityName} {typedItem} => {typedItem}, "
                        + "_ => throw new global::System.InvalidOperationException() })] : "
                        + $"({targetSetName}?)null)";
                }
            }

            if (targetType is ExpressBoundNamedType targetSelectName
                && targetSelectName.Declaration.Kind != ExpressDeclarationKind.Entity
                && plan.Resolver.GetDefinedType(targetSelectName.Declaration).UnderlyingType
                    is ExpressBoundSelectType targetSelect
                && valueExpression.Type.DeclaredType is ExpressBoundNamedType
                { Declaration.Kind: ExpressDeclarationKind.Entity, } valueEntity)
            {
                var valueProjection = plan.EntityProjections.Single(projection =>
                    projection.Entity.Symbol == valueEntity.Declaration);
                var alternatives = plan.Resolver.GetSelectAlternatives(targetSelect)
                    .Where(alternative => alternative.Kind == ExpressDeclarationKind.Entity
                        && valueProjection.PhysicalComponents.Any(component =>
                            component.Symbol == alternative))
                    .ToArray();
                if (alternatives.Length == 1)
                {
                    valueCode = ExpressExpressionEmitter.BoundTypeName(targetSelectName)
                        + ".From"
                        + ExpressEntityProjection.ToPascalCase(alternatives[0].Name)
                        + $"({valueCode})";
                }
            }

            if (targetType is ExpressBoundScalarType { Kind: ExpressScalarKind.Real, }
                && valueExpression.Type.Kind == ExpressExpressionTypeKind.Integer)
            {
                if (valueExpression.Type.CanBeIndeterminate)
                {
                    var presentInteger = allocateTemporaryName("__expressAssignedInteger");
                    valueCode = $"(({valueCode}) is {{ }} {presentInteger} ? "
                        + ExpressExpressionEmitter.PromoteNumeric(
                            valueExpression,
                            presentInteger,
                            ExpressExpressionTypeKind.Real)
                        + " : (global::TedToolkit.Step21.RealValue?)null)";
                }
                else
                {
                    valueCode = ExpressExpressionEmitter.PromoteNumeric(
                        valueExpression,
                        valueCode,
                        ExpressExpressionTypeKind.Real);
                }
            }

            if (targetType is ExpressBoundScalarType { Kind: ExpressScalarKind.Logical, }
                && valueExpression.Type.Kind == ExpressExpressionTypeKind.Boolean)
            {
                valueCode = ExpressExpressionEmitter.AsLogical(valueExpression, valueCode);
            }
            else if (targetType is ExpressBoundScalarType { Kind: ExpressScalarKind.Boolean, }
                     && valueExpression.Type.Kind == ExpressExpressionTypeKind.Logical)
            {
                valueCode = $"(({valueCode}) switch {{ "
                    + "global::TedToolkit.Step21.LogicalValue.True => true, "
                    + "global::TedToolkit.Step21.LogicalValue.False => false, "
                    + "global::TedToolkit.Step21.LogicalValue.Unknown => (global::System.Boolean?)null })";
            }

            if (valueExpression.Type.CanBeIndeterminate)
            {
                var unknownResult = functionResultType is ExpressBoundScalarType
                { Kind: ExpressScalarKind.Logical, }
                        ? "global::TedToolkit.Step21.LogicalValue.Unknown"
                        : "null";
                if (valueExpression.Kind == ExpressExpressionKind.Indeterminate)
                {
                    if (targetIsOptional && optionalUnsetCode is not null)
                    {
                        owner.AddStatement(new CustomExpression(optionalUnsetCode));
                        return true;
                    }

                    if (!targetIsOptional && canReturnIndeterminate)
                    {
                        owner.AddStatement(new CustomExpression(unknownResult).Return);
                        return false;
                    }
                }

                var presentValue = allocateTemporaryName("__expressAssignedValue");
                if (targetIsOptional && optionalUnsetCode is not null)
                {
                    var presence = new IfStatement(new CustomExpression(
                            $"({valueCode}) is {{ }} {presentValue}"))
                        .AddStatement(new CustomExpression(targetCode).Assign(
                            new CustomExpression(presentValue)));
                    presence.Else().AddStatement(new CustomExpression(optionalUnsetCode));
                    owner.AddStatement(presence);
                    return true;
                }

                if (!targetIsOptional && canReturnIndeterminate)
                {
                    if (!hasQualifier && canEstablishDeterminacy)
                    {
                        determinateLexicals?.Add(target);
                    }

                    var presence = new IfStatement(new CustomExpression(
                            $"({valueCode}) is {{ }} {presentValue}"))
                        .AddStatement(new CustomExpression(targetCode).Assign(
                            new CustomExpression(presentValue)));
                    presence.Else().AddStatement(new CustomExpression(unknownResult).Return);
                    owner.AddStatement(presence);
                    return true;
                }
            }

            if (!hasQualifier
                && !targetIsOptional
                && canEstablishDeterminacy
                && valueIsKnownDeterminate)
            {
                determinateLexicals?.Add(target);
            }

            owner.AddStatement(new CustomExpression(targetCode).Assign(
                new CustomExpression(valueCode)));
            return true;
        }

        if (operation.Production == "returnStmt")
        {
            var boundExpression = plan.GetExpression(operation.RequiredChild("expression"));
            var expression = ExpressExpressionEmitter.Emit(
                boundExpression,
                CreateContext(
                    plan,
                    selfExpression: null,
                    "entities",
                    lexicalNames,
                    safeIndices,
                    allocateTemporaryName,
                    selectNarrowings,
                    pathNarrowings,
                    determinateLexicals,
                    safeIndexPaths,
                    scalarNarrowings));
            var expressionCode = expression.Code;
            if (functionResultType is ExpressBoundNamedType resultSelectName
                && resultSelectName.Declaration.Kind != ExpressDeclarationKind.Entity
                && plan.Resolver.GetDefinedType(resultSelectName.Declaration).UnderlyingType
                    is ExpressBoundSelectType resultSelect
                && boundExpression.Type.DeclaredType is ExpressBoundNamedType
                { Declaration.Kind: ExpressDeclarationKind.Entity, } resultEntity)
            {
                var resultProjection = plan.EntityProjections.Single(projection =>
                    projection.Entity.Symbol == resultEntity.Declaration);
                var alternatives = plan.Resolver.GetSelectAlternatives(resultSelect)
                    .Where(alternative => alternative.Kind == ExpressDeclarationKind.Entity
                        && resultProjection.PhysicalComponents.Any(component =>
                            component.Symbol == alternative))
                    .ToArray();
                if (alternatives.Length == 1)
                {
                    expressionCode = ExpressExpressionEmitter.BoundTypeName(resultSelectName)
                        + ".From"
                        + ExpressEntityProjection.ToPascalCase(alternatives[0].Name)
                        + $"({expressionCode})";
                }
            }

            if (functionResultType is ExpressBoundNamedType
                { Declaration.Kind: not ExpressDeclarationKind.Entity, }
                && boundExpression.Kind != ExpressExpressionKind.Indeterminate)
            {
                var targetType = functionResultType;
                var definedTypes = new List<ExpressBoundNamedType>();
                while (targetType is ExpressBoundNamedType definedType
                       && definedType.Declaration.Kind != ExpressDeclarationKind.Entity)
                {
                    definedTypes.Add(definedType);
                    targetType = plan.Resolver.GetDefinedType(definedType.Declaration).UnderlyingType;
                }

                var actualNominal = boundExpression.Type.DeclaredType as ExpressBoundNamedType;
                var primitiveActual = actualNominal is null;
                var sameDefinedActual = definedTypes.Count > 0
                    && actualNominal is not null
                    && ReferenceEquals(actualNominal.Declaration, definedTypes[0].Declaration);
                var exactScalar = targetType is ExpressBoundScalarType targetScalar
                    && targetScalar.Kind switch
                    {
                        ExpressScalarKind.Binary => boundExpression.Type.Kind == ExpressExpressionTypeKind.Binary,
                        ExpressScalarKind.Boolean => boundExpression.Type.Kind == ExpressExpressionTypeKind.Boolean,
                        ExpressScalarKind.Integer => boundExpression.Type.Kind == ExpressExpressionTypeKind.Integer,
                        ExpressScalarKind.Logical => boundExpression.Type.Kind == ExpressExpressionTypeKind.Logical,
                        ExpressScalarKind.Number => boundExpression.Type.Kind == ExpressExpressionTypeKind.Number,
                        ExpressScalarKind.Real => boundExpression.Type.Kind == ExpressExpressionTypeKind.Real,
                        ExpressScalarKind.String => boundExpression.Type.Kind == ExpressExpressionTypeKind.String,
                        _ => false,
                    };
                if ((primitiveActual || sameDefinedActual) && exactScalar)
                {
                    if (boundExpression.Type.CanBeIndeterminate)
                    {
                        var presentValue = allocateTemporaryName("__expressReturnedValue");
                        var presentCode = presentValue;
                        for (var definedIndex = definedTypes.Count - 1; definedIndex >= 0; definedIndex--)
                        {
                            presentCode = "new "
                                + ExpressExpressionEmitter.BoundTypeName(definedTypes[definedIndex])
                                + $"({presentCode})";
                        }

                        expressionCode = $"(({expressionCode}) is {{ }} {presentValue} ? {presentCode} : ("
                            + ExpressExpressionEmitter.BoundTypeName(functionResultType)
                            + "?)null)";
                    }
                    else
                    {
                        for (var definedIndex = definedTypes.Count - 1; definedIndex >= 0; definedIndex--)
                        {
                            expressionCode = "new "
                                + ExpressExpressionEmitter.BoundTypeName(definedTypes[definedIndex])
                                + $"({expressionCode})";
                        }
                    }
                }
            }

            if (functionResultType is ExpressBoundScalarType { Kind: ExpressScalarKind.Logical, }
                && boundExpression.Type.Kind == ExpressExpressionTypeKind.Boolean)
            {
                expressionCode = ExpressExpressionEmitter.AsLogical(boundExpression, expressionCode);
            }
            else if (functionResultType is ExpressBoundScalarType { Kind: ExpressScalarKind.Boolean, }
                     && boundExpression.Type.Kind == ExpressExpressionTypeKind.Logical)
            {
                expressionCode = $"(({expressionCode}) switch {{ "
                    + "global::TedToolkit.Step21.LogicalValue.True => true, "
                    + "global::TedToolkit.Step21.LogicalValue.False => false, "
                    + "global::TedToolkit.Step21.LogicalValue.Unknown => (global::System.Boolean?)null })";
            }

            owner.AddStatement(new CustomExpression(expressionCode).Return);
            return false;
        }

        if (operation.Production == "repeatStmt")
        {
            var increment = operation.RequiredChild("repeatControl").RequiredChild("incrementControl");
            var variable = increment.RequiredChild("variableId");
            var variableName = variable.IdentifierToken().Text;
            var generatedName = "__repeat_"
                + ExpressEntityProjection.ToPascalCase(variableName)
                + "_"
                + variable.Span.Start.Line.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "_"
                + variable.Span.Start.Column.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var repeatName = plan.Schema.NameReferences
                .Select(reference => reference.Target)
                .Where(candidate => candidate.Kind == ExpressBoundNameKind.RepeatVariable
                    && SameStart(candidate.Span, variable.Span))
                .Distinct()
                .SingleOrDefault();
            var nestedNames = lexicalNames.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            if (repeatName?.Type is not null)
            {
                nestedNames.Add(variableName, (generatedName, repeatName.Type));
            }

            var lower = plan.GetExpression(increment.RequiredChild("bound1").RequiredChild("numericExpression"));
            var upper = plan.GetExpression(increment.RequiredChild("bound2").RequiredChild("numericExpression"));
            var step = increment.ChildRules("increment").SingleOrDefault() is { } stepSyntax
                ? plan.GetExpression(stepSyntax.RequiredChild("numericExpression"))
                : null;
            var lowerValue = ExpressExpressionEmitter.Emit(
                lower,
                CreateContext(
                    plan,
                    selfExpression: null,
                    "entities",
                    lexicalNames,
                    safeIndices,
                    allocateTemporaryName,
                    selectNarrowings,
                    pathNarrowings,
                    determinateLexicals,
                    safeIndexPaths,
                    scalarNarrowings));
            var upperValue = ExpressExpressionEmitter.Emit(
                upper,
                CreateContext(
                    plan,
                    selfExpression: null,
                    "entities",
                    lexicalNames,
                    safeIndices,
                    allocateTemporaryName,
                    selectNarrowings,
                    pathNarrowings,
                    determinateLexicals,
                    safeIndexPaths,
                    scalarNarrowings));
            var stepValue = step is null
                ? null
                : ExpressExpressionEmitter.Emit(
                    step,
                    CreateContext(
                        plan,
                        selfExpression: null,
                        "entities",
                        lexicalNames,
                        safeIndices,
                        allocateTemporaryName,
                        selectNarrowings,
                        pathNarrowings,
                        determinateLexicals,
                        safeIndexPaths,
                        scalarNarrowings));
            var lowerCode = lower.Type.Kind switch
            {
                ExpressExpressionTypeKind.Integer =>
                    $"global::TedToolkit.Step21.NumberValue.FromInteger({lowerValue.Code})",
                ExpressExpressionTypeKind.Real =>
                    $"global::TedToolkit.Step21.NumberValue.FromReal({lowerValue.Code})",
                _ => lowerValue.Code,
            };
            var upperCode = upper.Type.Kind switch
            {
                ExpressExpressionTypeKind.Integer =>
                    $"global::TedToolkit.Step21.NumberValue.FromInteger({upperValue.Code})",
                ExpressExpressionTypeKind.Real =>
                    $"global::TedToolkit.Step21.NumberValue.FromReal({upperValue.Code})",
                _ => upperValue.Code,
            };
            var stepCode = step?.Type.Kind switch
            {
                null => "global::TedToolkit.Step21.NumberValue.FromInteger(global::System.Numerics.BigInteger.One)",
                ExpressExpressionTypeKind.Integer =>
                    $"global::TedToolkit.Step21.NumberValue.FromInteger({stepValue!.Code})",
                ExpressExpressionTypeKind.Real =>
                    $"global::TedToolkit.Step21.NumberValue.FromReal({stepValue!.Code})",
                _ => stepValue!.Code,
            };
            var repeatSafeIndices = safeIndices?.ToList() ?? [];
            var repeatSafeIndexPaths = safeIndexPaths?.ToList()
                ?? [];
            ExpressBoundName? upperAggregate = null;
            ExpressBoundExpression? upperAggregatePath = null;
            var upperUsesSize = upper.Operation == "SIZEOF";
            var upperUsesHighIndex = upper.Operation == "HIINDEX";
            if ((upperUsesSize || upperUsesHighIndex)
                && upper.Children.Count == 1
                && upper.Children[0].Reference is { Type: ExpressBoundAggregateType, } directAggregate)
            {
                upperAggregate = directAggregate;
                upperAggregatePath = upper.Children[0];
            }
            else if (upper.Reference is { } upperAlias
                     && sizeAliases?.TryGetValue(upperAlias, out var aliasedAggregate) == true)
            {
                upperAggregate = aliasedAggregate;
                upperUsesSize = true;
            }

            if (System.Numerics.BigInteger.TryParse(
                    lower.SourceText,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var lowerBound)
                && lowerBound >= System.Numerics.BigInteger.One
                && upperAggregate is { Type: ExpressBoundAggregateType upperAggregateType, }
                && ((upperUsesSize && upperAggregateType.Kind == ExpressAggregateKind.List)
                    || (upperUsesHighIndex
                        && lowerBound == System.Numerics.BigInteger.One
                        && upperAggregateType.Kind == ExpressAggregateKind.Bag))
                && repeatName is not null
                && (step is null
                    || (System.Numerics.BigInteger.TryParse(
                            step.SourceText,
                            System.Globalization.NumberStyles.Integer,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var stepValueInteger)
                        && stepValueInteger == System.Numerics.BigInteger.One)))
            {
                repeatSafeIndices = [.. repeatSafeIndices, new(upperAggregate, repeatName),];
                if (upperAggregatePath is { Kind: not ExpressExpressionKind.Reference, })
                {
                    repeatSafeIndexPaths.Add(new(upperAggregatePath, repeatName));
                }
            }

            var nestedSizeAliases = sizeAliases is null
                ? null
                : new Dictionary<ExpressBoundName, ExpressBoundName>(sizeAliases);
            var nestedSelectNarrowings = selectNarrowings is null
                ? new Dictionary<ExpressBoundName, ExpressBoundSymbol>()
                : selectNarrowings.ToDictionary(pair => pair.Key, pair => pair.Value);
            var nestedPathNarrowings = pathNarrowings?.ToList() ?? [];
            var nestedDeterminateLexicals = determinateLexicals is null
                ? null
                : new HashSet<ExpressBoundName>(determinateLexicals);
            var nestedScalarNarrowings = scalarNarrowings is null
                ? new Dictionary<string, (string StorageCode, string Code, ExpressBoundType Type)>(
                    StringComparer.OrdinalIgnoreCase)
                : scalarNarrowings.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase);
            var bodyOwner = new IfStatement(new CustomExpression("true"));
            var bodyFallsThrough = true;
            foreach (var nested in operation.ChildRules("stmt"))
            {
                if (!bodyFallsThrough)
                {
                    break;
                }

                bodyFallsThrough = EmitFunctionStatement(
                    plan,
                    nested,
                    functionResultType,
                    canReturnIndeterminate,
                    bodyOwner,
                    nestedNames,
                    allocateTemporaryName,
                    repeatSafeIndices,
                    nestedSizeAliases,
                    nestedSelectNarrowings,
                    nestedPathNarrowings,
                    nestedDeterminateLexicals,
                    repeatSafeIndexPaths,
                    nestedScalarNarrowings);
            }

            if (bodyFallsThrough)
            {
                IntersectDictionaryFacts(
                    scalarNarrowings,
                    static (left, right) => string.Equals(
                            left.StorageCode,
                            right.StorageCode,
                            StringComparison.Ordinal)
                        && string.Equals(left.Code, right.Code, StringComparison.Ordinal)
                        && ReferenceEquals(left.Type, right.Type),
                    scalarNarrowings?.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value,
                        StringComparer.OrdinalIgnoreCase)
                        ?? [],
                    nestedScalarNarrowings);
                IntersectCollectionFacts(
                    safeIndices,
                    static (left, right) => ReferenceEquals(left.Key, right.Key)
                        && ReferenceEquals(left.Value, right.Value),
                    safeIndices?.ToList() ?? [],
                    repeatSafeIndices);
                IntersectDictionaryFacts(
                    sizeAliases,
                    static (left, right) => ReferenceEquals(left, right),
                    sizeAliases?.ToDictionary(pair => pair.Key, pair => pair.Value)
                        ?? [],
                    nestedSizeAliases ?? []);
                JoinSelectFacts(
                    selectNarrowings,
                    nestedSelectNarrowings,
                    selectNarrowings?.ToDictionary(pair => pair.Key, pair => pair.Value)
                        ?? []);
                IntersectCollectionFacts(
                    pathNarrowings,
                    static (left, right) => SameDirectReferencePath(left.Key, right.Key)
                        && ReferenceEquals(left.Value, right.Value),
                    nestedPathNarrowings,
                    pathNarrowings?.ToList() ?? []);
                IntersectCollectionFacts(
                    determinateLexicals,
                    static (left, right) => ReferenceEquals(left, right),
                    determinateLexicals?.ToArray() ?? Array.Empty<ExpressBoundName>(),
                    (IReadOnlyCollection<ExpressBoundName>?)nestedDeterminateLexicals
                        ?? Array.Empty<ExpressBoundName>());
                IntersectCollectionFacts(
                    safeIndexPaths,
                    static (left, right) => ReferenceEquals(left.Key, right.Key)
                        && ReferenceEquals(left.Value, right.Value),
                    safeIndexPaths?.ToList() ?? [],
                    repeatSafeIndexPaths);
            }

            owner.AddStatement(new Custom((ref SourceBuilder source) =>
            {
                source.Append($"for (var {generatedName} = {lowerCode}; "
                    + $"{generatedName} <= {upperCode}; {generatedName} += {stepCode})");
                source.BeginBlock();
                foreach (var bodyStatement in bodyOwner.Statements)
                {
                    bodyStatement.ToCode(ref source);
                    source.AppendLine();
                }

                source.EndBlock();
            }));
            return true;
        }

        if (operation.Production == "compoundStmt")
        {
            var fallsThrough = true;
            foreach (var nested in operation.ChildRules("stmt"))
            {
                if (!fallsThrough)
                {
                    break;
                }

                fallsThrough = EmitFunctionStatement(
                    plan,
                    nested,
                    functionResultType,
                    canReturnIndeterminate,
                    owner,
                    lexicalNames,
                    allocateTemporaryName,
                    safeIndices,
                    sizeAliases,
                    selectNarrowings,
                    pathNarrowings,
                    determinateLexicals,
                    safeIndexPaths,
                    scalarNarrowings);
            }

            return fallsThrough;
        }

        if (operation.Production == "caseStmt")
        {
            var selectorSyntax = operation.RequiredChild("selector").RequiredChild("expression");
            var selector = plan.GetExpression(selectorSyntax);
            var emittedSelector = ExpressExpressionEmitter.Emit(
                selector,
                CreateContext(
                    plan,
                    selfExpression: null,
                    "entities",
                    lexicalNames,
                    safeIndices,
                    allocateTemporaryName,
                    selectNarrowings,
                    pathNarrowings,
                    determinateLexicals,
                    safeIndexPaths,
                    scalarNarrowings));
            var selectorName = "__case_"
                + operation.Span.Start.Line.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "_"
                + operation.Span.Start.Column.ToString(System.Globalization.CultureInfo.InvariantCulture);
            owner.AddStatement(new VariableExpression(DataType.Var, selectorName)
                .AddDefault(new CustomExpression(emittedSelector.Code)));
            var incomingLexicalNames = lexicalNames.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var incomingSafeIndices = safeIndices?.ToList() ?? [];
            var incomingAliases = sizeAliases?.ToDictionary(pair => pair.Key, pair => pair.Value)
                ?? [];
            var incomingSelectNarrowings = selectNarrowings?.ToDictionary(
                pair => pair.Key,
                pair => pair.Value)
                ?? [];
            var incomingPathNarrowings = pathNarrowings?.ToList() ?? [];
            var incomingDeterminateLexicals = determinateLexicals is null
                ? new HashSet<ExpressBoundName>()
                : new HashSet<ExpressBoundName>(determinateLexicals);
            var incomingSafeIndexPaths = safeIndexPaths?.ToList() ?? [];
            var incomingScalarNarrowings = scalarNarrowings?.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase)
                ?? new(
                    StringComparer.OrdinalIgnoreCase);
            var fallingSafeIndices = new List<List<KeyValuePair<ExpressBoundName, ExpressBoundName>>>();
            var fallingAliases = new List<Dictionary<ExpressBoundName, ExpressBoundName>>();
            var fallingSelectNarrowings = new List<Dictionary<ExpressBoundName, ExpressBoundSymbol>>();
            var fallingPathNarrowings =
                new List<List<KeyValuePair<ExpressBoundExpression, ExpressBoundSymbol>>>();
            var fallingDeterminateLexicals = new List<HashSet<ExpressBoundName>>();
            var fallingSafeIndexPaths =
                new List<List<KeyValuePair<ExpressBoundExpression, ExpressBoundName>>>();
            var fallingScalarNarrowings = new List<Dictionary<
                string,
                (string StorageCode, string Code, ExpressBoundType Type)>>();
            IfStatement? conditionalCase = null;
            foreach (var action in operation.ChildRules("caseAction"))
            {
                var comparisons = action.ChildRules("caseLabel").Select(label =>
                {
                    var expression = plan.GetExpression(label.RequiredChild("expression"));
                    var emitted = ExpressExpressionEmitter.Emit(
                        expression,
                        CreateContext(
                            plan,
                            selfExpression: null,
                            "entities",
                            lexicalNames,
                            safeIndices,
                            allocateTemporaryName,
                            selectNarrowings,
                            pathNarrowings,
                            determinateLexicals,
                            safeIndexPaths,
                            scalarNarrowings));
                    return ExpressExpressionEmitter.ValueEqualityCore(
                        selector,
                        selectorName,
                        expression,
                        emitted.Code);
                });
                var caseCondition = string.Join(" || ", comparisons.Select(comparison => $"({comparison})"));
                var caseBranch = conditionalCase is null
                    ? new IfStatement(new CustomExpression(caseCondition))
                    : conditionalCase.ElseIf(new CustomExpression(caseCondition));
                conditionalCase ??= caseBranch;
                var actionLexicalNames = incomingLexicalNames.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase);
                var actionSafeIndices = incomingSafeIndices.ToList();
                var actionAliases = incomingAliases.ToDictionary(pair => pair.Key, pair => pair.Value);
                var actionSelectNarrowings = incomingSelectNarrowings.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value);
                var actionPathNarrowings = incomingPathNarrowings.ToList();
                var actionDeterminateLexicals = new HashSet<ExpressBoundName>(
                    incomingDeterminateLexicals);
                var actionSafeIndexPaths = incomingSafeIndexPaths.ToList();
                var actionScalarNarrowings = incomingScalarNarrowings.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase);
                var actionOwner = new IfStatement(new CustomExpression("true"));
                var actionFallsThrough = EmitFunctionStatement(
                    plan,
                    action.RequiredChild("stmt"),
                    functionResultType,
                    canReturnIndeterminate,
                    actionOwner,
                    actionLexicalNames,
                    allocateTemporaryName,
                    actionSafeIndices,
                    actionAliases,
                    actionSelectNarrowings,
                    actionPathNarrowings,
                    actionDeterminateLexicals,
                    actionSafeIndexPaths,
                    actionScalarNarrowings);
                foreach (var actionStatement in actionOwner.Statements)
                {
                    caseBranch.AddStatement(actionStatement);
                }

                if (actionFallsThrough)
                {
                    fallingSafeIndices.Add(actionSafeIndices);
                    fallingAliases.Add(actionAliases);
                    fallingSelectNarrowings.Add(actionSelectNarrowings);
                    fallingPathNarrowings.Add(actionPathNarrowings);
                    fallingDeterminateLexicals.Add(actionDeterminateLexicals);
                    fallingSafeIndexPaths.Add(actionSafeIndexPaths);
                    fallingScalarNarrowings.Add(actionScalarNarrowings);
                }
            }

            if (operation.ChildRules("stmt").SingleOrDefault() is { } otherwise)
            {
                var otherwiseBranch = conditionalCase?.Else()
                    ?? throw new InvalidOperationException("CASE requires at least one action.");
                var otherwiseLexicalNames = incomingLexicalNames.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase);
                var otherwiseSafeIndices = incomingSafeIndices.ToList();
                var otherwiseAliases = incomingAliases.ToDictionary(pair => pair.Key, pair => pair.Value);
                var otherwiseSelectNarrowings = incomingSelectNarrowings.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value);
                var otherwisePathNarrowings = incomingPathNarrowings.ToList();
                var otherwiseDeterminateLexicals = new HashSet<ExpressBoundName>(
                    incomingDeterminateLexicals);
                var otherwiseSafeIndexPaths = incomingSafeIndexPaths.ToList();
                var otherwiseScalarNarrowings = incomingScalarNarrowings.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase);
                var otherwiseOwner = new IfStatement(new CustomExpression("true"));
                var otherwiseFallsThrough = EmitFunctionStatement(
                    plan,
                    otherwise,
                    functionResultType,
                    canReturnIndeterminate,
                    otherwiseOwner,
                    otherwiseLexicalNames,
                    allocateTemporaryName,
                    otherwiseSafeIndices,
                    otherwiseAliases,
                    otherwiseSelectNarrowings,
                    otherwisePathNarrowings,
                    otherwiseDeterminateLexicals,
                    otherwiseSafeIndexPaths,
                    otherwiseScalarNarrowings);
                foreach (var otherwiseStatement in otherwiseOwner.Statements)
                {
                    otherwiseBranch.AddStatement(otherwiseStatement);
                }

                if (otherwiseFallsThrough)
                {
                    fallingSafeIndices.Add(otherwiseSafeIndices);
                    fallingAliases.Add(otherwiseAliases);
                    fallingSelectNarrowings.Add(otherwiseSelectNarrowings);
                    fallingPathNarrowings.Add(otherwisePathNarrowings);
                    fallingDeterminateLexicals.Add(otherwiseDeterminateLexicals);
                    fallingSafeIndexPaths.Add(otherwiseSafeIndexPaths);
                    fallingScalarNarrowings.Add(otherwiseScalarNarrowings);
                }
            }
            else
            {
                fallingSafeIndices.Add(incomingSafeIndices);
                fallingAliases.Add(incomingAliases);
                fallingSelectNarrowings.Add(incomingSelectNarrowings);
                fallingPathNarrowings.Add(incomingPathNarrowings);
                fallingDeterminateLexicals.Add(incomingDeterminateLexicals);
                fallingSafeIndexPaths.Add(incomingSafeIndexPaths);
                fallingScalarNarrowings.Add(incomingScalarNarrowings);
            }

            owner.AddStatement(conditionalCase
                ?? throw new InvalidOperationException("CASE requires at least one action."));
            if (fallingScalarNarrowings.Count == 0)
            {
                return false;
            }

            IntersectDictionaryFacts(
                scalarNarrowings,
                static (left, right) => string.Equals(
                        left.StorageCode,
                        right.StorageCode,
                        StringComparison.Ordinal)
                    && string.Equals(left.Code, right.Code, StringComparison.Ordinal)
                    && ReferenceEquals(left.Type, right.Type),
                fallingScalarNarrowings[0],
                fallingScalarNarrowings[0]);
            IntersectCollectionFacts(
                safeIndices,
                static (left, right) => ReferenceEquals(left.Key, right.Key)
                    && ReferenceEquals(left.Value, right.Value),
                fallingSafeIndices[0],
                fallingSafeIndices[0]);
            IntersectDictionaryFacts(
                sizeAliases,
                static (left, right) => ReferenceEquals(left, right),
                fallingAliases[0],
                fallingAliases[0]);
            JoinSelectFacts(
                selectNarrowings,
                fallingSelectNarrowings[0],
                fallingSelectNarrowings[0]);
            IntersectCollectionFacts(
                pathNarrowings,
                static (left, right) => ReferenceEquals(left.Value, right.Value)
                    && SameDirectReferencePath(left.Key, right.Key),
                fallingPathNarrowings[0],
                fallingPathNarrowings[0]);
            IntersectCollectionFacts(
                determinateLexicals,
                static (left, right) => ReferenceEquals(left, right),
                fallingDeterminateLexicals[0],
                fallingDeterminateLexicals[0]);
            IntersectCollectionFacts(
                safeIndexPaths,
                static (left, right) => ReferenceEquals(left.Key, right.Key)
                    && ReferenceEquals(left.Value, right.Value),
                fallingSafeIndexPaths[0],
                fallingSafeIndexPaths[0]);
            for (var index = 1; index < fallingScalarNarrowings.Count; index++)
            {
                var currentScalarNarrowings = scalarNarrowings?.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase)
                    ?? new(
                        StringComparer.OrdinalIgnoreCase);
                var currentSafeIndices = safeIndices?.ToList() ?? [];
                var currentAliases = sizeAliases?.ToDictionary(pair => pair.Key, pair => pair.Value)
                    ?? [];
                var currentSelectNarrowings = selectNarrowings?.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value)
                    ?? [];
                var currentPathNarrowings = pathNarrowings?.ToList() ?? [];
                var currentDeterminateLexicals = determinateLexicals?.ToArray()
                    ?? Array.Empty<ExpressBoundName>();
                var currentSafeIndexPaths = safeIndexPaths?.ToList() ?? [];
                IntersectDictionaryFacts(
                    scalarNarrowings,
                    static (left, right) => string.Equals(
                            left.StorageCode,
                            right.StorageCode,
                            StringComparison.Ordinal)
                        && string.Equals(left.Code, right.Code, StringComparison.Ordinal)
                        && ReferenceEquals(left.Type, right.Type),
                    currentScalarNarrowings,
                    fallingScalarNarrowings[index]);
                IntersectCollectionFacts(
                    safeIndices,
                    static (left, right) => ReferenceEquals(left.Key, right.Key)
                        && ReferenceEquals(left.Value, right.Value),
                    currentSafeIndices,
                    fallingSafeIndices[index]);
                IntersectDictionaryFacts(
                    sizeAliases,
                    static (left, right) => ReferenceEquals(left, right),
                    currentAliases,
                    fallingAliases[index]);
                JoinSelectFacts(
                    selectNarrowings,
                    currentSelectNarrowings,
                    fallingSelectNarrowings[index]);
                IntersectCollectionFacts(
                    pathNarrowings,
                    static (left, right) => SameDirectReferencePath(left.Key, right.Key)
                        && ReferenceEquals(left.Value, right.Value),
                    currentPathNarrowings,
                    fallingPathNarrowings[index]);
                IntersectCollectionFacts(
                    determinateLexicals,
                    static (left, right) => ReferenceEquals(left, right),
                    currentDeterminateLexicals,
                    fallingDeterminateLexicals[index]);
                IntersectCollectionFacts(
                    safeIndexPaths,
                    static (left, right) => ReferenceEquals(left.Key, right.Key)
                        && ReferenceEquals(left.Value, right.Value),
                    currentSafeIndexPaths,
                    fallingSafeIndexPaths[index]);
            }

            return true;
        }

        var conditional = operation.RequiredChild("logicalExpression").RequiredChild("expression");
        var boundCondition = plan.GetExpression(conditional);
        var condition = ExpressExpressionEmitter.Emit(
            boundCondition,
            CreateContext(
                plan,
                selfExpression: null,
                "entities",
                lexicalNames,
                safeIndices,
                allocateTemporaryName,
                selectNarrowings,
                pathNarrowings,
                determinateLexicals,
                safeIndexPaths,
                scalarNarrowings));
        var conditionCode = $"({ExpressExpressionEmitter.AsLogical(boundCondition, condition.Code)}) "
            + "== global::TedToolkit.Step21.LogicalValue.True";
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

        var thenAliases = sizeAliases is null
            ? null
            : new Dictionary<ExpressBoundName, ExpressBoundName>(sizeAliases);
        var thenSafeIndices = safeIndices?.ToList() ?? [];
        var thenLexicalNames = lexicalNames.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);
        var elseLexicalNames = lexicalNames.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);
        var thenScalarNarrowings = scalarNarrowings is null
            ? new Dictionary<string, (string StorageCode, string Code, ExpressBoundType Type)>(
                StringComparer.OrdinalIgnoreCase)
            : scalarNarrowings.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
        var elseScalarNarrowings = scalarNarrowings is null
            ? new Dictionary<string, (string StorageCode, string Code, ExpressBoundType Type)>(
                StringComparer.OrdinalIgnoreCase)
            : scalarNarrowings.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
        var thenSelectNarrowings = selectNarrowings is null
            ? new Dictionary<ExpressBoundName, ExpressBoundSymbol>()
            : selectNarrowings.ToDictionary(pair => pair.Key, pair => pair.Value);
        var elseSelectNarrowings = selectNarrowings is null
            ? new Dictionary<ExpressBoundName, ExpressBoundSymbol>()
            : selectNarrowings.ToDictionary(pair => pair.Key, pair => pair.Value);
        var thenPathNarrowings = pathNarrowings?.ToList()
            ?? [];
        var elsePathNarrowings = pathNarrowings?.ToList()
            ?? [];
        var thenDeterminateLexicals = determinateLexicals is null
            ? new HashSet<ExpressBoundName>()
            : new HashSet<ExpressBoundName>(determinateLexicals);
        var elseDeterminateLexicals = determinateLexicals is null
            ? new HashSet<ExpressBoundName>()
            : new HashSet<ExpressBoundName>(determinateLexicals);
        var thenSafeIndexPaths = safeIndexPaths?.ToList()
            ?? [];
        var elseSafeIndexPaths = safeIndexPaths?.ToList()
            ?? [];
        var typeName = boundCondition.Children.Count == 2 ? boundCondition.Children[0] : null;
        var typeOf = boundCondition.Children.Count == 2 ? boundCondition.Children[1] : null;
        if (boundCondition.Operation == "IN"
            && typeName?.Kind == ExpressExpressionKind.Literal
            && typeName.Type.Kind == ExpressExpressionTypeKind.String
            && typeOf?.Kind == ExpressExpressionKind.Application
            && typeOf.Operation == "TYPEOF"
            && typeOf.Children.Count == 1)
        {
            var narrowedExpression = typeOf.Children[0];
            var narrowedReference = narrowedExpression.Kind == ExpressExpressionKind.Reference
                ? narrowedExpression.Reference
                : null;
            ExpressBoundType? narrowedType = narrowedExpression.Type.DeclaredType;
            while (narrowedType is ExpressBoundNamedType namedNarrowed
                   && namedNarrowed.Declaration.Kind != ExpressDeclarationKind.Entity)
            {
                narrowedType = plan.Resolver.GetDefinedType(namedNarrowed.Declaration).UnderlyingType;
            }

            if (narrowedType is ExpressBoundSelectType narrowedSelect)
            {
                var alternatives = new List<ExpressBoundSymbol>();
                var pendingAlternatives = new Queue<ExpressBoundSymbol>(
                    plan.Resolver.GetSelectAlternatives(narrowedSelect));
                var visitedAlternatives = new HashSet<ExpressBoundSymbol>();
                var allAlternativesClosed = !narrowedSelect.IsExtensible;
                while (pendingAlternatives.Count > 0)
                {
                    var alternative = pendingAlternatives.Dequeue();
                    if (!visitedAlternatives.Add(alternative))
                    {
                        continue;
                    }

                    ExpressBoundType alternativeType = new ExpressBoundNamedType(
                        alternative,
                        narrowedSelect.Span);
                    while (alternativeType is ExpressBoundNamedType namedAlternative
                           && namedAlternative.Declaration.Kind != ExpressDeclarationKind.Entity)
                    {
                        var underlying = plan.Resolver.GetDefinedType(
                            namedAlternative.Declaration).UnderlyingType;
                        if (underlying is ExpressBoundSelectType nestedSelect)
                        {
                            allAlternativesClosed &= !nestedSelect.IsExtensible;
                            foreach (var nestedAlternative in plan.Resolver.GetSelectAlternatives(
                                         nestedSelect))
                            {
                                pendingAlternatives.Enqueue(nestedAlternative);
                            }

                            alternativeType = nestedSelect;
                            break;
                        }

                        alternativeType = underlying;
                    }

                    if (alternativeType is not ExpressBoundSelectType)
                    {
                        alternatives.Add(alternativeType is ExpressBoundNamedType namedLeaf
                            ? namedLeaf.Declaration
                            : alternative);
                    }
                }

                var qualifiedName = typeName.SourceText.Length >= 2
                    ? typeName.SourceText.Substring(1, typeName.SourceText.Length - 2)
                    : "";
                var selected = alternatives.SingleOrDefault(alternative => string.Equals(
                    alternative.DeclaringSchema.Name + "." + alternative.Name,
                    qualifiedName,
                    StringComparison.OrdinalIgnoreCase));
                if (selected is not null)
                {
                    if (narrowedReference is not null)
                    {
                        thenSelectNarrowings[narrowedReference] = selected;
                    }
                    else
                    {
                        thenPathNarrowings.Add(new(narrowedExpression, selected));
                    }

                    var remaining = alternatives.Where(alternative => alternative != selected).ToArray();
                    if (allAlternativesClosed && remaining.Length == 1)
                    {
                        if (narrowedReference is not null)
                        {
                            elseSelectNarrowings[narrowedReference] = remaining[0];
                        }
                        else
                        {
                            elsePathNarrowings.Add(new(narrowedExpression, remaining[0]));
                        }
                    }
                }
                else
                {
                    ExpressScalarKind? selectedScalarKind = qualifiedName.ToUpperInvariant() switch
                    {
                        "INTEGER" => ExpressScalarKind.Integer,
                        "REAL" => ExpressScalarKind.Real,
                        _ => null,
                    };
                    if (selectedScalarKind is not null
                        && narrowedReference is not null
                        && lexicalNames.TryGetValue(narrowedReference.Name, out var lexicalName))
                    {
                        var scalarAlternatives = new List<(ExpressBoundSymbol Alternative, ExpressBoundScalarType Type)>();
                        foreach (var alternative in alternatives)
                        {
                            ExpressBoundType terminalType = new ExpressBoundNamedType(
                                alternative,
                                narrowedSelect.Span);
                            while (terminalType is ExpressBoundNamedType namedTerminal
                                   && namedTerminal.Declaration.Kind != ExpressDeclarationKind.Entity)
                            {
                                terminalType = plan.Resolver.GetDefinedType(
                                    namedTerminal.Declaration).UnderlyingType;
                            }

                            if (terminalType is ExpressBoundScalarType scalarTerminal)
                            {
                                scalarAlternatives.Add((alternative, scalarTerminal));
                            }
                        }

                        var matching = scalarAlternatives
                            .Where(candidate => candidate.Type.Kind == selectedScalarKind
                                || (candidate.Type.Kind == ExpressScalarKind.Number
                                    && selectedScalarKind is ExpressScalarKind.Integer or ExpressScalarKind.Real))
                            .ToArray();
                        if (matching.Length > 0)
                        {
                            var terminalType = new ExpressBoundScalarType(
                                selectedScalarKind.Value,
                                constraintText: null,
                                isFixed: false,
                                narrowedSelect.Span);
                            thenScalarNarrowings[narrowedReference.Name] = (
                                lexicalName.Code,
                                ResolveReference(
                                    plan,
                                    narrowedReference,
                                    selfExpression: null,
                                    "entities",
                                    lexicalNames,
                                    selectNarrowings,
                                    lexicalName.Type,
                                    lexicalName.Code,
                                    narrowedScalarType: terminalType),
                                terminalType);
                        }

                        var remaining = scalarAlternatives
                            .Select(candidate => candidate.Type.Kind switch
                            {
                                var kind when kind == selectedScalarKind => (ExpressScalarKind?)null,
                                ExpressScalarKind.Number when selectedScalarKind == ExpressScalarKind.Real =>
                                    ExpressScalarKind.Integer,
                                ExpressScalarKind.Number when selectedScalarKind == ExpressScalarKind.Integer =>
                                    ExpressScalarKind.Real,
                                var kind => kind,
                            })
                            .Where(kind => kind is not null)
                            .ToArray();
                        if (allAlternativesClosed
                            && scalarAlternatives.Count == alternatives.Count
                            && remaining.Length > 0
                            && remaining.All(kind => kind == remaining[0]))
                        {
                            var terminalType = new ExpressBoundScalarType(
                                remaining[0]!.Value,
                                constraintText: null,
                                isFixed: false,
                                narrowedSelect.Span);
                            elseScalarNarrowings[narrowedReference.Name] = (
                                lexicalName.Code,
                                ResolveReference(
                                    plan,
                                    narrowedReference,
                                    selfExpression: null,
                                    "entities",
                                    lexicalNames,
                                    selectNarrowings,
                                    lexicalName.Type,
                                    lexicalName.Code,
                                    narrowedScalarType: terminalType),
                                terminalType);
                        }
                    }
                }
            }
        }

        var sizeOf = boundCondition.Children.Count == 2
            ? boundCondition.Children[0]
            : null;
        if (boundCondition.Operation == ">"
            && boundCondition.Children.Count == 2
            && sizeOf?.Kind == ExpressExpressionKind.Application
            && sizeOf.Operation == "SIZEOF"
            && sizeOf.Children.Count == 1
            && sizeOf.Children[0].Kind == ExpressExpressionKind.Reference
            && sizeOf.Children[0].Reference is { } determinateReference
            && boundCondition.Children[1].Kind == ExpressExpressionKind.Literal
            && System.Numerics.BigInteger.TryParse(
                boundCondition.Children[1].SourceText,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var guardBound)
            && guardBound == System.Numerics.BigInteger.Zero)
        {
            thenDeterminateLexicals.Add(determinateReference);
        }

        var conditionalStatement = new IfStatement(new CustomExpression(conditionCode));
        var thenOwner = new IfStatement(new CustomExpression("true"));
        var thenFallsThrough = true;
        foreach (var nested in thenStatements)
        {
            if (!thenFallsThrough)
            {
                break;
            }

            thenFallsThrough = EmitFunctionStatement(
                plan,
                nested,
                functionResultType,
                canReturnIndeterminate,
                thenOwner,
                thenLexicalNames,
                allocateTemporaryName,
                thenSafeIndices,
                thenAliases,
                thenSelectNarrowings,
                thenPathNarrowings,
                thenDeterminateLexicals,
                thenSafeIndexPaths,
                thenScalarNarrowings);
        }

        foreach (var thenStatement in thenOwner.Statements)
        {
            conditionalStatement.AddStatement(thenStatement);
        }

        if (elseStatements.Count == 0)
        {
            var implicitElseScalarNarrowings = scalarNarrowings?.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase)
                ?? new(
                    StringComparer.OrdinalIgnoreCase);
            if (thenFallsThrough)
            {
                IntersectDictionaryFacts(
                    scalarNarrowings,
                    static (left, right) => string.Equals(
                            left.StorageCode,
                            right.StorageCode,
                            StringComparison.Ordinal)
                        && string.Equals(left.Code, right.Code, StringComparison.Ordinal)
                        && ReferenceEquals(left.Type, right.Type),
                    thenScalarNarrowings,
                    implicitElseScalarNarrowings);
                IntersectCollectionFacts(
                    safeIndices,
                    static (left, right) => ReferenceEquals(left.Key, right.Key)
                        && ReferenceEquals(left.Value, right.Value),
                    thenSafeIndices,
                    safeIndices?.ToList() ?? []);
                IntersectDictionaryFacts(
                    sizeAliases,
                    static (left, right) => ReferenceEquals(left, right),
                    thenAliases ?? [],
                    sizeAliases?.ToDictionary(pair => pair.Key, pair => pair.Value)
                        ?? []);
                JoinSelectFacts(
                    selectNarrowings,
                    thenSelectNarrowings,
                    selectNarrowings?.ToDictionary(pair => pair.Key, pair => pair.Value)
                        ?? []);
                IntersectCollectionFacts(
                    pathNarrowings,
                    static (left, right) => SameDirectReferencePath(left.Key, right.Key)
                        && ReferenceEquals(left.Value, right.Value),
                    thenPathNarrowings,
                    pathNarrowings?.ToList() ?? []);
                IntersectCollectionFacts(
                    determinateLexicals,
                    static (left, right) => ReferenceEquals(left, right),
                    thenDeterminateLexicals,
                    determinateLexicals?.ToArray() ?? Array.Empty<ExpressBoundName>());
                IntersectCollectionFacts(
                    safeIndexPaths,
                    static (left, right) => ReferenceEquals(left.Key, right.Key)
                        && ReferenceEquals(left.Value, right.Value),
                    thenSafeIndexPaths,
                    safeIndexPaths?.ToList() ?? []);
            }

            owner.AddStatement(conditionalStatement);
            return true;
        }

        var elseAliases = sizeAliases is null
            ? null
            : new Dictionary<ExpressBoundName, ExpressBoundName>(sizeAliases);
        var elseSafeIndices = safeIndices?.ToList() ?? [];
        var elseOwner = new IfStatement(new CustomExpression("true"));
        var elseFallsThrough = true;
        foreach (var nested in elseStatements)
        {
            if (!elseFallsThrough)
            {
                break;
            }

            elseFallsThrough = EmitFunctionStatement(
                plan,
                nested,
                functionResultType,
                canReturnIndeterminate,
                elseOwner,
                elseLexicalNames,
                allocateTemporaryName,
                elseSafeIndices,
                elseAliases,
                elseSelectNarrowings,
                elsePathNarrowings,
                elseDeterminateLexicals,
                elseSafeIndexPaths,
                elseScalarNarrowings);
        }

        var elseStatement = conditionalStatement.Else();
        foreach (var nestedElseStatement in elseOwner.Statements)
        {
            elseStatement.AddStatement(nestedElseStatement);
        }

        if (thenFallsThrough || elseFallsThrough)
        {
            var leftScalarNarrowings = thenFallsThrough
                ? thenScalarNarrowings
                : elseScalarNarrowings;
            var rightScalarNarrowings = elseFallsThrough
                ? elseScalarNarrowings
                : thenScalarNarrowings;
            var leftSafeIndices = thenFallsThrough ? thenSafeIndices : elseSafeIndices;
            var rightSafeIndices = elseFallsThrough ? elseSafeIndices : thenSafeIndices;
            var leftAliases = thenFallsThrough ? thenAliases : elseAliases;
            var rightAliases = elseFallsThrough ? elseAliases : thenAliases;
            var leftSelectNarrowings = thenFallsThrough ? thenSelectNarrowings : elseSelectNarrowings;
            var rightSelectNarrowings = elseFallsThrough ? elseSelectNarrowings : thenSelectNarrowings;
            var leftPathNarrowings = thenFallsThrough ? thenPathNarrowings : elsePathNarrowings;
            var rightPathNarrowings = elseFallsThrough ? elsePathNarrowings : thenPathNarrowings;
            var leftDeterminateLexicals = thenFallsThrough
                ? thenDeterminateLexicals
                : elseDeterminateLexicals;
            var rightDeterminateLexicals = elseFallsThrough
                ? elseDeterminateLexicals
                : thenDeterminateLexicals;
            var leftSafeIndexPaths = thenFallsThrough ? thenSafeIndexPaths : elseSafeIndexPaths;
            var rightSafeIndexPaths = elseFallsThrough ? elseSafeIndexPaths : thenSafeIndexPaths;
            IntersectDictionaryFacts(
                scalarNarrowings,
                static (left, right) => string.Equals(
                        left.StorageCode,
                        right.StorageCode,
                        StringComparison.Ordinal)
                    && string.Equals(left.Code, right.Code, StringComparison.Ordinal)
                    && ReferenceEquals(left.Type, right.Type),
                leftScalarNarrowings,
                rightScalarNarrowings);
            IntersectCollectionFacts(
                safeIndices,
                static (left, right) => ReferenceEquals(left.Key, right.Key)
                    && ReferenceEquals(left.Value, right.Value),
                leftSafeIndices,
                rightSafeIndices);
            IntersectDictionaryFacts(
                sizeAliases,
                static (left, right) => ReferenceEquals(left, right),
                leftAliases ?? [],
                rightAliases ?? []);
            JoinSelectFacts(
                selectNarrowings,
                leftSelectNarrowings,
                rightSelectNarrowings);
            IntersectCollectionFacts(
                pathNarrowings,
                static (left, right) => SameDirectReferencePath(left.Key, right.Key)
                    && ReferenceEquals(left.Value, right.Value),
                leftPathNarrowings,
                rightPathNarrowings);
            IntersectCollectionFacts(
                determinateLexicals,
                static (left, right) => ReferenceEquals(left, right),
                leftDeterminateLexicals,
                rightDeterminateLexicals);
            IntersectCollectionFacts(
                safeIndexPaths,
                static (left, right) => ReferenceEquals(left.Key, right.Key)
                    && ReferenceEquals(left.Value, right.Value),
                leftSafeIndexPaths,
                rightSafeIndexPaths);
        }

        owner.AddStatement(conditionalStatement);
        return thenFallsThrough || elseFallsThrough;
    }

    private static Method CreateDerivedMethod(
        ExpressReachableRulePlan plan,
        ExpressBoundAttribute attribute,
        ExpressGeneratedTypeResolver resolver)
    {
        var owner = plan.GetAttributeOwner(attribute);
        var ownerName = ExpressEntityProjection.ToPascalCase(owner.Name);
        var expression = plan.GetDerivedExpression(attribute);
        var returnType = resolver.Resolve(plan.Schema.Identity, attribute.Type).DataType;
        if (expression.Type.CanBeIndeterminate)
        {
            returnType = returnType.Null;
        }

        var method = CreateMethod(
            DerivedMethodName(owner, attribute),
            returnType);
        method.AddParameter(SourceComposer.Parameter(
            new DataType($"I{ownerName}"),
            "value"));
        var temporaryOrdinal = 0;
        Func<string, string> allocateTemporaryName = prefix => prefix
            + (temporaryOrdinal++).ToString(CultureInfo.InvariantCulture);
        AddPopulationParameter(method);
        var generated = ExpressExpressionEmitter.Emit(
            expression,
            CreateContext(
                plan,
                "value",
                "entities",
                allocateTemporaryName: allocateTemporaryName));
        var generatedCode = generated.Code;
        if (attribute.Type is ExpressBoundNamedType
            { Declaration.Kind: not ExpressDeclarationKind.Entity, }
            && expression.Kind != ExpressExpressionKind.Indeterminate)
        {
            var targetType = attribute.Type;
            var definedTypes = new List<ExpressBoundNamedType>();
            while (targetType is ExpressBoundNamedType definedType
                   && definedType.Declaration.Kind != ExpressDeclarationKind.Entity)
            {
                definedTypes.Add(definedType);
                targetType = plan.Resolver.GetDefinedType(definedType.Declaration).UnderlyingType;
            }

            var actualNominal = expression.Type.DeclaredType as ExpressBoundNamedType;
            var primitiveActual = actualNominal is null;
            var sameDefinedActual = definedTypes.Count > 0
                && actualNominal is not null
                && ReferenceEquals(actualNominal.Declaration, definedTypes[0].Declaration);
            var exactScalar = targetType is ExpressBoundScalarType targetScalar
                && targetScalar.Kind switch
                {
                    ExpressScalarKind.Binary => expression.Type.Kind == ExpressExpressionTypeKind.Binary,
                    ExpressScalarKind.Boolean => expression.Type.Kind == ExpressExpressionTypeKind.Boolean,
                    ExpressScalarKind.Integer => expression.Type.Kind == ExpressExpressionTypeKind.Integer,
                    ExpressScalarKind.Logical => expression.Type.Kind == ExpressExpressionTypeKind.Logical,
                    ExpressScalarKind.Number => expression.Type.Kind == ExpressExpressionTypeKind.Number,
                    ExpressScalarKind.Real => expression.Type.Kind == ExpressExpressionTypeKind.Real,
                    ExpressScalarKind.String => expression.Type.Kind == ExpressExpressionTypeKind.String,
                    _ => false,
                };
            if ((primitiveActual || sameDefinedActual) && exactScalar)
            {
                if (expression.Type.CanBeIndeterminate)
                {
                    var presentValue = allocateTemporaryName("__expressDerivedValue");
                    var presentCode = presentValue;
                    for (var definedIndex = definedTypes.Count - 1; definedIndex >= 0; definedIndex--)
                    {
                        presentCode = "new "
                            + ExpressExpressionEmitter.BoundTypeName(definedTypes[definedIndex])
                            + $"({presentCode})";
                    }

                    generatedCode = $"(({generatedCode}) is {{ }} {presentValue} ? {presentCode} : ("
                        + ExpressExpressionEmitter.BoundTypeName(attribute.Type)
                        + "?)null)";
                }
                else
                {
                    for (var definedIndex = definedTypes.Count - 1; definedIndex >= 0; definedIndex--)
                    {
                        generatedCode = "new "
                            + ExpressExpressionEmitter.BoundTypeName(definedTypes[definedIndex])
                            + $"({generatedCode})";
                    }
                }
            }
        }

        method.AddStatement(new CustomExpression(generatedCode).Return);
        AddSummary(method, $"Evaluates reachable derived attribute {owner.Name}.{attribute.Name}.");
        return method;
    }

    private static IEnumerable<Method> CreateModelMethods(
        ExpressReachableRulePlan plan,
        ExpressGeneratedTypeResolver resolver,
        IReadOnlyList<ExpressEntityProjection> entities)
    {
        yield return CreateTypeOfMethod(plan, entities);
        yield return CreateUsesRoleMethod(plan, resolver, entities);
        yield return CreateUsedInMethod();
        yield return CreateRolesOfMethod(plan, entities);
        foreach (var inverse in plan.Schema.Declarations
                     .OfType<ExpressBoundEntity>()
                     .SelectMany(entity => entity.Attributes
                         .Where(attribute => attribute.Kind == ExpressAttributeKind.Inverse)
                         .Select(attribute => (Entity: entity, Attribute: attribute)))
                     .Where(candidate => candidate.Attribute.Type is ExpressBoundAggregateType))
        {
            yield return CreateInverseMethod(plan, inverse.Entity, inverse.Attribute, resolver);
        }
    }

    private static Method CreateTypeOfMethod(
        ExpressReachableRulePlan plan,
        IReadOnlyList<ExpressEntityProjection> entities)
    {
        var method = CreateMethod(
            "__ExpressTypeOf",
            new DataType("global::TedToolkit.Step21.ExpressSet<global::System.String>"));
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::TedToolkit.Step21.Entity"),
            "candidate"));
        method.AddStatement(new CustomExpression(
            "var result = new global::TedToolkit.Step21.ExpressSet<global::System.String>(0)"));
        foreach (var entity in entities)
        {
            var schemaType = $"{plan.Schema.Name}.{entity.Entity.Name}".ToUpperInvariant();
            method.AddStatement(new IfStatement(new CustomExpression($"candidate is I{entity.Name}"))
                .AddStatement(new CustomExpression($"result.Add(\"{schemaType}\")")));
        }

        method.AddStatement(new CustomExpression("result").Return);
        return method;
    }

    private static Method CreateUsesRoleMethod(
        ExpressReachableRulePlan plan,
        ExpressGeneratedTypeResolver resolver,
        IReadOnlyList<ExpressEntityProjection> entities)
    {
        var method = CreateMethod("__ExpressUsesRole", DataType.Bool);
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::TedToolkit.Step21.Entity"),
            "owner"));
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::TedToolkit.Step21.Entity"),
            "candidate"));
        method.AddParameter(SourceComposer.Parameter(DataType.String, "role"));
        foreach (var entity in entities)
        {
            for (var attributeIndex = 0; attributeIndex < entity.OwnAttributes.Count; attributeIndex++)
            {
                var attribute = entity.OwnAttributes[attributeIndex];
                var typedName = $"typed{entity.Name}"
                    + attributeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var role = $"{plan.Schema.Name}.{entity.Entity.Name}.{attribute.Attribute.Name}"
                    .ToUpperInvariant();
                var references = ExpressDirectReferenceExpression.Create(
                    attribute.Type,
                    $"{typedName}.{attribute.StorageMemberName}",
                    resolver);
                var roleMatches = "(role.Length == 0 || global::System.String.Equals("
                    + $"role, \"{role}\", global::System.StringComparison.OrdinalIgnoreCase))";
                var contains = "global::System.Linq.Enumerable.Any("
                    + $"{references}, reference => global::System.Object.ReferenceEquals(reference, candidate))";
                method.AddStatement(new IfStatement(new CustomExpression(
                        $"owner is I{entity.Name} {typedName} && {roleMatches} && {contains}"))
                    .AddStatement(true.ToLiteral().Return));
            }
        }

        method.AddStatement(false.ToLiteral().Return);
        return method;
    }

    private static Method CreateUsedInMethod()
    {
        var method = CreateMethod(
            "__ExpressUsedIn",
            new DataType("global::TedToolkit.Step21.ExpressBag<TEntity>"));
        method.AddTypeParameter(SourceComposer.TypeParameter("TEntity"));
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::TedToolkit.Step21.Entity"),
            "candidate"));
        method.AddParameter(SourceComposer.Parameter(DataType.String, "role"));
        AddPopulationParameter(method);
        method.AddStatement(new CustomExpression(
            "var result = new global::TedToolkit.Step21.ExpressBag<TEntity>(0)"));
        var loop = new ForEachStatement(DataType.Var, "entry", new CustomExpression("entities"));
        loop.AddStatement(new IfStatement(new CustomExpression(
                "__ExpressUsesRole(entry.Value, candidate, role) && entry.Value is TEntity typedEntry"))
            .AddStatement(new CustomExpression("result.Add(typedEntry)")));
        method.AddStatement(loop);
        method.AddStatement(new CustomExpression("result").Return);
        return method;
    }

    private static Method CreateRolesOfMethod(
        ExpressReachableRulePlan plan,
        IReadOnlyList<ExpressEntityProjection> entities)
    {
        var method = CreateMethod(
            "__ExpressRolesOf",
            new DataType("global::TedToolkit.Step21.ExpressSet<global::System.String>"));
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::TedToolkit.Step21.Entity"),
            "candidate"));
        AddPopulationParameter(method);
        method.AddStatement(new CustomExpression(
            "var result = new global::TedToolkit.Step21.ExpressSet<global::System.String>(0)"));
        foreach (var role in entities
                     .SelectMany(entity => entity.OwnAttributes.Select(attribute =>
                         $"{plan.Schema.Name}.{entity.Entity.Name}.{attribute.Attribute.Name}".ToUpperInvariant()))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(role => role, StringComparer.Ordinal))
        {
            method.AddStatement(new IfStatement(new CustomExpression(
                    $"__ExpressUsedIn<global::TedToolkit.Step21.Entity>(candidate, \"{role}\", entities).Count > 0"))
                .AddStatement(new CustomExpression($"result.Add(\"{role}\")")));
        }

        method.AddStatement(new CustomExpression("result").Return);
        return method;
    }

    private static Method CreateInverseMethod(
        ExpressReachableRulePlan plan,
        ExpressBoundEntity owner,
        ExpressBoundAttribute attribute,
        ExpressGeneratedTypeResolver resolver)
    {
        var aggregate = (ExpressBoundAggregateType)attribute.Type;
        var returnType = resolver.Resolve(plan.Schema.Identity, aggregate).DataType;
        var returnTypeName = ExpressExpressionEmitter.BoundTypeName(aggregate);
        var elementTypeName = ExpressExpressionEmitter.BoundTypeName(aggregate.ElementType);
        var method = CreateMethod(InverseMethodName(owner, attribute), returnType);
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::TedToolkit.Step21.Entity"),
            "candidate"));
        AddPopulationParameter(method);
        var role = $"{plan.Schema.Name}.{attribute.InverseEntity!.Name}.{attribute.InverseAttributeName}"
            .ToUpperInvariant();
        method.AddStatement(new CustomExpression($"var result = new {returnTypeName}(0)"));
        var loop = new ForEachStatement(
            DataType.Var,
            "user",
            new CustomExpression(
                $"__ExpressUsedIn<global::TedToolkit.Step21.Entity>(candidate, \"{role}\", entities)"));
        loop.AddStatement(new IfStatement(new CustomExpression($"user is {elementTypeName} typedUser"))
            .AddStatement(new CustomExpression("result.Add(typedUser)")));
        method.AddStatement(loop);
        method.AddStatement(new CustomExpression("result").Return);
        return method;
    }

    private static void AddPopulationParameter(Method method)
    {
        method.AddParameter(SourceComposer.Parameter(new DataType(POPULATION_TYPE), "entities"));
    }

    private static string ResolveModelFunction(
        ExpressReachableRulePlan plan,
        string operation,
        ExpressBoundExpression expression,
        IReadOnlyList<string> arguments,
        string populationExpression,
        ExpressBoundType? usedInCarrierType = null,
        int usedInDepth = 0,
        ExpressBoundType? aggregateUnionSourceType = null,
        ExpressBoundType? aggregateUnionTargetType = null,
        int aggregateUnionDepth = 0)
    {
        if (operation == "AGGREGATE_UNION_ELEMENT")
        {
            string? entityTargetType = null;
            if (aggregateUnionTargetType is ExpressBoundGenericType { IsEntity: true, })
            {
                if (aggregateUnionSourceType is ExpressBoundGenericType { IsEntity: true, })
                {
                    return arguments[0];
                }

                if (aggregateUnionSourceType is not ExpressBoundNamedType
                    { Declaration.Kind: ExpressDeclarationKind.Entity, })
                {
                    throw new InvalidOperationException(
                        "Aggregate entity union requires an entity source element.");
                }

                entityTargetType = "global::TedToolkit.Step21.Entity";
            }

            if (aggregateUnionSourceType is not ExpressBoundNamedType source
                || (aggregateUnionTargetType is not ExpressBoundNamedType
                    && entityTargetType is null))
            {
                throw new InvalidOperationException(
                    "Aggregate SELECT union elements require named source and target types.");
            }

            var target = aggregateUnionTargetType as ExpressBoundNamedType;
            if (source.Declaration.Kind == ExpressDeclarationKind.Entity && target is not null)
            {
                var sourceProjection = plan.EntityProjections.Single(projection =>
                    projection.Entity.Symbol == source.Declaration);
                if (target.Declaration.Kind == ExpressDeclarationKind.Entity)
                {
                    if (!sourceProjection.PhysicalComponents.Any(component =>
                            component.Symbol == target.Declaration))
                    {
                        throw new InvalidOperationException(
                            "Aggregate SELECT union entity alternative is incompatible with the result element type.");
                    }

                    if (source.Declaration == target.Declaration)
                    {
                        return arguments[0];
                    }

                    entityTargetType = ExpressExpressionEmitter.BoundTypeName(target);
                }
                else
                {
                    var targetDefined = plan.Resolver.GetDefinedType(target.Declaration).UnderlyingType;
                    if (targetDefined is not ExpressBoundSelectType targetSelect)
                    {
                        throw new InvalidOperationException(
                            "Aggregate union adapts entity elements only to compatible sole-alternative SELECT targets.");
                    }

                    var targetAlternatives = plan.Resolver.GetSelectAlternatives(targetSelect)
                        .Where(alternative => alternative.Kind == ExpressDeclarationKind.Entity
                            && sourceProjection.PhysicalComponents.Any(component =>
                                component.Symbol == alternative))
                        .ToArray();
                    if (targetAlternatives.Length != 1
                        || plan.Resolver.GetSelectAlternatives(targetSelect).Count != 1)
                    {
                        throw new InvalidOperationException(
                            "Aggregate union SELECT target must have exactly one compatible entity alternative.");
                    }

                    return ExpressExpressionEmitter.BoundTypeName(target)
                        + ".From"
                        + ExpressEntityProjection.ToPascalCase(targetAlternatives[0].Name)
                        + $"({arguments[0]})";
                }
            }

            if (entityTargetType is not null)
            {
                var entity = "__expressAggregateUnionEntity"
                    + aggregateUnionDepth.ToString(CultureInfo.InvariantCulture);
                return $"({arguments[0]}) switch {{ {entityTargetType} {entity} => {entity}, "
                    + "_ => throw new global::System.InvalidOperationException() }";
            }

            if (target is null)
            {
                throw new InvalidOperationException(
                    "Aggregate SELECT union elements require named source and target types.");
            }

            var underlying = plan.Resolver.GetDefinedType(source.Declaration).UnderlyingType;
            if (underlying is ExpressBoundNamedType namedUnderlying)
            {
                return ResolveModelFunction(
                    plan,
                    operation,
                    expression,
                    [$"({arguments[0]}).Value",],
                    populationExpression,
                    aggregateUnionSourceType: namedUnderlying,
                    aggregateUnionTargetType: target,
                    aggregateUnionDepth: aggregateUnionDepth);
            }

            if (underlying is not ExpressBoundSelectType select)
            {
                throw new InvalidOperationException(
                    "Aggregate union adapts only sole-alternative entity SELECT elements.");
            }

            var alternatives = plan.Resolver.GetSelectAlternatives(select);
            if (alternatives.Count != 1)
            {
                throw new InvalidOperationException(
                    "Aggregate union SELECT element must have exactly one alternative.");
            }

            var selected = "__expressAggregateUnionSelected"
                + aggregateUnionDepth.ToString(CultureInfo.InvariantCulture);
            return $"({arguments[0]}).Match({selected} => "
                + ResolveModelFunction(
                    plan,
                    operation,
                    expression,
                    [selected,],
                    populationExpression,
                    aggregateUnionSourceType: new ExpressBoundNamedType(alternatives[0], select.Span),
                    aggregateUnionTargetType: target,
                    aggregateUnionDepth: aggregateUnionDepth + 1)
                + ")";
        }

        if (operation == "AGGREGATE_UNION")
        {
            var resultAggregate = expression.Type.DeclaredType as ExpressBoundAggregateType;
            var operandAggregates = expression.Children
                .Select(child => child.Type.DeclaredType as ExpressBoundAggregateType)
                .ToArray();
            if (resultAggregate is not { IsOptional: false, }
                || resultAggregate.Kind is not (ExpressAggregateKind.List
                    or ExpressAggregateKind.Bag
                    or ExpressAggregateKind.Set)
                || operandAggregates.All(aggregate => aggregate is null)
                || operandAggregates.Any(aggregate => aggregate is { IsOptional: true, }))
            {
                throw new InvalidOperationException(
                    "Aggregate union requires at least one non-optional LIST, BAG, or SET operand.");
            }

            var target = resultAggregate.ElementType;
            var values = new string[2];
            for (var index = 0; index < operandAggregates.Length; index++)
            {
                var source = operandAggregates[index]?.ElementType
                    ?? expression.Children[index].Type.DeclaredType;
                if (source is null)
                {
                    throw new InvalidOperationException(
                        "Aggregate entity union requires typed operands.");
                }

                if ((source is ExpressBoundNamedType namedSource
                        && target is ExpressBoundNamedType namedTarget
                        && namedSource.Declaration == namedTarget.Declaration)
                    || (source is ExpressBoundGenericType { IsEntity: true, }
                        && target is ExpressBoundGenericType { IsEntity: true, }))
                {
                    values[index] = arguments[index];
                    continue;
                }

                var item = "__expressAggregateUnionItem"
                    + index.ToString(CultureInfo.InvariantCulture);
                var adapted = ResolveModelFunction(
                        plan,
                        "AGGREGATE_UNION_ELEMENT",
                        expression,
                        [operandAggregates[index] is null ? arguments[index] : item,],
                        populationExpression,
                        aggregateUnionSourceType: source,
                        aggregateUnionTargetType: target,
                        aggregateUnionDepth: index);
                values[index] = operandAggregates[index] is null
                    ? adapted
                    : "global::System.Linq.Enumerable.Select("
                        + $"({arguments[index]}), {item} => {adapted})";
            }

            var combined = (operandAggregates[0] is not null, operandAggregates[1] is not null) switch
            {
                (true, true) => $"global::System.Linq.Enumerable.Concat({values[0]}, {values[1]})",
                (true, false) => $"global::System.Linq.Enumerable.Append({values[0]}, {values[1]})",
                (false, true) => $"global::System.Linq.Enumerable.Prepend({values[1]}, {values[0]})",
                _ => throw new InvalidOperationException(
                    "Aggregate union requires at least one aggregate operand."),
            };
            var distinct = resultAggregate.Kind == ExpressAggregateKind.Set
                ? $"global::System.Linq.Enumerable.Distinct({combined})"
                : combined;
            return $"({ExpressExpressionEmitter.BoundTypeName(resultAggregate)})[..{distinct}]";
        }

        var usedInElementType = expression.Type.DeclaredType is ExpressBoundAggregateType aggregate
            ? ExpressExpressionEmitter.BoundTypeName(aggregate.ElementType)
            : "global::TedToolkit.Step21.Entity";
        if (operation == "USEDIN")
        {
            var carrierType = usedInCarrierType ?? expression.Children[0].Type.DeclaredType;
            var carrier = arguments[0];
            while (carrierType is ExpressBoundNamedType namedCarrier
                   && namedCarrier.Declaration.Kind != ExpressDeclarationKind.Entity)
            {
                var underlying = plan.Resolver.GetDefinedType(namedCarrier.Declaration).UnderlyingType;
                if (underlying is ExpressBoundNamedType)
                {
                    carrier = $"({carrier}).Value";
                }

                carrierType = underlying;
            }

            if (carrierType is ExpressBoundSelectType select)
            {
                var alternatives = plan.Resolver.GetSelectAlternatives(select);
                var branches = alternatives.Select((alternative, index) =>
                {
                    var selected = "__expressUsedInCarrier"
                        + usedInDepth.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + index.ToString(CultureInfo.InvariantCulture);
                    return selected
                        + " => "
                        + ResolveModelFunction(
                            plan,
                            operation,
                            expression,
                            [selected, arguments[1],],
                            populationExpression,
                            new ExpressBoundNamedType(alternative, select.Span),
                            usedInDepth + 1);
                });
                return $"({carrier}).Match({string.Join(", ", branches)})";
            }

            if (!(carrierType is ExpressBoundNamedType namedEntityCarrier
                    && namedEntityCarrier.Declaration.Kind == ExpressDeclarationKind.Entity)
                && carrierType is not ExpressBoundGenericType { IsEntity: true, })
            {
                throw new InvalidOperationException(
                    "USEDIN requires an entity or a SELECT whose complete alternatives are entities.");
            }

            return $"__ExpressUsedIn<{usedInElementType}>("
                + $"(global::TedToolkit.Step21.Entity)({carrier}), {arguments[1]}, {populationExpression})";
        }

        return operation switch
        {
            "COMPLEX_CONSTRUCTOR" => LowerComplexConstructor(plan, expression, arguments),
            "INSTANCE_EQUALITY" => ResolveValueEquality(
                plan,
                expression.Children[0],
                arguments[0],
                expression.Children[1],
                arguments[1],
                null,
                instanceEquality: true),
            "TYPEOF" => LowerTypeOf(
                plan,
                expression.Children[0].Type.DeclaredType,
                arguments[0]),
            "ROLESOF" => "__ExpressRolesOf("
                + $"(global::TedToolkit.Step21.Entity)({arguments[0]}), {populationExpression})",
            _ => throw new InvalidOperationException($"{operation}: {expression.SourceText}"),
        };
    }

    private static string LowerTypeOf(
        ExpressReachableRulePlan plan,
        ExpressBoundType? type,
        string value)
    {
        var names = new List<string>();
        while (type is ExpressBoundNamedType named)
        {
            if (named.Declaration.Kind == ExpressDeclarationKind.Entity)
            {
                return $"__ExpressTypeOf((global::TedToolkit.Step21.Entity)({value}))";
            }

            var defined = plan.Resolver.GetDefinedType(named.Declaration);
            if (defined.UnderlyingType is ExpressBoundSelectType select)
            {
                var branches = plan.Resolver.GetSelectAlternatives(select)
                    .Select((alternative, index) =>
                    {
                        var selected = "__expressTypeOf"
                            + ExpressEntityProjection.ToPascalCase(alternative.Name)
                            + index.ToString(CultureInfo.InvariantCulture);
                        var selectedType = new ExpressBoundNamedType(alternative, type.Span);
                        return $"{selected} => {LowerTypeOf(plan, selectedType, selected)}";
                    });
                return $"({value}).Match({string.Join(", ", branches)})";
            }

            if (defined.UnderlyingType is ExpressBoundAggregateType aggregate)
            {
                type = aggregate;
                break;
            }

            names.Add($"{named.Declaration.DeclaringSchema.Name}.{named.Declaration.Name}".ToUpperInvariant());
            value = $"({value}).Value";
            type = defined.UnderlyingType;
        }

        if (type is ExpressBoundScalarType scalar)
        {
            if (scalar.Kind == ExpressScalarKind.Number)
            {
                var declaredNames = names.Append("NUMBER").Distinct(StringComparer.Ordinal).ToArray();
                var integerItems = string.Join(", ", declaredNames.Append("INTEGER")
                    .Distinct(StringComparer.Ordinal)
                    .Select(name => $"\"{name}\""));
                var realItems = string.Join(", ", declaredNames.Append("REAL")
                    .Distinct(StringComparer.Ordinal)
                    .Select(name => $"\"{name}\""));
                return $"({value}).Kind switch {{ "
                    + "global::TedToolkit.Step21.NumberValueKind.Integer => "
                    + "new global::TedToolkit.Step21.ExpressSet<global::System.String>(0) "
                    + $"{{ {integerItems} }}, "
                    + "global::TedToolkit.Step21.NumberValueKind.Real => "
                    + "new global::TedToolkit.Step21.ExpressSet<global::System.String>(0) "
                    + $"{{ {realItems} }}, "
                    + "_ => new global::TedToolkit.Step21.ExpressSet<global::System.String>(0) }";
            }

            names.AddRange(scalar.Kind switch
            {
                ExpressScalarKind.Integer => ["INTEGER", "NUMBER",],
                ExpressScalarKind.Real => ["REAL", "NUMBER",],
                ExpressScalarKind.Boolean => ["BOOLEAN", "LOGICAL",],
                ExpressScalarKind.Binary => ["BINARY",],
                ExpressScalarKind.Logical => ["LOGICAL",],
                ExpressScalarKind.String => ["STRING",],
                _ => throw new InvalidOperationException(
                    $"Unsupported TYPEOF scalar kind '{scalar.Kind.ToString()}'."),
            });
        }
        else if (type is ExpressBoundAggregateType aggregate)
        {
            var kind = aggregate.Kind switch
            {
                ExpressAggregateKind.Array => "ARRAY",
                ExpressAggregateKind.Bag => "BAG",
                ExpressAggregateKind.List => "LIST",
                ExpressAggregateKind.Set => "SET",
                _ => throw new InvalidOperationException(
                    $"Unsupported TYPEOF aggregate kind '{aggregate.Kind.ToString()}'."),
            };
            return "new global::TedToolkit.Step21.ExpressSet<global::System.String>(0)"
                + $" {{ \"{kind}\" }}";
        }

        if (names.Count > 0)
        {
            var items = string.Join(", ", names.Distinct(StringComparer.Ordinal).Select(name => $"\"{name}\""));
            return "new global::TedToolkit.Step21.ExpressSet<global::System.String>(0)"
                + $" {{ {items} }}";
        }

        return $"__ExpressTypeOf((global::TedToolkit.Step21.Entity)({value}))";
    }

    private static string LowerComplexConstructor(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression expression,
        IReadOnlyList<string> arguments)
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

        var targetType = (ExpressBoundNamedType)expression.Type.DeclaredType!;
        var target = plan.EntityProjections.Single(projection => ReferenceEquals(
            projection.Entity.Symbol,
            targetType.Declaration));
        var variables = Enumerable.Range(0, arguments.Count)
            .Select(index => $"__complexArgument{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}")
            .ToArray();
        var supplied = new Dictionary<(ExpressBoundSymbol Entity, string Attribute), string>();
        var argumentIndex = 0;
        foreach (var component in components)
        {
            if (component.Kind == ExpressExpressionKind.Application
                && component.Reference is
                {
                    Kind: ExpressBoundNameKind.Entity,
                    SchemaDeclaration: { } componentSymbol,
                })
            {
                var projection = plan.EntityProjections.Single(candidate => ReferenceEquals(
                    candidate.Entity.Symbol,
                    componentSymbol));
                for (var attributeIndex = 0; attributeIndex < projection.OwnAttributes.Count; attributeIndex++)
                {
                    var attribute = projection.OwnAttributes[attributeIndex];
                    var actual = component.Children[attributeIndex];
                    var value = variables[argumentIndex++];
                    var attributeType = attribute.Type;
                    var definedTypes = new List<ExpressBoundNamedType>();
                    while (attributeType is ExpressBoundNamedType definedType
                           && definedType.Declaration.Kind != ExpressDeclarationKind.Entity)
                    {
                        definedTypes.Add(definedType);
                        attributeType = plan.Resolver.GetDefinedType(definedType.Declaration).UnderlyingType;
                    }

                    var actualNominal = actual.Type.DeclaredType as ExpressBoundNamedType;
                    var primitiveActual = actualNominal is null;
                    var sameDefinedActual = definedTypes.Count > 0
                        && actualNominal is not null
                        && ReferenceEquals(actualNominal.Declaration, definedTypes[0].Declaration);
                    var exactScalar = attributeType is ExpressBoundScalarType targetScalar
                        && targetScalar.Kind switch
                        {
                            ExpressScalarKind.Binary => actual.Type.Kind == ExpressExpressionTypeKind.Binary,
                            ExpressScalarKind.Boolean => actual.Type.Kind == ExpressExpressionTypeKind.Boolean,
                            ExpressScalarKind.Integer => actual.Type.Kind == ExpressExpressionTypeKind.Integer,
                            ExpressScalarKind.Logical => actual.Type.Kind == ExpressExpressionTypeKind.Logical,
                            ExpressScalarKind.Number => actual.Type.Kind == ExpressExpressionTypeKind.Number,
                            ExpressScalarKind.Real => actual.Type.Kind == ExpressExpressionTypeKind.Real,
                            ExpressScalarKind.String => actual.Type.Kind == ExpressExpressionTypeKind.String,
                            _ => false,
                        };
                    var widensToReal = actual.Type.Kind == ExpressExpressionTypeKind.Integer
                        && attributeType is ExpressBoundScalarType { Kind: ExpressScalarKind.Real, };
                    if ((primitiveActual || sameDefinedActual)
                        && (exactScalar || widensToReal))
                    {
                        if (widensToReal)
                        {
                            value = ExpressExpressionEmitter.PromoteNumeric(
                                actual,
                                value,
                                ExpressExpressionTypeKind.Real);
                        }

                        for (var definedIndex = definedTypes.Count - 1; definedIndex >= 0; definedIndex--)
                        {
                            value = "new "
                                + ExpressExpressionEmitter.BoundTypeName(definedTypes[definedIndex])
                                + $"({value})";
                        }
                    }

                    supplied.Add(
                        (attribute.StorageEntity.Symbol, attribute.StorageAttributeName),
                        value);
                }

                continue;
            }

            var sourceType = (ExpressBoundNamedType)component.Type.DeclaredType!;
            var source = plan.EntityProjections.Single(candidate => ReferenceEquals(
                candidate.Entity.Symbol,
                sourceType.Declaration));
            var sourceVariable = variables[argumentIndex++];
            foreach (var attribute in source.EffectiveAttributes)
            {
                supplied.Add(
                    (attribute.StorageEntity.Symbol, attribute.StorageAttributeName),
                    $"({sourceVariable}).{attribute.StorageMemberName}");
            }
        }

        var constructorArguments = target.EffectiveAttributes
            .Where(attribute => !attribute.Attribute.IsOptional)
            .Select(attribute => supplied[(attribute.StorageEntity.Symbol, attribute.StorageAttributeName)]);
        var initializers = target.EffectiveAttributes
            .Where(attribute => attribute.Attribute.IsOptional
                && supplied.ContainsKey((attribute.StorageEntity.Symbol, attribute.StorageAttributeName)))
            .Select(attribute => $"{attribute.StorageMemberName} = "
                + supplied[(attribute.StorageEntity.Symbol, attribute.StorageAttributeName)])
            .ToArray();
        var construction = $"new {target.Name}({string.Join(", ", constructorArguments)})"
            + (initializers.Length == 0 ? "" : $" {{ {string.Join(", ", initializers)} }}");
        if (arguments.Count == 0)
        {
            return construction;
        }

        if (arguments.Count == 1)
        {
            return $"({arguments[0]}) switch {{ var {variables[0]} => {construction} }}";
        }

        return $"(({string.Join(", ", arguments.Select(argument => $"({argument})"))})) switch "
            + $"{{ var ({string.Join(", ", variables)}) => {construction} }}";
    }

    private static string FunctionInvocation(
        ExpressReachableRulePlan plan,
        ExpressBoundSymbol symbol,
        string populationExpression)
    {
        var declaration = (ExpressBoundOpaqueDeclaration)plan.GetDeclaration(symbol);
        var parameterTypes = declaration.Syntax.RequiredChild("functionHead")
            .ChildRules("formalParameter")
            .SelectMany(formal => formal.ChildRules("parameterId"))
            .Select(parameter => plan.Schema.NameReferences
                .Select(reference => reference.Target)
                .Where(candidate => candidate.Kind == ExpressBoundNameKind.Parameter
                    && SameStart(candidate.Span, parameter.Span))
                .Distinct()
                .Single())
            .Select(parameter => ExpressExpressionEmitter.BoundTypeName(parameter.Type!))
            .ToArray();
        var parameters = Enumerable.Range(0, parameterTypes.Length)
            .Select(index => $"__argument{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}")
            .ToArray();
        var invocationArguments = parameters.Append(populationExpression);
        var returnType = ExpressExpressionEmitter.BoundTypeName(declaration.DeclaredType!)
            + (plan.Schema.IndeterminateFunctions.Contains(symbol) ? "?" : "");
        var delegateTypes = parameterTypes.Append(returnType);
        return $"((global::System.Func<{string.Join(", ", delegateTypes)}>)("
            + $"({string.Join(", ", parameters)}) => "
            + $"{FunctionMethodName(symbol)}({string.Join(", ", invocationArguments)})))";
    }

    private static string ResolveApplication(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression expression,
        IReadOnlyList<string> arguments,
        string populationExpression,
        string? selfExpression,
        IReadOnlyDictionary<string, (string Code, ExpressBoundType Type)>? lexicalNames,
        IReadOnlyDictionary<ExpressBoundName, ExpressBoundSymbol>? selectNarrowings,
        IReadOnlyList<KeyValuePair<ExpressBoundExpression, ExpressBoundSymbol>>? pathNarrowings)
    {
        var symbol = expression.Reference!.SchemaDeclaration!;
        var declaration = plan.GetDeclaration(symbol);
        if (declaration is ExpressBoundOpaqueDeclaration genericFunction
            && ExpressExpressionEmitter.GenericTypeLabels([genericFunction.DeclaredType!,]).Count > 0)
        {
            return $"{FunctionMethodName(symbol)}({string.Join(", ", arguments.Append(populationExpression))})";
        }

        var emittedArguments = arguments.ToArray();
        ExpressBoundType[] formalTypes;
        string invocation;
        if (declaration is ExpressBoundEntity entity)
        {
            var projection = plan.EntityProjections.Single(candidate => candidate.Entity == entity);
            formalTypes = projection.EffectiveAttributes
                .Where(attribute => !attribute.Attribute.IsOptional)
                .Select(attribute => attribute.Type)
                .ToArray();
            invocation = $"new {projection.Name}";
        }
        else
        {
            var function = (ExpressBoundOpaqueDeclaration)declaration;
            formalTypes = function.Syntax.RequiredChild("functionHead")
                .ChildRules("formalParameter")
                .SelectMany(formal => formal.ChildRules("parameterId"))
                .Select(parameter => plan.Schema.NameReferences
                    .Select(reference => reference.Target)
                    .Where(candidate => candidate.Kind == ExpressBoundNameKind.Parameter
                        && SameStart(candidate.Span, parameter.Span))
                    .Distinct()
                    .Single()
                    .Type!)
                .ToArray();
            invocation = FunctionInvocation(plan, symbol, populationExpression);
        }

        var dynamicArguments = new List<(
            string Carrier,
            string Placeholder,
            IReadOnlyList<(string Pattern, string? Value)> Branches,
            bool UsesSelectMatch)>();
        if (formalTypes.Length == expression.Children.Count
            && formalTypes.Length == emittedArguments.Length)
        {
            for (var index = 0; index < formalTypes.Length; index++)
            {
                var targetType = formalTypes[index];
                var actual = expression.Children[index];
                var wasPathNarrowed = false;
                if (targetType is ExpressBoundNamedType
                    { Declaration.Kind: ExpressDeclarationKind.Entity, } formalEntity
                    && pathNarrowings is not null)
                {
                    var narrowedAlternatives = pathNarrowings
                        .Where(narrowing => SameDirectReferencePath(narrowing.Key, actual))
                        .Select(narrowing => narrowing.Value)
                        .Distinct()
                        .ToArray();
                    if (narrowedAlternatives.Length == 1
                        && narrowedAlternatives[0].Kind == ExpressDeclarationKind.Entity
                        && plan.EntityProjections.Single(projection =>
                                projection.Entity.Symbol == narrowedAlternatives[0])
                            .PhysicalComponents.Any(component =>
                                component.Symbol == formalEntity.Declaration))
                    {
                        var narrowedAlternative = narrowedAlternatives[0];
                        var pathReference = actual.DescendantsAndSelf()
                            .Select(candidate => candidate.Reference)
                            .First(reference => reference is not null)!;
                        emittedArguments[index] = ResolveReference(
                            plan,
                            pathReference,
                            selfExpression,
                            populationExpression,
                            lexicalNames,
                            selectNarrowings,
                            actual.Type.DeclaredType,
                            emittedArguments[index],
                            narrowedAlternative);
                        wasPathNarrowed = true;
                    }
                }

                if (!wasPathNarrowed
                    && targetType is ExpressBoundNamedType
                    { Declaration.Kind: ExpressDeclarationKind.Entity, } formalEntityTarget
                    && actual.Type.DeclaredType is ExpressBoundNamedType actualSelectName
                    && actualSelectName.Declaration.Kind != ExpressDeclarationKind.Entity
                    && plan.Resolver.GetDefinedType(actualSelectName.Declaration).UnderlyingType
                        is ExpressBoundSelectType actualSelect
                    && ((actual.Reference is { } invalidatedReference
                            && selectNarrowings?.TryGetValue(
                                invalidatedReference,
                                out var invalidatedAlternative) == true
                            && ReferenceEquals(
                                invalidatedAlternative,
                                actualSelectName.Declaration))
                        || pathNarrowings?.Any(narrowing =>
                            ReferenceEquals(narrowing.Value, actualSelectName.Declaration)
                            && SameDirectReferencePath(narrowing.Key, actual)) == true))
                {
                    var actualAlternatives = plan.Resolver.GetSelectAlternatives(actualSelect);
                    var compatibleAlternatives = actualAlternatives
                        .Where(alternative => alternative.Kind == ExpressDeclarationKind.Entity
                            && plan.EntityProjections.Single(projection =>
                                    projection.Entity.Symbol == alternative)
                                .PhysicalComponents.Any(component =>
                                    component.Symbol == formalEntityTarget.Declaration))
                        .ToArray();
                    if (compatibleAlternatives.Length > 0)
                    {
                        var placeholder = "__expressDynamicApplicationArgument_"
                            + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                            + "_"
                            + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                            + "_"
                            + index.ToString(CultureInfo.InvariantCulture)
                            + "__";
                        var branches = actualAlternatives
                            .Select((alternative, branchIndex) =>
                            {
                                var variable = "__expressDynamicApplicationValue_"
                                    + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                                    + "_"
                                    + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                                    + "_"
                                    + index.ToString(CultureInfo.InvariantCulture)
                                    + "_"
                                    + branchIndex.ToString(CultureInfo.InvariantCulture);
                                var value = compatibleAlternatives.Contains(alternative)
                                    ? variable
                                    : null;
                                return (Pattern: variable, Value: value);
                            })
                            .ToArray();
                        dynamicArguments.Add((
                            emittedArguments[index],
                            placeholder,
                            branches,
                            true));
                        emittedArguments[index] = placeholder;
                    }
                }

                if (!wasPathNarrowed
                    && actual.Type.DeclaredType is ExpressBoundNamedType
                    { Declaration.Kind: ExpressDeclarationKind.Entity, } actualEntity)
                {
                    var pendingTypes = new Stack<(
                        ExpressBoundType Type,
                        IReadOnlyList<(ExpressBoundSymbol Wrapper, ExpressBoundSymbol Alternative)> Path)>();
                    pendingTypes.Push((
                        targetType,
                        Array.Empty<(ExpressBoundSymbol, ExpressBoundSymbol)>()));
                    var visitedTypes = new HashSet<ExpressBoundSymbol>();
                    var matchingEntities = new List<(
                        ExpressBoundSymbol Leaf,
                        ExpressEntityProjection Projection,
                        IReadOnlyList<(ExpressBoundSymbol Wrapper, ExpressBoundSymbol Alternative)> Path)>();
                    while (pendingTypes.Count > 0)
                    {
                        var pending = pendingTypes.Pop();
                        if (pending.Type is not ExpressBoundNamedType pendingNamed
                            || !visitedTypes.Add(pendingNamed.Declaration))
                        {
                            continue;
                        }

                        if (pendingNamed.Declaration.Kind == ExpressDeclarationKind.Entity)
                        {
                            var projection = plan.EntityProjections.Single(candidate =>
                                candidate.Entity.Symbol == pendingNamed.Declaration);
                            if (!ReferenceEquals(pendingNamed.Declaration, actualEntity.Declaration)
                                && projection.PhysicalComponents.Any(component =>
                                    component.Symbol == actualEntity.Declaration))
                            {
                                matchingEntities.Add((pendingNamed.Declaration, projection, pending.Path));
                            }

                            continue;
                        }

                        var underlying = plan.Resolver.GetDefinedType(pendingNamed.Declaration).UnderlyingType;
                        if (underlying is ExpressBoundSelectType select)
                        {
                            foreach (var alternative in plan.Resolver.GetSelectAlternatives(select))
                            {
                                pendingTypes.Push((
                                    new ExpressBoundNamedType(alternative, select.Span),
                                    pending.Path.Append((pendingNamed.Declaration, alternative)).ToArray()));
                            }

                            continue;
                        }

                        if (underlying is ExpressBoundNamedType)
                        {
                            pendingTypes.Push((underlying, pending.Path));
                        }
                    }

                    var hasOverlappingAlternatives = matchingEntities.SelectMany(
                            (left, leftIndex) => matchingEntities.Skip(leftIndex + 1)
                                .Select(right => (Left: left, Right: right)))
                        .Any(pair => pair.Left.Projection.PhysicalComponents.Any(component =>
                                component.Symbol == pair.Right.Leaf)
                            || pair.Right.Projection.PhysicalComponents.Any(component =>
                                component.Symbol == pair.Left.Leaf));
                    if (matchingEntities.Count > 0 && !hasOverlappingAlternatives)
                    {
                        var placeholder = "__expressDynamicApplicationArgument_"
                            + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                            + "_"
                            + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                            + "_"
                            + index.ToString(CultureInfo.InvariantCulture)
                            + "__";
                        var branches = matchingEntities
                            .OrderByDescending(candidate => candidate.Projection.PhysicalComponents.Count)
                            .Select((candidate, branchIndex) =>
                            {
                                var variable = "__expressDynamicApplicationValue_"
                                    + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                                    + "_"
                                    + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                                    + "_"
                                    + index.ToString(CultureInfo.InvariantCulture)
                                    + "_"
                                    + branchIndex.ToString(CultureInfo.InvariantCulture);
                                var value = variable;
                                for (var pathIndex = candidate.Path.Count - 1; pathIndex >= 0; pathIndex--)
                                {
                                    var step = candidate.Path[pathIndex];
                                    value = ExpressExpressionEmitter.BoundTypeName(new ExpressBoundNamedType(
                                            step.Wrapper,
                                            targetType.Span))
                                        + ".From"
                                        + ExpressEntityProjection.ToPascalCase(step.Alternative.Name)
                                        + $"({value})";
                                }

                                var pattern = ExpressExpressionEmitter.BoundTypeName(
                                        new ExpressBoundNamedType(candidate.Leaf, targetType.Span))
                                    + " "
                                    + variable;
                                return (Pattern: pattern, Value: (string?)value);
                            })
                            .ToArray();
                        dynamicArguments.Add((emittedArguments[index], placeholder, branches, false));
                        emittedArguments[index] = placeholder;
                    }
                }

                var definedTypes = new List<ExpressBoundNamedType>();
                while (targetType is ExpressBoundNamedType definedType
                       && definedType.Declaration.Kind != ExpressDeclarationKind.Entity)
                {
                    definedTypes.Add(definedType);
                    targetType = plan.Resolver.GetDefinedType(definedType.Declaration).UnderlyingType;
                }

                var actualNominal = actual.Type.DeclaredType as ExpressBoundNamedType;
                var primitiveActual = actualNominal is null;
                var sameDefinedActual = definedTypes.Count > 0
                    && actualNominal is not null
                    && ReferenceEquals(actualNominal.Declaration, definedTypes[0].Declaration);
                var exactScalar = targetType is ExpressBoundScalarType targetScalar
                    && targetScalar.Kind switch
                    {
                        ExpressScalarKind.Binary => actual.Type.Kind == ExpressExpressionTypeKind.Binary,
                        ExpressScalarKind.Boolean => actual.Type.Kind == ExpressExpressionTypeKind.Boolean,
                        ExpressScalarKind.Integer => actual.Type.Kind == ExpressExpressionTypeKind.Integer,
                        ExpressScalarKind.Logical => actual.Type.Kind == ExpressExpressionTypeKind.Logical,
                        ExpressScalarKind.Number => actual.Type.Kind == ExpressExpressionTypeKind.Number,
                        ExpressScalarKind.Real => actual.Type.Kind == ExpressExpressionTypeKind.Real,
                        ExpressScalarKind.String => actual.Type.Kind == ExpressExpressionTypeKind.String,
                        _ => false,
                    };
                var widensToReal = actual.Type.Kind == ExpressExpressionTypeKind.Integer
                    && targetType is ExpressBoundScalarType { Kind: ExpressScalarKind.Real, };
                if (!actual.Type.CanBeIndeterminate
                    && (primitiveActual || sameDefinedActual)
                    && (exactScalar || widensToReal))
                {
                    if (widensToReal)
                    {
                        emittedArguments[index] = ExpressExpressionEmitter.PromoteNumeric(
                            actual,
                            emittedArguments[index],
                            ExpressExpressionTypeKind.Real);
                    }

                    for (var definedIndex = definedTypes.Count - 1; definedIndex >= 0; definedIndex--)
                    {
                        emittedArguments[index] = "new "
                            + ExpressExpressionEmitter.BoundTypeName(definedTypes[definedIndex])
                            + $"({emittedArguments[index]})";
                    }
                }

                if (declaration is not ExpressBoundOpaqueDeclaration)
                {
                    continue;
                }

                if (formalTypes[index] is not ExpressBoundNamedType formalNamed
                    || formalNamed.Declaration.Kind == ExpressDeclarationKind.Entity
                    || plan.Resolver.GetDefinedType(formalNamed.Declaration).UnderlyingType
                        is not ExpressBoundSelectType formalSelect
                    || expression.Children[index].Type.DeclaredType
                        is not ExpressBoundNamedType actualNamed
                    || actualNamed.Declaration.Kind != ExpressDeclarationKind.Entity)
                {
                    continue;
                }

                var actualProjection = plan.EntityProjections.Single(projection =>
                    projection.Entity.Symbol == actualNamed.Declaration);
                var alternatives = plan.Resolver.GetSelectAlternatives(formalSelect)
                    .Where(alternative => alternative.Kind == ExpressDeclarationKind.Entity
                        && actualProjection.PhysicalComponents.Any(component =>
                            component.Symbol == alternative))
                    .ToArray();
                if (alternatives.Length == 1)
                {
                    emittedArguments[index] = ExpressExpressionEmitter.BoundTypeName(formalNamed)
                        + ".From"
                        + ExpressEntityProjection.ToPascalCase(alternatives[0].Name)
                        + $"({emittedArguments[index]})";
                }
            }
        }

        var result = $"{invocation}({string.Join(", ", emittedArguments)})";
        var nullResult = dynamicArguments.Count == 0
            ? ""
            : "(" + ExpressExpressionEmitter.BoundTypeName(expression.Type.DeclaredType!) + "?)null";
        for (var index = dynamicArguments.Count - 1; index >= 0; index--)
        {
            var dynamicArgument = dynamicArguments[index];
            var branches = dynamicArgument.Branches.Select(branch =>
                branch.Pattern + " => " + (branch.Value is null
                    ? nullResult
                    : result.Replace(dynamicArgument.Placeholder, branch.Value)));
            result = dynamicArgument.UsesSelectMatch
                ? $"({dynamicArgument.Carrier}).Match({string.Join(", ", branches)})"
                : $"({dynamicArgument.Carrier}) switch {{ {string.Join(", ", branches)}, "
                    + $"_ => {nullResult} }}";
        }

        return result;
    }

    private static bool SameDirectReferencePath(
        ExpressBoundExpression left,
        ExpressBoundExpression right)
    {
        if (left.Kind != right.Kind
            || !string.Equals(left.Operation, right.Operation, StringComparison.OrdinalIgnoreCase)
            || left.Children.Count != right.Children.Count)
        {
            return false;
        }

        if (left.Reference?.Attribute is not null || right.Reference?.Attribute is not null)
        {
            if (!ReferenceEquals(left.Reference?.Attribute, right.Reference?.Attribute))
            {
                return false;
            }
        }
        else if (left.Reference?.SchemaDeclaration is not null
                 || right.Reference?.SchemaDeclaration is not null)
        {
            if (!ReferenceEquals(
                left.Reference?.SchemaDeclaration,
                right.Reference?.SchemaDeclaration))
            {
                return false;
            }
        }
        else if (!ReferenceEquals(left.Reference, right.Reference))
        {
            return false;
        }

        if (left.Kind is ExpressExpressionKind.Literal or ExpressExpressionKind.Indeterminate
            && !string.Equals(left.SourceText, right.SourceText, StringComparison.Ordinal))
        {
            return false;
        }

        return left.Children.Zip(right.Children, SameDirectReferencePath).All(matches => matches);
    }

    private static void IntersectDictionaryFacts<TKey, TValue>(
        IDictionary<TKey, TValue>? target,
        Func<TValue, TValue, bool> sameValue,
        params IReadOnlyDictionary<TKey, TValue>[] branches)
        where TKey : notnull
    {
        if (target is null || branches.Length == 0)
        {
            return;
        }

        target.Clear();
        foreach (var pair in branches[0])
        {
            if (branches.Skip(1).All(branch => branch.TryGetValue(pair.Key, out var candidate)
                    && sameValue(candidate, pair.Value)))
            {
                target.Add(pair.Key, pair.Value);
            }
        }
    }

    private static void IntersectCollectionFacts<T>(
        ICollection<T>? target,
        Func<T, T, bool> sameItem,
        params IReadOnlyCollection<T>[] branches)
    {
        if (target is null || branches.Length == 0)
        {
            return;
        }

        target.Clear();
        foreach (var item in branches[0])
        {
            if (branches.Skip(1).All(branch => branch.Any(candidate => sameItem(candidate, item))))
            {
                target.Add(item);
            }
        }
    }

    private static void JoinSelectFacts(
        Dictionary<ExpressBoundName, ExpressBoundSymbol>? target,
        params IReadOnlyDictionary<ExpressBoundName, ExpressBoundSymbol>[] branches)
    {
        if (target is null || branches.Length == 0)
        {
            return;
        }

        target.Clear();
        foreach (var pair in branches[0])
        {
            if (!branches.Skip(1).All(branch => branch.ContainsKey(pair.Key)))
            {
                continue;
            }

            var values = branches.Select(branch => branch[pair.Key]).ToArray();
            if (values.All(value => value.Kind == ExpressDeclarationKind.Entity)
                && values.All(value => ReferenceEquals(value, values[0])))
            {
                target.Add(pair.Key, values[0]);
                continue;
            }

            var markers = values
                .Where(value => value.Kind != ExpressDeclarationKind.Entity)
                .Distinct()
                .ToArray();
            if (markers.Length == 1
                && pair.Key.Type is ExpressBoundNamedType declaredSelect
                && ReferenceEquals(declaredSelect.Declaration, markers[0]))
            {
                target.Add(pair.Key, markers[0]);
            }
        }
    }

    private static Method CreateMethod(string name, DataType returnType)
    {
        var method = SourceComposer<ExpressIncrementalGenerator>.Method(
            name,
            SourceComposer.ReturnType(returnType));
        method.Accessibility = TedToolkit.RoslynHelper.Accessibility.PRIVATE;
        method.IsStatic = true;
        return method;
    }

    private static Method CreateEntityValueEqualsMethod(
        ExpressReachableRulePlan plan,
        ExpressGeneratedTypeResolver resolver,
        IReadOnlyList<ExpressEntityProjection> entities,
        IReadOnlyList<ExpressComplexEntityProjection> complexEntities)
    {
        var logicalType = new DataType("global::TedToolkit.Step21.LogicalValue");
        var method = CreateMethod("__ExpressEntityValueEquals", logicalType);
        method.AddParameter(SourceComposer.Parameter(new DataType("global::TedToolkit.Step21.Entity"), "left"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::TedToolkit.Step21.Entity"), "right"));
        method.AddParameter(SourceComposer.Parameter(
            new DataType(
                "global::System.Collections.Generic.List<global::System.Collections.Generic.KeyValuePair<"
                + "global::TedToolkit.Step21.Entity, global::TedToolkit.Step21.Entity>>"),
            "activePairs"));
        method.AddStatement(new IfStatement(new CustomExpression(
            "global::System.Linq.Enumerable.Any(activePairs, pair => "
            + "global::System.Object.ReferenceEquals(pair.Key, left) "
            + "&& global::System.Object.ReferenceEquals(pair.Value, right))"))
            .AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.True").Return));

        foreach (var entity in entities.Where(entity => !entity.Entity.IsAbstract))
        {
            method.AddStatement(CreateEntityValueBranch(
                plan,
                resolver,
                entity.Name,
                entity.EffectiveAttributes));
        }

        foreach (var complex in complexEntities)
        {
            method.AddStatement(CreateEntityValueBranch(
                plan,
                resolver,
                complex.Name,
                complex.Properties));
        }

        method.AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.False").Return);
        AddSummary(method, "Compares generated entity values by exact dynamic projection and effective storage slots.");
        return method;
    }

    private static IfStatement CreateEntityValueBranch(
        ExpressReachableRulePlan plan,
        ExpressGeneratedTypeResolver resolver,
        string typeName,
        IReadOnlyList<ExpressEntityAttributeProjection> attributes)
    {
        var variableSuffix = typeName.TrimStart('_');
        var typedLeft = "typedLeft" + variableSuffix;
        var typedRight = "typedRight" + variableSuffix;
        var comparisons = attributes.Select(attribute => CreateAttributeValueEquality(
            plan,
            resolver,
            attribute,
            $"{typedLeft}.{attribute.StorageMemberName}",
            $"{typedRight}.{attribute.StorageMemberName}",
            "activePairs"));
        return new IfStatement(new CustomExpression($"left is {typeName} {typedLeft}"))
            .AddStatement(new IfStatement(new CustomExpression($"right is not {typeName} {typedRight}"))
                .AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.False").Return))
            .AddStatement(new Statement(new CustomExpression(
                "activePairs.Add(new global::System.Collections.Generic.KeyValuePair<"
                + "global::TedToolkit.Step21.Entity, global::TedToolkit.Step21.Entity>(left, right))")))
            .AddStatement(new Statement(new CustomExpression(
                "var comparisons = new global::TedToolkit.Step21.LogicalValue[] { "
                + string.Join(", ", comparisons)
                + " }")))
            .AddStatement(new Statement(new CustomExpression(
                "activePairs.RemoveAt(activePairs.Count - 1)")))
            .AddStatement(new IfStatement(new CustomExpression(
                "global::System.Linq.Enumerable.Any(comparisons, comparison => "
                + "comparison == global::TedToolkit.Step21.LogicalValue.False)"))
                .AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.False").Return))
            .AddStatement(new IfStatement(new CustomExpression(
                "global::System.Linq.Enumerable.Any(comparisons, comparison => "
                + "comparison == global::TedToolkit.Step21.LogicalValue.Unknown)"))
                .AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.Unknown").Return))
            .AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.True").Return);
    }

    private static string CreateAttributeValueEquality(
        ExpressReachableRulePlan plan,
        ExpressGeneratedTypeResolver resolver,
        ExpressEntityAttributeProjection attribute,
        string left,
        string right,
        string activePairs)
    {
        var projection = resolver.Resolve(plan.Schema.Identity, attribute.StorageType);
        var canBeNull = attribute.Attribute.IsOptional || projection.IsReferenceType;
        if (!canBeNull)
        {
            return CreateBoundValueEquality(plan, resolver, attribute.StorageType, left, right, activePairs);
        }

        var leftValue = projection.IsReferenceType ? left : $"({left}).Value";
        var rightValue = projection.IsReferenceType ? right : $"({right}).Value";
        return $"(({left}) is null || ({right}) is null ? global::TedToolkit.Step21.LogicalValue.Unknown : "
            + CreateBoundValueEquality(plan, resolver, attribute.StorageType, leftValue, rightValue, activePairs)
            + ")";
    }

    private static string CreateBoundValueEquality(
        ExpressReachableRulePlan plan,
        ExpressGeneratedTypeResolver resolver,
        ExpressBoundType type,
        string left,
        string right,
        string activePairs)
    {
        if (type is ExpressBoundNamedType named)
        {
            if (named.Declaration.Kind == ExpressDeclarationKind.Entity)
            {
                return $"__ExpressEntityValueEquals((global::TedToolkit.Step21.Entity)({left}), "
                    + $"(global::TedToolkit.Step21.Entity)({right}), {activePairs})";
            }

            var defined = plan.Resolver.GetDefinedType(named.Declaration);
            if (defined.UnderlyingType is ExpressBoundSelectType select)
            {
                return CreateSelectValueEquality(plan, resolver, select, left, right, activePairs);
            }

            if (defined.UnderlyingType is ExpressBoundEnumerationType)
            {
                return LogicalValueComparison(
                    $"global::System.StringComparer.Ordinal.Equals(({left}).Value, ({right}).Value)");
            }

            return CreateBoundValueEquality(
                plan,
                resolver,
                defined.UnderlyingType,
                $"({left}).Value",
                $"({right}).Value",
                activePairs);
        }

        if (type is ExpressBoundAggregateType aggregate)
        {
            var elementType = ExpressExpressionEmitter.BoundTypeName(aggregate.ElementType);
            var comparison = CreateBoundValueEquality(
                plan,
                resolver,
                aggregate.ElementType,
                "elementLeft",
                "elementRight",
                activePairs);
            var callback = $"(elementLeft, elementRight) => {comparison}";
            return aggregate.Kind switch
            {
                ExpressAggregateKind.Array => $"__ExpressArrayValueEquals<{elementType}>({left}, {right}, {callback})",
                ExpressAggregateKind.List => $"__ExpressOrderedValueEquals<{elementType}>({left}, {right}, {callback})",
                ExpressAggregateKind.Set or ExpressAggregateKind.Bag =>
                    $"__ExpressUnorderedValueEquals<{elementType}>({left}, {right}, {callback})",
                _ => throw new InvalidOperationException("The aggregate kind has no generated value comparer."),
            };
        }

        return LogicalValueComparison(
            $"global::System.Collections.Generic.EqualityComparer<{ExpressExpressionEmitter.BoundTypeName(type)}>"
            + $".Default.Equals(({left}), ({right}))");
    }

    private static string CreateSelectValueEquality(
        ExpressReachableRulePlan plan,
        ExpressGeneratedTypeResolver resolver,
        ExpressBoundSelectType select,
        string left,
        string right,
        string activePairs,
        bool instanceEquality = false,
        ExpressBoundType? rightType = null,
        int nestingDepth = 0)
    {
        if (!instanceEquality)
        {
            var valueOuter = new List<string>();
            var valueAlternatives = plan.Resolver.GetSelectAlternatives(select);
            for (var leftIndex = 0; leftIndex < valueAlternatives.Count; leftIndex++)
            {
                var leftAlternative = valueAlternatives[leftIndex];
                var leftName = $"selectedLeft{leftIndex.ToString(CultureInfo.InvariantCulture)}";
                var inner = new List<string>();
                for (var rightIndex = 0; rightIndex < valueAlternatives.Count; rightIndex++)
                {
                    var rightAlternative = valueAlternatives[rightIndex];
                    var rightName = $"selectedRight{rightIndex.ToString(CultureInfo.InvariantCulture)}";
                    var equality = ReferenceEquals(leftAlternative, rightAlternative)
                        ? CreateBoundValueEquality(
                            plan,
                            resolver,
                            new ExpressBoundNamedType(leftAlternative, select.Span),
                            leftName,
                            rightName,
                            activePairs)
                        : "global::TedToolkit.Step21.LogicalValue.False";
                    inner.Add($"{rightName} => {equality}");
                }

                valueOuter.Add($"{leftName} => ({right}).Match({string.Join(", ", inner)})");
            }

            return $"({left}).Match({string.Join(", ", valueOuter)})";
        }

        ExpressBoundSelectType? rightSelect = null;
        var resolvedRightType = rightType;
        while (resolvedRightType is ExpressBoundNamedType namedRight)
        {
            if (namedRight.Declaration.Kind == ExpressDeclarationKind.Entity)
            {
                break;
            }

            resolvedRightType = resolver.GetDefinedType(namedRight.Declaration).UnderlyingType;
        }

        rightSelect = resolvedRightType as ExpressBoundSelectType;
        var outer = new List<string>();
        var alternatives = plan.Resolver.GetSelectAlternatives(select);
        for (var leftIndex = 0; leftIndex < alternatives.Count; leftIndex++)
        {
            var leftAlternative = alternatives[leftIndex];
            var leftName = "selectedLeft"
                + nestingDepth.ToString(CultureInfo.InvariantCulture)
                + "_"
                + leftIndex.ToString(CultureInfo.InvariantCulture);
            var leftValue = leftName;
            var resolvedLeft = leftAlternative;
            ExpressBoundSelectType? nestedLeft = null;
            while (resolvedLeft.Kind != ExpressDeclarationKind.Entity)
            {
                var underlying = resolver.GetDefinedType(resolvedLeft).UnderlyingType;
                if (underlying is ExpressBoundNamedType nestedNamed)
                {
                    leftValue = $"({leftValue}).Value";
                    resolvedLeft = nestedNamed.Declaration;
                    continue;
                }

                nestedLeft = underlying as ExpressBoundSelectType;
                break;
            }

            if (nestedLeft is not null)
            {
                outer.Add($"{leftName} => {CreateSelectValueEquality(
                    plan,
                    resolver,
                    nestedLeft,
                    leftValue,
                    right,
                    activePairs,
                    instanceEquality: true,
                    rightType,
                    nestingDepth + 1)}");
                continue;
            }

            if (rightSelect is null)
            {
                var rightIsEntity = (resolvedRightType is ExpressBoundNamedType namedRightEntity
                    && namedRightEntity.Declaration.Kind == ExpressDeclarationKind.Entity)
                    || resolvedRightType is ExpressBoundGenericType { IsEntity: true, };
                var equality = resolvedLeft.Kind == ExpressDeclarationKind.Entity && rightIsEntity
                        ? $"global::System.Object.ReferenceEquals(({leftValue}), ({right}))"
                        : "false";
                outer.Add($"{leftName} => {equality}");
                continue;
            }

            var inner = new List<string>();
            var rightAlternatives = plan.Resolver.GetSelectAlternatives(rightSelect);
            for (var rightIndex = 0; rightIndex < rightAlternatives.Count; rightIndex++)
            {
                var rightAlternative = rightAlternatives[rightIndex];
                var rightName = "selectedRight"
                    + nestingDepth.ToString(CultureInfo.InvariantCulture)
                    + "_"
                    + rightIndex.ToString(CultureInfo.InvariantCulture);
                var rightValue = rightName;
                var resolvedRight = rightAlternative;
                ExpressBoundSelectType? nestedRight = null;
                while (resolvedRight.Kind != ExpressDeclarationKind.Entity)
                {
                    var underlying = resolver.GetDefinedType(resolvedRight).UnderlyingType;
                    if (underlying is ExpressBoundNamedType nestedNamed)
                    {
                        rightValue = $"({rightValue}).Value";
                        resolvedRight = nestedNamed.Declaration;
                        continue;
                    }

                    nestedRight = underlying as ExpressBoundSelectType;
                    break;
                }

                string equality;
                if (nestedRight is not null)
                {
                    equality = CreateSelectValueEquality(
                        plan,
                        resolver,
                        nestedRight,
                        rightValue,
                        leftValue,
                        activePairs,
                        instanceEquality: true,
                        new ExpressBoundNamedType(resolvedLeft, select.Span),
                        nestingDepth + 1);
                }
                else if (resolvedLeft.Kind == ExpressDeclarationKind.Entity
                    && resolvedRight.Kind == ExpressDeclarationKind.Entity)
                {
                    equality = $"global::System.Object.ReferenceEquals(({leftValue}), ({rightValue}))";
                }
                else
                {
                    equality = "false";
                }

                inner.Add($"{rightName} => {equality}");
            }

            outer.Add($"{leftName} => ({right}).Match({string.Join(", ", inner)})");
        }

        return $"({left}).Match({string.Join(", ", outer)})";
    }

    private static string LogicalValueComparison(string comparison)
    {
        return $"(({comparison}) ? global::TedToolkit.Step21.LogicalValue.True : "
            + "global::TedToolkit.Step21.LogicalValue.False)";
    }

    private static Method CreateOrderedValueEqualsMethod()
    {
        var method = CreateMethod("__ExpressOrderedValueEquals", new DataType("global::TedToolkit.Step21.LogicalValue"));
        method.AddTypeParameter(SourceComposer.TypeParameter("T"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::System.Collections.Generic.IReadOnlyList<T>"), "left"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::System.Collections.Generic.IReadOnlyList<T>"), "right"));
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::System.Func<T, T, global::TedToolkit.Step21.LogicalValue>"),
            "equals"));
        method.AddStatement(new IfStatement(new CustomExpression("left.Count != right.Count"))
            .AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.False").Return));
        method.AddStatement(new Statement(new CustomExpression("var hasUnknown = false")));
        var loop = new ForEachStatement(DataType.Var, "index", new CustomExpression("global::System.Linq.Enumerable.Range(0, left.Count)"));
        loop.AddStatement(new Statement(new CustomExpression("var comparison = equals(left[index], right[index])")));
        loop.AddStatement(new IfStatement(new CustomExpression("comparison == global::TedToolkit.Step21.LogicalValue.False"))
            .AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.False").Return));
        loop.AddStatement(new IfStatement(new CustomExpression("comparison == global::TedToolkit.Step21.LogicalValue.Unknown"))
            .AddStatement(new Statement(new CustomExpression("hasUnknown = true"))));
        method.AddStatement(loop);
        method.AddStatement(new CustomExpression(
            "hasUnknown ? global::TedToolkit.Step21.LogicalValue.Unknown : "
            + "global::TedToolkit.Step21.LogicalValue.True").Return);
        return method;
    }

    private static Method CreateArrayValueEqualsMethod()
    {
        var method = CreateMethod("__ExpressArrayValueEquals", new DataType("global::TedToolkit.Step21.LogicalValue"));
        method.AddTypeParameter(SourceComposer.TypeParameter("T"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::TedToolkit.Step21.ExpressArray<T>"), "left"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::TedToolkit.Step21.ExpressArray<T>"), "right"));
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::System.Func<T, T, global::TedToolkit.Step21.LogicalValue>"),
            "equals"));
        method.AddStatement(new IfStatement(new CustomExpression(
            "left.LowerIndex != right.LowerIndex || left.UpperIndex != right.UpperIndex"))
            .AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.False").Return));
        method.AddStatement(new Statement(new CustomExpression("var hasUnknown = false")));
        var loop = new ForEachStatement(
            DataType.Var,
            "index",
            new CustomExpression("global::System.Linq.Enumerable.Range(left.LowerIndex, left.Count)"));
        var unset = new IfStatement(new CustomExpression("!left.IsSet(index) || !right.IsSet(index)"))
            .AddStatement(new Statement(new CustomExpression("hasUnknown = true")));
        var set = unset.Else()
            .AddStatement(new Statement(new CustomExpression(
                "var comparison = equals(left[index], right[index])")))
            .AddStatement(new IfStatement(new CustomExpression(
                "comparison == global::TedToolkit.Step21.LogicalValue.False"))
                .AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.False").Return))
            .AddStatement(new IfStatement(new CustomExpression(
                "comparison == global::TedToolkit.Step21.LogicalValue.Unknown"))
                .AddStatement(new Statement(new CustomExpression("hasUnknown = true"))));
        loop.AddStatement(unset);
        method.AddStatement(loop);
        method.AddStatement(new CustomExpression(
            "hasUnknown ? global::TedToolkit.Step21.LogicalValue.Unknown : "
            + "global::TedToolkit.Step21.LogicalValue.True").Return);
        return method;
    }

    private static Method CreateUnorderedValueEqualsMethod()
    {
        var method = CreateMethod("__ExpressUnorderedValueEquals", new DataType("global::TedToolkit.Step21.LogicalValue"));
        method.AddTypeParameter(SourceComposer.TypeParameter("T"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::System.Collections.Generic.IReadOnlyCollection<T>"), "left"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::System.Collections.Generic.IReadOnlyCollection<T>"), "right"));
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::System.Func<T, T, global::TedToolkit.Step21.LogicalValue>"),
            "equals"));
        method.AddStatement(new IfStatement(new CustomExpression("left.Count != right.Count"))
            .AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.False").Return));
        method.AddStatement(new Statement(new CustomExpression("var leftValues = global::System.Linq.Enumerable.ToArray(left)")));
        method.AddStatement(new Statement(new CustomExpression("var rightValues = global::System.Linq.Enumerable.ToArray(right)")));
        method.AddStatement(new Statement(new CustomExpression(
            "var comparisons = new global::TedToolkit.Step21.LogicalValue[left.Count, right.Count]")));
        var leftLoop = new ForEachStatement(
            DataType.Var,
            "leftIndex",
            new CustomExpression("global::System.Linq.Enumerable.Range(0, left.Count)"));
        var rightLoop = new ForEachStatement(
            DataType.Var,
            "rightIndex",
            new CustomExpression("global::System.Linq.Enumerable.Range(0, right.Count)"))
            .AddStatement(new Statement(new CustomExpression(
                "comparisons[leftIndex, rightIndex] = equals(leftValues[leftIndex], rightValues[rightIndex])")));
        leftLoop.AddStatement(rightLoop);
        method.AddStatement(leftLoop);
        method.AddStatement(new IfStatement(new CustomExpression("__ExpressHasPerfectMatch(comparisons, false)"))
            .AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.True").Return));
        method.AddStatement(new IfStatement(new CustomExpression("__ExpressHasPerfectMatch(comparisons, true)"))
            .AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.Unknown").Return));
        method.AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.False").Return);
        return method;
    }

    private static Method CreateHasPerfectMatchMethod()
    {
        var method = CreateMethod("__ExpressHasPerfectMatch", new DataType("global::System.Boolean"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::TedToolkit.Step21.LogicalValue[,]"), "comparisons"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::System.Boolean"), "allowUnknown"));
        method.AddStatement(new Statement(new CustomExpression(
            "var rightToLeft = global::System.Linq.Enumerable.ToArray("
            + "global::System.Linq.Enumerable.Repeat(-1, comparisons.GetLength(1)))")));
        var loop = new ForEachStatement(
            DataType.Var,
            "leftIndex",
            new CustomExpression("global::System.Linq.Enumerable.Range(0, comparisons.GetLength(0))"));
        loop.AddStatement(new Statement(new CustomExpression("var visitedRight = new bool[comparisons.GetLength(1)]")));
        loop.AddStatement(new IfStatement(new CustomExpression(
            "!__ExpressTryMatch(leftIndex, comparisons, allowUnknown, visitedRight, rightToLeft)"))
            .AddStatement(new CustomExpression("false").Return));
        method.AddStatement(loop);
        method.AddStatement(new CustomExpression("true").Return);
        return method;
    }

    private static Method CreateTryMatchMethod()
    {
        var method = CreateMethod("__ExpressTryMatch", new DataType("global::System.Boolean"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::System.Int32"), "leftIndex"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::TedToolkit.Step21.LogicalValue[,]"), "comparisons"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::System.Boolean"), "allowUnknown"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::System.Boolean[]"), "visitedRight"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::System.Int32[]"), "rightToLeft"));
        var loop = new ForEachStatement(
            DataType.Var,
            "rightIndex",
            new CustomExpression("global::System.Linq.Enumerable.Range(0, comparisons.GetLength(1))"));
        var eligible = new IfStatement(new CustomExpression(
            "!visitedRight[rightIndex] && (comparisons[leftIndex, rightIndex] == global::TedToolkit.Step21.LogicalValue.True "
            + "|| (allowUnknown && comparisons[leftIndex, rightIndex] == global::TedToolkit.Step21.LogicalValue.Unknown))"))
            .AddStatement(new Statement(new CustomExpression("visitedRight[rightIndex] = true")));
        var match = new IfStatement(new CustomExpression(
            "rightToLeft[rightIndex] < 0 || __ExpressTryMatch(rightToLeft[rightIndex], comparisons, "
            + "allowUnknown, visitedRight, rightToLeft)"))
            .AddStatement(new Statement(new CustomExpression("rightToLeft[rightIndex] = leftIndex")));
        match.AddStatement(new CustomExpression("true").Return);
        eligible.AddStatement(match);
        loop.AddStatement(eligible);
        method.AddStatement(loop);
        method.AddStatement(new CustomExpression("false").Return);
        return method;
    }

    private static string ResolveReference(
        ExpressReachableRulePlan plan,
        ExpressBoundName reference,
        string? selfExpression,
        string populationExpression,
        IReadOnlyDictionary<string, (string Code, ExpressBoundType Type)>? lexicalNames,
        IReadOnlyDictionary<ExpressBoundName, ExpressBoundSymbol>? selectNarrowings,
        ExpressBoundType? narrowedType = null,
        string? narrowedCode = null,
        ExpressBoundSymbol? narrowedAlternative = null,
        ExpressBoundScalarType? narrowedScalarType = null,
        int narrowingDepth = 0)
    {
        if (narrowedType is not null
            && narrowedCode is not null
            && (narrowedAlternative is not null || narrowedScalarType is not null))
        {
            if (narrowedAlternative is { Kind: not ExpressDeclarationKind.Entity, }
                && plan.Resolver.GetDefinedType(narrowedAlternative).UnderlyingType
                    is ExpressBoundSelectType targetSelect)
            {
                var pendingSelects = new Stack<(
                    ExpressBoundSymbol Wrapper,
                    ExpressBoundSelectType Select,
                    IReadOnlyList<(ExpressBoundSymbol Wrapper, ExpressBoundSymbol Alternative)> Path)>();
                pendingSelects.Push((
                    narrowedAlternative,
                    targetSelect,
                    Array.Empty<(ExpressBoundSymbol, ExpressBoundSymbol)>()));
                var leafPaths = new List<(
                    ExpressBoundSymbol Leaf,
                    IReadOnlyList<(ExpressBoundSymbol Wrapper, ExpressBoundSymbol Alternative)> Path)>();
                var visitedSelects = new HashSet<ExpressBoundSymbol>();
                var allEntityLeaves = true;
                while (pendingSelects.Count > 0 && allEntityLeaves)
                {
                    var pending = pendingSelects.Pop();
                    if (!visitedSelects.Add(pending.Wrapper))
                    {
                        continue;
                    }

                    foreach (var alternative in plan.Resolver.GetSelectAlternatives(pending.Select))
                    {
                        var path = pending.Path
                            .Append((pending.Wrapper, alternative))
                            .ToArray();
                        if (alternative.Kind == ExpressDeclarationKind.Entity)
                        {
                            leafPaths.Add((alternative, path));
                            continue;
                        }

                        if (plan.Resolver.GetDefinedType(alternative).UnderlyingType
                            is ExpressBoundSelectType nestedSelect)
                        {
                            pendingSelects.Push((alternative, nestedSelect, path));
                            continue;
                        }

                        allEntityLeaves = false;
                        break;
                    }
                }

                if (allEntityLeaves && leafPaths.Count > 0)
                {
                    var branches = leafPaths
                        .OrderByDescending(candidate => plan.EntityProjections
                            .Single(projection => projection.Entity.Symbol == candidate.Leaf)
                            .PhysicalComponents.Count)
                        .Select((candidate, index) =>
                        {
                            var variable = "__expressNarrowedReference"
                                + narrowingDepth.ToString(CultureInfo.InvariantCulture)
                                + "_"
                                + index.ToString(CultureInfo.InvariantCulture);
                            var value = variable;
                            for (var pathIndex = candidate.Path.Count - 1; pathIndex >= 0; pathIndex--)
                            {
                                var step = candidate.Path[pathIndex];
                                value = ExpressExpressionEmitter.BoundTypeName(new ExpressBoundNamedType(
                                        step.Wrapper,
                                        targetSelect.Span))
                                    + ".From"
                                    + ExpressEntityProjection.ToPascalCase(step.Alternative.Name)
                                    + $"({value})";
                            }

                            var leafType = ExpressExpressionEmitter.BoundTypeName(
                                new ExpressBoundNamedType(candidate.Leaf, targetSelect.Span));
                            return $"{leafType} {variable} => {value}";
                        });
                    return $"({narrowedCode}) switch {{ {string.Join(", ", branches)}, "
                        + "_ => throw new global::System.InvalidOperationException() }";
                }
            }

            var carrierType = narrowedType;
            var carrier = narrowedCode;
            if (carrierType is ExpressBoundGenericType { IsEntity: true, }
                && narrowedAlternative is { Kind: ExpressDeclarationKind.Entity, })
            {
                var narrowedTypeName = ExpressExpressionEmitter.BoundTypeName(
                    new ExpressBoundNamedType(narrowedAlternative, carrierType.Span));
                var narrowedVariable = "__expressNarrowedReference"
                    + narrowingDepth.ToString(CultureInfo.InvariantCulture);
                return $"({carrier}) switch {{ {narrowedTypeName} {narrowedVariable} => "
                    + $"{narrowedVariable}, _ => throw new global::System.InvalidOperationException() }}";
            }

            while (carrierType is ExpressBoundNamedType namedCarrier)
            {
                if (narrowedAlternative is not null
                    && ReferenceEquals(namedCarrier.Declaration, narrowedAlternative))
                {
                    return carrier;
                }

                if (namedCarrier.Declaration.Kind == ExpressDeclarationKind.Entity)
                {
                    if (narrowedAlternative is
                        {
                            Kind: ExpressDeclarationKind.Entity,
                        })
                    {
                        var narrowedTypeName = ExpressExpressionEmitter.BoundTypeName(
                            new ExpressBoundNamedType(narrowedAlternative, namedCarrier.Span));
                        var narrowedVariable = "__expressNarrowedReference"
                            + narrowingDepth.ToString(CultureInfo.InvariantCulture);
                        return $"({carrier}) switch {{ {narrowedTypeName} {narrowedVariable} => "
                            + $"{narrowedVariable}, _ => throw new global::System.InvalidOperationException() }}";
                    }

                    return "throw new global::System.InvalidOperationException()";
                }

                var underlying = plan.Resolver.GetDefinedType(namedCarrier.Declaration).UnderlyingType;
                if (underlying is not ExpressBoundSelectType)
                {
                    carrier = $"({carrier}).Value";
                }

                carrierType = underlying;
            }

            if (carrierType is ExpressBoundSelectType select)
            {
                var branches = plan.Resolver.GetSelectAlternatives(select)
                    .Select((alternative, index) =>
                    {
                        var variable = "__expressNarrowedReference"
                            + narrowingDepth.ToString(CultureInfo.InvariantCulture)
                            + "_"
                            + index.ToString(CultureInfo.InvariantCulture);
                        var containsNarrowing = narrowedAlternative is not null
                            && (ReferenceEquals(alternative, narrowedAlternative)
                                || (alternative.Kind == ExpressDeclarationKind.Entity
                                    && narrowedAlternative.Kind == ExpressDeclarationKind.Entity
                                    && plan.EntityProjections.Single(projection =>
                                            projection.Entity.Symbol == narrowedAlternative)
                                        .PhysicalComponents.Any(component =>
                                            component.Symbol == alternative)));
                        if (!containsNarrowing
                            && alternative.Kind != ExpressDeclarationKind.Entity)
                        {
                            var pendingTypes = new Stack<ExpressBoundType>();
                            pendingTypes.Push(new ExpressBoundNamedType(alternative, select.Span));
                            var visitedDeclarations = new HashSet<ExpressBoundSymbol>();
                            while (pendingTypes.Count > 0 && !containsNarrowing)
                            {
                                var pendingType = pendingTypes.Pop();
                                if (pendingType is ExpressBoundNamedType pendingNamed)
                                {
                                    if (!visitedDeclarations.Add(pendingNamed.Declaration))
                                    {
                                        continue;
                                    }

                                    if (ReferenceEquals(
                                        pendingNamed.Declaration,
                                        narrowedAlternative))
                                    {
                                        containsNarrowing = true;
                                        break;
                                    }

                                    if (pendingNamed.Declaration.Kind == ExpressDeclarationKind.Entity
                                        && narrowedAlternative?.Kind == ExpressDeclarationKind.Entity
                                        && plan.EntityProjections.Single(projection =>
                                                projection.Entity.Symbol == narrowedAlternative)
                                            .PhysicalComponents.Any(component =>
                                                component.Symbol == pendingNamed.Declaration))
                                    {
                                        containsNarrowing = true;
                                        break;
                                    }

                                    if (pendingNamed.Declaration.Kind != ExpressDeclarationKind.Entity)
                                    {
                                        pendingTypes.Push(plan.Resolver.GetDefinedType(
                                            pendingNamed.Declaration).UnderlyingType);
                                    }
                                }
                                else if (pendingType is ExpressBoundSelectType pendingSelect)
                                {
                                    foreach (var pendingAlternative in plan.Resolver.GetSelectAlternatives(
                                                 pendingSelect))
                                    {
                                        pendingTypes.Push(new ExpressBoundNamedType(
                                            pendingAlternative,
                                            pendingSelect.Span));
                                    }
                                }
                                else if (pendingType is ExpressBoundScalarType pendingScalar
                                         && narrowedScalarType is not null
                                         && (pendingScalar.Kind == narrowedScalarType.Kind
                                             || (pendingScalar.Kind == ExpressScalarKind.Number
                                                 && narrowedScalarType.Kind
                                                 is ExpressScalarKind.Integer or ExpressScalarKind.Real)))
                                {
                                    containsNarrowing = true;
                                }
                            }
                        }

                        var value = containsNarrowing
                            ? ResolveReference(
                                plan,
                                reference,
                                selfExpression,
                                populationExpression,
                                lexicalNames,
                                selectNarrowings,
                                new ExpressBoundNamedType(alternative, select.Span),
                                variable,
                                narrowedAlternative,
                                narrowedScalarType,
                                narrowingDepth + 1)
                            : "throw new global::System.InvalidOperationException()";
                        return $"{variable} => {value}";
                    });
                return $"({carrier}).Match({string.Join(", ", branches)})";
            }

            if (carrierType is ExpressBoundScalarType scalar
                && narrowedScalarType is not null
                && (scalar.Kind == narrowedScalarType.Kind
                    || (scalar.Kind == ExpressScalarKind.Number
                        && narrowedScalarType.Kind is ExpressScalarKind.Integer or ExpressScalarKind.Real)))
            {
                return (scalar.Kind, narrowedScalarType.Kind) switch
                {
                    (ExpressScalarKind.Number, ExpressScalarKind.Integer) =>
                        $"({carrier}).ToIntegerTruncated()",
                    (ExpressScalarKind.Number, ExpressScalarKind.Real) => $"({carrier}).ToReal()",
                    _ => carrier,
                };
            }

            return "throw new global::System.InvalidOperationException()";
        }

        if (lexicalNames is not null
            && lexicalNames.TryGetValue(reference.Name, out var lexicalName))
        {
            if (selectNarrowings?.TryGetValue(reference, out var alternative) == true
                && alternative.Kind == ExpressDeclarationKind.Entity)
            {
                return ResolveReference(
                    plan,
                    reference,
                    selfExpression,
                    populationExpression,
                    lexicalNames,
                    selectNarrowings,
                    lexicalName.Type,
                    lexicalName.Code,
                    alternative);
            }

            return lexicalName.Code;
        }

        if (reference.Kind == ExpressBoundNameKind.Enumeration
            && reference.Type is ExpressBoundNamedType enumeration)
        {
            return ExpressExpressionEmitter.BoundTypeName(enumeration)
                + "."
                + ExpressEntityProjection.ToPascalCase(reference.Name);
        }

        if (reference.Attribute is { } attribute)
        {
            if (selfExpression is null)
            {
                throw new InvalidOperationException(
                    $"Unqualified attribute '{reference.Name}' has no enclosing entity value.");
            }

            return ResolveAttribute(plan, null, reference, selfExpression, populationExpression, null);
        }

        if (reference.SchemaDeclaration is { } symbol)
        {
            return symbol.Kind switch
            {
                ExpressDeclarationKind.Constant => $"{ConstantMethodName(symbol)}({populationExpression})",
                ExpressDeclarationKind.Function => FunctionInvocation(
                    plan,
                    symbol,
                    populationExpression),
                _ => throw new InvalidOperationException(
                    $"Reachable reference '{reference.Name}' has no private generated evaluator."),
            };
        }

        throw new InvalidOperationException(
            $"Lexical reference '{reference.Name}' has no generated spelling in this operation.");
    }

    private static string ResolveAttribute(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression? sourceExpression,
        ExpressBoundName reference,
        string source,
        string populationExpression,
        IReadOnlyDictionary<ExpressBoundName, ExpressBoundSymbol>? selectNarrowings)
    {
        ExpressBoundType? sourceType = sourceExpression?.Type.DeclaredType;
        ExpressBoundSymbol? narrowedAlternative = null;
        if (sourceExpression?.Reference is { } sourceReference
            && selectNarrowings?.TryGetValue(sourceReference, out narrowedAlternative) == true)
        {
            sourceType = new ExpressBoundNamedType(
                narrowedAlternative,
                sourceType?.Span ?? narrowedAlternative.Span);
        }

        while (sourceType is ExpressBoundNamedType namedSource
               && namedSource.Declaration.Kind != ExpressDeclarationKind.Entity)
        {
            sourceType = plan.Resolver.GetDefinedType(namedSource.Declaration).UnderlyingType;
        }

        if (reference.Kind == ExpressBoundNameKind.Entity
            && reference.SchemaDeclaration is { } narrowedGroup
            && narrowedAlternative?.Kind == ExpressDeclarationKind.Entity
            && plan.EntityProjections.Single(candidate =>
                    candidate.Entity.Symbol == narrowedAlternative)
                .PhysicalComponents.Any(component => component.Symbol == narrowedGroup))
        {
            return source;
        }

        if (sourceType is ExpressBoundSelectType select)
        {
            var resultType = reference.Type
                ?? throw new InvalidOperationException(
                    $"SELECT attribute '{reference.Name}' has no statically resolved type.");
            var alternatives = plan.Resolver.GetSelectAlternatives(select);
            var group = reference.Kind == ExpressBoundNameKind.Entity
                ? reference.SchemaDeclaration
                : null;
            if (group is not null
                && !alternatives.Any(alternative =>
                    alternative.Kind == ExpressDeclarationKind.Entity
                    && plan.EntityProjections.Single(candidate => candidate.Entity.Symbol == alternative)
                        .PhysicalComponents.Any(component => component.Symbol == group)))
            {
                return $"(({ExpressExpressionEmitter.BoundTypeName(resultType)})({source}))";
            }

            var branches = alternatives.Select((alternative, index) =>
            {
                var variable = "__selected"
                    + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var projection = alternative.Kind == ExpressDeclarationKind.Entity
                    ? plan.EntityProjections.Single(candidate => candidate.Entity.Symbol == alternative)
                    : null;
                var supportsGroup = group is not null
                    && projection?.PhysicalComponents.Any(component => component.Symbol == group) == true;
                var supportedAttributes = projection is null
                    ? []
                    : reference.AttributeCandidates
                        .Where(candidate => projection.PhysicalComponents.Contains(plan.GetAttributeOwner(candidate)))
                        .ToArray();
                var mostSpecificAttributes = supportedAttributes
                    .Where(candidate => !supportedAttributes.Any(other =>
                        !ReferenceEquals(candidate, other)
                        && plan.EntityProjections.Single(ownerProjection => ReferenceEquals(
                                ownerProjection.Entity,
                                plan.GetAttributeOwner(other)))
                            .PhysicalComponents.Contains(plan.GetAttributeOwner(candidate))))
                    .ToArray();
                var narrowingProvesGroup = group is not null
                    && narrowedAlternative is not null
                    && alternatives.Contains(narrowedAlternative)
                    && plan.EntityProjections.Single(candidate =>
                            candidate.Entity.Symbol == narrowedAlternative)
                        .PhysicalComponents.Any(component => component.Symbol == group);
                var supportsAttribute = supportedAttributes.Length > 0;
                var narrowingProvesAttribute = narrowedAlternative is not null
                    && alternatives.Contains(narrowedAlternative)
                    && plan.EntityProjections.Single(candidate =>
                            candidate.Entity.Symbol == narrowedAlternative)
                        .PhysicalComponents.Any(component =>
                            reference.AttributeCandidates.Any(candidate =>
                                ReferenceEquals(component, plan.GetAttributeOwner(candidate))));
                string value;
                if ((narrowingProvesGroup || narrowingProvesAttribute)
                    && alternative != narrowedAlternative)
                {
                    value = "throw new global::System.InvalidOperationException()";
                }
                else if (supportsGroup)
                {
                    value = variable;
                }
                else if (supportsAttribute)
                {
                    if (mostSpecificAttributes.Length == 1)
                    {
                        var candidate = mostSpecificAttributes[0];
                        var owner = plan.GetAttributeOwner(candidate);
                        value = candidate.Kind switch
                        {
                            ExpressAttributeKind.Derived =>
                                $"{DerivedMethodName(owner, candidate)}({variable}, {populationExpression})",
                            ExpressAttributeKind.Inverse =>
                                $"{InverseMethodName(owner, candidate)}((global::TedToolkit.Step21.Entity)({variable}), {populationExpression})",
                            _ => $"{variable}.{ExpressEntityProjection.ToPascalCase(candidate.Name)}",
                        };
                    }
                    else
                    {
                        value = ResolveAttribute(plan, null, reference, variable, populationExpression, null);
                    }
                }
                else
                {
                    value = $"default({ExpressExpressionEmitter.BoundTypeName(resultType)}?)";
                }

                return $"{variable} => {value}";
            });
            var groupResultType = reference.Kind == ExpressBoundNameKind.Entity
                ? $"<{ExpressExpressionEmitter.BoundTypeName(resultType)}?>"
                : "";
            return $"({source}).Match{groupResultType}({string.Join(", ", branches)})";
        }

        if (reference.AttributeCandidates.Count > 1)
        {
            var sourceProjection = sourceExpression?.Type.DeclaredType is ExpressBoundNamedType
            { Declaration.Kind: ExpressDeclarationKind.Entity, } namedCarrier
                ? plan.EntityProjections.Single(candidate => ReferenceEquals(
                    candidate.Entity.Symbol,
                    namedCarrier.Declaration))
                : null;
            var compatibleCandidates = sourceProjection is null
                ? reference.AttributeCandidates.ToArray()
                : reference.AttributeCandidates
                    .Where(candidate => sourceProjection.PhysicalComponents.Contains(
                        plan.GetAttributeOwner(candidate)))
                    .ToArray();
            if (compatibleCandidates.Length == 0)
            {
                compatibleCandidates = reference.AttributeCandidates.ToArray();
            }

            var mostSpecificCandidates = compatibleCandidates
                .Where(candidate => !compatibleCandidates.Any(other =>
                    !ReferenceEquals(candidate, other)
                    && plan.EntityProjections.Single(ownerProjection => ReferenceEquals(
                            ownerProjection.Entity,
                            plan.GetAttributeOwner(other)))
                        .PhysicalComponents.Contains(plan.GetAttributeOwner(candidate))))
                .ToArray();
            if (mostSpecificCandidates.Length == 1)
            {
                var candidate = mostSpecificCandidates[0];
                var owner = plan.GetAttributeOwner(candidate);
                return candidate.Kind switch
                {
                    ExpressAttributeKind.Derived =>
                        $"{DerivedMethodName(owner, candidate)}({source}, {populationExpression})",
                    ExpressAttributeKind.Inverse =>
                        $"{InverseMethodName(owner, candidate)}((global::TedToolkit.Step21.Entity)({source}), {populationExpression})",
                    _ => $"({source}).{ExpressEntityProjection.ToPascalCase(candidate.Name)}",
                };
            }

            var candidateOwners = compatibleCandidates
                .Select(plan.GetAttributeOwner)
                .ToArray();
            var orderedCandidates = compatibleCandidates
                .OrderByDescending(candidate => plan.EntityProjections.Single(ownerProjection => ReferenceEquals(
                        ownerProjection.Entity,
                        plan.GetAttributeOwner(candidate)))
                    .PhysicalComponents.Count(component => candidateOwners.Contains(component)))
                .ThenBy(candidate => plan.GetAttributeOwner(candidate).Name, StringComparer.Ordinal)
                .ToArray();
            var cases = orderedCandidates.Select((candidate, index) =>
            {
                var owner = plan.GetAttributeOwner(candidate);
                var ownerType = "global::TedToolkit.Step21.Generated."
                    + ExpressEntityProjection.ToPascalCase(owner.Symbol.DeclaringSchema.Name)
                    + ".I"
                    + ExpressEntityProjection.ToPascalCase(owner.Name);
                var variable = $"__candidate{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                var access = candidate.Kind switch
                {
                    ExpressAttributeKind.Derived =>
                        $"{DerivedMethodName(owner, candidate)}({variable}, {populationExpression})",
                    ExpressAttributeKind.Inverse =>
                        $"{InverseMethodName(owner, candidate)}((global::TedToolkit.Step21.Entity)({variable}), {populationExpression})",
                    _ => $"{variable}.{ExpressEntityProjection.ToPascalCase(candidate.Name)}",
                };
                return $"{ownerType} {variable} => {access}";
            });
            return $"({source}) switch {{ {string.Join(", ", cases)}, _ => throw new global::System.InvalidOperationException() }}";
        }

        var attribute = reference.Attribute;
        if (attribute?.Kind == ExpressAttributeKind.Derived)
        {
            var owner = plan.GetAttributeOwner(attribute);
            return $"{DerivedMethodName(owner, attribute)}({source}, {populationExpression})";
        }

        if (attribute?.Kind == ExpressAttributeKind.Inverse)
        {
            var owner = plan.GetAttributeOwner(attribute);
            return $"{InverseMethodName(owner, attribute)}("
                + $"(global::TedToolkit.Step21.Entity)({source}), {populationExpression})";
        }

        return $"({source}).{ExpressEntityProjection.ToPascalCase(reference.Name)}";
    }

    private static string ConstantMethodName(ExpressBoundSymbol symbol)
    {
        return $"__ExpressConstant_{ExpressEntityProjection.ToPascalCase(symbol.Name)}";
    }

    private static string FunctionMethodName(ExpressBoundSymbol symbol)
    {
        return $"__ExpressFunction_{ExpressEntityProjection.ToPascalCase(symbol.Name)}";
    }

    private static string DerivedMethodName(
        ExpressBoundEntity owner,
        ExpressBoundAttribute attribute)
    {
        return "__ExpressDerived_"
            + ExpressEntityProjection.ToPascalCase(owner.Name)
            + "_"
            + ExpressEntityProjection.ToPascalCase(attribute.Name);
    }

    private static string InverseMethodName(
        ExpressBoundEntity owner,
        ExpressBoundAttribute attribute)
    {
        return "__ExpressInverse_"
            + ExpressEntityProjection.ToPascalCase(owner.Name)
            + "_"
            + ExpressEntityProjection.ToPascalCase(attribute.Name);
    }

    private static string ParameterName(string name)
    {
        return $"__parameter_{ExpressEntityProjection.ToPascalCase(name)}";
    }

    private static bool SameStart(ExpressSourceSpan left, ExpressSourceSpan right)
    {
        return string.Equals(left.Start.FilePath, right.Start.FilePath, StringComparison.Ordinal)
            && left.Start.Line == right.Start.Line
            && left.Start.Column == right.Start.Column;
    }

    private static void AddSummary(IRootDescription target, string text)
    {
        target.AddRootDescription(new DescriptionSummary(
            new IDescriptionItem[] { new DescriptionText(text), }));
    }
}