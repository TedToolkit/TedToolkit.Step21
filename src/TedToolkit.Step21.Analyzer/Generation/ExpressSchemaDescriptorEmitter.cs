// -----------------------------------------------------------------------
// <copyright file="ExpressSchemaDescriptorEmitter.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using TedToolkit.RoslynHelper;
using TedToolkit.RoslynHelper.Syntaxes;

using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Structurally composes one generated schema descriptor.
/// </summary>
internal static class ExpressSchemaDescriptorEmitter
{
    private const int HYDRATION_BRANCHES_PER_METHOD = 32;

    private const int ENTITY_TYPE_IDENTITY_BRANCHES_PER_METHOD = 256;

    private const int SELECT_HYDRATION_METHODS_PER_SHARD = 16;

    private const int SELECT_PROJECTION_METHODS_PER_SHARD = 16;

    /// <summary>
    /// Emits one path-independent sealed schema descriptor through RoslynHelper.
    /// </summary>
    /// <param name="context">The source-production context.</param>
    /// <param name="schema">The valid bound schema.</param>
    /// <param name="entities">The generated entities owned by the schema.</param>
    /// <param name="complexEntities">The generated multi-leaf entities owned by the schema.</param>
    /// <param name="resolver">The closed-set generated value resolver.</param>
    /// <param name="rulePlan">The validated reachable rule closure.</param>
    /// <param name="physicalNames">The explicit Part 21 physical-name inventory.</param>
    internal static void Emit(
        in SourceProductionContext context,
        ExpressBoundSchema schema,
        IReadOnlyList<ExpressEntityProjection> entities,
        IReadOnlyList<ExpressComplexEntityProjection> complexEntities,
        ExpressGeneratedTypeResolver resolver,
        ExpressReachableRulePlan rulePlan,
        ExpressPhysicalNameMap physicalNames)
    {
        var descriptor = SourceComposer<ExpressIncrementalGenerator>.Class("SchemaDescriptor");
        descriptor.Accessibility = TedToolkit.RoslynHelper.Accessibility.PUBLIC;
        descriptor.Polymorphism = Polymorphism.SEALED;
        descriptor.AddBaseType(new DataType("global::TedToolkit.Step21.SchemaDescriptor"));
        AddSummary(descriptor, $"Provides reflection-free mapping infrastructure for the {schema.Name} EXPRESS schema.");
        ExpressStructuralValidationEmitter.AddDescriptorDocumentation(descriptor, schema, entities, rulePlan);
        var shards = new ExpressDescriptorShards(
            descriptor,
            ExpressDescriptorShards.IsRequired(entities.Count + complexEntities.Count));

        descriptor.AddMember(CreateConstructor());
        descriptor.AddMember(CreateEntityTypeIdentityMethod(
            entities,
            complexEntities,
            resolver,
            shards,
            physicalNames));
        descriptor.AddMember(CreateInstanceProperty());
        if (rulePlan.ReachableSingularInverseAttributes.Count > 0)
        {
            descriptor.AddMember(CreateInverseUnavailableExceptionType());
        }

        descriptor.AddMember(CreateNameProperty(schema));
        descriptor.AddMember(CreateAllocateMethod(entities, complexEntities, physicalNames));
        descriptor.AddMember(CreateHydrateMethod(
            schema.Identity,
            entities,
            complexEntities,
            resolver,
            shards,
            physicalNames));

        foreach (var method in ExpressStructuralValidationEmitter.CreateDispatchMethods(
                     schema,
                     entities,
                     complexEntities,
                     resolver,
                     rulePlan,
                     shards))
        {
            descriptor.AddMember(method);
        }

        foreach (var method in ExpressStructuralValidationEmitter.CreateEntityPopulationDispatchMethods(
                     schema,
                     entities,
                     complexEntities,
                     resolver,
                     rulePlan,
                     shards))
        {
            descriptor.AddMember(method);
        }

        var validationIndex = 0;
        foreach (var entity in entities.Where(candidate => !candidate.Entity.IsAbstract))
        {
            shards.Add(
                ExpressStructuralValidationEmitter.CreateEntityMethod(entity, resolver, rulePlan),
                ExpressStructuralValidationEmitter.ValidationShardName(validationIndex++));
        }

        foreach (var entity in complexEntities)
        {
            shards.Add(
                ExpressStructuralValidationEmitter.CreateComplexEntityMethod(entity, resolver, rulePlan),
                ExpressStructuralValidationEmitter.ValidationShardName(validationIndex++));
        }

        foreach (var method in ExpressReachableRuleEmitter.CreateDependencyMethods(
                     rulePlan,
                     resolver,
                     entities,
                     complexEntities,
                     shards))
        {
            descriptor.AddMember(method);
        }

        descriptor.AddMember(CreateCapabilityMethod());
        descriptor.AddMember(CreateProjectMethod(
            schema.Identity,
            entities,
            complexEntities,
            resolver,
            shards,
            physicalNames));
        descriptor.AddMember(CreateReferenceCompatibilityMethod(schema));
        var entityConstantNames = GetConstantNames(schema, entityConstants: true);
        if (entityConstantNames.Length > 0)
        {
            descriptor.AddMember(CreateConstantLookupMethod(entityConstantNames, entityConstants: true));
        }

        var valueConstantNames = GetConstantNames(schema, entityConstants: false);
        if (valueConstantNames.Length > 0)
        {
            descriptor.AddMember(CreateConstantLookupMethod(valueConstantNames, entityConstants: false));
        }

        var generatedNamespace = $"TedToolkit.Step21.Schemas.{ExpressEntityProjection.ToPascalCase(schema.Name)}";
        shards.Emit(context, generatedNamespace, schema.Name.ToUpperInvariant());
    }

    private static Constructor CreateConstructor()
    {
        var constructor = SourceComposer<ExpressIncrementalGenerator>.Constructor();
        constructor.Accessibility = TedToolkit.RoslynHelper.Accessibility.PRIVATE;
        constructor.AddStatement(new CustomExpression("ConfigureEntityTypeIdentity(MatchesEntityTypeIdentity)"));
        AddSummary(constructor, "Initializes the singleton schema descriptor.");
        return constructor;
    }

    private static TypeDeclaration CreateInverseUnavailableExceptionType()
    {
        var type = SourceComposer<ExpressIncrementalGenerator>.Class(
            "__ExpressInverseUnavailableException");
        type.Accessibility = TedToolkit.RoslynHelper.Accessibility.PRIVATE;
        type.Polymorphism = Polymorphism.SEALED;
        type.AddBaseType(new DataType("global::System.Exception"));
        return type;
    }

    private static Property CreateInstanceProperty()
    {
        var property = SourceComposer<ExpressIncrementalGenerator>.Property(
            new DataType("SchemaDescriptor"),
            "Instance");
        property.Accessibility = TedToolkit.RoslynHelper.Accessibility.PUBLIC;
        property.IsStatic = true;
        property.AddAccessor(SourceComposer<ExpressIncrementalGenerator>.Accessor(AccessorType.GET));
        property.AddDefault(new DataType("SchemaDescriptor").New);
        AddSummary(property, "Gets the single stateless schema descriptor instance.");
        return property;
    }

    private static Property CreateNameProperty(ExpressBoundSchema schema)
    {
        var property = SourceComposer<ExpressIncrementalGenerator>.Property(
            new DataType("global::TedToolkit.Step21.SchemaName"),
            "Name");
        property.Accessibility = TedToolkit.RoslynHelper.Accessibility.PUBLIC;
        property.Polymorphism = Polymorphism.OVERRIDE;
        property.AddAccessor(SourceComposer<ExpressIncrementalGenerator>.Accessor(AccessorType.GET));
        property.AddDefault(new DataType("global::TedToolkit.Step21.SchemaName").New
            .AddArgument(SourceComposer.Argument(schema.Name.ToLiteral())));
        AddSummary(property, "Gets the exact EXPRESS schema name.");
        return property;
    }

    private static Method CreateAllocateMethod(
        IReadOnlyList<ExpressEntityProjection> entities,
        IReadOnlyList<ExpressComplexEntityProjection> complexEntities,
        ExpressPhysicalNameMap physicalNames)
    {
        var method = CreateOverrideMethod(
            "AllocateEntityCore",
            new DataType("global::TedToolkit.Step21.Entity").Null);
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::System.Collections.Generic.IReadOnlyList<global::System.String>"),
            "entityNames"));
        foreach (var entity in entities.Where(entity => !entity.Entity.IsAbstract))
        {
            method.AddStatement(new IfStatement(new CustomExpression(CreateMappingCondition(entity, physicalNames)))
                .AddStatement(new CustomExpression(CreateEntity(entity)).Return));
        }

        foreach (var entity in complexEntities)
        {
            method.AddStatement(new IfStatement(new CustomExpression(
                    CreateComplexMappingCondition(entity, "entityNames", hasKey: false, physicalNames)))
                .AddStatement(new CustomExpression($"new {entity.Name}()").Return));
        }

        method.AddStatement(new CustomExpression("null").Return);
        AddSummary(method, "Allocates a supported generated entity from its ordered physical name group.");
        return method;
    }

    private static string CreateMappingCondition(
        ExpressEntityProjection entity,
        ExpressPhysicalNameMap physicalNames)
    {
        var entityName = entity.Entity.Name.ToUpperInvariant();
        var condition = CreateNameCondition(
            "entityNames[0]",
            entityName,
            physicalNames.EntityName(entity.Entity.Symbol));
        return $"(entityNames.Count == 1 && {condition})";
    }

    private static string[] GetConstantNames(ExpressBoundSchema schema, bool entityConstants)
    {
        return schema.Declarations
            .OfType<ExpressBoundOpaqueDeclaration>()
            .Where(declaration => declaration.Kind == ExpressDeclarationKind.Constant
                && IsDirectEntityConstant(declaration) == entityConstants)
            .Select(declaration => declaration.Name.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private static Method CreateConstantLookupMethod(
        string[] names,
        bool entityConstants)
    {
        var method = CreateOverrideMethod(
            entityConstants ? "ContainsConstantEntityCore" : "ContainsConstantValueCore",
            new DataType("global::System.Boolean"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::System.String"), "name"));
        var expression = $"name is {string.Join(" or ", names.Select(name =>
            Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(name, quote: true)))}";
        method.AddStatement(new CustomExpression(expression).Return);
        AddSummary(method, entityConstants
            ? "Recognizes direct entity-valued EXPRESS constant occurrence names."
            : "Recognizes non-entity EXPRESS constant occurrence names.");
        return method;
    }

    private static bool IsDirectEntityConstant(ExpressBoundOpaqueDeclaration declaration)
    {
        return declaration.DeclaredType is ExpressBoundNamedType
        {
            Declaration.Kind: ExpressDeclarationKind.Entity,
        };
    }

    private static string CreateComplexMappingCondition(
        ExpressComplexEntityProjection entity,
        string components,
        bool hasKey,
        ExpressPhysicalNameMap physicalNames)
    {
        return $"({components}.Count == {entity.Components.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
            + string.Concat(entity.Components.Select((component, index) =>
            {
                var access = $"{components}[{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}]";
                if (hasKey)
                {
                    access += ".Key";
                }

                return " && " + CreateNameCondition(
                    access,
                    component.Entity.Name.ToUpperInvariant(),
                    physicalNames.EntityName(component.Entity.Symbol));
            }))
            + ")";
    }

    private static string CreateEntity(ExpressEntityProjection entity)
    {
        var arguments = entity.EffectiveAttributes
            .Where(attribute => !attribute.Attribute.IsOptional)
            .Select(_ => "default!");
        return $"new {entity.Name}({string.Join(", ", arguments)})";
    }

    private static Method CreateHydrateMethod(
        ExpressBoundSchemaIdentity currentSchema,
        IReadOnlyList<ExpressEntityProjection> entities,
        IReadOnlyList<ExpressComplexEntityProjection> complexEntities,
        ExpressGeneratedTypeResolver resolver,
        ExpressDescriptorShards shards,
        ExpressPhysicalNameMap physicalNames)
    {
        const string diagnosticsTypeName =
            "global::System.Collections.Generic.IReadOnlyList<global::TedToolkit.Step21.Step21Diagnostic>";
        var diagnosticsType = new DataType(diagnosticsTypeName);
        var selectHydrationHelpers = new SelectHydrationHelpers(
            currentSchema,
            resolver,
            shards,
            physicalNames);
        var branches = new List<IfStatement>();
        foreach (var entity in entities.Where(entity => CanMapEntity(entity, resolver)))
        {
            branches.Add(CreateHydrateEntityBranch(entity, resolver, selectHydrationHelpers, physicalNames));
        }

        foreach (var entity in complexEntities)
        {
            branches.Add(CreateHydrateComplexEntityBranch(entity, resolver, selectHydrationHelpers, physicalNames));
        }

        var dispatch = CreateOverrideMethod("HydrateEntityCore", diagnosticsType);
        AddHydrationParameters(dispatch);
        for (var offset = 0; offset < branches.Count; offset += HYDRATION_BRANCHES_PER_METHOD)
        {
            var groupIndex = offset / HYDRATION_BRANCHES_PER_METHOD;
            var methodName = $"__ExpressTryHydrateGroup{groupIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            var shardName = $"__ExpressHydrationShard{groupIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            var resultName = $"hydrationResult{groupIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            dispatch.AddStatement(new CustomExpression(
                $"var {resultName} = {shards.Qualify(methodName, shardName)}(structure, value, components)"));
            dispatch.AddStatement(new IfStatement(new CustomExpression($"{resultName} is not null"))
                .AddStatement(new CustomExpression(resultName).Return));

            var group = SourceComposer<ExpressIncrementalGenerator>.Method(
                methodName,
                SourceComposer.ReturnType(new DataType(diagnosticsTypeName).Null));
            group.Accessibility = TedToolkit.RoslynHelper.Accessibility.PRIVATE;
            group.IsStatic = true;
            AddHydrationParameters(group);
            foreach (var branch in branches.Skip(offset).Take(HYDRATION_BRANCHES_PER_METHOD))
            {
                group.AddStatement(branch);
            }

            group.AddStatement(new CustomExpression("null").Return);
            shards.Add(group, shardName);
        }

        dispatch.AddStatement(new CustomExpression(
            "[new global::TedToolkit.Step21.Step21Diagnostic(\"P21-BIND-ENTITY\", "
            + "global::TedToolkit.Step21.Step21DiagnosticSeverity.Error, "
            + "\"The entity is not supported by this schema descriptor.\")]").Return);
        AddSummary(dispatch, "Hydrates one supported entity from strong physical components and parameters.");
        return dispatch;
    }

    private static void AddHydrationParameters(Method method)
    {
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::TedToolkit.Step21.ExchangeStructure"),
            "structure"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::TedToolkit.Step21.Entity"), "value"));
        method.AddParameter(SourceComposer.Parameter(
            new DataType(
                "global::System.Collections.Generic.IReadOnlyList<global::System.Collections.Generic.KeyValuePair<global::System.String, global::System.Collections.Generic.IReadOnlyList<global::TedToolkit.Step21.ParameterValue>>>"),
            "components"));
    }

    private static Method CreateCapabilityMethod()
    {
        var method = CreateOverrideMethod(
            "GetCapabilityDiagnosticsCore",
            new DataType(
                "global::System.Collections.Generic.IReadOnlyList<global::TedToolkit.Step21.Step21Diagnostic>"));
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::TedToolkit.Step21.ExchangeStructure"),
            "structure"));
        method.AddStatement(new CustomExpression("global::System.Array.Empty<global::TedToolkit.Step21.Step21Diagnostic>()").Return);
        AddSummary(method, "Returns capability diagnostics for the current delivery stage.");
        return method;
    }

    private static Method CreateProjectMethod(
        ExpressBoundSchemaIdentity currentSchema,
        IReadOnlyList<ExpressEntityProjection> entities,
        IReadOnlyList<ExpressComplexEntityProjection> complexEntities,
        ExpressGeneratedTypeResolver resolver,
        ExpressDescriptorShards shards,
        ExpressPhysicalNameMap physicalNames)
    {
        var componentType = new DataType(
            "global::System.Collections.Generic.IReadOnlyList<global::System.Collections.Generic.KeyValuePair<global::System.String, global::System.Collections.Generic.IReadOnlyList<global::TedToolkit.Step21.ParameterValue>>>");
        var method = CreateOverrideMethod("ProjectEntityCore", componentType);
        method.AddParameter(SourceComposer.Parameter(new DataType("global::TedToolkit.Step21.Entity"), "value"));
        var selectProjectionHelpers = new SelectProjectionHelpers(
            currentSchema,
            resolver,
            shards,
            physicalNames);
        foreach (var entity in entities.Where(entity => CanMapEntity(entity, resolver)))
        {
            method.AddStatement(CreateProjectEntityBranch(
                entity,
                resolver,
                selectProjectionHelpers,
                physicalNames));
        }

        foreach (var entity in complexEntities)
        {
            method.AddStatement(CreateProjectComplexEntityBranch(
                entity,
                resolver,
                selectProjectionHelpers,
                physicalNames));
        }

        method.AddStatement(new CustomExpression("[]").Return);
        AddSummary(method, "Projects one supported entity to strong physical components and parameters.");
        return method;
    }

    private static Method CreateReferenceCompatibilityMethod(ExpressBoundSchema schema)
    {
        var method = CreateOverrideMethod(
            "IsEntityReferenceCompatibleCore",
            new DataType("global::System.Boolean"));
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::TedToolkit.Step21.Entity"),
            "value"));
        var entityTypes = schema.Declarations
            .Select(declaration => declaration.Symbol)
            .Concat(schema.Imports.Select(import => import.Declaration))
            .Where(symbol => symbol.Kind == ExpressDeclarationKind.Entity)
            .Distinct()
            .Select(symbol => GetGeneratedTypeName(schema.Identity, symbol))
            .ToArray();
        var expression = entityTypes.Length == 0
            ? "false"
            : string.Join(" || ", entityTypes.Select(type => $"value is {type}"));
        method.AddStatement(new CustomExpression(expression).Return);
        AddSummary(method, "Determines EXPRESS interface compatibility for schema-population inclusion.");
        return method;
    }

    private static Method CreateEntityTypeIdentityMethod(
        IReadOnlyList<ExpressEntityProjection> entities,
        IReadOnlyList<ExpressComplexEntityProjection> complexEntities,
        ExpressGeneratedTypeResolver resolver,
        ExpressDescriptorShards shards,
        ExpressPhysicalNameMap physicalNames)
    {
        var method = SourceComposer<ExpressIncrementalGenerator>.Method(
            "MatchesEntityTypeIdentity",
            SourceComposer.ReturnType(new DataType("global::System.Boolean")));
        method.Accessibility = TedToolkit.RoslynHelper.Accessibility.PRIVATE;
        method.IsStatic = true;
        method.AddParameter(SourceComposer.Parameter(new DataType("global::TedToolkit.Step21.Entity"), "value"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::System.String?"), "entityName"));
        var matches = entities.Where(entity => CanMapEntity(entity, resolver))
            .Select(entity => CreateEntityTypeIdentity(entity.Name, entity.Entity.Symbol, physicalNames))
            .Concat(complexEntities.SelectMany(entity => entity.Components.Select(component =>
                CreateEntityTypeIdentity(entity.Name, component.Entity.Symbol, physicalNames))))
            .ToArray();
        for (var offset = 0; offset < matches.Length; offset += ENTITY_TYPE_IDENTITY_BRANCHES_PER_METHOD)
        {
            var groupIndex = offset / ENTITY_TYPE_IDENTITY_BRANCHES_PER_METHOD;
            var suffix = groupIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var methodName = $"__ExpressMatchesEntityTypeIdentityGroup{suffix}";
            var shardName = $"__ExpressEntityTypeIdentityShard{suffix}";
            method.AddStatement(new IfStatement(new CustomExpression(
                    $"{shards.Qualify(methodName, shardName)}(value, entityName)"))
                .AddStatement(new CustomExpression("true").Return));

            var group = SourceComposer<ExpressIncrementalGenerator>.Method(
                methodName,
                SourceComposer.ReturnType(new DataType("global::System.Boolean")));
            group.Accessibility = TedToolkit.RoslynHelper.Accessibility.PRIVATE;
            group.IsStatic = true;
            group.AddParameter(SourceComposer.Parameter(
                new DataType("global::TedToolkit.Step21.Entity"),
                "value"));
            group.AddParameter(SourceComposer.Parameter(new DataType("global::System.String?"), "entityName"));
            foreach (var match in matches.Skip(offset).Take(ENTITY_TYPE_IDENTITY_BRANCHES_PER_METHOD))
            {
                group.AddStatement(new IfStatement(new CustomExpression(match))
                    .AddStatement(new CustomExpression("true").Return));
            }

            group.AddStatement(new CustomExpression("false").Return);
            shards.Add(group, shardName);
        }

        method.AddStatement(new CustomExpression("false").Return);
        return method;
    }

    private static Method CreateOverrideMethod(string name, DataType returnType)
    {
        var method = SourceComposer<ExpressIncrementalGenerator>.Method(
            name,
            SourceComposer.ReturnType(returnType));
        method.Accessibility = TedToolkit.RoslynHelper.Accessibility.PROTECTED;
        method.Polymorphism = Polymorphism.OVERRIDE;
        return method;
    }

    private static string CreateEntityTypeIdentity(
        string generatedName,
        ExpressBoundSymbol symbol,
        ExpressPhysicalNameMap physicalNames)
    {
        var longName = symbol.Name.ToUpperInvariant();
        var physicalName = physicalNames.EntityName(symbol);
        var equality = $"global::System.String.Equals(entityName, \"{longName}\", global::System.StringComparison.OrdinalIgnoreCase)";
        if (!StringComparer.Ordinal.Equals(longName, physicalName))
        {
            equality += " || global::System.String.Equals(entityName, "
                + $"\"{physicalName}\", global::System.StringComparison.OrdinalIgnoreCase)";
        }

        return $"value is {generatedName} && (entityName is null || {equality})";
    }

    private static string CreateNameCondition(string expression, string longName, string physicalName)
    {
        return StringComparer.Ordinal.Equals(longName, physicalName)
            ? $"{expression} == \"{longName}\""
            : $"{expression} is \"{longName}\" or \"{physicalName}\"";
    }

    private static IfStatement CreateHydrateEntityBranch(
        ExpressEntityProjection entity,
        ExpressGeneratedTypeResolver resolver,
        SelectHydrationHelpers selectHydrationHelpers,
        ExpressPhysicalNameMap physicalNames)
    {
        var typedName = $"typed{entity.Name}";
        var longName = entity.Entity.Name.ToUpperInvariant();
        var physicalName = physicalNames.EntityName(entity.Entity.Symbol);
        var componentMismatch = StringComparer.Ordinal.Equals(longName, physicalName)
            ? $"components[0].Key != \"{longName}\""
            : $"!({CreateNameCondition("components[0].Key", longName, physicalName)})";
        var branch = new IfStatement(new CustomExpression($"value is {entity.Name} {typedName}"))
            .AddStatement(new IfStatement(new CustomExpression(
                $"components.Count != 1 || {componentMismatch}"))
            .AddStatement(new CustomExpression(
                "[new global::TedToolkit.Step21.Step21Diagnostic(\"P21-BIND-COMPONENT\", "
                + "global::TedToolkit.Step21.Step21DiagnosticSeverity.Error, "
                + $"\"The component sequence is not a supported mapping of {entity.Entity.Name.ToUpperInvariant()}.\")]").Return))
            .AddStatement(new Statement(new CustomExpression(
                "var diagnostics = new global::System.Collections.Generic.List<global::TedToolkit.Step21.Step21Diagnostic>()")))
            .AddStatement(new Statement(new CustomExpression("var parameters = components[0].Value")))
            .AddStatement(CreateParameterCountCheck(entity.Entity.Name, entity.EffectiveAttributes.Count));
        for (var index = 0; index < entity.EffectiveAttributes.Count; index++)
        {
            branch.AddStatement(CreateHydrateAttribute(
                entity,
                entity.EffectiveAttributes[index],
                index,
                typedName,
                resolver,
                entity.Entity.Name,
                selectHydrationHelpers,
                physicalNames));
        }

        branch.AddStatement(new CustomExpression("diagnostics").Return);
        return branch;
    }

    private static IfStatement CreateHydrateComplexEntityBranch(
        ExpressComplexEntityProjection entity,
        ExpressGeneratedTypeResolver resolver,
        SelectHydrationHelpers selectHydrationHelpers,
        ExpressPhysicalNameMap physicalNames)
    {
        var typedName = $"typed{entity.Name}";
        var branch = new IfStatement(new CustomExpression($"value is {entity.Name} {typedName}"))
            .AddStatement(new IfStatement(new CustomExpression(
                $"!{CreateComplexMappingCondition(entity, "components", hasKey: true, physicalNames)}"))
            .AddStatement(new CustomExpression(
                "[new global::TedToolkit.Step21.Step21Diagnostic(\"P21-BIND-COMPONENT\", "
                + "global::TedToolkit.Step21.Step21DiagnosticSeverity.Error, "
                + "\"The complex component sequence is not supported.\")]").Return))
            .AddStatement(new Statement(new CustomExpression(
                "var diagnostics = new global::System.Collections.Generic.List<global::TedToolkit.Step21.Step21Diagnostic>()")));
        var context = entity.Leaves[0];
        for (var componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++)
        {
            var component = entity.Components[componentIndex];
            var attributes = ExpressComplexEntityProjection.GetComponentAttributes(component);
            var componentBranch = new IfStatement(new CustomExpression("true"))
                .AddStatement(new Statement(new CustomExpression(
                    $"var parameters = components[{componentIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)}].Value")))
                .AddStatement(CreateParameterCountCheck(component.Entity.Name, attributes.Count));
            for (var attributeIndex = 0; attributeIndex < attributes.Count; attributeIndex++)
            {
                var physicalAttribute = attributes[attributeIndex];
                var storageAttribute = entity.Properties.Where(attribute =>
                        ReferenceEquals(attribute.StorageEntity.Symbol, physicalAttribute.StorageEntity.Symbol)
                        && StringComparer.OrdinalIgnoreCase.Equals(
                            attribute.StorageAttributeName,
                            physicalAttribute.StorageAttributeName))
                    .OrderBy(attribute => attribute.RedirectTargetName is null ? 0 : 1)
                    .FirstOrDefault()
                    ?? physicalAttribute;
                componentBranch.AddStatement(CreateHydrateAttribute(
                    context,
                    storageAttribute,
                    attributeIndex,
                    typedName,
                    resolver,
                    component.Entity.Name,
                    selectHydrationHelpers,
                    physicalNames,
                    entity.IsDerivedRedeclared(physicalAttribute)));
            }

            branch.AddStatement(componentBranch);
        }

        branch.AddStatement(new CustomExpression("diagnostics").Return);
        return branch;
    }

    private static IfStatement CreateParameterCountCheck(string entityName, int count)
    {
        return new IfStatement(new CustomExpression(
                $"parameters.Count != {count.ToString(System.Globalization.CultureInfo.InvariantCulture)}"))
            .AddStatement(new CustomExpression(
                "[new global::TedToolkit.Step21.Step21Diagnostic(\"P21-BIND-PARAMETER-COUNT\", "
                + "global::TedToolkit.Step21.Step21DiagnosticSeverity.Error, "
                + $"\"Expected {count.ToString(System.Globalization.CultureInfo.InvariantCulture)} "
                + $"parameters for {entityName.ToUpperInvariant()}.\")]").Return);
    }

    private static IfStatement CreateHydrateAttribute(
        ExpressEntityProjection entity,
        ExpressEntityAttributeProjection attribute,
        int index,
        string typedName,
        ExpressGeneratedTypeResolver resolver,
        string physicalEntityName,
        SelectHydrationHelpers selectHydrationHelpers,
        ExpressPhysicalNameMap physicalNames,
        bool isDerivedRedeclared = false)
    {
        var parameterName = $"parameter{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        var expectedType = ExpressTypeDocumentation.Format(attribute.Type);
        var invalid = new CustomExpression(
            "diagnostics.Add(new global::TedToolkit.Step21.Step21Diagnostic(\"P21-BIND-PARAMETER\", "
            + "global::TedToolkit.Step21.Step21DiagnosticSeverity.Error, "
            + $"\"{physicalEntityName.ToUpperInvariant()} parameter "
            + $"{index.ToString(System.Globalization.CultureInfo.InvariantCulture)} ({attribute.Attribute.Name}) "
            + $"is not {expectedType}.\"))");
        var valueBranch = CreateValueHydrationBranch(
            entity,
            attribute,
            index,
            parameterName,
            typedName,
            invalid,
            resolver,
            selectHydrationHelpers,
            physicalNames,
            isDerivedRedeclared);
        IfStatement hydration;
        if (attribute.Attribute.IsOptional)
        {
            hydration = new IfStatement(new CustomExpression(
                    $"parameters[{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}].Kind "
                    + "== global::TedToolkit.Step21.ParameterValueKind.Omitted"))
                .AddStatement(new CustomExpression($"{typedName}.{attribute.StorageMemberName} = null"))
                .Else()
                .AddStatement(valueBranch);
        }
        else
        {
            hydration = valueBranch;
        }

        var parameter = $"parameters[{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}]";
        var incompatibleReference = CreateIncompatibleReferenceCondition(
            entity.Schema.Identity,
            attribute.Type,
            parameter,
            $"reference{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            resolver);
        if (incompatibleReference is null)
        {
            return hydration;
        }

        var referenceInvalid = new CustomExpression(
            "diagnostics.Add(new global::TedToolkit.Step21.Step21Diagnostic("
            + $"\"P21-BIND-REFERENCE-TYPE-{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}\", "
            + "global::TedToolkit.Step21.Step21DiagnosticSeverity.Error, "
            + $"\"{physicalEntityName.ToUpperInvariant()} parameter "
            + $"{index.ToString(System.Globalization.CultureInfo.InvariantCulture)} ({attribute.Attribute.Name}) "
            + $"contains a reference target that is not assignable to {expectedType}.\"))");
        return new IfStatement(new CustomExpression(incompatibleReference))
            .AddStatement(referenceInvalid)
            .Else()
            .AddStatement(hydration);
    }

    private static IfStatement CreateProjectEntityBranch(
        ExpressEntityProjection entity,
        ExpressGeneratedTypeResolver resolver,
        SelectProjectionHelpers selectProjectionHelpers,
        ExpressPhysicalNameMap physicalNames)
    {
        var typedName = $"typed{entity.Name}";
        string[] components =
        [
            CreateProjectedComponent(
                entity,
                physicalNames.EntityName(entity.Entity.Symbol),
                entity.EffectiveAttributes,
                typedName,
                resolver,
                selectProjectionHelpers,
                physicalNames),
        ];
        var expression = $"[{string.Join(", ", components)}]";
        return new IfStatement(new CustomExpression($"value is {entity.Name} {typedName}"))
            .AddStatement(new CustomExpression(expression).Return);
    }

    private static IfStatement CreateProjectComplexEntityBranch(
        ExpressComplexEntityProjection entity,
        ExpressGeneratedTypeResolver resolver,
        SelectProjectionHelpers selectProjectionHelpers,
        ExpressPhysicalNameMap physicalNames)
    {
        var typedName = $"typed{entity.Name}";
        var context = entity.Leaves[0];
        var components = new List<string>();
        var indexOffset = 0;
        foreach (var component in entity.Components)
        {
            var attributes = ExpressComplexEntityProjection.GetComponentAttributes(component);
            components.Add(CreateProjectedComponent(
                context,
                physicalNames.EntityName(component.Entity.Symbol),
                attributes,
                typedName,
                resolver,
                selectProjectionHelpers,
                physicalNames,
                indexOffset,
                entity));
            indexOffset += attributes.Count;
        }

        return new IfStatement(new CustomExpression($"value is {entity.Name} {typedName}"))
            .AddStatement(new CustomExpression($"[{string.Join(", ", components)}]").Return);
    }

    private static string CreateProjectedComponent(
        ExpressEntityProjection entity,
        string componentName,
        IReadOnlyList<ExpressEntityAttributeProjection> attributes,
        string typedName,
        ExpressGeneratedTypeResolver resolver,
        SelectProjectionHelpers selectProjectionHelpers,
        ExpressPhysicalNameMap physicalNames,
        int indexOffset = 0,
        ExpressComplexEntityProjection? complexEntity = null)
    {
        var parameters = attributes.Select((attribute, index) => CreateProjectedAttribute(
            entity,
            attribute,
            typedName,
            indexOffset + index,
            resolver,
            selectProjectionHelpers,
            physicalNames,
            complexEntity?.IsDerivedRedeclared(attribute) == true,
            useInterfaceContract: complexEntity is not null));
        return "new global::System.Collections.Generic.KeyValuePair<global::System.String, "
            + "global::System.Collections.Generic.IReadOnlyList<global::TedToolkit.Step21.ParameterValue>>("
            + $"\"{componentName.ToUpperInvariant()}\", [{string.Join(", ", parameters)}])";
    }

    private static bool CanMapEntity(
        ExpressEntityProjection entity,
        ExpressGeneratedTypeResolver resolver)
    {
        return entity.EffectiveAttributes.All(attribute =>
            ExpressDescriptorTypeSupport.CanMap(attribute.Type, resolver));
    }

    private static IfStatement CreateScalarHydrationBranch(
        ExpressScalarKind kind,
        int index,
        string parameterName,
        Func<string, string> createAssignment,
        CustomExpression invalid)
    {
        var parameter = $"parameters[{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}]";
        if (kind == ExpressScalarKind.Number)
        {
            return new IfStatement(new CustomExpression($"{parameter}.TryGetInteger(out var {parameterName}Integer)"))
                .AddStatement(new CustomExpression(
                    createAssignment(
                        $"global::TedToolkit.Step21.NumberValue.FromInteger({parameterName}Integer)")))
                .ElseIf(new CustomExpression($"{parameter}.TryGetReal(out var {parameterName}Real)"))
                .AddStatement(new CustomExpression(
                    createAssignment(
                        $"global::TedToolkit.Step21.NumberValue.FromReal({parameterName}Real)")))
                .Else()
                .AddStatement(invalid);
        }

        if (kind == ExpressScalarKind.Boolean)
        {
            return new IfStatement(new CustomExpression($"{parameter}.TryGetBoolean(out var {parameterName})"))
                .AddStatement(new CustomExpression(createAssignment(parameterName)))
                .ElseIf(new CustomExpression(
                    $"{parameter}.TryGetEnumeration(out var {parameterName}Symbol) "
                    + $"&& ({parameterName}Symbol == \"T\" || {parameterName}Symbol == \"F\")"))
                .AddStatement(new CustomExpression(createAssignment($"{parameterName}Symbol == \"T\"")))
                .Else()
                .AddStatement(invalid);
        }

        if (kind == ExpressScalarKind.Logical)
        {
            return new IfStatement(new CustomExpression($"{parameter}.TryGetLogical(out var {parameterName})"))
                .AddStatement(new CustomExpression(createAssignment(parameterName)))
                .ElseIf(new CustomExpression(
                    $"{parameter}.TryGetEnumeration(out var {parameterName}Symbol) "
                    + $"&& ({parameterName}Symbol == \"T\" || {parameterName}Symbol == \"F\" "
                    + $"|| {parameterName}Symbol == \"U\")"))
                .AddStatement(new CustomExpression(createAssignment(
                    $"{parameterName}Symbol == \"T\" "
                    + "? global::TedToolkit.Step21.LogicalValue.True "
                    + $": {parameterName}Symbol == \"F\" "
                    + "? global::TedToolkit.Step21.LogicalValue.False "
                    + ": global::TedToolkit.Step21.LogicalValue.Unknown")))
                .Else()
                .AddStatement(invalid);
        }

        if (kind == ExpressScalarKind.Real)
        {
            return new IfStatement(new CustomExpression(
                    $"TryHydrateReal(structure, {parameter}, out var {parameterName})"))
                .AddStatement(new CustomExpression(createAssignment(parameterName)))
                .Else()
                .AddStatement(invalid);
        }

        var tryGetMethod = kind switch
        {
            ExpressScalarKind.Binary => "TryGetBinary",
            ExpressScalarKind.Integer => "TryGetInteger",
            ExpressScalarKind.String => "TryGetString",
            _ => throw new InvalidOperationException($"Unsupported scalar hydration kind '{kind.ToString()}'."),
        };
        return new IfStatement(new CustomExpression($"{parameter}.{tryGetMethod}(out var {parameterName})"))
            .AddStatement(new CustomExpression(createAssignment(parameterName)))
            .Else()
            .AddStatement(invalid);
    }

    private static string CreateProjectedAttribute(
        ExpressEntityProjection entity,
        ExpressEntityAttributeProjection attribute,
        string typedName,
        int index,
        ExpressGeneratedTypeResolver resolver,
        SelectProjectionHelpers selectProjectionHelpers,
        ExpressPhysicalNameMap physicalNames,
        bool isDerivedRedeclared = false,
        bool useInterfaceContract = false)
    {
        if (isDerivedRedeclared || entity.IsDerivedRedeclared(attribute))
        {
            return "global::TedToolkit.Step21.ParameterValue.Derived";
        }

        string value;
        if (useInterfaceContract)
        {
            value = $"(({GetGeneratedTypeName(entity.Schema.Identity, attribute.DeclaringEntity.Symbol)})"
                + $"{typedName}).{attribute.Name}";
        }
        else if (StringComparer.Ordinal.Equals(attribute.Name, attribute.StorageMemberName))
        {
            value = $"{typedName}.{attribute.Name}";
        }
        else
        {
            value = $"(({GetGeneratedTypeName(entity.Schema.Identity, attribute.StorageEntity.Symbol)})"
                + $"{typedName}).{attribute.Name}";
        }

        var physicalValue = attribute.Attribute.IsOptional
            && !resolver.Resolve(entity.Schema.Identity, attribute.Type).IsReferenceType
                ? $"{value}.Value"
                : value;
        var mapped = CreateProjectedValue(
            entity.Schema.Identity,
            attribute.Type,
            physicalValue,
            index,
            resolver,
            selectProjectionHelpers,
            physicalNames);
        return attribute.Attribute.IsOptional
            ? $"{value} is null ? global::TedToolkit.Step21.ParameterValue.Omitted : {mapped}"
            : mapped;
    }

    private static IfStatement CreateValueHydrationBranch(
        ExpressEntityProjection entity,
        ExpressEntityAttributeProjection attribute,
        int index,
        string parameterName,
        string typedName,
        CustomExpression invalid,
        ExpressGeneratedTypeResolver resolver,
        SelectHydrationHelpers selectHydrationHelpers,
        ExpressPhysicalNameMap physicalNames,
        bool isDerivedRedeclared)
    {
        if (isDerivedRedeclared || entity.IsDerivedRedeclared(attribute))
        {
            return new IfStatement(new CustomExpression(
                    $"parameters[{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}].Kind "
                    + "== global::TedToolkit.Step21.ParameterValueKind.Derived"))
                .Else()
                .AddStatement(invalid);
        }

        var target = $"{typedName}.{attribute.StorageMemberName}";
        if (attribute.Type is ExpressBoundNamedType namedEntity
            && namedEntity.Declaration.Kind == ExpressDeclarationKind.Entity)
        {
            var entityParameter = $"parameters[{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}]";
            var entityType = GetGeneratedTypeName(entity.Schema.Identity, namedEntity.Declaration);
            return new IfStatement(new CustomExpression(
                    $"{entityParameter}.TryGetEntity(out var {parameterName}Entity) "
                    + $"&& {parameterName}Entity is {entityType} {parameterName}"))
                .AddStatement(new CustomExpression($"{target} = {parameterName}"))
                .Else()
                .AddStatement(invalid);
        }

        if (attribute.Type is ExpressBoundNamedType namedSelect
            && resolver.GetDefinedType(namedSelect.Declaration).UnderlyingType is ExpressBoundSelectType select)
        {
            return CreateSelectHydrationBranch(
                namedSelect,
                select,
                index,
                parameterName,
                target,
                invalid,
                selectHydrationHelpers);
        }

        var terminalType = ExpressDescriptorTypeSupport.GetTerminalType(attribute.Type, resolver);
        if (terminalType is ExpressBoundAggregateType aggregate)
        {
            return CreateAggregateHydrationBranch(
                entity.Schema.Identity,
                attribute.Type,
                aggregate,
                index,
                parameterName,
                target,
                invalid,
                resolver,
                selectHydrationHelpers,
                physicalNames);
        }

        if (terminalType is ExpressBoundScalarType scalar)
        {
            return CreateScalarHydrationBranch(
                scalar.Kind,
                index,
                parameterName,
                rawValue => $"{target} = {CreateReadValueExpression(
                    entity.Schema.Identity,
                    attribute.Type,
                    rawValue,
                    resolver,
                    physicalNames)}",
                invalid);
        }

        var enumeration = (ExpressBoundEnumerationType)terminalType;
        var parameter = $"parameters[{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}]";
        var values = resolver.GetEnumerationValues(enumeration);
        var condition = $"{parameter}.TryGetEnumeration(out var {parameterName})";
        if (!enumeration.IsExtensible)
        {
            condition += " && " + CreateEnumerationCondition(
                parameterName,
                attribute.Type,
                values,
                resolver,
                physicalNames);
        }

        return new IfStatement(new CustomExpression(condition))
            .AddStatement(new CustomExpression(
                $"{target} = {CreateReadValueExpression(entity.Schema.Identity, attribute.Type, parameterName, resolver, physicalNames)}"))
            .Else()
            .AddStatement(invalid);
    }

    private static string CreateProjectedValue(
        ExpressBoundSchemaIdentity currentSchema,
        ExpressBoundType type,
        string value,
        int index,
        ExpressGeneratedTypeResolver resolver,
        SelectProjectionHelpers selectProjectionHelpers,
        ExpressPhysicalNameMap physicalNames)
    {
        if (type is ExpressBoundAggregateType aggregate)
        {
            return CreateProjectedAggregate(
                currentSchema,
                aggregate,
                value,
                index,
                resolver,
                selectProjectionHelpers,
                physicalNames);
        }

        if (type is ExpressBoundNamedType named)
        {
            if (named.Declaration.Kind == ExpressDeclarationKind.Entity)
            {
                return "global::TedToolkit.Step21.ParameterValue.FromEntity("
                    + $"(global::TedToolkit.Step21.Entity){value})";
            }

            var declaration = resolver.GetDefinedType(named.Declaration);
            return declaration.UnderlyingType switch
            {
                ExpressBoundEnumerationType =>
                    "global::TedToolkit.Step21.ParameterValue.FromEnumeration("
                    + CreateEnumerationProjection(named.Declaration, $"{value}.Value", resolver, physicalNames)
                    + ")",
                ExpressBoundSelectType select => selectProjectionHelpers.CreateCall(
                    named,
                    select,
                    value),
                _ => CreateProjectedValue(
                    currentSchema,
                    declaration.UnderlyingType,
                    $"{value}.Value",
                    index,
                    resolver,
                    selectProjectionHelpers,
                    physicalNames),
            };
        }

        var scalar = (ExpressBoundScalarType)type;
        return scalar.Kind switch
        {
            ExpressScalarKind.Binary => $"global::TedToolkit.Step21.ParameterValue.FromBinary({value})",
            ExpressScalarKind.Boolean => $"global::TedToolkit.Step21.ParameterValue.FromBoolean({value})",
            ExpressScalarKind.Integer => $"global::TedToolkit.Step21.ParameterValue.FromInteger({value})",
            ExpressScalarKind.Logical => $"global::TedToolkit.Step21.ParameterValue.FromLogical({value})",
            ExpressScalarKind.Number => CreateProjectedNumber(value, index),
            ExpressScalarKind.Real => $"global::TedToolkit.Step21.ParameterValue.FromReal({value})",
            ExpressScalarKind.String => $"global::TedToolkit.Step21.ParameterValue.FromString({value})",
            _ => throw new InvalidOperationException($"Unsupported scalar projection kind '{scalar.Kind.ToString()}'."),
        };
    }

    private static string CreateReadValueExpression(
        ExpressBoundSchemaIdentity currentSchema,
        ExpressBoundType type,
        string rawValue,
        ExpressGeneratedTypeResolver resolver,
        ExpressPhysicalNameMap physicalNames)
    {
        if (type is ExpressBoundScalarType or ExpressBoundAggregateType)
        {
            return rawValue;
        }

        var named = (ExpressBoundNamedType)type;
        if (named.Declaration.Kind == ExpressDeclarationKind.Entity)
        {
            return rawValue;
        }

        var declaration = resolver.GetDefinedType(named.Declaration);
        var generatedType = GetGeneratedTypeName(currentSchema, named.Declaration);
        if (declaration.UnderlyingType is ExpressBoundEnumerationType enumeration)
        {
            if (enumeration.IsExtensible)
            {
                return $"new {generatedType}({CreateEnumerationLongProjection(named.Declaration, rawValue, resolver, physicalNames)})";
            }

            return $"{rawValue} switch {{ {string.Join(", ", resolver.GetEnumerationValues(enumeration).SelectMany(value =>
                CreateEnumerationReadCases(named.Declaration, value, generatedType, physicalNames)))}, "
                + "_ => default }";
        }

        var underlyingValue = declaration.UnderlyingType is ExpressBoundAggregateType
            ? rawValue
            : CreateReadValueExpression(
            currentSchema,
            declaration.UnderlyingType,
            rawValue,
            resolver,
            physicalNames);
        return $"new {generatedType}({underlyingValue})";
    }

    private static IfStatement CreateAggregateHydrationBranch(
        ExpressBoundSchemaIdentity currentSchema,
        ExpressBoundType declaredType,
        ExpressBoundAggregateType aggregate,
        int index,
        string parameterName,
        string target,
        CustomExpression invalid,
        ExpressGeneratedTypeResolver resolver,
        SelectHydrationHelpers selectHydrationHelpers,
        ExpressPhysicalNameMap physicalNames)
    {
        if (!ExpressDescriptorTypeSupport.TryGetAggregateBounds(
                aggregate,
                out var lowerBound,
                out var upperBound))
        {
            throw new InvalidOperationException("Aggregate mapping requires literal bounds.");
        }

        var suffix = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var parameter = $"parameters[{suffix}]";
        var elements = $"{parameterName}Elements";
        var element = $"{parameterName}Element";
        var raw = $"{parameterName}Raw";
        var condition = $"{parameter}.TryGetAggregate(out var {elements})";
        if (aggregate.Kind == ExpressAggregateKind.Array)
        {
            var count = checked(upperBound!.Value - lowerBound + 1);
            condition += $" && {elements}.Count == {count.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            var elementCondition = CreateAggregateElementReadCondition(
                currentSchema,
                aggregate.ElementType,
                element,
                raw,
                resolver,
                selectHydrationHelpers,
                physicalNames);
            if (aggregate.IsOptional)
            {
                elementCondition = $"{element}.Kind == global::TedToolkit.Step21.ParameterValueKind.Omitted"
                    + $" || ({elementCondition})";
            }

            condition += " && global::System.Linq.Enumerable.All("
                + $"{elements}, {element} => {elementCondition})";
        }
        else
        {
            var elementCondition = CreateAggregateElementReadCondition(
                currentSchema,
                aggregate.ElementType,
                element,
                raw,
                resolver,
                selectHydrationHelpers,
                physicalNames);
            condition += " && global::System.Linq.Enumerable.All("
                + $"{elements}, {element} => {elementCondition})";
        }

        var candidate = CreateAggregateCandidate(currentSchema, aggregate, lowerBound, upperBound, resolver);
        string populated;
        if (aggregate.Kind == ExpressAggregateKind.Array)
        {
            var item = $"{parameterName}Item";
            var offset = $"{parameterName}Offset";
            var arrayRaw = $"{parameterName}ArrayRaw";
            var read = CreateAggregateElementReadExpression(
                currentSchema,
                aggregate.ElementType,
                $"{item}.{element}",
                arrayRaw,
                resolver,
                selectHydrationHelpers,
                physicalNames);
            populated = "global::System.Linq.Enumerable.Aggregate("
                + $"global::System.Linq.Enumerable.Select({elements}, ({element}, {offset}) => ({element}, {offset})), "
                + $"{candidate}, (aggregateCandidate, {item}) => {{ "
                + $"if ({item}.{element}.Kind != global::TedToolkit.Step21.ParameterValueKind.Omitted) "
                + "aggregateCandidate["
                + $"{lowerBound.ToString(System.Globalization.CultureInfo.InvariantCulture)} + {item}.{offset}] "
                + $"= {read}; "
                + "return aggregateCandidate; })";
        }
        else
        {
            var aggregateRaw = $"{parameterName}AggregateRaw";
            var read = CreateAggregateElementReadExpression(
                currentSchema,
                aggregate.ElementType,
                element,
                aggregateRaw,
                resolver,
                selectHydrationHelpers,
                physicalNames);
            populated = "global::System.Linq.Enumerable.Aggregate("
                + $"{elements}, {candidate}, (aggregateCandidate, {element}) => {{ "
                + $"aggregateCandidate.Add({read}); return aggregateCandidate; }})";
        }

        return new IfStatement(new CustomExpression(condition))
            .AddStatement(new CustomExpression(
                $"{target} = {CreateReadValueExpression(currentSchema, declaredType, populated, resolver, physicalNames)}"))
            .Else()
            .AddStatement(invalid);
    }

    private static string CreateProjectedAggregate(
        ExpressBoundSchemaIdentity currentSchema,
        ExpressBoundAggregateType aggregate,
        string value,
        int index,
        ExpressGeneratedTypeResolver resolver,
        SelectProjectionHelpers selectProjectionHelpers,
        ExpressPhysicalNameMap physicalNames)
    {
        if (!ExpressDescriptorTypeSupport.TryGetAggregateBounds(
                aggregate,
                out var lowerBound,
                out var upperBound))
        {
            throw new InvalidOperationException("Aggregate projection requires literal bounds.");
        }

        var suffix = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var item = $"projectedAggregateItem{suffix}";
        if (aggregate.Kind == ExpressAggregateKind.Array)
        {
            item = $"{value}[projectedAggregateIndex{suffix}]";
            var projected = CreateProjectedValue(
                currentSchema,
                aggregate.ElementType,
                item,
                (index * 100) + 1,
                resolver,
                selectProjectionHelpers,
                physicalNames);
            var count = checked(upperBound!.Value - lowerBound + 1);
            return "global::TedToolkit.Step21.ParameterValue.FromAggregate("
                + "global::System.Linq.Enumerable.Select("
                + $"global::System.Linq.Enumerable.Range({lowerBound.ToString(System.Globalization.CultureInfo.InvariantCulture)}, "
                + $"{count.ToString(System.Globalization.CultureInfo.InvariantCulture)}), projectedAggregateIndex{suffix} => "
                + $"{value}.IsSet(projectedAggregateIndex{suffix}) ? {projected} "
                + ": global::TedToolkit.Step21.ParameterValue.Omitted))";
        }

        return "global::TedToolkit.Step21.ParameterValue.FromAggregate("
            + $"global::System.Linq.Enumerable.Select({value}, {item} => "
            + $"{CreateProjectedValue(
                currentSchema,
                aggregate.ElementType,
                item,
                (index * 100) + 1,
                resolver,
                selectProjectionHelpers,
                physicalNames)}))";
    }

    private static string CreateAggregateCandidate(
        ExpressBoundSchemaIdentity currentSchema,
        ExpressBoundAggregateType aggregate,
        int lowerBound,
        int? upperBound,
        ExpressGeneratedTypeResolver resolver)
    {
        var elementType = CreateClrTypeName(currentSchema, aggregate.ElementType, resolver);
        var lower = lowerBound.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var upper = upperBound?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null";
        var isOptional = aggregate.IsOptional ? "true" : "false";
        var isUnique = aggregate.IsUnique ? "true" : "false";
        return aggregate.Kind switch
        {
            ExpressAggregateKind.Array =>
                $"new global::TedToolkit.Step21.ExpressArray<{elementType}>({lower}, {upper}, "
                + $"{isOptional}, {isUnique})",
            ExpressAggregateKind.Bag =>
                $"new global::TedToolkit.Step21.ExpressBag<{elementType}>({lower}, {upper})",
            ExpressAggregateKind.List =>
                $"new global::TedToolkit.Step21.ExpressList<{elementType}>({lower}, {upper}, "
                + $"{isUnique})",
            ExpressAggregateKind.Set =>
                $"new global::TedToolkit.Step21.ExpressSet<{elementType}>({lower}, {upper})",
            _ => throw new InvalidOperationException("General AGGREGATE mapping requires a later descriptor contract."),
        };
    }

    private static string CreateAggregateElementReadCondition(
        ExpressBoundSchemaIdentity currentSchema,
        ExpressBoundType type,
        string parameter,
        string rawName,
        ExpressGeneratedTypeResolver resolver,
        SelectHydrationHelpers selectHydrationHelpers,
        ExpressPhysicalNameMap physicalNames)
    {
        if (type is ExpressBoundNamedType namedEntity
            && namedEntity.Declaration.Kind == ExpressDeclarationKind.Entity)
        {
            var entityType = GetGeneratedTypeName(currentSchema, namedEntity.Declaration);
            return $"{parameter}.TryGetEntity(out var {rawName}Entity) && {rawName}Entity is {entityType}";
        }

        if (type is ExpressBoundNamedType namedSelect
            && resolver.GetDefinedType(namedSelect.Declaration).UnderlyingType is ExpressBoundSelectType select)
        {
            return selectHydrationHelpers.CreateCall(namedSelect, select, parameter, "_");
        }

        var terminal = ExpressDescriptorTypeSupport.GetTerminalType(type, resolver);
        if (terminal is ExpressBoundScalarType { Kind: ExpressScalarKind.Number, })
        {
            return $"({parameter}.TryGetInteger(out _) || {parameter}.TryGetReal(out _))";
        }

        if (terminal is ExpressBoundScalarType { Kind: ExpressScalarKind.Boolean, })
        {
            return $"({parameter}.TryGetBoolean(out _) || ({parameter}.TryGetEnumeration(out var {rawName}Symbol) "
                + $"&& ({rawName}Symbol is \"T\" or \"F\")))";
        }

        if (terminal is ExpressBoundScalarType { Kind: ExpressScalarKind.Logical, })
        {
            return $"({parameter}.TryGetLogical(out _) || ({parameter}.TryGetEnumeration(out var {rawName}Symbol) "
                + $"&& ({rawName}Symbol is \"T\" or \"F\" or \"U\")))";
        }

        return CreateReadCondition(type, parameter, rawName, resolver, physicalNames);
    }

    private static string CreateAggregateElementReadExpression(
        ExpressBoundSchemaIdentity currentSchema,
        ExpressBoundType type,
        string parameter,
        string rawName,
        ExpressGeneratedTypeResolver resolver,
        SelectHydrationHelpers selectHydrationHelpers,
        ExpressPhysicalNameMap physicalNames)
    {
        if (type is ExpressBoundNamedType namedEntity
            && namedEntity.Declaration.Kind == ExpressDeclarationKind.Entity)
        {
            var entityType = GetGeneratedTypeName(currentSchema, namedEntity.Declaration);
            return $"{parameter}.TryGetEntity(out var {rawName}Entity) && {rawName}Entity is {entityType} {rawName} "
                + $"? {rawName} : throw new global::System.InvalidOperationException()";
        }

        if (type is ExpressBoundNamedType namedSelect
            && resolver.GetDefinedType(namedSelect.Declaration).UnderlyingType is ExpressBoundSelectType select)
        {
            var valueName = rawName + "Select";
            return selectHydrationHelpers.CreateCall(namedSelect, select, parameter, $"var {valueName}")
                + $" ? {valueName} : throw new global::System.InvalidOperationException()";
        }

        var terminal = ExpressDescriptorTypeSupport.GetTerminalType(type, resolver);
        if (terminal is ExpressBoundScalarType { Kind: ExpressScalarKind.Number, })
        {
            var number = $"{parameter}.TryGetInteger(out var {rawName}Integer) "
                + $"? global::TedToolkit.Step21.NumberValue.FromInteger({rawName}Integer) "
                + $": {parameter}.TryGetReal(out var {rawName}Real) "
                + $"? global::TedToolkit.Step21.NumberValue.FromReal({rawName}Real) "
                + ": throw new global::System.InvalidOperationException()";
            return CreateReadValueExpression(currentSchema, type, number, resolver, physicalNames);
        }

        if (terminal is ExpressBoundScalarType { Kind: ExpressScalarKind.Boolean, })
        {
            var boolean = $"{parameter}.TryGetBoolean(out var {rawName}Boolean) "
                + $"? {rawName}Boolean : {parameter}.TryGetEnumeration(out var {rawName}Symbol) "
                + $"? {rawName}Symbol == \"T\" : throw new global::System.InvalidOperationException()";
            return CreateReadValueExpression(currentSchema, type, boolean, resolver, physicalNames);
        }

        if (terminal is ExpressBoundScalarType { Kind: ExpressScalarKind.Logical, })
        {
            var logical = $"{parameter}.TryGetLogical(out var {rawName}Logical) "
                + $"? {rawName}Logical : {parameter}.TryGetEnumeration(out var {rawName}Symbol) "
                + $"? {rawName}Symbol == \"T\" ? global::TedToolkit.Step21.LogicalValue.True "
                + $": {rawName}Symbol == \"F\" ? global::TedToolkit.Step21.LogicalValue.False "
                + ": global::TedToolkit.Step21.LogicalValue.Unknown "
                + ": throw new global::System.InvalidOperationException()";
            return CreateReadValueExpression(currentSchema, type, logical, resolver, physicalNames);
        }

        var condition = CreateReadCondition(type, parameter, rawName, resolver, physicalNames);
        var value = CreateReadValueExpression(currentSchema, type, rawName, resolver, physicalNames);
        return $"{condition} ? {value} : throw new global::System.InvalidOperationException()";
    }

    private static string CreateClrTypeName(
        ExpressBoundSchemaIdentity currentSchema,
        ExpressBoundType type,
        ExpressGeneratedTypeResolver resolver)
    {
        if (type is ExpressBoundNamedType named)
        {
            return GetGeneratedTypeName(currentSchema, named.Declaration);
        }

        if (type is ExpressBoundAggregateType aggregate)
        {
            var aggregateName = aggregate.Kind switch
            {
                ExpressAggregateKind.Array => "ExpressArray",
                ExpressAggregateKind.Bag => "ExpressBag",
                ExpressAggregateKind.List => "ExpressList",
                ExpressAggregateKind.Set => "ExpressSet",
                _ => throw new InvalidOperationException("General AGGREGATE has no generated runtime type."),
            };
            return $"global::TedToolkit.Step21.{aggregateName}<{CreateClrTypeName(currentSchema, aggregate.ElementType, resolver)}>";
        }

        return ((ExpressBoundScalarType)type).Kind switch
        {
            ExpressScalarKind.Binary => "global::TedToolkit.Step21.BinaryValue",
            ExpressScalarKind.Boolean => "global::System.Boolean",
            ExpressScalarKind.Integer => "global::System.Numerics.BigInteger",
            ExpressScalarKind.Logical => "global::TedToolkit.Step21.LogicalValue",
            ExpressScalarKind.Number => "global::TedToolkit.Step21.NumberValue",
            ExpressScalarKind.Real => "global::TedToolkit.Step21.RealValue",
            ExpressScalarKind.String => "global::System.String",
            _ => throw new InvalidOperationException("Unsupported aggregate element scalar type."),
        };
    }

    private static string GetGeneratedTypeName(
        ExpressBoundSchemaIdentity currentSchema,
        ExpressBoundSymbol symbol)
    {
        var generatedName = ExpressEntityProjection.ToPascalCase(symbol.Name);
        if (symbol.Kind == ExpressDeclarationKind.Entity)
        {
            generatedName = $"I{generatedName}";
        }

        return ReferenceEquals(currentSchema, symbol.DeclaringSchema)
            ? generatedName
            : "global::TedToolkit.Step21.Schemas."
                + $"{ExpressEntityProjection.ToPascalCase(symbol.DeclaringSchema.Name)}.{generatedName}";
    }

    private static string? CreateIncompatibleReferenceCondition(
        ExpressBoundSchemaIdentity currentSchema,
        ExpressBoundType type,
        string parameter,
        string rawName,
        ExpressGeneratedTypeResolver resolver)
    {
        if (type is ExpressBoundAggregateType aggregate)
        {
            var element = $"{rawName}Element";
            var elementCondition = CreateIncompatibleReferenceCondition(
                currentSchema,
                aggregate.ElementType,
                element,
                $"{rawName}Nested",
                resolver);
            return elementCondition is null
                ? null
                : $"{parameter}.TryGetAggregate(out var {rawName}Values) "
                    + $"&& global::System.Linq.Enumerable.Any({rawName}Values, {element} => {elementCondition})";
        }

        if (type is ExpressBoundSelectType select)
        {
            var entityTypes = GetSelectEntityAlternatives(
                    select,
                    resolver,
                    new HashSet<ExpressBoundSymbol>())
                .Select(alternative => GetGeneratedTypeName(currentSchema, alternative))
                .ToArray();
            return entityTypes.Length == 0
                ? null
                : $"{parameter}.TryGetEntity(out var {rawName}Entity) "
                    + $"&& !({string.Join(" || ", entityTypes.Select(entityType => $"{rawName}Entity is {entityType}"))})";
        }

        if (type is not ExpressBoundNamedType named)
        {
            return null;
        }

        if (named.Declaration.Kind == ExpressDeclarationKind.Entity)
        {
            var entityType = GetGeneratedTypeName(currentSchema, named.Declaration);
            return $"{parameter}.TryGetEntity(out var {rawName}Entity) && {rawName}Entity is not {entityType}";
        }

        return CreateIncompatibleReferenceCondition(
            currentSchema,
            resolver.GetDefinedType(named.Declaration).UnderlyingType,
            parameter,
            rawName,
            resolver);
    }

    private static ExpressBoundSymbol[] GetSelectEntityAlternatives(
        ExpressBoundSelectType select,
        ExpressGeneratedTypeResolver resolver,
        HashSet<ExpressBoundSymbol> path)
    {
        var result = new List<ExpressBoundSymbol>();
        foreach (var alternative in resolver.GetSelectAlternatives(select))
        {
            if (alternative.Kind == ExpressDeclarationKind.Entity)
            {
                result.Add(alternative);
                continue;
            }

            if (!path.Add(alternative))
            {
                continue;
            }

            if (resolver.GetDefinedType(alternative).UnderlyingType is ExpressBoundSelectType nested)
            {
                result.AddRange(GetSelectEntityAlternatives(
                    nested,
                    resolver,
                    new HashSet<ExpressBoundSymbol>(path)));
            }
        }

        return result.Distinct().ToArray();
    }

    private static IfStatement CreateSelectHydrationBranch(
        ExpressBoundNamedType namedSelect,
        ExpressBoundSelectType select,
        int index,
        string parameterName,
        string target,
        CustomExpression invalid,
        SelectHydrationHelpers selectHydrationHelpers)
    {
        var parameter = $"parameters[{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}]";
        var valueName = parameterName + "Select";
        return new IfStatement(new CustomExpression(
                selectHydrationHelpers.CreateCall(namedSelect, select, parameter, $"var {valueName}")))
            .AddStatement(new CustomExpression($"{target} = {valueName}"))
            .Else()
            .AddStatement(invalid);
    }

    private static string CreateReadCondition(
        ExpressBoundType type,
        string parameter,
        string rawName,
        ExpressGeneratedTypeResolver resolver,
        ExpressPhysicalNameMap physicalNames)
    {
        var terminal = ExpressDescriptorTypeSupport.GetTerminalType(type, resolver);
        if (terminal is ExpressBoundScalarType scalar)
        {
            if (scalar.Kind == ExpressScalarKind.Boolean)
            {
                return $"({parameter}.TryGetBoolean(out var {rawName}) "
                    + $"|| ({parameter}.TryGetEnumeration(out var {rawName}Symbol) "
                    + $"&& ({rawName}Symbol is \"T\" or \"F\") "
                    + $"&& (({rawName} = {rawName}Symbol == \"T\") || !{rawName})))";
            }

            if (scalar.Kind == ExpressScalarKind.Logical)
            {
                return $"({parameter}.TryGetLogical(out var {rawName}) "
                    + $"|| ({parameter}.TryGetEnumeration(out var {rawName}Symbol) "
                    + $"&& ({rawName}Symbol is \"T\" or \"F\" or \"U\") "
                    + $"&& (({rawName} = {rawName}Symbol == \"T\" "
                    + "? global::TedToolkit.Step21.LogicalValue.True "
                    + $": {rawName}Symbol == \"F\" ? global::TedToolkit.Step21.LogicalValue.False "
                    + ": global::TedToolkit.Step21.LogicalValue.Unknown) "
                    + "== global::TedToolkit.Step21.LogicalValue.Unknown "
                    + $"|| {rawName} != global::TedToolkit.Step21.LogicalValue.Unknown)))";
            }

            if (scalar.Kind == ExpressScalarKind.Real)
            {
                return $"TryHydrateReal(structure, {parameter}, out var {rawName})";
            }

            var method = scalar.Kind switch
            {
                ExpressScalarKind.Binary => "TryGetBinary",
                ExpressScalarKind.Integer => "TryGetInteger",
                ExpressScalarKind.String => "TryGetString",
                _ => throw new InvalidOperationException("NUMBER SELECT alternatives require a later mapping branch."),
            };
            return $"{parameter}.{method}(out var {rawName})";
        }

        var enumeration = (ExpressBoundEnumerationType)terminal;
        var condition = $"{parameter}.TryGetEnumeration(out var {rawName})";
        return enumeration.IsExtensible
            ? condition
            : condition + " && " + CreateEnumerationCondition(
                rawName,
                type,
                resolver.GetEnumerationValues(enumeration),
                resolver,
                physicalNames);
    }

    private static List<SelectReadBranch> CreateSelectReadBranches(
        ExpressBoundSchemaIdentity currentSchema,
        ExpressBoundNamedType namedSelect,
        ExpressBoundSelectType select,
        string parameter,
        string prefix,
        ExpressGeneratedTypeResolver resolver,
        ExpressPhysicalNameMap physicalNames,
        HashSet<ExpressBoundSymbol> path)
    {
        if (!path.Add(namedSelect.Declaration))
        {
            return [];
        }

        var selectType = GetGeneratedTypeName(currentSchema, namedSelect.Declaration);
        var result = new List<SelectReadBranch>();
        foreach (var alternative in resolver.GetSelectAlternatives(select))
        {
            var alternativeName = ExpressEntityProjection.ToPascalCase(alternative.Name);
            var selectedPrefix = prefix + alternativeName;
            if (alternative.Kind == ExpressDeclarationKind.Entity)
            {
                var entityType = GetGeneratedTypeName(currentSchema, alternative);
                result.Add(new(
                    $"{parameter}.TryGetEntity(out var {selectedPrefix}Entity) "
                        + $"&& {selectedPrefix}Entity is {entityType} {selectedPrefix}",
                    $"{selectType}.From{alternativeName}({selectedPrefix})"));
                continue;
            }

            var alternativeType = CreateNamedType(alternative);
            var underlying = resolver.GetDefinedType(alternative).UnderlyingType;
            if (underlying is ExpressBoundSelectType nestedSelect)
            {
                foreach (var nested in CreateSelectReadBranches(
                             currentSchema,
                             alternativeType,
                             nestedSelect,
                             parameter,
                             selectedPrefix,
                             resolver,
                             physicalNames,
                             new HashSet<ExpressBoundSymbol>(path)))
                {
                    result.Add(new(
                        nested.Condition,
                        $"{selectType}.From{alternativeName}({nested.Value})"));
                }

                continue;
            }

            var innerName = selectedPrefix + "Inner";
            var rawName = selectedPrefix + "Raw";
            if (ExpressDescriptorTypeSupport.GetTerminalType(alternativeType, resolver) is ExpressBoundScalarType
                {
                    Kind: ExpressScalarKind.Number,
                })
            {
                var numberCondition = $"{parameter}.TryGetTyped(out var {selectedPrefix}Type, out var {innerName}) "
                    + "&& " + CreateNameCondition(
                        $"{selectedPrefix}Type",
                        alternative.Name.ToUpperInvariant(),
                        physicalNames.TypeName(alternative)) + " "
                    + $"&& ({innerName}.TryGetInteger(out _) || {innerName}.TryGetReal(out _))";
                var number = $"{innerName}.TryGetInteger(out var {rawName}Integer) "
                    + $"? global::TedToolkit.Step21.NumberValue.FromInteger({rawName}Integer) "
                    + $": {innerName}.TryGetReal(out var {rawName}Real) "
                    + $"? global::TedToolkit.Step21.NumberValue.FromReal({rawName}Real) "
                    + ": throw new global::System.InvalidOperationException()";
                var selectedValue = CreateReadValueExpression(
                    currentSchema,
                    alternativeType,
                    number,
                    resolver,
                    physicalNames);
                result.Add(new(
                    numberCondition,
                    $"{selectType}.From{alternativeName}({selectedValue})"));
                continue;
            }

            var condition = $"{parameter}.TryGetTyped(out var {selectedPrefix}Type, out var {innerName}) "
                + "&& " + CreateNameCondition(
                    $"{selectedPrefix}Type",
                    alternative.Name.ToUpperInvariant(),
                    physicalNames.TypeName(alternative)) + " "
                + $"&& {CreateReadCondition(alternativeType, innerName, rawName, resolver, physicalNames)}";
            var value = CreateReadValueExpression(
                currentSchema,
                alternativeType,
                rawName,
                resolver,
                physicalNames);
            result.Add(new(condition, $"{selectType}.From{alternativeName}({value})"));
        }

        return result;
    }

    private static string CreateEnumerationCondition(
        string expression,
        ExpressBoundType type,
        IEnumerable<string> values,
        ExpressGeneratedTypeResolver resolver,
        ExpressPhysicalNameMap physicalNames)
    {
        var owner = GetEnumerationOwner(type, resolver);
        var names = values.SelectMany(value => CreatePhysicalNamePatterns(
            value.ToUpperInvariant(),
            owner is null ? value.ToUpperInvariant() : physicalNames.EnumerationName(owner, value)));
        return $"{expression} is {string.Join(" or ", names)}";
    }

    private static IEnumerable<string> CreateEnumerationReadCases(
        ExpressBoundSymbol owner,
        string value,
        string generatedType,
        ExpressPhysicalNameMap physicalNames)
    {
        var longName = value.ToUpperInvariant();
        var physicalName = physicalNames.EnumerationName(owner, value);
        var target = $"{generatedType}.{ExpressEntityProjection.ToPascalCase(value)}";
        yield return $"\"{longName}\" => {target}";
        if (StringComparer.Ordinal.Equals(longName, physicalName))
        {
            yield break;
        }

        yield return $"\"{physicalName}\" => {target}";
    }

    private static IEnumerable<string> CreatePhysicalNamePatterns(string longName, string physicalName)
    {
        yield return $"\"{longName}\"";
        if (StringComparer.Ordinal.Equals(longName, physicalName))
        {
            yield break;
        }

        yield return $"\"{physicalName}\"";
    }

    private static string CreateEnumerationProjection(
        ExpressBoundSymbol owner,
        string expression,
        ExpressGeneratedTypeResolver resolver,
        ExpressPhysicalNameMap physicalNames)
    {
        var enumeration = (ExpressBoundEnumerationType)resolver.GetDefinedType(owner).UnderlyingType;
        var cases = resolver.GetEnumerationValues(enumeration)
            .Select(value => (Long: value.ToUpperInvariant(), Physical: physicalNames.EnumerationName(owner, value)))
            .Where(pair => !StringComparer.Ordinal.Equals(pair.Long, pair.Physical))
            .Select(pair => $"\"{pair.Long}\" => \"{pair.Physical}\"")
            .ToArray();
        return cases.Length == 0
            ? expression
            : $"{expression} switch {{ {string.Join(", ", cases)}, _ => {expression} }}";
    }

    private static string CreateEnumerationLongProjection(
        ExpressBoundSymbol owner,
        string expression,
        ExpressGeneratedTypeResolver resolver,
        ExpressPhysicalNameMap physicalNames)
    {
        var enumeration = (ExpressBoundEnumerationType)resolver.GetDefinedType(owner).UnderlyingType;
        var cases = resolver.GetEnumerationValues(enumeration)
            .Select(value => (Long: value.ToUpperInvariant(), Physical: physicalNames.EnumerationName(owner, value)))
            .Where(pair => !StringComparer.Ordinal.Equals(pair.Long, pair.Physical))
            .Select(pair => $"\"{pair.Physical}\" => \"{pair.Long}\"")
            .ToArray();
        return cases.Length == 0
            ? expression
            : $"{expression} switch {{ {string.Join(", ", cases)}, _ => {expression} }}";
    }

    private static ExpressBoundSymbol? GetEnumerationOwner(
        ExpressBoundType type,
        ExpressGeneratedTypeResolver resolver)
    {
        while (type is ExpressBoundNamedType named && named.Declaration.Kind != ExpressDeclarationKind.Entity)
        {
            var declaration = resolver.GetDefinedType(named.Declaration);
            if (declaration.UnderlyingType is ExpressBoundEnumerationType)
            {
                return named.Declaration;
            }

            type = declaration.UnderlyingType;
        }

        return null;
    }

    private static string CreateProjectedNumber(string value, int index)
    {
        var suffix = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return $"{value}.TryGetInteger(out var projectedInteger{suffix}) "
            + $"? global::TedToolkit.Step21.ParameterValue.FromInteger(projectedInteger{suffix}) "
            + $": {value}.TryGetReal(out var projectedReal{suffix}) "
            + $"? global::TedToolkit.Step21.ParameterValue.FromReal(projectedReal{suffix}) "
            + ": throw new global::System.InvalidOperationException()";
    }

    private static ExpressBoundNamedType CreateNamedType(ExpressBoundSymbol symbol)
    {
        return new(symbol, symbol.Span);
    }

    /// <summary>
    /// Owns one shared parameter projection method per named SELECT used by descriptor serialization.
    /// </summary>
    private sealed class SelectProjectionHelpers
    {
        private readonly ExpressBoundSchemaIdentity _currentSchema;

        private readonly ExpressGeneratedTypeResolver _resolver;

        private readonly ExpressDescriptorShards _shards;

        private readonly ExpressPhysicalNameMap _physicalNames;

        private readonly Dictionary<ExpressBoundSymbol, string> _calls = [];

        /// <summary>
        /// Initializes shared SELECT projectors for one generated descriptor.
        /// </summary>
        /// <param name="currentSchema">The schema owning generated use sites.</param>
        /// <param name="resolver">The generated value-type resolver.</param>
        /// <param name="shards">The structural descriptor partitions.</param>
        /// <param name="physicalNames">The explicit Part 21 physical-name inventory.</param>
        internal SelectProjectionHelpers(
            ExpressBoundSchemaIdentity currentSchema,
            ExpressGeneratedTypeResolver resolver,
            ExpressDescriptorShards shards,
            ExpressPhysicalNameMap physicalNames)
        {
            _currentSchema = currentSchema;
            _resolver = resolver;
            _shards = shards;
            _physicalNames = physicalNames;
        }

        /// <summary>
        /// Creates a call to the shared projector for one named SELECT.
        /// </summary>
        /// <param name="namedSelect">The named SELECT type.</param>
        /// <param name="select">The resolved SELECT domain.</param>
        /// <param name="value">The generated SELECT value expression.</param>
        /// <returns>The generated projector call.</returns>
        internal string CreateCall(
            ExpressBoundNamedType namedSelect,
            ExpressBoundSelectType select,
            string value)
        {
            if (!_calls.TryGetValue(namedSelect.Declaration, out var callName))
            {
                var ordinal = _calls.Count;
                var suffix = ExpressEntityProjection.ToPascalCase(namedSelect.Declaration.Name)
                    + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var methodName = "__ExpressProjectSelect" + suffix;
                var shardName = "__ExpressSelectProjectionShard"
                    + (ordinal / SELECT_PROJECTION_METHODS_PER_SHARD)
                        .ToString(System.Globalization.CultureInfo.InvariantCulture);
                callName = _shards.Qualify(methodName, shardName);
                _calls.Add(namedSelect.Declaration, callName);

                var method = SourceComposer<ExpressIncrementalGenerator>.Method(
                    methodName,
                    SourceComposer.ReturnType(new DataType("global::TedToolkit.Step21.ParameterValue")));
                method.Accessibility = TedToolkit.RoslynHelper.Accessibility.PRIVATE;
                method.IsStatic = true;
                var selectType = GetGeneratedTypeName(_currentSchema, namedSelect.Declaration);
                method.AddParameter(SourceComposer.Parameter(new DataType(selectType), "value"));

                var alternativeIndex = 0;
                foreach (var alternative in _resolver.GetSelectAlternatives(select))
                {
                    var alternativeName = ExpressEntityProjection.ToPascalCase(alternative.Name);
                    var selectedName = "projectedSelect"
                        + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + alternativeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var alternativeType = CreateNamedType(alternative);
                    var underlying = alternative.Kind == ExpressDeclarationKind.Entity
                        ? null
                        : _resolver.GetDefinedType(alternative).UnderlyingType;
                    var projected = CreateProjectedValue(
                        _currentSchema,
                        alternativeType,
                        selectedName,
                        checked((ordinal * 1000) + alternativeIndex),
                        _resolver,
                        this,
                        _physicalNames);
                    if (alternative.Kind != ExpressDeclarationKind.Entity
                        && underlying is not ExpressBoundSelectType)
                    {
                        projected = "global::TedToolkit.Step21.ParameterValue.FromTyped("
                            + $"\"{_physicalNames.TypeName(alternative)}\", {projected})";
                    }

                    method.AddStatement(new IfStatement(new CustomExpression(
                            $"value.TryGet{alternativeName}(out var {selectedName})"))
                        .AddStatement(new CustomExpression(projected).Return));
                    alternativeIndex++;
                }

                method.AddStatement(new CustomExpression(
                    "throw new global::System.InvalidOperationException()"));
                AddSummary(method, "Projects one named SELECT value to its physical parameter representation.");
                _shards.Add(method, shardName);
            }

            return $"{callName}({value})";
        }
    }

    /// <summary>
    /// Owns one shared parameter reader per named SELECT used by descriptor hydration.
    /// </summary>
    private sealed class SelectHydrationHelpers
    {
        private readonly ExpressBoundSchemaIdentity _currentSchema;

        private readonly ExpressGeneratedTypeResolver _resolver;

        private readonly ExpressDescriptorShards _shards;

        private readonly ExpressPhysicalNameMap _physicalNames;

        private readonly Dictionary<ExpressBoundSymbol, string> _calls = [];

        /// <summary>
        /// Initializes shared SELECT readers for one generated descriptor.
        /// </summary>
        /// <param name="currentSchema">The schema owning generated use sites.</param>
        /// <param name="resolver">The generated value-type resolver.</param>
        /// <param name="shards">The structural descriptor partitions.</param>
        /// <param name="physicalNames">The explicit Part 21 physical-name inventory.</param>
        internal SelectHydrationHelpers(
            ExpressBoundSchemaIdentity currentSchema,
            ExpressGeneratedTypeResolver resolver,
            ExpressDescriptorShards shards,
            ExpressPhysicalNameMap physicalNames)
        {
            _currentSchema = currentSchema;
            _resolver = resolver;
            _shards = shards;
            _physicalNames = physicalNames;
        }

        /// <summary>
        /// Creates a call to the shared reader for one named SELECT.
        /// </summary>
        /// <param name="namedSelect">The named SELECT type.</param>
        /// <param name="select">The resolved SELECT domain.</param>
        /// <param name="parameter">The parameter-value expression.</param>
        /// <param name="output">The generated out argument without the <c>out</c> modifier.</param>
        /// <returns>The generated reader call.</returns>
        internal string CreateCall(
            ExpressBoundNamedType namedSelect,
            ExpressBoundSelectType select,
            string parameter,
            string output)
        {
            if (!_calls.TryGetValue(namedSelect.Declaration, out var callName))
            {
                var ordinal = _calls.Count;
                var suffix = ExpressEntityProjection.ToPascalCase(namedSelect.Declaration.Name)
                    + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var methodName = "__ExpressTryReadSelect" + suffix;
                var shardName = "__ExpressSelectHydrationShard"
                    + (ordinal / SELECT_HYDRATION_METHODS_PER_SHARD)
                        .ToString(System.Globalization.CultureInfo.InvariantCulture);
                callName = _shards.Qualify(methodName, shardName);
                _calls.Add(namedSelect.Declaration, callName);

                var method = SourceComposer<ExpressIncrementalGenerator>.Method(
                    methodName,
                    SourceComposer.ReturnType(new DataType("global::System.Boolean")));
                method.Accessibility = TedToolkit.RoslynHelper.Accessibility.PRIVATE;
                method.IsStatic = true;
                var selectType = GetGeneratedTypeName(_currentSchema, namedSelect.Declaration);
                method.AddParameter(SourceComposer.Parameter(
                    new DataType("global::TedToolkit.Step21.ExchangeStructure"),
                    "structure"));
                method.AddParameter(SourceComposer.Parameter(
                    new DataType("global::TedToolkit.Step21.ParameterValue"),
                    "parameter"));
                method.AddParameter(SourceComposer.Parameter(new DataType("out " + selectType), "value"));

                var branches = CreateSelectReadBranches(
                    _currentSchema,
                    namedSelect,
                    select,
                    "parameter",
                    "selectRead" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _resolver,
                    _physicalNames,
                    new HashSet<ExpressBoundSymbol>());
                foreach (var branch in branches)
                {
                    method.AddStatement(new IfStatement(new CustomExpression(branch.Condition))
                        .AddStatement(new CustomExpression($"value = {branch.Value}"))
                        .AddStatement(new CustomExpression("true").Return));
                }

                method.AddStatement(new CustomExpression("value = default!"));
                method.AddStatement(new CustomExpression("false").Return);
                AddSummary(method, "Reads one named SELECT value through its compatible typed alternative.");
                _shards.Add(method, shardName);
            }

            return $"{callName}(structure, {parameter}, out {output})";
        }
    }

    private sealed class SelectReadBranch(string condition, string value)
    {
        internal string Condition { get; } = condition;

        internal string Value { get; } = value;
    }

    private static void AddSummary(IRootDescription target, string text)
    {
        target.AddRootDescription(new DescriptionSummary(
            new IDescriptionItem[] { new DescriptionText(text), }));
    }
}