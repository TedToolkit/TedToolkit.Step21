// -----------------------------------------------------------------------
// <copyright file="ExpressDescriptorShards.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using TedToolkit.RoslynHelper;
using TedToolkit.RoslynHelper.Syntaxes;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Owns structural descriptor partitions that bound compiler lambda caches without rewriting generated text.
/// </summary>
/// <param name="descriptor">The owning descriptor declaration.</param>
/// <param name="enabled">Whether this descriptor needs partitions.</param>
internal sealed class ExpressDescriptorShards(TypeDeclaration descriptor, bool enabled)
{
    private readonly Dictionary<string, TypeDeclaration> _shards = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets whether this descriptor needs independent compiler-cache owners.
    /// </summary>
    internal bool IsEnabled
    {
        get
        {
            return enabled;
        }
    }

    /// <summary>
    /// Determines whether a descriptor population warrants partitioned compiler caches.
    /// </summary>
    /// <param name="entityCount">The number of generated entity projections.</param>
    /// <returns>Whether to partition this descriptor.</returns>
    internal static bool IsRequired(int entityCount)
    {
        return entityCount > 256;
    }

    /// <summary>
    /// Adds an existing structural method to the descriptor or its bounded private shard.
    /// </summary>
    /// <param name="method">The complete helper-owned method.</param>
    /// <param name="shardName">The deterministic private container identity.</param>
    internal void Add(Method method, string shardName)
    {
        if (!enabled)
        {
            descriptor.AddMember(method);
            return;
        }

        if (!_shards.TryGetValue(shardName, out var shard))
        {
            shard = SourceComposer<ExpressIncrementalGenerator>.Class(shardName);
            shard.Accessibility = TedToolkit.RoslynHelper.Accessibility.PRIVATE;
            shard.IsStatic = true;
            _shards.Add(shardName, shard);
        }

        method.Accessibility = TedToolkit.RoslynHelper.Accessibility.INTERNAL;
        shard.AddMember(method);
    }

    /// <summary>
    /// Qualifies a generated call with the same structural container used for its declaration.
    /// </summary>
    /// <param name="methodName">The method identity.</param>
    /// <param name="shardName">The private container identity.</param>
    /// <returns>The generated callable name.</returns>
    internal string Qualify(string methodName, string shardName)
    {
        return enabled ? $"{shardName}.{methodName}" : methodName;
    }

    /// <summary>
    /// Emits each structural source unit once through the approved helper.
    /// </summary>
    /// <param name="context">The generator output context.</param>
    /// <param name="generatedNamespace">The owning namespace.</param>
    /// <param name="schemaName">The normalized schema identity.</param>
    internal void Emit(in SourceProductionContext context, string generatedNamespace, string schemaName)
    {
        descriptor.IsPartial = _shards.Count > 0;
        SourceComposer.File()
            .AddNameSpace(SourceComposer.NameSpace(generatedNamespace).AddMember(descriptor))
            .Generate(context, $"ExpressSchema_{schemaName}");
        foreach (var pair in _shards.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var partial = SourceComposer<ExpressIncrementalGenerator>.Class("SchemaDescriptor");
            partial.Accessibility = TedToolkit.RoslynHelper.Accessibility.PUBLIC;
            partial.Polymorphism = Polymorphism.SEALED;
            partial.IsPartial = true;

            // The core declaration owns the generated-code attributes for this partial type.
            partial.Attributes.Clear();
            partial.AddMember(pair.Value);
            SourceComposer.File()
                .AddNameSpace(SourceComposer.NameSpace(generatedNamespace).AddMember(partial))
                .Generate(context, $"ExpressSchemaShard_{schemaName}_{pair.Key}");
        }
    }
}