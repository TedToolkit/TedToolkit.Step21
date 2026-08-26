// -----------------------------------------------------------------------
// <copyright file="ExpressEntityAttributeProjection.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Describes one generated supported explicit entity attribute.
/// </summary>
internal sealed class ExpressEntityAttributeProjection
{
    /// <summary>
    /// Initializes a projected supported explicit entity attribute.
    /// </summary>
    /// <param name="declaringEntity">The entity that declares the storage occurrence.</param>
    /// <param name="attribute">The bound attribute.</param>
    /// <param name="name">The generated property name.</param>
    /// <param name="type">The resolved supported attribute type.</param>
    /// <param name="redeclaredEntityName">The qualified redeclared entity name, when present.</param>
    /// <param name="redeclaredAttributeName">The qualified redeclared attribute name, when present.</param>
    internal ExpressEntityAttributeProjection(
        ExpressBoundEntity declaringEntity,
        ExpressBoundAttribute attribute,
        string name,
        ExpressBoundType type,
        string? redeclaredEntityName,
        string? redeclaredAttributeName)
        : this(
            declaringEntity,
            attribute,
            name,
            name,
            type,
            declaringEntity,
            attribute.Name,
            type,
            redeclaredEntityName,
            redeclaredAttributeName,
            redirectTargetName: null)
    {
    }

    private ExpressEntityAttributeProjection(
        ExpressBoundEntity declaringEntity,
        ExpressBoundAttribute attribute,
        string name,
        string storageMemberName,
        ExpressBoundType type,
        ExpressBoundEntity storageEntity,
        string storageAttributeName,
        ExpressBoundType storageType,
        string? redeclaredEntityName,
        string? redeclaredAttributeName,
        string? redirectTargetName)
    {
        DeclaringEntity = declaringEntity;
        Attribute = attribute;
        Name = name;
        StorageMemberName = storageMemberName;
        Type = type;
        StorageEntity = storageEntity;
        StorageAttributeName = storageAttributeName;
        StorageType = storageType;
        RedeclaredEntityName = redeclaredEntityName;
        RedeclaredAttributeName = redeclaredAttributeName;
        RedirectTargetName = redirectTargetName;
    }

    /// <summary>
    /// Gets the entity that declares the storage occurrence.
    /// </summary>
    internal ExpressBoundEntity DeclaringEntity { get; }

    /// <summary>
    /// Gets the bound attribute.
    /// </summary>
    internal ExpressBoundAttribute Attribute { get; }

    /// <summary>
    /// Gets the generated property name.
    /// </summary>
    internal string Name { get; }

    /// <summary>
    /// Gets the generated class member that stores this physical occurrence.
    /// </summary>
    internal string StorageMemberName { get; }

    /// <summary>
    /// Gets the resolved supported attribute type.
    /// </summary>
    internal ExpressBoundType Type { get; }

    /// <summary>
    /// Gets the resolved target entity for this declaration, or <see langword="null"/> for another value type.
    /// </summary>
    internal ExpressBoundSymbol? TargetEntity
    {
        get
        {
            return (Type as ExpressBoundNamedType)?.Declaration is { Kind: ExpressDeclarationKind.Entity, } entity
                ? entity
                : null;
        }
    }

    /// <summary>
    /// Gets the entity that owns the physical storage slot.
    /// </summary>
    internal ExpressBoundEntity StorageEntity { get; }

    /// <summary>
    /// Gets the source attribute name that identifies the physical storage slot.
    /// </summary>
    internal string StorageAttributeName { get; }

    /// <summary>
    /// Gets the generated type originally declared for the physical storage slot.
    /// </summary>
    internal ExpressBoundType StorageType { get; }

    /// <summary>
    /// Gets the qualified redeclared entity name, when present.
    /// </summary>
    internal string? RedeclaredEntityName { get; }

    /// <summary>
    /// Gets the qualified redeclared attribute name, when present.
    /// </summary>
    internal string? RedeclaredAttributeName { get; }

    /// <summary>
    /// Gets the inherited property to which this renamed property forwards.
    /// </summary>
    internal string? RedirectTargetName { get; }

    /// <summary>
    /// Binds a redeclaration to its inherited physical storage slot.
    /// </summary>
    /// <param name="inheritedAttribute">The inherited physical storage slot.</param>
    /// <param name="redirectTargetName">The inherited property used by a renamed alias.</param>
    /// <returns>A final projection bound to the inherited physical storage slot.</returns>
    internal ExpressEntityAttributeProjection WithStorage(
        ExpressEntityAttributeProjection inheritedAttribute,
        string? redirectTargetName)
    {
        return new(
            DeclaringEntity,
            Attribute,
            Name,
            StorageMemberName,
            Type,
            inheritedAttribute.StorageEntity,
            inheritedAttribute.StorageAttributeName,
            inheritedAttribute.StorageType,
            RedeclaredEntityName,
            RedeclaredAttributeName,
            redirectTargetName);
    }

    /// <summary>
    /// Disambiguates this physical occurrence from another inherited occurrence with the same public interface name.
    /// </summary>
    /// <returns>A final projection with an unambiguous storage member name.</returns>
    internal ExpressEntityAttributeProjection WithDisambiguatedStorageMember()
    {
        return new(
            DeclaringEntity,
            Attribute,
            Name,
            ExpressEntityProjection.ToPascalCase(StorageEntity.Name) + Name,
            Type,
            StorageEntity,
            StorageAttributeName,
            StorageType,
            RedeclaredEntityName,
            RedeclaredAttributeName,
            RedirectTargetName);
    }
}