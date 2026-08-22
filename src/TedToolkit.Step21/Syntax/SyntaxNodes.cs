namespace TedToolkit.Step21.Syntax;

// Internal spans are half-open: Start is inclusive and End is exclusive.
internal sealed class Part21SourceSpan
{
    internal Part21SourceSpan(SourceLocation start, SourceLocation end)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);
        if (!string.Equals(start.FilePath, end.FilePath, StringComparison.Ordinal))
            throw new ArgumentException("A source span must remain within one file.", nameof(end));
        if (end.Line < start.Line || end.Line == start.Line && end.Column < start.Column)
            throw new ArgumentException("The source-span end must not precede its start.", nameof(end));

        Start = start;
        End = end;
    }

    internal SourceLocation Start { get; }

    internal SourceLocation End { get; }
}

internal abstract class Part21SyntaxNode
{
    protected Part21SyntaxNode(Part21SourceSpan span)
    {
        ArgumentNullException.ThrowIfNull(span);
        Span = span;
    }

    internal Part21SourceSpan Span { get; }
}

// This graph preserves physical exchange syntax only; schema meaning belongs to the later binder.
internal sealed class ExchangeStructureSyntax : Part21SyntaxNode
{
    internal ExchangeStructureSyntax(
        HeaderSectionSyntax header,
        AnchorSectionSyntax? anchor,
        ReferenceSectionSyntax? reference,
        IEnumerable<DataSectionSyntax> dataSections,
        IEnumerable<SignatureSectionSyntax> signatureSections,
        Part21SourceSpan span)
        : base(span)
    {
        ArgumentNullException.ThrowIfNull(header);
        Header = header;
        Anchor = anchor;
        Reference = reference;
        DataSections = SyntaxSnapshot.Create(dataSections);
        SignatureSections = SyntaxSnapshot.Create(signatureSections);
    }

    internal HeaderSectionSyntax Header { get; }

    internal AnchorSectionSyntax? Anchor { get; }

    internal ReferenceSectionSyntax? Reference { get; }

    internal IReadOnlyList<DataSectionSyntax> DataSections { get; }

    internal IReadOnlyList<SignatureSectionSyntax> SignatureSections { get; }

    internal void ThrowIfUnsupportedOperationsRequired(bool retainExternalReferenceEvidence = false)
    {
        var diagnostics = new List<Step21Diagnostic>();
        if (Anchor is not null)
        {
            diagnostics.Add(
                new Step21Diagnostic(
                    "P21-CAP-ANCHOR",
                    Step21DiagnosticSeverity.Error,
                    "Operational anchor resolution is not implemented.",
                    Anchor.Span.Start));
        }

        if (Reference is not null && !retainExternalReferenceEvidence)
        {
            diagnostics.Add(
                new Step21Diagnostic(
                    "P21-CAP-REFERENCE",
                    Step21DiagnosticSeverity.Error,
                    "External reference resolution is not implemented.",
                    Reference.Span.Start));
        }

        foreach (var signature in SignatureSections)
        {
            diagnostics.Add(
                new Step21Diagnostic(
                    "P21-CAP-SIGNATURE",
                    Step21DiagnosticSeverity.Error,
                    "Digital signature verification is not implemented.",
                    signature.Span.Start));
        }

        if (diagnostics.Count > 0)
            throw new ExchangeStructureCapabilityException(diagnostics);
    }
}

internal sealed class HeaderSectionSyntax : Part21SyntaxNode
{
    internal HeaderSectionSyntax(
        HeaderEntitySyntax fileDescription,
        HeaderEntitySyntax fileName,
        HeaderEntitySyntax fileSchema,
        IEnumerable<HeaderEntitySyntax> additionalEntities,
        Part21SourceSpan span)
        : base(span)
    {
        ArgumentNullException.ThrowIfNull(fileDescription);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(fileSchema);
        FileDescription = fileDescription;
        FileName = fileName;
        FileSchema = fileSchema;
        AdditionalEntities = SyntaxSnapshot.Create(additionalEntities);
    }

    internal HeaderEntitySyntax FileDescription { get; }

    internal HeaderEntitySyntax FileName { get; }

    internal HeaderEntitySyntax FileSchema { get; }

    internal IReadOnlyList<HeaderEntitySyntax> AdditionalEntities { get; }
}

internal sealed class HeaderEntitySyntax : Part21SyntaxNode
{
    internal HeaderEntitySyntax(string name, IEnumerable<ValueSyntax> parameters, Part21SourceSpan span)
        : base(span)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        Parameters = SyntaxSnapshot.Create(parameters);
    }

    internal string Name { get; }

    internal IReadOnlyList<ValueSyntax> Parameters { get; }
}

internal sealed class AnchorSectionSyntax : Part21SyntaxNode
{
    internal AnchorSectionSyntax(IEnumerable<AnchorSyntax> anchors, Part21SourceSpan span)
        : base(span) => Anchors = SyntaxSnapshot.Create(anchors);

    internal IReadOnlyList<AnchorSyntax> Anchors { get; }
}

internal sealed class AnchorSyntax : Part21SyntaxNode
{
    internal AnchorSyntax(
        ValueSyntax name,
        ValueSyntax item,
        IEnumerable<AnchorTagSyntax> tags,
        Part21SourceSpan span)
        : base(span)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(item);
        Name = name;
        Item = item;
        Tags = SyntaxSnapshot.Create(tags);
    }

    internal ValueSyntax Name { get; }

    internal ValueSyntax Item { get; }

    internal IReadOnlyList<AnchorTagSyntax> Tags { get; }
}

internal sealed class AnchorTagSyntax : Part21SyntaxNode
{
    internal AnchorTagSyntax(string name, ValueSyntax item, Part21SourceSpan span)
        : base(span)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(item);
        Name = name;
        Item = item;
    }

    internal string Name { get; }

    internal ValueSyntax Item { get; }
}

internal sealed class ReferenceSectionSyntax : Part21SyntaxNode
{
    internal ReferenceSectionSyntax(IEnumerable<ReferenceSyntax> references, Part21SourceSpan span)
        : base(span) => References = SyntaxSnapshot.Create(references);

    internal IReadOnlyList<ReferenceSyntax> References { get; }
}

internal sealed class ReferenceSyntax : Part21SyntaxNode
{
    internal ReferenceSyntax(ValueSyntax name, ValueSyntax resource, Part21SourceSpan span)
        : base(span)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(resource);
        Name = name;
        Resource = resource;
    }

    internal ValueSyntax Name { get; }

    internal ValueSyntax Resource { get; }
}

internal sealed class DataSectionSyntax : Part21SyntaxNode
{
    internal DataSectionSyntax(
        IEnumerable<ValueSyntax> parameters,
        IEnumerable<EntityInstanceSyntax> entityInstances,
        Part21SourceSpan span)
        : base(span)
    {
        Parameters = SyntaxSnapshot.Create(parameters);
        EntityInstances = SyntaxSnapshot.Create(entityInstances);
    }

    internal IReadOnlyList<ValueSyntax> Parameters { get; }

    internal IReadOnlyList<EntityInstanceSyntax> EntityInstances { get; }
}

internal enum EntityInstanceSyntaxKind
{
    Simple,

    Complex,
}

internal sealed class EntityInstanceSyntax : Part21SyntaxNode
{
    internal EntityInstanceSyntax(
        ValueSyntax name,
        EntityInstanceSyntaxKind kind,
        IEnumerable<EntityRecordSyntax> records,
        Part21SourceSpan span)
        : base(span)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        Kind = kind;
        Records = SyntaxSnapshot.Create(records);
    }

    internal ValueSyntax Name { get; }

    internal EntityInstanceSyntaxKind Kind { get; }

    internal IReadOnlyList<EntityRecordSyntax> Records { get; }
}

internal sealed class EntityRecordSyntax : Part21SyntaxNode
{
    internal EntityRecordSyntax(string name, IEnumerable<ValueSyntax> parameters, Part21SourceSpan span)
        : base(span)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        Parameters = SyntaxSnapshot.Create(parameters);
    }

    internal string Name { get; }

    internal IReadOnlyList<ValueSyntax> Parameters { get; }
}

internal sealed class SignatureSectionSyntax : Part21SyntaxNode
{
    internal SignatureSectionSyntax(ValueSyntax content, Part21SourceSpan span)
        : base(span)
    {
        ArgumentNullException.ThrowIfNull(content);
        Content = content;
    }

    internal ValueSyntax Content { get; }
}

internal enum Part21ValueKind
{
    Omitted,
    Derived,
    Integer,
    Real,
    String,
    EntityInstanceName,
    ValueInstanceName,
    ConstantEntityName,
    ConstantValueName,
    Enumeration,
    Binary,
    List,
    Typed,
    AnchorName,
    Resource,
    Signature,
}

internal sealed class ValueSyntax : Part21SyntaxNode
{
    internal ValueSyntax(
        Part21ValueKind kind,
        string text,
        string? typeName,
        IEnumerable<ValueSyntax> values,
        Part21SourceSpan span)
        : base(span)
    {
        ArgumentNullException.ThrowIfNull(text);
        Kind = kind;
        Text = text;
        TypeName = typeName;
        Values = SyntaxSnapshot.Create(values);
    }

    internal Part21ValueKind Kind { get; }

    internal string Text { get; }

    internal string? TypeName { get; }

    internal IReadOnlyList<ValueSyntax> Values { get; }
}

internal static class SyntaxSnapshot
{
    internal static IReadOnlyList<T> Create<T>(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}
