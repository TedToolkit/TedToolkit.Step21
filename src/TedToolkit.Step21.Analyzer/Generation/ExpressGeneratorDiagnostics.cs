// -----------------------------------------------------------------------
// <copyright file="ExpressGeneratorDiagnostics.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using TedToolkit.Step21.Analyzer.Express;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Bridges internal EXPRESS failures to source-located Roslyn diagnostics.
/// </summary>
internal static class ExpressGeneratorDiagnostics
{
    private static readonly DiagnosticDescriptor _syntaxFailure = new(
        "STEP21EXP001",
        "Invalid EXPRESS syntax",
        "{0}: {1}",
        "TedToolkit.Step21.Express",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _bindingFailure = new(
        "STEP21EXP002",
        "Invalid EXPRESS schema binding",
        "{0}: {1}",
        "TedToolkit.Step21.Express",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _unreadableInput = new(
        "STEP21EXP003",
        "Unreadable EXPRESS additional file",
        "Roslyn could not read EXPRESS AdditionalFile '{0}'",
        "TedToolkit.Step21.Express",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _generatedNameCollision = new(
        "STEP21EXP004",
        "Generated C# name collision",
        "EXPRESS name '{0}' maps to generated C# name '{1}', which is not unique in schema '{2}'",
        "TedToolkit.Step21.Express",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _unsupportedEntityProjection = new(
        "STEP21EXP005",
        "Unsupported generated entity projection",
        "{0}",
        "TedToolkit.Step21.Express",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _unsupportedReachableRule = new(
        "STEP21EXP006",
        "Unsupported reachable EXPRESS rule",
        "{0}",
        "TedToolkit.Step21.Express",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Reports every input, syntax, and binding failure in deterministic order.
    /// </summary>
    /// <param name="context">The source-production context.</param>
    /// <param name="result">The complete generator compilation.</param>
    internal static void Report(in SourceProductionContext context, ExpressGeneratorCompilation result)
    {
        foreach (var input in result.Inputs
                     .Where(input => input.Text is null)
                     .OrderBy(input => input.Path, StringComparer.Ordinal))
        {
            context.ReportDiagnostic(Diagnostic.Create(_unreadableInput, Location.None, input.Path));
        }

        foreach (var diagnostic in result.Compilation.SyntaxDiagnostics)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                _syntaxFailure,
                CreateLocation(result.Inputs, diagnostic.SourceLocation),
                diagnostic.Code,
                diagnostic.Message));
        }

        foreach (var diagnostic in result.Compilation.BindingDiagnostics)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                _bindingFailure,
                CreateLocation(result.Inputs, diagnostic.SourceLocation),
                diagnostic.Code,
                diagnostic.Message));
        }
    }

    /// <summary>
    /// Reports generated C# name collisions in deterministic source order.
    /// </summary>
    /// <param name="context">The source-production context.</param>
    /// <param name="result">The complete generator compilation.</param>
    /// <param name="collisions">The detected name collisions.</param>
    internal static void ReportNameCollisions(
        in SourceProductionContext context,
        ExpressGeneratorCompilation result,
        IEnumerable<ExpressEntityGenerationCollision> collisions)
    {
        foreach (var collision in collisions
                     .OrderBy(item => item.Location.FilePath, StringComparer.Ordinal)
                     .ThenBy(item => item.Location.Line)
                     .ThenBy(item => item.Location.Column)
                     .ThenBy(item => item.GeneratedName, StringComparer.Ordinal))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                _generatedNameCollision,
                CreateLocation(result.Inputs, collision.Location),
                collision.SourceName,
                collision.GeneratedName,
                collision.Schema.Name));
        }
    }

    /// <summary>
    /// Reports entity shapes that cannot be represented safely by the generated C# contract.
    /// </summary>
    /// <param name="context">The source-production context.</param>
    /// <param name="result">The complete generator compilation.</param>
    /// <param name="failures">The detected generation failures.</param>
    internal static void ReportGenerationFailures(
        in SourceProductionContext context,
        ExpressGeneratorCompilation result,
        IEnumerable<ExpressEntityGenerationFailure> failures)
    {
        foreach (var failure in failures
                     .OrderBy(item => item.Location.FilePath, StringComparer.Ordinal)
                     .ThenBy(item => item.Location.Line)
                     .ThenBy(item => item.Location.Column))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                _unsupportedEntityProjection,
                CreateLocation(result.Inputs, failure.Location),
                failure.Message));
        }
    }

    /// <summary>
    /// Reports reachable rule closures that cannot be generated without a validation gap.
    /// </summary>
    /// <param name="context">The source-production context.</param>
    /// <param name="result">The complete generator compilation.</param>
    /// <param name="failures">The source-located reachable-rule failures.</param>
    internal static void ReportReachableRuleFailures(
        in SourceProductionContext context,
        ExpressGeneratorCompilation result,
        IEnumerable<ExpressEntityGenerationFailure> failures)
    {
        foreach (var failure in failures
                     .OrderBy(item => item.Location.FilePath, StringComparer.Ordinal)
                     .ThenBy(item => item.Location.Line)
                     .ThenBy(item => item.Location.Column))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                _unsupportedReachableRule,
                CreateLocation(result.Inputs, failure.Location),
                failure.Message));
        }
    }

    private static Location CreateLocation(
        IEnumerable<ExpressGeneratorInput> inputs,
        ExpressSourceLocation sourceLocation)
    {
        var input = inputs.FirstOrDefault(candidate => string.Equals(
            candidate.Path,
            sourceLocation.FilePath,
            StringComparison.Ordinal));
        if (input.Text is null)
        {
            return Location.None;
        }

        var sourceText = SourceText.From(input.Text);
        var lineIndex = Math.Max(0, Math.Min(sourceLocation.Line - 1, sourceText.Lines.Count - 1));
        var line = sourceText.Lines[lineIndex];
        var column = Math.Max(0, Math.Min(sourceLocation.Column - 1, line.Span.Length));
        var linePosition = new LinePosition(lineIndex, column);
        return Location.Create(
            sourceLocation.FilePath,
            new TextSpan(line.Start + column, 0),
            new LinePositionSpan(linePosition, linePosition));
    }
}