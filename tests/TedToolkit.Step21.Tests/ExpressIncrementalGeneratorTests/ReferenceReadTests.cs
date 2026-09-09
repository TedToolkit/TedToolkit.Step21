using System.Collections;

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>
/// Proves two-phase direct-reference hydration and atomic aggregate read failures.
/// </summary>
public sealed class ReferenceReadTests
{
    private const string REFERENCE_SCHEMA = """
        SCHEMA reference_read;
        ENTITY node;
          label : STRING;
          next_node : OPTIONAL node;
          peer_node : OPTIONAL node;
        END_ENTITY;
        ENTITY other;
          label : STRING;
        END_ENTITY;
        ENTITY holder;
          target : node;
          optional_target : OPTIONAL node;
          targets : LIST [0:?] OF node;
        END_ENTITY;
        END_SCHEMA;
        """;

    /// <summary>Hydrates forward, backward, shared, aggregate, and cyclic references to actual generated objects.</summary>
    [Test]
    public async Task Should_hydrate_forward_shared_and_cyclic_references_by_object_identity()
    {
        var descriptor = CreateDescriptor();
        var structure = ExchangeStructure.Read(new StringReader(CreateExchange(
            referenceSection: string.Empty,
            records: """
                #1=NODE('one',#2,#3);
                #2=NODE('two',#1,$);
                #3=NODE('shared',$,$);
                #4=HOLDER(#2,$,(#3,#3));
                """)), [descriptor]);
        var entities = structure.Registrations.ToDictionary(
            registration => registration.Name.CanonicalDigits,
            registration => registration.Entity);
        var first = entities["1"];
        var second = entities["2"];
        var shared = entities["3"];
        var holder = entities["4"];
        var firstType = first.GetType();
        var secondType = second.GetType();
        var holderType = holder.GetType();
        var targets = ((IEnumerable)holderType.GetProperty("Targets")!.GetValue(holder)!).Cast<object>().ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(firstType.GetProperty("NextNode")!.GetValue(first)).IsSameReferenceAs(second);
            await Assert.That(firstType.GetProperty("NextNode")!.PropertyType.IsInterface).IsTrue();
            await Assert.That(firstType.GetProperty("NextNode")!.PropertyType.Name).IsEqualTo("INode");
            await Assert.That(firstType.GetProperty("PeerNode")!.GetValue(first)).IsSameReferenceAs(shared);
            await Assert.That(secondType.GetProperty("NextNode")!.GetValue(second)).IsSameReferenceAs(first);
            await Assert.That(holderType.GetProperty("Target")!.GetValue(holder)).IsSameReferenceAs(second);
            await Assert.That(holderType.GetProperty("Target")!.PropertyType.Name).IsEqualTo("INode");
            await Assert.That(holderType.GetProperty("OptionalTarget")!.GetValue(holder)).IsNull();
            await Assert.That(targets.Length).IsEqualTo(2);
            await Assert.That(targets[0]).IsSameReferenceAs(shared);
            await Assert.That(targets[1]).IsSameReferenceAs(shared);
            await Assert.That(first.DirectReferences.SequenceEqual([second, shared])).IsTrue();
            await Assert.That(structure.Validate().IsValid).IsTrue();
        }
    }

    /// <summary>Aggregates every incompatible direct and aggregate target as read-validation evidence.</summary>
    [Test]
    public async Task Should_aggregate_incompatible_targets_in_read_validation_result()
    {
        var descriptor = CreateDescriptor();
        var exception = Assert.Throws<ExchangeStructureReadValidationException>(() =>
            ExchangeStructure.Read(new StringReader(CreateExchange(
                referenceSection: """
                    REFERENCE;
                    #90=<https://example.com/external.p21#target>;
                    ENDSEC;
                    """,
                records: """
                    #1=OTHER('first');
                    #2=HOLDER(#1,$,(#1));
                    #3=OTHER('second');
                    #4=HOLDER(#3,$,());
                    #5=HOLDER(#1,#404,());
                    #6=HOLDER(#1,#90,(#1,#407));
                    """)), [descriptor]));
        var failures = exception.ValidationResult.Failures;

        using (Assert.Multiple())
        {
            await Assert.That(exception.ValidationResult.IsValid).IsFalse();
            await Assert.That(exception.GetType().GetProperties(
                    System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.DeclaredOnly)
                .Select(property => property.Name)).IsEquivalentTo(["ValidationResult"]);
            await Assert.That(failures.Count(failure => failure.Code == "P21.READ.REFERENCE.TYPE"))
                .IsEqualTo(6);
            await Assert.That(failures.Count(failure => failure.Code == "P21.READ.REFERENCE.MISSING"))
                .IsEqualTo(2);
            await Assert.That(failures.Count(failure => failure.Code == "P21.READ.REFERENCE.EXTERNAL"))
                .IsEqualTo(1);
            await Assert.That(failures.Select(failure => failure.Path).Distinct().Count()).IsEqualTo(9);
            await Assert.That(failures.Select(failure => failure.Path).SequenceEqual([
                "DataSections[0].#2.Parameters[0]",
                "DataSections[0].#2.Parameters[2]",
                "DataSections[0].#4.Parameters[0]",
                "DataSections[0].#5.Parameters[0]",
                "DataSections[0].#5.Parameters[1]",
                "DataSections[0].#6.Parameters[0]",
                "DataSections[0].#6.Parameters[1]",
                "DataSections[0].#6.Parameters[2]",
                "DataSections[0].#6.Parameters[2][1]",
            ])).IsTrue();
            await Assert.That(failures.Where(failure => failure.Code == "P21.READ.REFERENCE.TYPE").All(failure =>
                failure.Message.Contains("node", StringComparison.OrdinalIgnoreCase)))
                .IsTrue();
            await Assert.That(failures.All(failure => failure.SourceLocation is
            { FilePath: "<reader>", Line: > 0, Column: > 0, })).IsTrue();
        }
    }

    /// <summary>Distinguishes every missing occurrence from every declared but unsupported external occurrence.</summary>
    [Test]
    public async Task Should_aggregate_missing_and_external_reference_occurrences_separately()
    {
        var descriptor = CreateDescriptor();
        var exception = Assert.Throws<ExchangeStructureReadValidationException>(() =>
            ExchangeStructure.Read(new StringReader(CreateExchange(
                referenceSection: """
                    REFERENCE;
                    #90=<https://example.com/first.p21#target>;
                    #91=<https://example.com/second.p21#target>;
                    ENDSEC;
                    """,
                records: """
                    #1=HOLDER(#404,#90,(#405,#91));
                    #2=HOLDER(#406,$,(#407));
                    """)), [descriptor]));
        var failures = exception.ValidationResult.Failures;
        var missing = failures.Where(failure => failure.Code == "P21.READ.REFERENCE.MISSING").ToArray();
        var external = failures.Where(failure => failure.Code == "P21.READ.REFERENCE.EXTERNAL").ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(exception.ValidationResult.IsValid).IsFalse();
            await Assert.That(missing.Length).IsEqualTo(4);
            await Assert.That(external.Length).IsEqualTo(2);
            await Assert.That(failures.Select(failure => failure.Path).Distinct().Count()).IsEqualTo(6);
            await Assert.That(failures.Select(failure => failure.Code).SequenceEqual([
                "P21.READ.REFERENCE.MISSING",
                "P21.READ.REFERENCE.EXTERNAL",
                "P21.READ.REFERENCE.MISSING",
                "P21.READ.REFERENCE.EXTERNAL",
                "P21.READ.REFERENCE.MISSING",
                "P21.READ.REFERENCE.MISSING",
            ])).IsTrue();
            await Assert.That(missing.All(failure => failure.Message.Contains("not defined", StringComparison.Ordinal)))
                .IsTrue();
            await Assert.That(external.All(failure => failure.Message.Contains("external", StringComparison.Ordinal)))
                .IsTrue();
            await Assert.That(failures.All(failure => failure.SourceLocation is
            { FilePath: "<reader>", Line: > 0, Column: > 0, })).IsTrue();
        }
    }

    private static string CreateExchange(string referenceSection, string records) => $$"""
        ISO-10303-21;
        HEADER;
        FILE_DESCRIPTION(('reference test'),'4;2');
        FILE_NAME('references.p21','2026-08-22T00:00:00',('Author'),('Org'),'Pre','System','Auth');
        FILE_SCHEMA(('reference_read'));
        ENDSEC;
        {{referenceSection}}
        DATA;
        {{records}}
        ENDSEC;
        END-ISO-10303-21;
        """;

    private static SchemaDescriptor CreateDescriptor()
    {
        var result = GeneratorHostTests.Run(("schemas/reference-read.exp", REFERENCE_SCHEMA));
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
            "TedToolkit.Step21.Generated.ReferenceRead.SchemaDescriptor",
            throwOnError: true)!.GetProperty("Instance")!.GetValue(null)!;
    }
}