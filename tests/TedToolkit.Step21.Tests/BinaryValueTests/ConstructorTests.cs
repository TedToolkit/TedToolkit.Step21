namespace TedToolkit.Step21.Tests.BinaryValueTests;

internal sealed class ConstructorTests
{
    /// <summary>
    /// Verifies that binary values preserve leading zeroes and exact bit length.
    /// </summary>
    [Test]
    public async Task Should_preserve_every_supplied_bit()
    {
        var value = new BinaryValue("0010110");

        using (Assert.Multiple())
        {
            await Assert.That(value.Length).IsEqualTo(7);
            await Assert.That(value[0]).IsFalse();
            await Assert.That(value[2]).IsTrue();
            await Assert.That(value.ToString()).IsEqualTo("0010110");
            await Assert.That(value).IsEqualTo(new BinaryValue("0010110"));
            await Assert.That(value).IsNotEqualTo(new BinaryValue("10110"));
        }
    }

    /// <summary>
    /// Verifies that only a non-null sequence of binary digits can construct a binary value.
    /// </summary>
    [Test]
    public async Task Should_reject_non_binary_input()
    {
        static void CreateNull() => _ = new BinaryValue(null!);
        static void CreateInvalid() => _ = new BinaryValue("0102");

        using (Assert.Multiple())
        {
            await Assert.That((Action)CreateNull).Throws<ArgumentNullException>();
            await Assert.That((Action)CreateInvalid).Throws<FormatException>();
        }
    }
}
