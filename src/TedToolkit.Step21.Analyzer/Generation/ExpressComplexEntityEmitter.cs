// -----------------------------------------------------------------------
// <copyright file="ExpressComplexEntityEmitter.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.RoslynHelper;
using TedToolkit.RoslynHelper.Syntaxes;
using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Emits internal strongly typed storage for a multi-leaf complex entity value.
/// </summary>
internal static class ExpressComplexEntityEmitter
{
    private static readonly DataType _entityType = new("global::TedToolkit.Step21.Entity");

    private static readonly DataType _directReferencesType = new(
        "global::System.Collections.Generic.IEnumerable<global::TedToolkit.Step21.Entity>");

    /// <summary>
    /// Emits one internal synthetic complex entity class.
    /// </summary>
    /// <param name="context">The source-production context.</param>
    /// <param name="projection">The synthetic complex entity projection.</param>
    /// <param name="resolver">The closed-set generated value resolver.</param>
    internal static void Emit(
        in Microsoft.CodeAnalysis.SourceProductionContext context,
        ExpressComplexEntityProjection projection,
        ExpressGeneratedTypeResolver resolver)
    {
        var type = SourceComposer<ExpressIncrementalGenerator>.Class(projection.Name);
        type.Accessibility = TedToolkit.RoslynHelper.Accessibility.INTERNAL;
        type.Polymorphism = Polymorphism.SEALED;
        type.AddBaseType(_entityType);
        foreach (var leaf in projection.Leaves)
        {
            type.AddBaseType(ExpressEntityEmitter.EntityInterfaceDataType(leaf, leaf.Entity.Symbol));
        }

        var contextProjection = projection.Leaves[0];
        var explicitAttributes = new HashSet<ExpressBoundAttribute>();
        foreach (var attribute in projection.Properties)
        {
            type.AddMember(ExpressEntityEmitter.CreateProperty(
                contextProjection,
                attribute,
                resolver,
                isMutable: true,
                initializeDefault: true,
                generatedName: attribute.StorageMemberName));
            if ((!StringComparer.Ordinal.Equals(attribute.Name, attribute.StorageMemberName)
                    || resolver.RequiresAggregateView(attribute.Attribute))
                && explicitAttributes.Add(attribute.Attribute))
            {
                type.AddMember(ExpressEntityEmitter.CreateExplicitInterfaceGetter(
                    contextProjection,
                    attribute,
                    attribute,
                    resolver));
            }
        }

        foreach (var adapter in projection.Leaves.SelectMany(leaf => leaf.InterfaceAdapters))
        {
            if (!explicitAttributes.Add(adapter.InterfaceAttribute.Attribute))
            {
                continue;
            }

            var storageAttribute = projection.Properties.Where(attribute =>
                    ReferenceEquals(attribute.StorageEntity.Symbol, adapter.StorageAttribute.StorageEntity.Symbol)
                    && StringComparer.OrdinalIgnoreCase.Equals(
                        attribute.StorageAttributeName,
                        adapter.StorageAttribute.StorageAttributeName))
                .OrderBy(attribute => attribute.RedirectTargetName is null ? 0 : 1)
                .First();
            type.AddMember(ExpressEntityEmitter.CreateExplicitInterfaceGetter(
                contextProjection,
                adapter.InterfaceAttribute,
                storageAttribute,
                resolver));
        }

        var constructor = SourceComposer<ExpressIncrementalGenerator>.Constructor();
        constructor.Accessibility = TedToolkit.RoslynHelper.Accessibility.INTERNAL;
        type.AddMember(constructor);
        type.AddMember(CreateDirectReferencesProperty(projection, resolver));
        var generatedNamespace = $"TedToolkit.Step21.Schemas.{ExpressEntityProjection.ToPascalCase(projection.Schema.Name)}";
        SourceComposer.File()
            .AddNameSpace(SourceComposer.NameSpace(generatedNamespace).AddMember(type))
            .Generate(in context, $"ExpressComplex_{projection.Schema.Name.ToUpperInvariant()}_{projection.Name}");
    }

    private static Property CreateDirectReferencesProperty(
        ExpressComplexEntityProjection projection,
        ExpressGeneratedTypeResolver resolver)
    {
        var property = SourceComposer<ExpressIncrementalGenerator>.Property(_directReferencesType, "DirectReferences");
        property.Accessibility = TedToolkit.RoslynHelper.Accessibility.PUBLIC;
        property.Polymorphism = Polymorphism.OVERRIDE;
        var getter = SourceComposer<ExpressIncrementalGenerator>.Accessor(AccessorType.GET);
        getter.AddStatement(new CustomExpression(
            ExpressDirectReferenceExpression.Create(projection.Properties, resolver)).Return);
        property.AddAccessor(getter);
        return property;
    }
}