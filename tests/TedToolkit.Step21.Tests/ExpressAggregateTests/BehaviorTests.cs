using System.Xml.Linq;

namespace TedToolkit.Step21.Tests.ExpressAggregateTests;

internal sealed class BehaviorTests
{
    /// <summary>
    /// Verifies that the four EXPRESS aggregate categories expose distinct editing contracts.
    /// </summary>
    [Test]
    public async Task Should_retain_category_specific_editing_semantics()
    {
        var list = new ExpressList<int> { 3, 1, 1 };
        var bag = new ExpressBag<int> { 3, 1, 1 };
        var set = new ExpressSet<int> { 3, 1, 1 };
        var array = new ExpressArray<int>(-1, 1);

        list[0] = 2;
        _ = bag.Remove(3);
        _ = set.Remove(3);
        array[-1] = 2;
        array[1] = 1;

        using (Assert.Multiple())
        {
            await Assert.That(list.SequenceEqual([2, 1, 1])).IsTrue();
            await Assert.That(bag.Count).IsEqualTo(2);
            await Assert.That(set.Count).IsEqualTo(2);
            await Assert.That(array.Count).IsEqualTo(3);
            await Assert.That(array.IsSet(0)).IsFalse();
            await Assert.That(array[-1]).IsEqualTo(2);
            await Assert.That(array[1]).IsEqualTo(1);
        }
    }

    /// <summary>
    /// Verifies that mutable entity aggregates expose the same instance through covariant read-only views.
    /// </summary>
    [Test]
    public async Task Should_expose_covariant_read_only_views_without_copying_aggregate_state()
    {
        var element = new DerivedEntity();
        var array = new ExpressArray<DerivedEntity>(1, 1);
        var list = new ExpressList<DerivedEntity> { element };
        var bag = new ExpressBag<DerivedEntity> { element };
        var set = new ExpressSet<DerivedEntity> { element };
        array[1] = element;

        IExpressArray<BaseEntity> arrayView = array;
        IExpressList<BaseEntity> listView = list;
        IExpressBag<BaseEntity> bagView = bag;
        IExpressSet<BaseEntity> setView = set;

        using (Assert.Multiple())
        {
            await Assert.That(arrayView).IsSameReferenceAs(array);
            await Assert.That(listView).IsSameReferenceAs(list);
            await Assert.That(bagView).IsSameReferenceAs(bag);
            await Assert.That(setView).IsSameReferenceAs(set);
            await Assert.That(arrayView[1]).IsSameReferenceAs(element);
            await Assert.That(listView[0]).IsSameReferenceAs(element);
            await Assert.That(bagView.Single()).IsSameReferenceAs(element);
            await Assert.That(setView.Single()).IsSameReferenceAs(element);
            await Assert.That(arrayView.LowerIndex).IsEqualTo(1);
            await Assert.That(arrayView.UpperIndex).IsEqualTo(1);
            await Assert.That(listView.LowerBound).IsEqualTo(0);
            await Assert.That(bagView.UpperBound).IsNull();
            await Assert.That(setView.Validate().IsValid).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that LIST accepts upper-bound and uniqueness violations until explicit validation.
    /// </summary>
    [Test]
    public async Task Should_report_every_list_violation_without_mutating_the_candidate()
    {
        var value = new ExpressList<int>(lowerBound: 0, upperBound: 2, isUnique: true) { 1, 1, 2 };

        var result = value.Validate("ENTITY.Items");

        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(string.Join('|', result.Failures.Select(failure => failure.Code)))
                .IsEqualTo("EXPRESS.AGGREGATE.UPPER_BOUND|EXPRESS.AGGREGATE.UNIQUE");
            await Assert.That(result.Failures.All(failure => failure.Path == "ENTITY.Items")).IsTrue();
            await Assert.That(value.SequenceEqual([1, 1, 2])).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that lower-bound validation observes an empty LIST without repairing it.
    /// </summary>
    [Test]
    public async Task Should_report_list_lower_bound_without_adding_elements()
    {
        var value = new ExpressList<int>(lowerBound: 2);

        var result = value.Validate();

        using (Assert.Multiple())
        {
            await Assert.That(result.Failures.Single().Code).IsEqualTo("EXPRESS.AGGREGATE.LOWER_BOUND");
            await Assert.That(result.Failures.Single().Path).IsEqualTo("$");
            await Assert.That(value.Count).IsEqualTo(0);
        }
    }

    /// <summary>
    /// Verifies that BAG preserves multiplicity and validates only its declared cardinality bounds.
    /// </summary>
    [Test]
    public async Task Should_preserve_bag_duplicates_when_bound_validation_runs()
    {
        var value = new ExpressBag<string>(upperBound: 2) { "same", "same", "same" };

        var result = value.Validate("ENTITY.Bag");

        using (Assert.Multiple())
        {
            await Assert.That(result.Failures.Single().Code).IsEqualTo("EXPRESS.AGGREGATE.UPPER_BOUND");
            await Assert.That(value.Count).IsEqualTo(3);
            await Assert.That(value.Count(item => item == "same")).IsEqualTo(3);
        }
    }

    /// <summary>
    /// Verifies that SET keeps duplicate edit candidates so explicit validation can report them.
    /// </summary>
    [Test]
    public async Task Should_preserve_set_duplicates_until_explicit_validation()
    {
        var value = new ExpressSet<string> { "same", "same" };

        var result = value.Validate("ENTITY.Set");

        using (Assert.Multiple())
        {
            await Assert.That(result.Failures.Single().Code).IsEqualTo("EXPRESS.AGGREGATE.UNIQUE");
            await Assert.That(value.Count).IsEqualTo(2);
            await Assert.That(value.Count(item => item == "same")).IsEqualTo(2);
        }
    }

    /// <summary>
    /// Verifies that ARRAY reports every required-slot and uniqueness issue while retaining its index domain.
    /// </summary>
    [Test]
    public async Task Should_report_array_slot_and_uniqueness_violations_without_mutation()
    {
        var value = new ExpressArray<int>(lowerIndex: -1, upperIndex: 1, isUnique: true);
        value[-1] = 7;
        value[1] = 7;

        var result = value.Validate("ENTITY.Array");

        using (Assert.Multiple())
        {
            await Assert.That(string.Join('|', result.Failures.Select(failure => failure.Code)))
                .IsEqualTo("EXPRESS.ARRAY.REQUIRED_SLOT|EXPRESS.AGGREGATE.UNIQUE");
            await Assert.That(result.Failures[0].Path).IsEqualTo("ENTITY.Array[0]");
            await Assert.That(value.IsSet(-1)).IsTrue();
            await Assert.That(value.IsSet(0)).IsFalse();
            await Assert.That(value.IsSet(1)).IsTrue();
            await Assert.That(value[-1]).IsEqualTo(7);
            await Assert.That(value[1]).IsEqualTo(7);
        }
    }

    /// <summary>
    /// Verifies that OPTIONAL ARRAY slots may be explicitly unset after assignment.
    /// </summary>
    [Test]
    public async Task Should_allow_optional_array_slots_to_be_unset()
    {
        var value = new ExpressArray<int>(1, 2, isOptional: true);
        value[1] = 42;

        value.Unset(1);
        var result = value.Validate();

        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsTrue();
            await Assert.That(value.IsSet(1)).IsFalse();
            await Assert.That(value.TryGetValue(1, out _)).IsFalse();
        }
    }

    /// <summary>
    /// Verifies ARRAY value copies retain metadata, unset slots, and equality semantics without sharing storage.
    /// </summary>
    [Test]
    public async Task Should_copy_array_values_without_sharing_slot_storage()
    {
        var source = new ExpressArray<string>(
            -1,
            1,
            isOptional: true,
            isUnique: true,
            comparer: StringComparer.OrdinalIgnoreCase);
        source[-1] = "A";
        source[1] = "a";

        var copy = new ExpressArray<string>(source);
        source[-1] = "changed";

        using (Assert.Multiple())
        {
            await Assert.That(copy.LowerIndex).IsEqualTo(-1);
            await Assert.That(copy.UpperIndex).IsEqualTo(1);
            await Assert.That(copy.IsOptional).IsTrue();
            await Assert.That(copy.IsUnique).IsTrue();
            await Assert.That(copy[-1]).IsEqualTo("A");
            await Assert.That(copy.IsSet(0)).IsFalse();
            await Assert.That(copy[1]).IsEqualTo("a");
            await Assert.That(copy.Validate().Failures.Single().Code)
                .IsEqualTo("EXPRESS.AGGREGATE.UNIQUE");
        }
    }

    /// <summary>
    /// Verifies that valid candidates in all four categories produce empty validation results.
    /// </summary>
    [Test]
    public async Task Should_validate_all_four_categories_when_candidates_are_valid()
    {
        var list = new ExpressList<int>(1, 3, isUnique: true) { 1, 2 };
        var bag = new ExpressBag<int>(1, 3) { 1, 1 };
        var set = new ExpressSet<int>(1, 3) { 1, 2 };
        var array = new ExpressArray<int>(3, 4, isOptional: false, isUnique: true);
        array[3] = 1;
        array[4] = 2;

        using (Assert.Multiple())
        {
            await Assert.That(list.Validate().IsValid).IsTrue();
            await Assert.That(bag.Validate().IsValid).IsTrue();
            await Assert.That(set.Validate().IsValid).IsTrue();
            await Assert.That(array.Validate().IsValid).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that invalid declaration metadata and out-of-domain ARRAY indexes fail immediately.
    /// </summary>
    [Test]
    public async Task Should_reject_invalid_metadata_and_array_indexes()
    {
        static void CreateInvalidList() => _ = new ExpressList<int>(2, 1);
        static void CreateInvalidBag() => _ = new ExpressBag<int>(-1);
        static void CreateInvalidSet() => _ = new ExpressSet<int>(0, -1);
        static void CreateInvalidArray() => _ = new ExpressArray<int>(2, 1);
        static void ReadInvalidIndex()
        {
            var value = new ExpressArray<int>(1, 2);
            _ = value[0];
        }

        using (Assert.Multiple())
        {
            await Assert.That((Action)CreateInvalidList).Throws<ArgumentOutOfRangeException>();
            await Assert.That((Action)CreateInvalidBag).Throws<ArgumentOutOfRangeException>();
            await Assert.That((Action)CreateInvalidSet).Throws<ArgumentOutOfRangeException>();
            await Assert.That((Action)CreateInvalidArray).Throws<ArgumentOutOfRangeException>();
            await Assert.That((Action)ReadInvalidIndex).Throws<ArgumentOutOfRangeException>();
        }
    }

    /// <summary>
    /// Verifies that supplied equality semantics govern LIST, SET, and ARRAY uniqueness checks.
    /// </summary>
    [Test]
    public async Task Should_use_the_supplied_equality_comparer_for_unique_categories()
    {
        var list = new ExpressList<string>(isUnique: true, comparer: StringComparer.OrdinalIgnoreCase) { "A", "a" };
        var set = new ExpressSet<string>(comparer: StringComparer.OrdinalIgnoreCase) { "A", "a" };
        var array = new ExpressArray<string>(1, 2, isUnique: true, comparer: StringComparer.OrdinalIgnoreCase);
        array[1] = "A";
        array[2] = "a";

        using (Assert.Multiple())
        {
            await Assert.That(list.Validate().Failures.Single().Code).IsEqualTo("EXPRESS.AGGREGATE.UNIQUE");
            await Assert.That(set.Validate().Failures.Single().Code).IsEqualTo("EXPRESS.AGGREGATE.UNIQUE");
            await Assert.That(array.Validate().Failures.Single().Code).IsEqualTo("EXPRESS.AGGREGATE.UNIQUE");
            await Assert.That(set.Contains("a")).IsTrue();
            await Assert.That(set.Remove("a")).IsTrue();
            await Assert.That(set.Count).IsEqualTo(1);
        }
    }

    /// <summary>
    /// Verifies that published XML explains every caller-visible EXPRESS aggregate distinction and validation timing.
    /// </summary>
    [Test]
    public async Task Should_document_aggregate_semantics_in_runtime_xml()
    {
        var assembly = typeof(ExpressList<>).Assembly;
        var members = XDocument.Load(Path.ChangeExtension(assembly.Location, ".xml"))
            .Descendants("member")
            .Where(member => ((string?)member.Attribute("name"))?.StartsWith(
                "T:TedToolkit.Step21.Express",
                StringComparison.Ordinal) == true)
            .ToDictionary(member => (string)member.Attribute("name")!, member => member.Value, StringComparer.Ordinal);

        using (Assert.Multiple())
        {
            await Assert.That(members["T:TedToolkit.Step21.ExpressList`1"])
                .Contains("retains order and multiplicity")
                .And.Contains("never run validation");
            await Assert.That(members["T:TedToolkit.Step21.ExpressBag`1"])
                .Contains("preserves multiplicity")
                .And.Contains("no EXPRESS ordering meaning");
            await Assert.That(members["T:TedToolkit.Step21.ExpressSet`1"])
                .Contains("without silently discarding duplicates")
                .And.Contains("report the violated set uniqueness rule");
            await Assert.That(members["T:TedToolkit.Step21.ExpressArray`1"])
                .Contains("fixed declared index domain")
                .And.Contains("Unset")
                .And.Contains("never run validation");
        }
    }

    private class BaseEntity;

    private sealed class DerivedEntity : BaseEntity;
}
