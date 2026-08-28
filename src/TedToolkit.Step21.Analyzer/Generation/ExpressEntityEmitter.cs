// -----------------------------------------------------------------------
// <copyright file="ExpressEntityEmitter.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using TedToolkit.RoslynHelper;
using TedToolkit.RoslynHelper.Syntaxes;

using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Structurally composes one generated mutable EXPRESS entity API.
/// </summary>
internal static class ExpressEntityEmitter
{
    private static readonly DataType _entityType = new("global::TedToolkit.Step21.Entity");

    private static readonly DataType _directReferencesType = new(
        "global::System.Collections.Generic.IEnumerable<global::TedToolkit.Step21.Entity>");

    /// <summary>
    /// Emits one interface and mutable class for a projected EXPRESS entity.
    /// </summary>
    /// <param name="context">The source-production context.</param>
    /// <param name="projection">The projected entity.</param>
    /// <param name="valueResolver">The closed-set generated type resolver.</param>
    internal static void Emit(
        in SourceProductionContext context,
        ExpressEntityProjection projection,
        ExpressGeneratedTypeResolver valueResolver)
    {
        var entityInterface = CreateInterface(projection, valueResolver);
        var entityClass = CreateClass(projection, valueResolver);
        var generatedNamespace = $"TedToolkit.Step21.Generated.{ExpressEntityProjection.ToPascalCase(projection.Schema.Name)}";
        var sourceFile = SourceComposer.File()
            .AddNameSpace(SourceComposer.NameSpace(generatedNamespace)
                .AddMember(entityInterface)
                .AddMember(entityClass));

        sourceFile.Generate(
            in context,
            $"ExpressEntity_{projection.Schema.Name.ToUpperInvariant()}_{projection.Entity.Name.ToUpperInvariant()}");
    }

    private static TypeDeclaration CreateInterface(
        ExpressEntityProjection projection,
        ExpressGeneratedTypeResolver valueResolver)
    {
        var entityInterface = SourceComposer<ExpressIncrementalGenerator>.Interface($"I{projection.Name}");
        entityInterface.Accessibility = TedToolkit.RoslynHelper.Accessibility.PUBLIC;
        AddSummary(entityInterface, $"Represents the EXPRESS entity {projection.Entity.Name}.");
        ExpressStructuralValidationEmitter.AddTypeDocumentation(
            entityInterface,
            projection.Schema.Name,
            $"ENTITY {projection.Entity.Name}");

        foreach (var supertype in projection.Entity.DirectSupertypes)
        {
            entityInterface.AddBaseType(EntityInterfaceDataType(projection, supertype));
        }

        foreach (var attribute in projection.OwnAttributes)
        {
            entityInterface.AddMember(CreateProperty(projection, attribute, valueResolver, isMutable: false));
        }

        return entityInterface;
    }

    private static TypeDeclaration CreateClass(
        ExpressEntityProjection projection,
        ExpressGeneratedTypeResolver valueResolver)
    {
        var entityClass = SourceComposer<ExpressIncrementalGenerator>.Class(projection.Name);
        entityClass.Accessibility = TedToolkit.RoslynHelper.Accessibility.PUBLIC;
        entityClass.Polymorphism = projection.Entity.IsAbstract
            ? Polymorphism.ABSTRACT
            : Polymorphism.SEALED;
        entityClass.AddBaseType(_entityType)
            .AddBaseType(new DataType($"I{projection.Name}"));
        AddSummary(entityClass, $"Provides mutable storage for the EXPRESS entity {projection.Entity.Name}.");
        ExpressStructuralValidationEmitter.AddTypeDocumentation(
            entityClass,
            projection.Schema.Name,
            $"ENTITY {projection.Entity.Name}");

        foreach (var attribute in projection.FlattenedAttributes)
        {
            entityClass.AddMember(CreateProperty(
                projection,
                attribute,
                valueResolver,
                isMutable: true,
                generatedName: attribute.StorageMemberName));
            if (!StringComparer.Ordinal.Equals(attribute.Name, attribute.StorageMemberName)
                || valueResolver.RequiresAggregateView(attribute.Attribute))
            {
                entityClass.AddMember(CreateExplicitInterfaceGetter(
                    projection,
                    attribute,
                    attribute,
                    valueResolver));
            }
        }

        foreach (var adapter in projection.InterfaceAdapters)
        {
            entityClass.AddMember(CreateExplicitInterfaceGetter(
                projection,
                adapter.InterfaceAttribute,
                adapter.StorageAttribute,
                valueResolver));
        }

        entityClass.AddMember(CreateConstructor(projection, valueResolver));
        entityClass.AddMember(CreateDirectReferencesProperty(projection, valueResolver));
        entityClass.AddMember(CreateToStringMethod(projection));
        return entityClass;
    }

    /// <summary>
    /// Creates one generated entity property.
    /// </summary>
    /// <param name="projection">The owning entity projection.</param>
    /// <param name="attribute">The projected attribute.</param>
    /// <param name="valueResolver">The closed-set generated value resolver.</param>
    /// <param name="isMutable">Whether the property is mutable.</param>
    /// <param name="initializeDefault">Whether a mandatory property receives a null-forgiving default initializer.</param>
    /// <param name="generatedName">The generated class member name, or <see langword="null"/> for the interface name.</param>
    /// <returns>The composed property.</returns>
    internal static Property CreateProperty(
        ExpressEntityProjection projection,
        ExpressEntityAttributeProjection attribute,
        ExpressGeneratedTypeResolver valueResolver,
        bool isMutable,
        bool initializeDefault = false,
        string? generatedName = null)
    {
        var (dataType, isReferenceType) = AttributeDataType(
            projection,
            attribute,
            valueResolver,
            useInterfaceContract: !isMutable);
        if (attribute.Attribute.IsOptional)
        {
            dataType = dataType.Null;
        }

        var property = SourceComposer<ExpressIncrementalGenerator>.Property(
            dataType,
            generatedName ?? attribute.Name);
        property.Accessibility = TedToolkit.RoslynHelper.Accessibility.PUBLIC;
        if (isReferenceType)
        {
            AddNullabilityAttributes(property, attribute.Attribute.IsOptional);
        }

        string summary;
        if (isMutable && attribute.RedirectTargetName is not null)
        {
            var redirectTargetName = projection.FlattenedAttributes
                .First(candidate => StringComparer.Ordinal.Equals(candidate.Name, attribute.RedirectTargetName)
                    && ReferenceEquals(candidate.StorageEntity, attribute.StorageEntity)
                    && StringComparer.OrdinalIgnoreCase.Equals(
                        candidate.StorageAttributeName,
                        attribute.StorageAttributeName))
                .StorageMemberName;
            var getter = SourceComposer<ExpressIncrementalGenerator>.Accessor(AccessorType.GET)
                .AddStatement(redirectTargetName.ToSimpleName().Return);
            var setter = SourceComposer<ExpressIncrementalGenerator>.Accessor(AccessorType.SET)
                .AddStatement(redirectTargetName.ToSimpleName().Assign("value".ToSimpleName()));
            property.AddAccessor(getter)
                .AddAccessor(setter);
            summary = $"Gets or sets the {attribute.Attribute.Name} attribute.";
        }
        else
        {
            property.AddAccessor(SourceComposer<ExpressIncrementalGenerator>.Accessor(AccessorType.GET));
            if (isMutable)
            {
                property.AddAccessor(SourceComposer<ExpressIncrementalGenerator>.Accessor(AccessorType.SET));
                summary = $"Gets or sets the {attribute.Attribute.Name} attribute.";
            }
            else
            {
                summary = $"Gets the {attribute.Attribute.Name} attribute.";
            }
        }

        if (attribute.Type is ExpressBoundAggregateType aggregate)
        {
            summary += $" Its exact aggregate form is {ExpressTypeDocumentation.Format(aggregate)}."
                + " Mutations do not run schema validation; explicit and boundary validation observe the current candidate.";
        }

        AddSummary(property, summary);
        ExpressStructuralValidationEmitter.AddPropertyDocumentation(
            property,
            projection,
            attribute,
            valueResolver,
            isMutable);

        if (initializeDefault
            && !attribute.Attribute.IsOptional
            && attribute.RedirectTargetName is null)
        {
            property.AddDefault(new CustomExpression("default!"));
        }

        return property;
    }

    private static Constructor CreateConstructor(
        ExpressEntityProjection projection,
        ExpressGeneratedTypeResolver valueResolver)
    {
        var constructor = SourceComposer<ExpressIncrementalGenerator>.Constructor();
        constructor.Accessibility = projection.Entity.IsAbstract
            ? TedToolkit.RoslynHelper.Accessibility.PROTECTED
            : TedToolkit.RoslynHelper.Accessibility.PUBLIC;
        AddSummary(constructor, $"Initializes a new {projection.Name} entity.");

        foreach (var attribute in projection.EffectiveAttributes.Where(attribute => !attribute.Attribute.IsOptional))
        {
            var parameterName = char.ToLowerInvariant(attribute.StorageMemberName[0])
                + attribute.StorageMemberName.Substring(1);
            var parameterIdentifier = EscapeIdentifier(parameterName);
            var (parameterType, isReferenceType) = AttributeDataType(projection, attribute, valueResolver);
            var parameter = SourceComposer.Parameter(parameterType, parameterIdentifier);
            if (isReferenceType)
            {
                parameter.AddAttribute(SourceComposer.Attribute(new DataType(
                    "global::System.Diagnostics.CodeAnalysis.DisallowNullAttribute")));
            }

            constructor.AddParameter(parameter);
            constructor.AddStatement(
                attribute.StorageMemberName.ToSimpleName().Assign(parameterIdentifier.ToSimpleName()));
            constructor.AddRootDescription(new DescriptionParam(
                parameterName,
                new IDescriptionItem[] { new DescriptionText($"The {attribute.Attribute.Name} attribute."), }));
        }

        return constructor;
    }

    private static Custom CreateExplicitInterfaceGetter(
        ExpressEntityProjection projection,
        ExpressEntityAttributeProjection interfaceAttribute,
        ExpressEntityAttributeProjection storageAttribute,
        ExpressGeneratedTypeResolver valueResolver)
    {
        var (dataType, _) = AttributeDataType(
            projection,
            interfaceAttribute,
            valueResolver,
            useInterfaceContract: true);
        if (interfaceAttribute.Attribute.IsOptional)
        {
            dataType = dataType.Null;
        }

        var interfaceType = EntityInterfaceDataType(projection, interfaceAttribute.DeclaringEntity.Symbol);
        return new((ref SourceBuilder builder) =>
        {
            dataType.ToCode(ref builder);
            builder.AppendSpace();
            interfaceType.ToCode(ref builder);
            var storageExpression = storageAttribute.StorageMemberName;
            var projectionExpression = storageExpression;
            if (interfaceAttribute.Attribute.IsOptional && storageAttribute.Attribute.IsOptional)
            {
                projectionExpression = "__specializedValue";
            }

            var expression = valueResolver.CreateSpecializationProjection(
                projection.Schema.Identity,
                storageAttribute.Type,
                interfaceAttribute.Type,
                projectionExpression);
            if (interfaceAttribute.Attribute.IsOptional && storageAttribute.Attribute.IsOptional)
            {
                expression = $"{storageExpression} is {{ }} __specializedValue ? {expression} : null";
            }

            builder.Append($".{interfaceAttribute.Name} => {expression};");
        });
    }

    private static string EscapeIdentifier(string identifier)
    {
        return SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None
            ? identifier
            : $"@{identifier}";
    }

    private static Property CreateDirectReferencesProperty(
        ExpressEntityProjection projection,
        ExpressGeneratedTypeResolver valueResolver)
    {
        var property = SourceComposer<ExpressIncrementalGenerator>.Property(_directReferencesType, "DirectReferences");
        property.Accessibility = TedToolkit.RoslynHelper.Accessibility.PUBLIC;
        property.Polymorphism = Polymorphism.OVERRIDE;
        AddSummary(
            property,
            "Gets a live one-level enumeration of non-null direct entity occurrences in physical attribute order, "
            + "preserving repetitions without recursive entity traversal.");

        var getter = SourceComposer<ExpressIncrementalGenerator>.Accessor(AccessorType.GET);
        getter.AddStatement(new CustomExpression(
            ExpressDirectReferenceExpression.Create(projection, valueResolver)).Return);
        property.AddAccessor(getter);
        return property;
    }

    private static Method CreateToStringMethod(ExpressEntityProjection projection)
    {
        var method = SourceComposer<ExpressIncrementalGenerator>.Method(
            "ToString",
            SourceComposer.ReturnType(DataType.String));
        method.Accessibility = TedToolkit.RoslynHelper.Accessibility.PUBLIC;
        method.Polymorphism = Polymorphism.OVERRIDE;
        method.AddStatement($"{projection.Schema.Name}.{projection.Entity.Name}".ToLiteral().Return);
        AddSummary(method, "Returns a bounded schema and entity diagnostic name.");
        method.AddRootDescription(new DescriptionReturns(
            new IDescriptionItem[] { new DescriptionText("The schema and entity diagnostic name."), }));
        return method;
    }

    private static (DataType DataType, bool IsReferenceType) AttributeDataType(
        ExpressEntityProjection projection,
        ExpressEntityAttributeProjection attribute,
        ExpressGeneratedTypeResolver valueResolver,
        bool useInterfaceContract = false)
    {
        if (useInterfaceContract
            && attribute.Type is ExpressBoundAggregateType aggregate
            && valueResolver.RequiresAggregateView(attribute.Attribute))
        {
            return (valueResolver.ResolveAggregateView(projection.Schema.Identity, aggregate), true);
        }

        return valueResolver.Resolve(projection.Schema.Identity, attribute.Type);
    }

    /// <summary>
    /// Resolves the generated interface type for an entity symbol.
    /// </summary>
    /// <param name="projection">The consuming entity projection.</param>
    /// <param name="entity">The entity symbol.</param>
    /// <returns>The generated interface type.</returns>
    internal static DataType EntityInterfaceDataType(
        ExpressEntityProjection projection,
        ExpressBoundSymbol entity)
    {
        var interfaceName = $"I{ExpressEntityProjection.ToPascalCase(entity.Name)}";
        if (ReferenceEquals(projection.Schema.Identity, entity.DeclaringSchema))
        {
            return new(interfaceName);
        }

        var schemaName = ExpressEntityProjection.ToPascalCase(entity.DeclaringSchema.Name);
        return new($"global::TedToolkit.Step21.Generated.{schemaName}.{interfaceName}");
    }

    private static void AddNullabilityAttributes(Property property, bool isOptional)
    {
        if (isOptional)
        {
            property.AddAttribute(SourceComposer.Attribute(new DataType(
                "global::System.Diagnostics.CodeAnalysis.MaybeNullAttribute")));
            property.AddAttribute(SourceComposer.Attribute(new DataType(
                "global::System.Diagnostics.CodeAnalysis.AllowNullAttribute")));
            return;
        }

        property.AddAttribute(SourceComposer.Attribute(new DataType(
            "global::System.Diagnostics.CodeAnalysis.NotNullAttribute")));
        property.AddAttribute(SourceComposer.Attribute(new DataType(
            "global::System.Diagnostics.CodeAnalysis.DisallowNullAttribute")));
    }

    private static void AddSummary(IRootDescription target, string text)
    {
        target.AddRootDescription(new DescriptionSummary(
            new IDescriptionItem[] { new DescriptionText(text), }));
    }
}