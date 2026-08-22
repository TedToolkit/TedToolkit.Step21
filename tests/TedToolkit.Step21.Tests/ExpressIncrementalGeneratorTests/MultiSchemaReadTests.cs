using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>Proves typed binding and ISO population behavior across a closed generated schema set.</summary>
public sealed class MultiSchemaReadTests
{
    private const string BASE_SCHEMA = """
        SCHEMA base_model;
        ENTITY address;
          label : STRING;
        END_ENTITY;
        ENTITY measure;
          amount : INTEGER;
        WHERE
          positive : amount > 0;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string TAG_SCHEMA = """
        SCHEMA tag_model;
        ENTITY tag;
          label : STRING;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string EXTENSION_SCHEMA = """
        SCHEMA extension_model;
        USE FROM base_model (address, measure);
        REFERENCE FROM tag_model (tag);
        ENTITY person;
          name : STRING;
          address_ref : address;
          tag_ref : OPTIONAL tag;
        END_ENTITY;
        RULE positive_measure FOR (measure);
        WHERE
          positive : SIZEOF(measure) = 0;
        END_RULE;
        END_SCHEMA;
        """;

    private const string FOREIGN_SCHEMA = """
        SCHEMA foreign_model;
        ENTITY foreign_address;
          label : STRING;
        END_ENTITY;
        END_SCHEMA;
        """;

    /// <summary>Binds each named section under its exact descriptor and retains cross-schema object identity.</summary>
    [Test]
    public async Task Should_bind_three_schemas_and_include_referenced_targets()
    {
        var generated = GenerateSchemas();
        var structure = ExchangeStructure.Read(new StringReader(CreateExchange(
            "base_model','extension_model','tag_model",
            "FILE_POPULATION('extension_model','INCLUDE_REFERENCED',('extension'));",
            """
            DATA('base',('base_model'));
            #1=ADDRESS('home');
            ENDSEC;
            DATA('tag',('tag_model'));
            #2=TAG('primary');
            ENDSEC;
            DATA('extension',('extension_model'));
            #3=PERSON('Ada',#1,#2);
            ENDSEC;
            """)), generated.Descriptors);
        var registrations = structure.Registrations.ToDictionary(item => item.Name.CanonicalDigits);
        var person = registrations["3"].Entity;

        using (Assert.Multiple())
        {
            await Assert.That(structure.DataSections.Select(section => section.SchemaName).SequenceEqual([
                new SchemaName("base_model"),
                new SchemaName("tag_model"),
                new SchemaName("extension_model"),
            ])).IsTrue();
            await Assert.That(structure.DataSections.Select(section => section.Name).SequenceEqual([
                "base", "tag", "extension",
            ])).IsTrue();
            await Assert.That(registrations["1"].DataSection).IsSameReferenceAs(structure.DataSections[0]);
            await Assert.That(registrations["2"].DataSection).IsSameReferenceAs(structure.DataSections[1]);
            await Assert.That(person.GetType().GetProperty("AddressRef")!.GetValue(person))
                .IsSameReferenceAs(registrations["1"].Entity);
            await Assert.That(person.GetType().GetProperty("TagRef")!.GetValue(person))
                .IsSameReferenceAs(registrations["2"].Entity);
            await Assert.That(structure.Validate().IsValid).IsTrue();
        }
    }

    /// <summary>Applies section-boundary, compatible, referenced, and implicit boundary population visibility.</summary>
    [Test]
    public async Task Should_apply_standard_population_determination_methods()
    {
        var generated = GenerateSchemas();
        var boundary = Assert.Throws<ExchangeStructureReadValidationException>(() => ExchangeStructure.Read(
            new StringReader(CreatePopulationExchange(
                "SECTION_BOUNDARY",
                "('base','extension')",
                "#1=ADDRESS('home'); #2=MEASURE(-1);")),
            generated.Descriptors));
        var compatible = Assert.Throws<ExchangeStructureReadValidationException>(() => ExchangeStructure.Read(
            new StringReader(CreatePopulationExchange(
                "INCLUDE_ALL_COMPATIBLE",
                "('extension')",
                "#1=ADDRESS('home'); #2=MEASURE(1);")),
            generated.Descriptors));
        var referenced = ExchangeStructure.Read(
            new StringReader(CreatePopulationExchange(
                "INCLUDE_REFERENCED",
                "('extension')",
                "#1=ADDRESS('home'); #2=MEASURE(1);")),
            generated.Descriptors);
        var implicitBoundary = Assert.Throws<ExchangeStructureReadValidationException>(() => ExchangeStructure.Read(
            new StringReader(CreateExchange(
                "base_model','extension_model','tag_model",
                string.Empty,
                """
                DATA('base',('base_model'));
                #1=ADDRESS('home');
                ENDSEC;
                DATA('extension',('extension_model'));
                #3=PERSON('Ada',#1,$);
                ENDSEC;
                """)),
            generated.Descriptors));

        using (Assert.Multiple())
        {
            await Assert.That(boundary.ValidationResult.Failures.Any(failure =>
                failure.Code == "EXTENSION_MODEL.RULE.POSITIVE_MEASURE.WHERE.POSITIVE")).IsTrue();
            await Assert.That(boundary.ValidationResult.Failures.Any(failure =>
                failure.Code == "BASE_MODEL.MEASURE.WHERE.POSITIVE")).IsTrue();
            await Assert.That(compatible.ValidationResult.Failures.Any(failure =>
                failure.Code == "EXTENSION_MODEL.RULE.POSITIVE_MEASURE.WHERE.POSITIVE")).IsTrue();
            await Assert.That(referenced.Validate().IsValid).IsTrue();
            await Assert.That(implicitBoundary.ValidationResult.Failures.Any(failure =>
                failure.Code == "P21.POPULATION.REFERENCE.UNSET"
                && failure.Path.StartsWith("DataSections[1].#3", StringComparison.Ordinal))).IsTrue();
        }
    }

    /// <summary>Aggregates schema/population binding faults and rejects a structurally similar foreign target.</summary>
    [Test]
    public async Task Should_aggregate_invalid_multi_schema_evidence_atomically()
    {
        var generated = GenerateSchemas(includeForeign: true);
        var binding = Assert.Throws<ExchangeStructureBindingException>(() => ExchangeStructure.Read(
            new StringReader(CreateExchange(
                "base_model','extension_model','missing_model",
                "FILE_POPULATION('ghost_model','SECTION_BOUNDARY',('extension','extension','absent'));",
                """
                DATA('base',('base_model'));
                #1=ADDRESS('home');
                ENDSEC;
                DATA('extension',('extension_model'));
                #2=PERSON('Ada',#1,$);
                ENDSEC;
                """)),
            generated.Descriptors));
        var reference = Assert.Throws<ExchangeStructureReadValidationException>(() => ExchangeStructure.Read(
            new StringReader(CreateExchange(
                "extension_model','foreign_model",
                "FILE_POPULATION('extension_model','INCLUDE_REFERENCED',('extension'));",
                """
                DATA('foreign',('foreign_model'));
                #1=FOREIGN_ADDRESS('home');
                ENDSEC;
                DATA('extension',('extension_model'));
                #2=PERSON('Ada',#1,$);
                ENDSEC;
                """)),
            generated.Descriptors));

        using (Assert.Multiple())
        {
            await Assert.That(binding.Diagnostics.Count(diagnostic => diagnostic.Code == "P21-BIND-SCHEMA"))
                .IsEqualTo(1);
            await Assert.That(binding.Diagnostics.Count(diagnostic =>
                diagnostic.Code == "P21-BIND-SCHEMA-POPULATION")).IsEqualTo(3);
            await Assert.That(binding.Diagnostics.All(diagnostic => diagnostic.SourceLocation is
            { FilePath: "<reader>", Line: > 0, Column: > 0, })).IsTrue();
            await Assert.That(reference.ValidationResult.Failures.Select(failure => failure.Code))
                .IsEquivalentTo(["P21.READ.REFERENCE.TYPE"]);
            await Assert.That(reference.ValidationResult.Failures[0].Path)
                .IsEqualTo("DataSections[1].#2.Parameters[1]");
        }
    }

    /// <summary>Does not move a pre-registered referenced entity when a cross-schema root is added.</summary>
    [Test]
    public async Task Should_preserve_governing_section_for_a_manually_constructed_graph()
    {
        var generated = GenerateSchemas();
        var structure = new ExchangeStructure(CreateHeader("base_model", "extension_model", "tag_model"), generated.Descriptors);
        var baseSection = new DataSection(new SchemaName("base_model"));
        var extensionSection = new DataSection(new SchemaName("extension_model"));
        structure.DataSections.Add(baseSection);
        structure.DataSections.Add(extensionSection);
        var baseDescriptor = generated.ByName["base_model"];
        var extensionDescriptor = generated.ByName["extension_model"];
        var address = baseDescriptor.AllocateEntity(["ADDRESS"])!;
        var person = extensionDescriptor.AllocateEntity(["PERSON"])!;
        await Assert.That(baseDescriptor.HydrateEntity(structure, address,
            [new("ADDRESS", [ParameterValue.FromString("home")])])).IsEmpty();
        structure.Add(baseSection, new EntityInstanceName("10"), address);
        await Assert.That(extensionDescriptor.HydrateEntity(structure, person,
            [new("PERSON", [
                ParameterValue.FromString("Ada"),
                ParameterValue.FromEntity(address),
                ParameterValue.Omitted,
            ])])).IsEmpty();

        _ = structure.Add(extensionSection, person);

        using (Assert.Multiple())
        {
            await Assert.That(structure.Registrations.Single(item => ReferenceEquals(item.Entity, address)).DataSection)
                .IsSameReferenceAs(baseSection);
            await Assert.That(structure.Registrations.Single(item => ReferenceEquals(item.Entity, person)).DataSection)
                .IsSameReferenceAs(extensionSection);
        }
    }

    /// <summary>Rejects non-standard population algorithms before graph allocation begins.</summary>
    [Test]
    public async Task Should_reject_an_unknown_population_determination_method()
    {
        var generated = GenerateSchemas();
        var exception = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(CreateExchange(
                "base_model','extension_model",
                "FILE_POPULATION('extension_model','VENDOR_MAGIC',$);",
                """
                DATA('base',('base_model'));
                #1=ADDRESS('home');
                ENDSEC;
                DATA('extension',('extension_model'));
                #2=PERSON('Ada',#1,$);
                ENDSEC;
                """)),
            generated.Descriptors));

        using (Assert.Multiple())
        {
            await Assert.That(exception.Diagnostics.Select(diagnostic => diagnostic.Code))
                .IsEquivalentTo(["P21-CAP-SCHEMA-POPULATION"]);
            await Assert.That(exception.Diagnostics[0].SourceLocation).IsNotNull();
            await Assert.That(exception.Diagnostics[0].SourceLocation!.FilePath).IsEqualTo("<reader>");
            await Assert.That(exception.Diagnostics[0].SourceLocation!.Line).IsGreaterThan(0);
            await Assert.That(exception.Diagnostics[0].SourceLocation!.Column).IsGreaterThan(0);
        }
    }

    private static string CreatePopulationExchange(string method, string sections, string baseEntities) => CreateExchange(
        "base_model','extension_model','tag_model",
        $"FILE_POPULATION('extension_model','{method}',{sections});",
        $$"""
        DATA('base',('base_model'));
        {{baseEntities}}
        ENDSEC;
        DATA('extension',('extension_model'));
        #3=PERSON('Ada',#1,$);
        ENDSEC;
        """);

    private static string CreateExchange(string schemas, string populations, string sections) => $$"""
        ISO-10303-21;
        HEADER;
        FILE_DESCRIPTION(('multi schema test'),'3;1');
        FILE_NAME('multi.p21','2026-08-22T00:00:00',('Author'),('Org'),'Pre','System','Auth');
        FILE_SCHEMA(('{{schemas}}'));
        {{populations}}
        ENDSEC;
        {{sections}}
        END-ISO-10303-21;
        """;

    private static HeaderSection CreateHeader(params string[] schemas) => new(
        new FileDescription(["multi schema test"], "3;1"),
        new FileName("multi.p21", "2026-08-22T00:00:00", ["Author"], ["Org"], "Pre", "System", "Auth"),
        new FileSchema(schemas));

    private static GeneratedSchemas GenerateSchemas(bool includeForeign = false)
    {
        var sources = new List<(string Path, string Text)>
        {
            ("schemas/base.exp", BASE_SCHEMA),
            ("schemas/tag.exp", TAG_SCHEMA),
            ("schemas/extension.exp", EXTENSION_SCHEMA),
        };
        if (includeForeign)
            sources.Add(("schemas/foreign.exp", FOREIGN_SCHEMA));
        var result = GeneratorHostTests.Run([.. sources]);
        var diagnostics = result.Diagnostics
            .Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        if (diagnostics.Length > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics.AsEnumerable()));

        using var stream = new MemoryStream();
        var emit = result.OutputCompilation.Emit(stream);
        if (!emit.Success)
            throw new InvalidOperationException(string.Join(Environment.NewLine, emit.Diagnostics));
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        var descriptors = sources.Select(source =>
        {
            var schemaName = source.Text.Split(';')[0].Split(' ', StringSplitOptions.RemoveEmptyEntries)[1];
            var typeName = $"TedToolkit.Step21.Generated.{ToPascalCase(schemaName)}.SchemaDescriptor";
            return (SchemaDescriptor)assembly.GetType(typeName, throwOnError: true)!
                .GetProperty("Instance")!.GetValue(null)!;
        }).ToArray();
        return new GeneratedSchemas(
            descriptors,
            descriptors.ToDictionary(descriptor => descriptor.Name.Value, StringComparer.Ordinal));
    }

    private static string ToPascalCase(string name) => string.Concat(name.Split('_')
        .Select(part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));

    private sealed record GeneratedSchemas(
        IReadOnlyCollection<SchemaDescriptor> Descriptors,
        IReadOnlyDictionary<string, SchemaDescriptor> ByName);
}