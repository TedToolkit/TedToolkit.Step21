// -----------------------------------------------------------------------
// <copyright file="GeneratedFidelityTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Analyzer.Express.Binding;
using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>
/// Proves the pinned AP203 schema generates its representative public contract.
/// </summary>
public sealed class GeneratedFidelityTests
{
    private const string AP203_SHA256 = "255EAFFD5984373F5FE2F41369088B6FD07F970EB5915CE9920F0A5F339DDD44";
    private const string SchemaNamespace =
        "Ap203ConfigurationControlled3dDesignOfMechanicalPartsAndAssembliesMimLf";

    /// <summary>
    /// Verifies the official AP203 source checksum, descriptor, inheritance, nullability, and aggregates.
    /// </summary>
    [Test]
    public async Task Should_generate_pinned_ap203_surface_from_verified_schema()
    {
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "TestData", "Express", "Ap203", "mim_lf.exp");
        await Assert.That(File.Exists(schemaPath)).IsTrue();
        var schemaText = File.ReadAllText(schemaPath);
        foreach (var lineEnding in new[] { "\n", "\r\n", })
        {
            var variant = schemaText.ReplaceLineEndings(lineEnding);
            var canonicalBytes = Encoding.UTF8.GetBytes(variant.ReplaceLineEndings("\n"));
            await Assert.That(Convert.ToHexString(SHA256.HashData(canonicalBytes)))
                .IsEqualTo(AP203_SHA256);
        }

        var analyzed = ExpressSchemaCompiler.Analyze(
            [new ExpressSchemaSource("schemas/.cache/ap203/mim_lf.exp", schemaText)]);
        var bound = analyzed.Compilation;
        var boundExpressions = bound.Schemas.Single().Expressions.Select(expression => expression.Span).ToArray();
        var schema = bound.Schemas.Single();
        var missingRuleExpressions = schema.Declarations
            .OfType<ExpressBoundDefinedType>()
            .SelectMany(type => analyzed.GetAnalysis(schema).GetDeclaration(type).ChildRules("whereClause"))
            .SelectMany(clause => clause.ChildRules("domainRule"))
            .Select(rule => rule.RequiredChild("expression"))
            .Where(expression => !boundExpressions.Any(span =>
                span.Start.FilePath == expression.Span.Start.FilePath
                && span.Start.Line == expression.Span.Start.Line
                && span.Start.Column == expression.Span.Start.Column
                && span.End.Line == expression.Span.End.Line
                && span.End.Column == expression.Span.End.Column))
            .ToArray();
        await Assert.That(missingRuleExpressions).IsEmpty()
            .Because(string.Join(Environment.NewLine, missingRuleExpressions.Select(expression =>
                $"{expression.Span.Start.FilePath}:{expression.Span.Start.Line}:{expression.Span.Start.Column}")));

        var result = GeneratorHostTests.Run(("schemas/.cache/ap203/mim_lf.exp", schemaText));
        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error))
                .IsEmpty()
                .Because(string.Join(Environment.NewLine, result.Diagnostics
                    .GroupBy(diagnostic => (diagnostic.Id, diagnostic.GetMessage()))
                    .OrderBy(group => group.Key.Id, StringComparer.Ordinal)
                    .ThenBy(group => group.Key.Item2, StringComparer.Ordinal)
                    .Select(group => $"{group.Count()} x {group.Key.Id}: {group.Key.Item2}")));
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error))
                .IsEmpty();
        }

        var descriptor = result.OutputCompilation.GetTypeByMetadataName(
            $"TedToolkit.Step21.Schemas.{SchemaNamespace}.SchemaDescriptor")
            ?? throw new InvalidOperationException("The generated AP203 descriptor was not found.");
        var advancedFace = result.OutputCompilation.GetTypeByMetadataName(
            $"TedToolkit.Step21.Schemas.{SchemaNamespace}.IAdvancedFace")
            ?? throw new InvalidOperationException("The generated AP203 advanced_face interface was not found.");
        var product = result.OutputCompilation.GetTypeByMetadataName(
            $"TedToolkit.Step21.Schemas.{SchemaNamespace}.Product")
            ?? throw new InvalidOperationException("The generated AP203 product entity was not found.");
        var productCategory = result.OutputCompilation.GetTypeByMetadataName(
            $"TedToolkit.Step21.Schemas.{SchemaNamespace}.ProductCategory")
            ?? throw new InvalidOperationException("The generated AP203 product_category entity was not found.");
        var cartesianPoint = result.OutputCompilation.GetTypeByMetadataName(
            $"TedToolkit.Step21.Schemas.{SchemaNamespace}.CartesianPoint")
            ?? throw new InvalidOperationException("The generated AP203 cartesian_point entity was not found.");
        var descriptorSource = result.GeneratedSources.Single(source =>
            source.HintName ==
                "ExpressSchema_AP203_CONFIGURATION_CONTROLLED_3D_DESIGN_OF_MECHANICAL_PARTS_AND_ASSEMBLIES_MIM_LF.g.cs")
            .SourceText.ToString();

        using (Assert.Multiple())
        {
            await Assert.That(descriptorSource).Contains(
                "SchemaName(\"Ap203_configuration_controlled_3d_design_of_mechanical_parts_and_assemblies_mim_lf\")");
            await Assert.That(descriptor.GetMembers("Name").OfType<IPropertySymbol>().Single().Type.Name)
                .IsEqualTo("SchemaName");
            await Assert.That(advancedFace.Interfaces.Select(type => type.Name)).Contains("IFaceSurface");
            await Assert.That(product.GetMembers("FrameOfReference").OfType<IPropertySymbol>().Single().Type.Name)
                .IsEqualTo("ExpressSet");
            await Assert.That(productCategory.GetMembers("Description").OfType<IPropertySymbol>().Single().Type.NullableAnnotation)
                .IsEqualTo(NullableAnnotation.Annotated);
            await Assert.That(cartesianPoint.GetMembers("Coordinates").OfType<IPropertySymbol>().Single().Type.Name)
                .IsEqualTo("ExpressList");
        }
    }
}