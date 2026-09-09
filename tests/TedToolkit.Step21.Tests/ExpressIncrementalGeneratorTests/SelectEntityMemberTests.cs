// -----------------------------------------------------------------------
// <copyright file="SelectEntityMemberTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>Preserves identity when SELECT members have distinct entity-valued domains.</summary>
internal sealed class SelectEntityMemberTests
{
    /// <summary>Member dispatch must retain both branch types and the original entity references.</summary>
    [Test]
    public async Task Should_compare_heterogeneous_entity_members_by_instance_identity()
    {
        var result = GeneratorHostTests.Run("""
            using TedToolkit.Step21;
            using TedToolkit.Step21.Generated.MemberIdentity;
            internal static class MemberIdentityConsumer
            {
                internal static ValidationResult Validate(int scenario)
                {
                    var structure = new ExchangeStructure(
                        new HeaderSection(
                            new FileDescription(["identity"], "3;1"),
                            new FileName("identity.step", "2026-09-04T00:00:00+08:00", [""], [""], "tests", "tests", ""),
                            new FileSchema(["member_identity"])),
                        [TedToolkit.Step21.Generated.MemberIdentity.SchemaDescriptor.Instance]);
                    var section = new DataSection(new SchemaName("member_identity"));
                    structure.DataSections.Add(section);
                    var contextA = new ContextA("same");
                    var distinctContextA = new ContextA("same");
                    var contextB = new ContextB("same");
                    var firstA = new OwnerA(contextA);
                    var sameA = new OwnerA(contextA);
                    var distinctA = new OwnerA(distinctContextA);
                    var firstB = new OwnerB(contextB);
                    var sameB = new OwnerB(contextB);
                    var missing = new OwnerC();
                    foreach (var entity in new Entity[]
                    {
                        contextA, distinctContextA, contextB, firstA, sameA, distinctA, firstB, sameB, missing
                    })
                    {
                        _ = structure.Add(section, entity);
                    }
                    var left = scenario == 4 ? OwnerChoice.FromOwnerC(missing)
                        : scenario == 2 ? OwnerChoice.FromOwnerB(firstB) : OwnerChoice.FromOwnerA(firstA);
                    var right = scenario switch
                    {
                        0 => OwnerChoice.FromOwnerA(sameA),
                        1 => OwnerChoice.FromOwnerA(distinctA),
                        2 => OwnerChoice.FromOwnerB(sameB),
                        4 => OwnerChoice.FromOwnerC(missing),
                        _ => OwnerChoice.FromOwnerB(firstB)
                    };
                    _ = structure.Add(section, new Sample(left, right));
                    return structure.Validate();
                }
            }
            """, ("member-identity.exp", """
                SCHEMA member_identity;
                ENTITY context_a;
                  label_text : STRING;
                END_ENTITY;
                ENTITY context_b;
                  label_text : STRING;
                END_ENTITY;
                ENTITY owner_a;
                  context_of_items : context_a;
                END_ENTITY;
                ENTITY owner_b;
                  context_of_items : context_b;
                END_ENTITY;
                ENTITY owner_c;
                END_ENTITY;
                TYPE owner_choice = SELECT (owner_a, owner_b, owner_c);
                END_TYPE;
                ENTITY sample;
                  left_owner : owner_choice;
                  right_owner : owner_choice;
                WHERE
                  same_context : left_owner.context_of_items :=: right_owner.context_of_items;
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
        var validate = assembly.GetType("MemberIdentityConsumer", throwOnError: true)!
            .GetMethod("Validate", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        for (var scenario = 0; scenario < 5; scenario++)
        {
            var validation = (ValidationResult)validate.Invoke(null, [scenario])!;
            await Assert.That(validation.IsValid).IsEqualTo(scenario is 0 or 2)
                .Because($"scenario {scenario}: " + string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
        }
    }
}