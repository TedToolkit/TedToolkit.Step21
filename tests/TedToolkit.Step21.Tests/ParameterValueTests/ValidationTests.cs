namespace TedToolkit.Step21.Tests.ParameterValueTests;

internal sealed class ValidationTests
{
    /// <summary>
    /// Verifies that null payloads, invalid logical states, and non-canonical names cannot enter the strong union.
    /// </summary>
    [Test]
    public async Task Should_reject_invalid_parameter_payloads()
    {
        static void CreateNullString() => _ = ParameterValue.FromString(null!);
        static void CreateNullBinary() => _ = ParameterValue.FromBinary(null!);
        static void CreateInvalidLogical() => _ = ParameterValue.FromLogical((LogicalValue)99);
        static void CreateInvalidEnumeration() => _ = ParameterValue.FromEnumeration("not-canonical");
        static void CreateNullEntity() => _ = ParameterValue.FromEntity(null!);
        static void CreateNullAggregate() => _ = ParameterValue.FromAggregate(null!);
        static void CreateNullAggregateElement() => _ = ParameterValue.FromAggregate([ParameterValue.Omitted, null!]);
        static void CreateInvalidTypeName() => _ = ParameterValue.FromTyped("length-measure", ParameterValue.Omitted);
        static void CreateNullTypedValue() => _ = ParameterValue.FromTyped("LENGTH_MEASURE", null!);

        using (Assert.Multiple())
        {
            await Assert.That((Action)CreateNullString).Throws<ArgumentNullException>();
            await Assert.That((Action)CreateNullBinary).Throws<ArgumentNullException>();
            await Assert.That((Action)CreateInvalidLogical).Throws<ArgumentOutOfRangeException>();
            await Assert.That((Action)CreateInvalidEnumeration).Throws<FormatException>();
            await Assert.That((Action)CreateNullEntity).Throws<ArgumentNullException>();
            await Assert.That((Action)CreateNullAggregate).Throws<ArgumentNullException>();
            await Assert.That((Action)CreateNullAggregateElement).Throws<ArgumentException>();
            await Assert.That((Action)CreateInvalidTypeName).Throws<FormatException>();
            await Assert.That((Action)CreateNullTypedValue).Throws<ArgumentNullException>();
        }
    }
}
