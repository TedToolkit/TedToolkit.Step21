// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21;
using TedToolkit.Step21.Generated.CatalogCore;
using TedToolkit.Step21.Generated.CatalogModel;

namespace TedToolkit.Step21.PackedConsumer;

internal static class Program
{
    private const string SOURCE = """
        ISO-10303-21;
        HEADER;
        FILE_DESCRIPTION(('packed-aot'),'3;1');
        FILE_NAME('packed.p21','2026-08-22T00:00:00',('Author'),('Org'),'Pre','System','Auth');
        FILE_SCHEMA(('catalog_core','catalog_model'));
        FILE_POPULATION('catalog_model','INCLUDE_REFERENCED',('model'));
        ENDSEC;
        DATA('core',('catalog_core'));
        #2=TARGET('peer');
        ENDSEC;
        DATA('model',('catalog_model'));
        #1=(LEFT(.T.)RIGHT(7)ROOT('complex',#2,(1,2)));
        #3=SIMPLE('before');
        ENDSEC;
        END-ISO-10303-21;
        """;

    private static int Main()
    {
        SchemaDescriptor[] descriptors =
        [
            TedToolkit.Step21.Generated.CatalogCore.SchemaDescriptor.Instance,
            TedToolkit.Step21.Generated.CatalogModel.SchemaDescriptor.Instance,
        ];
        var structure = ExchangeStructure.Read(new StringReader(SOURCE), descriptors);
        var complex = structure.Entities.Single(entity => entity is ILeft && entity is IRight);
        var target = structure.Entities.OfType<Target>().Single();
        var simple = structure.Entities.OfType<Simple>().Single();

        if (complex is not ILeft { Enabled: true, } left
            || complex is not IRight { Rank: var rank }
            || complex is not IRoot { Label: "complex", Peer: var peer, Values.Count: 2 }
            || rank != 7
            || !ReferenceEquals(peer, target)
            || left.Label != "complex")
        {
            return 10;
        }

        target.Code = "peer-edited";
        simple.Name = "after";
        if (!structure.Validate().IsValid)
        {
            return 11;
        }

        var output = new StringWriter();
        structure.Write(output);
        var text = output.ToString();
        if (!text.Contains("#1=(LEFT(.T.)RIGHT(7)ROOT('complex',#2,(1,2)));", StringComparison.Ordinal)
            || !text.Contains("#2=TARGET('peer-edited');", StringComparison.Ordinal)
            || !text.Contains("#3=SIMPLE('after');", StringComparison.Ordinal))
        {
            return 12;
        }

        var reread = ExchangeStructure.Read(new StringReader(text), descriptors);
        if (!reread.Validate().IsValid || reread.Entities.Count() != 3)
        {
            return 13;
        }

        Console.WriteLine("PACKED_AOT_OK");
        return 0;
    }
}
