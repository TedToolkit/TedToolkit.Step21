// -----------------------------------------------------------------------
// <copyright file="AnnexFEcmaScriptBindingTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;

using TedToolkit.Step21.AnnexF;
using TedToolkit.Step21.IntegrationTests.ExternalCorpus;

namespace TedToolkit.Step21.IntegrationTests.AnnexFEcmaScriptBindingTests;

[NotInParallel("annex-f-engine")]
internal sealed class AnnexFEcmaScriptBindingTests
{
    [Test]
    public async Task Should_execute_every_annex_f_mapping_and_apply_mutations_with_node()
    {
        var structure = CreateStructure();
        var bridge = new AnnexFModelBridge(
            structure,
            new Part21Resource("https://example.test/original.step"));
        var original = bridge.ExportState();
        await Assert.That(bridge.ExportState()).IsEqualTo(original);

        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"TedToolkit.Step21.AnnexF.{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            var modulePath = Path.Combine(temporaryRoot, AnnexFEcmaScriptModule.FileName);
            var inputPath = Path.Combine(temporaryRoot, "input.json");
            var outputPath = Path.Combine(temporaryRoot, "output.json");
            await File.WriteAllTextAsync(modulePath, AnnexFEcmaScriptModule.Source);
            await File.WriteAllTextAsync(inputPath, original);

            var runner = Path.Combine(AppContext.BaseDirectory, "TestData", "AnnexF", "annex-f-integration.js");
            var result = await Run("node", runner, modulePath, inputPath, outputPath);
            var nodeVersion = await Run("node", "--version");
            await Assert.That(result).Contains("ANNEX_F_NODE_OK assertions=46");
            await Assert.That(nodeVersion).StartsWith("v");

            bridge.ApplyState(await File.ReadAllTextAsync(outputPath));
            await AssertMutatedModel(bridge);

            var unsignedPopulations = structure.SchemaPopulation
                .Select(population => new SchemaPopulationExternalFile(
                    population.Location,
                    population.TimeStamp))
                .ToArray();
            structure.SchemaPopulation.Clear();
            foreach (var population in unsignedPopulations)
                structure.SchemaPopulation.Add(population);
            var validation = structure.Validate();
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(string.Join(
                    Environment.NewLine,
                    validation.Failures.Select(failure => $"{failure.Code} {failure.Path}: {failure.Message}")));
            }
            using var destination = new StringWriter();
            structure.Write(destination);
            var p21 = destination.ToString();
            using (Assert.Multiple())
            {
                await Assert.That(p21).Contains("FILE_NAME('annex-f.changed'");
                await Assert.That(p21).Contains("<integer>=42;");
                await Assert.That(p21).Contains("<real>=1.E1;");
                await Assert.That(p21).Contains("<enumeration>=.F.;");
                await Assert.That(p21).Contains("<binary>=\"0F\";");
                await Assert.That(p21).Contains("{label:'changed label'}");
                await Assert.That(p21).Contains("SCHEMA_POPULATION((('https://example.test/changed-population.step'");
                await Assert.That(p21).Contains("('relative-population.step'");
            }
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    [Test]
    public async Task Should_reject_an_invalid_bridge_state_without_partial_mutation()
    {
        var bridge = new AnnexFModelBridge(CreateStructure(), new Part21Resource("urn:original"));
        var before = bridge.ExportState();
        var population = bridge.Structure.SchemaPopulation.Single();
        bridge.ApplyState(before);
        await Assert.That(bridge.Structure.SchemaPopulation.Single()).IsSameReferenceAs(population);
        await Assert.That(population.DigestStatus).IsEqualTo(SchemaPopulationDigestStatus.Verified);
        var invalid = before.Replace("\"name\":\"integer\"", "\"name\":\"renamed\"", StringComparison.Ordinal);

        await Assert.That(() => bridge.ApplyState(invalid)).Throws<JsonException>();
        await Assert.That(bridge.ExportState()).IsEqualTo(before);
    }

    [Test]
    public async Task Should_package_the_versioned_module_without_an_engine_dependency()
    {
        var root = RepositoryPaths.FindRoot();
        var project = Path.Combine(root, "src", "TedToolkit.Step21.AnnexF", "TedToolkit.Step21.AnnexF.csproj");
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"TedToolkit.Step21.AnnexF.Package.{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            _ = await Run(
                "dotnet",
                "pack",
                project,
                "--configuration",
                "Release",
                "--no-build",
                "--no-restore",
                "--output",
                temporaryRoot);
            var package = Directory.GetFiles(temporaryRoot, "TedToolkit.Step21.AnnexF.1.0.0.nupkg").Single();
            using var archive = ZipFile.OpenRead(package);
            var names = archive.Entries.Select(entry => entry.FullName).ToArray();
            var specification = await ReadEntry(archive, "TedToolkit.Step21.AnnexF.nuspec");
            var source = await ReadEntry(
                archive,
                "contentFiles/any/any/TedToolkit.Step21.AnnexF/AnnexF.js");
            using (Assert.Multiple())
            {
                await Assert.That(names).Contains("lib/net10.0/TedToolkit.Step21.AnnexF.dll");
                await Assert.That(names).Contains("README.md");
                await Assert.That(source).IsEqualTo(AnnexFEcmaScriptModule.Source);
                await Assert.That(AnnexFEcmaScriptModule.SourceSha256).Matches("^[0-9A-F]{64}$");
                await Assert.That(specification).Contains(
                    "<dependency id=\"TedToolkit.Step21\" version=\"[1.0.0, 2.0.0)\" exclude=\"Build,Analyzers\" />");
                await Assert.That(specification).DoesNotContain("Jint");
                await Assert.That(specification).DoesNotContain("JavaScriptEngineSwitcher");
                await Assert.That(File.ReadAllText(Path.Combine(root, "src", "TedToolkit.Step21", "TedToolkit.Step21.csproj")))
                    .DoesNotContain("AnnexF");
            }

            var approvedApi = NormalizeLineEndings(await File.ReadAllTextAsync(Path.Combine(
                root,
                "tests",
                "TedToolkit.Step21.IntegrationTests",
                "TestData",
                "AnnexF",
                "PublicApi.approved.txt")));
            await Assert.That(RenderPublicApi(typeof(AnnexFModelBridge).Assembly)).IsEqualTo(approvedApi);

            using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(
                root,
                "docs",
                "conformance",
                "annex-f-ecmascript-binding.json")));
            var requirements = manifest.RootElement.GetProperty("requirements")
                .EnumerateArray()
                .Select(item => item.GetProperty("id").GetString())
                .ToArray();
            await Assert.That(requirements.Length).IsEqualTo(22);
            await Assert.That(requirements.Distinct(StringComparer.Ordinal).Count()).IsEqualTo(22);
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    private static ExchangeStructure CreateStructure()
    {
        var header = new HeaderSection(
            new FileDescription(["Annex F integration"], "4;3"),
            new FileName(
                "annex-f.original",
                "2026-09-08T00:00:00Z",
                ["TedToolkit"],
                ["TedToolkit"],
                "TedToolkit.Step21",
                "integration-test",
                string.Empty),
            new FileSchema(["ANNEX_F_TEST"]));
        var structure = new ExchangeStructure(header, [AnnexFSchemaDescriptor.Instance]);
        structure.References.Add(new Part21Reference(
            new EntityInstanceName("20"),
            new Part21Resource("external.step#entity")));
        structure.References.Add(new Part21Reference(
            new ValueInstanceName("30"),
            new Part21Resource("external.step#value")));
        structure.References.Add(new Part21Reference(
            new EntityInstanceName("21"),
            new Part21Resource("changed.step#entity")));
        structure.References.Add(new Part21Reference(
            new ValueInstanceName("31"),
            new Part21Resource("changed.step#value")));
        structure.Anchors.Add(new Part21Anchor(new AnchorName("integer"), ParameterValue.FromInteger(10)));
        structure.Anchors.Add(new Part21Anchor(new AnchorName("real"), ParameterValue.FromReal(new RealValue(125, -2))));
        structure.Anchors.Add(new Part21Anchor(new AnchorName("text"), ParameterValue.FromString("hé'llo\\")));
        structure.Anchors.Add(new Part21Anchor(new AnchorName("enumeration"), ParameterValue.FromEnumeration("T")));
        structure.Anchors.Add(new Part21Anchor(new AnchorName("binary"), ParameterValue.FromBinary(new BinaryValue("101"))));
        structure.Anchors.Add(new Part21Anchor(
            new AnchorName("eid"),
            ParameterValue.FromEntityInstance(new EntityInstanceName("20"))));
        structure.Anchors.Add(new Part21Anchor(
            new AnchorName("vid"),
            ParameterValue.FromValueInstance(new ValueInstanceName("30"))));
        structure.Anchors.Add(new Part21Anchor(
            new AnchorName("cin"),
            ParameterValue.FromConstantEntity(new ConstantEntityName("INCH"))));
        structure.Anchors.Add(new Part21Anchor(
            new AnchorName("cvn"),
            ParameterValue.FromConstantValue(new ConstantValueName("PI"))));
        structure.Anchors.Add(new Part21Anchor(new AnchorName("null-value"), ParameterValue.Omitted));
        structure.Anchors.Add(new Part21Anchor(
            new AnchorName("list"),
            ParameterValue.FromAggregate([
                ParameterValue.FromInteger(1),
                ParameterValue.FromString("member"),
                ParameterValue.Omitted,
            ])));
        structure.Anchors.Add(new Part21Anchor(
            new AnchorName("resource"),
            ParameterValue.FromResource(new Part21Resource("#integer"))));
        structure.Anchors.Add(new Part21Anchor(
            new AnchorName("tagged"),
            ParameterValue.FromString("value"),
            [new Part21AnchorTag("label", ParameterValue.FromString("original"))]));
        var population = new SchemaPopulationExternalFile(
            new Uri("https://example.test/population.step"),
            "2026-09-08T00:00:00Z",
            "AQID");
        population.DigestStatus = SchemaPopulationDigestStatus.Verified;
        structure.SchemaPopulation.Add(population);
        return structure;
    }

    private static async Task AssertMutatedModel(AnnexFModelBridge bridge)
    {
        using (Assert.Multiple())
        {
            await Assert.That(bridge.Uri.Value).IsEqualTo("https://example.test/changed.step");
            await Assert.That(bridge.Structure.Header.FileName.Name).IsEqualTo("annex-f.changed");
            await Assert.That(bridge.Structure.Anchors.Count).IsEqualTo(13);
            await Assert.That(bridge.Structure.SchemaPopulation.Count).IsEqualTo(2);
            await Assert.That(bridge.Structure.SchemaPopulation[0].Location.OriginalString)
                .IsEqualTo("https://example.test/changed-population.step");
            await Assert.That(bridge.Structure.SchemaPopulation[0].MessageDigest).IsEqualTo("AQID");
            await Assert.That(bridge.Structure.SchemaPopulation[1].Location.OriginalString)
                .IsEqualTo("relative-population.step");
            await Assert.That(bridge.Structure.SchemaPopulation[1].TimeStamp)
                .IsEqualTo("2026-09-09T00:00:00.000Z");
            await Assert.That(bridge.Structure.Anchors[^1].Tags.Single().Item.TryGetString(out var tag))
                .IsTrue();
            await Assert.That(tag).IsEqualTo("changed label");
        }
    }

    private static async Task<string> ReadEntry(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path) ?? throw new InvalidDataException($"Package entry '{path}' is missing.");
        await using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private static string RenderPublicApi(Assembly assembly)
    {
        var result = new StringBuilder();
        foreach (var type in assembly.GetExportedTypes().OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            result.Append(type.IsAbstract && type.IsSealed ? "static class " : "sealed class ")
                .AppendLine(type.FullName);
            var members = new List<string>();
            members.AddRange(type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(field => $"field {field.FieldType.FullName} {field.Name} = {field.GetRawConstantValue()}"));
            members.AddRange(type.GetConstructors()
                .Select(constructor => $"constructor ({string.Join(',', constructor.GetParameters().Select(
                    parameter => parameter.ParameterType.FullName))})"));
            members.AddRange(type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .Select(method => $"method {method.ReturnType.FullName} {method.Name}({string.Join(',', method.GetParameters().Select(
                    parameter => parameter.ParameterType.FullName))})"));
            members.AddRange(type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(property => $"property {property.PropertyType.FullName} {property.Name}"));
            foreach (var member in members.OrderBy(member => member, StringComparer.Ordinal))
                result.Append("  ").AppendLine(member);
        }
        return NormalizeLineEndings(result.ToString());
    }

    private static string NormalizeLineEndings(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static async Task<string> Run(string fileName, params string[] arguments)
    {
        var start = new ProcessStartInfo(fileName)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Unable to start '{fileName}'.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{fileName} failed with exit code {process.ExitCode}: {error}");
        return output.Trim();
    }

    private sealed class AnnexFSchemaDescriptor : SchemaDescriptor
    {
        internal static AnnexFSchemaDescriptor Instance { get; } = new();

        public override SchemaName Name => new("ANNEX_F_TEST");

        protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => null;

        protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
            ExchangeStructure structure,
            Entity value,
            IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) => [];

        protected override ValidationResult ValidateCore(
            ExchangeStructure structure,
            IReadOnlyList<KeyValuePair<string, Entity>> entities) => new([]);

        protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(ExchangeStructure structure) => [];

        protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
            Entity value) => [];

        protected override bool ContainsConstantEntityCore(string name) => name is "INCH" or "FOOT";

        protected override bool ContainsConstantValueCore(string name) => name is "PI" or "TAU";
    }
}
