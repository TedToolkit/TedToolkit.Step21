// -----------------------------------------------------------------------
// <copyright file="Ap214FixtureMigration.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.PackedConsumer;

internal static class Ap214FixtureMigration
{
    internal static string Apply(string source)
    {
        const string sourceApplication = "'automotive_design',2000,#2";
        const string baselineApplication = "'AUTOMOTIVE_DESIGN_LF',2007,#2";
        const string sourceEnding = "#170 = PRODUCT_RELATED_PRODUCT_CATEGORY('part',$,(#7));\nENDSEC;\nEND-ISO-10303-21;";
        const string baselineEnding = """
            #170 = PRODUCT_RELATED_PRODUCT_CATEGORY('part',$,(#7));
            #171 = ORGANIZATION($,'TedToolkit AP214 fixture owner',$);
            #172 = ORGANIZATION_ROLE('id owner');
            #173 = APPLIED_ORGANIZATION_ASSIGNMENT(#171,#172,(#7));
            ENDSEC;
            END-ISO-10303-21;
            """;

        if (CountOccurrences(source, sourceApplication) != 1
            || CountOccurrences(source, sourceEnding) != 1)
        {
            throw new InvalidOperationException("The fixed AP214IS fixture no longer matches its approved migration input.");
        }

        return source
            .Replace(sourceApplication, baselineApplication, StringComparison.Ordinal)
            .Replace(sourceEnding, baselineEnding, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }
}