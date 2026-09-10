// -----------------------------------------------------------------------
// <copyright file="SelectAggregateMemberTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>Retains a shared inherited aggregate slot and branch-specific domain refinements.</summary>
internal sealed class SelectAggregateMemberTests
{
    /// <summary>Unguarded access uses the inherited slot while TYPEOF guards retain the narrower member.</summary>
    [Test]
    public async Task Should_access_common_aggregate_slots_and_preserve_guarded_specialization()
    {
        var result = GeneratorHostTests.Run("""
            using TedToolkit.Step21;
            using TedToolkit.Step21.Schemas.AggregateMember;
            internal static class AggregateMemberConsumer
            {
                internal static ValidationResult Validate(int scenario)
                {
                    var structure = new ExchangeStructure(
                        new HeaderSection(
                            new FileDescription(["aggregates"], "3;1"),
                            new FileName("aggregates.step", "2026-09-04T00:00:00+08:00", [""], [""], "tests", "tests", ""),
                            new FileSchema(["aggregate_member"])),
                        [TedToolkit.Step21.Schemas.AggregateMember.SchemaDescriptor.Instance]);
                    var section = new DataSection(new SchemaName("aggregate_member"));
                    structure.DataSections.Add(section);
                    var direct = new DirectItem();
                    var other = new OtherItem();
                    var directItems = new ExpressSet<IDirectItem>(0);
                    if (scenario != 1) directItems.Add(direct);
                    var directRep = new DirectRep(directItems);
                    var otherRep = new OtherRep(new ExpressSet<IOtherItem>(0) { other });
                    foreach (var entity in new Entity[] { direct, other, directRep, otherRep })
                    {
                        _ = structure.Add(section, entity);
                    }
                    var selected = scenario < 2 ? RepChoice.FromDirectRep(directRep) : RepChoice.FromOtherRep(otherRep);
                    IItem expected = scenario == 2 ? other : direct;
                    _ = structure.Add(section, new Sample(selected, expected));
                    return structure.Validate();
                }
            }
            """, ("aggregate-member.exp", """
                SCHEMA aggregate_member;
                ENTITY item; END_ENTITY;
                ENTITY direct_item SUBTYPE OF (item); END_ENTITY;
                ENTITY other_item SUBTYPE OF (item); END_ENTITY;
                ENTITY representation;
                  items : SET [0:?] OF item;
                END_ENTITY;
                ENTITY direct_rep SUBTYPE OF (representation);
                  SELF\representation.items : SET [0:?] OF direct_item;
                END_ENTITY;
                ENTITY other_rep SUBTYPE OF (representation);
                  SELF\representation.items : SET [0:?] OF other_item;
                END_ENTITY;
                TYPE rep_choice = SELECT (direct_rep, other_rep);
                END_TYPE;
                FUNCTION count_direct(values : SET [0:?] OF direct_item) : INTEGER;
                  RETURN(SIZEOF(values));
                END_FUNCTION;
                ENTITY sample;
                  selected : rep_choice;
                  expected : item;
                WHERE
                  membership : expected IN selected.items;
                  narrowed : NOT ('AGGREGATE_MEMBER.DIRECT_REP' IN TYPEOF(selected)) OR
                             (count_direct(selected.items) > 0);
                END_ENTITY;
                END_SCHEMA;
                """));
        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning).ToArray();
        await Assert.That(diagnostics).IsEmpty().Because(string.Join(Environment.NewLine, diagnostics));
        using var stream = new MemoryStream();
        var emitted = result.OutputCompilation.Emit(stream);
        await Assert.That(emitted.Success).IsTrue().Because(string.Join(Environment.NewLine, emitted.Diagnostics));
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        var validate = assembly.GetType("AggregateMemberConsumer", throwOnError: true)!
            .GetMethod("Validate", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        for (var scenario = 0; scenario < 4; scenario++)
        {
            var validation = (ValidationResult)validate.Invoke(null, [scenario])!;
            await Assert.That(validation.IsValid).IsEqualTo(scenario is 0 or 2)
                .Because($"scenario {scenario}: " + string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
        }
    }
}
