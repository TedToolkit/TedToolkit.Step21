using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExchangeStructureTests;
using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>
/// Proves generated one-level direct physical entity occurrence enumeration.
/// </summary>
public sealed class GeneratedDirectReferencesTests
{
    private const string REFERENCE_SCHEMA = """
        SCHEMA direct_reference_model;
        TYPE node_list = LIST [0:?] OF node;
        END_TYPE;
        TYPE nested_node_lists = LIST [0:?] OF node_list;
        END_TYPE;
        TYPE label = STRING;
        END_TYPE;
        TYPE node_choice = SELECT (node, node_list, label);
        END_TYPE;
        TYPE node_choices = LIST [0:?] OF node_choice;
        END_TYPE;
        ENTITY node;
          direct_node : OPTIONAL node;
          nested_nodes : nested_node_lists;
          choice_value : OPTIONAL node_choice;
          choices : node_choices;
          array_nodes : ARRAY [1:2] OF OPTIONAL node;
          bag_nodes : BAG [0:?] OF node;
          set_choices : SET [0:?] OF node_choice;
        DERIVE
          derived_node : node := direct_node;
        INVERSE
          inverse_nodes : SET [0:?] OF node FOR direct_node;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string CONSUMER = """
        #nullable enable
        using System.Collections.Generic;
        using System.Linq;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.DirectReferenceModel;

        internal sealed class DirectReferenceFixture
        {
            private readonly ExpressList<INode> _inner;
            private readonly ExpressList<NodeChoice> _choices;
            private readonly IEnumerable<Entity> _view;

            public DirectReferenceFixture()
            {
                Root = CreateNode();
                Shared = CreateNode();
                Second = CreateNode();
                Hidden = CreateNode();
                Later = CreateNode();

                Shared.DirectNode = Hidden;
                Root.DirectNode = Shared;
                _inner = new ExpressList<INode> { Shared, null!, Shared };
                Root.NestedNodes.Value.Add(new NodeList(_inner));
                Root.ChoiceValue = NodeChoice.FromNode(Second);
                _choices = Root.Choices.Value;
                _choices.Add(NodeChoice.FromNode(Second));
                _choices.Add(NodeChoice.FromNodeList(new NodeList(_inner)));
                Root.ArrayNodes[1] = Shared;
                Root.BagNodes.Add(Second);
                Root.BagNodes.Add(Second);
                Root.SetChoices.Add(NodeChoice.FromNodeList(new NodeList(_inner)));
                _view = Root.DirectReferences;
            }

            public Node Root { get; }

            public Node Shared { get; }

            public Node Second { get; }

            public Node Hidden { get; }

            public Node Later { get; }

            public Entity[] ReadReferences() => _view.ToArray();

            public Entity[] MutateAndReadReferences()
            {
                Root.DirectNode = null;
                _inner.Add(Later);
                Root.ChoiceValue = null;
                _choices.Add(NodeChoice.FromNode(Later));
                Later.DirectNode = Root;
                return _view.ToArray();
            }

            private static Node CreateNode()
            {
                return new Node(
                    new NestedNodeLists(new ExpressList<NodeList>()),
                    new NodeChoices(new ExpressList<NodeChoice>()),
                    new ExpressArray<INode>(1, 2, isOptional: true),
                    new ExpressBag<INode>(),
                    new ExpressSet<NodeChoice>());
            }
        }
        """;

    /// <summary>
    /// Verifies the same enumerable observes edits and expands aggregate/select containers in physical order.
    /// </summary>
    [Test]
    public async Task Should_enumerate_live_nested_physical_occurrences_once_without_deduplication()
    {
        var result = GeneratorHostTests.Run(CONSUMER, ("schemas/direct-references.exp", REFERENCE_SCHEMA));
        var node = RequiredType(result.OutputCompilation, "TedToolkit.Step21.Generated.DirectReferenceModel.Node");
        var nodeSource = result.GeneratedSources.Single(source =>
            source.HintName == "ExpressEntity_DIRECT_REFERENCE_MODEL_NODE.g.cs").SourceText.ToString();

        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                .IsEmpty();
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(node.GetMembers().OfType<IPropertySymbol>().Select(property => property.Name))
                .IsEquivalentTo([
                    "DirectNode",
                    "NestedNodes",
                    "ChoiceValue",
                    "Choices",
                    "ArrayNodes",
                    "BagNodes",
                    "SetChoices",
                    "DirectReferences",
                ]);
            await Assert.That(node.Constructors.Single().Parameters.Select(parameter => parameter.Name))
                .IsEquivalentTo(["nestedNodes", "choices", "arrayNodes", "bagNodes", "setChoices"]);
            await Assert.That(nodeSource)
                .Contains("live one-level enumeration of non-null direct entity occurrences in physical attribute order");
            await Assert.That(nodeSource).DoesNotContain("System.Reflection");
            await Assert.That(nodeSource).DoesNotContain("Distinct");
        }

        var assembly = Emit(result.OutputCompilation);
        var fixture = Activator.CreateInstance(assembly.GetType("DirectReferenceFixture", throwOnError: true)!)!;
        var fixtureType = fixture.GetType();
        var shared = (Entity)fixtureType.GetProperty("Shared")!.GetValue(fixture)!;
        var second = (Entity)fixtureType.GetProperty("Second")!.GetValue(fixture)!;
        var hidden = (Entity)fixtureType.GetProperty("Hidden")!.GetValue(fixture)!;
        var later = (Entity)fixtureType.GetProperty("Later")!.GetValue(fixture)!;
        var initial = (Entity[])fixtureType.GetMethod("ReadReferences")!.Invoke(fixture, null)!;
        var changed = (Entity[])fixtureType.GetMethod("MutateAndReadReferences")!.Invoke(fixture, null)!;

        using (Assert.Multiple())
        {
            await Assert.That(initial.SequenceEqual(
                [
                    shared,
                    shared,
                    shared,
                    second,
                    second,
                    shared,
                    shared,
                    shared,
                    second,
                    second,
                    shared,
                    shared,
                ])).IsTrue();
            await Assert.That(changed.SequenceEqual(
                [
                    shared,
                    shared,
                    later,
                    second,
                    shared,
                    shared,
                    later,
                    later,
                    shared,
                    second,
                    second,
                    shared,
                    shared,
                    later,
                ])).IsTrue();
            await Assert.That(initial).DoesNotContain(hidden);
            await Assert.That(changed).DoesNotContain(hidden);
        }
    }

    /// <summary>
    /// Verifies graph registration follows generated occurrences transitively with share/cycle/re-add safety.
    /// </summary>
    [Test]
    public async Task Should_compose_with_structure_add_for_shared_cycles_and_newly_reachable_entities()
    {
        var result = GeneratorHostTests.Run(CONSUMER, ("schemas/direct-references.exp", REFERENCE_SCHEMA));
        var assembly = Emit(result.OutputCompilation);
        var fixture = Activator.CreateInstance(assembly.GetType("DirectReferenceFixture", throwOnError: true)!)!;
        var fixtureType = fixture.GetType();
        var root = (Entity)fixtureType.GetProperty("Root")!.GetValue(fixture)!;
        var shared = (Entity)fixtureType.GetProperty("Shared")!.GetValue(fixture)!;
        var second = (Entity)fixtureType.GetProperty("Second")!.GetValue(fixture)!;
        var hidden = (Entity)fixtureType.GetProperty("Hidden")!.GetValue(fixture)!;
        var later = (Entity)fixtureType.GetProperty("Later")!.GetValue(fixture)!;
        var descriptorType = assembly.GetType(
            "TedToolkit.Step21.Generated.DirectReferenceModel.SchemaDescriptor",
            throwOnError: true)!;
        var descriptor = (SchemaDescriptor)descriptorType.GetProperty("Instance")!.GetValue(null)!;
        var structure = new ExchangeStructure(TestHeader.Create(), [descriptor]);
        var section = new DataSection(new SchemaName("direct_reference_model"));
        structure.DataSections.Add(section);

        var rootName = structure.Add(section, root);
        var sharedName = RequiredName(structure, shared);
        var secondName = RequiredName(structure, second);
        var hiddenName = RequiredName(structure, hidden);
        _ = fixtureType.GetMethod("MutateAndReadReferences")!.Invoke(fixture, null);
        var repeatedRootName = structure.Add(section, root);

        using (Assert.Multiple())
        {
            await Assert.That(structure.Registrations.Count).IsEqualTo(5);
            await Assert.That(repeatedRootName).IsEqualTo(rootName);
            await Assert.That(RequiredName(structure, shared)).IsEqualTo(sharedName);
            await Assert.That(RequiredName(structure, second)).IsEqualTo(secondName);
            await Assert.That(RequiredName(structure, hidden)).IsEqualTo(hiddenName);
            await Assert.That(RequiredName(structure, later).CanonicalDigits).IsEqualTo("5");
            await Assert.That(structure.Registrations.Select(registration => registration.Entity).Distinct().Count())
                .IsEqualTo(5);
        }
    }

    private static EntityInstanceName RequiredName(ExchangeStructure structure, Entity entity)
    {
        return structure.TryGetName(entity, out var name)
            ? name
            : throw new InvalidOperationException("The expected generated entity was not registered.");
    }

    private static INamedTypeSymbol RequiredType(Compilation compilation, string metadataName)
    {
        return compilation.GetTypeByMetadataName(metadataName)
            ?? throw new InvalidOperationException($"Generated type '{metadataName}' was not found.");
    }

    private static System.Reflection.Assembly Emit(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));
        }

        return System.Reflection.Assembly.Load(stream.ToArray());
    }
}