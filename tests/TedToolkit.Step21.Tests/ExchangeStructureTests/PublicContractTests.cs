using System.Reflection;

namespace TedToolkit.Step21.Tests.ExchangeStructureTests;

internal sealed class PublicContractTests
{
    /// <summary>
    /// Verifies the exact section-required Add/Remove and construction surface with no Replace operation.
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
            ]);
            await Assert.That(type.GetMethod("Replace")).IsNull();
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
}
