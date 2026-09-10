// -----------------------------------------------------------------------
// <copyright file="CombinedRepeatTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>Executes combined EXPRESS REPEAT controls through generated schema validation.</summary>
internal sealed class CombinedRepeatTests
{
    /// <summary>Entry bounds, signed increments, and pre/post conditions retain their distinct evaluation order.</summary>
    [Test]
    [Arguments("1,5,1,.T.,3", "WHILE enabled UNTIL index >= stop", 3)]
    [Arguments("1,5,1,.F.,3", "WHILE enabled UNTIL index >= stop", 0)]
    [Arguments("1,5,1,.U.,3", "WHILE enabled UNTIL index >= stop", 0)]
    [Arguments("1,5,1,.T.,3", "WHILE index < stop", 2)]
    [Arguments("1,5,1,.T.,0", "UNTIL index >= stop", 1)]
    [Arguments("5,1,-2,.T.,3", "WHILE enabled UNTIL index <= stop", 2)]
    [Arguments("1,5,-1,.T.,3", "WHILE enabled UNTIL index >= stop", 0)]
    [Arguments("5,1,1,.T.,3", "WHILE enabled UNTIL index >= stop", 0)]
    [Arguments("1,5,0,.T.,0", "WHILE enabled UNTIL index >= stop", 0)]
    [Arguments("$,5,1,.T.,3", "WHILE enabled UNTIL index >= stop", 0)]
    [Arguments("1,$,1,.T.,3", "WHILE enabled UNTIL index >= stop", 0)]
    [Arguments("1,5,$,.T.,3", "WHILE enabled UNTIL index >= stop", 0)]
    [Arguments("1,3,1,.U.,3", "UNTIL enabled", 3)]
    [Arguments("1.,2.,0.5,.T.,3", "WHILE enabled", 3)]
    [Arguments("2.,1.,-0.5,.T.,3", "WHILE enabled", 3)]
    [Arguments("1,5,1,.T.,2", "WHILE total < stop", 2)]
    [Arguments("1,5,1,.T.,2", "UNTIL total = stop", 2)]
    public async Task Should_preserve_combined_control_semantics(string arguments, string control, int expected)
    {
        var inputs = arguments.Split(',');
        await CheckValidation($$"""
            SCHEMA combined_repeat;
            FUNCTION control_value(input_number : NUMBER; present : BOOLEAN) : NUMBER;
              IF present THEN RETURN(input_number); END_IF;
              RETURN(?);
            END_FUNCTION;
            FUNCTION tally(lower, upper, stride : NUMBER; enabled : LOGICAL; stop : INTEGER) : INTEGER;
            LOCAL total : INTEGER := 0; END_LOCAL;
              REPEAT index := control_value(lower, {{(inputs[0] == "$" ? "FALSE" : "TRUE")}})
                TO control_value(upper, {{(inputs[1] == "$" ? "FALSE" : "TRUE")}})
                BY control_value(stride, {{(inputs[2] == "$" ? "FALSE" : "TRUE")}}) {{control}};
                total := total + 1;
              END_REPEAT;
              RETURN(total);
            END_FUNCTION;
            ENTITY sample;
              lower, upper, stride : NUMBER;
              enabled : LOGICAL;
              stop : INTEGER;
              expected : INTEGER;
            WHERE result_matches : tally(lower, upper, stride, enabled, stop) = expected;
            END_ENTITY;
            END_SCHEMA;
            """, arguments.Replace("$", "1", StringComparison.Ordinal), expected);
    }

    /// <summary>Changing the expressions' source locals inside the body does not change the established sequence.</summary>
    [Test]
    [Arguments("upper := 1;")]
    [Arguments("stride := 2;")]
    public async Task Should_evaluate_increment_bounds_and_step_once(string mutation)
    {
        await CheckValidation($$"""
            SCHEMA combined_repeat;
            FUNCTION tally(seed : INTEGER) : INTEGER;
            LOCAL
              upper : INTEGER := 3;
              stride : INTEGER := 1;
              total : INTEGER := seed;
            END_LOCAL;
              REPEAT index := 1 TO upper BY stride;
                total := total + 1;
                {{mutation}}
              END_REPEAT;
              RETURN(total);
            END_FUNCTION;
            ENTITY sample;
              expected : INTEGER;
            WHERE result_matches : tally(0) = expected;
            END_ENTITY;
            END_SCHEMA;
            """, "", 3);
    }

    /// <summary>Loop transfers respect nesting and evaluate UNTIL after SKIP, but not after ESCAPE.</summary>
    [Test]
    [Arguments("index := 1 TO 3", "total := total + 1; ESCAPE; total := total + 100;", 1)]
    [Arguments("index := 1 TO 3", "total := total + 1; SKIP; total := total + 100;", 3)]
    [Arguments("index := 1 TO 5 UNTIL total = 2", "total := total + 1; SKIP;", 2)]
    [Arguments("WHILE total < 5 UNTIL total = 2", "total := total + 1; SKIP;", 2)]
    [Arguments("UNTIL total = 2", "total := total + 1; IF total = 5 THEN ESCAPE; END_IF; SKIP;", 2)]
    [Arguments("index := 1 TO 3", "IF index = 2 THEN ESCAPE; END_IF; total := total + 1;", 1)]
    [Arguments("index := 1 TO 3", "CASE index OF 2: ESCAPE; OTHERWISE: total := total + 1; END_CASE;", 1)]
    [Arguments("index := 1 TO 3", "REPEAT inner := 1 TO 3; total := total + 1; ESCAPE; END_REPEAT;", 3)]
    [Arguments("index := 1 TO 3", "REPEAT inner := 1 TO 3; total := total + 1; SKIP; END_REPEAT;", 9)]
    [Arguments("index := 1 TO 3", "total := total + 1; IF index = 1 THEN SKIP; ELSE ESCAPE; END_IF;", 2)]
    public async Task Should_preserve_loop_transfer_semantics(string control, string body, int expected)
    {
        await CheckValidation($$"""
            SCHEMA combined_repeat;
            FUNCTION tally(seed : INTEGER) : INTEGER;
            LOCAL total : INTEGER := seed; END_LOCAL;
              REPEAT {{control}};
                {{body}}
              END_REPEAT;
              RETURN(total);
            END_FUNCTION;
            ENTITY sample;
              expected : INTEGER;
            WHERE result_matches : tally(0) = expected;
            END_ENTITY;
            END_SCHEMA;
            """, "", expected);
    }

    /// <summary>Transferred paths participate in UNTIL and post-loop presence proofs.</summary>
    [Test]
    [Arguments("SKIP", 0)]
    [Arguments("ESCAPE", 1)]
    public async Task Should_merge_transfer_path_determinacy(string transfer, int expected)
    {
        await CheckValidation($$"""
            SCHEMA combined_repeat;
            FUNCTION initial_value(seed : INTEGER) : INTEGER;
              IF seed = 0 THEN RETURN(1); END_IF;
              RETURN(?);
            END_FUNCTION;
            FUNCTION tally(seed : INTEGER) : INTEGER;
            LOCAL current_value : INTEGER := initial_value(seed); END_LOCAL;
              REPEAT index := 1 TO 2 UNTIL current_value = 1;
                IF index = 1 THEN
                  {{transfer}};
                END_IF;
                current_value := 1;
              END_REPEAT;
              IF EXISTS(current_value) THEN RETURN(0); END_IF;
              RETURN(1);
            END_FUNCTION;
            ENTITY sample;
              expected : INTEGER;
            WHERE result_matches : tally(1) = expected;
            END_ENTITY;
            END_SCHEMA;
            """, "", expected);
    }

    /// <summary>A transfer outside REPEAT is withheld, not emitted as an invalid C# jump.</summary>
    [Test]
    [Arguments("SKIP")]
    [Arguments("ESCAPE")]
    public async Task Should_reject_transfer_outside_repeat(string transfer)
    {
        var result = GeneratorHostTests.Run(("invalid-transfer.exp", $$"""
            SCHEMA invalid_transfer;
            FUNCTION invalid(seed : INTEGER) : INTEGER;
              {{transfer}};
              RETURN(seed);
            END_FUNCTION;
            ENTITY sample;
              seed : INTEGER;
            WHERE valid : invalid(seed) = seed;
            END_ENTITY;
            END_SCHEMA;
            """));
        await Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Id == "STEP21EXP006"
            && diagnostic.Location.GetLineSpan().Path == "invalid-transfer.exp")).IsTrue();
        await Assert.That(result.GeneratedSources).IsEmpty();
    }

    private static async Task CheckValidation(string schema, string arguments, int expected)
    {
        var result = GeneratorHostTests.Run("""
            using System.IO;
            using System.Linq;
            using TedToolkit.Step21;
            internal static class RepeatConsumer
            {
                internal static bool Check(string arguments, int expected, bool valid)
                {
                    var input = "ISO-10303-21;HEADER;FILE_DESCRIPTION(('repeat'),'3;1');" +
                        "FILE_NAME('repeat','2026-09-05T00:00:00',('A'),('O'),'P','S','');" +
                        "FILE_SCHEMA(('combined_repeat'));ENDSEC;DATA;#1=SAMPLE(" +
                        (arguments.Length == 0 ? "" : arguments + ",") + expected +
                        ");ENDSEC;END-ISO-10303-21;";
                    var descriptor = TedToolkit.Step21.Schemas.CombinedRepeat.SchemaDescriptor.Instance;
                    try
                    {
                        var structure = ExchangeStructure.Read(new StringReader(input), [descriptor]);
                        var output = new StringWriter();
                        structure.Write(output);
                        return valid && ExchangeStructure.Read(new StringReader(output.ToString()), [descriptor]).Validate().IsValid;
                    }
                    catch (ExchangeStructureReadValidationException failure)
                    {
                        return !valid && failure.ValidationResult.Failures.Any(item => item.Code.EndsWith(".RESULT_MATCHES"));
                    }
                }
            }
            """, ("combined-repeat.exp", schema));
        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning).ToArray();
        await Assert.That(diagnostics).IsEmpty().Because(string.Join(Environment.NewLine, diagnostics));
        using var stream = new MemoryStream();
        var emitted = result.OutputCompilation.Emit(stream);
        await Assert.That(emitted.Success).IsTrue().Because(string.Join(Environment.NewLine, emitted.Diagnostics));
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        var check = assembly.GetType("RepeatConsumer", throwOnError: true)!
            .GetMethod("Check", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        await Assert.That((bool)check.Invoke(null, [arguments, expected, true])!).IsTrue();
        await Assert.That((bool)check.Invoke(null, [arguments, expected + 1, false])!).IsTrue();
    }
}
