// -----------------------------------------------------------------------
// <copyright file="Ap203Ap214Program.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21;

using Ap203 = TedToolkit.Step21.Generated.ConfigControlDesign;
using Ap214 = TedToolkit.Step21.Generated.AutomotiveDesign;

namespace TedToolkit.Step21.PackedConsumer;

internal static class Ap203Ap214Program
{
    private static int Main(string[] arguments)
    {
        if (arguments.Length != 2)
        {
            return 64;
        }

        var ap203Descriptor = Ap203.SchemaDescriptor.Instance;
        var ap214Descriptor = Ap214.SchemaDescriptor.Instance;
        SchemaDescriptor[] descriptors = [ap203Descriptor, ap214Descriptor,];

        if (ap203Descriptor.Name.Value != "config_control_design"
            || ap214Descriptor.Name.Value != "AUTOMOTIVE_DESIGN"
            || ReferenceEquals(ap203Descriptor, ap214Descriptor)
            || ap203Descriptor.GetType().Assembly == ap214Descriptor.GetType().Assembly
            || ap203Descriptor.GetType().BaseType?.Assembly != typeof(ExchangeStructure).Assembly
            || ap214Descriptor.GetType().BaseType?.Assembly != typeof(ExchangeStructure).Assembly)
        {
            return 10;
        }

        ExchangeStructure ap203Structure;
        using (var source = File.OpenText(arguments[0]))
        {
            ap203Structure = ExchangeStructure.Read(source, descriptors);
        }

        var ap214Text = Ap214FixtureMigration.Apply(File.ReadAllText(arguments[1]));
        ExchangeStructure ap214Structure;
        using (var source = new StringReader(ap214Text))
        {
            ap214Structure = ExchangeStructure.Read(source, descriptors);
        }

        if (ap203Structure.Entities.Count() != 200
            || ap203Structure.Entities.OfType<Ap203.Product>().Count() != 1
            || ap214Structure.Entities.Count() != 173
            || ap214Structure.Entities.OfType<Ap214.Product>().Count() != 1)
        {
            return 11;
        }

        Console.WriteLine(
            "PACKED_AP203_AP214_OK ap203-entities=200 ap214-entities=173 "
            + "runtime-assemblies=1 schema-assemblies=2");
        return 0;
    }
}
