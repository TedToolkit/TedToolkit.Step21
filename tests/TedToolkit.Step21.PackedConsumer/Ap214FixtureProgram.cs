// -----------------------------------------------------------------------
// <copyright file="Ap214FixtureProgram.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;

using TedToolkit.Step21.Generated.AutomotiveDesign;

using Ap214SchemaDescriptor = TedToolkit.Step21.Generated.AutomotiveDesign.SchemaDescriptor;

namespace TedToolkit.Step21.PackedConsumer;

internal static class Ap214FixtureProgram
{
    private const string EditedProductName = "TedToolkit AP214 OCCT box 10x20x30 mm - edited";

    private const string OriginalProductName = "TedToolkit AP214 OCCT box 10x20x30 mm 1";

    private static int Main(string[] arguments)
    {
        if (arguments.Length == 0)
        {
            var descriptor = Ap214SchemaDescriptor.Instance;
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

        if (arguments.Length != 3)
        {
            return 64;
        }

        var probe = new ProbeTextReader();
        try
        {
            _ = ExchangeStructure.Read(
                probe,
                [Ap214SchemaDescriptor.Instance, Ap214SchemaDescriptor.Instance]);
            return 10;
        }
        catch (ArgumentException exception)
        {
            if (exception.ParamName != "schemaDescriptors" || probe.WasRead)
            {
                return 11;
            }

            Console.WriteLine("AP214_DUPLICATE_DESCRIPTOR_REJECTED input-chars=0");
        }

        var rawFixture = File.ReadAllText(arguments[0]);
        ExchangeStructure? rawStructure = null;
        try
        {
            using var source = new StringReader(rawFixture);
            rawStructure = ExchangeStructure.Read(source, [Ap214SchemaDescriptor.Instance]);
            return 12;
        }
        catch (ExchangeStructureReadValidationException exception)
        {
            var codes = exception.ValidationResult.Failures
                .Select(failure => failure.Code)
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToArray();
            string[] expectedCodes =
            [
                "AUTOMOTIVE_DESIGN.RULE.APPLICATION_PROTOCOL_DEFINITION_REQUIRED.WHERE.WR1",
                "AUTOMOTIVE_DESIGN.RULE.PRODUCT_REQUIRES_ID_OWNER.WHERE.WR1",
            ];
            if (rawStructure is not null || !codes.SequenceEqual(expectedCodes))
            {
                return 13;
            }

            Console.WriteLine(
                "AP214_RAW_EDITION_REJECTED rules=application-protocol-definition-required,product-requires-id-owner "
                + "partial-model=false");
        }

        ExchangeStructure structure;
        var migratedFixture = Ap214FixtureMigration.Apply(rawFixture);
        try
        {
            using var source = new StringReader(migratedFixture);
            structure = ExchangeStructure.Read(source, [Ap214SchemaDescriptor.Instance]);
        }
        catch (ExchangeStructureBindingException exception)
        {
            WriteBindingDiagnostics(exception);
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
        var extents = GetExtents(points);
        var vertexDegrees = GetVertexDegrees(vertices, edgeCurves);
        var representationContract = (IRepresentation)representation;
        var metre = units.Single(unit => unit.Name.Equals(SiUnitName.Metre));
        var radian = units.Single(unit => unit.Name.Equals(SiUnitName.Radian));
        var steradian = units.Single(unit => unit.Name.Equals(SiUnitName.Steradian));

        if (entities.Length != 173
            || product.Name.Value != OriginalProductName
            || faces.Length != 6
            || edgeCurves.Length != 12
            || vertices.Length != 8
            || points.Length != 27
            || units.Length != 3
            || extents != "10x20x30"
            || !vertexDegrees.SequenceEqual([3, 3, 3, 3, 3, 3, 3, 3])
            || !ReferenceEquals(shapeDefinition.UsedRepresentation, representation)
            || !representationContract.Items.Any(item => ReferenceEquals(item, solid))
            || metre.Prefix != SiPrefix.Milli
            || radian.Prefix is not null
            || steradian.Prefix is not null)
        {
            return 20;
        }

        Console.WriteLine(
            $"AP214_FIXTURE_OK entities={entities.Length} products=1 faces={faces.Length} "
            + $"edges={edgeCurves.Length} vertices={vertices.Length} points={points.Length} units={units.Length} "
            + $"extents={extents}");

        product.Name = new Label(EditedProductName);
        var editedValidation = structure.Validate();
        if (!editedValidation.IsValid)
        {
            return 21;
        }

        var expectedSignature = CreateSemanticSignature(structure);
        var serialized = new StringWriter(CultureInfo.InvariantCulture);
        structure.Write(serialized);
        var expectedGraph = Part21FixtureSignature.Create(migratedFixture);
        var writtenGraph = Part21FixtureSignature.Create(serialized.ToString()
            .Replace(EditedProductName, OriginalProductName, StringComparison.Ordinal));
        if (!StringComparer.Ordinal.Equals(expectedGraph, writtenGraph))
        {
            Console.Error.WriteLine("AP214 physical instance/value/reference graph changed during read or write.");
            return 42;
        }

        ExchangeStructure reread;
        using (var source = new StringReader(serialized.ToString()))
        {
            reread = ExchangeStructure.Read(source, [Ap214SchemaDescriptor.Instance]);
        }

        var actualSignature = CreateSemanticSignature(reread);
        var rewritten = new StringWriter(CultureInfo.InvariantCulture);
        reread.Write(rewritten);
        var rereadGraph = Part21FixtureSignature.Create(rewritten.ToString()
            .Replace(EditedProductName, OriginalProductName, StringComparison.Ordinal));
        if (!StringComparer.Ordinal.Equals(expectedSignature, actualSignature)
            || !StringComparer.Ordinal.Equals(expectedGraph, rereadGraph)
            || reread.Entities.OfType<IProduct>().Single().Name.Value != EditedProductName)
        {
            return 22;
        }

        Console.WriteLine("AP214_REFERENCE_GRAPH_OK entities=173 values-and-named-references=preserved");

        Console.WriteLine(
            "AP214_ROUND_TRIP_OK edit=product.name entities=173 faces=6 edges=12 "
            + "vertices=8 points=27 units=millimetre,radian,steradian extents=10x20x30 "
            + "shared-vertex-degrees=3,3,3,3,3,3,3,3");

        var invalidProduct = reread.Entities.OfType<Product>().Single();
        var invalidFace = reread.Entities.OfType<AdvancedFace>().First();
        invalidProduct.FrameOfReference.Clear();
        invalidFace.Bounds.Clear();
        var invalidValidation = reread.Validate();
        var expectedProductFailure = invalidValidation.Failures.Any(failure =>
            failure.Code == "AUTOMOTIVE_DESIGN.PRODUCT.FRAME_OF_REFERENCE.AGGREGATE_0.LOWER_BOUND"
            && failure.Path.EndsWith(".FrameOfReference", StringComparison.Ordinal));
        var expectedFaceFailure = invalidValidation.Failures.Any(failure =>
            failure.Code == "AUTOMOTIVE_DESIGN.FACE.BOUNDS.AGGREGATE_0.LOWER_BOUND"
            && failure.Path.EndsWith(".Bounds", StringComparison.Ordinal));
        if (invalidValidation.IsValid || !expectedProductFailure || !expectedFaceFailure)
        {
            return 23;
        }

        var rejectedOutput = new StringWriter(CultureInfo.InvariantCulture);
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
            $"AP214_INVALID_EDIT_REJECTED failures={invalidValidation.Failures.Count} "
            + "codes=product.frame-of-reference,face.bounds output-bytes=0");

        ExchangeStructure? unsupported = null;
        try
        {
            using var source = File.OpenText(arguments[1]);
            unsupported = ExchangeStructure.Read(source, [Ap214SchemaDescriptor.Instance]);
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
                $"AP214_EXTENSION_REJECTED code={diagnostic.Code} "
                + $"line={diagnostic.SourceLocation.Line} column={diagnostic.SourceLocation.Column} "
                + "partial-model=false");
        }

        ExchangeStructure? wrongSchema = null;
        try
        {
            using var source = File.OpenText(arguments[2]);
            wrongSchema = ExchangeStructure.Read(source, [Ap214SchemaDescriptor.Instance]);
            return 40;
        }
        catch (ExchangeStructureBindingException exception)
        {
            var diagnostic = exception.Diagnostics.Single(item => item.Code == "P21-BIND-SCHEMA");
            if (wrongSchema is not null)
            {
                return 41;
            }

            Console.WriteLine($"AP214_SCHEMA_REJECTED code={diagnostic.Code} partial-model=false");
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
            .Select(point => string.Join(
                ",",
                point.Coordinates.Select(value => value.Value.ToString())))
            .OrderBy(value => value, StringComparer.Ordinal);
        var unitNames = units
            .Select(unit => $"{unit.Prefix?.Value ?? "none"}:{unit.Name.Value}")
            .OrderBy(value => value, StringComparer.Ordinal);
        var vertexDegrees = GetVertexDegrees(vertices, edges);
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
            $"extents={GetExtents(points)}",
            $"vertex-degrees={string.Join(",", vertexDegrees)}",
            $"shape-reference={ReferenceEquals(shapeDefinition.UsedRepresentation, representation)}",
            $"solid-reference={representationContract.Items.Any(item => ReferenceEquals(item, solid))}");
    }

    private static int[] GetVertexDegrees(
        IReadOnlyCollection<IVertexPoint> vertices,
        IReadOnlyCollection<IEdgeCurve> edges) => vertices
        .Select(vertex => edges.Count(edge =>
            ReferenceEquals(edge.EdgeStart, vertex) || ReferenceEquals(edge.EdgeEnd, vertex)))
        .OrderBy(value => value)
        .ToArray();

    private static string GetExtents(IReadOnlyCollection<ICartesianPoint> points)
    {
        var coordinates = points
            .Where(point => point.Coordinates.Count == 3)
            .Select(point => point.Coordinates.Select(value => value.Value.ToDouble()).ToArray())
            .ToArray();
        var dimensions = Enumerable.Range(0, 3)
            .Select(axis => coordinates.Max(point => point[axis]) - coordinates.Min(point => point[axis]))
            .Select(value => value.ToString("0.###############", CultureInfo.InvariantCulture));
        return string.Join("x", dimensions);
    }

    private static string FailureEvidence(ValidationFailure failure) => $"{failure.Code}|{failure.Path}";

    private static void WriteBindingDiagnostics(ExchangeStructureBindingException exception)
    {
        foreach (var diagnostic in exception.Diagnostics)
        {
            Console.Error.WriteLine(
                $"{diagnostic.Code} {diagnostic.SourceLocation?.Line}:{diagnostic.SourceLocation?.Column} "
                + diagnostic.Message);
        }
    }

    private sealed class ProbeTextReader : TextReader
    {
        internal bool WasRead { get; private set; }

        public override int Read(char[] buffer, int index, int count)
        {
            WasRead = true;
            return 0;
        }
    }
}
