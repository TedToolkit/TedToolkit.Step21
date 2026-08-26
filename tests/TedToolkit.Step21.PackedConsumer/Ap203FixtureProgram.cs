// -----------------------------------------------------------------------
// <copyright file="Ap203FixtureProgram.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21.Generated.ConfigControlDesign;

using Ap203SchemaDescriptor = TedToolkit.Step21.Generated.ConfigControlDesign.SchemaDescriptor;

namespace TedToolkit.Step21.PackedConsumer;

internal static class Ap203FixtureProgram
{
    private static int Main(string[] arguments)
    {
        if (arguments.Length != 2)
            return 64;

        ExchangeStructure? structure = null;
        try
        {
            using var source = File.OpenText(arguments[0]);
            structure = ExchangeStructure.Read(source, [Ap203SchemaDescriptor.Instance]);
        }
        catch (ExchangeStructureBindingException exception)
        {
            foreach (var diagnostic in exception.Diagnostics)
            {
                Console.Error.WriteLine(
                    $"{diagnostic.Code} {diagnostic.SourceLocation?.Line}:{diagnostic.SourceLocation?.Column} "
                    + diagnostic.Message);
            }

            throw;
        }
        catch (ExchangeStructureReadValidationException exception)
        {
            foreach (var failure in exception.ValidationResult.Failures)
            {
                Console.Error.WriteLine($"{failure.Code} {failure.Path} {failure.Message}");
            }

            throw;
        }

        var entities = structure.Entities.ToArray();
        var product = entities.OfType<IProduct>().Single();
        var shapeDefinition = entities.OfType<IShapeDefinitionRepresentation>().Single();
        var representation = entities.OfType<IAdvancedBrepShapeRepresentation>().Single();
        var solid = entities.OfType<IManifoldSolidBrep>().Single();
        var faces = entities.OfType<IAdvancedFace>().ToArray();
        var edgeCurves = entities.OfType<IEdgeCurve>().ToArray();
        var vertices = entities.OfType<IVertexPoint>().ToArray();
        var points = entities.OfType<ICartesianPoint>().ToArray();
        var units = entities.OfType<ISiUnit>().ToArray();

        var representationContract = (IRepresentation)representation;
        if (entities.Length != 200
            || product.Name.Value != "TedToolkit AP203 OCCT box 10x20x30 mm 1"
            || faces.Length != 6
            || edgeCurves.Length != 12
            || vertices.Length != 8
            || points.Length != 27
            || units.Length != 3
            || !ReferenceEquals(shapeDefinition.UsedRepresentation, representation)
            || !representationContract.Items.Any(item => ReferenceEquals(item, solid))
            || units.Count(unit => unit.Name.Equals(SiUnitName.Metre)) != 1
            || units.Count(unit => unit.Name.Equals(SiUnitName.Radian)) != 1
            || units.Count(unit => unit.Name.Equals(SiUnitName.Steradian)) != 1)
        {
            return 20;
        }

        Console.WriteLine(
            $"AP203_FIXTURE_OK entities={entities.Length} products=1 faces={faces.Length} "
            + $"edges={edgeCurves.Length} vertices={vertices.Length} points={points.Length} units={units.Length}");

        ExchangeStructure? unsupported = null;
        try
        {
            using var source = File.OpenText(arguments[1]);
            unsupported = ExchangeStructure.Read(source, [Ap203SchemaDescriptor.Instance]);
            return 30;
        }
        catch (ExchangeStructureBindingException exception)
        {
            var diagnostic = exception.Diagnostics.Single();
            if (unsupported is not null
                || diagnostic.Code != "P21-BIND-ENTITY"
                || diagnostic.SourceLocation is null
                || diagnostic.SourceLocation.Line != 8
                || diagnostic.SourceLocation.Column != 6)
            {
                return 31;
            }

            Console.WriteLine(
                $"AP203_EXTENSION_REJECTED code={diagnostic.Code} "
                + $"line={diagnostic.SourceLocation.Line} column={diagnostic.SourceLocation.Column}");
        }

        return 0;
    }
}
