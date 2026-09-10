// -----------------------------------------------------------------------
// <copyright file="Ap203FixtureProgram.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21.Schemas.Ap203ConfigurationControlled3dDesignOfMechanicalPartsAndAssembliesMimLf;

using Ap203SchemaDescriptor = TedToolkit.Step21.Schemas.Ap203ConfigurationControlled3dDesignOfMechanicalPartsAndAssembliesMimLf.SchemaDescriptor;

namespace TedToolkit.Step21.PackedConsumer;

internal static class Ap203FixtureProgram
{
    private const string EditedProductName = "TedToolkit AP203 OCCT box 10x20x30 mm - edited";

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
        var product = entities.OfType<Product>().Single();
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

        product.Name = new Label(EditedProductName);
        var editedValidation = structure.Validate();
        if (!editedValidation.IsValid)
        {
            return 21;
        }

        var expectedSignature = CreateSemanticSignature(structure);
        var serialized = new StringWriter();
        structure.Write(serialized);
        ExchangeStructure reread;
        using (var source = new StringReader(serialized.ToString()))
        {
            reread = ExchangeStructure.Read(source, [Ap203SchemaDescriptor.Instance]);
        }

        var actualSignature = CreateSemanticSignature(reread);
        if (!StringComparer.Ordinal.Equals(expectedSignature, actualSignature)
            || reread.Entities.OfType<IProduct>().Single().Name.Value != EditedProductName)
        {
            return 22;
        }

        Console.WriteLine(
            "AP203_ROUND_TRIP_OK edit=product.name entities=200 faces=6 edges=12 "
            + "vertices=8 points=27 units=metre,radian,steradian shared-vertex-degrees=3,3,3,3,3,3,3,3");

        var invalidProduct = reread.Entities.OfType<Product>().Single();
        var invalidFace = reread.Entities.OfType<AdvancedFace>().First();
        invalidProduct.FrameOfReference.Clear();
        invalidFace.Bounds.Clear();
        var invalidValidation = reread.Validate();
        var expectedProductFailure = invalidValidation.Failures.Any(failure =>
            failure.Code == "AP203_CONFIGURATION_CONTROLLED_3D_DESIGN_OF_MECHANICAL_PARTS_AND_ASSEMBLIES_MIM_LF.PRODUCT.FRAME_OF_REFERENCE.AGGREGATE_0.LOWER_BOUND"
            && failure.Path.EndsWith(".FrameOfReference", StringComparison.Ordinal));
        var expectedFaceFailure = invalidValidation.Failures.Any(failure =>
            failure.Code == "AP203_CONFIGURATION_CONTROLLED_3D_DESIGN_OF_MECHANICAL_PARTS_AND_ASSEMBLIES_MIM_LF.FACE.BOUNDS.AGGREGATE_0.LOWER_BOUND"
            && failure.Path.EndsWith(".Bounds", StringComparison.Ordinal));
        if (invalidValidation.IsValid || !expectedProductFailure || !expectedFaceFailure)
        {
            return 23;
        }

        var rejectedOutput = new StringWriter();
        try
        {
            reread.Write(rejectedOutput);
            return 24;
        }
        catch (ExchangeStructureWriteValidationException exception)
        {
            var validationEvidence = invalidValidation.Failures.Select(FailureEvidence);
            var writeEvidence = exception.ValidationResult.Failures.Select(FailureEvidence);
            if (!validationEvidence.SequenceEqual(writeEvidence)
                || rejectedOutput.GetStringBuilder().Length != 0)
            {
                return 25;
            }
        }

        Console.WriteLine(
            $"AP203_INVALID_EDIT_REJECTED failures={invalidValidation.Failures.Count} output-bytes=0");

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

    private static string CreateSemanticSignature(ExchangeStructure structure)
    {
        var entities = structure.Entities.ToArray();
        var product = entities.OfType<IProduct>().Single();
        var shapeDefinition = entities.OfType<IShapeDefinitionRepresentation>().Single();
        var representation = entities.OfType<IAdvancedBrepShapeRepresentation>().Single();
        var solid = entities.OfType<IManifoldSolidBrep>().Single();
        var faces = entities.OfType<IAdvancedFace>().ToArray();
        var edges = entities.OfType<IEdgeCurve>().ToArray();
        var vertices = entities.OfType<IVertexPoint>().ToArray();
        var points = entities.OfType<ICartesianPoint>().ToArray();
        var units = entities.OfType<ISiUnit>().ToArray();
        var coordinates = points
            .Select(point => string.Join(",", point.Coordinates.Select(value => value.Value.ToString())))
            .OrderBy(value => value, StringComparer.Ordinal);
        var unitNames = units.Select(unit => unit.Name.Value).OrderBy(value => value, StringComparer.Ordinal);
        var vertexDegrees = vertices
            .Select(vertex => edges.Count(edge =>
                ReferenceEquals(edge.EdgeStart, vertex) || ReferenceEquals(edge.EdgeEnd, vertex)))
            .OrderBy(value => value);
        var representationContract = (IRepresentation)representation;

        return string.Join(
            "|",
            $"entities={entities.Length}",
            $"product={product.Name.Value}",
            $"faces={faces.Length}",
            $"edges={edges.Length}",
            $"vertices={vertices.Length}",
            $"points={points.Length}:{string.Join(";", coordinates)}",
            $"units={string.Join(",", unitNames)}",
            $"vertex-degrees={string.Join(",", vertexDegrees)}",
            $"shape-reference={ReferenceEquals(shapeDefinition.UsedRepresentation, representation)}",
            $"solid-reference={representationContract.Items.Any(item => ReferenceEquals(item, solid))}");
    }

    private static string FailureEvidence(ValidationFailure failure) => $"{failure.Code}|{failure.Path}";
}
