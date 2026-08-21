namespace TedToolkit.Step21.Tests.ExchangeStructureTests;

internal sealed class AddTests
{
    /// <summary>
    /// Verifies smallest-gap automatic allocation, arbitrary precision, canonical explicit identity, and reuse.
    /// </summary>
    [Test]
    public async Task Should_allocate_smallest_available_positive_name_and_reuse_gaps()
    {
        var (structure, section) = CreateStructure();
        var first = new TestEntity();
        var second = new TestEntity();
        var large = new TestEntity();

        var firstName = structure.Add(section, first);
        var secondName = structure.Add(section, second);
        structure.Add(section, new EntityInstanceName("00018446744073709551616"), large);
        _ = structure.Remove(firstName);
        var reused = structure.Add(section, new TestEntity());

        using (Assert.Multiple())
        {
            await Assert.That(firstName.ToString()).IsEqualTo("#1");
            await Assert.That(secondName.ToString()).IsEqualTo("#2");
            await Assert.That(reused.ToString()).IsEqualTo("#1");
            await Assert.That(structure.TryGetEntity(new EntityInstanceName("18446744073709551616"), out var entity)).IsTrue();
            await Assert.That(entity).IsSameReferenceAs(large);
        }
    }

    /// <summary>
    /// Verifies identical-pair idempotence and both conflict directions without partial mutation.
    /// </summary>
    [Test]
    public async Task Should_treat_identical_pair_as_idempotent_and_reject_both_conflicts_atomically()
    {
        var (structure, section) = CreateStructure();
        var registered = new TestEntity();
        var other = new TestEntity();
        var name = new EntityInstanceName("7");
        structure.Add(section, name, registered);
        structure.Add(section, name, registered);
        var before = structure.Registrations.ToArray();

        void ReuseName() => structure.Add(section, name, other);
        void ReuseEntity() => structure.Add(section, new EntityInstanceName("8"), registered);

        using (Assert.Multiple())
        {
            await Assert.That((Action)ReuseName).Throws<InvalidOperationException>();
            await Assert.That((Action)ReuseEntity).Throws<InvalidOperationException>();
            await Assert.That(structure.Registrations.SequenceEqual(before)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies root-first depth-first graph registration with repeats, sharing, and cycles.
    /// </summary>
    [Test]
    public async Task Should_register_graph_depth_first_once_per_reference_identity()
    {
        var (structure, section) = CreateStructure();
        var shared = new TestEntity();
        var left = new TestEntity(shared, shared);
        var right = new TestEntity(shared);
        var root = new TestEntity(left, right);
        shared.References.Add(root);

        var rootName = structure.Add(section, root);

        using (Assert.Multiple())
        {
            await Assert.That(root.DirectReferences.SequenceEqual([left, right])).IsTrue();
            await Assert.That(left.DirectReferences.SequenceEqual([shared, shared])).IsTrue();
            await Assert.That(structure.Registrations.Select(item => item.Entity).SequenceEqual([root, left, shared, right])).IsTrue();
            await Assert.That(structure.Registrations.Select(item => item.Name.ToString()).SequenceEqual(["#1", "#2", "#3", "#4"])).IsTrue();
            await Assert.That(structure.Registrations.All(item => ReferenceEquals(item.DataSection, section))).IsTrue();
            await Assert.That(rootName.ToString()).IsEqualTo("#1");
        }
    }

    /// <summary>
    /// Verifies that re-adding a registered root observes new live references and preserves prior membership.
    /// </summary>
    [Test]
    public async Task Should_reenumerate_registered_root_and_preserve_existing_names_and_sections()
    {
        var structure = new ExchangeStructure(TestHeader.Create());
        var firstSection = new DataSection(new SchemaName("FIRST"));
        var secondSection = new DataSection(new SchemaName("SECOND"));
        structure.DataSections.Add(firstSection);
        structure.DataSections.Add(secondSection);
        var existingChild = new TestEntity();
        var root = new TestEntity(existingChild);
        structure.Add(secondSection, new EntityInstanceName("9"), existingChild);
        var rootName = structure.Add(firstSection, root);
        var lateChild = new TestEntity();
        root.References.Add(lateChild);

        structure.Add(firstSection, rootName, root);
        var secondLateChild = new TestEntity();
        root.References.Add(secondLateChild);
        var repeatedRootName = structure.Add(firstSection, root);

        using (Assert.Multiple())
        {
            await Assert.That(repeatedRootName).IsEqualTo(rootName);
            await Assert.That(structure.TryGetSection(existingChild, out var preservedSection)).IsTrue();
            await Assert.That(preservedSection).IsSameReferenceAs(secondSection);
            await Assert.That(structure.TryGetName(existingChild, out var preservedName)).IsTrue();
            await Assert.That(preservedName.ToString()).IsEqualTo("#9");
            await Assert.That(structure.TryGetSection(lateChild, out var lateSection)).IsTrue();
            await Assert.That(lateSection).IsSameReferenceAs(firstSection);
            await Assert.That(structure.TryGetSection(secondLateChild, out var secondLateSection)).IsTrue();
            await Assert.That(secondLateSection).IsSameReferenceAs(firstSection);
            await Assert.That(structure.Registrations.Count).IsEqualTo(4);
        }
    }

    /// <summary>
    /// Verifies that an explicit root is registered first while descendants receive the smallest automatic names.
    /// </summary>
    [Test]
    public async Task Should_preserve_explicit_root_name_and_allocate_descendants_in_graph_order()
    {
        var (structure, section) = CreateStructure();
        var child = new TestEntity();
        var root = new TestEntity(child);

        structure.Add(section, new EntityInstanceName("100"), root);

        using (Assert.Multiple())
        {
            await Assert.That(structure.Registrations.Select(item => item.Entity).SequenceEqual([root, child])).IsTrue();
            await Assert.That(structure.Registrations.Select(item => item.Name.ToString()).SequenceEqual(["#100", "#1"])).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that a foreign section is rejected before graph enumeration or any structure mutation.
    /// </summary>
    [Test]
    public async Task Should_reject_foreign_section_before_enumeration_or_mutation()
    {
        var (structure, ownedSection) = CreateStructure();
        var existing = new TestEntity();
        _ = structure.Add(ownedSection, existing);
        var root = new CountingEntity();
        var before = structure.Registrations.ToArray();

        void AddToForeignSection() => structure.Add(new DataSection(new SchemaName("FOREIGN")), root);

        using (Assert.Multiple())
        {
            await Assert.That((Action)AddToForeignSection).Throws<ArgumentException>();
            await Assert.That(root.EnumerationCount).IsEqualTo(0);
            await Assert.That(structure.Registrations.SequenceEqual(before)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that the same entity object receives independent identity in separate structures.
    /// </summary>
    [Test]
    public async Task Should_assign_independent_names_in_separate_structures()
    {
        var entity = new TestEntity();
        var (first, firstSection) = CreateStructure();
        var (second, secondSection) = CreateStructure();
        _ = first.Add(firstSection, entity);
        second.Add(secondSection, new EntityInstanceName("42"), entity);

        using (Assert.Multiple())
        {
            await Assert.That(first.TryGetName(entity, out var firstName)).IsTrue();
            await Assert.That(second.TryGetName(entity, out var secondName)).IsTrue();
            await Assert.That(firstName.ToString()).IsEqualTo("#1");
            await Assert.That(secondName.ToString()).IsEqualTo("#42");
            await Assert.That(typeof(Entity).GetProperties().Select(property => property.Name)).IsEquivalentTo(["DirectReferences"]);
        }
    }

    /// <summary>
    /// Verifies that overridden value equality never merges distinct entity objects.
    /// </summary>
    [Test]
    public async Task Should_index_entities_only_by_clr_reference_identity()
    {
        var (structure, section) = CreateStructure();
        var first = new ValueEqualEntity();
        var second = new ValueEqualEntity();
        var root = new TestEntity(first, second);

        _ = structure.Add(section, root);

        using (Assert.Multiple())
        {
            await Assert.That(first.Equals(second)).IsTrue();
            await Assert.That(structure.TryGetName(first, out var firstName)).IsTrue();
            await Assert.That(structure.TryGetName(second, out var secondName)).IsTrue();
            await Assert.That(firstName).IsNotEqualTo(secondName);
            await Assert.That(structure.Registrations.Count).IsEqualTo(3);
        }
    }

    private static (ExchangeStructure Structure, DataSection Section) CreateStructure()
    {
        var structure = new ExchangeStructure(TestHeader.Create());
        var section = new DataSection(new SchemaName("TEST_SCHEMA"));
        structure.DataSections.Add(section);
        return (structure, section);
    }

    private sealed class CountingEntity : Entity
    {
        internal int EnumerationCount { get; private set; }

        public override IEnumerable<Entity> DirectReferences
        {
            get
            {
                EnumerationCount++;
                return [];
            }
        }
    }

    private sealed class ValueEqualEntity : Entity
    {
        public override IEnumerable<Entity> DirectReferences => [];

        public override bool Equals(object? obj) => obj is ValueEqualEntity;

        public override int GetHashCode() => 0;
    }
}
