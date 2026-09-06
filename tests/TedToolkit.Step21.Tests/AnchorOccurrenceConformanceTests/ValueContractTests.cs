namespace TedToolkit.Step21.Tests.AnchorOccurrenceConformanceTests;

internal sealed class ValueContractTests
{
    /// <summary>Clause 6.4.4 numeric names are positive, arbitrary precision and canonicalize leading zeroes.</summary>
    [Test]
    public async Task Should_preserve_arbitrary_precision_numeric_occurrence_identity()
    {
        const string digits = "184467440737095516161844674407370955161";
        var entity = new EntityInstanceName("000" + digits);
        var value = new ValueInstanceName("000" + digits);

        using (Assert.Multiple())
        {
            await Assert.That(entity.ToString()).IsEqualTo("#" + digits);
            await Assert.That(value.ToString()).IsEqualTo("@" + digits);
            await Assert.That(value).IsEqualTo(new ValueInstanceName(digits));
        }
    }

    /// <summary>Clause 6.4.4 constant names accept only canonical UPPER and DIGIT characters.</summary>
    [Test]
    public async Task Should_apply_the_part21_upper_character_set_to_constant_occurrence_names()
    {
        static void LowerEntity() => _ = new ConstantEntityName("Origin");
        static void DigitValue() => _ = new ConstantValueName("1PI");
        static void EmptyValue() => _ = new ConstantValueName(string.Empty);

        using (Assert.Multiple())
        {
            await Assert.That(new ConstantEntityName("ORIGIN_POINT").ToString()).IsEqualTo("#ORIGIN_POINT");
            await Assert.That(new ConstantValueName("_PI2").ToString()).IsEqualTo("@_PI2");
            await Assert.That(ParameterValue.FromEnumeration("_STATE").TryGetEnumeration(out var symbol)
                && symbol == "_STATE").IsTrue();
            await Assert.That(new Part21AnchorTag("_tag_name", ParameterValue.Omitted).Name)
                .IsEqualTo("_tag_name");
            await Assert.That((Action)LowerEntity).Throws<FormatException>();
            await Assert.That((Action)DigitValue).Throws<FormatException>();
            await Assert.That((Action)EmptyValue).Throws<FormatException>();
        }
    }

    /// <summary>Clause 6.5 editing values obey the same URI productions as parsed resource and anchor tokens.</summary>
    [Test]
    public async Task Should_reject_noncanonical_uri_reference_and_anchor_editing_values()
    {
        static void BadEscape() => _ = new Part21Resource("images/%GG.jpg");
        static void QueryWithoutPath() => _ = new Part21Resource("?mode=full");
        static void RelativeColon() => _ = new Part21Resource("1relative:value");
        static void AnchorHash() => _ = new AnchorName("left#right");
        static void NumericAnchor() => _ = new AnchorName("001");

        using (Assert.Multiple())
        {
            await Assert.That(new Part21Resource("images/a%20b.jpg#view").ToString())
                .IsEqualTo("<images/a%20b.jpg#view>");
            await Assert.That(new Part21Resource("#local").ToString()).IsEqualTo("<#local>");
            await Assert.That(new Part21Resource("foo:?bar").ToString()).IsEqualTo("<foo:?bar>");
            await Assert.That(new Part21Resource("path?next:/item").ToString())
                .IsEqualTo("<path?next:/item>");
            await Assert.That((Action)BadEscape).Throws<FormatException>();
            await Assert.That((Action)QueryWithoutPath).Throws<FormatException>();
            await Assert.That((Action)RelativeColon).Throws<FormatException>();
            await Assert.That((Action)AnchorHash).Throws<FormatException>();
            await Assert.That((Action)NumericAnchor).Throws<FormatException>();
        }
    }

    /// <summary>Clause 9 permits anchor simple values but excludes parameter-only derived and typed alternatives.</summary>
    [Test]
    public async Task Should_reject_parameter_only_anchor_item_categories()
    {
        static void Derived() => _ = new Part21Anchor(new AnchorName("derived"), ParameterValue.Derived);
        static void Typed() => _ = new Part21Anchor(
            new AnchorName("typed"),
            ParameterValue.FromTyped("LABEL", ParameterValue.FromString("value")));
        static void Boolean() => _ = new Part21Anchor(
            new AnchorName("boolean"),
            ParameterValue.FromBoolean(true));
        static void Logical() => _ = new Part21AnchorTag(
            "logical",
            ParameterValue.FromLogical(LogicalValue.Unknown));
        static void NestedBoolean() => _ = new Part21Anchor(
            new AnchorName("nested-boolean"),
            ParameterValue.FromAggregate([
                ParameterValue.FromAggregate([ParameterValue.FromBoolean(false)]),
            ]));

        using (Assert.Multiple())
        {
            await Assert.That((Action)Derived).Throws<ArgumentException>();
            await Assert.That((Action)Typed).Throws<ArgumentException>();
            await Assert.That((Action)Boolean).Throws<ArgumentException>();
            await Assert.That((Action)Logical).Throws<ArgumentException>();
            await Assert.That((Action)NestedBoolean).Throws<ArgumentException>();
        }
    }

    /// <summary>Clause 6.4.4 gives local entity and external value occurrences one shared numeric identity space.</summary>
    [Test]
    public async Task Should_reject_local_entity_and_external_value_overlap_before_writing_any_output()
    {
        var structure = new ExchangeStructure(
            ExchangeStructureTests.TestHeader.Create(),
            [new ExchangeStructureTests.TestSchemaDescriptor("TEST_SCHEMA")]);
        var section = new DataSection(new SchemaName("TEST_SCHEMA"));
        structure.DataSections.Add(section);
        structure.Add(section, new EntityInstanceName("1"), new ExchangeStructureTests.TestEntity());
        structure.References.Add(new Part21Reference(
            new ValueInstanceName("001"),
            new Part21Resource("values.p21#value")));
        var destination = new StringWriter();

        var validation = structure.Validate();
        var exception = Assert.Throws<ExchangeStructureWriteValidationException>(() => structure.Write(destination));

        using (Assert.Multiple())
        {
            await Assert.That(validation.Failures.Select(failure => failure.Code))
                .Contains("P21.STRUCTURE.OCCURRENCE.OVERLAP");
            await Assert.That(validation.Failures.Single(failure =>
                failure.Code == "P21.STRUCTURE.OCCURRENCE.OVERLAP").Path)
                .IsEqualTo("References[0]");
            await Assert.That(exception.ValidationResult.Failures.Select(failure => failure.Code))
                .Contains("P21.STRUCTURE.OCCURRENCE.OVERLAP");
            await Assert.That(destination.ToString()).IsEmpty();
        }
    }

    /// <summary>Clause 9.2.1 requires local entity anchors to retain object identity rather than a retargetable name.</summary>
    [Test]
    public async Task Should_require_direct_object_identity_for_local_entity_anchor_edits()
    {
        var structure = new ExchangeStructure(
            ExchangeStructureTests.TestHeader.Create(),
            [new ExchangeStructureTests.TestSchemaDescriptor("TEST_SCHEMA")]);
        var section = new DataSection(new SchemaName("TEST_SCHEMA"));
        structure.DataSections.Add(section);
        var original = new ExchangeStructureTests.TestEntity();
        structure.Add(section, new EntityInstanceName("1"), original);
        structure.Anchors.Add(new Part21Anchor(
            new AnchorName("root"),
            ParameterValue.FromEntityInstance(new EntityInstanceName("1"))));
        var destination = new StringWriter();

        var unresolved = structure.Validate();
        var exception = Assert.Throws<ExchangeStructureWriteValidationException>(() => structure.Write(destination));
        structure.Anchors[0] = new Part21Anchor(new AnchorName("root"), ParameterValue.FromEntity(original));
        _ = structure.Remove(original);
        var replacement = new ExchangeStructureTests.TestEntity();
        structure.Add(section, new EntityInstanceName("1"), replacement);
        var replaced = structure.Validate();
        _ = structure.Anchors[0].Item.TryGetEntity(out var retained);

        using (Assert.Multiple())
        {
            await Assert.That(unresolved.Failures.Select(failure => failure.Code))
                .Contains("P21.STRUCTURE.ANCHOR.ENTITY_IDENTITY");
            await Assert.That(exception.ValidationResult.Failures.Select(failure => failure.Code))
                .Contains("P21.STRUCTURE.ANCHOR.ENTITY_IDENTITY");
            await Assert.That(destination.ToString()).IsEmpty();
            await Assert.That(retained).IsSameReferenceAs(original);
            await Assert.That(retained).IsNotSameReferenceAs(replacement);
            await Assert.That(replaced.Failures.Select(failure => failure.Code))
                .Contains("P21.STRUCTURE.ANCHOR.ENTITY_REGISTRATION");
        }
    }

    /// <summary>Public edits are validated as a complete candidate before any destination character is published.</summary>
    [Test]
    public async Task Should_reject_invalid_anchor_edits_before_writing_any_output()
    {
        var structure = new ExchangeStructure(
            ExchangeStructureTests.TestHeader.Create(),
            [new ExchangeStructureTests.TestSchemaDescriptor("TEST_SCHEMA")]);
        structure.Anchors.Add(new Part21Anchor(
            new AnchorName("dangling"),
            ParameterValue.FromValueInstance(new ValueInstanceName("1"))));
        structure.Anchors.Add(new Part21Anchor(new AnchorName("dangling"), ParameterValue.Omitted));
        var destination = new StringWriter();

        var exception = Assert.Throws<ExchangeStructureWriteValidationException>(() => structure.Write(destination));

        using (Assert.Multiple())
        {
            await Assert.That(destination.ToString()).IsEmpty();
            await Assert.That(exception.ValidationResult.Failures.Select(failure => failure.Code))
                .IsEquivalentTo([
                    "P21.STRUCTURE.ANCHOR.VALUE_OCCURRENCE",
                    "P21.STRUCTURE.ANCHOR.DUPLICATE",
                ]);
        }
    }
}