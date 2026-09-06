// -----------------------------------------------------------------------
// <copyright file="LocalConstantTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>Executes lexical constants through the same typed initialization and validation path as function locals.</summary>
internal sealed class LocalConstantTests
{
    /// <summary>A constant remains immutable when addressed directly or through an aggregate index.</summary>
    [Test]
    [Arguments("minimum := 2;")]
    [Arguments("letters[1] := 'z';")]
    public async Task Should_reject_assignment_to_local_constants(string assignment)
    {
        var result = GeneratorHostTests.Run(("constant-assignment.exp", $$"""
            SCHEMA constant_assignment;
            FUNCTION check_constant : LOGICAL;
            CONSTANT
              minimum : INTEGER := 1;
              letters : LIST [1:1] OF STRING := ['a'];
            END_CONSTANT;
              {{assignment}}
              RETURN((minimum = 1) AND (letters[1] = 'a'));
            END_FUNCTION;
            ENTITY sample;
            WHERE valid : check_constant;
            END_ENTITY;
            END_SCHEMA;
            """));
        var diagnostic = result.Diagnostics.Single();
        await Assert.That(diagnostic.Id).IsEqualTo("STEP21EXP002");
        await Assert.That(diagnostic.GetMessage()).Contains("EXPRESS-BIND-CONSTANT-ASSIGNMENT");
        await Assert.That(result.GeneratedSources).IsEmpty();
    }

    /// <summary>Scalar, named, aggregate, and indeterminate constants participate in reachable function evaluation.</summary>
    [Test]
    [Arguments("a", true)]
    [Arguments("b", true)]
    [Arguments("z", false)]
    public async Task Should_evaluate_function_constants_before_locals_and_preserve_unknown_values(string value, bool expected)
    {
        var result = GeneratorHostTests.Run("""
            using System.IO;
            using System.Linq;
            using TedToolkit.Step21;
            using TedToolkit.Step21.Generated.LocalConstants;
            internal static class ConstantConsumer
            {
                internal static bool Check(string value, bool expected)
                {
                    var input = "ISO-10303-21;HEADER;FILE_DESCRIPTION(('constants'),'3;1');" +
                        "FILE_NAME('constants','2026-09-05T00:00:00',('A'),('O'),'P','S','');" +
                        "FILE_SCHEMA(('local_constants'));ENDSEC;DATA;#1=SAMPLE('" + value +
                        "');ENDSEC;END-ISO-10303-21;";
                    var descriptor = TedToolkit.Step21.Generated.LocalConstants.SchemaDescriptor.Instance;
                    try
                    {
                        var structure = ExchangeStructure.Read(new StringReader(input), [descriptor]);
                        var output = new StringWriter();
                        structure.Write(output);
                        var reread = ExchangeStructure.Read(new StringReader(output.ToString()), [descriptor]);
                        return expected && reread.Validate().IsValid && reread.Entities.OfType<Sample>().Single().Text == value;
                    }
                    catch (ExchangeStructureReadValidationException failure)
                    {
                        return !expected && failure.ValidationResult.Failures.Any(item => item.Code.EndsWith(".ALLOWED"));
                    }
                }
            }
            """, ("local-constants.exp", """
                SCHEMA local_constants;
                TYPE label = STRING; END_TYPE;
                FUNCTION accepts(input_text : STRING) : LOGICAL;
                CONSTANT
                  letters : SET [2:2] OF STRING := ['a', 'b'];
                  first_letter : label := 'a';
                  minimum : INTEGER := 1;
                  missing : REAL := ?;
                  sequence : LIST [1:1] OF STRING := ['a'];
                END_CONSTANT;
                LOCAL
                  minimum_copy : INTEGER := minimum;
                  sequence_copy : LIST [1:1] OF STRING := sequence;
                  missing_copy : REAL := missing;
                END_LOCAL;
                  sequence_copy[1] := 'z';
                  RETURN((input_text IN letters) AND (LENGTH(first_letter) = minimum_copy) AND NOT EXISTS(missing)
                    AND NOT EXISTS(missing_copy) AND (sequence[1] = 'a') AND (sequence_copy[1] = 'z'));
                END_FUNCTION;
                ENTITY sample;
                  text : STRING;
                WHERE allowed : accepts(text);
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
        var check = assembly.GetType("ConstantConsumer", throwOnError: true)!
            .GetMethod("Check", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        await Assert.That((bool)check.Invoke(null, [value, expected])!).IsTrue();
    }
}