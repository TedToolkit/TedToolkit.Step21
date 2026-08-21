using System.Numerics;

namespace TedToolkit.Step21.Tests.ParameterValueTests;

internal sealed class FactoryTests
{
    /// <summary>
    /// Verifies that every schema-neutral scalar and marker remains a distinct parameter alternative.
    /// </summary>
    [Test]
    public async Task Should_preserve_every_scalar_and_marker_alternative()
    {
        var values = new[]
        {
            ParameterValue.Omitted,
            ParameterValue.Derived,
            ParameterValue.FromInteger(BigInteger.Parse("18446744073709551616")),
            ParameterValue.FromReal(new RealValue(new BigInteger(125), new BigInteger(-2))),
            ParameterValue.FromString("O'Brien"),
            ParameterValue.FromBinary(new BinaryValue("00101")),
            ParameterValue.FromBoolean(true),
            ParameterValue.FromLogical(LogicalValue.Unknown),
            ParameterValue.FromEnumeration("CUSTOM_VALUE"),
        };

        await Assert.That(values.Select(value => value.Kind)).IsEquivalentTo(
        [
            ParameterValueKind.Omitted,
            ParameterValueKind.Derived,
            ParameterValueKind.Integer,
            ParameterValueKind.Real,
            ParameterValueKind.String,
            ParameterValueKind.Binary,
            ParameterValueKind.Boolean,
            ParameterValueKind.Logical,
            ParameterValueKind.Enumeration,
        ]);
    }

    /// <summary>
    /// Verifies recursive aggregate, typed, and resolved entity parameters without an object payload.
    /// </summary>
    [Test]
    public async Task Should_preserve_recursive_typed_and_resolved_entity_alternatives()
    {
        var entity = new TestEntity();
        var aggregate = ParameterValue.FromAggregate(
        [
            ParameterValue.FromInteger(BigInteger.One),
            ParameterValue.FromAggregate([ParameterValue.Omitted, ParameterValue.Derived]),
        ]);
        var typed = ParameterValue.FromTyped("LENGTH_MEASURE", aggregate);
        var resolved = ParameterValue.FromEntity(entity);

        using (Assert.Multiple())
        {
            await Assert.That(aggregate.TryGetAggregate(out var elements)).IsTrue();
            await Assert.That(elements).Count().IsEqualTo(2);
            await Assert.That(typed.TryGetTyped(out var typeName, out var inner)).IsTrue();
            await Assert.That(typeName).IsEqualTo("LENGTH_MEASURE");
            await Assert.That(inner).IsSameReferenceAs(aggregate);
            await Assert.That(resolved.TryGetEntity(out var actualEntity)).IsTrue();
            await Assert.That(actualEntity).IsSameReferenceAs(entity);
            await Assert.That(typeof(ParameterValue).GetProperties().Select(property => property.PropertyType))
                .DoesNotContain(typeof(object));
            await Assert.That(typeof(ParameterValue).GetNestedTypes()).IsEmpty();
        }
    }

    /// <summary>
    /// Verifies structural parameter equality while resolved entity alternatives retain reference identity.
    /// </summary>
    [Test]
    public async Task Should_compare_strong_parameter_values_without_erasing_their_alternatives()
    {
        var entity = new TestEntity();
        var first = ParameterValue.FromTyped(
            "MEASURE",
            ParameterValue.FromAggregate(
            [
                ParameterValue.FromInteger(BigInteger.One),
                ParameterValue.FromBinary(new BinaryValue("001")),
                ParameterValue.FromEntity(entity),
            ]));
        var equal = ParameterValue.FromTyped(
            "MEASURE",
            ParameterValue.FromAggregate(
            [
                ParameterValue.FromInteger(BigInteger.One),
                ParameterValue.FromBinary(new BinaryValue("001")),
                ParameterValue.FromEntity(entity),
            ]));
        var differentEntity = ParameterValue.FromTyped(
            "MEASURE",
            ParameterValue.FromAggregate(
            [
                ParameterValue.FromInteger(BigInteger.One),
                ParameterValue.FromBinary(new BinaryValue("001")),
                ParameterValue.FromEntity(new TestEntity()),
            ]));

        using (Assert.Multiple())
        {
            await Assert.That(first).IsEqualTo(equal);
            await Assert.That(first.GetHashCode()).IsEqualTo(equal.GetHashCode());
            await Assert.That(first).IsNotEqualTo(differentEntity);
            await Assert.That(ParameterValue.Omitted).IsNotEqualTo(ParameterValue.Derived);
            await Assert.That(ParameterValue.FromBoolean(false))
                .IsNotEqualTo(ParameterValue.FromLogical(LogicalValue.False));
        }
    }

    private sealed class TestEntity : Entity
    {
        public override IEnumerable<Entity> DirectReferences => [];
    }
}
