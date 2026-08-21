namespace TedToolkit.Step21.Tests.ValidationContractTests;

internal sealed class BehaviorTests
{
    /// <summary>
    /// Verifies that a source location preserves the approved three-member 1-based position.
    /// </summary>
    [Test]
    public async Task Should_preserve_minimal_position_when_source_location_is_created()
    {
        var location = new SourceLocation("schemas/example.exp", 12, 5);

        using (Assert.Multiple())
        {
            await Assert.That(location.FilePath).IsEqualTo("schemas/example.exp");
            await Assert.That(location.Line).IsEqualTo(12);
            await Assert.That(location.Column).IsEqualTo(5);
        }
    }

    /// <summary>
    /// Verifies that line and column reject values outside their 1-based domain.
    /// </summary>
    [Test]
    [Arguments(0, 1)]
    [Arguments(-1, 1)]
    [Arguments(1, 0)]
    [Arguments(1, -1)]
    public async Task Should_reject_position_when_line_or_column_is_not_positive(int line, int column)
    {
        void CreateLocation() => _ = new SourceLocation("schema.exp", line, column);

        await Assert.That((Action)CreateLocation).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that diagnostic evidence preserves every approved value and optional location.
    /// </summary>
    [Test]
    [Arguments(Step21DiagnosticSeverity.Information)]
    [Arguments(Step21DiagnosticSeverity.Warning)]
    [Arguments(Step21DiagnosticSeverity.Error)]
    public async Task Should_preserve_evidence_when_diagnostic_is_created(Step21DiagnosticSeverity severity)
    {
        var location = new SourceLocation("model.p21", 7, 3);
        var diagnostic = new Step21Diagnostic("P21-001", severity, "Diagnostic message.", location);

        using (Assert.Multiple())
        {
            await Assert.That(diagnostic.Code).IsEqualTo("P21-001");
            await Assert.That(diagnostic.Severity).IsEqualTo(severity);
            await Assert.That(diagnostic.Message).IsEqualTo("Diagnostic message.");
            await Assert.That(ReferenceEquals(diagnostic.SourceLocation, location)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that validation failures preserve deterministic identity, path, message, and source evidence.
    /// </summary>
    [Test]
    public async Task Should_preserve_evidence_when_validation_failure_is_created()
    {
        var location = new SourceLocation("schema.exp", 20, 9);
        var failure = new ValidationFailure("EXAMPLE.WR1", "DataSections[0].#42.Length", "Invalid length.", location);

        using (Assert.Multiple())
        {
            await Assert.That(failure.Code).IsEqualTo("EXAMPLE.WR1");
            await Assert.That(failure.Path).IsEqualTo("DataSections[0].#42.Length");
            await Assert.That(failure.Message).IsEqualTo("Invalid length.");
            await Assert.That(ReferenceEquals(failure.SourceLocation, location)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that required textual evidence rejects null rather than exposing it through non-null properties.
    /// </summary>
    [Test]
    public async Task Should_reject_null_when_required_evidence_text_is_created()
    {
        static void CreateLocation() => _ = new SourceLocation(null!, 1, 1);
        static void CreateDiagnosticWithNullCode() =>
            _ = new Step21Diagnostic(null!, Step21DiagnosticSeverity.Error, "Message");
        static void CreateDiagnosticWithNullMessage() =>
            _ = new Step21Diagnostic("P21-001", Step21DiagnosticSeverity.Error, null!);
        static void CreateFailureWithNullCode() => _ = new ValidationFailure(null!, "#1", "Message");
        static void CreateFailureWithNullPath() => _ = new ValidationFailure("EXAMPLE.WR1", null!, "Message");
        static void CreateFailureWithNullMessage() => _ = new ValidationFailure("EXAMPLE.WR1", "#1", null!);

        using (Assert.Multiple())
        {
            await Assert.That((Action)CreateLocation).Throws<ArgumentNullException>();
            await Assert.That((Action)CreateDiagnosticWithNullCode).Throws<ArgumentNullException>();
            await Assert.That((Action)CreateDiagnosticWithNullMessage).Throws<ArgumentNullException>();
            await Assert.That((Action)CreateFailureWithNullCode).Throws<ArgumentNullException>();
            await Assert.That((Action)CreateFailureWithNullPath).Throws<ArgumentNullException>();
            await Assert.That((Action)CreateFailureWithNullMessage).Throws<ArgumentNullException>();
        }
    }

    /// <summary>
    /// Verifies that a validation result snapshots all failures in supplied deterministic order.
    /// </summary>
    [Test]
    public async Task Should_snapshot_ordered_failures_when_validation_result_is_created()
    {
        var first = new ValidationFailure("EXAMPLE.WR1", "#1.A", "First failure.");
        var second = new ValidationFailure("EXAMPLE.WR2", "#2.B", "Second failure.");
        var supplied = new List<ValidationFailure> { first, second };
        var result = new ValidationResult(supplied);

        supplied.Clear();
        void MutateSnapshot() => ((IList<ValidationFailure>)result.Failures).Clear();

        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(result.Failures.Count).IsEqualTo(2);
            await Assert.That(ReferenceEquals(result.Failures[0], first)).IsTrue();
            await Assert.That(ReferenceEquals(result.Failures[1], second)).IsTrue();
            await Assert.That((Action)MutateSnapshot).Throws<NotSupportedException>();
        }
    }

    /// <summary>
    /// Verifies that an empty validation result is valid and can be inspected without side effects or exceptions.
    /// </summary>
    [Test]
    public async Task Should_be_valid_when_validation_result_contains_no_failure()
    {
        var result = new ValidationResult([]);

        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsTrue();
            await Assert.That(result.Failures.Count).IsEqualTo(0);
        }
    }

    /// <summary>
    /// Verifies that syntax, binding, and capability exceptions snapshot complete ordered diagnostics.
    /// </summary>
    [Test]
    public async Task Should_snapshot_diagnostics_when_diagnostic_stage_exception_is_created()
    {
        var first = new Step21Diagnostic("P21-001", Step21DiagnosticSeverity.Warning, "First diagnostic.");
        var second = new Step21Diagnostic("P21-002", Step21DiagnosticSeverity.Error, "Second diagnostic.");
        var supplied = new List<Step21Diagnostic> { first, second };

        var syntax = new ExchangeStructureSyntaxException(supplied);
        var binding = new ExchangeStructureBindingException(supplied);
        var capability = new ExchangeStructureCapabilityException(supplied);
        supplied.Clear();

        foreach (var diagnostics in new[] { syntax.Diagnostics, binding.Diagnostics, capability.Diagnostics })
        {
            using (Assert.Multiple())
            {
                await Assert.That(diagnostics.Count).IsEqualTo(2);
                await Assert.That(ReferenceEquals(diagnostics[0], first)).IsTrue();
                await Assert.That(ReferenceEquals(diagnostics[1], second)).IsTrue();
                void MutateSnapshot() => ((IList<Step21Diagnostic>)diagnostics).Clear();
                await Assert.That((Action)MutateSnapshot).Throws<NotSupportedException>();
            }
        }
    }

    /// <summary>
    /// Verifies that read and write validation exceptions retain the same complete result instance.
    /// </summary>
    [Test]
    public async Task Should_retain_complete_result_when_validation_stage_exception_is_created()
    {
        var result = new ValidationResult(
        [
            new ValidationFailure("EXAMPLE.WR1", "#1.A", "Invalid value."),
        ]);
        var read = new ExchangeStructureReadValidationException(result);
        var write = new ExchangeStructureWriteValidationException(result);

        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(read.ValidationResult, result)).IsTrue();
            await Assert.That(ReferenceEquals(write.ValidationResult, result)).IsTrue();
            await Assert.That(read.ValidationResult.Failures.Count).IsEqualTo(1);
            await Assert.That(write.ValidationResult.Failures.Count).IsEqualTo(1);
        }
    }
}