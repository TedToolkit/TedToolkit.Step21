// -----------------------------------------------------------------------
// <copyright file="Ap203Program.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21.Schemas.ConfigControlDesign;

namespace TedToolkit.Step21.PackedConsumer;

internal static class Ap203Program
{
    private static int Main()
    {
        var descriptor = global::TedToolkit.Step21.Schemas.ConfigControlDesign.SchemaDescriptor.Instance;
        var schemaAssembly = descriptor.GetType().Assembly;

        if (descriptor.Name.Value != "config_control_design"
            || typeof(Product).Assembly != schemaAssembly
            || typeof(IBoundedPcurve).Assembly != schemaAssembly)
        {
            return 20;
        }

        Console.WriteLine("PACKED_AP203_OK");
        return 0;
    }
}
