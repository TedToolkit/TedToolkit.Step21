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
        #4=SPECIALIZED_TARGET('narrow');
        #5=SPECIALIZATION_CHILD(#4,#4,18446744073709551616000000000000000001,1.234567890123456789E-17,(#4,#4),(#4),(#4,#4),(#4),#4);
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
        var specialization = structure.Entities.OfType<SpecializationChild>().Single();
        var specializedTarget = structure.Entities.OfType<SpecializedTarget>().Single();
        ISpecializationRoot broadSpecialization = specialization;
        var narrowSelected = specialization.SelectValue.TryGetSpecializedTarget(out var selectedSpecializedTarget);
        var broadSelected = broadSpecialization.SelectValue.TryGetTarget(out var selectedTarget);

        if (complex is not ILeft { Enabled: true, } left
            || complex is not IRight { Rank: var rank }
            || complex is not IRoot { Label: "complex", Peer: var peer, Values.Count: 2 }
            || rank != 7
            || !ReferenceEquals(peer, target)
            || left.Label != "complex"
            || !ReferenceEquals(specialization.Link, specializedTarget)
            || !ReferenceEquals(broadSpecialization.Link, specializedTarget)
            || !narrowSelected
            || !ReferenceEquals(selectedSpecializedTarget, specializedTarget)
            || !broadSelected
            || !ReferenceEquals(selectedTarget, specializedTarget)
            || specialization.IntegerValue.ToString() != "18446744073709551616000000000000000001"
            || broadSpecialization.IntegerValue != NumberValue.FromInteger(specialization.IntegerValue)
            || broadSpecialization.RealValue != NumberValue.FromReal(specialization.RealValue)
            || !ReferenceEquals(specialization.ArrayValue, broadSpecialization.ArrayValue)
            || !ReferenceEquals(specialization.ListValue, broadSpecialization.ListValue)
            || !ReferenceEquals(specialization.BagValue, broadSpecialization.BagValue)
            || !ReferenceEquals(specialization.SetValue, broadSpecialization.SetValue)
            || !ReferenceEquals(specialization.OptionalLink, broadSpecialization.OptionalLink))
        {
            return 10;
        }

        target.Code = "peer-edited";
        simple.Name = "after";
        specializedTarget.Code = "narrow-edited";
        specialization.BagValue.Add(specializedTarget);
        if (!structure.Validate().IsValid)
        {
            return 11;
        }

        var output = new StringWriter();
        structure.Write(output);
        var text = output.ToString();
        if (!text.Contains("#1=(LEFT(.T.)RIGHT(7)ROOT('complex',#2,(1,2)));", StringComparison.Ordinal)
            || !text.Contains("#2=TARGET('peer-edited');", StringComparison.Ordinal)
            || !text.Contains("#3=SIMPLE('after');", StringComparison.Ordinal)
            || !text.Contains("#4=SPECIALIZED_TARGET('narrow-edited');", StringComparison.Ordinal)
            || !text.Contains("#5=SPECIALIZATION_CHILD(#4,#4,18446744073709551616000000000000000001,", StringComparison.Ordinal)
            || !text.Contains("(#4,#4,#4)", StringComparison.Ordinal))
        {
            return 12;
        }

        var reread = ExchangeStructure.Read(new StringReader(text), descriptors);
        var rereadComplex = reread.Entities.Single(entity => entity is ILeft && entity is IRight);
        var rereadTarget = reread.Entities.OfType<Target>().Single();
        var rereadSimple = reread.Entities.OfType<Simple>().Single();
        var rereadSpecialization = reread.Entities.OfType<SpecializationChild>().Single();
        var rereadSpecializedTarget = reread.Entities.OfType<SpecializedTarget>().Single();
        ISpecializationRoot rereadBroadSpecialization = rereadSpecialization;
        var rereadNarrowSelected = rereadSpecialization.SelectValue.TryGetSpecializedTarget(
            out var rereadSelectedSpecializedTarget);
        var rereadBroadSelected = rereadBroadSpecialization.SelectValue.TryGetTarget(out var rereadSelectedTarget);
        if (!reread.Validate().IsValid
            || reread.Entities.Count() != 5
            || rereadComplex is not ILeft { Enabled: true, }
            || rereadComplex is not IRight { Rank: var rereadRank }
            || rereadComplex is not IRoot { Label: "complex", Peer: var rereadPeer, Values: var rereadValues }
            || rereadRank != 7
            || !rereadValues.SequenceEqual([1, 2])
            || !ReferenceEquals(rereadPeer, rereadTarget)
            || rereadTarget.Code != "peer-edited"
            || rereadSimple.Name != "after"
            || !ReferenceEquals(rereadSpecialization.Link, rereadSpecializedTarget)
            || !ReferenceEquals(rereadBroadSpecialization.Link, rereadSpecializedTarget)
            || !rereadNarrowSelected
            || !ReferenceEquals(rereadSelectedSpecializedTarget, rereadSpecializedTarget)
            || !rereadBroadSelected
            || !ReferenceEquals(rereadSelectedTarget, rereadSpecializedTarget)
            || rereadSpecialization.IntegerValue.ToString() != "18446744073709551616000000000000000001"
            || rereadBroadSpecialization.IntegerValue != NumberValue.FromInteger(
                rereadSpecialization.IntegerValue)
            || rereadBroadSpecialization.RealValue != NumberValue.FromReal(rereadSpecialization.RealValue)
            || !ReferenceEquals(rereadSpecialization.ArrayValue, rereadBroadSpecialization.ArrayValue)
            || !ReferenceEquals(rereadSpecialization.ListValue, rereadBroadSpecialization.ListValue)
            || !ReferenceEquals(rereadSpecialization.BagValue, rereadBroadSpecialization.BagValue)
            || !ReferenceEquals(rereadSpecialization.SetValue, rereadBroadSpecialization.SetValue)
            || !ReferenceEquals(rereadSpecialization.OptionalLink, rereadSpecializedTarget)
            || !ReferenceEquals(rereadBroadSpecialization.OptionalLink, rereadSpecializedTarget)
            || !ReferenceEquals(rereadSpecialization.ArrayValue[1], rereadSpecializedTarget)
            || !ReferenceEquals(rereadSpecialization.ArrayValue[2], rereadSpecializedTarget)
            || rereadSpecializedTarget.Code != "narrow-edited"
            || !rereadSpecialization.ListValue.SequenceEqual([rereadSpecializedTarget])
            || rereadSpecialization.BagValue.Count != 3
            || rereadSpecialization.BagValue.Any(item => !ReferenceEquals(item, rereadSpecializedTarget))
            || rereadSpecialization.SetValue.Count != 1
            || !rereadSpecialization.SetValue.Contains(rereadSpecializedTarget))
        {
            return 13;
        }

        Console.WriteLine("PACKED_AOT_OK");
        return 0;
    }
}