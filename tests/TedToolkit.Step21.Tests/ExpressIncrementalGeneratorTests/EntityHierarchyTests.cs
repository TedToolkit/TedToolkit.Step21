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

    private const string RENAMED_NARROWED_REDECLARATION_SCHEMA = """
        SCHEMA renamed_narrowed_redeclaration;
        ENTITY target;
        END_ENTITY;
        ENTITY specialized SUBTYPE OF (target);
        END_ENTITY;
        ENTITY root ABSTRACT;
          link : target;
        END_ENTITY;
        ENTITY child SUBTYPE OF (root);
          SELF\root.link RENAMED specialized_link : specialized;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string UNRELATED_REDECLARATION_SCHEMA = """
        SCHEMA unrelated_redeclaration;
        ENTITY target;
        END_ENTITY;
        ENTITY unrelated;
        END_ENTITY;
        ENTITY root ABSTRACT;
          link : target;
        END_ENTITY;
        ENTITY child SUBTYPE OF (root);
          SELF\root.link : unrelated;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string NUMERIC_REDECLARATION_SCHEMA = """
        SCHEMA numeric_redeclaration;
        ENTITY root ABSTRACT;
          integer_value : NUMBER;
          real_value : NUMBER;
        END_ENTITY;
        ENTITY child SUBTYPE OF (root);
          SELF\root.integer_value : INTEGER;
          SELF\root.real_value : REAL;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string AGGREGATE_REDECLARATION_SCHEMA = """
        SCHEMA aggregate_redeclaration;
        ENTITY target;
        END_ENTITY;
        ENTITY specialized SUBTYPE OF (target);
        END_ENTITY;
        ENTITY root ABSTRACT;
          array_value : ARRAY [1:2] OF target;
          list_value : LIST [0:?] OF UNIQUE target;
          bag_value : BAG [0:?] OF target;
          set_value : SET [0:?] OF target;
        END_ENTITY;
        ENTITY child SUBTYPE OF (root);
          SELF\root.array_value : ARRAY [1:2] OF specialized;
          SELF\root.list_value : LIST [0:?] OF UNIQUE specialized;
          SELF\root.bag_value : BAG [0:?] OF specialized;
          SELF\root.set_value : SET [0:?] OF specialized;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string AGGREGATE_REDECLARATION_CONSUMER = """
        #nullable enable
        using System;
        using System.Linq;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.AggregateRedeclaration;
        internal static class AggregateRedeclarationConsumer
        {
            internal static bool Exercise()
            {
                var first = new Specialized();
                var second = new Specialized();
                var array = new ExpressArray<ISpecialized>(1, 2);
                array[1] = first;
                array[2] = second;
                var list = new ExpressList<ISpecialized>(0, null, isUnique: true) { first };
                var bag = new ExpressBag<ISpecialized> { first, first };
                var set = new ExpressSet<ISpecialized> { first };
                var child = new Child(array, list, bag, set);
                IRoot root = child;
                IChild narrowed = child;

                list.Add(second);
                bag.Add(second);
                set.Add(second);
                return ReferenceEquals(child.ArrayValue, root.ArrayValue)
                    && ReferenceEquals(child.ListValue, root.ListValue)
                    && ReferenceEquals(child.BagValue, root.BagValue)
                    && ReferenceEquals(child.SetValue, root.SetValue)
                    && ReferenceEquals(narrowed.ArrayValue, root.ArrayValue)
                    && root.ArrayValue[1] == first
                    && root.ListValue.SequenceEqual(new ITarget[] { first, second })
                    && root.BagValue.Count == 3
                    && root.SetValue.Count == 2;
            }
        }
        """;

    private const string AGGREGATE_SELECT_REDECLARATION_SCHEMA = """
        SCHEMA aggregate_select_redeclaration;
        ENTITY target;
        END_ENTITY;
        ENTITY first SUBTYPE OF (target);
        END_ENTITY;
        ENTITY second SUBTYPE OF (target);
        END_ENTITY;
        ENTITY other SUBTYPE OF (target);
        END_ENTITY;
        TYPE broad_choice = SELECT (first, second);
        END_TYPE;
        TYPE equal_choice = SELECT (first, second);
        END_TYPE;
        TYPE narrow_choice = SELECT (first, second);
        END_TYPE;
        ENTITY root ABSTRACT;
          array_value : ARRAY [1:2] OF OPTIONAL UNIQUE target;
          list_value : LIST [1:?] OF UNIQUE broad_choice;
          bag_value : BAG [0:?] OF target;
          set_value : SET [1:?] OF broad_choice;
        END_ENTITY;
        ENTITY child SUBTYPE OF (root);
          SELF\root.array_value : ARRAY [1:2] OF OPTIONAL UNIQUE narrow_choice;
          SELF\root.list_value : LIST [1:?] OF UNIQUE equal_choice;
          SELF\root.bag_value : BAG [0:?] OF narrow_choice;
          SELF\root.set_value : SET [1:?] OF equal_choice;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string AGGREGATE_SELECT_REDECLARATION_CONSUMER = """
        #nullable enable
        using System.Linq;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.AggregateSelectRedeclaration;
        internal static class AggregateSelectRedeclarationConsumer
        {
            internal static bool Exercise()
            {
                var first = new First();
                var second = new Second();
                var narrowFirst = NarrowChoice.FromFirst(first);
                var narrowSecond = NarrowChoice.FromSecond(second);
                var equalFirst = EqualChoice.FromFirst(first);
                var equalSecond = EqualChoice.FromSecond(second);
                var array = new ExpressArray<NarrowChoice>(1, 2, isOptional: true, isUnique: true);
                array[1] = narrowFirst;
                var list = new ExpressList<EqualChoice>(1, null, isUnique: true) { equalFirst };
                var bag = new ExpressBag<NarrowChoice> { narrowFirst, narrowFirst };
                var set = new ExpressSet<EqualChoice>(1, null) { equalFirst };
                var child = new Child(array, list, bag, set);
                IRoot root = child;

                var arrayView = root.ArrayValue;
                var listView = root.ListValue;
                var bagView = root.BagValue;
                var setView = root.SetValue;
                list.Add(equalSecond);
                bag.Add(narrowSecond);
                set.Add(equalSecond);

                return arrayView.LowerIndex == 1
                    && arrayView.UpperIndex == 2
                    && arrayView.Count == 2
                    && arrayView.IsOptional
                    && arrayView.IsUnique
                    && arrayView.IsSet(1)
                    && !arrayView.IsSet(2)
                    && ReferenceEquals(arrayView[1], first)
                    && listView.LowerBound == 1
                    && listView.UpperBound is null
                    && listView.IsUnique
                    && listView.Count == 2
                    && ReferenceEquals(Unwrap(listView[0]), first)
                    && ReferenceEquals(Unwrap(listView[1]), second)
                    && bagView.Count == 3
                    && bagView.Count(item => ReferenceEquals(item, first)) == 2
                    && bagView.Count(item => ReferenceEquals(item, second)) == 1
                    && setView.Count == 2
                    && ReferenceEquals(Unwrap(setView.First()), first)
                    && ReferenceEquals(Unwrap(setView.Last()), second)
                    && child.DirectReferences.Count() == 8;
            }

            private static ITarget Unwrap(BroadChoice value) => value.Match<ITarget>(
                first => first,
                second => second);
        }
        """;

    private const string INVALID_AGGREGATE_SELECT_REDECLARATION_SCHEMA = """
        SCHEMA invalid_aggregate_select_redeclaration;
        ENTITY target;
        END_ENTITY;
        ENTITY accepted SUBTYPE OF (target);
        END_ENTITY;
        ENTITY unrelated;
        END_ENTITY;
        TYPE broad_choice = SELECT (accepted);
        END_TYPE;
        TYPE widened_choice = SELECT (accepted, unrelated);
        END_TYPE;
        ENTITY root ABSTRACT;
          items : SET [0:?] OF broad_choice;
        END_ENTITY;
        ENTITY child SUBTYPE OF (root);
          SELF\root.items : SET [0:?] OF widened_choice;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string UNSUPPORTED_AGGREGATE_SELECT_REDECLARATION_SCHEMA = """
        SCHEMA unsupported_aggregate_select_redeclaration;
        ENTITY target;
        END_ENTITY;
        ENTITY accepted SUBTYPE OF (target);
        END_ENTITY;
        TYPE label = STRING;
        END_TYPE;
        TYPE mixed_choice = SELECT (accepted, label);
        END_TYPE;
        TYPE extensible_choice = EXTENSIBLE SELECT (accepted);
        END_TYPE;
        ENTITY root ABSTRACT;
          mixed_items : SET [0:?] OF target;
          extensible_items : SET [0:?] OF target;
        END_ENTITY;
        ENTITY child SUBTYPE OF (root);
          SELF\root.mixed_items : SET [0:?] OF mixed_choice;
          SELF\root.extensible_items : SET [0:?] OF extensible_choice;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string OPTIONAL_AGGREGATE_SELECT_REDECLARATION_SCHEMA = """
        SCHEMA optional_aggregate_select_redeclaration;
        ENTITY target;
        END_ENTITY;
        ENTITY specialized SUBTYPE OF (target);
        END_ENTITY;
        TYPE narrow_choice = SELECT (specialized);
        END_TYPE;
        ENTITY root ABSTRACT;
          array_value : OPTIONAL ARRAY [1:1] OF target;
          list_value : OPTIONAL LIST [0:?] OF target;
          bag_value : OPTIONAL BAG [0:?] OF target;
          set_value : OPTIONAL SET [0:?] OF target;
        END_ENTITY;
        ENTITY child SUBTYPE OF (root);
          SELF\root.array_value : OPTIONAL ARRAY [1:1] OF narrow_choice;
          SELF\root.list_value : OPTIONAL LIST [0:?] OF narrow_choice;
          SELF\root.bag_value : OPTIONAL BAG [0:?] OF narrow_choice;
          SELF\root.set_value : OPTIONAL SET [0:?] OF narrow_choice;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string OPTIONAL_AGGREGATE_SELECT_REDECLARATION_CONSUMER = """
        #nullable enable
        using System.Linq;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.OptionalAggregateSelectRedeclaration;
        internal static class OptionalAggregateSelectRedeclarationConsumer
        {
            internal static bool Exercise()
            {
                var empty = new Child();
                IRoot emptyRoot = empty;
                if (emptyRoot.ArrayValue is not null
                    || emptyRoot.ListValue is not null
                    || emptyRoot.BagValue is not null
                    || emptyRoot.SetValue is not null)
                {
                    return false;
                }

                var specialized = new Specialized();
                var selected = NarrowChoice.FromSpecialized(specialized);
                var array = new ExpressArray<NarrowChoice>(1, 1);
                array[1] = selected;
                var list = new ExpressList<NarrowChoice> { selected };
                var bag = new ExpressBag<NarrowChoice> { selected };
                var set = new ExpressSet<NarrowChoice> { selected };
                var populated = new Child
                {
                    ArrayValue = array,
                    ListValue = list,
                    BagValue = bag,
                    SetValue = set,
                };
                IRoot populatedRoot = populated;
                var listView = populatedRoot.ListValue!;
                list.Add(selected);

                return ReferenceEquals(populatedRoot.ArrayValue![1], specialized)
                    && listView.Count == 2
                    && listView.All(item => ReferenceEquals(item, specialized))
                    && populatedRoot.BagValue!.Single() == specialized
                    && populatedRoot.SetValue!.Single() == specialized;
            }
        }
        """;

    private const string SELECT_REDECLARATION_SCHEMA = """
        SCHEMA select_redeclaration;
        ENTITY target;
        END_ENTITY;
        ENTITY specialized SUBTYPE OF (target);
        END_ENTITY;
        ENTITY other;
        END_ENTITY;
        TYPE broad_choice = SELECT (target, specialized, other);
        END_TYPE;
        TYPE narrow_choice = SELECT (specialized);
        END_TYPE;
        ENTITY root ABSTRACT;
          select_to_select : broad_choice;
          entity_to_select : broad_choice;
          select_to_entity : target;
        END_ENTITY;
        ENTITY child SUBTYPE OF (root);
          SELF\root.select_to_select : narrow_choice;
          SELF\root.entity_to_select : specialized;
          SELF\root.select_to_entity : narrow_choice;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string OPTIONAL_REDECLARATION_SCHEMA = """
        SCHEMA optional_redeclaration;
        ENTITY target;
        END_ENTITY;
        ENTITY specialized SUBTYPE OF (target);
        END_ENTITY;
        ENTITY root ABSTRACT;
          optional_number : OPTIONAL NUMBER;
          optional_link : OPTIONAL target;
        END_ENTITY;
        ENTITY child SUBTYPE OF (root);
          SELF\root.optional_number : INTEGER;
          SELF\root.optional_link : specialized;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SPECIALIZATION_ROUND_TRIP_SCHEMA = """
        SCHEMA specialization_round_trip;
        ENTITY target;
          code : STRING;
        END_ENTITY;
        ENTITY specialized SUBTYPE OF (target);
        END_ENTITY;
        ENTITY other;
        END_ENTITY;
        TYPE broad_choice = SELECT (target, other);
        END_TYPE;
        TYPE narrow_choice = SELECT (specialized);
        END_TYPE;
        ENTITY root ABSTRACT;
          link : target;
          selected : broad_choice;
          integer_value : NUMBER;
          real_value : NUMBER;
          array_value : ARRAY [1:2] OF target;
          list_value : LIST [0:?] OF UNIQUE target;
          bag_value : BAG [0:?] OF target;
          set_value : SET [0:?] OF target;
          optional_link : OPTIONAL target;
        END_ENTITY;
        ENTITY child SUBTYPE OF (root);
          SELF\root.link : specialized;
          SELF\root.selected : narrow_choice;
          SELF\root.integer_value : INTEGER;
          SELF\root.real_value : REAL;
          SELF\root.array_value : ARRAY [1:2] OF specialized;
          SELF\root.list_value : LIST [0:?] OF UNIQUE specialized;
          SELF\root.bag_value : BAG [0:?] OF specialized;
          SELF\root.set_value : SET [0:?] OF specialized;
          SELF\root.optional_link : specialized;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string INVALID_SELECT_REDECLARATION_SCHEMA = """
        SCHEMA invalid_select_redeclaration;
        ENTITY target;
        END_ENTITY;
        ENTITY specialized SUBTYPE OF (target);
        END_ENTITY;
        ENTITY other;
        END_ENTITY;
        ENTITY unrelated;
        END_ENTITY;
        TYPE broad_choice = SELECT (target, other);
        END_TYPE;
        TYPE added_choice = SELECT (target, other, unrelated);
        END_TYPE;
        TYPE replacement_choice = SELECT (specialized, unrelated);
        END_TYPE;
        ENTITY root ABSTRACT;
          added : broad_choice;
          replaced : broad_choice;
        END_ENTITY;
        ENTITY child SUBTYPE OF (root);
          SELF\root.added : added_choice;
          SELF\root.replaced : replacement_choice;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string INVALID_OPTIONAL_REDECLARATION_SCHEMA = """
        SCHEMA invalid_optional_redeclaration;
        ENTITY root ABSTRACT;
          measure : INTEGER;
        END_ENTITY;
        ENTITY child SUBTYPE OF (root);
          SELF\root.measure : OPTIONAL INTEGER;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string NARROWED_AGGREGATE_REDECLARATION_SCHEMA = """
        SCHEMA narrowed_aggregate_redeclaration;
        ENTITY target;
        END_ENTITY;
        ENTITY specialized SUBTYPE OF (target);
        END_ENTITY;
        ENTITY root ABSTRACT;
          items : LIST [0:?] OF target;
        END_ENTITY;
        ENTITY child SUBTYPE OF (root);
          SELF\root.items : LIST [1:2] OF specialized;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string UNSUPPORTED_SPECIALIZATION_MATRIX_SCHEMA = """
        SCHEMA unsupported_specialization_matrix;
        ENTITY target;
        END_ENTITY;
        ENTITY specialized SUBTYPE OF (target);
        END_ENTITY;
        TYPE broad_code = STRING;
        END_TYPE;
        TYPE constrained_code = STRING;
        WHERE
          nonempty : LENGTH(SELF) > 0;
        END_TYPE;
        ENTITY root ABSTRACT;
          array_bounds : ARRAY [1:2] OF target;
          array_optional : ARRAY [1:2] OF target;
          array_unique : ARRAY [1:2] OF target;
          list_bounds : LIST [0:?] OF target;
          list_unique : LIST [0:?] OF target;
          kind_change : LIST [0:?] OF target;
          nested : LIST [0:?] OF LIST [0:?] OF target;
          constrained : broad_code;
        END_ENTITY;
        ENTITY child SUBTYPE OF (root);
          SELF\root.array_bounds : ARRAY [1:3] OF specialized;
          SELF\root.array_optional : ARRAY [1:2] OF OPTIONAL specialized;
          SELF\root.array_unique : ARRAY [1:2] OF UNIQUE specialized;
          SELF\root.list_bounds : LIST [1:?] OF specialized;
          SELF\root.list_unique : LIST [0:?] OF UNIQUE specialized;
          SELF\root.kind_change : SET [0:?] OF specialized;
          SELF\root.nested : LIST [0:?] OF LIST [0:?] OF specialized;
          SELF\root.constrained : constrained_code;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string INVALID_ORIGIN_REDECLARATION_SCHEMA = """
        SCHEMA invalid_origin_redeclaration;
        ENTITY target;
        END_ENTITY;
        ENTITY root ABSTRACT;
          link : target;
        END_ENTITY;
        ENTITY unrelated;
          link : target;
        END_ENTITY;
        ENTITY child SUBTYPE OF (root);
          SELF\unrelated.link : target;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string INCOMPARABLE_DIAMOND_SCHEMA = """
        SCHEMA incomparable_diamond;
        ENTITY target;
        END_ENTITY;
        ENTITY left_target SUBTYPE OF (target);
        END_ENTITY;
        ENTITY right_target SUBTYPE OF (target);
        END_ENTITY;
        ENTITY root ABSTRACT;
          link : target;
        END_ENTITY;
        ENTITY left_branch ABSTRACT SUBTYPE OF (root);
          SELF\root.link : left_target;
        END_ENTITY;
        ENTITY right_branch ABSTRACT SUBTYPE OF (root);
          SELF\root.link : right_target;
        END_ENTITY;
        ENTITY leaf SUBTYPE OF (left_branch, right_branch);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string COMPOUND_INVALID_DIAMOND_SCHEMA = """
        SCHEMA compound_invalid_diamond;
        ENTITY target;
        END_ENTITY;
        ENTITY left_target;
        END_ENTITY;
        ENTITY right_target;
        END_ENTITY;
        ENTITY root ABSTRACT;
          link : target;
        END_ENTITY;
        ENTITY left_branch ABSTRACT SUBTYPE OF (root);
          SELF\root.link : left_target;
        END_ENTITY;
        ENTITY right_branch ABSTRACT SUBTYPE OF (root);
          SELF\root.link : right_target;
        END_ENTITY;
        ENTITY leaf SUBTYPE OF (left_branch, right_branch);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string ORDERED_REDECLARATION_SCHEMA = """
        SCHEMA ordered_redeclaration;
        ENTITY target;
        END_ENTITY;
        ENTITY specialized SUBTYPE OF (target);
        END_ENTITY;
        ENTITY most_specialized SUBTYPE OF (specialized);
        END_ENTITY;
        ENTITY root ABSTRACT;
          link : target;
        END_ENTITY;
        ENTITY middle ABSTRACT SUBTYPE OF (root);
          SELF\root.link : specialized;
        END_ENTITY;
        ENTITY leaf SUBTYPE OF (middle);
          SELF\root.link : most_specialized;
        END_ENTITY;
        ENTITY left_branch ABSTRACT SUBTYPE OF (root);
          SELF\root.link : specialized;
        END_ENTITY;
        ENTITY right_branch ABSTRACT SUBTYPE OF (root);
          SELF\root.link : specialized;
        END_ENTITY;
        ENTITY diamond_leaf SUBTYPE OF (left_branch, right_branch);
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
        ENTITY surface;
        END_ENTITY;
        ENTITY plane SUBTYPE OF (surface);
        END_ENTITY;
        ENTITY curve_base;
          basis : surface;
        END_ENTITY;
        ENTITY curve_on_plane SUBTYPE OF (curve_base);
          SELF\curve_base.basis : plane;
        END_ENTITY;
        ENTITY point_base;
          basis : surface;
        END_ENTITY;
        ENTITY point_on_plane SUBTYPE OF (point_base);
          SELF\point_base.basis : plane;
        END_ENTITY;
        ENTITY line_and_point_on_plane SUBTYPE OF (curve_on_plane, point_on_plane);
        DERIVE
          SELF\curve_base.basis : plane := SELF\point_base.basis;
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
    /// Verifies an entity subtype redeclaration uses one narrow mutable slot and a lossless inherited getter.
    /// </summary>
    [Test]
    public async Task Should_project_entity_type_narrowing_through_the_inherited_interface()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/narrowed.exp", NARROWED_REDECLARATION_SCHEMA));

        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();

        var assembly = Emit(result.OutputCompilation);
        var specialized = Activator.CreateInstance(assembly.GetType(
            "TedToolkit.Step21.Generated.NarrowedRedeclaration.Specialized",
            throwOnError: true)!)!;
        var childType = assembly.GetType(
            "TedToolkit.Step21.Generated.NarrowedRedeclaration.Child",
            throwOnError: true)!;
        var rootInterface = assembly.GetType(
            "TedToolkit.Step21.Generated.NarrowedRedeclaration.IRoot",
            throwOnError: true)!;
        var child = childType.GetConstructors().Single().Invoke([specialized]);

        using (Assert.Multiple())
        {
            await Assert.That(childType.GetProperty("Link")!.PropertyType.Name).IsEqualTo("ISpecialized");
            await Assert.That(childType.GetProperty("Link")!.GetValue(child)).IsSameReferenceAs(specialized);
            await Assert.That(rootInterface.GetProperty("Link")!.GetValue(child)).IsSameReferenceAs(specialized);
            await Assert.That(((Entity)child).DirectReferences.Single()).IsSameReferenceAs(specialized);
        }
    }

    /// <summary>
    /// Verifies a renamed specialization uses the renamed narrow member as its only mutable physical slot.
    /// </summary>
    [Test]
    public async Task Should_project_renamed_entity_narrowing_through_one_narrow_storage_member()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/renamed-narrowed.exp", RENAMED_NARROWED_REDECLARATION_SCHEMA));

        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();

        var childSymbol = RequiredType(
            result.OutputCompilation,
            "TedToolkit.Step21.Generated.RenamedNarrowedRedeclaration.Child");
        await Assert.That(childSymbol.GetMembers().OfType<IPropertySymbol>()
            .Where(property => property.DeclaredAccessibility == Accessibility.Public)
            .Select(property => property.Name)).IsEquivalentTo(["SpecializedLink", "DirectReferences"]);
        await Assert.That(childSymbol.Constructors.Single().Parameters.Select(parameter => parameter.Name))
            .IsEquivalentTo(["specializedLink"]);

        var assembly = Emit(result.OutputCompilation);
        var descriptor = (SchemaDescriptor)assembly.GetType(
            "TedToolkit.Step21.Generated.RenamedNarrowedRedeclaration.SchemaDescriptor",
            throwOnError: true)!.GetProperty("Instance")!.GetValue(null)!;
        var rootInterface = assembly.GetType(
            "TedToolkit.Step21.Generated.RenamedNarrowedRedeclaration.IRoot",
            throwOnError: true)!;
        var childInterface = assembly.GetType(
            "TedToolkit.Step21.Generated.RenamedNarrowedRedeclaration.IChild",
            throwOnError: true)!;
        var structure = ExchangeStructure.Read(new StringReader(CreateExchange(
            "RENAMED_NARROWED_REDECLARATION",
            "#1=SPECIALIZED();\r\n#2=CHILD(#1);")), [descriptor]);
        var specialized = structure.Registrations
            .Single(item => item.Name.Equals(new EntityInstanceName("1"))).Entity;
        var child = structure.Registrations.Single(item => item.Name.Equals(new EntityInstanceName("2"))).Entity;
        var output = new StringWriter();
        structure.Write(output);
        var reread = ExchangeStructure.Read(new StringReader(output.ToString()), [descriptor]);
        var rereadSpecialized = reread.Registrations
            .Single(item => item.Name.Equals(new EntityInstanceName("1"))).Entity;
        var rereadChild = reread.Registrations
            .Single(item => item.Name.Equals(new EntityInstanceName("2"))).Entity;

        using (Assert.Multiple())
        {
            await Assert.That(child.GetType().GetProperty("SpecializedLink")!.GetValue(child))
                .IsSameReferenceAs(specialized);
            await Assert.That(rootInterface.GetProperty("Link")!.GetValue(child)).IsSameReferenceAs(specialized);
            await Assert.That(childInterface.GetProperty("SpecializedLink")!.GetValue(child))
                .IsSameReferenceAs(specialized);
            await Assert.That(((Entity)child).DirectReferences.Single()).IsSameReferenceAs(specialized);
            await Assert.That(output.ToString()).Contains("#2=CHILD(#1);");
            await Assert.That(rootInterface.GetProperty("Link")!.GetValue(rereadChild))
                .IsSameReferenceAs(rereadSpecialized);
            await Assert.That(childInterface.GetProperty("SpecializedLink")!.GetValue(rereadChild))
                .IsSameReferenceAs(rereadSpecialized);
        }
    }

    /// <summary>
    /// Verifies narrowed entity storage reads, writes, and rereads one inherited physical parameter atomically.
    /// </summary>
    [Test]
    public async Task Should_round_trip_narrowed_entity_storage_and_reject_a_broad_only_value()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/narrowed.exp", NARROWED_REDECLARATION_SCHEMA));
        var assembly = Emit(result.OutputCompilation);
        var descriptor = (SchemaDescriptor)assembly.GetType(
            "TedToolkit.Step21.Generated.NarrowedRedeclaration.SchemaDescriptor",
            throwOnError: true)!.GetProperty("Instance")!.GetValue(null)!;
        var rootInterface = assembly.GetType(
            "TedToolkit.Step21.Generated.NarrowedRedeclaration.IRoot",
            throwOnError: true)!;
        var source = CreateExchange(
            "NARROWED_REDECLARATION",
            "#1=SPECIALIZED();\r\n#2=CHILD(#1);");

        var structure = ExchangeStructure.Read(new StringReader(source), [descriptor]);
        var specialized = structure.Registrations.Single(item => item.Name.Equals(new EntityInstanceName("1"))).Entity;
        var child = structure.Registrations.Single(item => item.Name.Equals(new EntityInstanceName("2"))).Entity;
        var output = new StringWriter();
        structure.Write(output);
        var reread = ExchangeStructure.Read(new StringReader(output.ToString()), [descriptor]);
        var rereadSpecialized = reread.Registrations
            .Single(item => item.Name.Equals(new EntityInstanceName("1"))).Entity;
        var rereadChild = reread.Registrations.Single(item => item.Name.Equals(new EntityInstanceName("2"))).Entity;
        var invalid = Assert.Throws<ExchangeStructureReadValidationException>(() => ExchangeStructure.Read(
            new StringReader(CreateExchange(
                "NARROWED_REDECLARATION",
                "#1=TARGET();\r\n#2=CHILD(#1);")),
            [descriptor]));
        var projectedBeforeInvalidEdit = rootInterface.GetProperty("Link")!.GetValue(child);
        child.GetType().GetProperty("Link")!.SetValue(child, null);
        var invalidOutput = new StringWriter();
        var writeFailure = Assert.Throws<ExchangeStructureWriteValidationException>(() =>
            structure.Write(invalidOutput));

        using (Assert.Multiple())
        {
            await Assert.That(projectedBeforeInvalidEdit).IsSameReferenceAs(specialized);
            await Assert.That(rootInterface.GetProperty("Link")!.GetValue(rereadChild))
                .IsSameReferenceAs(rereadSpecialized);
            await Assert.That(output.ToString()).Contains("#2=CHILD(#1);");
            await Assert.That(invalid.ValidationResult.Failures.Single().Code)
                .IsEqualTo("P21.READ.REFERENCE.TYPE");
            await Assert.That(invalid.ValidationResult.Failures.Single().Path).Contains("Parameters[0]");
            await Assert.That(writeFailure.ValidationResult.Failures.Any(item => item.Path.Contains(
                "Link",
                StringComparison.Ordinal))).IsTrue();
            await Assert.That(invalidOutput.ToString()).IsEmpty();
        }
    }

    /// <summary>
    /// Verifies an unrelated redeclared entity domain is rejected as an ISO-invalid binding.
    /// </summary>
    [Test]
    public async Task Should_reject_unrelated_entity_redeclaration_as_invalid_binding()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/unrelated.exp", UNRELATED_REDECLARATION_SCHEMA));
        var diagnostic = result.Diagnostics.Single(item => item.Id == "STEP21EXP002");

        using (Assert.Multiple())
        {
            await Assert.That(diagnostic.GetMessage()).Contains("EXPRESS-BIND-INVALID-REDECLARATION");
            await Assert.That(diagnostic.Location.GetLineSpan().StartLinePosition.Line).IsEqualTo(9);
            await Assert.That(result.GeneratedSources).IsEmpty();
        }
    }

    /// <summary>
    /// Verifies NUMBER-to-INTEGER and NUMBER-to-REAL getters retain exact numeric alternatives.
    /// </summary>
    [Test]
    public async Task Should_project_exact_numeric_redeclarations_without_binary_floating_point()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/numeric.exp", NUMERIC_REDECLARATION_SCHEMA));

        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();

        var assembly = Emit(result.OutputCompilation);
        var childType = assembly.GetType(
            "TedToolkit.Step21.Generated.NumericRedeclaration.Child",
            throwOnError: true)!;
        var rootInterface = assembly.GetType(
            "TedToolkit.Step21.Generated.NumericRedeclaration.IRoot",
            throwOnError: true)!;
        var integer = System.Numerics.BigInteger.Parse("18446744073709551616000000000000000001");
        var real = new RealValue(
            System.Numerics.BigInteger.Parse("12345678901234567890123456789"),
            new System.Numerics.BigInteger(-17));
        var child = childType.GetConstructors().Single().Invoke([integer, real]);

        using (Assert.Multiple())
        {
            await Assert.That(childType.GetProperty("IntegerValue")!.GetValue(child)).IsEqualTo(integer);
            await Assert.That(childType.GetProperty("RealValue")!.GetValue(child)).IsEqualTo(real);
            await Assert.That(rootInterface.GetProperty("IntegerValue")!.GetValue(child))
                .IsEqualTo(NumberValue.FromInteger(integer));
            await Assert.That(rootInterface.GetProperty("RealValue")!.GetValue(child))
                .IsEqualTo(NumberValue.FromReal(real));
        }
    }

    /// <summary>
    /// Verifies ARRAY, LIST, BAG, and SET element specialization retains one mutable aggregate instance.
    /// </summary>
    [Test]
    public async Task Should_project_all_aggregate_specializations_through_covariant_views()
    {
        var result = GeneratorHostTests.Run(
            AGGREGATE_REDECLARATION_CONSUMER,
            ("schemas/aggregate.exp", AGGREGATE_REDECLARATION_SCHEMA));

        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();

        var assembly = Emit(result.OutputCompilation);
        var consumer = assembly.GetType("AggregateRedeclarationConsumer", throwOnError: true)!;
        await Assert.That((bool)consumer.GetMethod(
            "Exercise",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, null)!)
            .IsTrue();
    }

    /// <summary>
    /// Verifies strict and equal-coverage SELECT element redeclarations expose live views for every aggregate kind.
    /// </summary>
    [Test]
    public async Task Should_project_aggregate_select_specializations_through_live_read_only_views()
    {
        var result = GeneratorHostTests.Run(
            AGGREGATE_SELECT_REDECLARATION_CONSUMER,
            ("schemas/aggregate-select.exp", AGGREGATE_SELECT_REDECLARATION_SCHEMA));

        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();

        var assembly = Emit(result.OutputCompilation);
        var consumer = assembly.GetType("AggregateSelectRedeclarationConsumer", throwOnError: true)!;
        await Assert.That((bool)consumer.GetMethod(
            "Exercise",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, null)!)
            .IsTrue();
    }

    /// <summary>
    /// Verifies a named aggregate specialization can project through its unique inherited SELECT alternative.
    /// </summary>
    [Test]
    public async Task Should_project_named_aggregate_specialization_through_select_alternative()
    {
        var result = GeneratorHostTests.Run(
            """
            using System.IO;
            using System.Linq;
            using TedToolkit.Step21;
            using TedToolkit.Step21.Generated.NamedAggregateSelectRedeclaration;

            internal static class NamedAggregateSelectConsumer
            {
                internal static bool Exercise()
                {
                    const string source = "ISO-10303-21;HEADER;FILE_DESCRIPTION(('test'),'3;1');"
                        + "FILE_NAME('test.step','2026-09-02T00:00:00',(''),(''),'','','');"
                        + "FILE_SCHEMA(('named_aggregate_select_redeclaration'));ENDSEC;DATA;"
                        + "#1=SPECIALIZED();#2=CHILD((#1));ENDSEC;END-ISO-10303-21;";
                    var structure = ExchangeStructure.Read(
                        new StringReader(source),
                        [TedToolkit.Step21.Generated.NamedAggregateSelectRedeclaration.SchemaDescriptor.Instance]);
                    var child = structure.Entities.OfType<Child>().Single();
                    IRoot root = child;
                    var count = root.ItemElement.Match(
                        list => list.ReadOnlyValue.Count,
                        set => -1);
                    var output = new StringWriter();
                    structure.Write(output);
                    return count == 1 && output.ToString().Contains("#2=CHILD((#1));");
                }
            }
            """,
            ("schemas/named-aggregate-select.exp", """
                SCHEMA named_aggregate_select_redeclaration;
                ENTITY representation_item;
                END_ENTITY;
                ENTITY specialized SUBTYPE OF (representation_item);
                END_ENTITY;
                TYPE list_representation_item = LIST [1:?] OF representation_item;
                END_TYPE;
                TYPE set_representation_item = SET [1:?] OF representation_item;
                END_TYPE;
                TYPE compound_item_definition = SELECT
                  (list_representation_item, set_representation_item);
                END_TYPE;
                TYPE specialized_members = LIST [1:?] OF specialized;
                END_TYPE;
                ENTITY root ABSTRACT;
                  item_element : compound_item_definition;
                END_ENTITY;
                ENTITY child SUBTYPE OF (root);
                  SELF\root.item_element : specialized_members;
                END_ENTITY;
                END_SCHEMA;
                """));

        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
            .IsEmpty();
        var assembly = Emit(result.OutputCompilation);
        var consumer = assembly.GetType("NamedAggregateSelectConsumer", throwOnError: true)!;
        await Assert.That((bool)consumer.GetMethod(
            "Exercise",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, null)!)
            .IsTrue();
    }

    /// <summary>Verifies an aggregate SELECT leaf outside the inherited domain is ISO-invalid.</summary>
    [Test]
    public async Task Should_reject_aggregate_select_leaf_widening_as_invalid()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/invalid-aggregate-select.exp", INVALID_AGGREGATE_SELECT_REDECLARATION_SCHEMA));
        var diagnostic = result.Diagnostics.Single();

        using (Assert.Multiple())
        {
            await Assert.That(diagnostic.Id).IsEqualTo("STEP21EXP002");
            await Assert.That(diagnostic.GetMessage()).Contains("EXPRESS-BIND-INVALID-REDECLARATION");
            await Assert.That(diagnostic.Location.GetLineSpan().StartLinePosition.Line).IsGreaterThan(0);
            await Assert.That(result.GeneratedSources).IsEmpty();
        }
    }

    /// <summary>Verifies non-entity and extensible SELECT element domains remain explicitly unsupported.</summary>
    [Test]
    public async Task Should_reject_unbounded_aggregate_select_shapes_as_unsupported()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/unsupported-aggregate-select.exp", UNSUPPORTED_AGGREGATE_SELECT_REDECLARATION_SCHEMA));
        var diagnostics = result.Diagnostics.ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(diagnostics.Length).IsEqualTo(2);
            await Assert.That(diagnostics.All(diagnostic => diagnostic.Id == "STEP21EXP005")).IsTrue();
            await Assert.That(diagnostics.All(diagnostic => diagnostic.GetMessage().Contains(
                "outside the supported M-01 through M-06 mapping matrix",
                StringComparison.Ordinal))).IsTrue();
            await Assert.That(diagnostics.All(diagnostic => diagnostic.Location.GetLineSpan().StartLinePosition.Line > 0))
                .IsTrue();
            await Assert.That(result.GeneratedSources).IsEmpty();
        }
    }

    /// <summary>Verifies optional narrowed aggregate storage projects null and live non-null values losslessly.</summary>
    [Test]
    public async Task Should_preserve_optional_aggregate_select_specialization_nullability()
    {
        var result = GeneratorHostTests.Run(
            OPTIONAL_AGGREGATE_SELECT_REDECLARATION_CONSUMER,
            ("schemas/optional-aggregate-select.exp", OPTIONAL_AGGREGATE_SELECT_REDECLARATION_SCHEMA));

        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
                || diagnostic.Severity == DiagnosticSeverity.Warning)).IsEmpty();

        var assembly = Emit(result.OutputCompilation);
        var consumer = assembly.GetType("OptionalAggregateSelectRedeclarationConsumer", throwOnError: true)!;
        await Assert.That((bool)consumer.GetMethod(
            "Exercise",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, null)!)
            .IsTrue();
    }

    /// <summary>Verifies M-06 values survive schema-bound read, edit, write, and reread.</summary>
    [Test]
    public async Task Should_round_trip_aggregate_select_specializations_and_reject_invalid_values_atomically()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/aggregate-select.exp", AGGREGATE_SELECT_REDECLARATION_SCHEMA));
        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();

        var assembly = Emit(result.OutputCompilation);
        var descriptor = (SchemaDescriptor)assembly.GetType(
            "TedToolkit.Step21.Generated.AggregateSelectRedeclaration.SchemaDescriptor",
            throwOnError: true)!.GetProperty("Instance")!.GetValue(null)!;
        var rootInterface = assembly.GetType(
            "TedToolkit.Step21.Generated.AggregateSelectRedeclaration.IRoot",
            throwOnError: true)!;
        var structure = ExchangeStructure.Read(
            new StringReader(CreateAggregateSelectExchange("(#1,$)", "(#1)", "(#1,#2)", "(#1,#2)")),
            [descriptor]);
        var child = structure.Registrations.Single(item => item.Name.Equals(new EntityInstanceName("4"))).Entity;
        var first = structure.Registrations.Single(item => item.Name.Equals(new EntityInstanceName("1"))).Entity;
        var second = structure.Registrations.Single(item => item.Name.Equals(new EntityInstanceName("2"))).Entity;
        var list = child.GetType().GetProperty("ListValue")!.GetValue(child)!;
        var equalChoice = assembly.GetType(
            "TedToolkit.Step21.Generated.AggregateSelectRedeclaration.EqualChoice",
            throwOnError: true)!;
        var equalSecond = equalChoice.GetMethod("FromSecond")!.Invoke(null, [second])!;
        list.GetType().GetMethod("Add")!.Invoke(list, [equalSecond]);

        var output = new StringWriter();
        structure.Write(output);
        var reread = ExchangeStructure.Read(new StringReader(output.ToString()), [descriptor]);
        var rereadChild = reread.Registrations.Single(item => item.Name.Equals(new EntityInstanceName("4"))).Entity;
        var rereadFirst = reread.Registrations.Single(item => item.Name.Equals(new EntityInstanceName("1"))).Entity;
        var rereadSecond = reread.Registrations.Single(item => item.Name.Equals(new EntityInstanceName("2"))).Entity;
        var arrayView = rootInterface.GetProperty("ArrayValue")!.GetValue(rereadChild)!;
        var listView = (System.Collections.IEnumerable)rootInterface.GetProperty("ListValue")!.GetValue(rereadChild)!;
        var listItems = listView.Cast<object>().ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(reread.Validate().IsValid).IsTrue();
            await Assert.That(arrayView.GetType().GetProperty("Count")!.GetValue(arrayView)).IsEqualTo(2);
            await Assert.That((bool)arrayView.GetType().GetMethod("IsSet")!.Invoke(arrayView, [1])!).IsTrue();
            await Assert.That((bool)arrayView.GetType().GetMethod("IsSet")!.Invoke(arrayView, [2])!).IsFalse();
            await Assert.That(arrayView.GetType().GetProperty("Item")!.GetValue(arrayView, [1]))
                .IsSameReferenceAs(rereadFirst);
            await Assert.That(listItems.Length).IsEqualTo(2);
            await Assert.That(ReadSelectedEntity(listItems[0].GetType(), listItems[0], "TryGetFirst"))
                .IsSameReferenceAs(rereadFirst);
            await Assert.That(ReadSelectedEntity(listItems[1].GetType(), listItems[1], "TryGetSecond"))
                .IsSameReferenceAs(rereadSecond);
            await Assert.That(output.ToString()).Contains("#4=CHILD((#1,$),(#1,#2),(#1,#2),(#1,#2));");
        }

        Exception? readFailure = null;
        try
        {
            _ = ExchangeStructure.Read(
                new StringReader(CreateAggregateSelectExchange("(#3,$)", "(#1)", "(#1)", "(#1)")),
                [descriptor]);
        }
        catch (Exception exception) when (exception is ExchangeStructureBindingException
            or ExchangeStructureReadValidationException)
        {
            readFailure = exception;
        }

        await Assert.That(readFailure).IsNotNull();

        list.GetType().GetMethod("Clear")!.Invoke(list, null);
        var invalidOutput = new StringWriter();
        _ = Assert.Throws<ExchangeStructureWriteValidationException>(() => structure.Write(invalidOutput));
        await Assert.That(invalidOutput.ToString()).IsEmpty();
    }

    /// <summary>
    /// Verifies closed entity-valued SELECT specializations preserve the selected entity identity.
    /// </summary>
    [Test]
    public async Task Should_project_closed_select_specializations_without_erasing_the_selected_leaf()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/select.exp", SELECT_REDECLARATION_SCHEMA));

        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();

        var assembly = Emit(result.OutputCompilation);
        var specialized = Activator.CreateInstance(assembly.GetType(
            "TedToolkit.Step21.Generated.SelectRedeclaration.Specialized",
            throwOnError: true)!)!;
        var narrowType = assembly.GetType(
            "TedToolkit.Step21.Generated.SelectRedeclaration.NarrowChoice",
            throwOnError: true)!;
        var narrow = narrowType.GetMethod("FromSpecialized")!.Invoke(null, [specialized])!;
        var childType = assembly.GetType(
            "TedToolkit.Step21.Generated.SelectRedeclaration.Child",
            throwOnError: true)!;
        var rootInterface = assembly.GetType(
            "TedToolkit.Step21.Generated.SelectRedeclaration.IRoot",
            throwOnError: true)!;
        var child = childType.GetConstructors().Single().Invoke([narrow, specialized, narrow]);
        var projectedSelect = rootInterface.GetProperty("SelectToSelect")!.GetValue(child)!;
        var projectedEntity = rootInterface.GetProperty("EntityToSelect")!.GetValue(child)!;
        var directEntity = rootInterface.GetProperty("SelectToEntity")!.GetValue(child)!;
        var broadType = projectedSelect.GetType();

        using (Assert.Multiple())
        {
            await Assert.That(broadType.GetProperty("Kind")!.GetValue(projectedSelect)!.ToString())
                .IsEqualTo("Specialized");
            await Assert.That(broadType.GetProperty("Kind")!.GetValue(projectedEntity)!.ToString())
                .IsEqualTo("Specialized");
            await Assert.That(ReadSelectedEntity(broadType, projectedSelect, "TryGetSpecialized"))
                .IsSameReferenceAs(specialized);
            await Assert.That(ReadSelectedEntity(broadType, projectedEntity, "TryGetSpecialized"))
                .IsSameReferenceAs(specialized);
            await Assert.That(directEntity).IsSameReferenceAs(specialized);
        }
    }

    /// <summary>
    /// Verifies a multiply inherited entity uses the direct SELECT alternative when another route is nested.
    /// </summary>
    [Test]
    public async Task Should_prefer_a_direct_entity_select_projection_over_a_nested_route()
    {
        const string schema = """
            SCHEMA direct_select_projection;
            ENTITY characterized_object;
            END_ENTITY;
            ENTITY product_definition;
            END_ENTITY;
            TYPE characterized_product_definition = SELECT (product_definition);
            END_TYPE;
            TYPE characterized_definition = SELECT (
              characterized_object,
              characterized_product_definition);
            END_TYPE;
            ENTITY risk_value SUBTYPE OF (characterized_object, product_definition);
            END_ENTITY;
            ENTITY property_definition ABSTRACT;
              definition : characterized_definition;
            END_ENTITY;
            ENTITY risk_level SUBTYPE OF (property_definition);
              SELF\property_definition.definition : risk_value;
            END_ENTITY;
            END_SCHEMA;
            """;
        var result = GeneratorHostTests.Run(("schemas/direct-select-projection.exp", schema));

        await Assert.That(result.Diagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        await Assert.That(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();

        var assembly = Emit(result.OutputCompilation);
        var riskValue = Activator.CreateInstance(assembly.GetType(
            "TedToolkit.Step21.Generated.DirectSelectProjection.RiskValue",
            throwOnError: true)!)!;
        var riskLevel = Activator.CreateInstance(
            assembly.GetType("TedToolkit.Step21.Generated.DirectSelectProjection.RiskLevel", throwOnError: true)!,
            riskValue)!;
        var root = assembly.GetType(
            "TedToolkit.Step21.Generated.DirectSelectProjection.IPropertyDefinition",
            throwOnError: true)!;
        var projected = root.GetProperty("Definition")!.GetValue(riskLevel)!;

        using (Assert.Multiple())
        {
            await Assert.That(projected.GetType().GetProperty("Kind")!.GetValue(projected)!.ToString())
                .IsEqualTo("CharacterizedObject");
            await Assert.That(ReadSelectedEntity(projected.GetType(), projected, "TryGetCharacterizedObject"))
                .IsSameReferenceAs(riskValue);
        }
    }

    /// <summary>
    /// Verifies SELECT leaf addition and unrelated replacement remain ISO-invalid rather than unsupported.
    /// </summary>
    [Test]
    public async Task Should_reject_select_leaf_widening_with_exact_invalid_binding_diagnostics()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/invalid-select.exp", INVALID_SELECT_REDECLARATION_SCHEMA));
        var diagnostics = result.Diagnostics.ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id))
                .IsEquivalentTo(["STEP21EXP002", "STEP21EXP002"]);
            await Assert.That(diagnostics.All(diagnostic => diagnostic.GetMessage().Contains(
                "EXPRESS-BIND-INVALID-REDECLARATION",
                StringComparison.Ordinal))).IsTrue();
            await Assert.That(diagnostics.Select(diagnostic =>
                    diagnostic.Location.GetLineSpan().StartLinePosition.Line))
                .IsEquivalentTo([20, 21]);
            await Assert.That(result.GeneratedSources).IsEmpty();
        }
    }

    /// <summary>
    /// Verifies OPTIONAL-to-required redeclarations expose required storage through inherited optional getters.
    /// </summary>
    [Test]
    public async Task Should_tighten_optional_redeclarations_without_duplicating_storage()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/optional.exp", OPTIONAL_REDECLARATION_SCHEMA));

        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();

        var assembly = Emit(result.OutputCompilation);
        var specialized = Activator.CreateInstance(assembly.GetType(
            "TedToolkit.Step21.Generated.OptionalRedeclaration.Specialized",
            throwOnError: true)!)!;
        var childType = assembly.GetType(
            "TedToolkit.Step21.Generated.OptionalRedeclaration.Child",
            throwOnError: true)!;
        var rootInterface = assembly.GetType(
            "TedToolkit.Step21.Generated.OptionalRedeclaration.IRoot",
            throwOnError: true)!;
        var integer = System.Numerics.BigInteger.Parse("999999999999999999999999999999999999");
        var child = childType.GetConstructors().Single().Invoke([integer, specialized]);

        using (Assert.Multiple())
        {
            await Assert.That(rootInterface.GetProperty("OptionalNumber")!.GetValue(child))
                .IsEqualTo(NumberValue.FromInteger(integer));
            await Assert.That(rootInterface.GetProperty("OptionalLink")!.GetValue(child))
                .IsSameReferenceAs(specialized);
            await Assert.That(childType.GetConstructors().Single().GetParameters().Length).IsEqualTo(2);
            await Assert.That(((Entity)child).DirectReferences.Single()).IsSameReferenceAs(specialized);
        }
    }

    /// <summary>
    /// Verifies all supported specialization mappings survive read, edit, validation, write, and reread.
    /// </summary>
    [Test]
    public async Task Should_round_trip_all_supported_specialization_mappings_with_complete_reread_oracles()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/specialization-round-trip.exp", SPECIALIZATION_ROUND_TRIP_SCHEMA));
        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();

        var assembly = Emit(result.OutputCompilation);
        var descriptor = (SchemaDescriptor)assembly.GetType(
            "TedToolkit.Step21.Generated.SpecializationRoundTrip.SchemaDescriptor",
            throwOnError: true)!.GetProperty("Instance")!.GetValue(null)!;
        var rootInterface = assembly.GetType(
            "TedToolkit.Step21.Generated.SpecializationRoundTrip.IRoot",
            throwOnError: true)!;
        var structure = ExchangeStructure.Read(
            new StringReader(CreateSpecializationExchange(CreateValidSpecializationParameters())),
            [descriptor]);
        var specialized = structure.Registrations
            .Single(item => item.Name.Equals(new EntityInstanceName("2"))).Entity;
        var child = structure.Registrations.Single(item => item.Name.Equals(new EntityInstanceName("3"))).Entity;
        specialized.GetType().GetProperty("Code")!.SetValue(specialized, "edited");
        child.GetType().GetProperty("BagValue")!.GetValue(child)!.GetType()
            .GetMethod("Add")!.Invoke(child.GetType().GetProperty("BagValue")!.GetValue(child), [specialized]);
        await Assert.That(structure.Validate().IsValid).IsTrue();

        var output = new StringWriter();
        structure.Write(output);
        var reread = ExchangeStructure.Read(new StringReader(output.ToString()), [descriptor]);
        var rereadSpecialized = reread.Registrations
            .Single(item => item.Name.Equals(new EntityInstanceName("2"))).Entity;
        var rereadChild = reread.Registrations.Single(item => item.Name.Equals(new EntityInstanceName("3"))).Entity;
        var selected = rootInterface.GetProperty("Selected")!.GetValue(rereadChild)!;
        var childType = rereadChild.GetType();

        using (Assert.Multiple())
        {
            await Assert.That(reread.Validate().IsValid).IsTrue();
            await Assert.That(rereadSpecialized.GetType().GetProperty("Code")!.GetValue(rereadSpecialized))
                .IsEqualTo("edited");
            await Assert.That(rootInterface.GetProperty("Link")!.GetValue(rereadChild))
                .IsSameReferenceAs(rereadSpecialized);
            await Assert.That(ReadSelectedEntity(selected.GetType(), selected, "TryGetTarget"))
                .IsSameReferenceAs(rereadSpecialized);
            await Assert.That(rootInterface.GetProperty("IntegerValue")!.GetValue(rereadChild))
                .IsEqualTo(NumberValue.FromInteger((System.Numerics.BigInteger)childType
                    .GetProperty("IntegerValue")!.GetValue(rereadChild)!));
            await Assert.That(rootInterface.GetProperty("RealValue")!.GetValue(rereadChild))
                .IsEqualTo(NumberValue.FromReal((RealValue)childType
                    .GetProperty("RealValue")!.GetValue(rereadChild)!));
            await Assert.That(rootInterface.GetProperty("OptionalLink")!.GetValue(rereadChild))
                .IsSameReferenceAs(rereadSpecialized);
            await Assert.That(ReferenceEquals(
                childType.GetProperty("ArrayValue")!.GetValue(rereadChild),
                rootInterface.GetProperty("ArrayValue")!.GetValue(rereadChild))).IsTrue();
            await Assert.That(ReferenceEquals(
                childType.GetProperty("ListValue")!.GetValue(rereadChild),
                rootInterface.GetProperty("ListValue")!.GetValue(rereadChild))).IsTrue();
            await Assert.That(ReferenceEquals(
                childType.GetProperty("BagValue")!.GetValue(rereadChild),
                rootInterface.GetProperty("BagValue")!.GetValue(rereadChild))).IsTrue();
            await Assert.That(ReferenceEquals(
                childType.GetProperty("SetValue")!.GetValue(rereadChild),
                rootInterface.GetProperty("SetValue")!.GetValue(rereadChild))).IsTrue();
            await Assert.That(((System.Collections.IEnumerable)rootInterface
                    .GetProperty("ArrayValue")!.GetValue(rereadChild)!).Cast<object>()
                .All(item => ReferenceEquals(item, rereadSpecialized))).IsTrue();
            await Assert.That(((System.Collections.IEnumerable)rootInterface
                    .GetProperty("ListValue")!.GetValue(rereadChild)!).Cast<object>()
                .All(item => ReferenceEquals(item, rereadSpecialized))).IsTrue();
            await Assert.That(((System.Collections.IEnumerable)rootInterface
                    .GetProperty("BagValue")!.GetValue(rereadChild)!).Cast<object>())
                .Count().IsEqualTo(3);
            await Assert.That(((System.Collections.IEnumerable)rootInterface
                    .GetProperty("SetValue")!.GetValue(rereadChild)!).Cast<object>()
                .All(item => ReferenceEquals(item, rereadSpecialized))).IsTrue();
        }

        child.GetType().GetProperty("OptionalLink")!.SetValue(child, null);
        var invalidOutput = new StringWriter();
        var writeFailure = Assert.Throws<ExchangeStructureWriteValidationException>(() =>
            structure.Write(invalidOutput));
        using (Assert.Multiple())
        {
            await Assert.That(writeFailure.ValidationResult.Failures.Any(failure => failure.Path.Contains(
                "OptionalLink",
                StringComparison.Ordinal))).IsTrue();
            await Assert.That(invalidOutput.ToString()).IsEmpty();
        }
    }

    /// <summary>
    /// Verifies every supported narrowed domain rejects a value that only satisfies the inherited broad domain.
    /// </summary>
    [Test]
    [Arguments(0, "#1")]
    [Arguments(1, "#1")]
    [Arguments(2, "1.5")]
    [Arguments(3, "7")]
    [Arguments(4, "(#1,#2)")]
    [Arguments(5, "(#1)")]
    [Arguments(6, "(#1)")]
    [Arguments(7, "(#1)")]
    [Arguments(8, "$")]
    public async Task Should_reject_broad_only_values_before_publishing_a_specialized_model(
        int parameterIndex,
        string broadOnlyValue)
    {
        var result = GeneratorHostTests.Run(
            ("schemas/specialization-round-trip.exp", SPECIALIZATION_ROUND_TRIP_SCHEMA));
        var assembly = Emit(result.OutputCompilation);
        var descriptor = (SchemaDescriptor)assembly.GetType(
            "TedToolkit.Step21.Generated.SpecializationRoundTrip.SchemaDescriptor",
            throwOnError: true)!.GetProperty("Instance")!.GetValue(null)!;
        var parameters = CreateValidSpecializationParameters();
        parameters[parameterIndex] = broadOnlyValue;
        var exception = CaptureSpecializationReadFailure(descriptor, parameters);

        if (exception is ExchangeStructureReadValidationException validation)
        {
            using (Assert.Multiple())
            {
                await Assert.That(validation.ValidationResult.Failures).IsNotEmpty();
                await Assert.That(validation.ValidationResult.Failures.Any(failure => failure.Path.Contains(
                    $"Parameters[{parameterIndex}]",
                    StringComparison.Ordinal))).IsTrue();
                await Assert.That(validation.ValidationResult.Failures.All(failure => failure.Code.StartsWith(
                    "P21.READ.",
                    StringComparison.Ordinal))).IsTrue();
            }

            return;
        }

        var binding = (ExchangeStructureBindingException)exception;
        using (Assert.Multiple())
        {
            await Assert.That(binding.Diagnostics).IsNotEmpty();
            await Assert.That(binding.Diagnostics.All(diagnostic => diagnostic.Code.StartsWith(
                "P21-BIND-",
                StringComparison.Ordinal))).IsTrue();
            await Assert.That(binding.Diagnostics.All(diagnostic => diagnostic.SourceLocation is
            { Line: > 0, Column: > 0, })).IsTrue();
        }
    }

    /// <summary>
    /// Verifies required-to-OPTIONAL redeclaration is rejected as ISO-invalid.
    /// </summary>
    [Test]
    public async Task Should_reject_required_to_optional_redeclaration()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/invalid-optional.exp", INVALID_OPTIONAL_REDECLARATION_SCHEMA));
        await Assert.That(string.Join(",", result.Diagnostics.Select(item => item.Id)))
            .IsEqualTo("STEP21EXP002");
        var diagnostic = result.Diagnostics.Single(item => item.Id == "STEP21EXP002");

        using (Assert.Multiple())
        {
            await Assert.That(diagnostic.GetMessage()).Contains("EXPRESS-BIND-INVALID-REDECLARATION");
            await Assert.That(diagnostic.GetMessage()).Contains("cannot be widened to OPTIONAL");
            await Assert.That(result.GeneratedSources).IsEmpty();
        }
    }

    /// <summary>
    /// Verifies narrowed aggregate cardinality retains one covariant storage and enforces its tighter bounds.
    /// </summary>
    [Test]
    [Arguments("LIST")]
    [Arguments("BAG")]
    [Arguments("SET")]
    public async Task Should_project_narrowed_aggregate_cardinality(string kind)
    {
        var result = GeneratorHostTests.Run("""
            using System.IO;
            using System.Linq;
            using TedToolkit.Step21;
            using TedToolkit.Step21.Generated.NarrowedAggregateRedeclaration;
            internal static class BoundsConsumer
            {
                internal static bool Check(int count)
                {
                    var parameters = string.Join(",", Enumerable.Range(1, count).Select(index => "#" + index));
                    var input = "ISO-10303-21;HEADER;FILE_DESCRIPTION(('bounds'),'3;1');" +
                        "FILE_NAME('bounds','2026-09-04T00:00:00',('A'),('O'),'P','S','');" +
                        "FILE_SCHEMA(('narrowed_aggregate_redeclaration'));ENDSEC;DATA;" +
                        "#1=SPECIALIZED();#2=SPECIALIZED();#3=SPECIALIZED();" +
                        "#4=CHILD((" + parameters + "));ENDSEC;END-ISO-10303-21;";
                    try
                    {
                        var structure = ExchangeStructure.Read(new StringReader(input),
                            [TedToolkit.Step21.Generated.NarrowedAggregateRedeclaration.SchemaDescriptor.Instance]);
                        var child = structure.Entities.OfType<Child>().Single();
                        IRoot root = child;
                        if (!object.ReferenceEquals(root.Items, child.Items)) return false;
                        var output = new StringWriter();
                        structure.Write(output);
                        var reread = ExchangeStructure.Read(new StringReader(output.ToString()),
                            [TedToolkit.Step21.Generated.NarrowedAggregateRedeclaration.SchemaDescriptor.Instance]);
                        return count is 1 or 2 && reread.Validate().IsValid &&
                            reread.Entities.OfType<Child>().Single().Items.Count == count;
                    }
                    catch (ExchangeStructureReadValidationException failure)
                    {
                        return count is 0 or 3 && failure.ValidationResult.Failures.Count > 0;
                    }
                }
            }
            """, ("schemas/narrowed-aggregate.exp", NARROWED_AGGREGATE_REDECLARATION_SCHEMA
                .Replace("LIST", kind, StringComparison.Ordinal)));
        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)).IsEmpty();
        var assembly = Emit(result.OutputCompilation);
        var check = assembly.GetType("BoundsConsumer", throwOnError: true)!
            .GetMethod("Check", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        for (var count = 0; count < 4; count++)
        {
            await Assert.That((bool)check.Invoke(null, [count])!).IsTrue().Because($"count {count}");
        }
    }

    /// <summary>
    /// Verifies aggregate kind, bounds, flags, nesting, and WHERE-constrained relations fail as unsupported.
    /// </summary>
    [Test]
    public async Task Should_reject_every_bounded_unsupported_specialization_partition_atomically()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/unsupported-matrix.exp", UNSUPPORTED_SPECIALIZATION_MATRIX_SCHEMA));
        var diagnostics = result.Diagnostics.ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(diagnostics.Length).IsEqualTo(7);
            await Assert.That(diagnostics.All(diagnostic => diagnostic.Id == "STEP21EXP005")).IsTrue();
            await Assert.That(diagnostics.All(diagnostic => diagnostic.GetMessage().Contains(
                "outside the supported M-01 through M-06 mapping matrix",
                StringComparison.Ordinal))).IsTrue();
            await Assert.That(diagnostics.All(diagnostic => diagnostic.Location.GetLineSpan().StartLinePosition is
            { Line: > 0, Character: > 0, })).IsTrue();
            await Assert.That(result.GeneratedSources).IsEmpty();
        }
    }

    /// <summary>
    /// Verifies a qualified origin outside the supertype closure is rejected as ISO-invalid.
    /// </summary>
    [Test]
    public async Task Should_reject_redeclaration_origin_outside_the_supertype_closure()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/invalid-origin.exp", INVALID_ORIGIN_REDECLARATION_SCHEMA));
        var diagnostic = result.Diagnostics.Single(item => item.Id == "STEP21EXP002");

        using (Assert.Multiple())
        {
            await Assert.That(diagnostic.GetMessage()).Contains("EXPRESS-BIND-INVALID-REDECLARATION");
            await Assert.That(diagnostic.GetMessage()).Contains("qualified origin");
            await Assert.That(result.GeneratedSources).IsEmpty();
        }
    }

    /// <summary>
    /// Verifies an incomparable diamond cannot select storage by traversal order.
    /// </summary>
    [Test]
    public async Task Should_reject_incomparable_diamond_redeclarations_without_selecting_storage()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/incomparable-diamond.exp", INCOMPARABLE_DIAMOND_SCHEMA));
        var diagnostic = result.Diagnostics.Single(item => item.Id == "STEP21EXP005");

        using (Assert.Multiple())
        {
            await Assert.That(diagnostic.GetMessage()).Contains("no unique most-specific storage");
            await Assert.That(result.GeneratedSources).IsEmpty();
        }
    }

    /// <summary>
    /// Verifies individually invalid diamond branches do not receive an incomparable-supported-domain diagnostic.
    /// </summary>
    [Test]
    public async Task Should_report_only_invalid_binding_diagnostics_for_compound_invalid_diamond()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/compound-invalid-diamond.exp", COMPOUND_INVALID_DIAMOND_SCHEMA));
        var diagnostics = result.Diagnostics.ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id))
                .IsEquivalentTo(["STEP21EXP002", "STEP21EXP002"]);
            await Assert.That(diagnostics.All(diagnostic => diagnostic.GetMessage().Contains(
                "EXPRESS-BIND-INVALID-REDECLARATION",
                StringComparison.Ordinal))).IsTrue();
            await Assert.That(diagnostics.Select(diagnostic =>
                    diagnostic.Location.GetLineSpan().StartLinePosition.Line))
                .IsEquivalentTo([11, 14]);
            await Assert.That(result.GeneratedSources).IsEmpty();
        }
    }

    /// <summary>
    /// Verifies a narrowing chain and equivalent diamond choose one deterministic most-specific storage class.
    /// </summary>
    [Test]
    public async Task Should_accept_ordered_chain_and_equivalent_diamond_redeclarations()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/ordered.exp", ORDERED_REDECLARATION_SCHEMA));

        await Assert.That(result.Diagnostics).IsEmpty();
        var errors = result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.Location.SourceTree?.GetText()
                .Lines[diagnostic.Location.GetLineSpan().StartLinePosition.Line].ToString() ?? diagnostic.ToString());
        await Assert.That(string.Join(Environment.NewLine, errors)).IsEmpty();

        var assembly = Emit(result.OutputCompilation);
        var mostSpecialized = Activator.CreateInstance(assembly.GetType(
            "TedToolkit.Step21.Generated.OrderedRedeclaration.MostSpecialized",
            throwOnError: true)!)!;
        var specialized = Activator.CreateInstance(assembly.GetType(
            "TedToolkit.Step21.Generated.OrderedRedeclaration.Specialized",
            throwOnError: true)!)!;
        var leafType = assembly.GetType(
            "TedToolkit.Step21.Generated.OrderedRedeclaration.Leaf",
            throwOnError: true)!;
        var diamondType = assembly.GetType(
            "TedToolkit.Step21.Generated.OrderedRedeclaration.DiamondLeaf",
            throwOnError: true)!;
        var rootInterface = assembly.GetType(
            "TedToolkit.Step21.Generated.OrderedRedeclaration.IRoot",
            throwOnError: true)!;
        var middleInterface = assembly.GetType(
            "TedToolkit.Step21.Generated.OrderedRedeclaration.IMiddle",
            throwOnError: true)!;
        var leaf = leafType.GetConstructors().Single().Invoke([mostSpecialized]);
        var diamond = diamondType.GetConstructors().Single().Invoke([specialized]);

        using (Assert.Multiple())
        {
            await Assert.That(leafType.GetProperty("Link")!.PropertyType.Name).IsEqualTo("IMostSpecialized");
            await Assert.That(rootInterface.GetProperty("Link")!.GetValue(leaf))
                .IsSameReferenceAs(mostSpecialized);
            await Assert.That(middleInterface.GetProperty("Link")!.GetValue(leaf))
                .IsSameReferenceAs(mostSpecialized);
            await Assert.That(rootInterface.GetProperty("Link")!.GetValue(diamond))
                .IsSameReferenceAs(specialized);
            await Assert.That(diamondType.GetProperties().Count(property => property.Name == "Link"))
                .IsEqualTo(1);
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
            await Assert.That(leafType.GetProperties().Select(property => property.Name)
                .SequenceEqual(["FirstBaseName", "SecondBaseName", "Code", "DirectReferences"])).IsTrue();
            await Assert.That(derivedType.GetProperties().Select(property => property.Name)
                .SequenceEqual(["FirstBaseName", "SecondBaseName", "Code", "Enabled", "DirectReferences"])).IsTrue();
            await Assert.That(leafType.GetConstructors().Single().GetParameters()
                .Select(parameter => parameter.Name ?? string.Empty)
                .SequenceEqual(["firstBaseName", "secondBaseName", "code"])).IsTrue();
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

    private static object ReadSelectedEntity(Type selectType, object select, string methodName)
    {
        object?[] arguments = [null];
        var selected = (bool)selectType.GetMethod(methodName)!.Invoke(select, arguments)!;
        return selected
            ? arguments[0]!
            : throw new InvalidOperationException($"The expected SELECT alternative '{methodName}' was not active.");
    }

    private static string CreateExchange(string schema, string entities)
    {
        return $"""
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('specialization'),'3;1');
            FILE_NAME('specialization.p21','2026-08-27T00:00:00',('Author'),('Org'),'Pre','System','Auth');
            FILE_SCHEMA(('{schema}'));
            ENDSEC;
            DATA;
            {entities}
            ENDSEC;
            END-ISO-10303-21;
            """;
    }

    private static string[] CreateValidSpecializationParameters()
    {
        return
        [
            "#2",
            "#2",
            "18446744073709551616000000000000000001",
            "1.234567890123456789E-17",
            "(#2,#2)",
            "(#2)",
            "(#2,#2)",
            "(#2)",
            "#2",
        ];
    }

    private static string CreateSpecializationExchange(IReadOnlyList<string> parameters)
    {
        return CreateExchange(
            "SPECIALIZATION_ROUND_TRIP",
            $"#1=TARGET('broad');\r\n#2=SPECIALIZED('narrow');\r\n#3=CHILD({string.Join(",", parameters)});");
    }

    private static string CreateAggregateSelectExchange(
        string array,
        string list,
        string bag,
        string set)
    {
        return CreateExchange(
            "AGGREGATE_SELECT_REDECLARATION",
            $"#1=FIRST();\r\n#2=SECOND();\r\n#3=OTHER();\r\n#4=CHILD({array},{list},{bag},{set});");
    }

    private static Exception CaptureSpecializationReadFailure(
        SchemaDescriptor descriptor,
        IReadOnlyList<string> parameters)
    {
        try
        {
            _ = ExchangeStructure.Read(
                new StringReader(CreateSpecializationExchange(parameters)),
                [descriptor]);
        }
        catch (ExchangeStructureBindingException exception)
        {
            return exception;
        }
        catch (ExchangeStructureReadValidationException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("The broad-only fixture unexpectedly published an exchange structure.");
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
