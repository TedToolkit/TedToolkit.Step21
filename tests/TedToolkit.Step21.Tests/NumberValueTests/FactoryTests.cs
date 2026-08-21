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
}
