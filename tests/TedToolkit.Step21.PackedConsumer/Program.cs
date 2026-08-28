// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Numerics;

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
        #5=SPECIALIZATION_CHILD(#4,#4,18446744073709551616000000000000000001,1.234567890123456789E-17,(#4,#4),(#4),(#4,#4),(#4),#4,(#4,$),(#4),(#4,#4),(#4));
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
        var selectArrayView = broadSpecialization.SelectArray;
        var selectListView = broadSpecialization.SelectList;
        var selectBagView = broadSpecialization.SelectBag;
        var selectSetView = broadSpecialization.SelectSet;

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
            || !ReferenceEquals(specialization.OptionalLink, broadSpecialization.OptionalLink)
            || !selectArrayView.IsSet(1)
            || selectArrayView.IsSet(2)
            || !selectArrayView[1].TryGetSpecializedTarget(out var arrayTarget)
            || !ReferenceEquals(arrayTarget, specializedTarget)
            || selectListView.Count != 1
            || !ReferenceEquals(selectListView[0], specializedTarget)
            || selectBagView.Count != 2
            || selectBagView.Any(item => !item.TryGetSpecializedTarget(out var itemTarget)
                || !ReferenceEquals(itemTarget, specializedTarget))
            || selectSetView.Count != 1
            || !ReferenceEquals(selectSetView.Single(), specializedTarget))
        {
            return 10;
        }

        target.Code = "peer-edited";
        simple.Name = "after";
        specializedTarget.Code = "narrow-edited";
        specialization.BagValue.Add(specializedTarget);
        specialization.SelectArray[2] = EqualChoice.FromSpecializedTarget(specializedTarget);
        specialization.SelectList.Add(NarrowChoice.FromSpecializedTarget(specializedTarget));
        specialization.SelectBag.Add(EqualChoice.FromSpecializedTarget(specializedTarget));
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
            || !text.Contains("(#4,#4,#4)", StringComparison.Ordinal)
            || !text.Contains("#4,(#4,#4),(#4,#4),(#4,#4,#4),(#4));", StringComparison.Ordinal))
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
            || !rereadSpecialization.SetValue.Contains(rereadSpecializedTarget)
            || !rereadBroadSpecialization.SelectArray.IsSet(1)
            || !rereadBroadSpecialization.SelectArray.IsSet(2)
            || rereadBroadSpecialization.SelectArray.Any(item =>
                !item.TryGetSpecializedTarget(out var itemTarget)
                || !ReferenceEquals(itemTarget, rereadSpecializedTarget))
            || rereadBroadSpecialization.SelectList.Count != 2
            || rereadBroadSpecialization.SelectList.Any(item => !ReferenceEquals(item, rereadSpecializedTarget))
            || rereadBroadSpecialization.SelectBag.Count != 3
            || rereadBroadSpecialization.SelectBag.Any(item =>
                !item.TryGetSpecializedTarget(out var itemTarget)
                || !ReferenceEquals(itemTarget, rereadSpecializedTarget))
            || rereadBroadSpecialization.SelectSet.Count != 1
            || rereadBroadSpecialization.SelectSet.Any(item => !ReferenceEquals(item, rereadSpecializedTarget)))
        {
            return 13;
        }

        if (!VerifyAggregateSelectAtomicity(descriptors, structure, specialization))
        {
            return 14;
        }

        if (!VerifySingularInverse())
        {
            return 15;
        }

        Console.WriteLine("PACKED_AOT_OK");
        return 0;
    }

    private static bool VerifyAggregateSelectAtomicity(
        IReadOnlyList<SchemaDescriptor> descriptors,
        ExchangeStructure structure,
        SpecializationChild specialization)
    {
        var invalidSource = SOURCE.Replace(
            "#4,(#4,$),(#4),(#4,#4),(#4)",
            "#4,(#2,$),(#4),(#4,#4),(#4)",
            StringComparison.Ordinal);
        try
        {
            _ = ExchangeStructure.Read(new StringReader(invalidSource), descriptors);
            return false;
        }
        catch (Exception exception) when (exception is ExchangeStructureBindingException
            or ExchangeStructureReadValidationException)
        {
        }

        specialization.SelectList.Clear();
        var output = new StringWriter();
        try
        {
            structure.Write(output);
        }
        catch (ExchangeStructureWriteValidationException)
        {
            return output.ToString().Length == 0;
        }

        return false;
    }

    private static bool VerifySingularInverse()
    {
        var unique = CreateCatalogStructure();
        var uniqueTarget = new InverseTarget(BigInteger.One);
        _ = unique.Add(unique.DataSections[0], uniqueTarget);
        _ = unique.Add(
            unique.DataSections[0],
            new InverseOwner(
                BigInteger.One,
                new ExpressList<IInverseTarget>(0) { uniqueTarget, uniqueTarget }));
        if (!unique.Validate().IsValid)
        {
            return false;
        }

        var uniqueOutput = new StringWriter();
        unique.Write(uniqueOutput);
        var uniqueText = uniqueOutput.ToString();
        if (!uniqueText.Contains("#1=INVERSE_TARGET(1);", StringComparison.Ordinal)
            || !uniqueText.Contains("#2=INVERSE_OWNER(1,(#1,#1));", StringComparison.Ordinal)
            || !ExchangeStructure.Read(
                    new StringReader(uniqueText),
                    [TedToolkit.Step21.Generated.CatalogModel.SchemaDescriptor.Instance])
                .Validate()
                .IsValid)
        {
            return false;
        }

        var zero = CreateCatalogStructure();
        _ = zero.Add(zero.DataSections[0], new InverseTarget(BigInteger.One));
        var explicitFailure = zero.Validate();
        if (explicitFailure.Failures.Count != 1
            || explicitFailure.Failures[0].Code
                != "CATALOG_MODEL.INVERSE_TARGET.SINGLE_OWNER.INVERSE_CARDINALITY"
            || explicitFailure.Failures[0].Path != "DataSections[0].#1.SingleOwner"
            || !explicitFailure.Failures[0].Message.Contains("CATALOG_MODEL.INVERSE_OWNER.TARGETS", StringComparison.Ordinal)
            || !explicitFailure.Failures[0].Message.Contains("0", StringComparison.Ordinal))
        {
            return false;
        }

        var many = CreateCatalogStructure();
        var manyTarget = new InverseTarget(BigInteger.One);
        _ = many.Add(many.DataSections[0], manyTarget);
        _ = many.Add(
            many.DataSections[0],
            new InverseOwner(BigInteger.One, new ExpressList<IInverseTarget>(0) { manyTarget }));
        _ = many.Add(
            many.DataSections[0],
            new InverseOwner(BigInteger.One, new ExpressList<IInverseTarget>(0) { manyTarget }));
        var manyFailure = many.Validate();
        if (manyFailure.Failures.Count != 1
            || !manyFailure.Failures[0].Message.Contains("2", StringComparison.Ordinal))
        {
            return false;
        }

        var lazy = CreateCatalogStructure();
        _ = lazy.Add(lazy.DataSections[0], new InverseLazyTarget());
        var lazyFailure = lazy.Validate();
        if (lazyFailure.Failures.Count != 1
            || lazyFailure.Failures[0].Code != "CATALOG_MODEL.INVERSE_LAZY_TARGET.WHERE.FALSE_SHORT_CIRCUIT")
        {
            return false;
        }

        var output = new StringWriter();
        ValidationResult writeFailure;
        try
        {
            zero.Write(output);
            return false;
        }
        catch (ExchangeStructureWriteValidationException exception)
        {
            writeFailure = exception.ValidationResult;
        }

        if (output.ToString().Length != 0
            || !FailureEvidence(writeFailure.Failures[0]).Equals(
                FailureEvidence(explicitFailure.Failures[0]),
                StringComparison.Ordinal))
        {
            return false;
        }

        const string invalidSource = """
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('singular inverse'),'3;1');
            FILE_NAME('singular.step','2026-08-27T00:00:00',(),(),'tests','tests','');
            FILE_SCHEMA(('catalog_model'));
            ENDSEC;
            DATA;
            #1=INVERSE_TARGET(1);
            ENDSEC;
            END-ISO-10303-21;
            """;
        try
        {
            _ = ExchangeStructure.Read(
                new StringReader(invalidSource),
                [TedToolkit.Step21.Generated.CatalogModel.SchemaDescriptor.Instance]);
            return false;
        }
        catch (ExchangeStructureReadValidationException exception)
        {
            return exception.ValidationResult.Failures.Count == 1
                && FailureEvidence(exception.ValidationResult.Failures[0]).Equals(
                    FailureEvidence(explicitFailure.Failures[0]),
                    StringComparison.Ordinal);
        }
    }

    private static ExchangeStructure CreateCatalogStructure()
    {
        var structure = new ExchangeStructure(
            new HeaderSection(
                new FileDescription(["singular inverse"], "3;1"),
                new FileName("singular.step", "2026-08-27T00:00:00+08:00", [], [], "tests", "tests", ""),
                new FileSchema(["catalog_model"])),
            [TedToolkit.Step21.Generated.CatalogModel.SchemaDescriptor.Instance]);
        structure.DataSections.Add(new DataSection(new SchemaName("catalog_model")));
        return structure;
    }

    private static string FailureEvidence(ValidationFailure failure)
    {
        return $"{failure.Code}|{failure.Path}|{failure.Message}|"
            + $"{failure.SourceLocation?.FilePath}|{failure.SourceLocation?.Line}|{failure.SourceLocation?.Column}";
    }
}
