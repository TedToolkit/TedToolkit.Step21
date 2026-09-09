// -----------------------------------------------------------------------
// <copyright file="NamedAliasRedeclarationTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>Retains nominal alias constraints when narrowing inherited physical attributes.</summary>
internal sealed class NamedAliasRedeclarationTests
{
    /// <summary>Parent getters unwrap named aliases without discarding validation at the stored domain.</summary>
    [Test]
    public async Task Should_project_named_alias_redeclarations_and_validate_the_narrowed_domain()
    {
        var result = GeneratorHostTests.Run("""
            using TedToolkit.Step21;
            using TedToolkit.Step21.Generated.AliasRedeclaration;
            internal static class AliasConsumer
            {
                internal static bool Check(int scenario)
                {
                    var code = new NarrowCode(new Code(scenario == 1 ? "" : "ok"));
                    var element = new NarrowCode(new Code(scenario == 2 ? "" : "ok"));
                    var codes = new ExpressList<NarrowCode>(0) { element };
                    var owner = new Child(code, codes);
                    IRoot root = owner;
                    if (root.Code.Value != code.Value.Value) return false;
                    if (root.Codes.Count != 1 || root.Codes[0].Value != element.Value.Value) return false;
                    codes.Add(new NarrowCode(new Code("second")));
                    if (root.Codes.Count != 2 || root.Codes[1].Value != "second") return false;
                    var structure = new ExchangeStructure(
                        new HeaderSection(new FileDescription(["alias"], "3;1"),
                            new FileName("alias", "2026-09-04T00:00:00", [""], [""], "tests", "tests", ""),
                            new FileSchema(["alias_redeclaration"])),
                        [TedToolkit.Step21.Generated.AliasRedeclaration.SchemaDescriptor.Instance]);
                    var section = new DataSection(new SchemaName("alias_redeclaration"));
                    structure.DataSections.Add(section);
                    _ = structure.Add(section, owner);
                    return structure.Validate().IsValid == (scenario == 0);
                }
            }
            """, ("alias.exp", """
                SCHEMA alias_redeclaration;
                TYPE code = STRING; END_TYPE;
                TYPE narrow_code = code;
                WHERE nonempty : LENGTH(SELF) > 0;
                END_TYPE;
                ENTITY root ABSTRACT;
                  code : code;
                  codes : LIST [0:?] OF code;
                END_ENTITY;
                ENTITY child SUBTYPE OF (root);
                  SELF\root.code : narrow_code;
                  SELF\root.codes : LIST [0:?] OF narrow_code;
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
        var check = assembly.GetType("AliasConsumer", throwOnError: true)!
            .GetMethod("Check", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        for (var scenario = 0; scenario < 3; scenario++)
        {
            await Assert.That((bool)check.Invoke(null, [scenario])!).IsTrue().Because($"scenario {scenario}");
        }
    }
}