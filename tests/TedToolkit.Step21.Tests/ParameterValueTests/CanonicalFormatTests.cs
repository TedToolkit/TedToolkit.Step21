using System.Numerics;

using TedToolkit.Step21.Tests.Parsing;

namespace TedToolkit.Step21.Tests.ParameterValueTests;

internal sealed class CanonicalFormatTests
{
    /// <summary>
    /// Verifies that every parameter alternative has an exact canonical physical spelling accepted by the grammar.
    /// </summary>
    [Test]
    public async Task Should_format_and_reparse_every_parameter_alternative()
    {
        var entity = new TestEntity();
        var values = new[]
        {
            ParameterValue.Omitted,
            ParameterValue.Derived,
            ParameterValue.FromInteger(BigInteger.Parse("18446744073709551616")),
            ParameterValue.FromInteger(BigInteger.Parse("-18446744073709551617")),
            ParameterValue.FromReal(new RealValue(new BigInteger(125), new BigInteger(-2))),
            ParameterValue.FromReal(new RealValue(new BigInteger(-7), BigInteger.Parse("18446744073709551616"))),
            ParameterValue.FromString("O'Brien\\😀"),
            ParameterValue.FromString(string.Empty),
            ParameterValue.FromBinary(new BinaryValue("00101")),
            ParameterValue.FromBinary(new BinaryValue(string.Empty)),
            ParameterValue.FromBoolean(false),
            ParameterValue.FromBoolean(true),
            ParameterValue.FromLogical(LogicalValue.False),
            ParameterValue.FromLogical(LogicalValue.Unknown),
            ParameterValue.FromLogical(LogicalValue.True),
            ParameterValue.FromEnumeration("CUSTOM1"),
            ParameterValue.FromEntity(entity),
            ParameterValue.FromAggregate([ParameterValue.Omitted, ParameterValue.Derived]),
            ParameterValue.FromTyped(
                "LENGTH_MEASURE",
                ParameterValue.FromReal(new RealValue(new BigInteger(125), new BigInteger(-2)))),
        };

        var formatted = values
            .Select(value => ParameterValueFormatter.Format(value, _ => new EntityInstanceName("42")))
            .ToArray();
        var source = $$"""
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('values'),'2;1');
            FILE_NAME('values.step','2026-08-21T00:00:00',('TedToolkit'),('TedToolkit'),'TedToolkit.Step21','TedToolkit.Step21','');
            FILE_SCHEMA(('VALUE_SCHEMA'));
            ENDSEC;
            DATA;
            #1=VALUE_HOLDER({{string.Join(',', formatted)}});
            ENDSEC;
            END-ISO-10303-21;
            """;
        var parse = ParserFixture.ParseStepText(source);

        using (Assert.Multiple())
        {
            await Assert.That(formatted).IsEquivalentTo(
            [
                "$",
                "*",
                "18446744073709551616",
                "-18446744073709551617",
                "125.E-2",
                "-7.E18446744073709551616",
                "'O''Brien\\\\\\X4\\0001F600\\X0\\'",
                "''",
                "\"328\"",
                "\"0\"",
                ".F.",
                ".T.",
                ".F.",
                ".U.",
                ".T.",
                ".CUSTOM1.",
                "#42",
                "($,*)",
                "LENGTH_MEASURE(125.E-2)",
            ]);
            await Assert.That(parse.Errors).IsEmpty();
            await Assert.That(parse.ReachedEndOfFile).IsTrue();
        }
    }

    private sealed class TestEntity : Entity
    {
        public override IEnumerable<Entity> DirectReferences => [];
    }
}
