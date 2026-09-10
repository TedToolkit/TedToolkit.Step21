// -----------------------------------------------------------------------
// <copyright file="Ap242Program.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21.Schemas.Ap242ManagedModelBased3dEngineeringMimLf;

namespace TedToolkit.Step21.PackedConsumer;

internal static class Ap242Program
{
    private static int Main()
    {
        var descriptor = global::TedToolkit.Step21.Schemas.Ap242ManagedModelBased3dEngineeringMimLf.SchemaDescriptor.Instance;
        var schemaAssembly = descriptor.GetType().Assembly;

        if (descriptor.Name.Value != "Ap242_managed_model_based_3d_engineering_mim_lf"
            || typeof(Product).Assembly != schemaAssembly
            || typeof(IAdvancedFace).Assembly != schemaAssembly)
        {
            return 30;
        }

        Console.WriteLine("PACKED_AP242_OK");
        return 0;
    }
}
