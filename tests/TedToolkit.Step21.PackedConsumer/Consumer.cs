// -----------------------------------------------------------------------
// <copyright file="Consumer.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21;

namespace TedToolkit.Step21.PackedConsumer;

internal static class Consumer
{
    internal static SchemaName CreateSchemaName()
    {
        return new SchemaName("lunar_catalog");
    }
}
