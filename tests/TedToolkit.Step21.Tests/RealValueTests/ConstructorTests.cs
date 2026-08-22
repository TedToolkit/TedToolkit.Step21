using System.Numerics;

namespace TedToolkit.Step21.Tests.RealValueTests;

internal sealed class ConstructorTests
{
    /// <summary>
    /// Verifies that equivalent arbitrary-precision decimals share one normalized value.
    /// </summary>
    [Test]
    public async Task Should_normalize_trailing_decimal_zeroes_without_narrowing_the_exponent()
    {
        var normalized = new RealValue(new BigInteger(12), BigInteger.Zero);
        var redundant = new RealValue(new BigInteger(1200), new BigInteger(-2));
        var extreme = new RealValue(BigInteger.One, BigInteger.Parse("18446744073709551616"));

        using (Assert.Multiple())
        {
            await Assert.That(redundant).IsEqualTo(normalized);
            await Assert.That(redundant.Significand).IsEqualTo(new BigInteger(12));
            await Assert.That(redundant.Exponent).IsEqualTo(BigInteger.Zero);
            await Assert.That(extreme.Exponent).IsEqualTo(BigInteger.Parse("18446744073709551616"));
            await Assert.That(default(RealValue)).IsEqualTo(new RealValue(BigInteger.Zero, new BigInteger(99)));
        }
    }

    /// <summary>
    /// Verifies finite CLR approximations enter and leave the exact decimal representation deterministically.
    /// </summary>
    [Test]
    public async Task Should_convert_only_finite_double_values_with_round_trip_precision()
    {
        var value = RealValue.FromDouble(1.25);

        using (Assert.Multiple())
        {
            await Assert.That(value).IsEqualTo(new RealValue(125, -2));
            await Assert.That(value.ToDouble()).IsEqualTo(1.25);
            await Assert.That(() => RealValue.FromDouble(double.NaN)).Throws<ArgumentOutOfRangeException>();
            await Assert.That(() => RealValue.FromDouble(double.PositiveInfinity))
                .Throws<ArgumentOutOfRangeException>();
        }
    }

    /// <summary>
    /// Verifies decimal arithmetic remains exact where possible and division returns a deterministic finite approximation.
    /// </summary>
    [Test]
    public async Task Should_apply_real_arithmetic_and_ordering()
    {
        var left = new RealValue(125, -2);
        var right = new RealValue(25, -1);

        using (Assert.Multiple())
        {
            await Assert.That(left + right).IsEqualTo(new RealValue(375, -2));
            await Assert.That(right - left).IsEqualTo(new RealValue(125, -2));
            await Assert.That(left * right).IsEqualTo(new RealValue(3125, -3));
            await Assert.That(right / left).IsEqualTo(new RealValue(2, 0));
            await Assert.That(left < right).IsTrue();
            await Assert.That(right >= left).IsTrue();
            await Assert.That(-left).IsEqualTo(new RealValue(-125, -2));
            await Assert.That(+left).IsEqualTo(left);
        }
    }
}
