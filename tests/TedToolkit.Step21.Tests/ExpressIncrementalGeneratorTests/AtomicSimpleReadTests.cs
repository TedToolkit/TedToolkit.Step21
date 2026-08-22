using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>
/// Proves the atomic public read boundary for one simple schema-bound data section.
/// </summary>
public sealed class AtomicSimpleReadTests
{
    private const string SIMPLE_SCHEMA = """
        SCHEMA simple_read;
        TYPE identifier = STRING;
        END_TYPE;
        TYPE status = ENUMERATION OF (active, inactive);
        END_TYPE;
        TYPE text_choice = SELECT (identifier);
        END_TYPE;
        TYPE boolean_alias = BOOLEAN;
        END_TYPE;
        TYPE logical_alias = LOGICAL;
        END_TYPE;
        TYPE truth_choice = SELECT (boolean_alias, logical_alias);
        END_TYPE;
        ENTITY sample;
          integer_value : INTEGER;
          real_value : REAL;
          number_value : NUMBER;
          string_value : STRING;
          binary_value : BINARY;
          boolean_value : BOOLEAN;
          logical_value : LOGICAL;
          status_value : status;
          choice_value : text_choice;
          boolean_choice : truth_choice;
          logical_choice : truth_choice;
          optional_text : OPTIONAL STRING;
          present_text : OPTIONAL STRING;
          integer_values : LIST [1:?] OF INTEGER;
          boolean_values : LIST [1:?] OF BOOLEAN;
          logical_values : LIST [1:?] OF LOGICAL;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string VALIDATING_SCHEMA = """
        SCHEMA validating_read;
        ENTITY positive_value;
          amount : INTEGER;
        WHERE
          positive : amount > 0;
        END_ENTITY;
        END_SCHEMA;
        """;

    /// <summary>Reads all simple physical value families and publishes only the validated typed structure.</summary>
    [Test]
    public async Task Should_read_one_simple_data_section_with_exact_typed_values()
    {
        var descriptor = CreateDescriptor(SIMPLE_SCHEMA, "SimpleRead");
        const string source = """
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('O''Brien model'),'2;1');
            FILE_NAME('simple.p21','2026-08-22T00:00:00',('Author'),('Org'),'Pre','System','Auth');
            FILE_SCHEMA(('simple_read'));
            ENDSEC;
            DATA;
            #0001=SAMPLE(18446744073709551616,1.25,-7,'O''Brien\X2\03C0\X0\\X4\0001F600\X0\\PB\\S\!\PA\\S\!\N\line\F\form\\slash\X\A7',"3F",.T.,.U.,.ACTIVE.,IDENTIFIER('typed'),BOOLEAN_ALIAS(.F.),LOGICAL_ALIAS(.U.),$,'present',(1,2,3),(.F.,.T.),(.U.,.F.));
            ENDSEC;
            END-ISO-10303-21;
            """;

        var structure = ExchangeStructure.Read(new StringReader(source), [descriptor]);
        var registration = structure.Registrations.Single();
        var projected = descriptor.ProjectEntity(registration.Entity).Single().Value;
        var presentTextProperty = registration.Entity.GetType().GetProperty("PresentText")
            ?? throw new InvalidOperationException("The generated OPTIONAL property was not emitted.");
        presentTextProperty.SetValue(registration.Entity, "edited");
        var edited = descriptor.ProjectEntity(registration.Entity).Single().Value;

        using (Assert.Multiple())
        {
            await Assert.That(structure.Header.FileDescription.Description.SequenceEqual(["O'Brien model"])).IsTrue();
            await Assert.That(structure.Header.FileName.Name).IsEqualTo("simple.p21");
            await Assert.That(structure.Header.FileSchema.SchemaIdentifiers.SequenceEqual(["simple_read"])).IsTrue();
            await Assert.That(structure.DataSections).HasSingleItem();
            await Assert.That(structure.DataSections[0].SchemaName).IsEqualTo(new SchemaName("simple_read"));
            await Assert.That(registration.Name).IsEqualTo(new EntityInstanceName("1"));
            await Assert.That(projected[0].TryGetInteger(out var integer) && integer.ToString() == "18446744073709551616")
                .IsTrue();
            await Assert.That(projected[1].TryGetReal(out var real) && real == new RealValue(125, -2)).IsTrue();
            await Assert.That(projected[2].TryGetInteger(out var number) && number == -7).IsTrue();
            await Assert.That(projected[3].TryGetString(out var text)
                && text == "O'Brienπ😀Ą¡\nline\fform\\slash§").IsTrue();
            await Assert.That(projected[4].TryGetBinary(out var binary) && binary.ToString() == "1").IsTrue();
            await Assert.That(projected[5].TryGetBoolean(out var boolean) && boolean).IsTrue();
            await Assert.That(projected[6].TryGetLogical(out var logical) && logical == LogicalValue.Unknown).IsTrue();
            await Assert.That(projected[7].TryGetEnumeration(out var status) && status == "ACTIVE").IsTrue();
            await Assert.That(projected[8].TryGetTyped(out var typeName, out var selected)
                && typeName == "IDENTIFIER"
                && selected.TryGetString(out var selectedText)
                && selectedText == "typed").IsTrue();
            await Assert.That(projected[9].TryGetTyped(out var booleanType, out var selectedBoolean)
                && booleanType == "BOOLEAN_ALIAS"
                && selectedBoolean.TryGetBoolean(out var booleanChoice)
                && !booleanChoice).IsTrue();
            await Assert.That(projected[10].TryGetTyped(out var logicalType, out var selectedLogical)
                && logicalType == "LOGICAL_ALIAS"
                && selectedLogical.TryGetLogical(out var logicalChoice)
                && logicalChoice == LogicalValue.Unknown).IsTrue();
            await Assert.That(projected[11].Kind).IsEqualTo(ParameterValueKind.Omitted);
            await Assert.That(projected[12].TryGetString(out var presentText) && presentText == "present").IsTrue();
            await Assert.That(presentTextProperty.CanWrite).IsTrue();
            await Assert.That(edited[12].TryGetString(out var editedText) && editedText == "edited").IsTrue();
            await Assert.That(projected[13].TryGetAggregate(out var aggregate) && aggregate.Count == 3).IsTrue();
            await Assert.That(projected[14].TryGetAggregate(out var booleans)
                && booleans[0].TryGetBoolean(out var firstBoolean)
                && !firstBoolean
                && booleans[1].TryGetBoolean(out var secondBoolean)
                && secondBoolean).IsTrue();
            await Assert.That(projected[15].TryGetAggregate(out var logicals)
                && logicals[0].TryGetLogical(out var firstLogical)
                && firstLogical == LogicalValue.Unknown
                && logicals[1].TryGetLogical(out var secondLogical)
                && secondLogical == LogicalValue.False).IsTrue();
            await Assert.That(structure.Validate().IsValid).IsTrue();
        }
    }

    /// <summary>Rejects duplicate descriptors before making the first source read.</summary>
    [Test]
    public async Task Should_reject_duplicate_descriptors_before_consuming_source()
    {
        var descriptor = CreateDescriptor(SIMPLE_SCHEMA, "SimpleRead");
        var source = new ProbeTextReader();

        var exception = Assert.Throws<ArgumentException>(() =>
            ExchangeStructure.Read(source, [descriptor, descriptor]));

        using (Assert.Multiple())
        {
            await Assert.That(exception.ParamName).IsEqualTo("schemaDescriptors");
            await Assert.That(source.WasRead).IsFalse();
        }
    }

    /// <summary>Preserves syntax, binding, validation, capability, and I/O failure categories exactly.</summary>
    [Test]
    public async Task Should_fail_atomically_with_complete_stage_specific_evidence()
    {
        var simple = CreateDescriptor(SIMPLE_SCHEMA, "SimpleRead");
        var validating = CreateDescriptor(VALIDATING_SCHEMA, "ValidatingRead");

        var syntax = Assert.Throws<ExchangeStructureSyntaxException>(() =>
            ExchangeStructure.Read(new StringReader("not part 21"), [simple]));
        var mismatch = Assert.Throws<ExchangeStructureBindingException>(() =>
            ExchangeStructure.Read(new StringReader(CreateExchange("wrong_schema", "#1=SAMPLE(1,1.,1,'x',\"0\",.T.,.U.,$,(1));")), [simple]));
        var unknownEntity = Assert.Throws<ExchangeStructureBindingException>(() =>
            ExchangeStructure.Read(
                new StringReader(CreateExchange("simple_read", "#1=UNKNOWN();#2=SAMPLE();#3=ALSO_UNKNOWN();")),
                [simple]));
        var invalidValue = Assert.Throws<ExchangeStructureBindingException>(() =>
            ExchangeStructure.Read(
                new StringReader(CreateExchange(
                    "simple_read",
                    "#1=SAMPLE(1,1.,1,'x',\"3\",.T.,.U.,.ACTIVE.,IDENTIFIER('typed'),BOOLEAN_ALIAS(.F.),LOGICAL_ALIAS(.U.),$,'present',(1),(.T.),(.U.));")),
                [simple]));
        var invalidOccurrence = Assert.Throws<ExchangeStructureBindingException>(() =>
            ExchangeStructure.Read(
                new StringReader(CreateExchange(
                    "simple_read",
                    "#0=SAMPLE(1,1.,1,'x',\"0\",.T.,.U.,.ACTIVE.,IDENTIFIER('typed'),BOOLEAN_ALIAS(.F.),LOGICAL_ALIAS(.U.),$,'present',(1),(.T.),(.U.));")),
                [simple]));
        var invalid = Assert.Throws<ExchangeStructureReadValidationException>(() =>
            ExchangeStructure.Read(new StringReader(CreateExchange("validating_read", "#1=POSITIVE_VALUE(-1);")), [validating]));
        var unsupported = Assert.Throws<ExchangeStructureCapabilityException>(() =>
            ExchangeStructure.Read(
                new StringReader(CreateExchange("simple_read", string.Empty).Replace(
                    "FILE_SCHEMA(('simple_read'));",
                    "FILE_SCHEMA(('simple_read','other_schema'));",
                    StringComparison.Ordinal)),
                [simple]));
        var io = Assert.Throws<IOException>(() => ExchangeStructure.Read(new ThrowingTextReader(), [simple]));

        using (Assert.Multiple())
        {
            await Assert.That(syntax.Diagnostics).IsNotEmpty();
            await Assert.That(syntax.Diagnostics.All(diagnostic => diagnostic.SourceLocation?.FilePath == "<reader>"))
                .IsTrue();
            await Assert.That(mismatch.Diagnostics.Select(diagnostic => diagnostic.Code)).Contains("P21-BIND-SCHEMA");
            await Assert.That(unknownEntity.Diagnostics.Count(diagnostic => diagnostic.Code == "P21-BIND-ENTITY"))
                .IsEqualTo(2);
            await Assert.That(unknownEntity.Diagnostics.Select(diagnostic => diagnostic.Code))
                .Contains("P21-BIND-PARAMETER-COUNT");
            await Assert.That(unknownEntity.Diagnostics.All(diagnostic => diagnostic.SourceLocation?.FilePath == "<reader>"))
                .IsTrue();
            await Assert.That(unknownEntity.Diagnostics.All(diagnostic => diagnostic.SourceLocation is
                { Line: > 0, Column: > 0, })).IsTrue();
            await Assert.That(invalidValue.Diagnostics.Select(diagnostic => diagnostic.Code)).Contains("P21-BIND-VALUE");
            await Assert.That(invalidOccurrence.Diagnostics.Select(diagnostic => diagnostic.Code))
                .Contains("P21-BIND-OCCURRENCE");
            await Assert.That(invalid.ValidationResult.IsValid).IsFalse();
            await Assert.That(invalid.ValidationResult.Failures.Select(failure => failure.Code))
                .Contains("VALIDATING_READ.POSITIVE_VALUE.WHERE.POSITIVE");
            await Assert.That(unsupported.Diagnostics.Select(diagnostic => diagnostic.Code))
                .Contains("P21-CAP-SCHEMA-POPULATION");
            await Assert.That(io.Message).IsEqualTo("probe I/O failure");
        }
    }

    /// <summary>Exposes only the approved static method and no reader/context/raw-model facade.</summary>
    [Test]
    public async Task Should_expose_only_the_exact_public_read_entry_point()
    {
        var method = typeof(ExchangeStructure).GetMethod(
            "Read",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            binder: null,
            [typeof(TextReader), typeof(IReadOnlyCollection<SchemaDescriptor>)],
            modifiers: null);
        var assembly = typeof(ExchangeStructure).Assembly;

        using (Assert.Multiple())
        {
            await Assert.That(method?.ReturnType).IsEqualTo(typeof(ExchangeStructure));
            await Assert.That(assembly.GetExportedTypes().Where(type =>
                type.Name.Contains("Reader", StringComparison.Ordinal)
                || type.Name.Contains("Context", StringComparison.Ordinal)
                || type.Name.Contains("Syntax", StringComparison.Ordinal) && type != typeof(ExchangeStructureSyntaxException)))
                .IsEmpty();
            await Assert.That(assembly.GetExportedTypes().SelectMany(type =>
                type.GetNestedTypes(System.Reflection.BindingFlags.Public)))
                .IsEmpty();
        }
    }

    private static string CreateExchange(string schemaName, string records) => $$"""
        ISO-10303-21;
        HEADER;
        FILE_DESCRIPTION(('test'),'2;1');
        FILE_NAME('test.p21','2026-08-22T00:00:00',('Author'),('Org'),'Pre','System','Auth');
        FILE_SCHEMA(('{{schemaName}}'));
        ENDSEC;
        DATA;
        {{records}}
        ENDSEC;
        END-ISO-10303-21;
        """;

    private static SchemaDescriptor CreateDescriptor(string schema, string generatedNamespace)
    {
        var result = GeneratorHostTests.Run(("schemas/read.exp", schema));
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
            $"TedToolkit.Step21.Generated.{generatedNamespace}.SchemaDescriptor",
            throwOnError: true)!.GetProperty("Instance")!.GetValue(null)!;
    }

    private sealed class ProbeTextReader : TextReader
    {
        internal bool WasRead { get; private set; }

        public override int Read(char[] buffer, int index, int count)
        {
            WasRead = true;
            return 0;
        }
    }

    private sealed class ThrowingTextReader : TextReader
    {
        public override int Read(char[] buffer, int index, int count) =>
            throw new IOException("probe I/O failure");
    }
}
