using System.Reflection;

namespace TedToolkit.Step21.Tests.ExchangeStructureTests;

internal sealed class SchemaDescriptorTests
{
    /// <summary>
    /// Verifies the exact public identity and protected generated-extension surface.
    /// </summary>
    [Test]
    public async Task Should_expose_only_the_approved_descriptor_contract()
    {
        var type = typeof(SchemaDescriptor);
        var publicMembers = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(member => member is MethodInfo or PropertyInfo)
            .Select(member => member.Name)
            .Order()
            .ToArray();
        var protectedMethods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.IsFamily)
            .Select(Format)
            .Order()
            .ToArray();
        var internalMethods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.IsAssembly)
            .Select(Format)
            .Order()
            .ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(publicMembers).IsEquivalentTo(["get_Name", "Name"]);
            await Assert.That(protectedMethods).IsEquivalentTo([
                "AllocateEntityCore(IReadOnlyList<String>) -> Entity",
                "GetCapabilityDiagnosticsCore(ExchangeStructure) -> IReadOnlyList<Step21Diagnostic>",
                "HydrateEntityCore(ExchangeStructure, Entity, IReadOnlyList<KeyValuePair<String, IReadOnlyList<ParameterValue>>>) -> IReadOnlyList<Step21Diagnostic>",
                "ProjectEntityCore(Entity) -> IReadOnlyList<KeyValuePair<String, IReadOnlyList<ParameterValue>>>",
                "ValidateCore(ExchangeStructure, IReadOnlyList<KeyValuePair<String, Entity>>) -> ValidationResult",
            ]);
            await Assert.That(internalMethods).IsEquivalentTo([
                "AllocateEntity(IReadOnlyList<String>) -> Entity",
                "GetCapabilityDiagnostics(ExchangeStructure) -> IReadOnlyList<Step21Diagnostic>",
                "HydrateEntity(ExchangeStructure, Entity, IReadOnlyList<KeyValuePair<String, IReadOnlyList<ParameterValue>>>) -> IReadOnlyList<Step21Diagnostic>",
                "ProjectEntity(Entity) -> IReadOnlyList<KeyValuePair<String, IReadOnlyList<ParameterValue>>>",
                "Validate(ExchangeStructure, IReadOnlyList<KeyValuePair<String, Entity>>) -> ValidationResult",
            ]);
            await Assert.That(type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)).IsEmpty();
        }
    }

    /// <summary>
    /// Verifies every runtime-internal non-virtual dispatch method reaches its protected override with strong values.
    /// </summary>
    [Test]
    public async Task Should_dispatch_every_runtime_operation_to_the_protected_core()
    {
        var entity = new TestEntity();
        var projected = new KeyValuePair<string, IReadOnlyList<ParameterValue>>[]
        {
            new("TEST_ENTITY", [ParameterValue.FromString("value")]),
        };
        var descriptor = new DispatchProbeDescriptor(entity, projected);
        var structure = new ExchangeStructure(TestHeader.Create(), [descriptor]);

        using (Assert.Multiple())
        {
            await Assert.That(descriptor.AllocateEntity(["TEST_ENTITY"])).IsSameReferenceAs(entity);
            await Assert.That(descriptor.HydrateEntity(structure, entity, projected).Single().Code)
                .IsEqualTo("TEST-HYDRATE");
            var entities = new KeyValuePair<string, Entity>[] { new("DataSections[0].#1", entity), };
            await Assert.That(descriptor.Validate(structure, entities).Failures.Single().Code)
                .IsEqualTo("TEST-VALIDATE");
            await Assert.That(descriptor.GetCapabilityDiagnostics(structure).Single().Code)
                .IsEqualTo("TEST-CAPABILITY");
            await Assert.That(descriptor.ProjectEntity(entity)).IsSameReferenceAs(projected);
        }
    }

    private static string Format(MethodInfo method)
    {
        var parameters = string.Join(", ", method.GetParameters().Select(parameter => Format(parameter.ParameterType)));
        return $"{method.Name}({parameters}) -> {Format(method.ReturnType)}";
    }

    private static string Format(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var name = type.Name[..type.Name.IndexOf('`')];
        return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(Format))}>";
    }

    private sealed class DispatchProbeDescriptor(
        Entity allocated,
        IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> projected) : SchemaDescriptor
    {
        public override SchemaName Name { get; } = new("TEST_SCHEMA");

        protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => allocated;

        protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
            ExchangeStructure structure,
            Entity value,
            IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) =>
            [new("TEST-HYDRATE", Step21DiagnosticSeverity.Information, "Hydration dispatch reached.")];

        protected override ValidationResult ValidateCore(
            ExchangeStructure structure,
            IReadOnlyList<KeyValuePair<string, Entity>> entities) =>
            new([new("TEST-VALIDATE", "$", "Validation dispatch reached.")]);

        protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
            ExchangeStructure structure) =>
            [new("TEST-CAPABILITY", Step21DiagnosticSeverity.Information, "Capability dispatch reached.")];

        protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
            Entity value) => projected;
    }
}
