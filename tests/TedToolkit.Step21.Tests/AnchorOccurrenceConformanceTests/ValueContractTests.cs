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

        using (Assert.Multiple())
        {
            await Assert.That((Action)Derived).Throws<ArgumentException>();
            await Assert.That((Action)Typed).Throws<ArgumentException>();
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