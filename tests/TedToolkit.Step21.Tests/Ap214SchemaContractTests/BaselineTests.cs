// -----------------------------------------------------------------------
// <copyright file="BaselineTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Analyzer.Express.Binding;
using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.Ap214SchemaContractTests;

/// <summary>
/// Proves the pinned AP214 source, redistribution evidence, and complete generated contract.
/// </summary>
internal sealed class BaselineTests
{
    private const string UpstreamSha256 = "71AB140FE7F774321BEEE6A31E6FEE2AFC3973FD60350AE2018C74C211FB4295";

    private const string CanonicalLfSha256 = "9516315F0A8CBB9A4F6598D92FCE36BEE5189A28D1ACEA1D87E2C411266211B7";

    private static readonly SymbolDisplayFormat _publicApiDisplayFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters
            | SymbolDisplayGenericsOptions.IncludeVariance
            | SymbolDisplayGenericsOptions.IncludeTypeConstraints,
        memberOptions: SymbolDisplayMemberOptions.IncludeAccessibility
            | SymbolDisplayMemberOptions.IncludeModifiers
            | SymbolDisplayMemberOptions.IncludeContainingType
            | SymbolDisplayMemberOptions.IncludeType
            | SymbolDisplayMemberOptions.IncludeParameters
            | SymbolDisplayMemberOptions.IncludeExplicitInterface,
        parameterOptions: SymbolDisplayParameterOptions.IncludeExtensionThis
            | SymbolDisplayParameterOptions.IncludeParamsRefOut
            | SymbolDisplayParameterOptions.IncludeType
            | SymbolDisplayParameterOptions.IncludeName
            | SymbolDisplayParameterOptions.IncludeDefaultValue,
        propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
        localOptions: SymbolDisplayLocalOptions.IncludeType,
        kindOptions: SymbolDisplayKindOptions.IncludeMemberKeyword,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>
    /// Verifies the checked-in AP214 source and STEPcode attribution retain every approved identity and hash.
    /// </summary>
    [Test]
    public async Task Should_preserve_the_pinned_source_and_redistribution_evidence()
    {
        var directory = TestDataDirectory();
        var schemaPath = Path.Combine(directory, "AP214E3_2010.exp");
        var sourceBytes = File.ReadAllBytes(schemaPath);
        var source = Encoding.UTF8.GetString(sourceBytes);
        var canonicalSource = source.ReplaceLineEndings("\n");
        var upstreamSource = canonicalSource.ReplaceLineEndings("\r\n");
        var provenance = File.ReadAllText(Path.Combine(directory, "PROVENANCE.md"));

        using (Assert.Multiple())
        {
            await Assert.That(ComputeTextHash(upstreamSource)).IsEqualTo(UpstreamSha256);
            await Assert.That(ComputeTextHash(canonicalSource)).IsEqualTo(CanonicalLfSha256);
            await Assert.That(ComputeCanonicalTextFileHash(Path.Combine(directory, "COPYING")))
                .IsEqualTo("C787486F3E1358CF1CB4456B56E00862DE9C0433E7D49F5501D1289FF8BEF37E");
            await Assert.That(ComputeCanonicalTextFileHash(Path.Combine(directory, "AUTHORS")))
                .IsEqualTo("619EE3D3D9CE6DB690B4A20F36AB30616CB8F1FB8616FAEB85D9685AFDFD15FB");
            await Assert.That(ComputeCanonicalTextFileHash(Path.Combine(directory, "INTENT.md")))
                .IsEqualTo("B10C7DCC9C269B383C944ACC139F787CCA050D497142CA03ED90C49EF41CE23F");
            await Assert.That(source).Contains("SCHEMA AUTOMOTIVE_DESIGN;");
            await Assert.That(source).Contains("ISO/DIS 10303-214:2007");
            await Assert.That(source).Contains("2009-06-30");
            await Assert.That(source).Contains(
                "{ iso standard 10303 part(214) version(3) object(1) automotive-design-schema(1) }");
            await Assert.That(provenance).Contains("9baa5dadaa1dcfcdc623220d865d36d61ea351e9");
            await Assert.That(provenance).Contains(UpstreamSha256);
            await Assert.That(provenance).Contains(CanonicalLfSha256);
            await Assert.That(provenance).Contains("BSD-3-Clause");
        }
    }

    /// <summary>
    /// Verifies the complete AP214 source generates its unique descriptor, representative EXPRESS fidelity, and approved public API.
    /// </summary>
    [Test]
    public async Task Should_generate_the_approved_automotive_design_surface()
    {
        var directory = TestDataDirectory();
        var schemaText = File.ReadAllText(Path.Combine(directory, "AP214E3_2010.exp"));
        var compilation = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("schemas/ap214/AP214E3_2010.exp", schemaText),
        ]);
        await Assert.That(compilation.SyntaxDiagnostics).IsEmpty();
        await Assert.That(compilation.BindingDiagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, compilation.BindingDiagnostics.Select(item => item.Message)));

        var result = GeneratorHostTests.Run(("schemas/ap214/AP214E3_2010.exp", schemaText));
        await Assert.That(result.Diagnostics
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error))
            .IsEmpty()
            .Because(string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        await Assert.That(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error))
            .IsEmpty()
            .Because(string.Join(Environment.NewLine, result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)));

        var descriptor = RequiredType(result.OutputCompilation, "SchemaDescriptor");
        var advancedFace = RequiredType(result.OutputCompilation, "IAdvancedFace");
        var product = RequiredType(result.OutputCompilation, "Product");
        var productCategory = RequiredType(result.OutputCompilation, "ProductCategory");
        var reversibleList = RequiredType(result.OutputCompilation, "ListOfReversibleTopologyItem");
        var reversibleSet = RequiredType(result.OutputCompilation, "SetOfReversibleTopologyItem");
        var aheadOrBehind = RequiredType(result.OutputCompilation, "AheadOrBehind");
        var axis2PlacementKind = RequiredType(result.OutputCompilation, "Axis2PlacementKind");
        var descriptorSource = result.GeneratedSources.Single(source =>
            source.HintName == "ExpressSchema_AUTOMOTIVE_DESIGN.g.cs").SourceText.ToString();
        var publicApi = RenderPublicApi(result.OutputCompilation);
        var publicApiHash = ComputeTextHash(publicApi);
        var approvedPublicApiHash = File.ReadAllText(Path.Combine(directory, "PublicApi.approved.sha256")).Trim();

        using (Assert.Multiple())
        {
            await Assert.That(result.GeneratedSources.Count(source =>
                source.HintName == "ExpressSchema_AUTOMOTIVE_DESIGN.g.cs")).IsEqualTo(1);
            await Assert.That(descriptorSource).Contains("SchemaName(\"automotive_design\")");
            await Assert.That(descriptor.GetMembers("Name").OfType<IPropertySymbol>().Single().Type.Name)
                .IsEqualTo("SchemaName");
            await Assert.That(advancedFace.Interfaces.Select(type => type.Name)).Contains("IFaceSurface");
            await Assert.That(product.Constructors.Single(constructor =>
                        constructor.DeclaredAccessibility == Accessibility.Public).Parameters.Select(parameter => parameter.Name)
                    .SequenceEqual(["id", "name", "description", "frameOfReference"]))
                .IsTrue();
            await Assert.That(product.GetMembers("FrameOfReference").OfType<IPropertySymbol>().Single().Type.Name)
                .IsEqualTo("ExpressSet");
            await Assert.That(productCategory.GetMembers("Description").OfType<IPropertySymbol>().Single().Type.NullableAnnotation)
                .IsEqualTo(NullableAnnotation.Annotated);
            await Assert.That(reversibleList.GetMembers("Value").OfType<IPropertySymbol>().Single().Type.Name)
                .IsEqualTo("ExpressList");
            await Assert.That(reversibleSet.GetMembers("Value").OfType<IPropertySymbol>().Single().Type.Name)
                .IsEqualTo("ExpressSet");
            await Assert.That(aheadOrBehind.GetMembers().OfType<IPropertySymbol>()
                    .Where(property => property.IsStatic).Select(property => property.Name)
                    .SequenceEqual(["Ahead", "Behind"]))
                .IsTrue();
            await Assert.That(axis2PlacementKind.GetMembers().OfType<IFieldSymbol>()
                    .Where(field => field.HasConstantValue).Select(field => field.Name)
                    .SequenceEqual(["Axis2Placement2d", "Axis2Placement3d"]))
                .IsTrue();
            await Assert.That(publicApiHash).IsEqualTo(approvedPublicApiHash)
                .Because($"Actual AP214 generated public API SHA-256: {publicApiHash}");
        }
    }

    private static string TestDataDirectory() => Path.Combine(
        AppContext.BaseDirectory,
        "TestData",
        "Express",
        "Ap214");

    private static INamedTypeSymbol RequiredType(Compilation compilation, string name) =>
        compilation.GetTypeByMetadataName($"TedToolkit.Step21.Generated.AutomotiveDesign.{name}")
        ?? throw new InvalidOperationException($"The generated AP214 {name} type was not found.");

    private static string RenderPublicApi(Compilation compilation)
    {
        var generatedNamespace = compilation.GlobalNamespace
            .GetNamespaceMembers().Single(item => item.Name == "TedToolkit")
            .GetNamespaceMembers().Single(item => item.Name == "Step21")
            .GetNamespaceMembers().Single(item => item.Name == "Generated")
            .GetNamespaceMembers().Single(item => item.Name == "AutomotiveDesign");
        var lines = new List<string>();
        foreach (var type in generatedNamespace.GetTypeMembers()
                     .Where(type => type.DeclaredAccessibility == Accessibility.Public)
                     .OrderBy(type => type.MetadataName, StringComparer.Ordinal))
        {
            lines.Add(type.ToDisplayString(_publicApiDisplayFormat));
            lines.AddRange(type.GetMembers()
                .Where(IsPublicContract)
                .Select(member => member.ToDisplayString(_publicApiDisplayFormat))
                .OrderBy(member => member, StringComparer.Ordinal)
                .Select(member => $"  {member}"));
        }

        return string.Join('\n', lines) + '\n';
    }

    private static bool IsPublicContract(ISymbol symbol) =>
        symbol.DeclaredAccessibility is Accessibility.Public
            or Accessibility.Protected
            or Accessibility.ProtectedOrInternal;

    private static string ComputeCanonicalTextFileHash(string path) =>
        ComputeTextHash(File.ReadAllText(path).ReplaceLineEndings("\n"));

    private static string ComputeTextHash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
