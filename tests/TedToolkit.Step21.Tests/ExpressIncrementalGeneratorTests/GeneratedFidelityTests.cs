// -----------------------------------------------------------------------
// <copyright file="GeneratedFidelityTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Security.Cryptography;

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Analyzer.Express.Binding;
using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>
/// Proves the pinned AP203 schema generates its representative public contract.
/// </summary>
public sealed class GeneratedFidelityTests
{
    private const string AP203_SHA256 = "19497DCA88C6FCFE763DA23772B68356BE4361668426954DE9863E4285D0C251";

    /// <summary>
    /// Verifies the checked-in AP203 source checksum, descriptor, inheritance, nullability, aggregates, values, and member order.
    /// </summary>
    [Test]
    public async Task Should_generate_pinned_ap203_surface_from_checked_in_schema()
    {
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "TestData", "Express", "Ap203", "ap203.exp");
        await Assert.That(File.Exists(schemaPath)).IsTrue();
        await Assert.That(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(schemaPath))))
            .IsEqualTo(AP203_SHA256);

        var schemaText = File.ReadAllText(schemaPath);
        var bound = ExpressSchemaCompiler.Compile([new ExpressSchemaSource("schemas/ap203.exp", schemaText)]);
        var boundExpressions = bound.Schemas.Single().Expressions.Select(expression => expression.Span).ToArray();
        var missingRuleExpressions = bound.Schemas.Single().Declarations
            .OfType<ExpressBoundDefinedType>()
            .SelectMany(type => type.Syntax.ChildRules("whereClause"))
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

        var result = GeneratorHostTests.Run(("schemas/ap203.exp", schemaText));
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
            "TedToolkit.Step21.Generated.ConfigControlDesign.SchemaDescriptor")
            ?? throw new InvalidOperationException("The generated AP203 descriptor was not found.");
        var boundedPcurve = result.OutputCompilation.GetTypeByMetadataName(
            "TedToolkit.Step21.Generated.ConfigControlDesign.IBoundedPcurve")
            ?? throw new InvalidOperationException("The generated AP203 bounded_pcurve interface was not found.");
        var product = result.OutputCompilation.GetTypeByMetadataName(
            "TedToolkit.Step21.Generated.ConfigControlDesign.Product")
            ?? throw new InvalidOperationException("The generated AP203 product entity was not found.");
        var productCategory = result.OutputCompilation.GetTypeByMetadataName(
            "TedToolkit.Step21.Generated.ConfigControlDesign.ProductCategory")
            ?? throw new InvalidOperationException("The generated AP203 product_category entity was not found.");
        var reversibleList = result.OutputCompilation.GetTypeByMetadataName(
            "TedToolkit.Step21.Generated.ConfigControlDesign.ListOfReversibleTopologyItem")
            ?? throw new InvalidOperationException("The generated AP203 reversible topology LIST was not found.");
        var reversibleSet = result.OutputCompilation.GetTypeByMetadataName(
            "TedToolkit.Step21.Generated.ConfigControlDesign.SetOfReversibleTopologyItem")
            ?? throw new InvalidOperationException("The generated AP203 reversible topology SET was not found.");
        var aheadOrBehind = result.OutputCompilation.GetTypeByMetadataName(
            "TedToolkit.Step21.Generated.ConfigControlDesign.AheadOrBehind")
            ?? throw new InvalidOperationException("The generated AP203 ahead_or_behind enumeration was not found.");
        var axis2PlacementKind = result.OutputCompilation.GetTypeByMetadataName(
            "TedToolkit.Step21.Generated.ConfigControlDesign.Axis2PlacementKind")
            ?? throw new InvalidOperationException("The generated AP203 axis2_placement SELECT kind was not found.");
        var descriptorSource = result.GeneratedSources.Single(source =>
            source.HintName == "ExpressSchema_CONFIG_CONTROL_DESIGN.g.cs").SourceText.ToString();

        using (Assert.Multiple())
        {
            await Assert.That(descriptorSource).Contains("SchemaName(\"config_control_design\")");
            await Assert.That(descriptor.GetMembers("Name").OfType<IPropertySymbol>().Single().Type.Name)
                .IsEqualTo("SchemaName");
            await Assert.That(boundedPcurve.Interfaces.Select(type => type.Name)
                .SequenceEqual(["IPcurve", "IBoundedCurve"])).IsTrue();
            await Assert.That(product.Constructors.Single(constructor =>
                    constructor.DeclaredAccessibility == Accessibility.Public).Parameters.Select(parameter => parameter.Name)
                .SequenceEqual(["id", "name", "description", "frameOfReference"])).IsTrue();
            await Assert.That(product.GetMembers("FrameOfReference").OfType<IPropertySymbol>().Single().Type.ToDisplayString())
                .IsEqualTo("TedToolkit.Step21.ExpressSet<TedToolkit.Step21.Generated.ConfigControlDesign.IProductContext>");
            await Assert.That(productCategory.GetMembers("Description").OfType<IPropertySymbol>().Single().Type.NullableAnnotation)
                .IsEqualTo(NullableAnnotation.Annotated);
            await Assert.That(reversibleList.GetMembers("Value").OfType<IPropertySymbol>().Single().Type.Name)
                .IsEqualTo("ExpressList");
            await Assert.That(reversibleSet.GetMembers("Value").OfType<IPropertySymbol>().Single().Type.Name)
                .IsEqualTo("ExpressSet");
            await Assert.That(aheadOrBehind.GetMembers().OfType<IPropertySymbol>()
                .Where(property => property.IsStatic).Select(property => property.Name)
                .SequenceEqual(["Ahead", "Behind"])).IsTrue();
            await Assert.That(axis2PlacementKind.GetMembers().OfType<IFieldSymbol>()
                .Where(field => field.HasConstantValue).Select(field => field.Name)
                .SequenceEqual(["Axis2Placement2d", "Axis2Placement3d"])).IsTrue();
        }
    }
}
