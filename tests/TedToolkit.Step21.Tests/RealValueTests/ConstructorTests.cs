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
}
