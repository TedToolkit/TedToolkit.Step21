using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>Proves atomic typed reading across multiple data sections governed by one schema.</summary>
public sealed class SameSchemaDataSectionReadTests
{
    private const string SECTION_SCHEMA = """
        SCHEMA section_read;
        ENTITY node;
          label : STRING;
          next_node : OPTIONAL node;
        END_ENTITY;
        ENTITY positive_value;
          amount : INTEGER;
        WHERE
          positive : amount > 0;
        END_ENTITY;
        END_SCHEMA;
        """;

    /// <summary>Preserves section context while sharing occurrence identity across forward cyclic references.</summary>
    [Test]
    public async Task Should_bind_named_sections_into_one_identity_space()
    {
        var descriptor = CreateDescriptor();
        var structure = ExchangeStructure.Read(new StringReader(CreateExchange("""
            DATA('first',('section_read'));
            #1=NODE('one',#3);
            #2=NODE('two',#1);
            ENDSEC;
            DATA('second',('section_read'));
            #3=NODE('three',#2);
            ENDSEC;
            """)), [descriptor]);
        var registrations = structure.Registrations.ToDictionary(
            registration => registration.Name.CanonicalDigits,
            StringComparer.Ordinal);
        var first = registrations["1"].Entity;
        var second = registrations["2"].Entity;
        var third = registrations["3"].Entity;

        using (Assert.Multiple())
        {
            await Assert.That(structure.DataSections.Count).IsEqualTo(2);
            await Assert.That(structure.DataSections.Select(section => section.SchemaName).SequenceEqual([
                new SchemaName("section_read"),
                new SchemaName("section_read"),
            ])).IsTrue();
            await Assert.That(structure.DataSections.Select(section => section.Name).SequenceEqual([
                "first",
                "second",
            ])).IsTrue();
            await Assert.That(registrations["1"].DataSection).IsSameReferenceAs(structure.DataSections[0]);
            await Assert.That(registrations["2"].DataSection).IsSameReferenceAs(structure.DataSections[0]);
            await Assert.That(registrations["3"].DataSection).IsSameReferenceAs(structure.DataSections[1]);
            await Assert.That(first.GetType().GetProperty("NextNode")!.GetValue(first)).IsSameReferenceAs(third);
            await Assert.That(second.GetType().GetProperty("NextNode")!.GetValue(second)).IsSameReferenceAs(first);
            await Assert.That(third.GetType().GetProperty("NextNode")!.GetValue(third)).IsSameReferenceAs(second);
            await Assert.That(structure.Validate().IsValid).IsTrue();
        }
    }

    /// <summary>Aggregates malformed section contexts and structure-global duplicate occurrence names.</summary>
    [Test]
    public async Task Should_aggregate_every_invalid_section_context_and_duplicate_occurrence()
    {
        var descriptor = CreateDescriptor();
        var exception = Assert.Throws<ExchangeStructureBindingException>(() =>
            ExchangeStructure.Read(new StringReader(CreateExchange("""
                DATA;
                #1=NODE('one',$);
                ENDSEC;
                DATA('duplicate',('section_read'));
                #2=NODE('two',$);
                ENDSEC;
                DATA('duplicate',('wrong_schema'));
                #3=NODE('three',$);
                ENDSEC;
                DATA(1,('section_read','extra'));
                #4=NODE('four',$);
                ENDSEC;
                DATA('last',('section_read'));
                #2=NODE('duplicate occurrence',$);
                ENDSEC;
                """)), [descriptor]));

        using (Assert.Multiple())
        {
            await Assert.That(exception.Diagnostics.Count(diagnostic =>
                diagnostic.Code == "P21-BIND-DATA-SECTION")).IsEqualTo(5);
            await Assert.That(exception.Diagnostics.Count(diagnostic =>
                diagnostic.Code == "P21-BIND-OCCURRENCE")).IsEqualTo(1);
            await Assert.That(exception.Diagnostics.Where(diagnostic =>
                    diagnostic.Code == "P21-BIND-DATA-SECTION")
                .Select(diagnostic => diagnostic.Message)).IsEquivalentTo([
                    "DataSections[0] requires a section name and one governing schema name.",
                    "DataSections[2] repeats section name 'duplicate'.",
                    "DataSections[2] schema 'wrong_schema' does not occur in FILE_SCHEMA.",
                    "DataSections[3] parameter 0 must be a STRING section name.",
                    "DataSections[3] parameter 1 must be a list containing exactly one STRING schema name.",
                ]);
            await Assert.That(exception.Diagnostics.All(diagnostic => diagnostic.SourceLocation is
            { FilePath: "<reader>", Line: > 0, Column: > 0, })).IsTrue();
        }
    }

    /// <summary>Retains section-qualified paths for every unresolved occurrence across the population.</summary>
    [Test]
    public async Task Should_aggregate_reference_failures_across_sections()
    {
        var descriptor = CreateDescriptor();
        var exception = Assert.Throws<ExchangeStructureReadValidationException>(() =>
            ExchangeStructure.Read(new StringReader(CreateExchange("""
                DATA('first',('section_read'));
                #1=POSITIVE_VALUE(1);
                #3=NODE('one',#404);
                ENDSEC;
                DATA('second',('section_read'));
                #2=NODE('two',#1);
                #4=NODE('four',#405);
                ENDSEC;
                """)), [descriptor]));

        using (Assert.Multiple())
        {
            await Assert.That(exception.ValidationResult.Failures.Select(failure => failure.Code).SequenceEqual([
                "P21.READ.REFERENCE.MISSING",
                "P21.READ.REFERENCE.TYPE",
                "P21.READ.REFERENCE.MISSING",
            ])).IsTrue();
            await Assert.That(exception.ValidationResult.Failures.Select(failure => failure.Path).SequenceEqual([
                "DataSections[0].#3.Parameters[1]",
                "DataSections[1].#2.Parameters[1]",
                "DataSections[1].#4.Parameters[1]",
            ])).IsTrue();
        }
    }

    /// <summary>Validates every section under the shared descriptor before publishing any model.</summary>
    [Test]
    public async Task Should_aggregate_schema_validation_failures_across_sections()
    {
        var descriptor = CreateDescriptor();
        var exception = Assert.Throws<ExchangeStructureReadValidationException>(() =>
            ExchangeStructure.Read(new StringReader(CreateExchange("""
                DATA('first',('section_read'));
                #1=POSITIVE_VALUE(-1);
                ENDSEC;
                DATA('second',('section_read'));
                #2=POSITIVE_VALUE(-2);
                ENDSEC;
                """)), [descriptor]));
        var failures = exception.ValidationResult.Failures;

        using (Assert.Multiple())
        {
            await Assert.That(failures.Count).IsEqualTo(2);
            await Assert.That(failures.Select(failure => failure.Code).SequenceEqual([
                "SECTION_READ.POSITIVE_VALUE.WHERE.POSITIVE",
                "SECTION_READ.POSITIVE_VALUE.WHERE.POSITIVE",
            ])).IsTrue();
            await Assert.That(failures.Select(failure => failure.Path).SequenceEqual([
                "DataSections[0].#1",
                "DataSections[1].#2",
            ])).IsTrue();
        }
    }

    private static string CreateExchange(string sections) => $$"""
        ISO-10303-21;
        HEADER;
        FILE_DESCRIPTION(('section test'),'3;1');
        FILE_NAME('sections.p21','2026-08-22T00:00:00',('Author'),('Org'),'Pre','System','Auth');
        FILE_SCHEMA(('section_read'));
        ENDSEC;
        {{sections}}
        END-ISO-10303-21;
        """;

    private static SchemaDescriptor CreateDescriptor()
    {
        var result = GeneratorHostTests.Run(("schemas/section-read.exp", SECTION_SCHEMA));
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
            "TedToolkit.Step21.Schemas.SectionRead.SchemaDescriptor",
            throwOnError: true)!.GetProperty("Instance")!.GetValue(null)!;
    }
}
