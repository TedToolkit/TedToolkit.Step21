// -----------------------------------------------------------------------
// <copyright file="ListProcedureTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>Executes EXPRESS list procedures without changing the source value or bypassing schema validation.</summary>
internal sealed class ListProcedureTests
{
    /// <summary>Insertion is after the EXPRESS position, removal is one-based, and function-local copies are independent.</summary>
    [Test]
    [Arguments(9, true, false)]
    [Arguments(8, false, false)]
    [Arguments(9, true, true)]
    [Arguments(8, false, true)]
    public async Task Should_execute_insert_remove_and_generic_local_copies(int seed, bool expected, bool namedList)
    {
        var result = GeneratorHostTests.Run("""
            using System.IO;
            using System.Linq;
            using TedToolkit.Step21;
            internal static class ListConsumer
            {
                internal static bool Check(int seed, bool expected)
                {
                    var input = "ISO-10303-21;HEADER;FILE_DESCRIPTION(('lists'),'3;1');" +
                        "FILE_NAME('lists','2026-09-05T00:00:00',('A'),('O'),'P','S','');" +
                        "FILE_SCHEMA(('list_procedures'));ENDSEC;DATA;#1=SAMPLE(" + seed +
                        ");ENDSEC;END-ISO-10303-21;";
                    var descriptor = TedToolkit.Step21.Schemas.ListProcedures.SchemaDescriptor.Instance;
                    try
                    {
                        var structure = ExchangeStructure.Read(new StringReader(input), [descriptor]);
                        var output = new StringWriter();
                        structure.Write(output);
                        return expected && ExchangeStructure.Read(new StringReader(output.ToString()), [descriptor]).Validate().IsValid;
                    }
                    catch (ExchangeStructureReadValidationException failure)
                    {
                        return !expected && failure.ValidationResult.Failures.Any(item => item.Code.EndsWith(".LISTS"));
                    }
                }
            }
            """, ("list-procedures.exp", $$"""
                SCHEMA list_procedures;
                CONSTANT empty_list : integer_list := []; END_CONSTANT;
                TYPE integer_list = LIST OF INTEGER; END_TYPE;
                TYPE value_choice = SELECT (value_item); END_TYPE;
                ENTITY value_item;
                  amount : INTEGER;
                END_ENTITY;
                FUNCTION insert_selected(seed : INTEGER) : LOGICAL;
                LOCAL
                  item : value_item := value_item(seed);
                  selected : value_choice := item;
                  items : LIST OF value_item := [];
                  choices : LIST OF value_choice := [];
                END_LOCAL;
                  INSERT(items, selected, 0);
                  INSERT(choices, item, 0);
                  RETURN((items[1] :=: item) AND (choices[1] :=: selected));
                END_FUNCTION;
                FUNCTION make_list(seed : INTEGER) : integer_list;
                LOCAL items : integer_list := empty_list; END_LOCAL;
                  INSERT(items, seed, 0);
                  RETURN(items);
                END_FUNCTION;
                FUNCTION maybe_integer(present : BOOLEAN) : INTEGER;
                  IF present THEN RETURN(1); END_IF;
                  RETURN(?);
                END_FUNCTION;
                FUNCTION unknown_element(present : BOOLEAN) : INTEGER;
                LOCAL items : LIST OF INTEGER := []; END_LOCAL;
                  INSERT(items, maybe_integer(present), 0);
                  RETURN(SIZEOF(items));
                END_FUNCTION;
                FUNCTION unknown_position(present : BOOLEAN) : INTEGER;
                LOCAL items : LIST OF INTEGER := [1]; END_LOCAL;
                  REMOVE(items, maybe_integer(present));
                  RETURN(SIZEOF(items));
                END_FUNCTION;
                FUNCTION trim_first(source : LIST OF GENERIC : GEN) : LIST OF GENERIC : GEN;
                LOCAL working : LIST OF GENERIC : GEN := source; END_LOCAL;
                  REMOVE(working, 1);
                  RETURN(working);
                END_FUNCTION;
                FUNCTION trim_parameter(source : LIST OF INTEGER) : BOOLEAN;
                  REMOVE(source, 1);
                  RETURN(source = [2,3]);
                END_FUNCTION;
                FUNCTION exercise(seed : INTEGER) : LOGICAL;
                CONSTANT original : {{(namedList ? "integer_list" : "LIST [3:3] OF INTEGER")}} := [1,2,3]; END_CONSTANT;
                LOCAL
                  items : {{(namedList ? "integer_list" : "LIST OF INTEGER")}} := original;
                  reals : LIST OF REAL := [];
                END_LOCAL;
                  INSERT(items, seed, 0);
                  INSERT(items, 8, 2);
                  INSERT(items, 7, SIZEOF(items));
                  REMOVE(items, 2);
                  REMOVE(items, SIZEOF(items));
                  INSERT(reals, 1, 0);
                  RETURN((items = [9,8,2,3]) AND (original = [1,2,3]) AND (items[1] = seed)
                    AND (trim_first(items) = [8,2,3]) AND (items = [9,8,2,3]) AND (reals = [1.0])
                    AND trim_parameter(original) AND (original = [1,2,3])
                    AND (unknown_element(TRUE) = 1) AND NOT EXISTS(unknown_element(FALSE))
                    AND (unknown_position(TRUE) = 0) AND NOT EXISTS(unknown_position(FALSE))
                    AND (make_list(seed) = [seed]) AND (SIZEOF(empty_list) = 0)
                    AND insert_selected(seed));
                END_FUNCTION;
                ENTITY sample;
                  seed : INTEGER;
                WHERE lists : exercise(seed);
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
        var check = assembly.GetType("ListConsumer", throwOnError: true)!
            .GetMethod("Check", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        await Assert.That((bool)check.Invoke(null, [seed, expected])!).IsTrue();
    }

    /// <summary>A VAR actual parameter cannot provide mutable access to a constant.</summary>
    [Test]
    [Arguments("INSERT(items, 2, 0);")]
    [Arguments("REMOVE(items, 1);")]
    public async Task Should_reject_list_constant_mutation(string statement)
    {
        var result = GeneratorHostTests.Run(("constant-list.exp", $$"""
            SCHEMA constant_list;
            FUNCTION inspect_list(seed : INTEGER) : BOOLEAN;
            CONSTANT items : LIST OF INTEGER := [1]; END_CONSTANT;
              {{statement}}
              RETURN(SIZEOF(items) = seed);
            END_FUNCTION;
            ENTITY sample;
            WHERE valid : inspect_list(1);
            END_ENTITY;
            END_SCHEMA;
            """));
        await Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Id == "STEP21EXP002"
            && diagnostic.GetMessage().Contains("EXPRESS-BIND-CONSTANT-ASSIGNMENT", StringComparison.Ordinal))).IsTrue();
        await Assert.That(result.GeneratedSources).IsEmpty();
    }

    /// <summary>Attribute, group, and index qualifiers retain the mutable entity selected by a parameter.</summary>
    [Test]
    public async Task Should_assign_through_parameter_qualifiers()
    {
        var result = GeneratorHostTests.Run("""
            using System.IO;
            using System.Linq;
            using TedToolkit.Step21;
            internal static class QualifiedParameterConsumer
            {
                internal static bool Check()
                {
                    const string input = "ISO-10303-21;HEADER;FILE_DESCRIPTION(('qualified'),'3;1');" +
                        "FILE_NAME('qualified','2026-09-05T00:00:00',('A'),('O'),'P','S','');" +
                        "FILE_SCHEMA(('qualified_parameter'));ENDSEC;DATA;#1=CHILD_ITEM(1,(2));" +
                        "ENDSEC;END-ISO-10303-21;";
                    var descriptor = TedToolkit.Step21.Schemas.QualifiedParameter.SchemaDescriptor.Instance;
                    var structure = ExchangeStructure.Read(new StringReader(input), [descriptor]);
                    return structure.Validate().IsValid;
                }
            }
            """, ("qualified-parameter.exp", """
                SCHEMA qualified_parameter;
                ENTITY root_item SUPERTYPE OF (child_item);
                  amount : INTEGER;
                END_ENTITY;
                ENTITY child_item SUBTYPE OF (root_item);
                  values : LIST [1:1] OF INTEGER;
                WHERE updated : rewrite(SELF).amount = 7;
                END_ENTITY;
                FUNCTION rewrite(item : root_item) : root_item;
                  item.amount := 7;
                  IF 'QUALIFIED_PARAMETER.CHILD_ITEM' IN TYPEOF(item) THEN
                    item\child_item.values[1] := 9;
                  END_IF;
                  RETURN(item);
                END_FUNCTION;
                END_SCHEMA;
                """));
        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning).ToArray();
        await Assert.That(diagnostics).IsEmpty().Because(string.Join(Environment.NewLine, diagnostics));
        using var stream = new MemoryStream();
        var emitted = result.OutputCompilation.Emit(stream);
        await Assert.That(emitted.Success).IsTrue().Because(string.Join(Environment.NewLine, emitted.Diagnostics));
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        var check = assembly.GetType("QualifiedParameterConsumer", throwOnError: true)!
            .GetMethod("Check", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        await Assert.That((bool)check.Invoke(null, null)!).IsTrue();
    }

    /// <summary>Nested procedures update VAR arguments, call siblings, and return only from their own invocation.</summary>
    [Test]
    public async Task Should_execute_nested_procedures_with_var_parameters()
    {
        var result = GeneratorHostTests.Run("""
            using System.IO;
            using TedToolkit.Step21;
            internal static class NestedProcedureConsumer
            {
                internal static bool Check()
                {
                    const string input = "ISO-10303-21;HEADER;FILE_DESCRIPTION(('procedure'),'3;1');" +
                        "FILE_NAME('procedure','2026-09-05T00:00:00',('A'),('O'),'P','S','');" +
                        "FILE_SCHEMA(('nested_procedure'));ENDSEC;DATA;#1=SAMPLE(2);" +
                        "ENDSEC;END-ISO-10303-21;";
                    var descriptor = TedToolkit.Step21.Schemas.NestedProcedure.SchemaDescriptor.Instance;
                    return ExchangeStructure.Read(new StringReader(input), [descriptor]).Validate().IsValid;
                }
            }
            """, ("nested-procedure.exp", """
            SCHEMA nested_procedure;
            FUNCTION adjust(seed : INTEGER) : INTEGER;
            FUNCTION doubled(current : INTEGER) : INTEGER;
              RETURN(current * 2);
            END_FUNCTION;
            PROCEDURE normalize(VAR current : INTEGER);
              IF current > 5 THEN
                current := 5;
              END_IF;
            END_PROCEDURE;
            PROCEDURE bump(step : INTEGER; VAR current : INTEGER);
              current := doubled(current) + step;
              normalize(current);
              IF current = 5 THEN
                RETURN;
              END_IF;
              current := 0;
            END_PROCEDURE;
            LOCAL result : INTEGER := seed; END_LOCAL;
              bump(1, result);
              RETURN(result);
            END_FUNCTION;
            ENTITY sample;
              seed : INTEGER;
            WHERE adjusted : adjust(seed) = 5;
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
        var check = assembly.GetType("NestedProcedureConsumer", throwOnError: true)!
            .GetMethod("Check", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        await Assert.That((bool)check.Invoke(null, null)!).IsTrue();
    }
}
