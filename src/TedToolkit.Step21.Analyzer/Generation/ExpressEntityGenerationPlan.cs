// -----------------------------------------------------------------------
// <copyright file="ExpressEntityGenerationPlan.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Validates and retains the atomic entity-generation boundary for a closed schema compilation.
/// </summary>
internal sealed class ExpressEntityGenerationPlan
{
    private ExpressEntityGenerationPlan(
        IEnumerable<ExpressEntityProjection> projections,
        IEnumerable<ExpressBoundSchema> invalidSchemas,
        IEnumerable<ExpressEntityGenerationCollision> collisions,
        IEnumerable<ExpressEntityGenerationFailure> failures)
    {
        Projections = new ReadOnlyCollection<ExpressEntityProjection>(projections.ToArray());
        InvalidSchemas = new HashSet<ExpressBoundSchema>(invalidSchemas);
        Collisions = new ReadOnlyCollection<ExpressEntityGenerationCollision>(collisions.ToArray());
        Failures = new ReadOnlyCollection<ExpressEntityGenerationFailure>(failures.ToArray());
    }

    /// <summary>
    /// Gets every entity projection in deterministic declaration order.
    /// </summary>
    internal IReadOnlyList<ExpressEntityProjection> Projections { get; }

    /// <summary>
    /// Gets schemas for which no generated artifact may be emitted.
    /// </summary>
    internal IReadOnlyCollection<ExpressBoundSchema> InvalidSchemas { get; }

    /// <summary>
    /// Gets source-located generated-name collisions.
    /// </summary>
    internal IReadOnlyList<ExpressEntityGenerationCollision> Collisions { get; }

    /// <summary>
    /// Gets source-located entity shapes that cannot be represented safely in C#.
    /// </summary>
    internal IReadOnlyList<ExpressEntityGenerationFailure> Failures { get; }

    /// <summary>
    /// Creates and validates the entity-generation plan.
    /// </summary>
    /// <param name="compilation">The valid closed schema compilation.</param>
    /// <returns>The atomic generation plan.</returns>
    internal static ExpressEntityGenerationPlan Create(ExpressSchemaCompilation compilation)
    {
        var projections = ExpressEntityProjection.Create(compilation);
        var invalidSchemas = new HashSet<ExpressBoundSchema>();
        var collisions = new List<ExpressEntityGenerationCollision>();
        var failures = new List<ExpressEntityGenerationFailure>();

        AddSchemaNameCollisions(compilation, invalidSchemas, collisions);
        foreach (var schema in compilation.Schemas)
        {
            var schemaProjections = projections.Where(projection => ReferenceEquals(projection.Schema, schema)).ToArray();
            AddTypeNameCollisions(schema, schemaProjections, invalidSchemas, collisions);
            AddMemberNameCollisions(schema, schemaProjections, invalidSchemas, collisions);
            AddUnsupportedRedeclarations(schema, schemaProjections, invalidSchemas, failures);
        }

        PropagateInvalidImports(compilation, invalidSchemas);
        return new(projections, invalidSchemas, collisions, failures);
    }

    private static void AddSchemaNameCollisions(
        ExpressSchemaCompilation compilation,
        HashSet<ExpressBoundSchema> invalidSchemas,
        List<ExpressEntityGenerationCollision> collisions)
    {
        var groups = compilation.Schemas.GroupBy(
            schema => ExpressEntityProjection.ToPascalCase(schema.Name),
            StringComparer.Ordinal);
        foreach (var group in groups.Where(group => group.Count() > 1))
        {
            foreach (var schema in group)
            {
                invalidSchemas.Add(schema);
                collisions.Add(new(
                    schema,
                    schema.Identity.Span.Start,
                    schema.Name,
                    group.Key));
            }
        }
    }

    private static void AddTypeNameCollisions(
        ExpressBoundSchema schema,
        IReadOnlyList<ExpressEntityProjection> projections,
        HashSet<ExpressBoundSchema> invalidSchemas,
        List<ExpressEntityGenerationCollision> collisions)
    {
        var generatedTypes = projections.SelectMany(projection => new[]
        {
            (Projection: projection, GeneratedName: projection.Name),
            (Projection: projection, GeneratedName: $"I{projection.Name}"),
        });
        foreach (var group in generatedTypes
                     .GroupBy(item => item.GeneratedName, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            invalidSchemas.Add(schema);
            foreach (var item in group)
            {
                collisions.Add(new(
                    schema,
                    item.Projection.Entity.Symbol.Span.Start,
                    item.Projection.Entity.Name,
                    group.Key));
            }
        }
    }

    private static void AddMemberNameCollisions(
        ExpressBoundSchema schema,
        IEnumerable<ExpressEntityProjection> projections,
        HashSet<ExpressBoundSchema> invalidSchemas,
        List<ExpressEntityGenerationCollision> collisions)
    {
        foreach (var projection in projections)
        {
            var groups = projection.FlattenedAttributes
                .GroupBy(attribute => attribute.Name, StringComparer.Ordinal)
                .Where(group => group.Count() > 1
                    || group.Key is "DirectReferences" or "ToString"
                    || StringComparer.Ordinal.Equals(group.Key, projection.Name));
            foreach (var group in groups)
            {
                invalidSchemas.Add(schema);
                foreach (var attribute in group)
                {
                    collisions.Add(new(
                        schema,
                        attribute.Attribute.Span.Start,
                        attribute.Attribute.Name,
                        group.Key));
                }
            }
        }
    }

    private static void AddUnsupportedRedeclarations(
        ExpressBoundSchema schema,
        IEnumerable<ExpressEntityProjection> projections,
        HashSet<ExpressBoundSchema> invalidSchemas,
        List<ExpressEntityGenerationFailure> failures)
    {
        var reportedAttributes = new HashSet<ExpressBoundAttribute>();
        foreach (var attribute in projections
                     .SelectMany(projection => projection.EffectiveAttributes)
                     .Where(attribute =>
                         !ReferenceEquals(attribute.TargetEntity, attribute.StorageTargetEntity)
                         && reportedAttributes.Add(attribute.Attribute)))
        {
            invalidSchemas.Add(schema);
            failures.Add(new(
                schema,
                attribute.Attribute.Span.Start,
                $"Redeclared entity attribute '{attribute.Attribute.Name}' changes its target entity type; "
                + "one C# property cannot implement both inherited interface contracts safely."));
        }
    }

    private static void PropagateInvalidImports(
        ExpressSchemaCompilation compilation,
        HashSet<ExpressBoundSchema> invalidSchemas)
    {
        bool changed;
        do
        {
            changed = false;
            var invalidIdentities = invalidSchemas.Select(schema => schema.Identity).ToArray();
            foreach (var schema in compilation.Schemas.Where(schema => !invalidSchemas.Contains(schema)))
            {
                if (schema.Imports.Any(import => invalidIdentities.Any(identity =>
                        ReferenceEquals(identity, import.Declaration.DeclaringSchema))))
                {
                    changed |= invalidSchemas.Add(schema);
                }
            }
        }
        while (changed);
    }
}