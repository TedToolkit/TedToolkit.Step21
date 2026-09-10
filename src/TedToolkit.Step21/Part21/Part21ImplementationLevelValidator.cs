using TedToolkit.Step21.Syntax;

namespace TedToolkit.Step21;

/// <summary>Validates the declared implementation level against used ISO 10303-21 facilities.</summary>
internal static class Part21ImplementationLevelValidator
{
    private const string DiagnosticCode = "P21-CONFORMANCE-IMPLEMENTATION-LEVEL";

    internal static Part21ReadFeatureScan GetReadFeatureScan(ExchangeStructureSyntax syntax)
    {
        Guard.NotNull(syntax);
        if (syntax.Header.FileDescription.Parameters.Count < 2
            || syntax.Header.FileDescription.Parameters[1].Kind != Part21ValueKind.String)
        {
            return Part21ReadFeatureScan.None;
        }

        return syntax.Header.FileDescription.Parameters[1].Text switch
        {
            "'4;3'" => Part21ReadFeatureScan.None,
            "'4;1'" or "'4;2'" => Part21ReadFeatureScan.Class3Occurrence,
            "'3;1'" or "'2;1'" => Part21ReadFeatureScan.Class3Occurrence | Part21ReadFeatureScan.RawHighString,
            _ => Part21ReadFeatureScan.Class3Occurrence | Part21ReadFeatureScan.RawHighString,
        };
    }

    internal static void ValidateForRead(
        ExchangeStructureSyntax syntax,
        Part21ReadGraphFeatures graphFeatures)
    {
        Guard.NotNull(syntax);
        if (!TryReadLevel(syntax.Header.FileDescription, out var level))
            return;

        var diagnostics = new List<Step21Diagnostic>();
        ValidateKnownLevel(level, syntax.Header.FileDescription.Span.Start, diagnostics);
        if (diagnostics.Count == 0)
            ValidateReadFeatures(syntax, level, graphFeatures, diagnostics);
        ThrowIfAny(diagnostics);
    }

    internal static void ValidateForWrite(
        ExchangeStructure structure,
        Part21WriteGraphFeatures graphFeatures,
        bool writesSignature,
        Part21StringEncoding stringEncoding)
    {
        Guard.NotNull(structure);

        var level = structure.Header.FileDescription.ImplementationLevel;
        var diagnostics = new List<Step21Diagnostic>();
        ValidateKnownLevel(level, sourceLocation: null, diagnostics);
        if (diagnostics.Count == 0)
            ValidateWriteFeatures(structure, graphFeatures, writesSignature, stringEncoding, level, diagnostics);
        ThrowIfAny(diagnostics);
    }

    internal static void ValidateForMultiFileZip(ExchangeStructureSyntax syntax)
    {
        Guard.NotNull(syntax);
        if (!TryReadLevel(syntax.Header.FileDescription, out var level))
            return;
        if (level is not ("4;1" or "3;1" or "2;1"))
            return;

        var diagnostics = new List<Step21Diagnostic>();
        Add(diagnostics, level, "a multi-file ZIP structure", syntax.Header.FileDescription.Span.Start);
        ThrowIfAny(diagnostics);
    }

    private static bool TryReadLevel(HeaderEntitySyntax fileDescription, out string level)
    {
        level = string.Empty;
        if (fileDescription.Parameters.Count < 2
            || fileDescription.Parameters[1].Kind != Part21ValueKind.String)
        {
            return false;
        }

        try
        {
            level = fileDescription.Parameters[1].Text switch
            {
                "'4;1'" => "4;1",
                "'4;2'" => "4;2",
                "'4;3'" => "4;3",
                "'3;1'" => "3;1",
                "'2;1'" => "2;1",
                var token => Part21LexicalValueDecoder.DecodeString(token),
            };
            return true;
        }
        catch (Exception exception) when (exception is FormatException
            or ArgumentOutOfRangeException
            or OverflowException)
        {
            return false;
        }
    }

    private static void ValidateKnownLevel(
        string level,
        SourceLocation? sourceLocation,
        ICollection<Step21Diagnostic> diagnostics)
    {
        if (level is "4;1" or "4;2" or "4;3" or "3;1" or "2;1")
            return;

        diagnostics.Add(new Step21Diagnostic(
            DiagnosticCode,
            Step21DiagnosticSeverity.Error,
            $"Implementation level '{level}' is not one of 4;1, 4;2, 4;3, 3;1, or 2;1.",
            sourceLocation));
    }

    private static void ValidateReadFeatures(
        ExchangeStructureSyntax syntax,
        string level,
        Part21ReadGraphFeatures graphFeatures,
        ICollection<Step21Diagnostic> diagnostics)
    {
        if ((level is "4;1" or "3;1" or "2;1") && syntax.Reference is not null)
            Add(diagnostics, level, "a reference section", syntax.Reference!.Span.Start);

        if (level != "4;3" && graphFeatures.Class3Occurrence is not null)
        {
            Add(
                diagnostics,
                level,
                "a value-instance or EXPRESS-constant occurrence",
                graphFeatures.Class3Occurrence.Span.Start);
        }

        if (level is not ("3;1" or "2;1"))
            return;

        if (syntax.Anchor is not null)
            Add(diagnostics, level, "an anchor section", syntax.Anchor.Span.Start);
        if (syntax.SignatureSections.Count > 0)
            Add(diagnostics, level, "a signature section", syntax.SignatureSections[0].Span.Start);

        var additional = syntax.Header.AdditionalEntities;
        var schemaPopulation = additional.FirstOrDefault(entity =>
            string.Equals(entity.Name, "SCHEMA_POPULATION", StringComparison.OrdinalIgnoreCase));
        if (schemaPopulation is not null)
            Add(diagnostics, level, "a SCHEMA_POPULATION header entity", schemaPopulation.Span.Start);

        if (graphFeatures.RawHighString is not null)
            Add(diagnostics, level, "a raw UTF-8 string character", graphFeatures.RawHighString.Span.Start);

        if (level != "2;1")
            return;

        if (syntax.DataSections.Count != 1 || syntax.DataSections[0].Parameters.Count != 0)
        {
            Add(
                diagnostics,
                level,
                "anything other than one unparameterized DATA section",
                syntax.DataSections.FirstOrDefault()?.Span.Start ?? syntax.Span.Start);
        }

        foreach (var name in new[] { "FILE_POPULATION", "SECTION_LANGUAGE", "SECTION_CONTEXT" })
        {
            var entity = additional.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
            if (entity is not null)
                Add(diagnostics, level, $"a {name} header entity", entity.Span.Start);
        }
    }

    private static void ValidateWriteFeatures(
        ExchangeStructure structure,
        Part21WriteGraphFeatures graphFeatures,
        bool writesSignature,
        Part21StringEncoding stringEncoding,
        string level,
        ICollection<Step21Diagnostic> diagnostics)
    {
        if (level == "4;3")
            return;

        var usesClass3Occurrence = structure.ReferenceEntries.Any(
            reference => reference.Kind == Part21ReferenceKind.ValueInstance)
            || graphFeatures.UsesClass3Occurrence;
        var checksRawUtf8 = (level is "3;1" or "2;1")
            && stringEncoding == Part21StringEncoding.Utf8;
        var usesRawUtf8 = checksRawUtf8 && graphFeatures.ContainsHighString;

        if ((level is "4;1" or "3;1" or "2;1") && structure.ReferenceEntries.Count > 0)
            Add(diagnostics, level, "a reference section", sourceLocation: null);
        if (level is not "4;3" && usesClass3Occurrence)
            Add(diagnostics, level, "a value-instance or EXPRESS-constant occurrence", sourceLocation: null);

        if (level is not ("3;1" or "2;1"))
            return;

        if (structure.AnchorEntries.Count > 0)
            Add(diagnostics, level, "an anchor section", sourceLocation: null);
        if (structure.SchemaPopulationExternalFiles.Count > 0)
            Add(diagnostics, level, "a SCHEMA_POPULATION header entity", sourceLocation: null);
        if (structure.Signatures.Count > 0 || writesSignature)
            Add(diagnostics, level, "a signature section", sourceLocation: null);
        if (usesRawUtf8)
            Add(diagnostics, level, "a raw UTF-8 string character", sourceLocation: null);

        if (level == "2;1"
            && (structure.DataSections.Count != 1 || structure.DataSections[0].Name is not null))
        {
            Add(
                diagnostics,
                level,
                "anything other than one unparameterized DATA section",
                sourceLocation: null);
        }

        if (level == "2;1" && structure.SchemaPopulations.Count > 0)
            Add(diagnostics, level, "a FILE_POPULATION header entity", sourceLocation: null);
        if (level == "2;1" && structure.SectionLanguageEntries.Count > 0)
            Add(diagnostics, level, "a SECTION_LANGUAGE header entity", sourceLocation: null);
        if (level == "2;1" && structure.SectionContextEntries.Count > 0)
            Add(diagnostics, level, "a SECTION_CONTEXT header entity", sourceLocation: null);
    }

    private static void Add(
        ICollection<Step21Diagnostic> diagnostics,
        string level,
        string facility,
        SourceLocation? sourceLocation)
    {
        diagnostics.Add(new Step21Diagnostic(
            DiagnosticCode,
            Step21DiagnosticSeverity.Error,
            $"Implementation level '{level}' cannot encode {facility}.",
            sourceLocation));
    }

    private static void ThrowIfAny(IReadOnlyCollection<Step21Diagnostic> diagnostics)
    {
        if (diagnostics.Count > 0)
            throw new ExchangeStructureCapabilityException(diagnostics);
    }
}
