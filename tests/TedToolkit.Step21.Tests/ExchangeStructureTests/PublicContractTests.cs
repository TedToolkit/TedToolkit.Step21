using System.Reflection;

namespace TedToolkit.Step21.Tests.ExchangeStructureTests;

internal sealed class PublicContractTests
{
    /// <summary>
    /// Verifies the exact section-required Add/Remove, validation, and construction surface with no Replace operation.
    /// </summary>
    [Test]
    public async Task Should_expose_only_approved_exchange_structure_operations()
    {
        var type = typeof(ExchangeStructure);
        var constructors = type.GetConstructors().Select(Format).Order().ToArray();
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(Format)
            .Order()
            .ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(constructors).IsEquivalentTo([
                ".ctor(HeaderSection)",
                ".ctor(HeaderSection, IReadOnlyCollection<SchemaDescriptor>)",
            ]);
            await Assert.That(methods).IsEquivalentTo([
                "Add(DataSection, Entity) -> EntityInstanceName",
                "Add(DataSection, EntityInstanceName, Entity) -> Void",
                "Remove(Entity) -> Boolean",
                "Remove(EntityInstanceName) -> Boolean",
                "Validate() -> ValidationResult",
                "Write(TextWriter) -> Void",
                "WriteEntity(TextWriter, Entity) -> Void",
            ]);
            await Assert.That(type.GetMethod("Replace")).IsNull();
        }
    }

    /// <summary>
    /// Exposes registered model values for public navigation without exposing occurrence registrations.
    /// </summary>
    [Test]
    public async Task Should_expose_a_live_read_only_entity_view_in_registration_order()
    {
        var structure = new ExchangeStructure(new HeaderSection(
            new FileDescription(["entities"], "3;1"),
            new FileName("entities.p21", "2026-08-22T00:00:00", [], [], "Pre", "System", "Auth"),
            new FileSchema(["model"])));
        var section = new DataSection(new SchemaName("model"));
        structure.DataSections.Add(section);
        var first = new TestEntity();
        var second = new TestEntity();
        var view = structure.Entities;

        structure.Add(section, first);
        structure.Add(section, second);
        var initial = view.ToArray();
        _ = structure.Remove(first);

        using (Assert.Multiple())
        {
            await Assert.That(initial.SequenceEqual([first, second])).IsTrue();
            await Assert.That(view.SequenceEqual([second])).IsTrue();
        }
    }

    private static string Format(MethodBase method)
    {
        var parameters = string.Join(", ", method.GetParameters().Select(parameter => Format(parameter.ParameterType)));
        var name = method is ConstructorInfo ? ".ctor" : method.Name;
        return method is MethodInfo methodInfo
            ? $"{name}({parameters}) -> {Format(methodInfo.ReturnType)}"
            : $"{name}({parameters})";
    }

    private static string Format(Type type) => type.IsGenericType
        ? $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(", ", type.GetGenericArguments().Select(Format))}>"
        : type.Name;

    private sealed class TestEntity : Entity
    {
        public override IEnumerable<Entity> DirectReferences => [];
    }
}