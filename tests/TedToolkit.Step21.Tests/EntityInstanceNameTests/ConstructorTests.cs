namespace TedToolkit.Step21.Tests.EntityInstanceNameTests;

internal sealed class ConstructorTests
{
    /// <summary>
    /// Verifies that leading zeros do not change nominal entity-instance identity.
    /// </summary>
    [Test]
    public async Task Should_canonicalize_leading_zeros_for_equality_and_formatting()
    {
        var canonical = new EntityInstanceName("42");
        var padded = new EntityInstanceName("00042");

        using (Assert.Multiple())
        {
            await Assert.That(padded).IsEqualTo(canonical);
            await Assert.That(padded.GetHashCode()).IsEqualTo(canonical.GetHashCode());
            await Assert.That(padded.ToString()).IsEqualTo("#42");
        }
    }

    /// <summary>
    /// Verifies that standard names are not truncated to a CLR integral range.
    /// </summary>
    [Test]
    public async Task Should_retain_names_larger_than_long_max_value()
    {
        const string digits = "184467440737095516161844674407370955161";

        var name = new EntityInstanceName(digits);

        await Assert.That(name.ToString()).IsEqualTo($"#{digits}");
    }

    /// <summary>
    /// Verifies that null, empty, zero, non-decimal, and default values cannot represent an instance name.
    /// </summary>
    [Test]
    public async Task Should_reject_invalid_entity_instance_names()
    {
        static void CreateNull() => _ = new EntityInstanceName(null!);
        static void CreateEmpty() => _ = new EntityInstanceName(string.Empty);
        static void CreateZero() => _ = new EntityInstanceName("000");
        static void CreateNonDecimal() => _ = new EntityInstanceName("1A");
        static void FormatDefault() => _ = default(EntityInstanceName).ToString();

        using (Assert.Multiple())
        {
            await Assert.That((Action)CreateNull).Throws<ArgumentNullException>();
            await Assert.That((Action)CreateEmpty).Throws<FormatException>();
            await Assert.That((Action)CreateZero).Throws<ArgumentOutOfRangeException>();
            await Assert.That((Action)CreateNonDecimal).Throws<FormatException>();
            await Assert.That((Action)FormatDefault).Throws<InvalidOperationException>();
        }
    }
}
