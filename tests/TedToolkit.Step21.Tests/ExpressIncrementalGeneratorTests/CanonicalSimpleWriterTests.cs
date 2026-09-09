using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;
using TedToolkit.Step21.Tests.Parsing;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>Proves deterministic simple entity and complete exchange-structure writing.</summary>
public sealed class CanonicalSimpleWriterTests
{
    private const string WRITER_SCHEMA = """
        SCHEMA writer_model;
        TYPE label = STRING;
        END_TYPE;
        TYPE status = ENUMERATION OF (active, inactive);
        END_TYPE;
        TYPE text_choice = SELECT (label);
        END_TYPE;
        ENTITY sample;
          integer_value : INTEGER;
          real_value : REAL;
          number_value : NUMBER;
          text_value : STRING;
          bits_value : BINARY;
          boolean_value : BOOLEAN;
          logical_value : LOGICAL;
          status_value : status;
          choice_value : text_choice;
          optional_text : OPTIONAL STRING;
          integer_values : LIST [1:?] OF INTEGER;
        END_ENTITY;
        END_SCHEMA;
        """;

    /// <summary>Canonicalizes every generated simple value and reads the edited output back equivalently.</summary>
    [Test]
    public async Task Should_write_and_reread_generated_simple_values()
    {
        var descriptor = CreateGeneratedDescriptor();
        var structure = ExchangeStructure.Read(new StringReader("""
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('O''Brien'),'3;1');
            FILE_NAME('writer.p21','2026-08-22T00:00:00',('Author'),('Org'),'Pre','System','Auth');
            FILE_SCHEMA(('WRITER_MODEL'));
            ENDSEC;
            DATA;
            #0001=SAMPLE(18446744073709551616,1.25,-7,'original',"31",.T.,.U.,.ACTIVE.,LABEL('typed'),$,(1,2));
            ENDSEC;
            END-ISO-10303-21;
            """), [descriptor]);
        var entity = structure.Registrations.Single().Entity;
        entity.GetType().GetProperty("TextValue")!.SetValue(entity, "edited π😀");
        var expectedProjection = descriptor.ProjectEntity(entity).Single().Value;
        var destination = new StringWriter();

        structure.Write(destination);

        const string expected = """
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('O''Brien'),'3;1');
            FILE_NAME('writer.p21','2026-08-22T00:00:00',('Author'),('Org'),'Pre','System','Auth');
            FILE_SCHEMA(('WRITER_MODEL'));
            ENDSEC;
            DATA;
            #1=SAMPLE(18446744073709551616,125.E-2,-7,'edited \X2\03C0\X0\\X4\0001F600\X0\',"31",.T.,.U.,.ACTIVE.,LABEL('typed'),$,(1,2));
            ENDSEC;
            END-ISO-10303-21;
            """;
        var output = destination.ToString();
        var reread = ExchangeStructure.Read(new StringReader(output), [descriptor]);
        var actualProjection = descriptor.ProjectEntity(reread.Registrations.Single().Entity).Single().Value;

        using (Assert.Multiple())
        {
            await Assert.That(output).IsEqualTo(expected.Replace("\r\n", "\n"));
            await Assert.That(actualProjection.SequenceEqual(expectedProjection)).IsTrue();
        }
    }

    /// <summary>Formats omitted, derived, aggregate, typed, and entity-reference values with structure-owned names.</summary>
    [Test]
    public async Task Should_write_one_registered_entity_with_independent_structure_identity()
    {
        var descriptor = new ProjectionDescriptor("writer_custom");
        var target = new ProjectionEntity("TARGET", []);
        var root = new ProjectionEntity(
            "ROOT",
            [
                ParameterValue.Omitted,
                ParameterValue.Derived,
                ParameterValue.FromEntity(target),
                ParameterValue.FromAggregate([ParameterValue.FromLogical(LogicalValue.Unknown)]),
                ParameterValue.FromTyped("LABEL", ParameterValue.FromString("x")),
            ],
            target);
        var first = CreateStructure(descriptor);
        var firstSection = new DataSection(descriptor.Name);
        first.DataSections.Add(firstSection);
        first.Add(firstSection, new EntityInstanceName("42"), target);
        first.Add(firstSection, new EntityInstanceName("7"), root);
        var second = CreateStructure(descriptor);
        var secondSection = new DataSection(descriptor.Name);
        second.DataSections.Add(secondSection);
        second.Add(secondSection, new EntityInstanceName("99"), target);
        second.Add(secondSection, new EntityInstanceName("8"), root);
        var firstOutput = new StringWriter();
        var secondOutput = new StringWriter();

        first.WriteEntity(firstOutput, root);
        second.WriteEntity(secondOutput, root);

        using (Assert.Multiple())
        {
            await Assert.That(firstOutput.ToString()).IsEqualTo("#7=ROOT($,*,#42,(.U.),LABEL('x'));");
            await Assert.That(secondOutput.ToString()).IsEqualTo("#8=ROOT($,*,#99,(.U.),LABEL('x'));");
            await Assert.That(root.ToString()).DoesNotContain("#7=");
            await Assert.That(root.ToString()).DoesNotContain("#8=");
        }
    }

    /// <summary>Retains named multi-schema sections and standard FILE_POPULATION context.</summary>
    [Test]
    public async Task Should_write_multi_schema_section_and_population_context()
    {
        var firstDescriptor = new ProjectionDescriptor("first_schema");
        var secondDescriptor = new ProjectionDescriptor("second_schema");
        var structure = new ExchangeStructure(CreateHeader("first_schema", "second_schema"),
            [firstDescriptor, secondDescriptor]);
        var firstSection = new DataSection(firstDescriptor.Name, "first");
        var secondSection = new DataSection(secondDescriptor.Name, "second");
        structure.DataSections.Add(firstSection);
        structure.DataSections.Add(secondSection);
        structure.Add(firstSection, new EntityInstanceName("1"), new ProjectionEntity("FIRST", []));
        structure.Add(secondSection, new EntityInstanceName("2"), new ProjectionEntity("SECOND", []));
        structure.SetSchemaPopulations([
            new SchemaPopulationDefinition(
                firstDescriptor.Name,
                SchemaPopulationDetermination.SectionBoundary,
                [firstSection, secondSection],
                explicitSectionNames: null),
            new SchemaPopulationDefinition(
                secondDescriptor.Name,
                SchemaPopulationDetermination.IncludeReferenced,
                [secondSection],
                ["second"]),
        ]);
        var destination = new StringWriter();

        structure.Write(destination);
        var output = destination.ToString();
        var parse = ParserFixture.ParseStepText(output);

        using (Assert.Multiple())
        {
            await Assert.That(output).Contains("FILE_SCHEMA(('FIRST_SCHEMA','SECOND_SCHEMA'));\n");
            await Assert.That(output).Contains("FILE_POPULATION('FIRST_SCHEMA','SECTION_BOUNDARY',$);\n");
            await Assert.That(output).Contains(
                "FILE_POPULATION('SECOND_SCHEMA','INCLUDE_REFERENCED',('second'));\n");
            await Assert.That(output).Contains("DATA('first',('FIRST_SCHEMA'));\n#1=FIRST();\nENDSEC;\n");
            await Assert.That(output).Contains("DATA('second',('SECOND_SCHEMA'));\n#2=SECOND();\nENDSEC;\n");
            await Assert.That(parse.Errors).IsEmpty();
            await Assert.That(parse.ReachedEndOfFile).IsTrue();
        }
    }

    /// <summary>Preflights domain/capability failures before output and preserves destination I/O exceptions.</summary>
    [Test]
    public async Task Should_preserve_the_exact_write_failure_boundaries()
    {
        var descriptor = new ProjectionDescriptor("writer_custom");
        var invalid = CreateStructure(descriptor);
        var invalidSection = new DataSection(descriptor.Name);
        invalid.DataSections.Add(invalidSection);
        var missing = new ProjectionEntity("TARGET", []);
        var invalidRoot = new ProjectionEntity(
            "ROOT",
            [ParameterValue.FromEntity(missing)],
            missing);
        invalid.Add(invalidSection, invalidRoot);
        _ = invalid.Remove(missing);
        var invalidDestination = new ProbeTextWriter();
        var invalidException = Assert.Throws<ExchangeStructureWriteValidationException>(() =>
            invalid.Write(invalidDestination));

        var capabilityDescriptor = new ProjectionDescriptor("writer_custom", emitCapability: true);
        var unsupported = CreateStructure(capabilityDescriptor);
        var unsupportedDestination = new ProbeTextWriter();
        var capabilityException = Assert.Throws<ExchangeStructureCapabilityException>(() =>
            unsupported.Write(unsupportedDestination));

        var unprojectable = CreateStructure(descriptor);
        var unprojectableSection = new DataSection(descriptor.Name);
        unprojectable.DataSections.Add(unprojectableSection);
        unprojectable.Add(unprojectableSection, new UnknownProjectionEntity());
        var unprojectableDestination = new ProbeTextWriter();
        var projectionException = Assert.Throws<ExchangeStructureCapabilityException>(() =>
            unprojectable.Write(unprojectableDestination));

        var unnamed = CreateStructure(descriptor);
        unnamed.DataSections.Add(new DataSection(descriptor.Name));
        unnamed.DataSections.Add(new DataSection(descriptor.Name));
        var unnamedDestination = new ProbeTextWriter();
        var sectionException = Assert.Throws<ExchangeStructureWriteValidationException>(() =>
            unnamed.Write(unnamedDestination));

        var unbound = new ExchangeStructure(CreateHeader("absent_schema"));
        var unboundDestination = new ProbeTextWriter();
        var descriptorException = Assert.Throws<ExchangeStructureCapabilityException>(() =>
            unbound.Write(unboundDestination));

        var valid = CreateStructure(descriptor);
        var validSection = new DataSection(descriptor.Name);
        valid.DataSections.Add(validSection);
        var validEntity = new ProjectionEntity("ROOT", []);
        valid.Add(validSection, validEntity);
        var io = Assert.Throws<IOException>(() => valid.Write(new ThrowingTextWriter()));
        var unregisteredDestination = new ProbeTextWriter();
        var unregistered = Assert.Throws<ExchangeStructureWriteValidationException>(() =>
            valid.WriteEntity(unregisteredDestination, new ProjectionEntity("ROOT", [])));

        using (Assert.Multiple())
        {
            await Assert.That(invalidException.ValidationResult.Failures.Select(failure => failure.Code))
                .Contains("P21.STRUCTURE.REFERENCE.REGISTRATION");
            await Assert.That(invalidDestination.WriteCount).IsEqualTo(0);
            await Assert.That(capabilityException.Diagnostics.Select(diagnostic => diagnostic.Code))
                .IsEquivalentTo(["TEST-CAPABILITY"]);
            await Assert.That(unsupportedDestination.WriteCount).IsEqualTo(0);
            await Assert.That(projectionException.Diagnostics.Select(diagnostic => diagnostic.Code))
                .IsEquivalentTo(["P21-CAP-COMPLEX-ENTITY"]);
            await Assert.That(unprojectableDestination.WriteCount).IsEqualTo(0);
            await Assert.That(sectionException.ValidationResult.Failures.Select(failure => failure.Code))
                .IsEquivalentTo([
                    "P21.STRUCTURE.DATA_SECTION.NAME.REQUIRED",
                    "P21.STRUCTURE.DATA_SECTION.NAME.REQUIRED",
                ]);
            await Assert.That(unnamedDestination.WriteCount).IsEqualTo(0);
            await Assert.That(descriptorException.Diagnostics.Select(diagnostic => diagnostic.Code))
                .IsEquivalentTo(["P21-CAP-SCHEMA-DESCRIPTOR"]);
            await Assert.That(unboundDestination.WriteCount).IsEqualTo(0);
            await Assert.That(io.Message).IsEqualTo("probe write failure");
            await Assert.That(unregistered.ValidationResult.Failures.Select(failure => failure.Code))
                .IsEquivalentTo(["P21.WRITE.ENTITY.REGISTRATION"]);
            await Assert.That(unregisteredDestination.WriteCount).IsEqualTo(0);
        }
    }

    private static ExchangeStructure CreateStructure(SchemaDescriptor descriptor) =>
        new(CreateHeader(descriptor.Name.Value), [descriptor]);

    private static HeaderSection CreateHeader(params string[] schemas) => new(
        new FileDescription(["writer"], "3;1"),
        new FileName("writer.p21", "2026-08-22T00:00:00", ["Author"], ["Org"], "Pre", "System", "Auth"),
        new FileSchema(schemas));

    private static SchemaDescriptor CreateGeneratedDescriptor()
    {
        var result = GeneratorHostTests.Run(("schemas/writer.exp", WRITER_SCHEMA));
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
        return (SchemaDescriptor)assembly.GetType(
            "TedToolkit.Step21.Generated.WriterModel.SchemaDescriptor",
            throwOnError: true)!.GetProperty("Instance")!.GetValue(null)!;
    }

    private sealed class ProjectionEntity(
        string componentName,
        IReadOnlyList<ParameterValue> parameters,
        params Entity[] references) : Entity
    {
        internal string ComponentName { get; } = componentName;

        internal IReadOnlyList<ParameterValue> Parameters { get; } = parameters;

        public override IEnumerable<Entity> DirectReferences => references;
    }

    private sealed class UnknownProjectionEntity : Entity
    {
        public override IEnumerable<Entity> DirectReferences => [];
    }

    private sealed class ProjectionDescriptor(string name, bool emitCapability = false) : SchemaDescriptor
    {
        public override SchemaName Name { get; } = new(name);

        protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => null;

        protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
            ExchangeStructure structure,
            Entity value,
            IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) => [];

        protected override ValidationResult ValidateCore(
            ExchangeStructure structure,
            IReadOnlyList<KeyValuePair<string, Entity>> entities) => new([]);

        protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
            ExchangeStructure structure) => emitCapability
            ? [new Step21Diagnostic("TEST-CAPABILITY", Step21DiagnosticSeverity.Error, "Unsupported test projection.")]
            : [];

        protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
            Entity value) => value is ProjectionEntity entity
            ? [new(entity.ComponentName, entity.Parameters)]
            : [];
    }

    private sealed class ProbeTextWriter : StringWriter
    {
        internal int WriteCount { get; private set; }

        public override void Write(string? value)
        {
            WriteCount++;
            base.Write(value);
        }
    }

    private sealed class ThrowingTextWriter : StringWriter
    {
        public override void Write(string? value) => throw new IOException("probe write failure");
    }
}
