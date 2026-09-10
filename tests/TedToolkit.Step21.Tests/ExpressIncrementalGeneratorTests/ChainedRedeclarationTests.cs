// -----------------------------------------------------------------------
// <copyright file="ChainedRedeclarationTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>Preserves the physical slot when a redeclaration qualifies an intermediate owner.</summary>
internal sealed class ChainedRedeclarationTests
{
    /// <summary>All inherited views must observe the same most-specific mutable slot.</summary>
    [Test]
    public async Task Should_resolve_redeclarations_through_an_intermediate_owner()
    {
        var result = GeneratorHostTests.Run("""
            using TedToolkit.Step21.Schemas.Chained;
            using TedToolkit.Step21;
            using System.IO;
            using System.Linq;
            internal static class ChainedConsumer
            {
                internal static bool Check()
                {
                    var first = new LeafItem();
                    var second = new LeafItem();
                    var owner = new LeafOwner(first);
                    IBaseOwner root = owner;
                    IMiddleOwner middle = owner;
                    if (!object.ReferenceEquals(root.Item, first) || !object.ReferenceEquals(middle.Item, first)) return false;
                    owner.Item = second;
                    if (!object.ReferenceEquals(root.Item, second) || !object.ReferenceEquals(middle.Item, second)) return false;
                    var structure = ExchangeStructure.Read(new StringReader(
                        "ISO-10303-21;HEADER;FILE_DESCRIPTION(('chain'),'3;1');" +
                        "FILE_NAME('chain','2026-09-04T00:00:00',('A'),('O'),'P','S','');" +
                        "FILE_SCHEMA(('chained'));ENDSEC;DATA;#1=LEAF_ITEM();#2=LEAF_OWNER(#1);ENDSEC;END-ISO-10303-21;"),
                        [TedToolkit.Step21.Schemas.Chained.SchemaDescriptor.Instance]);
                    var readOwner = structure.Entities.OfType<LeafOwner>().Single();
                    if (!object.ReferenceEquals(((IBaseOwner)readOwner).Item, ((IMiddleOwner)readOwner).Item)) return false;
                    var output = new StringWriter();
                    structure.Write(output);
                    var reread = ExchangeStructure.Read(new StringReader(output.ToString()),
                        [TedToolkit.Step21.Schemas.Chained.SchemaDescriptor.Instance]);
                    var rereadOwner = reread.Entities.OfType<LeafOwner>().Single();
                    var rereadItem = reread.Entities.OfType<LeafItem>().Single();
                    return reread.Validate().IsValid && reread.Entities.Count() == 2 &&
                        object.ReferenceEquals(((IBaseOwner)rereadOwner).Item, rereadItem) &&
                        object.ReferenceEquals(((IMiddleOwner)rereadOwner).Item, rereadItem);
                }
            }
            """, ("chained.exp", """
                SCHEMA chained;
                ENTITY item; END_ENTITY;
                ENTITY middle_item SUBTYPE OF (item); END_ENTITY;
                ENTITY leaf_item SUBTYPE OF (middle_item); END_ENTITY;
                ENTITY base_owner;
                  item : item;
                END_ENTITY;
                ENTITY middle_owner SUBTYPE OF (base_owner);
                  SELF\base_owner.item : middle_item;
                END_ENTITY;
                ENTITY leaf_owner SUBTYPE OF (middle_owner);
                  SELF\middle_owner.item : leaf_item;
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
        var check = assembly.GetType("ChainedConsumer", throwOnError: true)!
            .GetMethod("Check", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        await Assert.That((bool)check.Invoke(null, null)!).IsTrue();
    }
}
