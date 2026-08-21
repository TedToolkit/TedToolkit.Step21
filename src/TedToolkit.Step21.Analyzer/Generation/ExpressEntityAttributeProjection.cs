// -----------------------------------------------------------------------
// <copyright file="ExpressEntityAttributeProjection.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Describes one generated direct entity attribute.
/// </summary>
internal sealed class ExpressEntityAttributeProjection
{
    /// <summary>
    /// Initializes a projected direct entity attribute.
    /// </summary>
    /// <param name="declaringEntity">The entity that declares the storage occurrence.</param>
    /// <param name="attribute">The bound attribute.</param>
    /// <param name="name">The generated property name.</param>
    /// <param name="interfaceName">The generated entity-interface type name.</param>
    /// <param name="targetEntity">The resolved target entity.</param>
    /// <param name="targetSchema">The schema that declares the target entity.</param>
    /// <param name="redeclaredEntityName">The qualified redeclared entity name, when present.</param>
    /// <param name="redeclaredAttributeName">The qualified redeclared attribute name, when present.</param>
    internal ExpressEntityAttributeProjection(
        ExpressBoundEntity declaringEntity,
        ExpressBoundAttribute attribute,
        string name,
        string interfaceName,
        ExpressBoundSymbol targetEntity,
        ExpressBoundSchemaIdentity targetSchema,
        string? redeclaredEntityName,
        string? redeclaredAttributeName)
    {
        DeclaringEntity = declaringEntity;
        Attribute = attribute;
        Name = name;
        InterfaceName = interfaceName;
        TargetEntity = targetEntity;
        TargetSchema = targetSchema;
        StorageEntity = declaringEntity;
        StorageAttributeName = attribute.Name;
        StorageTargetEntity = targetEntity;
        RedeclaredEntityName = redeclaredEntityName;
        RedeclaredAttributeName = redeclaredAttributeName;
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
    /// Gets the generated target interface name.
    /// </summary>
    internal string InterfaceName { get; }

    /// <summary>
    /// Gets the resolved target entity for this declaration.
    /// </summary>
    internal ExpressBoundSymbol TargetEntity { get; }

    /// <summary>
    /// Gets the target entity's declaring schema.
    /// </summary>
    internal ExpressBoundSchemaIdentity TargetSchema { get; }

    /// <summary>
    /// Gets the entity that owns the physical storage slot.
    /// </summary>
    internal ExpressBoundEntity StorageEntity { get; private set; }

    /// <summary>
    /// Gets the source attribute name that identifies the physical storage slot.
    /// </summary>
    internal string StorageAttributeName { get; private set; }

    /// <summary>
    /// Gets the target entity originally declared for the physical storage slot.
    /// </summary>
    internal ExpressBoundSymbol StorageTargetEntity { get; private set; }

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
    internal string? RedirectTargetName { get; private set; }

    /// <summary>
    /// Binds a redeclaration to its inherited physical storage slot.
    /// </summary>
    /// <param name="inheritedAttribute">The inherited physical storage slot.</param>
    /// <param name="redirectTargetName">The inherited property used by a renamed alias.</param>
    internal void BindStorage(
        ExpressEntityAttributeProjection inheritedAttribute,
        string? redirectTargetName)
    {
        StorageEntity = inheritedAttribute.StorageEntity;
        StorageAttributeName = inheritedAttribute.StorageAttributeName;
        StorageTargetEntity = inheritedAttribute.StorageTargetEntity;
        RedirectTargetName = redirectTargetName;
    }
}