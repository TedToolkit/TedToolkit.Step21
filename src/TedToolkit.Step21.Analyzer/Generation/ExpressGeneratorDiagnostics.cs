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