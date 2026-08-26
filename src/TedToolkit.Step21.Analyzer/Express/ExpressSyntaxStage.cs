// -----------------------------------------------------------------------
// <copyright file="ExpressSyntaxStage.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express;

/// <summary>
/// Parses a closed source set into deterministic schema syntax and diagnostics.
/// </summary>
internal static class ExpressSyntaxStage
{
    /// <summary>
    /// Parses the supplied logical sources without consulting external state.
    /// </summary>
    /// <param name="sources">The complete logical source set.</param>
    /// <returns>The ordered parsed schemas and syntax diagnostics.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> is null.</exception>
    internal static ExpressSyntaxCompilation Parse(IEnumerable<(string FilePath, string Text)> sources)
    {
        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        var diagnostics = new List<ExpressSyntaxDiagnostic>();
        var schemas = new List<ExpressParsedSchema>();
        foreach (var source in sources
                     .OrderBy(source => source.FilePath, StringComparer.Ordinal)
                     .ThenBy(source => source.Text, StringComparer.Ordinal))
        {
            var parseResult = ExpressSyntaxParser.Parse(source.FilePath, source.Text);
            diagnostics.AddRange(parseResult.Diagnostics);
            if (parseResult.Root is null)
            {
                continue;
            }

            foreach (var syntax in parseResult.Root.Children
                         .OfType<ExpressRuleSyntax>()
                         .Where(child => child.Production == "schemaDecl"))
            {
                var nameToken = RequiredChild(syntax, "schemaId")
                    .DescendantTokens()
                    .First(token => token.TokenName == "SimpleId");
                schemas.Add(new(
                    syntax,
                    RequiredChild(syntax, "schemaBody"),
                    nameToken));
            }
        }

        return new(
            schemas,
            diagnostics
                .OrderBy(diagnostic => diagnostic.SourceLocation.FilePath, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.SourceLocation.Line)
                .ThenBy(diagnostic => diagnostic.SourceLocation.Column)
                .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal));
    }

    private static ExpressRuleSyntax RequiredChild(ExpressRuleSyntax node, string production)
    {
        return node.Children
            .OfType<ExpressRuleSyntax>()
            .Single(child => child.Production == production);
    }
}