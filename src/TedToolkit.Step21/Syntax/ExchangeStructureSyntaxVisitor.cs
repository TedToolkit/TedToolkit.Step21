using System.Text;

using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

using TedToolkit.Step21.Grammar;

namespace TedToolkit.Step21.Syntax;

// Removes the generated ANTLR tree at the handoff boundary and retains only immutable ISO syntax.
internal sealed class ExchangeStructureSyntaxVisitor : STEPParserBaseVisitor<Part21SyntaxNode?>
{
    private readonly string _filePath;

    internal ExchangeStructureSyntaxVisitor(string filePath) => _filePath = filePath;

    internal ExchangeStructureSyntax Create(STEPParser.ExchangeFileContext context) =>
        Require<ExchangeStructureSyntax>(VisitExchangeFile(context));

    public override Part21SyntaxNode VisitExchangeFile(STEPParser.ExchangeFileContext context)
    {
        var signatures = context.signatureSection().Select(VisitRequired<SignatureSectionSyntax>).ToArray();
        var endToken = signatures.Length == 0
            ? context.ISO_END().Symbol
            : context.signatureSection()[^1].ENDSEC().Symbol;

        return new ExchangeStructureSyntax(
            VisitRequired<HeaderSectionSyntax>(context.headerSection()),
            VisitOptional<AnchorSectionSyntax>(context.anchorSection()),
            VisitOptional<ReferenceSectionSyntax>(context.referenceSection()),
            context.dataSection().Select(VisitRequired<DataSectionSyntax>),
            signatures,
            CreateSpan(context.ISO_START().Symbol, endToken));
    }

    public override Part21SyntaxNode VisitHeaderSection(STEPParser.HeaderSectionContext context) =>
        new HeaderSectionSyntax(
            VisitRequired<HeaderEntitySyntax>(context.fileDescription()),
            VisitRequired<HeaderEntitySyntax>(context.fileName()),
            VisitRequired<HeaderEntitySyntax>(context.fileSchema()),
            context.headerEntity().Select(VisitRequired<HeaderEntitySyntax>),
            CreateSpan(context));

    public override Part21SyntaxNode VisitFileDescription(STEPParser.FileDescriptionContext context) =>
        CreateHeaderEntity("FILE_DESCRIPTION", context.parameterList(), context);

    public override Part21SyntaxNode VisitFileName(STEPParser.FileNameContext context) =>
        CreateHeaderEntity("FILE_NAME", context.parameterList(), context);

    public override Part21SyntaxNode VisitFileSchema(STEPParser.FileSchemaContext context) =>
        CreateHeaderEntity("FILE_SCHEMA", context.parameterList(), context);

    public override Part21SyntaxNode VisitHeaderEntity(STEPParser.HeaderEntityContext context) =>
        CreateHeaderEntity(Normalize(context.keyword().GetText()), context.parameterList(), context);

    public override Part21SyntaxNode VisitParameter(STEPParser.ParameterContext context)
    {
        if (context.typedParameter() is { } typed)
            return VisitRequired<ValueSyntax>(typed);
        if (context.untypedParameter() is { } untyped)
            return VisitRequired<ValueSyntax>(untyped);

        return CreateLeaf(Part21ValueKind.Derived, context);
    }

    public override Part21SyntaxNode VisitTypedParameter(STEPParser.TypedParameterContext context) =>
        new ValueSyntax(
            Part21ValueKind.Typed,
            Normalize(context.GetText()),
            Normalize(context.keyword().GetText()),
            [VisitRequired<ValueSyntax>(context.parameter())],
            CreateSpan(context));

    public override Part21SyntaxNode VisitUntypedParameter(STEPParser.UntypedParameterContext context)
    {
        if (context.list() is { } list)
            return VisitRequired<ValueSyntax>(list);
        if (context.rhsOccurrenceName() is { } occurrence)
            return CreateOccurrence(occurrence);

        return CreateLexicalValue(context);
    }

    public override Part21SyntaxNode VisitList(STEPParser.ListContext context) =>
        new ValueSyntax(
            Part21ValueKind.List,
            Normalize(context.GetText()),
            null,
            context.parameter().Select(VisitRequired<ValueSyntax>),
            CreateSpan(context));

    public override Part21SyntaxNode VisitAnchorSection(STEPParser.AnchorSectionContext context) =>
        new AnchorSectionSyntax(context.anchor().Select(VisitRequired<AnchorSyntax>), CreateSpan(context));

    public override Part21SyntaxNode VisitAnchor(STEPParser.AnchorContext context) =>
        new AnchorSyntax(
            CreateLeaf(Part21ValueKind.AnchorName, context.anchorName()),
            VisitRequired<ValueSyntax>(context.anchorItem()),
            context.anchorTag().Select(VisitRequired<AnchorTagSyntax>),
            CreateSpan(context));

    public override Part21SyntaxNode VisitAnchorItem(STEPParser.AnchorItemContext context)
    {
        if (context.anchorItemList() is { } list)
            return VisitRequired<ValueSyntax>(list);
        if (context.resource() is { } resource)
            return CreateLeaf(Part21ValueKind.Resource, resource);
        if (context.rhsOccurrenceName() is { } occurrence)
            return CreateOccurrence(occurrence);

        return CreateLexicalValue(context);
    }

    public override Part21SyntaxNode VisitAnchorItemList(STEPParser.AnchorItemListContext context) =>
        new ValueSyntax(
            Part21ValueKind.List,
            Normalize(context.GetText()),
            null,
            context.anchorItem().Select(VisitRequired<ValueSyntax>),
            CreateSpan(context));

    public override Part21SyntaxNode VisitAnchorTag(STEPParser.AnchorTagContext context) =>
        new AnchorTagSyntax(
            Normalize(context.tagName().GetText()),
            VisitRequired<ValueSyntax>(context.anchorItem()),
            CreateSpan(context));

    public override Part21SyntaxNode VisitReferenceSection(STEPParser.ReferenceSectionContext context) =>
        new ReferenceSectionSyntax(context.reference().Select(VisitRequired<ReferenceSyntax>), CreateSpan(context));

    public override Part21SyntaxNode VisitReference(STEPParser.ReferenceContext context) =>
        new ReferenceSyntax(
            CreateOccurrence(context.lhsOccurrenceName()),
            CreateLeaf(Part21ValueKind.Resource, context.resource()),
            CreateSpan(context));

    public override Part21SyntaxNode VisitDataSection(STEPParser.DataSectionContext context) =>
        new DataSectionSyntax(
            CreateParameters(context.parameterList()),
            context.entityInstance().Select(VisitRequired<EntityInstanceSyntax>),
            CreateSpan(context));

    public override Part21SyntaxNode VisitEntityInstance(STEPParser.EntityInstanceContext context)
    {
        if (context.simpleEntityInstance() is { } simple)
            return VisitRequired<EntityInstanceSyntax>(simple);

        return VisitRequired<EntityInstanceSyntax>(context.complexEntityInstance());
    }

    public override Part21SyntaxNode VisitSimpleEntityInstance(STEPParser.SimpleEntityInstanceContext context) =>
        new EntityInstanceSyntax(
            CreateOccurrence(context.EntityInstanceName()),
            EntityInstanceSyntaxKind.Simple,
            [VisitRequired<EntityRecordSyntax>(context.simpleRecord())],
            CreateSpan(context));

    public override Part21SyntaxNode VisitComplexEntityInstance(STEPParser.ComplexEntityInstanceContext context) =>
        new EntityInstanceSyntax(
            CreateOccurrence(context.EntityInstanceName()),
            EntityInstanceSyntaxKind.Complex,
            context.subSuperRecord().simpleRecord().Select(VisitRequired<EntityRecordSyntax>),
            CreateSpan(context));

    public override Part21SyntaxNode VisitSimpleRecord(STEPParser.SimpleRecordContext context) =>
        new EntityRecordSyntax(
            Normalize(context.keyword().GetText()),
            CreateParameters(context.parameterList()),
            CreateSpan(context));

    public override Part21SyntaxNode VisitSignatureSection(STEPParser.SignatureSectionContext context) =>
        new SignatureSectionSyntax(
            CreateLeaf(Part21ValueKind.Signature, context.SignatureContent()),
            context.Start.StartIndex,
            CreateSpan(context));

    private HeaderEntitySyntax CreateHeaderEntity(
        string name,
        STEPParser.ParameterListContext? parameters,
        ParserRuleContext context) =>
        new(name, CreateParameters(parameters), CreateSpan(context));

    private IEnumerable<ValueSyntax> CreateParameters(STEPParser.ParameterListContext? context) =>
        context is null ? [] : context.parameter().Select(VisitRequired<ValueSyntax>);

    private ValueSyntax CreateLexicalValue(ParserRuleContext context)
    {
        var token = context.Start;
        var kind = token.Type switch
        {
            STEPParser.DOLLAR => Part21ValueKind.Omitted,
            STEPParser.Integer => Part21ValueKind.Integer,
            STEPParser.Real => Part21ValueKind.Real,
            STEPParser.String => Part21ValueKind.String,
            STEPParser.Enumeration => Part21ValueKind.Enumeration,
            STEPParser.Binary => Part21ValueKind.Binary,
            _ => throw new InvalidOperationException($"Unsupported Part 21 value token type {token.Type}."),
        };

        return CreateLeaf(kind, context);
    }

    private ValueSyntax CreateOccurrence(ParserRuleContext context) =>
        CreateOccurrence(context.Start);

    private ValueSyntax CreateOccurrence(ITerminalNode terminal) =>
        CreateOccurrence(terminal.Symbol);

    private ValueSyntax CreateOccurrence(IToken token)
    {
        var kind = token.Type switch
        {
            STEPParser.EntityInstanceName => Part21ValueKind.EntityInstanceName,
            STEPParser.ValueInstanceName => Part21ValueKind.ValueInstanceName,
            STEPParser.ConstantEntityName => Part21ValueKind.ConstantEntityName,
            STEPParser.ConstantValueName => Part21ValueKind.ConstantValueName,
            _ => throw new InvalidOperationException($"Unsupported occurrence token type {token.Type}."),
        };

        return CreateLeaf(kind, token);
    }

    private ValueSyntax CreateLeaf(Part21ValueKind kind, ParserRuleContext context) =>
        new(kind, Normalize(context.GetText()), null, [], CreateSpan(context));

    private ValueSyntax CreateLeaf(Part21ValueKind kind, ITerminalNode terminal) =>
        CreateLeaf(kind, terminal.Symbol);

    private ValueSyntax CreateLeaf(Part21ValueKind kind, IToken token) =>
        new(kind, Normalize(token.Text), null, [], CreateSpan(token, token));

    private T VisitRequired<T>(ParserRuleContext context)
        where T : Part21SyntaxNode => Require<T>(Visit(context));

    private T? VisitOptional<T>(ParserRuleContext? context)
        where T : Part21SyntaxNode => context is null ? null : Require<T>(Visit(context));

    private static T Require<T>(Part21SyntaxNode? node)
        where T : Part21SyntaxNode => node as T
        ?? throw new InvalidOperationException($"Expected syntax node {typeof(T).Name}.");

    private Part21SourceSpan CreateSpan(ParserRuleContext context) =>
        CreateSpan(context.Start, context.Stop);

    private Part21SourceSpan CreateSpan(IToken start, IToken stop) =>
        new(CreateStart(start), CreateEnd(stop));

    private SourceLocation CreateStart(IToken token) =>
        new(_filePath, token.Line, token.Column + 1);

    private SourceLocation CreateEnd(IToken token)
    {
        var line = token.Line;
        var column = token.Column + 1;
        foreach (var character in token.Text ?? string.Empty)
        {
            if (character == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        return new SourceLocation(_filePath, line, column);
    }

    private static string Normalize(string? text)
    {
        var source = text ?? string.Empty;
        var builder = new StringBuilder(source.Length);
        foreach (var character in source)
        {
            if (character > '\u001F' && character != '\u007F')
                builder.Append(character);
        }

        return builder.ToString();
    }
}
