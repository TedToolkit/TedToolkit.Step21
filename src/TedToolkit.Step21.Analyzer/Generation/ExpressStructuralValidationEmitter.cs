// -----------------------------------------------------------------------
// <copyright file="ExpressStructuralValidationEmitter.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis.CSharp;

using TedToolkit.RoslynHelper;
using TedToolkit.RoslynHelper.Syntaxes;

using TedToolkit.Step21.Analyzer.Express;
using TedToolkit.Step21.Analyzer.Express.Analysis;
using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Composes generated structural rules and their caller-facing documentation from one bound EXPRESS projection.
/// </summary>
internal static class ExpressStructuralValidationEmitter
{
    private const int VALIDATION_BRANCHES_PER_METHOD = 32;

    private const string FAILURE_LIST_TYPE =
        "global::System.Collections.Generic.List<global::TedToolkit.Step21.ValidationFailure>";

    private const string POPULATION_TYPE =
        "global::System.Collections.Generic.IReadOnlyList<global::System.Collections.Generic.KeyValuePair<global::System.String, global::TedToolkit.Step21.Entity>>";

    private const string INVERSE_CACHE_TYPE =
        "global::System.Collections.Generic.Dictionary<global::TedToolkit.Step21.Entity, "
        + "global::System.Collections.Generic.Dictionary<global::System.String, global::System.Object?>>";

    /// <summary>
    /// Creates the generated descriptor validation dispatch method.
    /// </summary>
    /// <param name="schema">The owning schema.</param>
    /// <param name="entities">The generated entity projections.</param>
    /// <param name="complexEntities">The generated multi-leaf entity projections.</param>
    /// <param name="resolver">The closed-set generated type resolver.</param>
    /// <param name="rulePlan">The validated reachable rule closure.</param>
    /// <param name="shards">The structural descriptor partitions.</param>
    /// <returns>The protected descriptor override.</returns>
    internal static List<Method> CreateDispatchMethods(
        ExpressBoundSchema schema,
        IReadOnlyList<ExpressEntityProjection> entities,
        IReadOnlyList<ExpressComplexEntityProjection> complexEntities,
        ExpressGeneratedTypeResolver resolver,
        ExpressReachableRulePlan rulePlan,
        ExpressDescriptorShards shards)
    {
        return CreateDispatchMethods(
            "ValidateCore",
            schema,
            entities,
            complexEntities,
            resolver,
            rulePlan,
            shards,
            recognizeImportedEntities: true,
            executeGlobalRules: true);
    }

    /// <summary>
    /// Creates entity-local validation used when this schema's entities enter another schema population.
    /// </summary>
    /// <param name="schema">The owning schema.</param>
    /// <param name="entities">The generated entity projections.</param>
    /// <param name="complexEntities">The generated multi-leaf entity projections.</param>
    /// <param name="resolver">The closed-set generated type resolver.</param>
    /// <param name="rulePlan">The validated reachable rule closure.</param>
    /// <param name="shards">The structural descriptor partitions.</param>
    /// <returns>The protected entity-population descriptor override.</returns>
    internal static List<Method> CreateEntityPopulationDispatchMethods(
        ExpressBoundSchema schema,
        IReadOnlyList<ExpressEntityProjection> entities,
        IReadOnlyList<ExpressComplexEntityProjection> complexEntities,
        ExpressGeneratedTypeResolver resolver,
        ExpressReachableRulePlan rulePlan,
        ExpressDescriptorShards shards)
    {
        return CreateDispatchMethods(
            "ValidateEntityPopulationCore",
            schema,
            entities,
            complexEntities,
            resolver,
            rulePlan,
            shards,
            recognizeImportedEntities: false,
            executeGlobalRules: false);
    }

    private static List<Method> CreateDispatchMethods(
        string methodName,
        ExpressBoundSchema schema,
        IReadOnlyList<ExpressEntityProjection> entities,
        IReadOnlyList<ExpressComplexEntityProjection> complexEntities,
        ExpressGeneratedTypeResolver resolver,
        ExpressReachableRulePlan rulePlan,
        ExpressDescriptorShards shards,
        bool recognizeImportedEntities,
        bool executeGlobalRules)
    {
        var method = CreateMethod(
            methodName,
            new DataType("global::TedToolkit.Step21.ValidationResult"),
            TedToolkit.RoslynHelper.Accessibility.PROTECTED);
        method.Polymorphism = Polymorphism.OVERRIDE;
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::TedToolkit.Step21.ExchangeStructure"),
            "structure"));
        method.AddParameter(SourceComposer.Parameter(
            new DataType(
                "global::System.Collections.Generic.IReadOnlyList<global::System.Collections.Generic.KeyValuePair<global::System.String, global::TedToolkit.Step21.Entity>>"),
            "entities"));
        method.AddStatement(new CustomExpression($"var failures = new {FAILURE_LIST_TYPE}()"));
        var inverseContext = rulePlan.ReachableSingularInverseAttributes.Count > 0;
        if (inverseContext)
        {
            method.AddStatement(new CustomExpression(
                $"var inverseCache = new {INVERSE_CACHE_TYPE}("
                + "global::System.Collections.Generic.ReferenceEqualityComparer.Instance)"));
        }

        var inverseArgument = inverseContext ? ", inverseCache" : "";
        var temporaryOrdinal = 0;
        Func<string, string> allocateTemporaryName = prefix => prefix + Invariant(temporaryOrdinal++);

        var methods = new List<Method>() { method, };
        var concreteEntityNames = entities
            .Where(candidate => !candidate.Entity.IsAbstract)
            .Select(entity => entity.Name)
            .Concat(complexEntities.Select(entity => entity.Name))
            .ToArray();
        var groupCalls = new List<string>();
        var groupPrefix = executeGlobalRules
            ? "__ExpressTryValidateGroup"
            : "__ExpressTryValidateEntityPopulationGroup";
        for (var offset = 0; offset < concreteEntityNames.Length; offset += VALIDATION_BRANCHES_PER_METHOD)
        {
            var groupIndex = offset / VALIDATION_BRANCHES_PER_METHOD;
            var groupName = groupPrefix
                + groupIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
            groupCalls.Add($"{groupName}(entry.Value, entry.Key, failures, entities{inverseArgument})");
            methods.Add(CreateValidationGroupMethod(
                groupName,
                concreteEntityNames.Skip(offset).Take(VALIDATION_BRANCHES_PER_METHOD),
                rulePlan,
                shards,
                offset));
        }

        var loop = new ForEachStatement(DataType.Var, "entry", new CustomExpression("entities"));
        var recognitionExpression = groupCalls.Count == 0 ? "false" : string.Join(" || ", groupCalls);
        loop.AddStatement(new CustomExpression($"var recognized = {recognitionExpression}"));

        if (recognizeImportedEntities)
        {
            foreach (var importedEntity in schema.Imports
                         .Select(import => import.Declaration)
                         .Where(symbol => symbol.Kind == ExpressDeclarationKind.Entity)
                         .Distinct())
            {
                var interfaceName = GetGeneratedEntityInterface(schema.Identity, importedEntity);
                loop.AddStatement(new IfStatement(new CustomExpression($"entry.Value is {interfaceName}"))
                    .AddStatement(new CustomExpression("recognized = true")));
            }
        }

        var schemaCode = $"{schema.Name.ToUpperInvariant()}.STRUCTURE.ENTITY_ASSIGNABILITY";
        var unknown = new IfStatement(new CustomExpression("!recognized"))
            .AddStatement(AddFailure(
                schemaCode,
                "entry.Key",
                "The registered entity is not assignable to a concrete entity generated for this EXPRESS schema.",
                schema.Identity.Span));
        loop.AddStatement(unknown);
        method.AddStatement(loop);
        AddUniqueValidation(method, entities, resolver, rulePlan);
        if (executeGlobalRules)
        {
            AddGlobalRuleValidation(method, rulePlan, allocateTemporaryName);
        }

        method.AddStatement(new CustomExpression(
            "new global::TedToolkit.Step21.ValidationResult(failures)").Return);
        AddSummary(method, executeGlobalRules
            ? "Validates the ordered registered entities in a population governed by this EXPRESS schema."
            : "Validates entity-local and UNIQUE rules when schema-owned entities enter another population.");
        return methods;
    }

    private static Method CreateValidationGroupMethod(
        string name,
        IEnumerable<string> entityNames,
        ExpressReachableRulePlan rulePlan,
        ExpressDescriptorShards shards,
        int offset)
    {
        var method = CreateMethod(
            name,
            DataType.Bool,
            TedToolkit.RoslynHelper.Accessibility.PRIVATE);
        method.IsStatic = true;
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::TedToolkit.Step21.Entity"),
            "value"));
        method.AddParameter(SourceComposer.Parameter(DataType.String, "path"));
        method.AddParameter(SourceComposer.Parameter(new DataType(FAILURE_LIST_TYPE), "failures"));
        method.AddParameter(SourceComposer.Parameter(new DataType(POPULATION_TYPE), "entities"));
        AddInverseCacheParameter(method, rulePlan);
        var inverseArgument = rulePlan.ReachableSingularInverseAttributes.Count > 0 ? ", inverseCache" : "";
        foreach (var entityName in entityNames)
        {
            var localName = $"typed{entityName}";
            var validationName = shards.Qualify($"Validate{entityName}", ValidationShardName(offset++));
            method.AddStatement(new IfStatement(new CustomExpression($"value is {entityName} {localName}"))
                .AddStatement(new CustomExpression(
                    $"{validationName}({localName}, path, failures, entities{inverseArgument})"))
                .AddStatement(new CustomExpression("true").Return));
        }

        method.AddStatement(new CustomExpression("false").Return);
        return method;
    }

    /// <summary>
    /// Gets the shared declaration/dispatch container for an entity validation method.
    /// </summary>
    /// <param name="entityIndex">The index in concrete entity dispatch order.</param>
    /// <returns>The deterministic private shard name.</returns>
    internal static string ValidationShardName(int entityIndex)
    {
        // Each validator can contain thousands of lambdas. Combining validators also combines
        // their compiler-generated static caches into one Native AOT GC-layout symbol.
        return "__ExpressValidationShard" + Invariant(entityIndex);
    }

    private static void AddGlobalRuleValidation(
        IStatementOwner owner,
        ExpressReachableRulePlan rulePlan,
        Func<string, string> allocateTemporaryName)
    {
        var ruleIndex = 0;
        foreach (var declaration in rulePlan.Schema.Declarations
                     .Where(candidate => candidate.Kind == ExpressDeclarationKind.Rule))
        {
            var declarationRule = rulePlan.GetSemanticDeclaration(declaration);
            var head = declarationRule.RequiredChild("ruleHead");
            var lexicalNames = new Dictionary<string, (string Code, ExpressBoundType Type)>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var entityReference in head.ChildRules("entityRef"))
            {
                var entityName = entityReference.Identifier!;
                var entity = rulePlan.Schema.Declarations
                    .Select(candidate => candidate.Symbol)
                    .Concat(rulePlan.Schema.Imports.Select(import => import.Declaration))
                    .Single(candidate => candidate.Kind == ExpressDeclarationKind.Entity
                        && string.Equals(candidate.Name, entityName, StringComparison.OrdinalIgnoreCase));
                var generatedEntityName = GetGeneratedEntityInterface(rulePlan.Schema.Identity, entity);
                var populationName = $"rulePopulation{Invariant(ruleIndex++)}";
                var populations = rulePlan.Schema.NameReferences
                    .Select(reference => reference.Target)
                    .Where(candidate => candidate.Kind == ExpressBoundNameKind.Population
                        && string.Equals(candidate.Name, entityName, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(
                            candidate.Span.Start.FilePath,
                            declarationRule.Span.Start.FilePath,
                            StringComparison.Ordinal)
                        && (candidate.Span.Start.Line > declarationRule.Span.Start.Line
                            || (candidate.Span.Start.Line == declarationRule.Span.Start.Line
                                && candidate.Span.Start.Column >= declarationRule.Span.Start.Column))
                        && (candidate.Span.End.Line < declarationRule.Span.End.Line
                            || (candidate.Span.End.Line == declarationRule.Span.End.Line
                                && candidate.Span.End.Column <= declarationRule.Span.End.Column)))
                    .Distinct()
                    .ToArray();
                if (populations.Length == 0)
                {
                    continue;
                }

                var population = populations.Single();
                lexicalNames.Add(entityName, (populationName, population.Type!));
                owner.AddStatement(new CustomExpression(
                    $"var {populationName} = new global::TedToolkit.Step21.ExpressSet<{generatedEntityName}>(0)"));
                var entryName = $"rulePopulationEntry{Invariant(ruleIndex)}";
                var valueName = $"rulePopulationValue{Invariant(ruleIndex)}";
                var loop = new ForEachStatement(DataType.Var, entryName, new CustomExpression("entities"));
                loop.AddStatement(new IfStatement(new CustomExpression(
                        $"{entryName}.Value is {generatedEntityName} {valueName}"))
                    .AddStatement(new CustomExpression($"{populationName}.Add({valueName})")));
                owner.AddStatement(loop);
            }

            ExpressReachableRuleEmitter.EmitRuleAlgorithm(
                rulePlan,
                declaration,
                owner,
                lexicalNames,
                allocateTemporaryName);

            var rules = declarationRule.RequiredChild("whereClause")
                .ChildRules("domainRule")
                .ToArray();
            for (var index = 0; index < rules.Length; index++)
            {
                var rule = rules[index];
                var expression = rulePlan.GetExpression(rule.RequiredChild("expression"));
                var generated = ExpressExpressionEmitter.Emit(
                    expression,
                    ExpressReachableRuleEmitter.CreateContext(
                        rulePlan,
                        selfExpression: null,
                        "entities",
                        lexicalNames,
                        allocateTemporaryName: allocateTemporaryName));
                var label = rule.ChildRules("ruleLabelId").SingleOrDefault()?.Identifier
                    ?? $"RULE_{Invariant(index + 1)}";
                var code = $"{rulePlan.Schema.Name}.RULE.{declaration.Name}.WHERE.{label}"
                    .ToUpperInvariant();
                AddWhereRuleEvaluation(
                    owner,
                    rulePlan,
                    RuleFailureCondition(expression, generated.Code),
                    AddFailure(
                        code,
                        Literal($"Schema[{rulePlan.Schema.Name}].{declaration.Name}"),
                        $"The EXPRESS WHERE rule '{expression.SourceText}' must evaluate to TRUE.",
                        rule.Span));
            }
        }
    }

    private static string GetGeneratedEntityInterface(
        ExpressBoundSchemaIdentity currentSchema,
        ExpressBoundSymbol entity)
    {
        var name = $"I{ExpressEntityProjection.ToPascalCase(entity.Name)}";
        return ReferenceEquals(currentSchema, entity.DeclaringSchema)
            ? name
            : "global::TedToolkit.Step21.Generated."
                + $"{ExpressEntityProjection.ToPascalCase(entity.DeclaringSchema.Name)}.{name}";
    }

    private static void AddUniqueValidation(
        IStatementOwner owner,
        IReadOnlyList<ExpressEntityProjection> entities,
        ExpressGeneratedTypeResolver resolver,
        ExpressReachableRulePlan rulePlan)
    {
        var ruleIndex = 0;
        foreach (var entity in entities)
        {
            var uniqueClause = rulePlan.GetSemanticDeclaration(entity.Entity).RequiredChild("entityBody")
                .ChildRules("uniqueClause")
                .SingleOrDefault();
            if (uniqueClause is null)
            {
                continue;
            }

            var rules = uniqueClause.ChildRules("uniqueRule").ToArray();
            for (var index = 0; index < rules.Length; index++)
            {
                var guarded = rulePlan.ReachableSingularInverseAttributes.Count > 0
                    ? new TryStatement()
                    : null;
                var ruleOwner = (IStatementOwner?)guarded ?? owner;
                var rule = rules[index];
                var attributes = rule.ChildRules("referencedAttribute")
                    .Select(rulePlan.GetReferencedAttribute)
                    .ToArray();
                var projectedAttributes = attributes
                    .Select(attribute => ResolveUniqueKeyProjection(entity, attribute, resolver))
                    .ToArray();
                var keyTypes = attributes
                    .Select((attribute, attributeIndex) =>
                        projectedAttributes[attributeIndex]?.Type ?? attribute.Type)
                    .ToArray();
                var populationName = $"uniquePopulation{Invariant(ruleIndex)}";
                var duplicatesName = $"uniqueDuplicates{Invariant(ruleIndex)}";
                var duplicateName = $"uniqueDuplicate{Invariant(ruleIndex)}";
                var interfaceName = $"I{entity.Name}";
                ruleOwner.AddStatement(new CustomExpression(
                    $"var {populationName} = global::System.Linq.Enumerable.Select("
                    + "global::System.Linq.Enumerable.Where(entities, "
                    + $"entry => entry.Value is {interfaceName}), "
                    + "entry => new global::System.Collections.Generic.KeyValuePair<"
                    + $"global::System.String, {interfaceName}>(entry.Key, ({interfaceName})entry.Value))"));
                var rawKeyParts = attributes.Select((attribute, attributeIndex) => UniqueKeyExpression(
                    rulePlan,
                    attribute,
                    projectedAttributes[attributeIndex],
                    "item.Value")).ToArray();
                var rawPreviousKeyParts = attributes.Select((attribute, attributeIndex) => UniqueKeyExpression(
                    rulePlan,
                    attribute,
                    projectedAttributes[attributeIndex],
                    "previous.Value")).ToArray();
                var keyParts = rawKeyParts.ToArray();
                var previousKeyParts = rawPreviousKeyParts.ToArray();
                for (var attributeIndex = 0; attributeIndex < attributes.Length; attributeIndex++)
                {
                    var keyAttribute = projectedAttributes[attributeIndex]?.Attribute
                        ?? attributes[attributeIndex];
                    if ((keyAttribute.IsOptional
                            || (attributes[attributeIndex].Kind == ExpressAttributeKind.Derived
                                && rulePlan.GetDerivedExpression(attributes[attributeIndex])
                                    .Type.CanBeIndeterminate))
                        && !resolver.Resolve(rulePlan.Schema.Identity, keyTypes[attributeIndex]).IsReferenceType)
                    {
                        keyParts[attributeIndex] = $"({keyParts[attributeIndex]}).Value";
                        previousKeyParts[attributeIndex] = $"({previousKeyParts[attributeIndex]}).Value";
                    }
                }

                var equalityVariable = 0;
                var equality = string.Join(
                    " && ",
                    attributes.Select((_, attributeIndex) => UniqueKeyEquals(
                        keyTypes[attributeIndex],
                        keyParts[attributeIndex],
                        previousKeyParts[attributeIndex],
                        resolver,
                        rulePlan,
                        ref equalityVariable)));
                var determinate = string.Join(
                    " && ",
                    attributes.Select((attribute, attributeIndex) => UniqueKeyIsDeterminate(
                        attribute,
                        projectedAttributes[attributeIndex],
                        rawKeyParts[attributeIndex],
                        resolver,
                        rulePlan,
                        ref equalityVariable)));
                var previousDeterminate = string.Join(
                    " && ",
                    attributes.Select((attribute, attributeIndex) => UniqueKeyIsDeterminate(
                        attribute,
                        projectedAttributes[attributeIndex],
                        rawPreviousKeyParts[attributeIndex],
                        resolver,
                        rulePlan,
                        ref equalityVariable)));
                ruleOwner.AddStatement(new CustomExpression(
                    $"var {duplicatesName} = global::System.Linq.Enumerable.Where({populationName}, "
                    + $"(item, itemIndex) => ({determinate}) && global::System.Linq.Enumerable.Any("
                    + $"global::System.Linq.Enumerable.Take({populationName}, itemIndex), "
                    + $"previous => ({previousDeterminate}) && ({equality})))"));
                var label = rule.ChildRules("ruleLabelId").SingleOrDefault()?.Identifier
                    ?? $"RULE_{Invariant(index + 1)}";
                var code = $"{rulePlan.Schema.Name}.{entity.Entity.Name}.UNIQUE.{label}"
                    .ToUpperInvariant();
                var firstProperty = ExpressEntityProjection.ToPascalCase(attributes[0].Name);
                var loop = new ForEachStatement(DataType.Var, duplicateName, new CustomExpression(duplicatesName));
                loop.AddStatement(AddFailure(
                    code,
                    $"{duplicateName}.Key + {Literal($".{firstProperty}")}",
                    "The EXPRESS UNIQUE key must identify at most one entity candidate.",
                    rule.Span));
                ruleOwner.AddStatement(loop);
                if (guarded is not null)
                {
                    guarded.AddCatch(new CatchClause(
                            new DataType("__ExpressInverseUnavailableException"),
                            "inverseUnavailable")
                        .AddStatement(new CustomExpression("_ = inverseUnavailable")));
                    owner.AddStatement(guarded);
                }

                ruleIndex++;
            }
        }
    }

    private static ExpressEntityAttributeProjection? ResolveUniqueKeyProjection(
        ExpressEntityProjection entity,
        ExpressBoundAttribute attribute,
        ExpressGeneratedTypeResolver resolver)
    {
        if (attribute.Kind != ExpressAttributeKind.Explicit)
        {
            return null;
        }

        var storageAttribute = attribute;
        var visited = new HashSet<ExpressBoundAttribute>();
        while (visited.Add(storageAttribute)
               && resolver.TryGetRedeclaredAttribute(storageAttribute, out var inherited))
        {
            storageAttribute = inherited;
        }

        var propertyName = ExpressEntityProjection.ToPascalCase(attribute.Name);
        return entity.FlattenedAttributes.FirstOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.Name, propertyName)
            && (ReferenceEquals(candidate.Attribute, attribute)
                || (ReferenceEquals(candidate.StorageEntity.Symbol, storageAttribute.DeclaringEntity)
                    && StringComparer.OrdinalIgnoreCase.Equals(
                        candidate.StorageAttributeName,
                        storageAttribute.Name))));
    }

    private static string UniqueKeyExpression(
        ExpressReachableRulePlan rulePlan,
        ExpressBoundAttribute attribute,
        ExpressEntityAttributeProjection? projection,
        string valueExpression)
    {
        if (attribute.Kind == ExpressAttributeKind.Derived)
        {
            var owner = rulePlan.GetAttributeOwner(attribute);
            return "__ExpressDerived_"
                + ExpressEntityProjection.ToPascalCase(owner.Name)
                + "_"
                + ExpressEntityProjection.ToPascalCase(attribute.Name)
                + $"({valueExpression}, entities"
                + (rulePlan.ReachableSingularInverseAttributes.Count > 0
                    ? ", failures, inverseCache)"
                    : ")");
        }

        if (attribute.Kind == ExpressAttributeKind.Inverse)
        {
            var owner = rulePlan.GetAttributeOwner(attribute);
            return "__ExpressInverse_"
                + ExpressEntityProjection.ToPascalCase(owner.Name)
                + "_"
                + ExpressEntityProjection.ToPascalCase(attribute.Name)
                + $"((global::TedToolkit.Step21.Entity)({valueExpression}), entities"
                + (rulePlan.ReachableSingularInverseAttributes.Count > 0
                    ? ", failures, inverseCache)"
                    : ")");
        }

        var accessOwner = projection?.DeclaringEntity ?? rulePlan.GetAttributeOwner(attribute);
        var interfaceName = GetGeneratedEntityInterface(rulePlan.Schema.Identity, accessOwner.Symbol);
        return $"(({interfaceName})({valueExpression})).{ExpressEntityProjection.ToPascalCase(attribute.Name)}";
    }

    private static string UniqueKeyIsDeterminate(
        ExpressBoundAttribute attribute,
        ExpressEntityAttributeProjection? projection,
        string valueExpression,
        ExpressGeneratedTypeResolver resolver,
        ExpressReachableRulePlan rulePlan,
        ref int variable)
    {
        if ((projection?.Attribute.IsOptional ?? attribute.IsOptional)
            || (attribute.Kind == ExpressAttributeKind.Derived
                && rulePlan.GetDerivedExpression(attribute).Type.CanBeIndeterminate))
        {
            return $"({valueExpression}) is not null";
        }

        return UniqueValueIsDeterminate(
            projection?.Type ?? attribute.Type,
            valueExpression,
            resolver,
            rulePlan,
            ref variable);
    }

    private static string UniqueValueIsDeterminate(
        ExpressBoundType type,
        string valueExpression,
        ExpressGeneratedTypeResolver resolver,
        ExpressReachableRulePlan rulePlan,
        ref int variable)
    {
        if (type is ExpressBoundAggregateType aggregate)
        {
            var conditions = new List<string>();
            if (aggregate.Kind == ExpressAggregateKind.Array && aggregate.IsOptional)
            {
                var indexName = $"uniqueIndex{Invariant(variable++)}";
                conditions.Add(
                    "global::System.Linq.Enumerable.All(global::System.Linq.Enumerable.Range("
                    + $"({valueExpression}).LowerIndex, ({valueExpression}).Count), "
                    + $"{indexName} => ({valueExpression}).IsSet({indexName}))");
            }

            var elementName = $"uniqueElement{Invariant(variable++)}";
            var elementCondition = UniqueValueIsDeterminate(
                aggregate.ElementType,
                elementName,
                resolver,
                rulePlan,
                ref variable);
            if (elementCondition != "true")
            {
                conditions.Add(
                    $"global::System.Linq.Enumerable.All(({valueExpression}), "
                    + $"{elementName} => {elementCondition})");
            }

            return conditions.Count == 0
                ? "true"
                : string.Join(" && ", conditions);
        }

        if (type is ExpressBoundNamedType named
            && named.Declaration.Kind != ExpressDeclarationKind.Entity)
        {
            var declaration = resolver.GetDefinedType(named.Declaration);
            if (declaration is ExpressBoundDefinedType defined
                && defined.UnderlyingType is ExpressBoundSelectType select)
            {
                var alternatives = resolver.GetSelectAlternatives(select);
                var branches = new List<string>();
                foreach (var alternative in alternatives)
                {
                    var selected = $"uniqueSelected{Invariant(variable++)}";
                    var selectedCondition = UniqueValueIsDeterminate(
                        new ExpressBoundNamedType(alternative, named.Span),
                        selected,
                        resolver,
                        rulePlan,
                        ref variable);
                    branches.Add(
                        $"({valueExpression}).TryGet{ExpressEntityProjection.ToPascalCase(alternative.Name)}("
                        + $"out var {selected}) && ({selectedCondition})");
                }

                return $"({string.Join(" || ", branches)})";
            }

            if (declaration is ExpressBoundDefinedType
                { UnderlyingType: not ExpressBoundEnumerationType, } definedValue)
            {
                return UniqueValueIsDeterminate(
                    definedValue.UnderlyingType,
                    $"({valueExpression}).Value",
                    resolver,
                    rulePlan,
                    ref variable);
            }
        }

        return "true";
    }

    private static string UniqueKeyEquals(
        ExpressBoundType type,
        string left,
        string right,
        ExpressGeneratedTypeResolver resolver,
        ExpressReachableRulePlan rulePlan,
        ref int variable)
    {
        if (type is ExpressBoundAggregateType aggregate)
        {
            var leftElement = $"uniqueLeft{Invariant(variable++)}";
            var rightElement = $"uniqueRight{Invariant(variable++)}";
            var elementEquals = UniqueKeyEquals(
                aggregate.ElementType,
                leftElement,
                rightElement,
                resolver,
                rulePlan,
                ref variable);
            if (aggregate.Kind is ExpressAggregateKind.Array or ExpressAggregateKind.List)
            {
                return $"({left}).Count == ({right}).Count && global::System.Linq.Enumerable.All("
                    + $"global::System.Linq.Enumerable.Zip(({left}), ({right}), "
                    + $"({leftElement}, {rightElement}) => {elementEquals}), equal => equal)";
            }

            var candidate = $"uniqueCandidate{Invariant(variable++)}";
            var leftCountEquality = UniqueKeyEquals(
                aggregate.ElementType,
                leftElement,
                candidate,
                resolver,
                rulePlan,
                ref variable);
            var rightCountEquality = UniqueKeyEquals(
                aggregate.ElementType,
                rightElement,
                candidate,
                resolver,
                rulePlan,
                ref variable);
            return $"({left}).Count == ({right}).Count && global::System.Linq.Enumerable.All(({left}), "
                + $"{candidate} => global::System.Linq.Enumerable.Count(({left}), {leftElement} => {leftCountEquality}) "
                + $"== global::System.Linq.Enumerable.Count(({right}), {rightElement} => {rightCountEquality}))";
        }

        if (type is ExpressBoundNamedType named)
        {
            if (named.Declaration.Kind == ExpressDeclarationKind.Entity)
            {
                return $"global::System.Object.ReferenceEquals(({left}), ({right}))";
            }

            var declaration = resolver.GetDefinedType(named.Declaration);
            if (declaration is ExpressBoundDefinedType defined
                && defined.UnderlyingType is ExpressBoundSelectType select)
            {
                var branches = new List<string>();
                foreach (var alternative in resolver.GetSelectAlternatives(select))
                {
                    var leftSelected = $"uniqueLeftSelected{Invariant(variable++)}";
                    var rightSelected = $"uniqueRightSelected{Invariant(variable++)}";
                    var selectedEquality = UniqueKeyEquals(
                        new ExpressBoundNamedType(alternative, named.Span),
                        leftSelected,
                        rightSelected,
                        resolver,
                        rulePlan,
                        ref variable);
                    var method = $"TryGet{ExpressEntityProjection.ToPascalCase(alternative.Name)}";
                    branches.Add(
                        $"({left}).{method}(out var {leftSelected}) && "
                        + $"({right}).{method}(out var {rightSelected}) && ({selectedEquality})");
                }

                return $"({string.Join(" || ", branches)})";
            }

            if (declaration is ExpressBoundDefinedType
                { UnderlyingType: not ExpressBoundEnumerationType, } definedValue)
            {
                return UniqueKeyEquals(
                    definedValue.UnderlyingType,
                    $"({left}).Value",
                    $"({right}).Value",
                    resolver,
                    rulePlan,
                    ref variable);
            }
        }

        return "global::System.Collections.Generic.EqualityComparer<"
            + $"{UniqueValueTypeName(type, rulePlan)}>.Default.Equals(({left}), ({right}))";
    }

    private static string UniqueValueTypeName(
        ExpressBoundType type,
        ExpressReachableRulePlan rulePlan)
    {
        return type switch
        {
            ExpressBoundScalarType { Kind: ExpressScalarKind.Integer, } => "global::System.Numerics.BigInteger",
            ExpressBoundScalarType { Kind: ExpressScalarKind.Real or ExpressScalarKind.Number, } =>
                "global::TedToolkit.Step21.ExpressReal",
            ExpressBoundScalarType { Kind: ExpressScalarKind.Logical, } =>
                "global::TedToolkit.Step21.LogicalValue",
            ExpressBoundScalarType { Kind: ExpressScalarKind.Binary, } =>
                "global::TedToolkit.Step21.BinaryValue",
            ExpressBoundScalarType { Kind: ExpressScalarKind.Boolean, } => "global::System.Boolean",
            ExpressBoundScalarType { Kind: ExpressScalarKind.String, } => "global::System.String",
            ExpressBoundNamedType named => ExpressEntityProjection.ToPascalCase(named.Declaration.Name),
            _ => throw new InvalidOperationException(
                $"UNIQUE key type '{type.GetType().Name}' has no generated equality type in schema '{rulePlan.Schema.Name}'."),
        };
    }

    /// <summary>
    /// Adds the schema-level generated-entity assignability constraint to descriptor documentation.
    /// </summary>
    /// <param name="descriptor">The generated descriptor declaration.</param>
    /// <param name="schema">The owning schema.</param>
    /// <param name="entities">The generated entities validated by the descriptor.</param>
    /// <param name="rulePlan">The validated reachable rule closure.</param>
    internal static void AddDescriptorDocumentation(
        TypeDeclaration descriptor,
        ExpressBoundSchema schema,
        IReadOnlyList<ExpressEntityProjection> entities,
        ExpressReachableRulePlan rulePlan)
    {
        var table = new DescriptionTable(
            new DescriptionText("Constraint ID"),
            new DescriptionText("Normalized requirement"));
        table.AddItem(
            new DescriptionText($"{schema.Name.ToUpperInvariant()}.STRUCTURE.ENTITY_ASSIGNABILITY"),
            new DescriptionText(
                "Every registered value governed by this schema must be a concrete entity generated for the schema."));
        AddRuleDocumentationRows(table, schema, rulePlan);
        var remarks = new List<IDescriptionItem>()
        {
            Paragraph($"Schema: {schema.Name}. Declaration: SCHEMA {schema.Name}."),
            Paragraph("Normalized requirement: registered entities must belong to the generated schema model."),
            new DescriptionPara(new IDescriptionItem[]
            {
                new DescriptionText(
                    "Validation boundary: explicit structure validation and Part 21 read/write boundaries inspect "
                    + "registered entity assignability."),
            }),
            table,
        };
        if (entities.Count > 0)
        {
            var links = new List<IDescriptionItem>();
            links.Add(new DescriptionText("Generated entity values validated by this descriptor: "));
            for (var index = 0; index < entities.Count; index++)
            {
                if (index > 0)
                {
                    links.Add(new DescriptionText(", "));
                }

                links.Add(new DescriptionSee(new DataType(entities[index].Name)));
            }

            links.Add(new DescriptionText("."));
            remarks.Add(new DescriptionPara(links));
        }

        descriptor.AddRootDescription(new DescriptionRemarks(remarks));
    }

    private static void AddRuleDocumentationRows(
        DescriptionTable table,
        ExpressBoundSchema schema,
        ExpressReachableRulePlan rulePlan)
    {
        foreach (var declaration in schema.Declarations)
        {
            if (declaration is ExpressBoundEntity entity)
            {
                foreach (var inverse in entity.Attributes.Where(attribute =>
                             rulePlan.ReachableSingularInverseAttributes.Contains(attribute)))
                {
                    var role = $"{schema.Name}.{inverse.InverseEntity!.Name}.{inverse.InverseAttributeName}"
                        .ToUpperInvariant();
                    table.AddItem(
                        new DescriptionText(
                            $"{schema.Name}.{entity.Name}.{inverse.Name}.INVERSE_CARDINALITY"
                                .ToUpperInvariant()),
                        new DescriptionText(
                            "Normalized requirement: the entity-valued inverse must resolve exactly one distinct "
                            + $"compatible owner through forward role '{role}'. Validation boundary: first actual "
                            + "access during explicit structure validation or Part 21 read/write validation."));
                }

                AddWhereDocumentationRows(
                    table,
                    schema.Name,
                    entity.Name,
                    rulePlan.GetSemanticDeclaration(entity).RequiredChild("entityBody")
                        .ChildRules("whereClause")
                        .SingleOrDefault(),
                    rulePlan);
                var uniqueClause = rulePlan.GetSemanticDeclaration(entity).RequiredChild("entityBody")
                    .ChildRules("uniqueClause")
                    .SingleOrDefault();
                if (uniqueClause is not null)
                {
                    var rules = uniqueClause.ChildRules("uniqueRule").ToArray();
                    for (var index = 0; index < rules.Length; index++)
                    {
                        var rule = rules[index];
                        var label = rule.ChildRules("ruleLabelId").SingleOrDefault()?.Identifier
                            ?? $"RULE_{Invariant(index + 1)}";
                        table.AddItem(
                            new DescriptionText(
                                $"{schema.Name}.{entity.Name}.UNIQUE.{label}".ToUpperInvariant()),
                            new DescriptionText(
                                "Normalized requirement: the EXPRESS UNIQUE key "
                                + $"'{rule.SourceText}' must identify at most one entity candidate. "
                                + "Validation boundary: complete typed entity population validation."));
                    }
                }

                continue;
            }

            if (declaration is ExpressBoundDefinedType definedType)
            {
                AddWhereDocumentationRows(
                    table,
                    schema.Name,
                    definedType.Name,
                    rulePlan.GetSemanticDeclaration(definedType).ChildRules("whereClause").SingleOrDefault(),
                    rulePlan);
                continue;
            }

            if (declaration.Kind == ExpressDeclarationKind.Rule)
            {
                AddWhereDocumentationRows(
                    table,
                    schema.Name,
                    $"RULE.{declaration.Name}",
                    rulePlan.GetSemanticDeclaration(declaration).RequiredChild("whereClause"),
                    rulePlan);
            }
        }
    }

    private static void AddWhereDocumentationRows(
        DescriptionTable table,
        string schemaName,
        string declarationName,
        ExpressSemanticRule? whereClause,
        ExpressReachableRulePlan rulePlan)
    {
        if (whereClause is null)
        {
            return;
        }

        var rules = whereClause.ChildRules("domainRule").ToArray();
        for (var index = 0; index < rules.Length; index++)
        {
            var rule = rules[index];
            if (!rulePlan.ReachableRules.Contains(rule))
            {
                continue;
            }

            var label = rule.ChildRules("ruleLabelId").SingleOrDefault()?.Identifier
                ?? $"RULE_{Invariant(index + 1)}";
            var requirement = rule.RequiredChild("expression").SourceText;
            table.AddItem(
                new DescriptionText(
                    $"{schemaName}.{declarationName}.WHERE.{label}".ToUpperInvariant()),
                new DescriptionText(
                    $"Normalized requirement: '{requirement}' must evaluate to TRUE. "
                    + "Validation boundary: explicit structure validation and Part 21 read/write boundaries."));
        }
    }

    /// <summary>
    /// Adds deterministic source-schema and EXPRESS-declaration traceability to a generated type.
    /// </summary>
    /// <param name="type">The generated type declaration.</param>
    /// <param name="schemaName">The source EXPRESS schema name.</param>
    /// <param name="declaration">The normalized source declaration identity.</param>
    internal static void AddTypeDocumentation(TypeDeclaration type, string schemaName, string declaration)
    {
        var remarks = new List<IDescriptionItem>();
        remarks.Add(Paragraph($"Schema: {schemaName}. Declaration: {declaration}."));
        type.AddRootDescription(new DescriptionRemarks(remarks));
    }

    /// <summary>
    /// Creates one private generated entity-validation method.
    /// </summary>
    /// <param name="entity">The generated entity projection.</param>
    /// <param name="resolver">The generated type resolver.</param>
    /// <param name="rulePlan">The validated reachable rule closure.</param>
    /// <returns>The private validation method.</returns>
    internal static Method CreateEntityMethod(
        ExpressEntityProjection entity,
        ExpressGeneratedTypeResolver resolver,
        ExpressReachableRulePlan rulePlan)
    {
        var method = CreateMethod(
            $"Validate{entity.Name}",
            DataType.Void,
            TedToolkit.RoslynHelper.Accessibility.PRIVATE);
        method.IsStatic = true;
        method.AddParameter(SourceComposer.Parameter(new DataType($"I{entity.Name}"), "value"));
        method.AddParameter(SourceComposer.Parameter(DataType.String, "path"));
        method.AddParameter(SourceComposer.Parameter(new DataType(FAILURE_LIST_TYPE), "failures"));
        method.AddParameter(SourceComposer.Parameter(new DataType(POPULATION_TYPE), "entities"));
        AddInverseCacheParameter(method, rulePlan);

        var variable = 0;
        var temporaryOrdinal = 0;
        Func<string, string> allocateTemporaryName = prefix => prefix + Invariant(temporaryOrdinal++);
        for (var index = 0; index < entity.EffectiveAttributes.Count; index++)
        {
            if (entity.IsDerivedRedeclared(entity.EffectiveAttributes[index]))
            {
                continue;
            }

            AddAttributeValidation(
                method,
                entity,
                entity.EffectiveAttributes[index],
                index,
                resolver,
                ref variable);
            AddAttributeTypeWhereValidation(
                method,
                entity,
                entity.EffectiveAttributes[index],
                index,
                resolver,
                rulePlan,
                allocateTemporaryName,
                ref variable);
        }

        AddEntityWhereValidation(method, entity, rulePlan, allocateTemporaryName);

        AddSummary(method, $"Validates one {entity.Entity.Name} candidate without mutation.");
        return method;
    }

    /// <summary>
    /// Creates one private generated multi-leaf entity-validation method.
    /// </summary>
    /// <param name="entity">The synthetic multi-leaf entity projection.</param>
    /// <param name="resolver">The generated type resolver.</param>
    /// <param name="rulePlan">The validated reachable rule closure.</param>
    /// <returns>The private validation method.</returns>
    internal static Method CreateComplexEntityMethod(
        ExpressComplexEntityProjection entity,
        ExpressGeneratedTypeResolver resolver,
        ExpressReachableRulePlan rulePlan)
    {
        var method = CreateMethod(
            $"Validate{entity.Name}",
            DataType.Void,
            TedToolkit.RoslynHelper.Accessibility.PRIVATE);
        method.IsStatic = true;
        method.AddParameter(SourceComposer.Parameter(new DataType(entity.Name), "value"));
        method.AddParameter(SourceComposer.Parameter(DataType.String, "path"));
        method.AddParameter(SourceComposer.Parameter(new DataType(FAILURE_LIST_TYPE), "failures"));
        method.AddParameter(SourceComposer.Parameter(new DataType(POPULATION_TYPE), "entities"));
        AddInverseCacheParameter(method, rulePlan);

        var context = entity.Leaves[0];
        var attributes = entity.Components
            .SelectMany(ExpressComplexEntityProjection.GetComponentAttributes)
            .ToArray();
        var variable = 0;
        var temporaryOrdinal = 0;
        Func<string, string> allocateTemporaryName = prefix => prefix + Invariant(temporaryOrdinal++);
        for (var index = 0; index < attributes.Length; index++)
        {
            if (entity.IsDerivedRedeclared(attributes[index]))
            {
                continue;
            }

            AddAttributeValidation(
                method,
                context,
                attributes[index],
                index,
                resolver,
                ref variable,
                useInterfaceContract: true);
            AddAttributeTypeWhereValidation(
                method,
                context,
                attributes[index],
                index,
                resolver,
                rulePlan,
                allocateTemporaryName,
                ref variable,
                useInterfaceContract: true);
        }

        var visited = new HashSet<ExpressBoundSymbol>();
        foreach (var governingEntity in entity.Leaves.SelectMany(leaf =>
                     EntityRuleOwners(entity.Schema, leaf.Entity, visited)))
        {
            AddEntityWhereValidation(method, context, governingEntity, rulePlan, allocateTemporaryName);
        }

        AddSummary(method, "Validates one supported multi-leaf candidate without duplicate inherited checks.");
        return method;
    }

    private static void AddAttributeTypeWhereValidation(
        IStatementOwner owner,
        ExpressEntityProjection entity,
        ExpressEntityAttributeProjection attribute,
        int attributeIndex,
        ExpressGeneratedTypeResolver resolver,
        ExpressReachableRulePlan rulePlan,
        Func<string, string> allocateTemporaryName,
        ref int variable,
        bool useInterfaceContract = false)
    {
        if (!ContainsDefinedTypeWhere(
                attribute.Type,
                resolver,
                rulePlan,
                new HashSet<ExpressBoundSymbol>()))
        {
            return;
        }

        var valueName = $"ruleAttribute{Invariant(attributeIndex)}";
        var pathName = $"ruleAttributePath{Invariant(attributeIndex)}";
        var storageInterface = $"I{ExpressEntityProjection.ToPascalCase(attribute.DeclaringEntity.Name)}";
        if (!ReferenceEquals(entity.Schema.Identity, attribute.DeclaringEntity.Symbol.DeclaringSchema))
        {
            storageInterface = "global::TedToolkit.Step21.Generated."
                + $"{ExpressEntityProjection.ToPascalCase(attribute.DeclaringEntity.Symbol.DeclaringSchema.Name)}."
                + storageInterface;
        }

        var valueExpression = !useInterfaceContract
            && StringComparer.Ordinal.Equals(attribute.Name, attribute.StorageMemberName)
            && entity.Entity.DirectSupertypes.Count <= 1
            ? $"value.{attribute.Name}"
            : $"(({storageInterface})value).{attribute.Name}";
        owner.AddStatement(new CustomExpression(
            $"var {valueName} = {valueExpression}"));
        owner.AddStatement(new CustomExpression(
            $"var {pathName} = path + {Literal($".{attribute.Name}")}"));
        var isReference = resolver.Resolve(entity.Schema.Identity, attribute.Type).IsReferenceType;
        if (isReference || attribute.Attribute.IsOptional)
        {
            var present = new IfStatement(new CustomExpression($"{valueName} is not null"));
            var candidate = attribute.Attribute.IsOptional && !isReference
                ? $"{valueName}.Value"
                : valueName;
            AddTypeWhereValidation(
                present,
                attribute.Type,
                candidate,
                pathName,
                resolver,
                rulePlan,
                allocateTemporaryName,
                ref variable);
            owner.AddStatement(present);
            return;
        }

        AddTypeWhereValidation(
            owner,
            attribute.Type,
            valueName,
            pathName,
            resolver,
            rulePlan,
            allocateTemporaryName,
            ref variable);
    }

    private static void AddTypeWhereValidation(
        IStatementOwner owner,
        ExpressBoundType type,
        string valueExpression,
        string pathExpression,
        ExpressGeneratedTypeResolver resolver,
        ExpressReachableRulePlan rulePlan,
        Func<string, string> allocateTemporaryName,
        ref int variable,
        ISet<ExpressBoundSymbol>? activeNames = null)
    {
        activeNames ??= new HashSet<ExpressBoundSymbol>();
        if (type is ExpressBoundAggregateType aggregate)
        {
            if (!ContainsDefinedTypeWhere(
                    aggregate.ElementType,
                    resolver,
                    rulePlan,
                    new HashSet<ExpressBoundSymbol>()))
            {
                return;
            }

            var itemName = $"ruleAggregateItem{Invariant(variable++)}";
            var indexName = $"ruleAggregateIndex{Invariant(variable++)}";
            owner.AddStatement(new CustomExpression($"var {indexName} = 0"));
            var loop = new ForEachStatement(DataType.Var, itemName, new CustomExpression(valueExpression));
            AddTypeWhereValidation(
                loop,
                aggregate.ElementType,
                itemName,
                $"{pathExpression} + \"[\" + {indexName} + \"]\"",
                resolver,
                rulePlan,
                allocateTemporaryName,
                ref variable,
                activeNames);
            loop.AddStatement(new CustomExpression($"{indexName}++"));
            owner.AddStatement(loop);
            return;
        }

        if (type is not ExpressBoundNamedType named
            || named.Declaration.Kind == ExpressDeclarationKind.Entity)
        {
            return;
        }

        if (!activeNames.Add(named.Declaration))
        {
            return;
        }

        try
        {
            var declaration = resolver.GetDefinedType(named.Declaration);
            AddDefinedTypeWhereRules(
                owner,
                declaration,
                valueExpression,
                pathExpression,
                rulePlan,
                allocateTemporaryName);
            switch (declaration.UnderlyingType)
            {
                case ExpressBoundEnumerationType:
                    return;

                case ExpressBoundSelectType select:
                    foreach (var alternative in resolver.GetSelectAlternatives(select))
                    {
                        var alternativeType = new ExpressBoundNamedType(alternative, named.Span);
                        if (!ContainsDefinedTypeWhere(
                                alternativeType,
                                resolver,
                                rulePlan,
                                new HashSet<ExpressBoundSymbol>()))
                        {
                            continue;
                        }

                        var selectedName = $"ruleSelected{Invariant(variable++)}";
                        var branch = new IfStatement(new CustomExpression(
                            $"{valueExpression}.TryGet{ExpressEntityProjection.ToPascalCase(alternative.Name)}(out var {selectedName})"));
                        AddTypeWhereValidation(
                            branch,
                            alternativeType,
                            selectedName,
                            pathExpression,
                            resolver,
                            rulePlan,
                            allocateTemporaryName,
                            ref variable,
                            activeNames);
                        owner.AddStatement(branch);
                    }

                    return;

                default:
                    AddTypeWhereValidation(
                        owner,
                        declaration.UnderlyingType,
                        $"{valueExpression}.Value",
                        pathExpression,
                        resolver,
                        rulePlan,
                        allocateTemporaryName,
                        ref variable,
                        activeNames);
                    return;
            }
        }
        finally
        {
            activeNames.Remove(named.Declaration);
        }
    }

    private static void AddDefinedTypeWhereRules(
        IStatementOwner owner,
        ExpressBoundDefinedType declaration,
        string valueExpression,
        string pathExpression,
        ExpressReachableRulePlan rulePlan,
        Func<string, string> allocateTemporaryName)
    {
        var whereClause = rulePlan.GetSemanticDeclaration(declaration)
            .ChildRules("whereClause")
            .SingleOrDefault();
        if (whereClause is null)
        {
            return;
        }

        var rules = whereClause.ChildRules("domainRule").ToArray();
        for (var index = 0; index < rules.Length; index++)
        {
            var rule = rules[index];
            var expression = rulePlan.GetExpression(rule.RequiredChild("expression"));
            var generated = ExpressExpressionEmitter.Emit(
                expression,
                ExpressReachableRuleEmitter.CreateContext(
                    rulePlan,
                    valueExpression,
                    "entities",
                    allocateTemporaryName: allocateTemporaryName));
            var failed = RuleFailureCondition(expression, generated.Code);
            var label = rule.ChildRules("ruleLabelId").SingleOrDefault()?.Identifier
                ?? $"RULE_{Invariant(index + 1)}";
            var code = $"{rulePlan.Schema.Name}.{declaration.Name}.WHERE.{label}".ToUpperInvariant();
            AddWhereRuleEvaluation(
                owner,
                rulePlan,
                failed,
                AddFailure(
                    code,
                    pathExpression,
                    $"The EXPRESS WHERE rule '{expression.SourceText}' must evaluate to TRUE.",
                    rule.Span));
        }
    }

    private static bool ContainsDefinedTypeWhere(
        ExpressBoundType type,
        ExpressGeneratedTypeResolver resolver,
        ExpressReachableRulePlan rulePlan,
        ISet<ExpressBoundSymbol> visited)
    {
        if (type is ExpressBoundAggregateType aggregate)
        {
            return ContainsDefinedTypeWhere(aggregate.ElementType, resolver, rulePlan, visited);
        }

        if (type is not ExpressBoundNamedType named
            || named.Declaration.Kind == ExpressDeclarationKind.Entity
            || !visited.Add(named.Declaration))
        {
            return false;
        }

        var declaration = resolver.GetDefinedType(named.Declaration);
        if (rulePlan.GetSemanticDeclaration(declaration).ChildRules("whereClause").Any())
        {
            return true;
        }

        return declaration.UnderlyingType switch
        {
            ExpressBoundSelectType select => resolver.GetSelectAlternatives(select).Any(alternative =>
                ContainsDefinedTypeWhere(
                    new ExpressBoundNamedType(alternative, named.Span),
                    resolver,
                    rulePlan,
                    visited)),
            ExpressBoundEnumerationType => false,
            _ => ContainsDefinedTypeWhere(declaration.UnderlyingType, resolver, rulePlan, visited),
        };
    }

    private static void AddEntityWhereValidation(
        IStatementOwner owner,
        ExpressEntityProjection entity,
        ExpressReachableRulePlan rulePlan,
        Func<string, string> allocateTemporaryName)
    {
        foreach (var governingEntity in EntityRuleOwners(
                     entity.Schema,
                     entity.Entity,
                     new HashSet<ExpressBoundSymbol>()))
        {
            AddEntityWhereValidation(owner, entity, governingEntity, rulePlan, allocateTemporaryName);
        }
    }

    private static void AddEntityWhereValidation(
        IStatementOwner owner,
        ExpressEntityProjection candidateEntity,
        ExpressBoundEntity governingEntity,
        ExpressReachableRulePlan rulePlan,
        Func<string, string> allocateTemporaryName)
    {
        var whereClause = rulePlan.GetSemanticDeclaration(governingEntity).RequiredChild("entityBody")
            .ChildRules("whereClause")
            .SingleOrDefault();
        if (whereClause is null)
        {
            return;
        }

        var rules = whereClause.ChildRules("domainRule").ToArray();
        for (var index = 0; index < rules.Length; index++)
        {
            var rule = rules[index];
            var expressionSyntax = rule.RequiredChild("expression");
            var expression = rulePlan.GetExpression(expressionSyntax);
            var generated = ExpressExpressionEmitter.Emit(
                expression,
                ExpressReachableRuleEmitter.CreateContext(
                    rulePlan,
                    "value",
                    "entities",
                    allocateTemporaryName: allocateTemporaryName,
                    selfEntity: candidateEntity.Entity.Symbol));
            var failed = RuleFailureCondition(expression, generated.Code);
            var label = rule.ChildRules("ruleLabelId").SingleOrDefault()?.Identifier
                ?? $"RULE_{Invariant(index + 1)}";
            var code = $"{candidateEntity.Schema.Name}.{governingEntity.Name}.WHERE.{label}"
                .ToUpperInvariant();
            AddWhereRuleEvaluation(
                owner,
                rulePlan,
                failed,
                AddFailure(
                    code,
                    "path",
                    $"The EXPRESS WHERE rule '{expression.SourceText}' must evaluate to TRUE.",
                    rule.Span));
        }
    }

    private static void AddWhereRuleEvaluation(
        IStatementOwner owner,
        ExpressReachableRulePlan rulePlan,
        string failureCondition,
        CustomExpression failure)
    {
        var evaluation = new IfStatement(new CustomExpression(failureCondition))
            .AddStatement(failure);
        if (rulePlan.ReachableSingularInverseAttributes.Count == 0)
        {
            owner.AddStatement(evaluation);
            return;
        }

        var guarded = new TryStatement()
            .AddStatement(evaluation)
            .AddCatch(new CatchClause(
                    new DataType("__ExpressInverseUnavailableException"),
                    "inverseUnavailable")
                .AddStatement(new CustomExpression("_ = inverseUnavailable")));
        owner.AddStatement(guarded);
    }

    private static void AddInverseCacheParameter(Method method, ExpressReachableRulePlan rulePlan)
    {
        if (rulePlan.ReachableSingularInverseAttributes.Count == 0)
        {
            return;
        }

        method.AddParameter(SourceComposer.Parameter(new DataType(INVERSE_CACHE_TYPE), "inverseCache"));
    }

    private static IEnumerable<ExpressBoundEntity> EntityRuleOwners(
        ExpressBoundSchema schema,
        ExpressBoundEntity entity,
        ISet<ExpressBoundSymbol> visited)
    {
        if (!visited.Add(entity.Symbol))
        {
            yield break;
        }

        foreach (var supertype in entity.DirectSupertypes)
        {
            var parent = schema.Declarations.OfType<ExpressBoundEntity>()
                .SingleOrDefault(candidate => ReferenceEquals(candidate.Symbol, supertype));
            if (parent is null)
            {
                continue;
            }

            foreach (var ancestor in EntityRuleOwners(schema, parent, visited))
            {
                yield return ancestor;
            }
        }

        yield return entity;
    }

    private static string RuleFailureCondition(
        ExpressBoundExpression expression,
        string generatedCode)
    {
        return expression.Type.Kind switch
        {
            ExpressExpressionTypeKind.Boolean or ExpressExpressionTypeKind.Logical =>
                $"({ExpressExpressionEmitter.AsLogical(expression, generatedCode)}) "
                + "!= global::TedToolkit.Step21.LogicalValue.True",
            ExpressExpressionTypeKind.Indeterminate => "true",
            _ => throw new InvalidOperationException(
                $"WHERE expression '{expression.SourceText}' does not produce BOOLEAN or LOGICAL."),
        };
    }

    /// <summary>
    /// Adds deterministic EXPRESS declaration, lifecycle, and constraint documentation to a generated property.
    /// </summary>
    /// <param name="property">The generated property.</param>
    /// <param name="entity">The generated entity projection.</param>
    /// <param name="attribute">The projected attribute.</param>
    /// <param name="resolver">The generated type resolver.</param>
    /// <param name="isMutable">Whether the generated member has a setter.</param>
    internal static void AddPropertyDocumentation(
        Property property,
        ExpressEntityProjection entity,
        ExpressEntityAttributeProjection attribute,
        ExpressGeneratedTypeResolver resolver,
        bool isMutable)
    {
        var constraints = GetConstraints(entity, attribute, resolver);
        var declaration = $"{attribute.Attribute.Name} : "
            + $"{(attribute.Attribute.IsOptional ? "OPTIONAL " : "")}"
            + $"{ExpressTypeDocumentation.Format(attribute.Type)};";
        var valueText = isMutable
            ? "The current mutable candidate. Assignment performs no schema validation."
            : "The current candidate exposed by the generated entity contract.";
        property.AddRootDescription(new DescriptionValue(
            new IDescriptionItem[] { new DescriptionText(valueText), }));

        var remarks = new List<IDescriptionItem>()
        {
            Paragraph($"Schema: {entity.Schema.Name}. Declaration: {declaration}"),
            Paragraph($"Normalized requirement: {NormalizedRequirement(attribute)}"),
            new DescriptionPara(new IDescriptionItem[]
            {
                new DescriptionText(
                    "Validation boundary: setters and aggregate mutations remain unchecked; explicit structure "
                    + "validation and Part 21 read/write boundaries inspect the current value."),
            }),
        };
        if (constraints.Count > 0)
        {
            var table = new DescriptionTable(
                new DescriptionText("Constraint ID"),
                new DescriptionText("Normalized requirement"));
            foreach (var constraint in constraints)
            {
                table.AddItem(
                    new DescriptionText(constraint.Code),
                    new DescriptionText(constraint.Requirement));
            }

            remarks.Add(table);
        }

        property.AddRootDescription(new DescriptionRemarks(remarks));
    }

    private static Method CreateMethod(
        string name,
        DataType returnType,
        TedToolkit.RoslynHelper.Accessibility accessibility)
    {
        var method = SourceComposer<ExpressIncrementalGenerator>.Method(
            name,
            SourceComposer.ReturnType(returnType));
        method.Accessibility = accessibility;
        return method;
    }

    private static void AddAttributeValidation(
        IStatementOwner owner,
        ExpressEntityProjection entity,
        ExpressEntityAttributeProjection attribute,
        int attributeIndex,
        ExpressGeneratedTypeResolver resolver,
        ref int variable,
        bool useInterfaceContract = false)
    {
        var prefix = ConstraintPrefix(entity, attribute);
        var valueName = $"attribute{Invariant(attributeIndex)}";
        var pathName = $"attributePath{Invariant(attributeIndex)}";
        var storageInterface = $"I{ExpressEntityProjection.ToPascalCase(attribute.DeclaringEntity.Name)}";
        if (!ReferenceEquals(entity.Schema.Identity, attribute.DeclaringEntity.Symbol.DeclaringSchema))
        {
            storageInterface = "global::TedToolkit.Step21.Generated."
                + $"{ExpressEntityProjection.ToPascalCase(attribute.DeclaringEntity.Symbol.DeclaringSchema.Name)}."
                + storageInterface;
        }

        var valueExpression = !useInterfaceContract
            && StringComparer.Ordinal.Equals(attribute.Name, attribute.StorageMemberName)
            && entity.Entity.DirectSupertypes.Count <= 1
            ? $"value.{attribute.Name}"
            : $"(({storageInterface})value).{attribute.Name}";
        owner.AddStatement(new CustomExpression(
            $"var {valueName} = {valueExpression}"));
        owner.AddStatement(new CustomExpression(
            $"var {pathName} = path + {Literal($".{attribute.Name}")}"));

        var immediateReference = resolver.Resolve(entity.Schema.Identity, attribute.Type).IsReferenceType;
        var canBeNull = immediateReference || attribute.Attribute.IsOptional;
        if (!canBeNull)
        {
            AddNonNullTypeValidation(
                owner,
                entity.Schema.Identity,
                attribute.Type,
                valueName,
                pathName,
                prefix,
                attribute.Attribute.Span,
                0,
                resolver,
                $"{prefix}.REQUIRED",
                null,
                ref variable);
            AddDynamicAggregateUpperBoundValidation(
                owner,
                entity,
                attribute,
                valueName,
                pathName,
                prefix,
                resolver);
            return;
        }

        var nullBranch = new IfStatement(new CustomExpression($"{valueName} is null"));
        if (!attribute.Attribute.IsOptional)
        {
            nullBranch.AddStatement(AddFailure(
                $"{prefix}.REQUIRED",
                pathName,
                "The mandatory EXPRESS attribute has no value.",
                attribute.Attribute.Span));
        }

        var presentBranch = new IfStatement(new CustomExpression($"{valueName} is not null"));
        var presentValue = attribute.Attribute.IsOptional && !immediateReference
            ? $"{valueName}.Value"
            : valueName;
        AddNonNullTypeValidation(
            presentBranch,
            entity.Schema.Identity,
            attribute.Type,
            presentValue,
            pathName,
            prefix,
            attribute.Attribute.Span,
            0,
            resolver,
            $"{prefix}.REQUIRED",
            null,
            ref variable);
        owner.AddStatement(nullBranch);
        owner.AddStatement(presentBranch);
        AddDynamicAggregateUpperBoundValidation(
            owner,
            entity,
            attribute,
            valueName,
            pathName,
            prefix,
            resolver);
    }

    private static void AddDynamicAggregateUpperBoundValidation(
        IStatementOwner owner,
        ExpressEntityProjection entity,
        ExpressEntityAttributeProjection attribute,
        string valueExpression,
        string pathExpression,
        string prefix,
        ExpressGeneratedTypeResolver resolver)
    {
        if (attribute.Type is not ExpressBoundAggregateType aggregate
            || aggregate.UpperBoundText is not { } upperBoundName
            || int.TryParse(
                aggregate.ResolvedUpperBoundText ?? upperBoundName,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out _))
        {
            return;
        }

        var boundAttribute = entity.EffectiveAttributes.SingleOrDefault(candidate =>
            StringComparer.OrdinalIgnoreCase.Equals(candidate.Attribute.Name, upperBoundName));
        if (boundAttribute?.Attribute.IsOptional != false)
        {
            return;
        }

        var ownerInterface = "I" + ExpressEntityProjection.ToPascalCase(
            boundAttribute.DeclaringEntity.Name);
        if (!ReferenceEquals(entity.Schema.Identity, boundAttribute.DeclaringEntity.Symbol.DeclaringSchema))
        {
            ownerInterface = "global::TedToolkit.Step21.Generated."
                + ExpressEntityProjection.ToPascalCase(boundAttribute.DeclaringEntity.Symbol.DeclaringSchema.Name)
                + "."
                + ownerInterface;
        }

        var boundCode = $"(({ownerInterface})value).{boundAttribute.Name}";
        var boundType = boundAttribute.Type;
        while (boundType is ExpressBoundNamedType named
               && named.Declaration.Kind != ExpressDeclarationKind.Entity)
        {
            boundCode = $"({boundCode}).Value";
            boundType = resolver.GetDefinedType(named.Declaration).UnderlyingType;
        }

        boundCode = boundType switch
        {
            ExpressBoundScalarType { Kind: ExpressScalarKind.Integer, } =>
                $"checked((int)({boundCode}))",
            ExpressBoundScalarType { Kind: ExpressScalarKind.Number, } =>
                $"checked((int)({boundCode}).ToIntegerTruncated())",
            _ => throw new InvalidOperationException(
                "A dynamic aggregate bound must resolve to an integer-valued attribute."),
        };
        owner.AddStatement(new IfStatement(new CustomExpression(
                $"{valueExpression} is not null && {valueExpression}.Count > {boundCode}"))
            .AddStatement(AddFailure(
                $"{prefix}.AGGREGATE_0.UPPER_BOUND",
                pathExpression,
                $"The aggregate cardinality must not exceed the current '{upperBoundName}' attribute value.",
                attribute.Attribute.Span)));
    }

    private static void AddValueValidation(
        IStatementOwner owner,
        ExpressBoundSchemaIdentity schema,
        ExpressBoundType type,
        string valueExpression,
        string pathExpression,
        string prefix,
        ExpressSourceSpan source,
        int aggregateDepth,
        ExpressGeneratedTypeResolver resolver,
        string missingCode,
        string? invalidEntityCode,
        ref int variable,
        ISet<ExpressBoundSymbol>? activeNamedTypes = null)
    {
        activeNamedTypes ??= new HashSet<ExpressBoundSymbol>();
        var immediateReference = resolver.Resolve(schema, type).IsReferenceType;
        if (!immediateReference)
        {
            AddNonNullTypeValidation(
                owner,
                schema,
                type,
                valueExpression,
                pathExpression,
                prefix,
                source,
                aggregateDepth,
                resolver,
                missingCode,
                invalidEntityCode,
                ref variable,
                activeNamedTypes);
            return;
        }

        var local = $"candidate{Invariant(variable++)}";
        owner.AddStatement(new CustomExpression($"var {local} = {valueExpression}"));
        var nullBranch = new IfStatement(new CustomExpression($"{local} is null"))
            .AddStatement(AddFailure(
                missingCode,
                pathExpression,
                "The EXPRESS value is null where a present value is required.",
                source));
        var presentBranch = new IfStatement(new CustomExpression($"{local} is not null"));
        AddNonNullTypeValidation(
            presentBranch,
            schema,
            type,
            local,
            pathExpression,
            prefix,
            source,
            aggregateDepth,
            resolver,
            missingCode,
            invalidEntityCode,
            ref variable,
            activeNamedTypes);
        owner.AddStatement(nullBranch);
        owner.AddStatement(presentBranch);
    }

    private static void AddNonNullTypeValidation(
        IStatementOwner owner,
        ExpressBoundSchemaIdentity schema,
        ExpressBoundType type,
        string valueExpression,
        string pathExpression,
        string prefix,
        ExpressSourceSpan source,
        int aggregateDepth,
        ExpressGeneratedTypeResolver resolver,
        string missingCode,
        string? invalidEntityCode,
        ref int variable,
        ISet<ExpressBoundSymbol>? activeNamedTypes = null)
    {
        activeNamedTypes ??= new HashSet<ExpressBoundSymbol>();
        switch (type)
        {
            case ExpressBoundScalarType:
                return;

            case ExpressBoundAggregateType aggregate:
                AddAggregateValidation(
                    owner,
                    schema,
                    aggregate,
                    valueExpression,
                    pathExpression,
                    prefix,
                    source,
                    aggregateDepth,
                    resolver,
                    ref variable,
                    activeNamedTypes);
                return;

            case ExpressBoundNamedType named when named.Declaration.Kind == ExpressDeclarationKind.Entity:
                owner.AddStatement(new IfStatement(new CustomExpression(
                        $"{valueExpression} is not global::TedToolkit.Step21.Entity"))
                    .AddStatement(AddFailure(
                        invalidEntityCode ?? $"{prefix}.ENTITY",
                        pathExpression,
                        "The entity-valued candidate is not a runtime Entity and cannot participate in the exchange graph.",
                        source)));
                return;

            case ExpressBoundNamedType named:
                AddDefinedTypeValidation(
                    owner,
                    schema,
                    named,
                    valueExpression,
                    pathExpression,
                    prefix,
                    source,
                    aggregateDepth,
                    resolver,
                    missingCode,
                    invalidEntityCode,
                    ref variable,
                    activeNamedTypes);
                return;

            default:
                throw new InvalidOperationException(
                    $"Unsupported generated validation type '{type.GetType().Name}'.");
        }
    }

    private static void AddDefinedTypeValidation(
        IStatementOwner owner,
        ExpressBoundSchemaIdentity schema,
        ExpressBoundNamedType named,
        string valueExpression,
        string pathExpression,
        string prefix,
        ExpressSourceSpan source,
        int aggregateDepth,
        ExpressGeneratedTypeResolver resolver,
        string missingCode,
        string? invalidEntityCode,
        ref int variable,
        ISet<ExpressBoundSymbol> activeNamedTypes)
    {
        if (!activeNamedTypes.Add(named.Declaration))
        {
            return;
        }

        var declaration = resolver.GetDefinedType(named.Declaration);
        switch (declaration.UnderlyingType)
        {
            case ExpressBoundEnumerationType enumeration:
                var allowed = resolver.GetEnumerationValues(enumeration);
                owner.AddStatement(new IfStatement(new CustomExpression($"{valueExpression}.Value is null"))
                    .AddStatement(AddFailure(
                        missingCode,
                        pathExpression,
                        "The EXPRESS enumeration has no selected value.",
                        source)));
                if (!enumeration.IsExtensible)
                {
                    var condition = $"{valueExpression}.Value is not null && !("
                        + string.Join(
                            " || ",
                            allowed.Select(value =>
                                $"global::System.String.Equals({valueExpression}.Value, "
                                + $"{Literal(value.ToUpperInvariant())}, global::System.StringComparison.Ordinal)"))
                        + ")";
                    owner.AddStatement(new IfStatement(new CustomExpression(condition))
                        .AddStatement(AddFailure(
                            $"{prefix}.ENUMERATION",
                            pathExpression,
                            "The enumeration candidate is not one of the permitted EXPRESS values.",
                            source)));
                }

                return;

            case ExpressBoundSelectType select:
                foreach (var alternative in resolver.GetSelectAlternatives(select))
                {
                    var alternativeType = new ExpressBoundNamedType(alternative, named.Span);
                    var selectedName = $"selected{Invariant(variable++)}";
                    var branch = new IfStatement(new CustomExpression(
                        $"{valueExpression}.TryGet{ExpressEntityProjection.ToPascalCase(alternative.Name)}(out var {selectedName})"));
                    AddValueValidation(
                        branch,
                        schema,
                        alternativeType,
                        selectedName,
                        pathExpression,
                        prefix,
                        source,
                        aggregateDepth,
                        resolver,
                        missingCode,
                        invalidEntityCode,
                        ref variable,
                        new HashSet<ExpressBoundSymbol>(activeNamedTypes));
                    owner.AddStatement(branch);
                }

                return;

            default:
                AddValueValidation(
                    owner,
                    schema,
                    declaration.UnderlyingType,
                    $"{valueExpression}.Value",
                    pathExpression,
                    prefix,
                    source,
                    aggregateDepth,
                    resolver,
                    missingCode,
                    invalidEntityCode,
                    ref variable,
                    activeNamedTypes);
                return;
        }
    }

    private static void AddAggregateValidation(
        IStatementOwner owner,
        ExpressBoundSchemaIdentity schema,
        ExpressBoundAggregateType aggregate,
        string valueExpression,
        string pathExpression,
        string prefix,
        ExpressSourceSpan source,
        int aggregateDepth,
        ExpressGeneratedTypeResolver resolver,
        ref int variable,
        ISet<ExpressBoundSymbol> activeNamedTypes)
    {
        var aggregatePrefix = $"{prefix}.AGGREGATE_{Invariant(aggregateDepth)}";
        var hasLowerBound = TryGetLowerBound(aggregate, out var lowerBound);
        var hasUpperBound = TryGetUpperBound(aggregate, out var upperBound);
        var expectedUnique = aggregate.Kind == ExpressAggregateKind.Set || aggregate.IsUnique;
        var shapeConditions = new List<string>();
        if (aggregate.Kind == ExpressAggregateKind.Array)
        {
            if (hasLowerBound)
            {
                shapeConditions.Add($"{valueExpression}.LowerIndex != {Invariant(lowerBound)}");
            }

            if (hasUpperBound && upperBound.HasValue)
            {
                shapeConditions.Add($"{valueExpression}.UpperIndex != {Invariant(upperBound.Value)}");
            }

            shapeConditions.Add($"{valueExpression}.IsOptional != {Boolean(aggregate.IsOptional)}");
            shapeConditions.Add($"{valueExpression}.IsUnique != {Boolean(aggregate.IsUnique)}");
        }
        else
        {
            if (hasLowerBound)
            {
                shapeConditions.Add($"{valueExpression}.LowerBound != {Invariant(lowerBound)}");
            }

            if (hasUpperBound)
            {
                var upper = upperBound.HasValue ? Invariant(upperBound.Value) : "null";
                shapeConditions.Add($"{valueExpression}.UpperBound != {upper}");
            }

            if (aggregate.Kind == ExpressAggregateKind.List)
            {
                shapeConditions.Add($"{valueExpression}.IsUnique != {Boolean(aggregate.IsUnique)}");
            }
        }

        if (shapeConditions.Count > 0)
        {
            owner.AddStatement(new IfStatement(new CustomExpression(string.Join(" || ", shapeConditions)))
                .AddStatement(AddFailure(
                    $"{aggregatePrefix}.SHAPE",
                    pathExpression,
                    "The aggregate candidate metadata does not match the generated EXPRESS declaration.",
                    source)));
        }

        if (aggregate.Kind != ExpressAggregateKind.Array)
        {
            if (hasLowerBound && lowerBound > 0)
            {
                owner.AddStatement(new IfStatement(new CustomExpression(
                        $"{valueExpression}.Count < {Invariant(lowerBound)}"))
                    .AddStatement(AddFailure(
                        $"{aggregatePrefix}.LOWER_BOUND",
                        pathExpression,
                        $"The aggregate must contain at least {Invariant(lowerBound)} element(s).",
                        source)));
            }

            if (hasUpperBound && upperBound.HasValue)
            {
                owner.AddStatement(new IfStatement(new CustomExpression(
                        $"{valueExpression}.Count > {Invariant(upperBound.Value)}"))
                    .AddStatement(AddFailure(
                        $"{aggregatePrefix}.UPPER_BOUND",
                        pathExpression,
                        $"The aggregate must contain no more than {Invariant(upperBound.Value)} element(s).",
                        source)));
            }
        }

        if (expectedUnique)
        {
            owner.AddStatement(new IfStatement(new CustomExpression(
                    $"global::System.Linq.Enumerable.Count({valueExpression}) != "
                    + $"global::System.Linq.Enumerable.Count(global::System.Linq.Enumerable.Distinct({valueExpression}))"))
                .AddStatement(AddFailure(
                    $"{aggregatePrefix}.UNIQUE",
                    pathExpression,
                    "The aggregate must not contain duplicate element candidates.",
                    source)));
        }

        if (aggregate.Kind == ExpressAggregateKind.Array)
        {
            AddArrayElements(
                owner,
                schema,
                aggregate,
                valueExpression,
                pathExpression,
                prefix,
                aggregatePrefix,
                source,
                aggregateDepth,
                resolver,
                ref variable,
                activeNamedTypes);
            return;
        }

        AddVariableAggregateElements(
            owner,
            schema,
            aggregate,
            valueExpression,
            pathExpression,
            prefix,
            aggregatePrefix,
            source,
            aggregateDepth,
            resolver,
            ref variable,
            activeNamedTypes);
    }

    private static void AddArrayElements(
        IStatementOwner owner,
        ExpressBoundSchemaIdentity schema,
        ExpressBoundAggregateType aggregate,
        string valueExpression,
        string pathExpression,
        string prefix,
        string aggregatePrefix,
        ExpressSourceSpan source,
        int aggregateDepth,
        ExpressGeneratedTypeResolver resolver,
        ref int variable,
        ISet<ExpressBoundSymbol> activeNamedTypes)
    {
        var indexName = $"arrayIndex{Invariant(variable++)}";
        var itemName = $"{valueExpression}[{indexName}]";
        var loop = new ForEachStatement(
            DataType.Int,
            indexName,
            new CustomExpression(
                $"global::System.Linq.Enumerable.Range({valueExpression}.LowerIndex, {valueExpression}.Count)"));
        var missing = new IfStatement(new CustomExpression(
            $"!{valueExpression}.IsSet({indexName})"));
        if (!aggregate.IsOptional)
        {
            missing.AddStatement(AddFailure(
                $"{aggregatePrefix}.REQUIRED_SLOT",
                $"{pathExpression} + \"[\" + {indexName} + \"]\"",
                "The required EXPRESS ARRAY slot is unset.",
                source));
        }

        var present = new IfStatement(new CustomExpression(
            $"{valueExpression}.IsSet({indexName})"));
        AddValueValidation(
            present,
            schema,
            aggregate.ElementType,
            itemName,
            $"{pathExpression} + \"[\" + {indexName} + \"]\"",
            prefix,
            source,
            aggregateDepth + 1,
            resolver,
            $"{aggregatePrefix}.ELEMENT",
            $"{aggregatePrefix}.ELEMENT",
            ref variable,
            activeNamedTypes);
        loop.AddStatement(missing);
        loop.AddStatement(present);
        owner.AddStatement(loop);
    }

    private static void AddVariableAggregateElements(
        IStatementOwner owner,
        ExpressBoundSchemaIdentity schema,
        ExpressBoundAggregateType aggregate,
        string valueExpression,
        string pathExpression,
        string prefix,
        string aggregatePrefix,
        ExpressSourceSpan source,
        int aggregateDepth,
        ExpressGeneratedTypeResolver resolver,
        ref int variable,
        ISet<ExpressBoundSymbol> activeNamedTypes)
    {
        var indexName = $"aggregateIndex{Invariant(variable++)}";
        var itemName = $"aggregateItem{Invariant(variable++)}";
        owner.AddStatement(new CustomExpression($"var {indexName} = 0"));
        var loop = new ForEachStatement(DataType.Var, itemName, new CustomExpression(valueExpression));
        AddValueValidation(
            loop,
            schema,
            aggregate.ElementType,
            itemName,
            $"{pathExpression} + \"[\" + {indexName} + \"]\"",
            prefix,
            source,
            aggregateDepth + 1,
            resolver,
            $"{aggregatePrefix}.ELEMENT",
            $"{aggregatePrefix}.ELEMENT",
            ref variable,
            activeNamedTypes);
        loop.AddStatement(new CustomExpression($"{indexName}++"));
        owner.AddStatement(loop);
    }

    private static CustomExpression AddFailure(
        string code,
        string pathExpression,
        string message,
        ExpressSourceSpan source)
    {
        var location = source.Start;
        var filePath = StableFilePath(location.FilePath);
        return new(
            "failures.Add(new global::TedToolkit.Step21.ValidationFailure("
            + $"{Literal(code)}, {pathExpression}, {Literal(message)}, "
            + "new global::TedToolkit.Step21.SourceLocation("
            + $"{Literal(filePath)}, {Invariant(location.Line)}, {Invariant(location.Column)})))");
    }

    private static List<StructuralConstraint> GetConstraints(
        ExpressEntityProjection entity,
        ExpressEntityAttributeProjection attribute,
        ExpressGeneratedTypeResolver resolver)
    {
        var constraints = new List<StructuralConstraint>();
        var prefix = ConstraintPrefix(entity, attribute);
        if (!attribute.Attribute.IsOptional && CanRepresentMissing(attribute.Type, resolver))
        {
            constraints.Add(new(
                $"{prefix}.REQUIRED",
                "A mandatory attribute must contain a present value."));
        }

        AddTypeConstraints(
            constraints,
            attribute.Type,
            prefix,
            0,
            resolver,
            insideAggregate: false,
            visitedNamedTypes: new HashSet<ExpressBoundSymbol>());
        return constraints
            .GroupBy(constraint => constraint.Code, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
    }

    private static void AddTypeConstraints(
        ICollection<StructuralConstraint> constraints,
        ExpressBoundType type,
        string prefix,
        int aggregateDepth,
        ExpressGeneratedTypeResolver resolver,
        bool insideAggregate,
        ISet<ExpressBoundSymbol> visitedNamedTypes)
    {
        switch (type)
        {
            case ExpressBoundScalarType:
                return;

            case ExpressBoundAggregateType aggregate:
                AddAggregateConstraints(
                    constraints,
                    aggregate,
                    prefix,
                    aggregateDepth,
                    resolver,
                    visitedNamedTypes);
                return;

            case ExpressBoundNamedType named when named.Declaration.Kind == ExpressDeclarationKind.Entity:
                constraints.Add(new(
                    insideAggregate
                        ? $"{prefix}.AGGREGATE_{Invariant(aggregateDepth - 1)}.ELEMENT"
                        : $"{prefix}.ENTITY",
                    "An entity-valued candidate must be a runtime Entity assignable to the declared entity interface."));
                return;

            case ExpressBoundNamedType named:
                if (!visitedNamedTypes.Add(named.Declaration))
                {
                    return;
                }

                var underlying = resolver.GetDefinedType(named.Declaration).UnderlyingType;
                if (underlying is ExpressBoundEnumerationType enumeration)
                {
                    if (!enumeration.IsExtensible)
                    {
                        constraints.Add(new(
                            $"{prefix}.ENUMERATION",
                            "The enumeration value must belong to the declared closed value domain."));
                    }

                    return;
                }

                if (underlying is ExpressBoundSelectType select)
                {
                    foreach (var alternative in resolver.GetSelectAlternatives(select))
                    {
                        AddTypeConstraints(
                            constraints,
                            new ExpressBoundNamedType(alternative, named.Span),
                            prefix,
                            aggregateDepth,
                            resolver,
                            insideAggregate,
                            visitedNamedTypes);
                    }

                    return;
                }

                AddTypeConstraints(
                    constraints,
                    underlying,
                    prefix,
                    aggregateDepth,
                    resolver,
                    insideAggregate,
                    visitedNamedTypes);
                return;
        }
    }

    private static void AddAggregateConstraints(
        ICollection<StructuralConstraint> constraints,
        ExpressBoundAggregateType aggregate,
        string prefix,
        int aggregateDepth,
        ExpressGeneratedTypeResolver resolver,
        ISet<ExpressBoundSymbol> visitedNamedTypes)
    {
        var aggregatePrefix = $"{prefix}.AGGREGATE_{Invariant(aggregateDepth)}";
        var hasLowerBound = TryGetLowerBound(aggregate, out var lowerBound);
        var hasUpperBound = TryGetUpperBound(aggregate, out var upperBound);
        var hasShapeCheck = hasLowerBound
            || hasUpperBound
            || aggregate.Kind is ExpressAggregateKind.Array or ExpressAggregateKind.List;
        if (hasShapeCheck)
        {
            constraints.Add(new(
                $"{aggregatePrefix}.SHAPE",
                "Known aggregate metadata must match the declared bounds, OPTIONAL-slot, and UNIQUE modifiers."));
        }

        if (aggregate.Kind != ExpressAggregateKind.Array && hasLowerBound && lowerBound > 0)
        {
            constraints.Add(new(
                $"{aggregatePrefix}.LOWER_BOUND",
                $"Cardinality must be at least {Invariant(lowerBound)}."));
        }

        if (aggregate.Kind != ExpressAggregateKind.Array && hasUpperBound && upperBound.HasValue)
        {
            constraints.Add(new(
                $"{aggregatePrefix}.UPPER_BOUND",
                $"Cardinality must be no greater than {Invariant(upperBound.Value)}."));
        }

        if (aggregate.Kind == ExpressAggregateKind.Array && !aggregate.IsOptional)
        {
            constraints.Add(new(
                $"{aggregatePrefix}.REQUIRED_SLOT",
                "Every declared ARRAY slot must be assigned."));
        }

        if (aggregate.Kind == ExpressAggregateKind.Set || aggregate.IsUnique)
        {
            constraints.Add(new(
                $"{aggregatePrefix}.UNIQUE",
                "Element candidates must be pairwise distinct."));
        }

        if (CanRepresentMissing(aggregate.ElementType, resolver))
        {
            constraints.Add(new(
                $"{aggregatePrefix}.ELEMENT",
                "Every present aggregate occurrence must be assignable to the declared element type."));
        }

        AddTypeConstraints(
            constraints,
            aggregate.ElementType,
            prefix,
            aggregateDepth + 1,
            resolver,
            insideAggregate: true,
            visitedNamedTypes: visitedNamedTypes);
    }

    private static bool CanRepresentMissing(
        ExpressBoundType type,
        ExpressGeneratedTypeResolver resolver)
    {
        return CanRepresentMissing(type, resolver, new HashSet<ExpressBoundSymbol>());
    }

    private static bool CanRepresentMissing(
        ExpressBoundType type,
        ExpressGeneratedTypeResolver resolver,
        ISet<ExpressBoundSymbol> visitedNamedTypes)
    {
        return type switch
        {
            ExpressBoundScalarType scalar => scalar.Kind is ExpressScalarKind.String or ExpressScalarKind.Binary,
            ExpressBoundAggregateType => true,
            ExpressBoundNamedType named when named.Declaration.Kind == ExpressDeclarationKind.Entity => true,
            ExpressBoundNamedType named when !visitedNamedTypes.Add(named.Declaration) => true,
            ExpressBoundNamedType named => CanRepresentMissing(
                resolver.GetDefinedType(named.Declaration).UnderlyingType,
                resolver,
                visitedNamedTypes),
            ExpressBoundEnumerationType => true,
            ExpressBoundSelectType => true,
            _ => false,
        };
    }

    private static string ConstraintPrefix(
        ExpressEntityProjection entity,
        ExpressEntityAttributeProjection attribute)
    {
        return string.Join(
            ".",
            entity.Schema.Name.ToUpperInvariant(),
            attribute.StorageEntity.Name.ToUpperInvariant(),
            attribute.StorageAttributeName.ToUpperInvariant());
    }

    private static string NormalizedRequirement(ExpressEntityAttributeProjection attribute)
    {
        var presence = attribute.Attribute.IsOptional
            ? "The attribute may be absent; when present it must match"
            : "The attribute is mandatory and must match";
        return $"{presence} {ExpressTypeDocumentation.Format(attribute.Type)}.";
    }

    private static bool TryGetLowerBound(ExpressBoundAggregateType aggregate, out int lowerBound)
    {
        lowerBound = 0;
        var text = aggregate.ResolvedLowerBoundText ?? aggregate.LowerBoundText;
        return text is null
            || int.TryParse(
                text,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out lowerBound);
    }

    private static bool TryGetUpperBound(ExpressBoundAggregateType aggregate, out int? upperBound)
    {
        upperBound = null;
        var text = aggregate.ResolvedUpperBoundText ?? aggregate.UpperBoundText;
        if (text is null || text == "?")
        {
            return true;
        }

        if (!int.TryParse(
            text,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsedUpperBound))
        {
            return false;
        }

        upperBound = parsedUpperBound;
        return true;
    }

    private static string StableFilePath(string value)
    {
        var normalized = value.Replace('\\', '/');
        var separator = normalized.LastIndexOf('/');
        return separator < 0 ? normalized : normalized.Substring(separator + 1);
    }

    private static string Literal(string value)
    {
        return SymbolDisplay.FormatLiteral(value, quote: true);
    }

    private static string Invariant(int value)
    {
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Boolean(bool value)
    {
        return value ? "true" : "false";
    }

    private static DescriptionPara Paragraph(string text)
    {
        return new(new IDescriptionItem[] { new DescriptionText(text), });
    }

    private static void AddSummary(IRootDescription target, string text)
    {
        target.AddRootDescription(new DescriptionSummary(
            new IDescriptionItem[] { new DescriptionText(text), }));
    }

    private sealed class StructuralConstraint
    {
        /// <summary>
        /// Initializes one generated structural constraint description.
        /// </summary>
        /// <param name="code">The stable failure code.</param>
        /// <param name="requirement">The normalized caller-facing requirement.</param>
        internal StructuralConstraint(string code, string requirement)
        {
            Code = code;
            Requirement = requirement;
        }

        /// <summary>
        /// Gets the stable failure code.
        /// </summary>
        internal string Code { get; }

        /// <summary>
        /// Gets the normalized requirement.
        /// </summary>
        internal string Requirement { get; }
    }
}