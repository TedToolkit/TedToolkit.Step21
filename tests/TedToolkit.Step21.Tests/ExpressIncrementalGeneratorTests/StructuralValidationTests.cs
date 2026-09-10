using System.Xml.Linq;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>
/// Proves generated structural validation and its XML traceability contract.
/// </summary>
public sealed class StructuralValidationTests
{
    private const string VALIDATION_SCHEMA = """
        SCHEMA validation_model;
        CONSTANT
          lower_limit : INTEGER := 1;
          upper_limit : INTEGER := 3;
        END_CONSTANT;
        ENTITY target;
          label : STRING;
        END_ENTITY;
        TYPE label_type = STRING;
        END_TYPE;
        TYPE state = ENUMERATION OF (open, closed);
        END_TYPE;
        TYPE target_choice = SELECT (target);
        END_TYPE;
        ENTITY holder;
          required_name : STRING;
          optional_name : OPTIONAL STRING;
          target_ref : target;
          optional_ref : OPTIONAL target;
          values : LIST [2:3] OF UNIQUE STRING;
          candidates : SET [1:2] OF target;
          required_slots : ARRAY [1:2] OF UNIQUE STRING;
          optional_slots : OPTIONAL ARRAY [1:2] OF OPTIONAL STRING;
          nested : LIST [1:2] OF SET [1:2] OF STRING;
          entity_values : LIST [1:?] OF target;
          symbolic_values : LIST [lower_limit:upper_limit] OF UNIQUE target;
          nominal_label : label_type;
          state_value : state;
          choice_value : target_choice;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string VALIDATION_CONSUMER = """
        #nullable enable
        using System.Linq;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Schemas.ValidationModel;

        internal sealed class ForeignTarget : ITarget
        {
            public string Label { get; set; } = "foreign";
        }

        internal sealed class ForeignEntity : Entity
        {
            public override System.Collections.Generic.IEnumerable<Entity> DirectReferences => [];
        }

        internal sealed class ValidationEvidence
        {
            internal required ValidationResult First { get; init; }
            internal required ValidationResult Second { get; init; }
            internal required ExchangeStructure Structure { get; init; }
            internal required int ValueCountAfter { get; init; }
        }

        internal static class ValidationConsumer
        {
            internal static ValidationEvidence Exercise()
            {
                var target = new Target("valid");
                var values = new ExpressList<string>(2, 3, isUnique: true)
                {
                    "same",
                    "same",
                    null!,
                    "last",
                };
                var candidates = new ExpressSet<ITarget>(1, 2);
                var requiredSlots = new ExpressArray<string>(1, 2, isUnique: true);
                requiredSlots[1] = null!;
                var nested = new ExpressList<ExpressSet<string>>(1, 2)
                {
                    new ExpressSet<string>(1, 2),
                    null!,
                };
                var entityValues = new ExpressList<ITarget>(1) { new ForeignTarget() };
                var symbolicValues = new ExpressList<ITarget>(1, 3, isUnique: true)
                {
                    target,
                    target,
                    new ForeignTarget(),
                };
                var holder = new Holder(
                    "valid",
                    new ForeignTarget(),
                    values,
                    candidates,
                    requiredSlots,
                    nested,
                    entityValues,
                    symbolicValues,
                    default,
                    default,
                    TargetChoice.FromTarget(new ForeignTarget()));
                holder.RequiredName = null!;
                holder.OptionalName = null;
                holder.OptionalRef = target;
                holder.OptionalSlots = null;

                var structure = new ExchangeStructure(
                    TestHeader(),
                    [TedToolkit.Step21.Schemas.ValidationModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("validation_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, holder);
                target.Label = null!;

                var first = structure.Validate();
                var second = structure.Validate();
                return new ValidationEvidence
                {
                    First = first,
                    Second = second,
                    Structure = structure,
                    ValueCountAfter = values.Count,
                };
            }

            internal static ValidationResult ValidateWrongArrayShape()
            {
                var target = new Target("valid");
                var values = new ExpressList<string>(2, 3, isUnique: true) { "one", "two" };
                var candidates = new ExpressSet<ITarget>(1, 2) { target };
                var wrongSlots = new ExpressArray<string>(4, 5, isUnique: true);
                var nestedValues = new ExpressSet<string>(1, 2) { "nested" };
                var nested = new ExpressList<ExpressSet<string>>(1, 2) { nestedValues };
                var entityValues = new ExpressList<ITarget>(1) { target };
                var symbolicValues = new ExpressList<ITarget>(1, 3, isUnique: true) { target };
                var holder = new Holder(
                    "valid",
                    target,
                    values,
                    candidates,
                    wrongSlots,
                    nested,
                    entityValues,
                    symbolicValues,
                    new LabelType("valid"),
                    State.Open,
                    TargetChoice.FromTarget(target));
                var structure = new ExchangeStructure(
                    TestHeader(),
                    [TedToolkit.Step21.Schemas.ValidationModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("validation_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, holder);
                return structure.Validate();
            }

            internal static ValidationResult ValidateUnknownEntity()
            {
                var structure = new ExchangeStructure(
                    TestHeader(),
                    [TedToolkit.Step21.Schemas.ValidationModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("validation_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new ForeignEntity());
                return structure.Validate();
            }

            private static HeaderSection TestHeader() => new(
                new FileDescription(["validation"], "3;1"),
                new FileName(
                    "validation.step",
                    "2026-08-22T00:00:00+08:00",
                    [""],
                    [""],
                    "tests",
                    "tests",
                    ""),
                new FileSchema(["validation_model"]));
        }
        """;

    /// <summary>
    /// Verifies unchecked invalid edits remain observable and explicit validation aggregates every structural failure.
    /// </summary>
    [Test]
    public async Task Should_aggregate_deterministic_structural_failures_without_mutation_or_throwing()
    {
        var result = GeneratorHostTests.Run(
            VALIDATION_CONSUMER,
            ("schemas/validation.exp", VALIDATION_SCHEMA));
        var assembly = Emit(result.OutputCompilation);
        var evidence = assembly.GetType("ValidationConsumer", throwOnError: true)!
            .GetMethod("Exercise", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        var evidenceType = evidence.GetType();
        var first = (ValidationResult)evidenceType.GetProperty(
            "First",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(evidence)!;
        var second = (ValidationResult)evidenceType.GetProperty(
            "Second",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(evidence)!;
        var actual = first.Failures.Select(failure => (failure.Code, failure.Path)).ToArray();
        var expected = new (string Code, string Path)[]
        {
            ("VALIDATION_MODEL.HOLDER.REQUIRED_NAME.REQUIRED", "DataSections[0].#1.RequiredName"),
            ("VALIDATION_MODEL.HOLDER.TARGET_REF.ENTITY", "DataSections[0].#1.TargetRef"),
            ("VALIDATION_MODEL.HOLDER.VALUES.AGGREGATE_0.UPPER_BOUND", "DataSections[0].#1.Values"),
            ("VALIDATION_MODEL.HOLDER.VALUES.AGGREGATE_0.UNIQUE", "DataSections[0].#1.Values"),
            ("VALIDATION_MODEL.HOLDER.VALUES.AGGREGATE_0.ELEMENT", "DataSections[0].#1.Values[2]"),
            ("VALIDATION_MODEL.HOLDER.CANDIDATES.AGGREGATE_0.LOWER_BOUND", "DataSections[0].#1.Candidates"),
            ("VALIDATION_MODEL.HOLDER.REQUIRED_SLOTS.AGGREGATE_0.ELEMENT", "DataSections[0].#1.RequiredSlots[1]"),
            ("VALIDATION_MODEL.HOLDER.REQUIRED_SLOTS.AGGREGATE_0.REQUIRED_SLOT", "DataSections[0].#1.RequiredSlots[2]"),
            ("VALIDATION_MODEL.HOLDER.NESTED.AGGREGATE_1.LOWER_BOUND", "DataSections[0].#1.Nested[0]"),
            ("VALIDATION_MODEL.HOLDER.NESTED.AGGREGATE_0.ELEMENT", "DataSections[0].#1.Nested[1]"),
            ("VALIDATION_MODEL.HOLDER.ENTITY_VALUES.AGGREGATE_0.ELEMENT", "DataSections[0].#1.EntityValues[0]"),
            ("VALIDATION_MODEL.HOLDER.SYMBOLIC_VALUES.AGGREGATE_0.UNIQUE", "DataSections[0].#1.SymbolicValues"),
            ("VALIDATION_MODEL.HOLDER.SYMBOLIC_VALUES.AGGREGATE_0.ELEMENT", "DataSections[0].#1.SymbolicValues[2]"),
            ("VALIDATION_MODEL.HOLDER.NOMINAL_LABEL.REQUIRED", "DataSections[0].#1.NominalLabel"),
            ("VALIDATION_MODEL.HOLDER.STATE_VALUE.REQUIRED", "DataSections[0].#1.StateValue"),
            ("VALIDATION_MODEL.HOLDER.CHOICE_VALUE.ENTITY", "DataSections[0].#1.ChoiceValue"),
            ("VALIDATION_MODEL.TARGET.LABEL.REQUIRED", "DataSections[0].#2.Label"),
        };

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(first.IsValid).IsFalse();
            await Assert.That(actual.SequenceEqual(expected)).IsTrue();
            await Assert.That(second.Failures.Select(failure => (failure.Code, failure.Path)).SequenceEqual(expected))
                .IsTrue();
            await Assert.That(first.Failures.All(failure => failure.SourceLocation is
            { FilePath: "validation.exp", Line: > 0, Column: > 0, })).IsTrue();
            var structure = (ExchangeStructure)evidenceType.GetProperty(
                "Structure",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(evidence)!;
            await Assert.That(structure.Registrations.Count).IsEqualTo(2);
            await Assert.That((int)evidenceType.GetProperty(
                "ValueCountAfter",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(evidence)!)
                .IsEqualTo(4);
        }
    }

    /// <summary>
    /// Verifies wrong aggregate metadata and unknown registered CLR entities are failures rather than exceptions.
    /// </summary>
    [Test]
    public async Task Should_report_wrong_aggregate_shape_and_unknown_entity_without_throwing()
    {
        var result = GeneratorHostTests.Run(
            VALIDATION_CONSUMER,
            ("schemas/validation.exp", VALIDATION_SCHEMA));
        var assembly = Emit(result.OutputCompilation);
        var consumer = assembly.GetType("ValidationConsumer", throwOnError: true)!;
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var wrongShape = (ValidationResult)consumer.GetMethod("ValidateWrongArrayShape", flags)!.Invoke(null, null)!;
        var unknown = (ValidationResult)consumer.GetMethod("ValidateUnknownEntity", flags)!.Invoke(null, null)!;

        using (Assert.Multiple())
        {
            await Assert.That(wrongShape.Failures.Select(failure => failure.Code))
                .Contains("VALIDATION_MODEL.HOLDER.REQUIRED_SLOTS.AGGREGATE_0.SHAPE");
            await Assert.That(wrongShape.Failures.Count(failure =>
                failure.Code == "VALIDATION_MODEL.HOLDER.REQUIRED_SLOTS.AGGREGATE_0.REQUIRED_SLOT"))
                .IsEqualTo(2);
            await Assert.That(unknown.Failures).HasSingleItem();
            await Assert.That(unknown.Failures[0].Code)
                .IsEqualTo("VALIDATION_MODEL.STRUCTURE.ENTITY_ASSIGNABILITY");
            await Assert.That(unknown.Failures[0].Path).IsEqualTo("DataSections[0].#1");
        }
    }

    /// <summary>
    /// Verifies generated property XML and executable structural rules share stable IDs and normalized requirements.
    /// </summary>
    [Test]
    public async Task Should_generate_valid_deterministic_xml_for_every_structural_failure_code()
    {
        var first = GeneratorHostTests.Run(
            VALIDATION_CONSUMER,
            ("C:/agent-a/schemas/validation.exp", VALIDATION_SCHEMA));
        var second = GeneratorHostTests.Run(
            VALIDATION_CONSUMER,
            ("D:/agent-b/schemas/validation.exp", VALIDATION_SCHEMA));
        var holder = RequiredType(first.OutputCompilation, "TedToolkit.Step21.Schemas.ValidationModel.Holder");
        var target = RequiredType(first.OutputCompilation, "TedToolkit.Step21.Schemas.ValidationModel.Target");
        var descriptor = RequiredType(
            first.OutputCompilation,
            "TedToolkit.Step21.Schemas.ValidationModel.SchemaDescriptor");
        var labelType = RequiredType(first.OutputCompilation, "TedToolkit.Step21.Schemas.ValidationModel.LabelType");
        var state = RequiredType(first.OutputCompilation, "TedToolkit.Step21.Schemas.ValidationModel.State");
        var choice = RequiredType(first.OutputCompilation, "TedToolkit.Step21.Schemas.ValidationModel.TargetChoice");
        var documentation = holder.GetMembers().OfType<IPropertySymbol>()
            .Concat(target.GetMembers().OfType<IPropertySymbol>())
            .Select(property => property.GetDocumentationCommentXml())
            .Concat(new[]
            {
                holder.GetDocumentationCommentXml(),
                target.GetDocumentationCommentXml(),
                labelType.GetDocumentationCommentXml(),
                state.GetDocumentationCommentXml(),
                choice.GetDocumentationCommentXml(),
            })
            .Append(descriptor.GetDocumentationCommentXml())
            .Where(xml => !string.IsNullOrWhiteSpace(xml))
            .ToArray();
        var documentedCodes = documentation
            .SelectMany(xml => XDocument.Parse(xml!).Descendants("term"))
            .Select(term => term.Value.Trim())
            .Where(code => code != "Constraint ID")
            .ToHashSet(StringComparer.Ordinal);
        var emittedCodes = Regex.Matches(
                GeneratedSnapshot(first),
                "ValidationFailure\\(\\s*\"(?<code>[A-Z0-9_.]+)\"")
            .Select(match => match.Groups["code"].Value)
            .ToHashSet(StringComparer.Ordinal);
        var undocumentedCodes = emittedCodes.Except(documentedCodes, StringComparer.Ordinal).ToArray();
        var nonExecutableCodes = documentedCodes.Except(emittedCodes, StringComparer.Ordinal).ToArray();
        var combined = string.Join("\n", documentation);
        var assembly = Emit(first.OutputCompilation);
        var consumer = assembly.GetType("ValidationConsumer", throwOnError: true)!;
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var evidence = consumer.GetMethod("Exercise", flags)!.Invoke(null, null)!;
        var validation = (ValidationResult)evidence.GetType().GetProperty(
            "First",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(evidence)!;
        var unknown = (ValidationResult)consumer.GetMethod("ValidateUnknownEntity", flags)!.Invoke(null, null)!;
        var executedCodes = validation.Failures.Concat(unknown.Failures)
            .Select(failure => failure.Code)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(GeneratedSnapshot(first)).IsEqualTo(GeneratedSnapshot(second));
            await Assert.That(combined).Contains("Schema: validation_model");
            await Assert.That(combined).Contains("Declaration:");
            await Assert.That(combined).Contains("Declaration: ENTITY holder");
            await Assert.That(combined).Contains("Declaration: TYPE label_type");
            await Assert.That(combined).Contains("Normalized requirement:");
            await Assert.That(combined).Contains("Validation boundary:");
            await Assert.That(XDocument.Parse(descriptor.GetDocumentationCommentXml()!)
                .Descendants("see").Select(element => element.Attribute("cref")!.Value))
                .IsEquivalentTo(new[] { "T:TedToolkit.Step21.Schemas.ValidationModel.Target", "T:TedToolkit.Step21.Schemas.ValidationModel.Holder", });
            await Assert.That(combined).Contains("VALIDATION_MODEL.HOLDER.REQUIRED_NAME.REQUIRED");
            await Assert.That(combined).Contains("VALIDATION_MODEL.HOLDER.VALUES.AGGREGATE_0.UPPER_BOUND");
            await Assert.That(combined).Contains("VALIDATION_MODEL.HOLDER.VALUES.AGGREGATE_0.UNIQUE");
            await Assert.That(combined).Contains("VALIDATION_MODEL.HOLDER.VALUES.AGGREGATE_0.ELEMENT");
            await Assert.That(combined).Contains("VALIDATION_MODEL.HOLDER.REQUIRED_SLOTS.AGGREGATE_0.REQUIRED_SLOT");
            await Assert.That(combined).Contains("VALIDATION_MODEL.HOLDER.NESTED.AGGREGATE_1.LOWER_BOUND");
            await Assert.That(combined).Contains("VALIDATION_MODEL.HOLDER.ENTITY_VALUES.AGGREGATE_0.ELEMENT");
            await Assert.That(combined).Contains("VALIDATION_MODEL.HOLDER.SYMBOLIC_VALUES.AGGREGATE_0.UNIQUE");
            await Assert.That(combined).Contains("VALIDATION_MODEL.HOLDER.NOMINAL_LABEL.REQUIRED");
            await Assert.That(combined).Contains("VALIDATION_MODEL.HOLDER.STATE_VALUE.REQUIRED");
            await Assert.That(combined).Contains("VALIDATION_MODEL.HOLDER.CHOICE_VALUE.ENTITY");
            await Assert.That(combined).Contains("VALIDATION_MODEL.STRUCTURE.ENTITY_ASSIGNABILITY");
            await Assert.That(undocumentedCodes).IsEmpty();
            await Assert.That(nonExecutableCodes).IsEmpty();
            await Assert.That(executedCodes.All(documentedCodes.Contains)).IsTrue();
            await Assert.That(first.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
        }
    }

    private static INamedTypeSymbol RequiredType(Compilation compilation, string metadataName) =>
        compilation.GetTypeByMetadataName(metadataName)
        ?? throw new InvalidOperationException($"Generated type '{metadataName}' was not found.");

    private static string GeneratedSnapshot(GeneratorHostTests.GeneratorResult result) =>
        string.Join(
            "\n---\n",
            result.GeneratedSources
                .OrderBy(source => source.HintName, StringComparer.Ordinal)
                .Select(source => $"{source.HintName}\n{source.SourceText}"));

    private static System.Reflection.Assembly Emit(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));

        return System.Reflection.Assembly.Load(stream.ToArray());
    }
}
