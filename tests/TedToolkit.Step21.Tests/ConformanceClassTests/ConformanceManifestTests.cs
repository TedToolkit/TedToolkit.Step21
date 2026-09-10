using System.Text.Json;

namespace TedToolkit.Step21.Tests.ConformanceClassTests;

/// <summary>Mechanically verifies the ISO 10303-21:2016 clause trace and Annex D PICS answers.</summary>
internal sealed class ConformanceManifestTests
{
    [Test]
    public async Task Should_publish_a_complete_and_self_consistent_pics()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "docs", "conformance", "iso-10303-21-2016.json");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var manifest = document.RootElement;
        var requirements = manifest.GetProperty("requirements").EnumerateArray().ToArray();
        var ids = requirements.Select(item => item.GetProperty("id").GetString()!).ToArray();
        var sourceExcluded = requirements.Where(item =>
                item.GetProperty("status").GetString() == "source-excluded")
            .ToArray();

        foreach (var requirement in requirements)
        {
            var applicability = requirement.GetProperty("applicability").GetString();
            var status = requirement.GetProperty("status").GetString();
            await Assert.That(requirement.GetProperty("source").GetString()).IsNotEmpty();
            await Assert.That(requirement.GetProperty("implementation").GetString()).IsNotEmpty();
            await Assert.That(status).IsIn("implemented", "not-applicable", "source-excluded");
            await Assert.That(applicability).IsIn("applicable", "not-applicable");

            if (status == "not-applicable")
            {
                await Assert.That(applicability).IsEqualTo("not-applicable");
                await Assert.That(requirement.GetProperty("rationale").GetString()).IsNotEmpty();
                continue;
            }

            var proof = requirement.GetProperty("proof");
            var proofPath = Path.Combine(root, proof.GetProperty("file").GetString()!);
            var proofMember = proof.GetProperty("member").GetString()!;
            await Assert.That(File.Exists(proofPath)).IsTrue();
            await Assert.That(await File.ReadAllTextAsync(proofPath)).Contains(proofMember);
            if (status == "source-excluded")
            {
                await Assert.That(applicability).IsEqualTo("applicable");
                await Assert.That(requirement.GetProperty("exclusion").GetString()).IsNotEmpty();
            }
        }

        var pics = manifest.GetProperty("pics");
        var classes = pics.GetProperty("D.2");
        var instanceEncodings = pics.GetProperty("D.3.1");
        var shortNameEncodings = pics.GetProperty("D.3.2");
        var stringEncodings = pics.GetProperty("D.3.3");
        var referenceEncodings = pics.GetProperty("D.3.4");
        var limits = manifest.GetProperty("implementationLimits");
        var processing = Part21ProcessingLimits.Default;

        using (Assert.Multiple())
        {
            await Assert.That(manifest.GetProperty("claimStatus").GetString()).IsEqualTo("blocked");
            await Assert.That(ids.Distinct(StringComparer.Ordinal).Count()).IsEqualTo(ids.Length);
            await Assert.That(ids).IsEquivalentTo([
                "4.3-classification",
                "5-formal-syntax",
                "5.6-token-separators",
                "6.1-6.3-token-keywords",
                "6.4.1-6.4.2-numbers",
                "6.4.3.1-6.4.3.4-string-encodings",
                "6.4.3.5-string-limit",
                "6.4.4-local-occurrences",
                "6.4.4-express-constants",
                "6.4.5-enumeration",
                "6.4.6-binary",
                "6.5.1-6.5.5-resource-encodings",
                "6.5.6-base64",
                "7-structured-values",
                "8.1-8.2.4-required-header",
                "8.2.5-8.2.6-populations",
                "8.2.7-section-language",
                "8.2.8-section-context",
                "8.3-user-header",
                "9-anchor",
                "10-reference",
                "11.1-11.2-data-sections",
                "11.3-user-defined-data",
                "12.1-value-mapping",
                "12.2-entity-mapping",
                "12.3-12.6-schema-constant-rule",
                "13-print-control-semantics",
                "13-print-control-placement",
                "14-signatures",
                "A.1-A.3-legacy-media",
                "A.4-A.5-archive-directory",
                "C-registration",
                "D-pics",
                "E-multiple-schemas",
                "F-ecmascript",
                "G-uuid-anchor",
            ]);
            await Assert.That(sourceExcluded.Select(item => item.GetProperty("id").GetString()!)).IsEquivalentTo([
                "4.3-classification",
                "6.4.4-express-constants",
                "12.1-value-mapping",
                "12.3-12.6-schema-constant-rule",
            ]);
            foreach (var className in new[] { "class1", "class2" })
            {
                await Assert.That(classes.GetProperty(className).GetProperty("read").GetBoolean()).IsTrue();
                await Assert.That(classes.GetProperty(className).GetProperty("write").GetBoolean()).IsTrue();
            }
            await Assert.That(classes.GetProperty("class3").GetProperty("read").GetBoolean()).IsFalse();
            await Assert.That(classes.GetProperty("class3").GetProperty("write").GetBoolean()).IsFalse();

            foreach (var encodingName in new[] { "entityInstanceNames", "valueInstanceNames" })
            {
                await Assert.That(instanceEncodings.GetProperty(encodingName).GetProperty("read").GetBoolean()).IsTrue();
                await Assert.That(instanceEncodings.GetProperty(encodingName).GetProperty("write").GetBoolean()).IsTrue();
            }
            await Assert.That(instanceEncodings.GetProperty("expressConstantNames").GetProperty("read").GetBoolean())
                .IsFalse();
            await Assert.That(instanceEncodings.GetProperty("expressConstantNames").GetProperty("write").GetBoolean())
                .IsFalse();

            foreach (var encodingName in new[] { "entityShortNames", "selectShortNames", "enumerationShortNames" })
            {
                await Assert.That(shortNameEncodings.GetProperty(encodingName).GetProperty("read").GetBoolean())
                    .IsFalse();
                await Assert.That(shortNameEncodings.GetProperty(encodingName).GetProperty("write").GetBoolean())
                    .IsFalse();
            }

            foreach (var encodingName in new[] { "x", "iso8859Page", "x2", "x4", "utf8" })
            {
                var encoding = stringEncodings.GetProperty(encodingName);
                await Assert.That(encoding.GetProperty("read").GetBoolean()).IsTrue();
                await Assert.That(encoding.GetProperty("write").GetBoolean()).IsTrue();
                await Assert.That(encoding.GetProperty("representation").GetString()).IsNotEmpty();
            }

            var uriReferences = referenceEncodings.GetProperty("uriReferences");
            foreach (var capability in new[] { "anchors", "references", "clearText", "compressedArchives" })
                await Assert.That(uriReferences.GetProperty(capability).GetBoolean()).IsTrue();
            var explicitPopulation = referenceEncodings.GetProperty("explicitSchemaPopulation");
            foreach (var capability in new[] { "withoutTimestamps", "withTimestamps", "withoutSignatures", "withSignatures" })
                await Assert.That(explicitPopulation.GetProperty(capability).GetBoolean()).IsTrue();

            await Assert.That(limits.GetProperty("maximumSchemas").GetInt32()).IsEqualTo(processing.MaximumItemCount);
            await Assert.That(limits.GetProperty("maximumDataSections").GetInt32()).IsEqualTo(processing.MaximumItemCount);
            await Assert.That(limits.GetProperty("maximumInstanceNames").GetInt32()).IsEqualTo(processing.MaximumItemCount);
            await Assert.That(limits.GetProperty("integerRepresentation").GetString()).IsNotEmpty();
            await Assert.That(limits.GetProperty("realRepresentation").GetString()).IsNotEmpty();
            await Assert.That(limits.GetProperty("binaryLimit").GetString()).IsNotEmpty();
            await Assert.That(limits.GetProperty("maximumAggregateElements").GetInt32()).IsEqualTo(processing.MaximumItemCount);
            await Assert.That(limits.GetProperty("maximumAggregateNestingDepth").GetInt32()).IsEqualTo(processing.MaximumNestingDepth);
            await Assert.That(limits.GetProperty("maximumInputCharacters").GetInt32()).IsEqualTo(processing.MaximumInputCharacters);
            await Assert.That(limits.GetProperty("maximumOutputCharacters").GetInt32()).IsEqualTo(processing.MaximumOutputCharacters);
            await Assert.That(limits.GetProperty("maximumStoredStringOctets").GetInt32()).IsEqualTo(32769);
        }
    }

    [Test]
    [Property("ISO21WorkItem", "ISO21-009")]
    public async Task Should_publish_a_source_gated_express_semantic_profile()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "docs", "conformance", "iso-10303-11-validation-semantics.json");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var manifest = document.RootElement;
        var families = manifest.GetProperty("families").EnumerateArray().ToArray();
        var ids = families.Select(family => family.GetProperty("id").GetString()!).ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(manifest.GetProperty("claimStatus").GetString()).IsEqualTo("blocked");
            await Assert.That(ids.Distinct(StringComparer.Ordinal).Count()).IsEqualTo(ids.Length);
            await Assert.That(families).IsNotEmpty();
            await Assert.That(families.Any(family =>
                family.GetProperty("implementationStatus").GetString() == "source-excluded")).IsTrue();
        }

        foreach (var family in families)
        {
            var status = family.GetProperty("implementationStatus").GetString();
            await Assert.That(family.GetProperty("source").GetString()).IsNotEmpty();
            await Assert.That(family.GetProperty("sourceStatus").GetString()).IsNotEmpty();
            await Assert.That(family.GetProperty("boundary").GetString()).IsNotEmpty();
            await Assert.That(status).IsIn("implemented", "partial", "source-excluded");

            if (status == "source-excluded")
            {
                await Assert.That(family.GetProperty("exclusion").GetString()).IsNotEmpty();
                continue;
            }

            var proofs = family.GetProperty("proof").EnumerateArray().ToArray();
            await Assert.That(proofs).IsNotEmpty();
            foreach (var proof in proofs)
            {
                var proofPath = Path.Combine(root, proof.GetProperty("file").GetString()!);
                var member = proof.GetProperty("member").GetString()!;
                await Assert.That(File.Exists(proofPath)).IsTrue();
                await Assert.That(await File.ReadAllTextAsync(proofPath)).Contains(member);
            }

            if (status == "partial")
                await Assert.That(family.GetProperty("blocker").GetString()).IsNotEmpty();
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TedToolkit.Step21.slnx")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
