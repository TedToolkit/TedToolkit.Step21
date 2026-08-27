// -----------------------------------------------------------------------
// <copyright file="ExpressEntityAttributeAdapter.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Represents one inherited interface getter projected from the most-specific physical storage.
/// </summary>
internal sealed class ExpressEntityAttributeAdapter
{
    /// <summary>
    /// Initializes an inherited interface getter projection.
    /// </summary>
    /// <param name="interfaceAttribute">The declaration that owns the interface contract.</param>
    /// <param name="storageAttribute">The most-specific declaration that owns mutable storage.</param>
    internal ExpressEntityAttributeAdapter(
        ExpressEntityAttributeProjection interfaceAttribute,
        ExpressEntityAttributeProjection storageAttribute)
    {
        InterfaceAttribute = interfaceAttribute;
        StorageAttribute = storageAttribute;
    }

    /// <summary>
    /// Gets the declaration that owns the interface contract.
    /// </summary>
    internal ExpressEntityAttributeProjection InterfaceAttribute { get; }

    /// <summary>
    /// Gets the most-specific declaration that owns mutable storage.
    /// </summary>
    internal ExpressEntityAttributeProjection StorageAttribute { get; }

    /// <summary>
    /// Retargets the getter to a more-specific storage declaration.
    /// </summary>
    /// <param name="storageAttribute">The new most-specific storage declaration.</param>
    /// <returns>The retargeted getter projection.</returns>
    internal ExpressEntityAttributeAdapter WithStorage(ExpressEntityAttributeProjection storageAttribute)
    {
        return new(InterfaceAttribute, storageAttribute);
    }
}