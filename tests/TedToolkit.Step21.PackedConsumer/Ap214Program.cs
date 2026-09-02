// -----------------------------------------------------------------------
// <copyright file="Ap214Program.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21.Generated.AutomotiveDesign;

namespace TedToolkit.Step21.PackedConsumer;

internal static class Ap214Program
{
    private static int Main()
    {
        var descriptor = global::TedToolkit.Step21.Generated.AutomotiveDesign.SchemaDescriptor.Instance;
        var schemaAssembly = descriptor.GetType().Assembly;

        if (descriptor.Name.Value != "AUTOMOTIVE_DESIGN"
            || typeof(Product).Assembly != schemaAssembly
            || typeof(IAdvancedFace).Assembly != schemaAssembly)
        {
            return 30;
        }

        Console.WriteLine("PACKED_AP214_OK");
        return 0;
    }
}
