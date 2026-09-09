using System.Text;

using TedToolkit.Step21.Syntax;

namespace TedToolkit.Step21;

/// <summary>Applies shared item and nesting limits to the parsed physical graph before model binding.</summary>
internal static class Part21SyntaxLimitValidator
{
    internal static Part21ReadGraphFeatures Validate(
        ExchangeStructureSyntax syntax,
        Part21ProcessingLimits limits,
        Part21ReadFeatureScan featureScan)
    {
        ArgumentNullException.ThrowIfNull(syntax);
        ArgumentNullException.ThrowIfNull(limits);

        CheckCount(syntax.Header.AdditionalEntities.Count, limits, "additional header entity");
        CheckCount(syntax.DataSections.Count, limits, "data section");
        CheckCount(syntax.SignatureSections.Count, limits, "signature section");
        CheckCount(syntax.Anchor?.Anchors.Count ?? 0, limits, "anchor");
        CheckCount(syntax.Reference?.References.Count ?? 0, limits, "reference");

        long occurrenceCount = syntax.Reference?.References.Count ?? 0;
        foreach (var section in syntax.DataSections)
        {
            occurrenceCount += section.EntityInstances.Count;
            if (occurrenceCount > limits.MaximumItemCount)
                ThrowItemLimit("instance name");
            CheckCount(section.Parameters.Count, limits, "DATA parameter");
            foreach (var instance in section.EntityInstances)
            {
                CheckCount(instance.Records.Count, limits, "entity component");
                foreach (var record in instance.Records)
                    CheckCount(record.Parameters.Count, limits, "entity parameter");
            }
        }

        if (syntax.Anchor is not null)
        {
            foreach (var anchor in syntax.Anchor.Anchors)
                CheckCount(anchor.Tags.Count, limits, "anchor tag");
        }

        ValueSyntax? class3Occurrence = null;
        ValueSyntax? rawHighString = null;
        Stack<(ValueSyntax Value, int Depth)>? pending = null;
        foreach (var root in EnumerateRootValues(syntax))
        {
            ValidateValue(
                root,
                limits,
                featureScan,
                ref class3Occurrence,
                ref rawHighString,
                ref pending);
        }

        return new Part21ReadGraphFeatures(class3Occurrence, rawHighString);
    }

    private static void ValidateValue(
        ValueSyntax root,
        Part21ProcessingLimits limits,
        Part21ReadFeatureScan featureScan,
        ref ValueSyntax? class3Occurrence,
        ref ValueSyntax? rawHighString,
        ref Stack<(ValueSyntax Value, int Depth)>? pending)
    {
        if (root.Values.Count == 0)
        {
            Inspect(root, featureScan, ref class3Occurrence, ref rawHighString);
            return;
        }

        pending ??= new Stack<(ValueSyntax Value, int Depth)>();
        pending.Push((root, 0));
        while (pending.TryPop(out var entry))
        {
            Inspect(entry.Value, featureScan, ref class3Occurrence, ref rawHighString);
            var nextDepth = entry.Value.Kind is Part21ValueKind.List or Part21ValueKind.Typed
                ? entry.Depth + 1
                : entry.Depth;
            if (nextDepth > limits.MaximumNestingDepth)
            {
                throw new ExchangeStructureCapabilityException([
                    new Step21Diagnostic(
                        "P21-PROCESSING-LIMIT-NESTING",
                        Step21DiagnosticSeverity.Error,
                        "The configured nested-value depth limit was exceeded.",
                        entry.Value.Span.Start),
                ]);
            }

            CheckCount(entry.Value.Values.Count, limits, "aggregate element", entry.Value.Span.Start);
            for (var index = entry.Value.Values.Count - 1; index >= 0; index--)
                pending.Push((entry.Value.Values[index], nextDepth));
        }
    }

    private static void Inspect(
        ValueSyntax value,
        Part21ReadFeatureScan featureScan,
        ref ValueSyntax? class3Occurrence,
        ref ValueSyntax? rawHighString)
    {
        if (value.Kind == Part21ValueKind.String
            && Encoding.UTF8.GetByteCount(value.Text) > Part21StringTokenLimits.MaximumStoredOctets)
        {
            Part21StringTokenLimits.Throw(value.Span.Start);
        }

        if ((featureScan & Part21ReadFeatureScan.Class3Occurrence) != 0
            && class3Occurrence is null
            && value.Kind is Part21ValueKind.ValueInstanceName
                or Part21ValueKind.ConstantEntityName
                or Part21ValueKind.ConstantValueName)
        {
            class3Occurrence = value;
        }

        if ((featureScan & Part21ReadFeatureScan.RawHighString) != 0
            && rawHighString is null
            && value.Kind == Part21ValueKind.String
            && value.Text.AsSpan(1, value.Text.Length - 2).ContainsAnyExceptInRange('\0', '\x7f'))
        {
            rawHighString = value;
        }
    }

    private static IEnumerable<ValueSyntax> EnumerateRootValues(ExchangeStructureSyntax syntax)
    {
        foreach (var value in syntax.Header.FileDescription.Parameters)
            yield return value;
        foreach (var value in syntax.Header.FileName.Parameters)
            yield return value;
        foreach (var value in syntax.Header.FileSchema.Parameters)
            yield return value;
        foreach (var entity in syntax.Header.AdditionalEntities)
        {
            foreach (var value in entity.Parameters)
                yield return value;
        }

        if (syntax.Anchor is not null)
        {
            foreach (var anchor in syntax.Anchor.Anchors)
            {
                yield return anchor.Name;
                yield return anchor.Item;
                foreach (var tag in anchor.Tags)
                    yield return tag.Item;
            }
        }

        if (syntax.Reference is not null)
        {
            foreach (var reference in syntax.Reference.References)
            {
                yield return reference.Name;
                yield return reference.Resource;
            }
        }

        foreach (var section in syntax.DataSections)
        {
            foreach (var value in section.Parameters)
                yield return value;
            foreach (var instance in section.EntityInstances)
            {
                foreach (var record in instance.Records)
                {
                    foreach (var value in record.Parameters)
                        yield return value;
                }
            }
        }
    }

    private static void CheckCount(
        long count,
        Part21ProcessingLimits limits,
        string name,
        SourceLocation? sourceLocation = null)
    {
        if (count > limits.MaximumItemCount)
            ThrowItemLimit(name, sourceLocation);
    }

    private static void ThrowItemLimit(string name, SourceLocation? sourceLocation = null) =>
        throw new ExchangeStructureCapabilityException([
            new Step21Diagnostic(
                "P21-PROCESSING-LIMIT-ITEM",
                Step21DiagnosticSeverity.Error,
                $"The configured {name} count limit was exceeded.",
                sourceLocation),
        ]);
}

[Flags]
internal enum Part21ReadFeatureScan
{
    None = 0,
    Class3Occurrence = 1,
    RawHighString = 2,
}

internal readonly record struct Part21ReadGraphFeatures(
    ValueSyntax? Class3Occurrence,
    ValueSyntax? RawHighString);
