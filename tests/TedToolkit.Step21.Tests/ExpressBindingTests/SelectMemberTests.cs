// -----------------------------------------------------------------------
// <copyright file="SelectMemberTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Analyzer.Express.Binding;
using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressBindingTests;

/// <summary>Protects member binding across independently declared SELECT alternatives.</summary>
internal sealed class SelectMemberTests
{
    /// <summary>One entity containing both slots remains ambiguous even when their types match.</summary>
    [Test]
    [Arguments("REAL")]
    [Arguments("label")]
    public async Task Should_reject_unqualified_independent_inherited_slots(string memberType)
    {
        var source = $$"""
            SCHEMA ambiguous_slots;
            TYPE label = STRING;
            END_TYPE;
            ENTITY left_owner;
              amount : {{memberType}};
            END_ENTITY;
            ENTITY right_owner;
              amount : {{memberType}};
            END_ENTITY;
            ENTITY combined SUBTYPE OF (left_owner, right_owner);
            END_ENTITY;
            FUNCTION has_amount(candidate : combined) : BOOLEAN;
              RETURN(EXISTS(candidate.amount));
            END_FUNCTION;
            END_SCHEMA;
            """;
        var result = ExpressSchemaCompiler.Compile([new ExpressSchemaSource("ambiguous-slots.exp", source)]);
        using (Assert.Multiple())
        {
            await Assert.That(result.SyntaxDiagnostics).IsEmpty();
            await Assert.That(result.Schemas).IsEmpty();
            await Assert.That(result.BindingDiagnostics.Select(diagnostic => diagnostic.Code))
                .IsEquivalentTo(["EXPRESS-BIND-UNRESOLVED-MEMBER"]);
        }
    }

    /// <summary>Equivalent scalar declarations share a result type without sharing syntax objects.</summary>
    [Test]
    public async Task Should_bind_equal_scalar_members_from_distinct_select_alternatives()
    {
        var source = new ExpressSchemaSource("intervals.exp", """
                SCHEMA intervals;
                ENTITY bounded_interval;
                  lower : REAL;
                  upper : REAL;
                END_ENTITY;
                ENTITY lower_interval;
                  lower : REAL;
                END_ENTITY;
                ENTITY upper_interval;
                  upper : REAL;
                END_ENTITY;
                TYPE interval = SELECT (bounded_interval, lower_interval, upper_interval);
                END_TYPE;
                FUNCTION width(candidate : interval) : REAL;
                  RETURN(candidate.upper - candidate.lower);
                END_FUNCTION;
                ENTITY sample;
                  candidate : interval;
                WHERE
                  positive_width : width(candidate) > 0.0;
                END_ENTITY;
                ENTITY unknown_sample;
                  candidate : interval;
                WHERE
                  missing_width : NOT EXISTS(width(candidate));
                END_ENTITY;
                END_SCHEMA;
                """);
        var result = ExpressSchemaCompiler.Compile([source]);

        using (Assert.Multiple())
        {
            await Assert.That(result.SyntaxDiagnostics).IsEmpty();
            await Assert.That(result.BindingDiagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, result.BindingDiagnostics.Select(diagnostic => diagnostic.Message)));
        }

        var references = result.Schemas.Single().NameReferences
            .Where(reference => reference.Target.Name is "lower" or "upper").ToArray();
        using (Assert.Multiple())
        {
            await Assert.That(references.Length).IsEqualTo(2);
            await Assert.That(references.All(reference => reference.Target.AttributeCandidates.Count == 2)).IsTrue();
            await Assert.That(references.All(reference => reference.Target.Type is ExpressBoundScalarType
            { Kind: ExpressScalarKind.Real, })).IsTrue();
        }

        var generated = GeneratorHostTests.Run("""
            using TedToolkit.Step21;
            using TedToolkit.Step21.Generated.Intervals;
            internal static class IntervalConsumer
            {
                internal static ValidationResult Validate(int alternative)
                {
                    var structure = new ExchangeStructure(
                        new HeaderSection(
                            new FileDescription(["intervals"], "3;1"),
                            new FileName("intervals.step", "2026-09-04T00:00:00+08:00", [""], [""], "tests", "tests", ""),
                            new FileSchema(["intervals"])),
                        [TedToolkit.Step21.Generated.Intervals.SchemaDescriptor.Instance]);
                    var section = new DataSection(new SchemaName("intervals"));
                    structure.DataSections.Add(section);
                    Interval interval;
                    if (alternative == 2)
                    {
                        var lower = new LowerInterval(new RealValue(0, 0));
                        _ = structure.Add(section, lower);
                        interval = Interval.FromLowerInterval(lower);
                    }
                    else
                    {
                        var bounded = new BoundedInterval(
                            new RealValue(0, 0), new RealValue(alternative == 0 ? 10 : -10, 0));
                        _ = structure.Add(section, bounded);
                        interval = Interval.FromBoundedInterval(bounded);
                    }
                    if (alternative == 2)
                    {
                        _ = structure.Add(section, new UnknownSample(interval));
                    }
                    else
                    {
                        _ = structure.Add(section, new Sample(interval));
                    }
                    return structure.Validate();
                }
            }
            """, (source.FilePath, source.Text));
        var diagnostics = generated.Diagnostics.Concat(generated.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning).ToArray();
        await Assert.That(diagnostics).IsEmpty().Because(string.Join(Environment.NewLine, diagnostics));
        using var stream = new MemoryStream();
        var emitted = generated.OutputCompilation.Emit(stream);
        await Assert.That(emitted.Success).IsTrue().Because(string.Join(Environment.NewLine, emitted.Diagnostics));
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        var validate = assembly.GetType("IntervalConsumer", throwOnError: true)!
            .GetMethod("Validate", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        foreach (var alternative in new[] { 0, 1, 2 })
        {
            var validation = (ValidationResult)validate.Invoke(null, [alternative])!;
            await Assert.That(validation.IsValid).IsEqualTo(alternative != 1)
                .Because($"alternative {alternative}: " + string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
        }
    }
}