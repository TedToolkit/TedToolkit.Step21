// -----------------------------------------------------------------------
// <copyright file="EntityHierarchyTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using TedToolkit.Step21;
using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>
/// Proves generated entity hierarchy shape and inherited storage.
/// </summary>
public sealed class EntityHierarchyTests
{
    private const string INHERITANCE_SCHEMA = """
        SCHEMA inheritance_model;
        ENTITY target;
        END_ENTITY;
        ENTITY root ABSTRACT SUPERTYPE;
          mandatory_target : target;
          optional_target : OPTIONAL target;
        END_ENTITY;
        ENTITY left ABSTRACT SUPERTYPE SUBTYPE OF (root);
        END_ENTITY;
        ENTITY right ABSTRACT SUPERTYPE SUBTYPE OF (root);
        END_ENTITY;
        ENTITY leaf SUBTYPE OF (left, right);
          required_peer : root;
          optional_peer : OPTIONAL root;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string COLLIDING_SCHEMA = """
        SCHEMA collision_model;
        ENTITY foo_bar;
        END_ENTITY;
        ENTITY foo__bar;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string BASE_SCHEMA = """
        SCHEMA base_model;
        ENTITY target;
        END_ENTITY;
        ENTITY root;
          target_ref : target;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string CHILD_SCHEMA = """
        SCHEMA child_model;
        USE FROM base_model (root);
        ENTITY child SUBTYPE OF (root);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string REDECLARATION_SCHEMA = """
        SCHEMA redeclaration_model;
        ENTITY target;
        END_ENTITY;
        ENTITY root ABSTRACT;
          link : target;
        END_ENTITY;
        ENTITY child SUBTYPE OF (root);
          SELF\root.link RENAMED inherited_link : target;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string NARROWED_REDECLARATION_SCHEMA = """
        SCHEMA narrowed_redeclaration;
        ENTITY target;
        END_ENTITY;
        ENTITY specialized SUBTYPE OF (target);
        END_ENTITY;
        ENTITY root ABSTRACT;
          link : target;
        END_ENTITY;
        ENTITY child SUBTYPE OF (root);
          SELF\root.link : specialized;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string INVALID_IMPORTED_BASE_SCHEMA = """
        SCHEMA invalid_imported_base;
        ENTITY root;
        END_ENTITY;
        ENTITY foo_bar;
        END_ENTITY;
        ENTITY foo__bar;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string DEPENDENT_SCHEMA = """
        SCHEMA dependent_model;
        USE FROM invalid_imported_base (root);
        ENTITY child SUBTYPE OF (root);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string MEMBER_COLLISION_SCHEMA = """
        SCHEMA member_collision;
        ENTITY target;
        END_ENTITY;
        ENTITY holder;
          holder : target;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string KEYWORD_PARAMETER_SCHEMA = """
        SCHEMA keyword_parameter;
        ENTITY target;
        END_ENTITY;
        ENTITY holder;
          event : target;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string INHERITED_STORAGE_COLLISION_SCHEMA = """
        SCHEMA inherited_storage_collision;
        ENTITY first_base;
          name : STRING;
        END_ENTITY;
        ENTITY second_base;
          name : STRING;
        END_ENTITY;
        ENTITY leaf SUBTYPE OF (first_base, second_base);
          code : INTEGER;
        END_ENTITY;
        ENTITY derived_leaf SUBTYPE OF (leaf);
          enabled : BOOLEAN;
        END_ENTITY;
        END_SCHEMA;
        """;

    /// <summary>
    /// Verifies interface inheritance, class shape, flattened storage, and constructors.
    /// </summary>
    [Test]
    public async Task Should_generate_entity_hierarchy_and_flatten_inherited_attributes_once()
    {
        var result = GeneratorHostTests.Run(("schemas/inheritance.exp", INHERITANCE_SCHEMA));
        var rootInterface = RequiredType(result.OutputCompilation, "TedToolkit.Step21.Generated.InheritanceModel.IRoot");
        var leafInterface = RequiredType(result.OutputCompilation, "TedToolkit.Step21.Generated.InheritanceModel.ILeaf");
        var rootClass = RequiredType(result.OutputCompilation, "TedToolkit.Step21.Generated.InheritanceModel.Root");
        var leafClass = RequiredType(result.OutputCompilation, "TedToolkit.Step21.Generated.InheritanceModel.Leaf");
        var leafProperties = leafClass.GetMembers().OfType<IPropertySymbol>().ToArray();
        var leafConstructor = leafClass.Constructors.Single(constructor => constructor.DeclaredAccessibility == Accessibility.Public);

        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)).IsEmpty();
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(item => item.Severity == DiagnosticSeverity.Error)).IsEmpty();
            await Assert.That(rootInterface.TypeKind).IsEqualTo(TypeKind.Interface);
            await Assert.That(leafInterface.Interfaces.Select(type => type.Name)).IsEquivalentTo(["ILeft", "IRight"]);
            await Assert.That(rootClass.IsAbstract).IsTrue();
            await Assert.That(leafClass.IsSealed).IsTrue();
            await Assert.That(leafClass.BaseType?.ToDisplayString()).IsEqualTo("TedToolkit.Step21.Entity");
            await Assert.That(leafClass.Interfaces.Select(type => type.Name)).IsEquivalentTo(["ILeaf"]);
            await Assert.That(leafProperties.Select(property => property.Name))
                .IsEquivalentTo(["MandatoryTarget", "OptionalTarget", "RequiredPeer", "OptionalPeer", "DirectReferences"]);
            await Assert.That(leafProperties.Where(property => property.Name != "DirectReferences")
                .All(property => property.GetMethod is not null && property.SetMethod is not null)).IsTrue();
            await Assert.That(RequiredProperty(leafClass, "MandatoryTarget").Type
                .ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)).IsEqualTo("ITarget");
            await Assert.That(RequiredProperty(leafClass, "OptionalTarget").Type
                .ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)).IsEqualTo("ITarget?");
            await Assert.That(leafConstructor.Parameters.Select(parameter => parameter.Name))
                .IsEquivalentTo(["mandatoryTarget", "requiredPeer"]);
        }
    }

    /// <summary>
    /// Verifies reference identity, unchecked mutation, direct references, and bounded formatting.
    /// </summary>
    [Test]
    public async Task Should_preserve_reference_identity_and_report_live_direct_references_without_recursion()
    {
        var result = GeneratorHostTests.Run(("schemas/inheritance.exp", INHERITANCE_SCHEMA));
        var assembly = Emit(result.OutputCompilation);
        var targetType = assembly.GetType("TedToolkit.Step21.Generated.InheritanceModel.Target", throwOnError: true)!;
        var leafType = assembly.GetType("TedToolkit.Step21.Generated.InheritanceModel.Leaf", throwOnError: true)!;
        var firstTarget = Activator.CreateInstance(targetType)!;
        var secondTarget = Activator.CreateInstance(targetType)!;
        var leaf = leafType.GetConstructors().Single().Invoke([firstTarget, null]);

        leafType.GetProperty("OptionalTarget")!.SetValue(leaf, firstTarget);
        leafType.GetProperty("RequiredPeer")!.SetValue(leaf, leaf);
        leafType.GetProperty("OptionalPeer")!.SetValue(leaf, leaf);
        var references = ((Entity)leaf).DirectReferences.ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(references.Length).IsEqualTo(4);
            await Assert.That(references[0]).IsSameReferenceAs(firstTarget);
            await Assert.That(references[1]).IsSameReferenceAs(firstTarget);
            await Assert.That(references[2]).IsSameReferenceAs(leaf);
            await Assert.That(references[3]).IsSameReferenceAs(leaf);
            await Assert.That(firstTarget.Equals(secondTarget)).IsFalse();
            await Assert.That(leaf.ToString()).IsEqualTo("inheritance_model.leaf");
        }

        leafType.GetProperty("MandatoryTarget")!.SetValue(leaf, null);
        await Assert.That(leafType.GetProperty("MandatoryTarget")!.GetValue(leaf)).IsNull();
    }

    /// <summary>
    /// Verifies transformed C# name collisions fail the related schema atomically.
    /// </summary>
    [Test]
    public async Task Should_report_transformed_name_collision_without_emitting_related_schema()
    {
        var result = GeneratorHostTests.Run(("schemas/collision.exp", COLLIDING_SCHEMA));

        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Id)).Contains("STEP21EXP004");
            await Assert.That(result.GeneratedSources).IsEmpty();
        }
    }

    /// <summary>
    /// Verifies imported entity interfaces and flattened attributes use their declaring schema namespace.
    /// </summary>
    [Test]
    public async Task Should_qualify_cross_schema_inheritance_and_attribute_types()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/base.exp", BASE_SCHEMA),
            ("schemas/child.exp", CHILD_SCHEMA));
        var childInterface = RequiredType(
            result.OutputCompilation,
            "TedToolkit.Step21.Generated.ChildModel.IChild");
        var childClass = RequiredType(
            result.OutputCompilation,
            "TedToolkit.Step21.Generated.ChildModel.Child");

        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                .IsEmpty();
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
            await Assert.That(childInterface.Interfaces.Single().ToDisplayString())
                .IsEqualTo("TedToolkit.Step21.Generated.BaseModel.IRoot");
            await Assert.That(RequiredProperty(childClass, "TargetRef").Type.ToDisplayString())
                .IsEqualTo("TedToolkit.Step21.Generated.BaseModel.ITarget");
        }
    }

    /// <summary>
    /// Verifies mandatory construction and nullable optional members are visible to the C# compiler.
    /// </summary>
    [Test]
    public async Task Should_expose_mandatory_and_optional_nullability_to_consumers()
    {
        const string validConsumer = """
            #nullable enable
            using TedToolkit.Step21.Generated.InheritanceModel;
            internal static class Consumer
            {
                internal static void Edit(ITarget target, IRoot root)
                {
                    var leaf = new Leaf(target, root);
                    leaf.MandatoryTarget = target;
                    leaf.OptionalTarget = null;
                    leaf.OptionalPeer = null;
                }
            }
            """;
        const string invalidConsumer = """
            #nullable enable
            using TedToolkit.Step21.Generated.InheritanceModel;
            internal static class Consumer
            {
                internal static void Edit(ITarget target, IRoot root)
                {
                    _ = new Leaf(target);
                    var leaf = new Leaf(null, root);
                    leaf.MandatoryTarget = null;
                }
            }
            """;
        var valid = GeneratorHostTests.Run(validConsumer, ("schemas/inheritance.exp", INHERITANCE_SCHEMA));
        var invalid = GeneratorHostTests.Run(invalidConsumer, ("schemas/inheritance.exp", INHERITANCE_SCHEMA));
        var invalidIds = invalid.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.Id)
            .ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(valid.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error))
                .IsEmpty();
            await Assert.That(invalidIds).Contains("CS7036");
            await Assert.That(invalidIds.Count(id => id == "CS8625")).IsEqualTo(2);
        }
    }

    /// <summary>
    /// Verifies a renamed redeclaration exposes both interface names over one physical storage slot.
    /// </summary>
    [Test]
    public async Task Should_reuse_inherited_storage_for_renamed_redeclaration()
    {
        var result = GeneratorHostTests.Run(("schemas/redeclaration.exp", REDECLARATION_SCHEMA));
        var childClass = RequiredType(
            result.OutputCompilation,
            "TedToolkit.Step21.Generated.RedeclarationModel.Child");

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
            await Assert.That(childClass.GetMembers().OfType<IPropertySymbol>().Select(property => property.Name))
                .IsEquivalentTo(["Link", "InheritedLink", "DirectReferences"]);
            await Assert.That(childClass.Constructors.Single().Parameters.Select(parameter => parameter.Name))
                .IsEquivalentTo(["inheritedLink"]);
        }

        var assembly = Emit(result.OutputCompilation);
        var target = Activator.CreateInstance(assembly.GetType(
            "TedToolkit.Step21.Generated.RedeclarationModel.Target",
            throwOnError: true)!)!;
        var childType = assembly.GetType(
            "TedToolkit.Step21.Generated.RedeclarationModel.Child",
            throwOnError: true)!;
        var child = childType.GetConstructors().Single().Invoke([target]);

        using (Assert.Multiple())
        {
            await Assert.That(childType.GetProperty("Link")!.GetValue(child)).IsSameReferenceAs(target);
            await Assert.That(childType.GetProperty("InheritedLink")!.GetValue(child)).IsSameReferenceAs(target);
            await Assert.That(((Entity)child).DirectReferences.Single()).IsSameReferenceAs(target);
        }
    }

    /// <summary>
    /// Verifies an unsafe CLR property-type redeclaration reports a bounded generation diagnostic.
    /// </summary>
    [Test]
    public async Task Should_withhold_entity_type_narrowing_that_cannot_implement_both_interfaces()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/narrowed.exp", NARROWED_REDECLARATION_SCHEMA));

        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Id)).Contains("STEP21EXP005");
            await Assert.That(result.GeneratedSources).IsEmpty();
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        }
    }

    /// <summary>
    /// Verifies generation-invalid schemas withhold every importing schema transitively.
    /// </summary>
    [Test]
    public async Task Should_withhold_schemas_that_import_generation_invalid_schema()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/base-invalid.exp", INVALID_IMPORTED_BASE_SCHEMA),
            ("schemas/dependent.exp", DEPENDENT_SCHEMA));

        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Id)).Contains("STEP21EXP004");
            await Assert.That(result.GeneratedSources).IsEmpty();
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        }
    }

    /// <summary>
    /// Verifies a property cannot silently collide with its containing generated class name.
    /// </summary>
    [Test]
    public async Task Should_report_property_name_that_matches_containing_entity_class()
    {
        var result = GeneratorHostTests.Run(("schemas/member-collision.exp", MEMBER_COLLISION_SCHEMA));

        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Id)).Contains("STEP21EXP004");
            await Assert.That(result.GeneratedSources).IsEmpty();
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        }
    }

    /// <summary>
    /// Verifies C# keyword constructor parameters are escaped without changing metadata names.
    /// </summary>
    [Test]
    public async Task Should_escape_keyword_constructor_parameter()
    {
        var result = GeneratorHostTests.Run(("schemas/keyword.exp", KEYWORD_PARAMETER_SCHEMA));
        var holderClass = RequiredType(
            result.OutputCompilation,
            "TedToolkit.Step21.Generated.KeywordParameter.Holder");

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
            await Assert.That(holderClass.Constructors.Single().Parameters.Single().Name).IsEqualTo("event");
        }
    }

    /// <summary>
    /// Verifies inherited same-name attributes retain distinct physical storage and interface contracts.
    /// </summary>
    [Test]
    public async Task Should_disambiguate_inherited_same_name_storage_without_changing_slot_order()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/inherited-storage-collision.exp", INHERITED_STORAGE_COLLISION_SCHEMA));
        var diagnostics = result.Diagnostics
            .Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)
            .ToArray();

        await Assert.That(diagnostics).IsEmpty();

        var assembly = Emit(result.OutputCompilation);
        var leafType = assembly.GetType(
            "TedToolkit.Step21.Generated.InheritedStorageCollision.Leaf",
            throwOnError: true)!;
        var derivedType = assembly.GetType(
            "TedToolkit.Step21.Generated.InheritedStorageCollision.DerivedLeaf",
            throwOnError: true)!;
        var firstInterface = assembly.GetType(
            "TedToolkit.Step21.Generated.InheritedStorageCollision.IFirstBase",
            throwOnError: true)!;
        var secondInterface = assembly.GetType(
            "TedToolkit.Step21.Generated.InheritedStorageCollision.ISecondBase",
            throwOnError: true)!;
        var descriptor = (SchemaDescriptor)assembly.GetType(
            "TedToolkit.Step21.Generated.InheritedStorageCollision.SchemaDescriptor",
            throwOnError: true)!.GetProperty("Instance")!.GetValue(null)!;
        var source = """
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('collision'),'3;1');
            FILE_NAME('collision.p21','2026-08-24T00:00:00',('Author'),('Org'),'Pre','System','Auth');
            FILE_SCHEMA(('inherited_storage_collision'));
            ENDSEC;
            DATA;
            #1=LEAF('first','second',7);
            #2=DERIVED_LEAF('derived-first','derived-second',8,.T.);
            ENDSEC;
            END-ISO-10303-21;
            """;
        var first = ExchangeStructure.Read(new StringReader(source), [descriptor]);
        var leaf = first.Registrations.Single(item => item.Name.Equals(new EntityInstanceName("1"))).Entity;
        var derived = first.Registrations.Single(item => item.Name.Equals(new EntityInstanceName("2"))).Entity;
        var output = new StringWriter();
        first.Write(output);
        var reread = ExchangeStructure.Read(new StringReader(output.ToString()), [descriptor]);
        var rereadLeaf = reread.Registrations.Single(item => item.Name.Equals(new EntityInstanceName("1"))).Entity;
        var rereadDerived = reread.Registrations.Single(item => item.Name.Equals(new EntityInstanceName("2"))).Entity;

        using (Assert.Multiple())
        {
            await Assert.That(leafType.GetProperties().Select(property => property.Name))
                .IsEquivalentTo(["FirstBaseName", "SecondBaseName", "Code", "DirectReferences"]);
            await Assert.That(derivedType.GetProperties().Select(property => property.Name))
                .IsEquivalentTo(["FirstBaseName", "SecondBaseName", "Code", "Enabled", "DirectReferences"]);
            await Assert.That(leafType.GetConstructors().Single().GetParameters()
                .Select(parameter => parameter.Name ?? string.Empty))
                .IsEquivalentTo(["firstBaseName", "secondBaseName", "code"]);
            await Assert.That(firstInterface.GetProperty("Name")!.GetValue(leaf)).IsEqualTo("first");
            await Assert.That(secondInterface.GetProperty("Name")!.GetValue(leaf)).IsEqualTo("second");
            await Assert.That(firstInterface.GetProperty("Name")!.GetValue(derived)).IsEqualTo("derived-first");
            await Assert.That(secondInterface.GetProperty("Name")!.GetValue(derived)).IsEqualTo("derived-second");
            await Assert.That(firstInterface.GetProperty("Name")!.GetValue(rereadLeaf)).IsEqualTo("first");
            await Assert.That(secondInterface.GetProperty("Name")!.GetValue(rereadLeaf)).IsEqualTo("second");
            await Assert.That(firstInterface.GetProperty("Name")!.GetValue(rereadDerived)).IsEqualTo("derived-first");
            await Assert.That(secondInterface.GetProperty("Name")!.GetValue(rereadDerived)).IsEqualTo("derived-second");
            await Assert.That(output.ToString()).Contains("#1=LEAF('first','second',7);");
            await Assert.That(output.ToString()).Contains("#2=DERIVED_LEAF('derived-first','derived-second',8,.T.);");
        }
    }

    private static INamedTypeSymbol RequiredType(Compilation compilation, string metadataName)
    {
        return compilation.GetTypeByMetadataName(metadataName)
            ?? throw new InvalidOperationException($"Generated type '{metadataName}' was not found.");
    }

    private static IPropertySymbol RequiredProperty(INamedTypeSymbol type, string name)
    {
        return type.GetMembers(name).OfType<IPropertySymbol>().Single();
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