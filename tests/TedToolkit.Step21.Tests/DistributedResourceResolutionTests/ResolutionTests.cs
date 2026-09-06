using System.Collections;
using System.IO.Compression;
using System.Text;

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.DistributedResourceResolutionTests;

/// <summary>Proves ISO 10303-21 clause 10 and Annex A.4/A.5 resource-graph behavior.</summary>
public sealed class ResolutionTests
{
    private const string SCHEMA = """
        SCHEMA distributed_resource;
        ENTITY node;
          label : STRING;
          next_node : OPTIONAL node;
        END_ENTITY;
        ENTITY other;
          label : STRING;
        END_ENTITY;
        ENTITY holder;
          target : OPTIONAL node;
        END_ENTITY;
        END_SCHEMA;
        """;

    private static readonly Lazy<SchemaDescriptor> Descriptor = new(CreateDescriptor);

    /// <summary>Resolves local anchors, shared external entity identity, and external values.</summary>
    [Test]
    public async Task Should_resolve_local_external_entity_and_value_identity_with_one_shared_fetch()
    {
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            ["https://example.test/models/child.p21"] = ClearText(
                "https://example.test/models/child.p21",
                Exchange(
                    "ANCHOR;<target>=#1;<size>=42;ENDSEC;",
                    string.Empty,
                    "#1=NODE('external',$);")),
        });
        var structure = Read(
            Exchange(
                "ANCHOR;<local>=#1;<forward>=#93;ENDSEC;",
                "REFERENCE;#90=<#local>;#91=<child.p21#target>;@92=<child.p21#size>;#94=<#forward>;#93=<child.p21#target>;ENDSEC;",
                "#1=NODE('local',$);#2=HOLDER(#90);#3=HOLDER(#91);#4=HOLDER(#94);"),
            new ExchangeStructureReadOptions(
                new Uri("https://example.test/models/root.p21"),
                provider));

        var entities = structure.Registrations.ToDictionary(item => item.Name.CanonicalDigits, item => item.Entity);
        var localTarget = entities["2"].GetType().GetProperty("Target")!.GetValue(entities["2"]);
        var externalTarget = entities["3"].GetType().GetProperty("Target")!.GetValue(entities["3"]);
        var forwardedTarget = entities["4"].GetType().GetProperty("Target")!.GetValue(entities["4"]);
        var externalReference = structure.References.Single(item => item.CanonicalDigits == "91");
        var valueReference = structure.References.Single(item => item.CanonicalDigits == "92");
        _ = valueReference.TryGetResolvedValue(out var resolvedValue);

        using (Assert.Multiple())
        {
            await Assert.That(localTarget).IsSameReferenceAs(entities["1"]);
            await Assert.That(externalReference.ResolutionStatus).IsEqualTo(Part21ReferenceResolutionStatus.Resolved);
            await Assert.That(externalReference.TryGetResolvedValue(out var externalValue)).IsTrue();
            await Assert.That(externalValue!.TryGetEntity(out var externalEntity)).IsTrue();
            await Assert.That(externalTarget).IsSameReferenceAs(externalEntity);
            await Assert.That(forwardedTarget).IsSameReferenceAs(externalEntity);
            await Assert.That(valueReference.ResolutionStatus).IsEqualTo(Part21ReferenceResolutionStatus.Resolved);
            await Assert.That(resolvedValue!.TryGetInteger(out var integer)).IsTrue();
            await Assert.That(integer).IsEqualTo(42);
            await Assert.That(provider.Requests).IsEquivalentTo(["https://example.test/models/child.p21"]);
            await Assert.That(structure.Validate().IsValid).IsTrue();
        }
    }

    /// <summary>Applies the standard null result to fragmentless and missing targets.</summary>
    [Test]
    public async Task Should_apply_standard_null_result_without_acquiring_fragmentless_or_missing_resources()
    {
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>());
        var structure = Read(
            Exchange(
                string.Empty,
                "REFERENCE;#90=<missing.p21>;#91=<missing.p21#absent>;ENDSEC;",
                "#1=HOLDER(#90);#2=HOLDER(#91);"),
            new ExchangeStructureReadOptions(
                new Uri("https://example.test/root.p21"),
                provider));
        var holders = structure.Registrations.Select(item => item.Entity).ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(holders.All(holder => holder.GetType().GetProperty("Target")!.GetValue(holder) is null)).IsTrue();
            await Assert.That(structure.References.All(reference =>
                reference.ResolutionStatus == Part21ReferenceResolutionStatus.Null)).IsTrue();
            await Assert.That(provider.Requests).IsEquivalentTo(["https://example.test/missing.p21"]);
        }
    }

    /// <summary>Terminates circular anchor forwarding with the standard null result.</summary>
    [Test]
    public async Task Should_resolve_reference_cycles_to_null()
    {
        var structure = Read(
            Exchange(
                "ANCHOR;<first>=<#second>;<second>=<#first>;ENDSEC;",
                "REFERENCE;#90=<#first>;ENDSEC;",
                "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions());
        var holder = structure.Registrations.Single().Entity;

        using (Assert.Multiple())
        {
            await Assert.That(holder.GetType().GetProperty("Target")!.GetValue(holder)).IsNull();
            await Assert.That(structure.References.Single().ResolutionStatus)
                .IsEqualTo(Part21ReferenceResolutionStatus.Null);
        }
    }

    /// <summary>Reads a directory root and subsidiary entirely from supplied memory.</summary>
    [Test]
    public async Task Should_resolve_directory_root_and_subsidiary_without_disk_access()
    {
        var content = new Part21ResourceContent(
            new Uri("https://example.test/package/"),
            new Dictionary<string, ReadOnlyMemory<byte>>
            {
                ["ISO-10303.p21"] = Utf8(Exchange(
                    "ANCHOR;<published>=<parts/child.p21#target>;ENDSEC;",
                    string.Empty,
                    "#1=NODE('root',$);")),
                ["parts/child.p21"] = Utf8(Exchange(
                    "ANCHOR;<target>=#1;ENDSEC;",
                    string.Empty,
                    "#1=NODE('directory child',$);")),
            });
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            ["https://example.test/package/"] = content,
        });
        var structure = Read(
            Exchange(string.Empty, "REFERENCE;#90=<https://example.test/package/#published>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(resourceProvider: provider));

        var reference = structure.References.Single();
        await Assert.That(reference.ResolutionStatus).IsEqualTo(Part21ReferenceResolutionStatus.Resolved);
        await Assert.That(structure.Validate().IsValid).IsTrue();
    }

    /// <summary>Reads a ZIP root and subsidiary entirely from supplied memory.</summary>
    [Test]
    public async Task Should_resolve_zip_root_and_subsidiary_in_memory()
    {
        var zip = CreateZip(new Dictionary<string, string>
        {
            ["ISO-10303.p21"] = Exchange(
                "ANCHOR;<published>=<parts/child.p21#target>;ENDSEC;",
                string.Empty,
                "#1=NODE('root',$);"),
            ["parts/child.p21"] = Exchange(
                "ANCHOR;<target>=#1;ENDSEC;",
                string.Empty,
                "#1=NODE('zip child',$);"),
        });
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            ["https://example.test/package.zip"] = new(
                new Uri("https://example.test/package.zip"),
                Part21ResourceContentKind.ZipArchive,
                zip),
        });
        var structure = Read(
            Exchange(string.Empty, "REFERENCE;#90=<https://example.test/package.zip#published>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(resourceProvider: provider));

        await Assert.That(structure.References.Single().ResolutionStatus)
            .IsEqualTo(Part21ReferenceResolutionStatus.Resolved);
    }

    /// <summary>Separates missing capability, quota, and scoped-path diagnostics.</summary>
    [Test]
    public async Task Should_require_explicit_provider_and_distinguish_quota_and_archive_path_failures()
    {
        var missingProvider = Assert.Throws<ExchangeStructureCapabilityException>(() => Read(
            Exchange(string.Empty, "REFERENCE;#90=<https://example.test/child.p21#target>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions()));

        var quotaProvider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            ["https://example.test/a.p21"] = ClearText("https://example.test/a.p21", Exchange("ANCHOR;<x>=#1;ENDSEC;", string.Empty, "#1=NODE('a',$);")),
            ["https://example.test/b.p21"] = ClearText("https://example.test/b.p21", Exchange("ANCHOR;<x>=#1;ENDSEC;", string.Empty, "#1=NODE('b',$);")),
        });
        var quota = Assert.Throws<ExchangeStructureCapabilityException>(() => Read(
            Exchange(string.Empty, "REFERENCE;#90=<https://example.test/a.p21#x>;#91=<https://example.test/b.p21#x>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(
                resourceProvider: quotaProvider,
                resourceLimits: new Part21ResourceLimits(maximumResourceCount: 1))));

        var pathProvider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            ["https://example.test/package/"] = new(
                new Uri("https://example.test/package/"),
                new Dictionary<string, ReadOnlyMemory<byte>>
                {
                    ["ISO-10303.p21"] = Utf8(Exchange(
                        "ANCHOR;<bad>=<../outside.p21#x>;ENDSEC;",
                        string.Empty,
                        "#1=NODE('root',$);")),
                }),
        });
        var path = Assert.Throws<ExchangeStructureCapabilityException>(() => Read(
            Exchange(string.Empty, "REFERENCE;#90=<https://example.test/package/#bad>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(resourceProvider: pathProvider)));

        using (Assert.Multiple())
        {
            await Assert.That(missingProvider.Diagnostics.Single().Code).IsEqualTo("P21-CAP-RESOURCE-PROVIDER");
            await Assert.That(quota.Diagnostics.Single().Code).IsEqualTo("P21-RESOURCE-LIMIT");
            await Assert.That(path.Diagnostics.Single().Code).IsEqualTo("P21-RESOURCE-ARCHIVE-PATH");
        }
    }

    /// <summary>Uses explicit conversion and rejects provider re-entry.</summary>
    [Test]
    public async Task Should_use_explicit_other_format_converter_and_reject_provider_reentry()
    {
        var other = new Part21ResourceContent(
            new Uri("https://example.test/model.jt"),
            Part21ResourceContentKind.Other,
            Utf8("opaque"));
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            ["https://example.test/model.jt"] = other,
        });
        var converter = new StaticConverter(ClearText(
            "https://example.test/model.jt",
            Exchange("ANCHOR;<target>=#1;ENDSEC;", string.Empty, "#1=NODE('converted',$);")));
        var converted = Read(
            Exchange(string.Empty, "REFERENCE;#90=<https://example.test/model.jt#target>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(resourceProvider: provider, resourceConverter: converter));

        ReenteringProvider? reentering = null;
        var options = new ExchangeStructureReadOptions(
            resourceProvider: reentering = new ReenteringProvider(() => Read(
                Exchange(string.Empty, "REFERENCE;#90=<https://example.test/reenter.p21#target>;ENDSEC;", "#1=HOLDER(#90);"),
                new ExchangeStructureReadOptions(resourceProvider: reentering))));
        var reentry = Assert.Throws<ExchangeStructureCapabilityException>(() => Read(
            Exchange(string.Empty, "REFERENCE;#90=<https://example.test/outer.p21#target>;ENDSEC;", "#1=HOLDER(#90);"),
            options));

        using (Assert.Multiple())
        {
            await Assert.That(converted.References.Single().ResolutionStatus)
                .IsEqualTo(Part21ReferenceResolutionStatus.Resolved);
            await Assert.That(converter.CallCount).IsEqualTo(1);
            await Assert.That(reentry.Diagnostics.Single().Code).IsEqualTo("P21-RESOURCE-PROVIDER-REENTRY");
        }
    }

    /// <summary>Uses a UUID registry and preserves type mismatch evidence.</summary>
    [Test]
    public async Task Should_use_uuid_registry_and_report_external_schema_or_type_mismatch_atomically()
    {
        const string uuid = "97c6e1f0-3544-11e5-a2cb-0800200c9a66";
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            ["#" + uuid] = ClearText(
                "https://example.test/registry-result.p21",
                Exchange($"ANCHOR;<{uuid}>=#1;ENDSEC;", string.Empty, "#1=NODE('uuid',$);")),
            ["https://example.test/wrong.p21"] = ClearText(
                "https://example.test/wrong.p21",
                Exchange("ANCHOR;<target>=#1;ENDSEC;", string.Empty, "#1=OTHER('wrong type');")),
        });
        var resolved = Read(
            Exchange(string.Empty, $"REFERENCE;#90=<#{uuid}>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(resourceProvider: provider));
        var mismatch = Assert.Throws<ExchangeStructureReadValidationException>(() => Read(
            Exchange(string.Empty, "REFERENCE;#90=<https://example.test/wrong.p21#target>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(resourceProvider: provider)));

        using (Assert.Multiple())
        {
            await Assert.That(resolved.References.Single().ResolutionStatus)
                .IsEqualTo(Part21ReferenceResolutionStatus.Resolved);
            await Assert.That(provider.Requests).Contains("#" + uuid);
            await Assert.That(mismatch.ValidationResult.Failures.Single().Code)
                .IsEqualTo("P21.READ.REFERENCE.TYPE");
        }
    }

    /// <summary>Separates a missing archive root from nested archive recursion.</summary>
    [Test]
    public async Task Should_distinguish_missing_archive_root_and_nested_archive_recursion()
    {
        var nested = CreateZip(new Dictionary<string, string>
        {
            ["ISO-10303.p21"] = Exchange("ANCHOR;<target>=#1;ENDSEC;", string.Empty, "#1=NODE('nested',$);"),
        });
        var outer = CreateZip(new Dictionary<string, ReadOnlyMemory<byte>>
        {
            ["ISO-10303.p21"] = Utf8(Exchange(
                "ANCHOR;<target>=<nested.zip#target>;ENDSEC;",
                string.Empty,
                "#1=NODE('outer',$);")),
            ["nested.zip"] = nested,
        });
        var noRoot = CreateZip(new Dictionary<string, string> { ["child.p21"] = Exchange(string.Empty, string.Empty, "#1=NODE('x',$);") });
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            ["https://example.test/outer.zip"] = new(
                new Uri("https://example.test/outer.zip"),
                Part21ResourceContentKind.ZipArchive,
                outer),
            ["https://example.test/no-root.zip"] = new(
                new Uri("https://example.test/no-root.zip"),
                Part21ResourceContentKind.ZipArchive,
                noRoot),
        });
        var recursion = Assert.Throws<ExchangeStructureCapabilityException>(() => Read(
            Exchange(string.Empty, "REFERENCE;#90=<https://example.test/outer.zip#target>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(
                resourceProvider: provider,
                resourceLimits: new Part21ResourceLimits(maximumArchiveDepth: 1))));
        var root = Assert.Throws<ExchangeStructureCapabilityException>(() => Read(
            Exchange(string.Empty, "REFERENCE;#90=<https://example.test/no-root.zip#target>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(resourceProvider: provider)));

        using (Assert.Multiple())
        {
            await Assert.That(recursion.Diagnostics.Single().Code).IsEqualTo("P21-RESOURCE-ARCHIVE-RECURSION");
            await Assert.That(root.Diagnostics.Single().Code).IsEqualTo("P21-RESOURCE-ARCHIVE-ROOT");
        }
    }

    private static ExchangeStructure Read(string source, ExchangeStructureReadOptions options) =>
        ExchangeStructure.Read(new StringReader(source), [Descriptor.Value], options);

    private static string Exchange(string anchors, string references, string records) => $$"""
        ISO-10303-21;
        HEADER;
        FILE_DESCRIPTION(('distributed resource test'),'3;2');
        FILE_NAME('resource.p21','2026-09-07T00:00:00',('Author'),('Org'),'Pre','System','Auth');
        FILE_SCHEMA(('distributed_resource'));
        ENDSEC;
        {{anchors}}
        {{references}}
        DATA;
        {{records}}
        ENDSEC;
        END-ISO-10303-21;
        """;

    private static Part21ResourceContent ClearText(string identity, string source) => new(
        new Uri(identity),
        Part21ResourceContentKind.ClearText,
        Utf8(source));

    private static ReadOnlyMemory<byte> Utf8(string source) => Encoding.UTF8.GetBytes(source);

    private static ReadOnlyMemory<byte> CreateZip(IReadOnlyDictionary<string, string> entries)
        => CreateZip(entries.ToDictionary(item => item.Key, item => Utf8(item.Value)));

    private static ReadOnlyMemory<byte> CreateZip(IReadOnlyDictionary<string, ReadOnlyMemory<byte>> entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var item in entries)
            {
                using var destination = archive.CreateEntry(item.Key, CompressionLevel.Fastest).Open();
                destination.Write(item.Value.Span);
            }
        }
        return stream.ToArray();
    }

    private static SchemaDescriptor CreateDescriptor()
    {
        var result = GeneratorHostTests.Run(("schemas/distributed-resource.exp", SCHEMA));
        var diagnostics = result.Diagnostics
            .Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        if (diagnostics.Length > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics.AsEnumerable()));

        using var stream = new MemoryStream();
        var emit = result.OutputCompilation.Emit(stream);
        if (!emit.Success)
            throw new InvalidOperationException(string.Join(Environment.NewLine, emit.Diagnostics));
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        return (SchemaDescriptor)assembly.GetType(
            "TedToolkit.Step21.Generated.DistributedResource.SchemaDescriptor",
            throwOnError: true)!.GetProperty("Instance")!.GetValue(null)!;
    }

    private sealed class DictionaryProvider(IReadOnlyDictionary<string, Part21ResourceContent> resources)
        : IPart21ResourceProvider
    {
        internal List<string> Requests { get; } = [];

        public Part21ResourceContent? GetResource(Uri resourceIdentity)
        {
            var key = resourceIdentity.IsAbsoluteUri ? resourceIdentity.AbsoluteUri : resourceIdentity.OriginalString;
            Requests.Add(key);
            return resources.GetValueOrDefault(key);
        }
    }

    private sealed class StaticConverter(Part21ResourceContent converted) : IPart21ResourceConverter
    {
        internal int CallCount { get; private set; }

        public Part21ResourceContent Convert(Part21ResourceContent content)
        {
            CallCount++;
            return converted;
        }
    }

    private sealed class ReenteringProvider(Func<ExchangeStructure> reenter) : IPart21ResourceProvider
    {
        public Part21ResourceContent? GetResource(Uri resourceIdentity)
        {
            _ = reenter();
            return null;
        }
    }
}