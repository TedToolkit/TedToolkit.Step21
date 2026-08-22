using System.Numerics;

namespace TedToolkit.Step21.Tests.NumberValueTests;

internal sealed class FactoryTests
{
    /// <summary>
    /// Verifies that NUMBER retains whether its exact value came from INTEGER or REAL.
    /// </summary>
    [Test]
    public async Task Should_preserve_integer_and_real_alternatives()
    {
        var integer = NumberValue.FromInteger(BigInteger.Parse("18446744073709551616"));
        var real = NumberValue.FromReal(new RealValue(new BigInteger(125), new BigInteger(-2)));

        using (Assert.Multiple())
        {
            await Assert.That(integer.Kind).IsEqualTo(NumberValueKind.Integer);
            await Assert.That(integer.TryGetInteger(out var integerValue)).IsTrue();
            await Assert.That(integerValue).IsEqualTo(BigInteger.Parse("18446744073709551616"));
            await Assert.That(integer.TryGetReal(out _)).IsFalse();
            await Assert.That(real.Kind).IsEqualTo(NumberValueKind.Real);
            await Assert.That(real.TryGetReal(out var realValue)).IsTrue();
            await Assert.That(realValue).IsEqualTo(new RealValue(new BigInteger(125), new BigInteger(-2)));
            await Assert.That(real.TryGetInteger(out _)).IsFalse();
        }
    }

    /// <summary>
    /// Verifies NUMBER arithmetic preserves INTEGER results and promotes mixed inputs to exact REAL values.
    /// </summary>
    [Test]
    public async Task Should_execute_numeric_operations_across_alternatives()
    {
        var two = NumberValue.FromInteger(2);
        var three = NumberValue.FromInteger(3);
        var half = NumberValue.FromReal(new RealValue(5, -1));

        using (Assert.Multiple())
        {
            await Assert.That((two + three).TryGetInteger(out var sum)).IsTrue();
            await Assert.That(sum).IsEqualTo(new BigInteger(5));
            await Assert.That((two + half).TryGetReal(out var mixed)).IsTrue();
            await Assert.That(mixed).IsEqualTo(new RealValue(25, -1));
            await Assert.That(two * half).IsEqualTo(NumberValue.FromReal(new RealValue(1, 0)));
            await Assert.That(three - two).IsEqualTo(NumberValue.FromInteger(1));
            await Assert.That(three / two).IsEqualTo(new RealValue(15, -1));
            await Assert.That(-three).IsEqualTo(NumberValue.FromInteger(-3));
            await Assert.That(half < two).IsTrue();
            await Assert.That(NumberValue.FromReal(new RealValue(2, 0)).CompareTo(two)).IsEqualTo(0);
        }
    }

    /// <summary>
    /// Verifies VALUE-compatible parsing accepts exact INTEGER/REAL literals and rejects all other strings.
    /// </summary>
    [Test]
    public async Task Should_parse_only_express_numeric_literal_spellings()
    {
        using (Assert.Multiple())
        {
            await Assert.That(NumberValue.TryParse("20", out var integer)).IsTrue();
            await Assert.That(integer).IsEqualTo(NumberValue.FromInteger(20));
            await Assert.That(NumberValue.TryParse("1.234e2", out var real)).IsTrue();
            await Assert.That(real).IsEqualTo(NumberValue.FromReal(new RealValue(1234, -1)));
            await Assert.That(NumberValue.TryParse("1.", out var trailingPoint)).IsTrue();
            await Assert.That(trailingPoint).IsEqualTo(NumberValue.FromReal(new RealValue(1, 0)));
            await Assert.That(NumberValue.TryParse("-1", out _)).IsFalse();
            await Assert.That(NumberValue.TryParse("abc", out _)).IsFalse();
            await Assert.That(NumberValue.TryParse("1e2", out _)).IsFalse();
        }
    }

    /// <summary>
    /// Verifies FORMAT symbolic, picture, and default forms against the ISO examples.
    /// </summary>
    [Test]
    public async Task Should_format_symbolic_picture_and_default_representations()
    {
        var ten = NumberValue.FromInteger(10);
        var real = NumberValue.FromReal(new RealValue(123456789, -6));
        var grouped = NumberValue.FromReal(new RealValue(7123456, -3));

        using (Assert.Multiple())
        {
            await Assert.That(ten.Format("+7I")).IsEqualTo("    +10");
            await Assert.That(ten.Format("+07I")).IsEqualTo("+000010");
            await Assert.That(ten.Format("10.3E")).IsEqualTo(" 1.000E+01");
            await Assert.That(real.Format("8.2F")).IsEqualTo("  123.46");
            await Assert.That(real.Format("8.2E")).IsEqualTo(" 1.23E+02");
            await Assert.That(NumberValue.FromReal(new RealValue(32777, -3)).Format("6I"))
                .IsEqualTo("    33");
            await Assert.That(grouped.Format("###,###.##")).IsEqualTo("  7,123.46");
            await Assert.That(grouped.Format("###.###,##")).IsEqualTo("  7.123,46");
            await Assert.That(NumberValue.FromInteger(-10).Format("(###)")).IsEqualTo("( 10)");
            await Assert.That(ten.Format("###")).IsEqualTo(" 10");
            await Assert.That(ten.Format(string.Empty)).IsEqualTo("     10");
        }
    }
}
