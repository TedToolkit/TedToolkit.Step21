// -----------------------------------------------------------------------
// <copyright file="ExpressSchemaMarkerEmitter.cs" company="TedToolkit">
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
/// Structurally composes the minimal compiled proof artifact for one valid schema.
/// </summary>
internal static class ExpressSchemaMarkerEmitter
{
    private const string GENERATED_NAMESPACE = "TedToolkit.Step21.Generated";

    /// <summary>
    /// Emits one path-independent schema marker through RoslynHelper.
    /// </summary>
    /// <param name="context">The source-production context.</param>
    /// <param name="schema">The valid bound schema.</param>
    internal static void Emit(in SourceProductionContext context, ExpressBoundSchema schema)
    {
        var marker = SourceComposer<ExpressIncrementalGenerator>.Class($"ExpressSchema_{schema.Name}");
        marker.Accessibility = TedToolkit.RoslynHelper.Accessibility.INTERNAL;
        marker.Polymorphism = Polymorphism.SEALED;

        var schemaName = SourceComposer<ExpressIncrementalGenerator>.Field(DataType.String, "SchemaName");
        schemaName.Accessibility = TedToolkit.RoslynHelper.Accessibility.INTERNAL;
        schemaName.IsConst = true;
        schemaName.Default = schema.Name.ToLiteral();
        marker.AddMember(schemaName);

        var sourceFile = SourceComposer.File()
            .AddNameSpace(SourceComposer.NameSpace(GENERATED_NAMESPACE).AddMember(marker));
        sourceFile.Generate(in context, $"ExpressSchema_{schema.Name.ToUpperInvariant()}");
    }
}