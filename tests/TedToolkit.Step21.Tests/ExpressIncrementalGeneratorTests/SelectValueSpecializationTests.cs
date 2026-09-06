// -----------------------------------------------------------------------
// <copyright file="SelectValueSpecializationTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>Preserves exact nominal SELECT alternatives and narrowed value constraints.</summary>
internal sealed class SelectValueSpecializationTests
{
    /// <summary>Exact alternatives win over alias ancestry, while inherited alternatives remain lossless.</summary>
    [Test]
    public async Task Should_project_named_values_into_selects_without_erasing_the_exact_alternative()
    {
        var result = GeneratorHostTests.Run("""
            using System.IO;
            using System.Linq;
            using TedToolkit.Step21;
            using TedToolkit.Step21.Generated.SelectValues;
            internal static class ValueConsumer
            {
                internal static bool Check(int scenario)
                {
                    var value = new NarrowCode(new Code("ok"));
                    IRoot owner = new Child(value, value);
                    if (!owner.Exact.TryGetNarrowCode(out var exact) || exact.Value.Value != "ok" ||
                        !owner.Inherited.TryGetCode(out var inherited) || inherited.Value != "ok") return false;
                    var first = scenario == 1 ? "''" : "'ok'";
                    var second = scenario == 2 ? "''" : "'ok'";
                    var input = "ISO-10303-21;HEADER;FILE_DESCRIPTION(('values'),'3;1');" +
                        "FILE_NAME('values','2026-09-04T00:00:00',('A'),('O'),'P','S','');" +
                        "FILE_SCHEMA(('select_values'));ENDSEC;DATA;#1=CHILD(" + first + "," + second +
                        ");ENDSEC;END-ISO-10303-21;";
                    try
                    {
                        var structure = ExchangeStructure.Read(new StringReader(input),
                            [TedToolkit.Step21.Generated.SelectValues.SchemaDescriptor.Instance]);
                        var output = new StringWriter();
                        structure.Write(output);
                        var reread = ExchangeStructure.Read(new StringReader(output.ToString()),
                            [TedToolkit.Step21.Generated.SelectValues.SchemaDescriptor.Instance]);
                        IRoot child = reread.Entities.OfType<Child>().Single();
                        return scenario == 0 && reread.Validate().IsValid &&
                            child.Exact.TryGetNarrowCode(out var readExact) && readExact.Value.Value == "ok" &&
                            child.Inherited.TryGetCode(out var readInherited) && readInherited.Value == "ok";
                    }
                    catch (ExchangeStructureReadValidationException failure)
                    {
                        return scenario != 0 && failure.ValidationResult.Failures.Count > 0;
                    }
                }
            }
            """, ("select-values.exp", """
                SCHEMA select_values;
                TYPE code = STRING; END_TYPE;
                TYPE narrow_code = code;
                WHERE nonempty : LENGTH(SELF) > 0;
                END_TYPE;
                TYPE other = INTEGER; END_TYPE;
                TYPE exact_choice = SELECT (code, narrow_code, other); END_TYPE;
                TYPE inherited_choice = SELECT (code, other); END_TYPE;
                ENTITY root ABSTRACT;
                  exact : exact_choice;
                  inherited : inherited_choice;
                END_ENTITY;
                ENTITY child SUBTYPE OF (root);
                  SELF\root.exact : narrow_code;
                  SELF\root.inherited : narrow_code;
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
        var check = assembly.GetType("ValueConsumer", throwOnError: true)!
            .GetMethod("Check", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        for (var scenario = 0; scenario < 3; scenario++)
        {
            await Assert.That((bool)check.Invoke(null, [scenario])!).IsTrue().Because($"scenario {scenario}");
        }
    }
}