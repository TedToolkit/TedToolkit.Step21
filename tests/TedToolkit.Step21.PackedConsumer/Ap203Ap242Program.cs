// -----------------------------------------------------------------------
// <copyright file="Ap203Ap242Program.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21;

using Ap203 = TedToolkit.Step21.Generated.ConfigControlDesign;
using Ap242 = TedToolkit.Step21.Generated.Ap242ManagedModelBased3dEngineeringMimLf;

namespace TedToolkit.Step21.PackedConsumer;

internal static class Ap203Ap242Program
{
    private static int Main(string[] arguments)
    {
        if (arguments.Length != 2)
        {
            return 64;
        }

        var ap203Descriptor = Ap203.SchemaDescriptor.Instance;
        var ap242Descriptor = Ap242.SchemaDescriptor.Instance;
        SchemaDescriptor[] descriptors = [ap203Descriptor, ap242Descriptor,];

        if (ap203Descriptor.Name.Value != "config_control_design"
            || ap242Descriptor.Name.Value != "Ap242_managed_model_based_3d_engineering_mim_lf"
            || ReferenceEquals(ap203Descriptor, ap242Descriptor)
            || ap203Descriptor.GetType().Assembly == ap242Descriptor.GetType().Assembly
            || ap203Descriptor.GetType().BaseType?.Assembly != typeof(ExchangeStructure).Assembly
            || ap242Descriptor.GetType().BaseType?.Assembly != typeof(ExchangeStructure).Assembly)
        {
            return 10;
        }

        ExchangeStructure ap203Structure;
        using (var source = File.OpenText(arguments[0]))
        {
            ap203Structure = ExchangeStructure.Read(source, descriptors);
        }

        ExchangeStructure ap242Structure;
        using (var source = File.OpenText(arguments[1]))
        {
            ap242Structure = ExchangeStructure.Read(source, descriptors);
        }

        if (ap203Structure.Entities.Count() != 200
            || ap203Structure.Entities.OfType<Ap203.Product>().Count() != 1
            || ap242Structure.Entities.Count() != 170
            || ap242Structure.Entities.OfType<Ap242.Product>().Count() != 1)
        {
            return 11;
        }

        Console.WriteLine(
            "PACKED_AP203_AP242_OK ap203-entities=200 ap242-entities=170 "
            + "runtime-assemblies=1 schema-assemblies=2");
        return 0;
    }
}
