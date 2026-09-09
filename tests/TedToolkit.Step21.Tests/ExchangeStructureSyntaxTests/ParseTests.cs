using TedToolkit.Step21.Syntax;

namespace TedToolkit.Step21.Tests.ExchangeStructureSyntaxTests;

internal sealed class ParseTests
{
    /// <summary>
    /// Verifies that every Edition 3 section, record, and parameter family survives the syntax handoff.
    /// </summary>
    [Test]
    public async Task Should_preserve_every_syntax_family_when_complete_exchange_file_is_parsed()
    {
        const string relativePath = "Part21/Valid/edition3-all-sections.p21";
        var syntax = Parse(relativePath);
        var firstData = syntax.DataSections[0];
        var firstRecord = firstData.EntityInstances[0].Records[0];
        var typed = firstRecord.Parameters[0];
        var list = firstRecord.Parameters[9];

        using (Assert.Multiple())
        {
            await Assert.That(syntax.Span.Start.Line).IsEqualTo(1);
            await Assert.That(syntax.Span.Start.Column).IsEqualTo(1);
            await Assert.That(syntax.Span.End.Line).IsEqualTo(29);
            await Assert.That(syntax.Span.End.Column).IsEqualTo(27);
            await Assert.That(syntax.Header.FileDescription.Name).IsEqualTo("FILE_DESCRIPTION");
            await Assert.That(syntax.Header.FileName.Name).IsEqualTo("FILE_NAME");
            await Assert.That(syntax.Header.FileSchema.Name).IsEqualTo("FILE_SCHEMA");
            await Assert.That(string.Join('|', syntax.Header.AdditionalEntities.Select(value => value.Name)))
                .IsEqualTo("SCHEMA_POPULATION|FILE_POPULATION|SECTION_LANGUAGE|SECTION_CONTEXT|!CUSTOM_HEADER");
            await Assert.That(syntax.Anchor?.Anchors.Count).IsEqualTo(2);
            await Assert.That(syntax.Reference?.References.Count).IsEqualTo(2);
            await Assert.That(syntax.DataSections.Count).IsEqualTo(2);
            await Assert.That(syntax.SignatureSections.Count).IsEqualTo(1);
            await Assert.That(syntax.Anchor!.Anchors[0].Name.Kind).IsEqualTo(Part21ValueKind.AnchorName);
            await Assert.That(syntax.Anchor.Anchors[0].Name.Text).IsEqualTo("<root>");
            await Assert.That(syntax.Anchor.Anchors[0].Item.Kind).IsEqualTo(Part21ValueKind.EntityInstanceName);
            await Assert.That(syntax.Anchor.Anchors[0].Tags[0].Name).IsEqualTo("source");
            await Assert.That(syntax.Anchor.Anchors[0].Tags[0].Item.Kind).IsEqualTo(Part21ValueKind.Resource);
            await Assert.That(syntax.Anchor.Anchors[1].Item.Kind).IsEqualTo(Part21ValueKind.List);
            await Assert.That(string.Join('|', syntax.Anchor.Anchors[1].Item.Values.Select(value => value.Kind)))
                .IsEqualTo("Integer|Real|String|Enumeration|Binary|ValueInstanceName|ConstantEntityName|ConstantValueName|Omitted");
            await Assert.That(string.Join('|', syntax.Reference!.References.Select(value => value.Name.Kind)))
                .IsEqualTo("EntityInstanceName|ValueInstanceName");
            await Assert.That(syntax.Reference.References[0].Resource.Text)
                .IsEqualTo("<https://example.com/model.p21#external>");
            await Assert.That(firstData.Parameters.Count).IsEqualTo(2);
            await Assert.That(firstData.EntityInstances.Count).IsEqualTo(3);
            await Assert.That(firstData.EntityInstances[0].Kind).IsEqualTo(EntityInstanceSyntaxKind.Simple);
            await Assert.That(firstData.EntityInstances[2].Kind).IsEqualTo(EntityInstanceSyntaxKind.Complex);
            await Assert.That(firstData.EntityInstances[2].Records.Count).IsEqualTo(2);
            await Assert.That(string.Join('|', firstData.EntityInstances[2].Records.Select(value => value.Name)))
                .IsEqualTo("BASE|SUBTYPE");
            await Assert.That(firstRecord.Name).IsEqualTo("EXAMPLE_ENTITY");
            await Assert.That(firstRecord.Parameters.Count).IsEqualTo(10);
            await Assert.That(string.Join('|', firstRecord.Parameters.Select(value => value.Kind)))
                .IsEqualTo("Typed|Omitted|Derived|Enumeration|Binary|EntityInstanceName|ValueInstanceName|ConstantEntityName|ConstantValueName|List");
            await Assert.That(typed.TypeName).IsEqualTo("TYPE");
            await Assert.That(typed.Values.Count).IsEqualTo(1);
            await Assert.That(typed.Values[0].Kind).IsEqualTo(Part21ValueKind.Integer);
            await Assert.That(typed.Values[0].Text).IsEqualTo("+1");
            await Assert.That(string.Join('|', list.Values.Select(value => value.Kind)))
                .IsEqualTo("Integer|Real|String|String|String|String|String|String|String");
            await Assert.That(syntax.SignatureSections[0].Content.Kind).IsEqualTo(Part21ValueKind.Signature);
            await Assert.That(syntax.SignatureSections[0].Content.Text).IsEqualTo("QUJDRA==");
        }

        foreach (var node in EnumerateNodes(syntax))
        {
            using (Assert.Multiple())
            {
                await Assert.That(node.Span.Start.FilePath).IsEqualTo(relativePath);
                await Assert.That(node.Span.End.FilePath).IsEqualTo(relativePath);
                await Assert.That(node.Span.Start.Line).IsGreaterThan(0);
                await Assert.That(node.Span.Start.Column).IsGreaterThan(0);
                await Assert.That(node.Span.End.Line).IsGreaterThan(0);
                await Assert.That(node.Span.End.Column).IsGreaterThan(0);
                await Assert.That(IsOrdered(node.Span.Start, node.Span.End)).IsTrue();
            }
        }
    }

    /// <summary>
    /// Verifies that ignored controls do not alter deterministic syntax values or occurrence-name kinds.
    /// </summary>
    [Test]
    public async Task Should_normalize_ignored_controls_when_source_positions_are_preserved()
    {
        var syntax = Parse("Part21/Valid/ignored-controls-inside-tokens.p21");
        var instance = syntax.DataSections[0].EntityInstances[0];
        var record = instance.Records[0];

        using (Assert.Multiple())
        {
            await Assert.That(syntax.Header.FileDescription.Name).IsEqualTo("FILE_DESCRIPTION");
            await Assert.That(syntax.Header.FileSchema.Name).IsEqualTo("FILE_SCHEMA");
            await Assert.That(instance.Name.Text).IsEqualTo("#12");
            await Assert.That(record.Name).IsEqualTo("EXAMPLE_ENTITY");
            await Assert.That(record.Parameters[0].Kind).IsEqualTo(Part21ValueKind.Real);
            await Assert.That(record.Parameters[0].Text).IsEqualTo("-3.5E+2");
            await Assert.That(instance.Span.Start.Line).IsGreaterThan(1);
            await Assert.That(IsOrdered(instance.Span.Start, instance.Span.End)).IsTrue();
        }
    }

    [Test]
    public async Task Should_ignore_raw_controls_inside_comment_delimiters()
    {
        var source = "ISO-10303-21;\n/\r*comment\tbody*\n/\n"
            + "HEADER;FILE_DESCRIPTION(('comments'),'4;1');"
            + "FILE_NAME('x','2026-09-09T00:00:00',('a'),('o'),'p','s','a');"
            + "FILE_SCHEMA(('TEST_SCHEMA'));ENDSEC;DATA;ENDSEC;END-ISO-10303-21;"
            + "SIGNATURE /\r*signature\tbody*\n/ QUJD ENDSEC;";

        var syntax = ExchangeStructureSyntaxParser.Parse(source, "comments.p21");

        using (Assert.Multiple())
        {
            await Assert.That(syntax.Header.FileDescription.Name).IsEqualTo("FILE_DESCRIPTION");
            await Assert.That(syntax.DataSections).HasSingleItem();
            await Assert.That(syntax.SignatureSections.Single().Content.Text).IsEqualTo("QUJD");
        }
    }

    [Test]
    public async Task Should_reject_non_scalar_source_text_before_lexing()
    {
        var failure = Assert.Throws<ExchangeStructureSyntaxException>(() =>
            ExchangeStructureSyntaxParser.Parse("ISO-10303-21;\n/*\uD800*/", "invalid.p21"));

        using (Assert.Multiple())
        {
            await Assert.That(failure.Diagnostics).HasSingleItem();
            await Assert.That(failure.Diagnostics[0].Code).IsEqualTo("P21-SYNTAX-UNICODE-SCALAR");
            await Assert.That(failure.Diagnostics[0].SourceLocation!.FilePath).IsEqualTo("invalid.p21");
            await Assert.That(failure.Diagnostics[0].SourceLocation!.Line).IsEqualTo(2);
            await Assert.That(failure.Diagnostics[0].SourceLocation!.Column).IsEqualTo(3);
        }
    }

    /// <summary>
    /// Verifies that the Edition 3 zero-data and repeated-signature cardinalities survive the syntax handoff.
    /// </summary>
    [Test]
    public async Task Should_preserve_zero_data_sections_and_multiple_signatures()
    {
        var syntax = Parse("Part21/Valid/edition3-no-data-signatures.p21");

        using (Assert.Multiple())
        {
            await Assert.That(syntax.DataSections.Count).IsEqualTo(0);
            await Assert.That(syntax.SignatureSections.Count).IsEqualTo(2);
            await Assert.That(string.Join('|', syntax.SignatureSections.Select(value => value.Content.Text)))
                .IsEqualTo("QUJD|REVG");
            await Assert.That(syntax.Span.End.Line).IsEqualTo(9);
            await Assert.That(syntax.Span.End.Column).IsEqualTo(23);
        }
    }

    /// <summary>
    /// Verifies that every syntax-node collection exposed by the internal handoff is read-only.
    /// </summary>
    [Test]
    public async Task Should_expose_immutable_collection_snapshots()
    {
        var syntax = Parse("Part21/Valid/edition3-all-sections.p21");
        void MutateDataSections() => ((IList<DataSectionSyntax>)syntax.DataSections).Clear();
        void MutateParameters() => ((IList<ValueSyntax>)syntax.DataSections[0].EntityInstances[0].Records[0].Parameters)
            .Clear();
        void MutateValues() => ((IList<ValueSyntax>)syntax.DataSections[0].EntityInstances[0].Records[0].Parameters[9].Values)
            .Clear();

        using (Assert.Multiple())
        {
            await Assert.That((Action)MutateDataSections).Throws<NotSupportedException>();
            await Assert.That((Action)MutateParameters).Throws<NotSupportedException>();
            await Assert.That((Action)MutateValues).Throws<NotSupportedException>();
        }
    }

    private static ExchangeStructureSyntax Parse(string relativePath) =>
        ExchangeStructureSyntaxParser.Parse(File.ReadAllText(GetPath(relativePath)), relativePath);

    private static string GetPath(string relativePath) =>
        Path.Combine(AppContext.BaseDirectory, "TestData", relativePath);

    private static bool IsOrdered(SourceLocation start, SourceLocation end) =>
        end.Line > start.Line || end.Line == start.Line && end.Column >= start.Column;

    private static IEnumerable<Part21SyntaxNode> EnumerateNodes(ExchangeStructureSyntax syntax)
    {
        yield return syntax;
        foreach (var node in EnumerateHeader(syntax.Header))
            yield return node;
        if (syntax.Anchor is not null)
        {
            yield return syntax.Anchor;
            foreach (var anchor in syntax.Anchor.Anchors)
            {
                yield return anchor;
                foreach (var value in EnumerateValue(anchor.Name))
                    yield return value;
                foreach (var value in EnumerateValue(anchor.Item))
                    yield return value;
                foreach (var tag in anchor.Tags)
                {
                    yield return tag;
                    foreach (var value in EnumerateValue(tag.Item))
                        yield return value;
                }
            }
        }

        if (syntax.Reference is not null)
        {
            yield return syntax.Reference;
            foreach (var reference in syntax.Reference.References)
            {
                yield return reference;
                foreach (var value in EnumerateValue(reference.Name))
                    yield return value;
                foreach (var value in EnumerateValue(reference.Resource))
                    yield return value;
            }
        }

        foreach (var section in syntax.DataSections)
        {
            yield return section;
            foreach (var parameter in section.Parameters.SelectMany(EnumerateValue))
                yield return parameter;
            foreach (var instance in section.EntityInstances)
            {
                yield return instance;
                foreach (var value in EnumerateValue(instance.Name))
                    yield return value;
                foreach (var record in instance.Records)
                {
                    yield return record;
                    foreach (var parameter in record.Parameters.SelectMany(EnumerateValue))
                        yield return parameter;
                }
            }
        }

        foreach (var signature in syntax.SignatureSections)
        {
            yield return signature;
            foreach (var value in EnumerateValue(signature.Content))
                yield return value;
        }
    }

    private static IEnumerable<Part21SyntaxNode> EnumerateHeader(HeaderSectionSyntax header)
    {
        yield return header;
        foreach (var entity in new[] { header.FileDescription, header.FileName, header.FileSchema }
                     .Concat(header.AdditionalEntities))
        {
            yield return entity;
            foreach (var parameter in entity.Parameters.SelectMany(EnumerateValue))
                yield return parameter;
        }
    }

    private static IEnumerable<Part21SyntaxNode> EnumerateValue(ValueSyntax value)
    {
        yield return value;
        foreach (var child in value.Values.SelectMany(EnumerateValue))
            yield return child;
    }
}