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
    /// <summary>
    /// Emits one path-independent sealed schema descriptor through RoslynHelper.
    /// </summary>
    /// <param name="context">The source-production context.</param>
    /// <param name="schema">The valid bound schema.</param>
    /// <param name="entities">The generated entities owned by the schema.</param>
    /// <param name="resolver">The closed-set generated value resolver.</param>
    /// <param name="rulePlan">The validated reachable rule closure.</param>
    internal static void Emit(
        in SourceProductionContext context,
        ExpressBoundSchema schema,
        IReadOnlyList<ExpressEntityProjection> entities,
        ExpressGeneratedTypeResolver resolver,
        ExpressReachableRulePlan rulePlan)
    {
        var descriptor = SourceComposer<ExpressIncrementalGenerator>.Class("SchemaDescriptor");
        descriptor.Accessibility = TedToolkit.RoslynHelper.Accessibility.PUBLIC;
        descriptor.Polymorphism = Polymorphism.SEALED;
        descriptor.AddBaseType(new DataType("global::TedToolkit.Step21.SchemaDescriptor"));
        AddSummary(descriptor, $"Provides reflection-free mapping infrastructure for the {schema.Name} EXPRESS schema.");
        ExpressStructuralValidationEmitter.AddDescriptorDocumentation(descriptor, schema, entities, rulePlan);

        descriptor.AddMember(CreateConstructor());
        descriptor.AddMember(CreateInstanceProperty());
        descriptor.AddMember(CreateNameProperty(schema));
        descriptor.AddMember(CreateAllocateMethod(entities));
        descriptor.AddMember(CreateHydrateMethod(entities, resolver));
        descriptor.AddMember(ExpressStructuralValidationEmitter.CreateDispatchMethod(
            schema,
            entities,
            resolver,
            rulePlan));
        foreach (var entity in entities.Where(candidate => !candidate.Entity.IsAbstract))
        {
            descriptor.AddMember(ExpressStructuralValidationEmitter.CreateEntityMethod(entity, resolver, rulePlan));
        }

        foreach (var method in ExpressReachableRuleEmitter.CreateDependencyMethods(rulePlan, resolver))
        {
            descriptor.AddMember(method);
        }

        descriptor.AddMember(CreateCapabilityMethod());
        descriptor.AddMember(CreateProjectMethod(entities, resolver));

        var generatedNamespace = $"TedToolkit.Step21.Generated.{ExpressEntityProjection.ToPascalCase(schema.Name)}";
        SourceComposer.File()
            .AddNameSpace(SourceComposer.NameSpace(generatedNamespace).AddMember(descriptor))
            .Generate(in context, $"ExpressSchema_{schema.Name.ToUpperInvariant()}");
    }

    private static Constructor CreateConstructor()
    {
        var constructor = SourceComposer<ExpressIncrementalGenerator>.Constructor();
        constructor.Accessibility = TedToolkit.RoslynHelper.Accessibility.PRIVATE;
        AddSummary(constructor, "Initializes the singleton schema descriptor.");
        return constructor;
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

    private static Method CreateAllocateMethod(IReadOnlyList<ExpressEntityProjection> entities)
    {
        var method = CreateOverrideMethod(
            "AllocateEntityCore",
            new DataType("global::TedToolkit.Step21.Entity").Null);
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::System.Collections.Generic.IReadOnlyList<global::System.String>"),
            "entityNames"));
        var alternatives = entities
            .Where(entity => !entity.Entity.IsAbstract)
            .Select(entity => $"\"{entity.Entity.Name.ToUpperInvariant()}\" => {CreateEntity(entity)}")
            .ToArray();
        var expression = alternatives.Length == 0
            ? "null"
            : $"entityNames.Count == 1 ? entityNames[0] switch {{ {string.Join(", ", alternatives)}, _ => null }} : null";
        method.AddStatement(new CustomExpression(expression).Return);
        AddSummary(method, "Allocates a supported generated entity from its ordered physical name group.");
        return method;
    }

    private static string CreateEntity(ExpressEntityProjection entity)
    {
        var arguments = entity.EffectiveAttributes
            .Where(attribute => !attribute.Attribute.IsOptional)
            .Select(_ => "default!");
        return $"new {entity.Name}({string.Join(", ", arguments)})";
    }

    private static Method CreateHydrateMethod(
        IReadOnlyList<ExpressEntityProjection> entities,
        ExpressGeneratedTypeResolver resolver)
    {
        var diagnosticsType = new DataType(
            "global::System.Collections.Generic.IReadOnlyList<global::TedToolkit.Step21.Step21Diagnostic>");
        var method = CreateOverrideMethod("HydrateEntityCore", diagnosticsType);
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::TedToolkit.Step21.ExchangeStructure"),
            "structure"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::TedToolkit.Step21.Entity"), "value"));
        method.AddParameter(SourceComposer.Parameter(
            new DataType(
                "global::System.Collections.Generic.IReadOnlyList<global::System.Collections.Generic.KeyValuePair<global::System.String, global::System.Collections.Generic.IReadOnlyList<global::TedToolkit.Step21.ParameterValue>>>"),
            "components"));
        foreach (var entity in entities.Where(entity => CanMapSimpleEntity(entity, resolver)))
        {
            method.AddStatement(CreateHydrateEntityBranch(entity, resolver));
        }

        method.AddStatement(new CustomExpression(
            "[new global::TedToolkit.Step21.Step21Diagnostic(\"P21-BIND-ENTITY\", "
            + "global::TedToolkit.Step21.Step21DiagnosticSeverity.Error, "
            + "\"The entity is not supported by this schema descriptor.\")]").Return);
        AddSummary(method, "Hydrates one supported simple entity from strong physical parameters.");
        return method;
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
        IReadOnlyList<ExpressEntityProjection> entities,
        ExpressGeneratedTypeResolver resolver)
    {
        var componentType = new DataType(
            "global::System.Collections.Generic.IReadOnlyList<global::System.Collections.Generic.KeyValuePair<global::System.String, global::System.Collections.Generic.IReadOnlyList<global::TedToolkit.Step21.ParameterValue>>>");
        var method = CreateOverrideMethod("ProjectEntityCore", componentType);
        method.AddParameter(SourceComposer.Parameter(new DataType("global::TedToolkit.Step21.Entity"), "value"));
        foreach (var entity in entities.Where(entity => CanMapSimpleEntity(entity, resolver)))
        {
            method.AddStatement(CreateProjectEntityBranch(entity, resolver));
        }

        method.AddStatement(new CustomExpression("[]").Return);
        AddSummary(method, "Projects one supported simple entity to strong physical parameters.");
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

    private static IfStatement CreateHydrateEntityBranch(
        ExpressEntityProjection entity,
        ExpressGeneratedTypeResolver resolver)
    {
        var typedName = $"typed{entity.Name}";
        var branch = new IfStatement(new CustomExpression($"value is {entity.Name} {typedName}"))
            .AddStatement(new IfStatement(new CustomExpression(
                $"components.Count != 1 || components[0].Key != \"{entity.Entity.Name.ToUpperInvariant()}\""))
            .AddStatement(new CustomExpression(
                "[new global::TedToolkit.Step21.Step21Diagnostic(\"P21-BIND-COMPONENT\", "
                + "global::TedToolkit.Step21.Step21DiagnosticSeverity.Error, "
                + $"\"Expected the single {entity.Entity.Name.ToUpperInvariant()} component.\")]").Return))
            .AddStatement(new Statement(new CustomExpression("var parameters = components[0].Value")))
            .AddStatement(new IfStatement(new CustomExpression(
                $"parameters.Count != {entity.EffectiveAttributes.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)}"))
            .AddStatement(new CustomExpression(
                "[new global::TedToolkit.Step21.Step21Diagnostic(\"P21-BIND-PARAMETER-COUNT\", "
                + "global::TedToolkit.Step21.Step21DiagnosticSeverity.Error, "
                + $"\"Expected {entity.EffectiveAttributes.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)} "
                + $"parameters for {entity.Entity.Name.ToUpperInvariant()}.\")]").Return))
            .AddStatement(new Statement(new CustomExpression(
                "var diagnostics = new global::System.Collections.Generic.List<global::TedToolkit.Step21.Step21Diagnostic>()")));
        for (var index = 0; index < entity.EffectiveAttributes.Count; index++)
        {
            branch.AddStatement(CreateHydrateAttribute(
                entity,
                entity.EffectiveAttributes[index],
                index,
                typedName,
                resolver));
        }

        branch.AddStatement(new CustomExpression("diagnostics").Return);
        return branch;
    }

    private static IfStatement CreateHydrateAttribute(
        ExpressEntityProjection entity,
        ExpressEntityAttributeProjection attribute,
        int index,
        string typedName,
        ExpressGeneratedTypeResolver resolver)
    {
        var parameterName = $"parameter{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        var expectedType = ExpressTypeDocumentation.Format(attribute.Type);
        var invalid = new CustomExpression(
            "diagnostics.Add(new global::TedToolkit.Step21.Step21Diagnostic(\"P21-BIND-PARAMETER\", "
            + "global::TedToolkit.Step21.Step21DiagnosticSeverity.Error, "
            + $"\"{entity.Entity.Name.ToUpperInvariant()} parameter "
            + $"{index.ToString(System.Globalization.CultureInfo.InvariantCulture)} ({attribute.Attribute.Name}) "
            + $"is not {expectedType}.\"))");
        var valueBranch = CreateValueHydrationBranch(
            entity,
            attribute,
            index,
            parameterName,
            typedName,
            invalid,
            resolver);
        IfStatement hydration;
        if (attribute.Attribute.IsOptional)
        {
            hydration = new IfStatement(new CustomExpression(
                    $"parameters[{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}].Kind "
                    + "== global::TedToolkit.Step21.ParameterValueKind.Omitted"))
                .AddStatement(new CustomExpression($"{typedName}.{attribute.Name} = null"))
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
            + $"\"{entity.Entity.Name.ToUpperInvariant()} parameter "
            + $"{index.ToString(System.Globalization.CultureInfo.InvariantCulture)} ({attribute.Attribute.Name}) "
            + $"contains a reference target that is not assignable to {expectedType}.\"))");
        return new IfStatement(new CustomExpression(incompatibleReference))
            .AddStatement(referenceInvalid)
            .Else()
            .AddStatement(hydration);
    }

    private static IfStatement CreateProjectEntityBranch(
        ExpressEntityProjection entity,
        ExpressGeneratedTypeResolver resolver)
    {
        var typedName = $"typed{entity.Name}";
        var parameters = entity.EffectiveAttributes.Select((attribute, index) =>
            CreateProjectedAttribute(entity, attribute, typedName, index, resolver));
        var expression =
            "[new global::System.Collections.Generic.KeyValuePair<global::System.String, "
            + "global::System.Collections.Generic.IReadOnlyList<global::TedToolkit.Step21.ParameterValue>>("
            + $"\"{entity.Entity.Name.ToUpperInvariant()}\", [{string.Join(", ", parameters)}])]";
        return new IfStatement(new CustomExpression($"value is {entity.Name} {typedName}"))
            .AddStatement(new CustomExpression(expression).Return);
    }

    private static bool CanMapSimpleEntity(
        ExpressEntityProjection entity,
        ExpressGeneratedTypeResolver resolver)
    {
        return entity.EffectiveAttributes.All(attribute => CanMapType(attribute.Type, resolver));
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

        var tryGetMethod = kind switch
        {
            ExpressScalarKind.Binary => "TryGetBinary",
            ExpressScalarKind.Integer => "TryGetInteger",
            ExpressScalarKind.Real => "TryGetReal",
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
        ExpressGeneratedTypeResolver resolver)
    {
        var value = $"{typedName}.{attribute.Name}";
        var physicalValue = attribute.Attribute.IsOptional
            && !resolver.Resolve(entity.Schema.Identity, attribute.Type).IsReferenceType
                ? $"{value}.Value"
                : value;
        var mapped = CreateProjectedValue(
            entity.Schema.Identity,
            attribute.Type,
            physicalValue,
            index,
            resolver);
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
        ExpressGeneratedTypeResolver resolver)
    {
        var target = $"{typedName}.{attribute.Name}";
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
                entity.Schema.Identity,
                namedSelect,
                select,
                index,
                parameterName,
                target,
                invalid,
                resolver);
        }

        var terminalType = GetTerminalType(attribute.Type, resolver);
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
                resolver);
        }

        if (terminalType is ExpressBoundScalarType scalar)
        {
            return CreateScalarHydrationBranch(
                scalar.Kind,
                index,
                parameterName,
                rawValue => $"{target} = {CreateReadValueExpression(entity.Schema.Identity, attribute.Type, rawValue, resolver)}",
                invalid);
        }

        var enumeration = (ExpressBoundEnumerationType)terminalType;
        var parameter = $"parameters[{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}]";
        var values = resolver.GetEnumerationValues(enumeration);
        var condition = $"{parameter}.TryGetEnumeration(out var {parameterName})";
        if (!enumeration.IsExtensible)
        {
            condition += $" && {parameterName} is {string.Join(" or ", values.Select(value => $"\"{value.ToUpperInvariant()}\""))}";
        }

        return new IfStatement(new CustomExpression(condition))
            .AddStatement(new CustomExpression(
                $"{target} = {CreateReadValueExpression(entity.Schema.Identity, attribute.Type, parameterName, resolver)}"))
            .Else()
            .AddStatement(invalid);
    }

    private static string CreateProjectedValue(
        ExpressBoundSchemaIdentity currentSchema,
        ExpressBoundType type,
        string value,
        int index,
        ExpressGeneratedTypeResolver resolver)
    {
        if (type is ExpressBoundAggregateType aggregate)
        {
            return CreateProjectedAggregate(currentSchema, aggregate, value, index, resolver);
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
                    $"global::TedToolkit.Step21.ParameterValue.FromEnumeration({value}.Value)",
                ExpressBoundSelectType select => CreateProjectedSelect(
                    currentSchema,
                    select,
                    value,
                    index,
                    resolver),
                _ => CreateProjectedValue(
                    currentSchema,
                    declaration.UnderlyingType,
                    $"{value}.Value",
                    index,
                    resolver),
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
        ExpressGeneratedTypeResolver resolver)
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
                return $"new {generatedType}({rawValue})";
            }

            return $"{rawValue} switch {{ {string.Join(", ", resolver.GetEnumerationValues(enumeration).Select(value =>
                $"\"{value.ToUpperInvariant()}\" => {generatedType}.{ExpressEntityProjection.ToPascalCase(value)}"))}, "
                + "_ => default }";
        }

        var underlyingValue = declaration.UnderlyingType is ExpressBoundAggregateType
            ? rawValue
            : CreateReadValueExpression(
            currentSchema,
            declaration.UnderlyingType,
            rawValue,
            resolver);
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
        ExpressGeneratedTypeResolver resolver)
    {
        if (!TryGetAggregateBounds(aggregate, out var lowerBound, out var upperBound))
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
                resolver);
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
                resolver);
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
                resolver);
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
                resolver);
            populated = "global::System.Linq.Enumerable.Aggregate("
                + $"{elements}, {candidate}, (aggregateCandidate, {element}) => {{ "
                + $"aggregateCandidate.Add({read}); return aggregateCandidate; }})";
        }

        return new IfStatement(new CustomExpression(condition))
            .AddStatement(new CustomExpression(
                $"{target} = {CreateReadValueExpression(currentSchema, declaredType, populated, resolver)}"))
            .Else()
            .AddStatement(invalid);
    }

    private static string CreateProjectedAggregate(
        ExpressBoundSchemaIdentity currentSchema,
        ExpressBoundAggregateType aggregate,
        string value,
        int index,
        ExpressGeneratedTypeResolver resolver)
    {
        if (!TryGetAggregateBounds(aggregate, out var lowerBound, out var upperBound))
        {
            throw new InvalidOperationException("Aggregate projection requires literal bounds.");
        }

        var suffix = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var item = $"projectedAggregateItem{suffix}";
        if (aggregate.Kind == ExpressAggregateKind.Array)
        {
            var projected = CreateProjectedValue(
                currentSchema,
                aggregate.ElementType,
                item,
                (index * 100) + 1,
                resolver);
            var count = checked(upperBound!.Value - lowerBound + 1);
            return "global::TedToolkit.Step21.ParameterValue.FromAggregate("
                + "global::System.Linq.Enumerable.Select("
                + $"global::System.Linq.Enumerable.Range({lowerBound.ToString(System.Globalization.CultureInfo.InvariantCulture)}, "
                + $"{count.ToString(System.Globalization.CultureInfo.InvariantCulture)}), projectedAggregateIndex{suffix} => "
                + $"{value}.TryGetValue(projectedAggregateIndex{suffix}, out var {item}) ? {projected} "
                + ": global::TedToolkit.Step21.ParameterValue.Omitted))";
        }

        return "global::TedToolkit.Step21.ParameterValue.FromAggregate("
            + $"global::System.Linq.Enumerable.Select({value}, {item} => "
            + $"{CreateProjectedValue(currentSchema, aggregate.ElementType, item, (index * 100) + 1, resolver)}))";
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
        ExpressGeneratedTypeResolver resolver)
    {
        if (type is ExpressBoundNamedType namedEntity
            && namedEntity.Declaration.Kind == ExpressDeclarationKind.Entity)
        {
            var entityType = GetGeneratedTypeName(currentSchema, namedEntity.Declaration);
            return $"{parameter}.TryGetEntity(out var {rawName}Entity) && {rawName}Entity is {entityType}";
        }

        var terminal = GetTerminalType(type, resolver);
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

        return CreateReadCondition(type, parameter, rawName, resolver);
    }

    private static string CreateAggregateElementReadExpression(
        ExpressBoundSchemaIdentity currentSchema,
        ExpressBoundType type,
        string parameter,
        string rawName,
        ExpressGeneratedTypeResolver resolver)
    {
        if (type is ExpressBoundNamedType namedEntity
            && namedEntity.Declaration.Kind == ExpressDeclarationKind.Entity)
        {
            var entityType = GetGeneratedTypeName(currentSchema, namedEntity.Declaration);
            return $"{parameter}.TryGetEntity(out var {rawName}Entity) && {rawName}Entity is {entityType} {rawName} "
                + $"? {rawName} : throw new global::System.InvalidOperationException()";
        }

        var terminal = GetTerminalType(type, resolver);
        if (terminal is ExpressBoundScalarType { Kind: ExpressScalarKind.Number, })
        {
            var number = $"{parameter}.TryGetInteger(out var {rawName}Integer) "
                + $"? global::TedToolkit.Step21.NumberValue.FromInteger({rawName}Integer) "
                + $": {parameter}.TryGetReal(out var {rawName}Real) "
                + $"? global::TedToolkit.Step21.NumberValue.FromReal({rawName}Real) "
                + ": throw new global::System.InvalidOperationException()";
            return CreateReadValueExpression(currentSchema, type, number, resolver);
        }

        if (terminal is ExpressBoundScalarType { Kind: ExpressScalarKind.Boolean, })
        {
            var boolean = $"{parameter}.TryGetBoolean(out var {rawName}Boolean) "
                + $"? {rawName}Boolean : {parameter}.TryGetEnumeration(out var {rawName}Symbol) "
                + $"? {rawName}Symbol == \"T\" : throw new global::System.InvalidOperationException()";
            return CreateReadValueExpression(currentSchema, type, boolean, resolver);
        }

        if (terminal is ExpressBoundScalarType { Kind: ExpressScalarKind.Logical, })
        {
            var logical = $"{parameter}.TryGetLogical(out var {rawName}Logical) "
                + $"? {rawName}Logical : {parameter}.TryGetEnumeration(out var {rawName}Symbol) "
                + $"? {rawName}Symbol == \"T\" ? global::TedToolkit.Step21.LogicalValue.True "
                + $": {rawName}Symbol == \"F\" ? global::TedToolkit.Step21.LogicalValue.False "
                + ": global::TedToolkit.Step21.LogicalValue.Unknown "
                + ": throw new global::System.InvalidOperationException()";
            return CreateReadValueExpression(currentSchema, type, logical, resolver);
        }

        var condition = CreateReadCondition(type, parameter, rawName, resolver);
        var value = CreateReadValueExpression(currentSchema, type, rawName, resolver);
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

    private static bool CanMapAggregate(
        ExpressBoundAggregateType aggregate,
        ExpressGeneratedTypeResolver resolver)
    {
        return aggregate.Kind is ExpressAggregateKind.Array
                or ExpressAggregateKind.Bag
                or ExpressAggregateKind.List
                or ExpressAggregateKind.Set
            && TryGetAggregateBounds(aggregate, out _, out _)
            && aggregate.ElementType is not ExpressBoundAggregateType
            && CanMapAggregateElement(aggregate.ElementType, resolver);
    }

    private static bool CanMapAggregateElement(
        ExpressBoundType type,
        ExpressGeneratedTypeResolver resolver)
    {
        if (type is ExpressBoundNamedType named
            && named.Declaration.Kind == ExpressDeclarationKind.Entity)
        {
            return true;
        }

        var terminal = GetTerminalType(type, resolver);
        return terminal is ExpressBoundScalarType or ExpressBoundEnumerationType;
    }

    private static bool TryGetAggregateBounds(
        ExpressBoundAggregateType aggregate,
        out int lowerBound,
        out int? upperBound)
    {
        lowerBound = 0;
        upperBound = null;
        var lowerText = aggregate.ResolvedLowerBoundText ?? aggregate.LowerBoundText;
        var upperText = aggregate.ResolvedUpperBoundText ?? aggregate.UpperBoundText;
        if (lowerText is not null
            && !int.TryParse(
                lowerText,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out lowerBound))
        {
            return false;
        }

        if (upperText is not null && upperText != "?")
        {
            if (!int.TryParse(
                upperText,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsedUpperBound))
            {
                return false;
            }

            upperBound = parsedUpperBound;
        }

        if (aggregate.Kind == ExpressAggregateKind.Array)
        {
            if (!upperBound.HasValue)
            {
                return false;
            }

            var length = (long)upperBound.Value - lowerBound + 1;
            return length is > 0 and <= int.MaxValue;
        }

        return lowerBound >= 0 && (!upperBound.HasValue || upperBound.Value >= lowerBound);
    }

    private static ExpressBoundType GetTerminalType(
        ExpressBoundType type,
        ExpressGeneratedTypeResolver resolver)
    {
        while (type is ExpressBoundNamedType named
            && named.Declaration.Kind != ExpressDeclarationKind.Entity)
        {
            var underlying = resolver.GetDefinedType(named.Declaration).UnderlyingType;
            if (underlying is ExpressBoundEnumerationType)
            {
                return underlying;
            }

            type = underlying;
        }

        return type;
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
            : "global::TedToolkit.Step21.Generated."
                + $"{ExpressEntityProjection.ToPascalCase(symbol.DeclaringSchema.Name)}.{generatedName}";
    }

    private static bool CanMapType(ExpressBoundType type, ExpressGeneratedTypeResolver resolver)
    {
        if (type is ExpressBoundNamedType namedSelect
            && namedSelect.Declaration.Kind != ExpressDeclarationKind.Entity
            && resolver.GetDefinedType(namedSelect.Declaration).UnderlyingType is ExpressBoundSelectType select)
        {
            return resolver.GetSelectAlternatives(select).All(alternative =>
                alternative.Kind == ExpressDeclarationKind.Entity
                || CanMapSelectAlternative(alternative, resolver));
        }

        var terminal = GetTerminalType(type, resolver);
        return terminal is ExpressBoundScalarType or ExpressBoundEnumerationType
            || (terminal is ExpressBoundNamedType named
                && named.Declaration.Kind == ExpressDeclarationKind.Entity)
            || (terminal is ExpressBoundAggregateType aggregate
                && CanMapAggregate(aggregate, resolver));
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
            var entityTypes = resolver.GetSelectAlternatives(select)
                .Where(alternative => alternative.Kind == ExpressDeclarationKind.Entity)
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

    private static bool CanMapSelectAlternative(
        ExpressBoundSymbol alternative,
        ExpressGeneratedTypeResolver resolver)
    {
        var terminal = GetTerminalType(
            resolver.GetDefinedType(alternative).UnderlyingType,
            resolver);
        return terminal is ExpressBoundScalarType { Kind: not ExpressScalarKind.Number, }
            or ExpressBoundEnumerationType;
    }

    private static IfStatement CreateSelectHydrationBranch(
        ExpressBoundSchemaIdentity currentSchema,
        ExpressBoundNamedType namedSelect,
        ExpressBoundSelectType select,
        int index,
        string parameterName,
        string target,
        CustomExpression invalid,
        ExpressGeneratedTypeResolver resolver)
    {
        var selectType = GetGeneratedTypeName(currentSchema, namedSelect.Declaration);
        var parameter = $"parameters[{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}]";
        IfStatement? result = null;
        foreach (var alternative in resolver.GetSelectAlternatives(select))
        {
            var alternativeName = ExpressEntityProjection.ToPascalCase(alternative.Name);
            CustomExpression condition;
            string selectedValue;
            if (alternative.Kind == ExpressDeclarationKind.Entity)
            {
                var entityType = GetGeneratedTypeName(currentSchema, alternative);
                condition = new(
                    $"{parameter}.TryGetEntity(out var {parameterName}{alternativeName}Entity) "
                    + $"&& {parameterName}{alternativeName}Entity is {entityType} {parameterName}{alternativeName}");
                selectedValue = parameterName + alternativeName;
            }
            else
            {
                var typeName = $"{parameterName}{alternativeName}Type";
                var innerName = $"{parameterName}{alternativeName}Inner";
                var rawName = $"{parameterName}{alternativeName}Raw";
                var alternativeType = CreateNamedType(alternative);
                condition = new(
                    $"{parameter}.TryGetTyped(out var {typeName}, out var {innerName}) "
                    + $"&& {typeName} == \"{alternative.Name.ToUpperInvariant()}\" "
                    + $"&& {CreateReadCondition(alternativeType, innerName, rawName, resolver)}");
                selectedValue = CreateReadValueExpression(currentSchema, alternativeType, rawName, resolver);
            }

            var assignment = new CustomExpression(
                $"{target} = {selectType}.From{alternativeName}({selectedValue})");
            if (result is null)
            {
                result = new IfStatement(condition).AddStatement(assignment);
            }
            else
            {
                result.ElseIf(condition).AddStatement(assignment);
            }
        }

        return (result ?? new(new CustomExpression("false")))
            .Else()
            .AddStatement(invalid);
    }

    private static string CreateReadCondition(
        ExpressBoundType type,
        string parameter,
        string rawName,
        ExpressGeneratedTypeResolver resolver)
    {
        var terminal = GetTerminalType(type, resolver);
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

            var method = scalar.Kind switch
            {
                ExpressScalarKind.Binary => "TryGetBinary",
                ExpressScalarKind.Integer => "TryGetInteger",
                ExpressScalarKind.Real => "TryGetReal",
                ExpressScalarKind.String => "TryGetString",
                _ => throw new InvalidOperationException("NUMBER SELECT alternatives require a later mapping branch."),
            };
            return $"{parameter}.{method}(out var {rawName})";
        }

        var enumeration = (ExpressBoundEnumerationType)terminal;
        var condition = $"{parameter}.TryGetEnumeration(out var {rawName})";
        return enumeration.IsExtensible
            ? condition
            : condition + $" && {rawName} is {string.Join(" or ", resolver.GetEnumerationValues(enumeration)
                .Select(value => $"\"{value.ToUpperInvariant()}\""))}";
    }

    private static string CreateProjectedSelect(
        ExpressBoundSchemaIdentity currentSchema,
        ExpressBoundSelectType select,
        string value,
        int index,
        ExpressGeneratedTypeResolver resolver)
    {
        var alternatives = resolver.GetSelectAlternatives(select)
            .Select((alternative, alternativeIndex) =>
            {
                var alternativeName = ExpressEntityProjection.ToPascalCase(alternative.Name);
                var selectedName = $"selected{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                    + alternativeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var projected = CreateProjectedValue(
                    currentSchema,
                    CreateNamedType(alternative),
                    selectedName,
                    (index * 100) + alternativeIndex,
                    resolver);
                if (alternative.Kind != ExpressDeclarationKind.Entity)
                {
                    projected = "global::TedToolkit.Step21.ParameterValue.FromTyped("
                        + $"\"{alternative.Name.ToUpperInvariant()}\", {projected})";
                }

                return $"{value}.TryGet{alternativeName}(out var {selectedName}) ? {projected} : ";
            });
        return string.Concat(alternatives)
            + "throw new global::System.InvalidOperationException()";
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

    private static void AddSummary(IRootDescription target, string text)
    {
        target.AddRootDescription(new DescriptionSummary(
            new IDescriptionItem[] { new DescriptionText(text), }));
    }
}