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
        WHERE
          valid_label : label <> 'invalid';
        END_ENTITY;
        ENTITY other;
          label : STRING;
        END_ENTITY;
        ENTITY holder;
          target : OPTIONAL node;
        END_ENTITY;
        TYPE measure = INTEGER;
        END_TYPE;
        TYPE scalar_choice = SELECT (measure);
        END_TYPE;
        ENTITY value_holder;
          amount : INTEGER;
          amounts : LIST [1:?] OF INTEGER;
          selected : scalar_choice;
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
                "REFERENCE;#90=<#local>;#91=<child.p21#target>;@92=<child.p21#size>;#94=<#forward>;#93=<child.p21#target>;#95=<child.p21#1>;ENDSEC;",
                "#1=NODE('local',$);#2=HOLDER(#90);#3=HOLDER(#91);#4=HOLDER(#94);#5=VALUE_HOLDER(@92,(@92,@92),MEASURE(@92));#6=HOLDER(#95);"),
            new ExchangeStructureReadOptions(
                new Uri("https://example.test/models/root.p21"),
                provider));

        var entities = structure.Registrations.ToDictionary(item => item.Name.CanonicalDigits, item => item.Entity);
        var localTarget = entities["2"].GetType().GetProperty("Target")!.GetValue(entities["2"]);
        var externalTarget = entities["3"].GetType().GetProperty("Target")!.GetValue(entities["3"]);
        var forwardedTarget = entities["4"].GetType().GetProperty("Target")!.GetValue(entities["4"]);
        var numericTarget = entities["6"].GetType().GetProperty("Target")!.GetValue(entities["6"]);
        var externalReference = structure.References.Single(item => item.CanonicalDigits == "91");
        var valueReference = structure.References.Single(item => item.CanonicalDigits == "92");
        _ = valueReference.TryGetResolvedValue(out var resolvedValue);
        using var output = new StringWriter();
        structure.Write(output);

        using (Assert.Multiple())
        {
            await Assert.That(localTarget).IsSameReferenceAs(entities["1"]);
            await Assert.That(externalReference.ResolutionStatus).IsEqualTo(Part21ReferenceResolutionStatus.Resolved);
            await Assert.That(externalReference.TryGetResolvedValue(out var externalValue)).IsTrue();
            await Assert.That(externalValue!.TryGetEntity(out var externalEntity)).IsTrue();
            await Assert.That(externalTarget).IsSameReferenceAs(externalEntity);
            await Assert.That(forwardedTarget).IsSameReferenceAs(externalEntity);
            await Assert.That(numericTarget).IsSameReferenceAs(externalEntity);
            await Assert.That(valueReference.ResolutionStatus).IsEqualTo(Part21ReferenceResolutionStatus.Resolved);
            await Assert.That(resolvedValue!.TryGetInteger(out var integer)).IsTrue();
            await Assert.That(integer).IsEqualTo(42);
            await Assert.That(provider.Requests).IsEquivalentTo(["https://example.test/models/child.p21"]);
            await Assert.That(output.ToString()).Contains("VALUE_HOLDER(42,(42,42),MEASURE(42));");
            await Assert.That(output.ToString()).Contains("'4;3'");
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
                    "ANCHOR;<published>=<parts/child.p21#target>;<local>=#1;<self>=#90;ENDSEC;",
                    "REFERENCE;#90=<https://example.test/package/#local>;ENDSEC;",
                    "#1=NODE('root',$);",
                    "4;2")),
                ["parts/child.p21"] = Utf8(Exchange(
                    "ANCHOR;<target>=#1;ENDSEC;",
                    string.Empty,
                    "#1=NODE('directory child',$);",
                    "4;2")),
            });
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            ["https://example.test/package/"] = content,
        });
        var structure = Read(
            Exchange(
                string.Empty,
                "REFERENCE;#90=<https://example.test/package/#published>;#91=<https://example.test/package/#self>;ENDSEC;",
                "#1=HOLDER(#90);#2=HOLDER(#91);"),
            new ExchangeStructureReadOptions(resourceProvider: provider));

        using (Assert.Multiple())
        {
            await Assert.That(structure.References.All(reference =>
                reference.ResolutionStatus == Part21ReferenceResolutionStatus.Resolved)).IsTrue();
            await Assert.That(provider.Requests).IsEquivalentTo(["https://example.test/package/"]);
            await Assert.That(structure.Validate().IsValid).IsTrue();
        }
    }

    /// <summary>Exposes directory entries as an immutable snapshot.</summary>
    [Test]
    public async Task Should_expose_an_immutable_directory_snapshot()
    {
        var original = new Dictionary<string, ReadOnlyMemory<byte>>
        {
            ["ISO-10303.p21"] = Utf8("original"),
        };
        var content = new Part21ResourceContent(new Uri("https://example.test/snapshot/"), original);
        original["ISO-10303.p21"] = Utf8("changed");
        var exposed = (IDictionary<string, ReadOnlyMemory<byte>>)content.Entries;

        var mutation = Assert.Throws<NotSupportedException>(() =>
            exposed["ISO-10303.p21"] = Utf8("mutated"));

        using (Assert.Multiple())
        {
            await Assert.That(Encoding.UTF8.GetString(content.Entries["ISO-10303.p21"].Span))
                .IsEqualTo("original");
            await Assert.That(mutation).IsNotNull();
        }
    }

    /// <summary>Keeps ZIP compression and nesting limits independent from directory transport.</summary>
    [Test]
    public async Task Should_not_apply_zip_ratio_or_depth_to_directory_content()
    {
        const string plainIdentity = "https://example.test/plain-directory/";
        const string nestedIdentity = "https://example.test/nested-directory/";
        var nestedZip = CreateZip(new Dictionary<string, string>
        {
            ["ISO-10303.p21"] = Exchange(
                "ANCHOR;<target>=#1;ENDSEC;",
                string.Empty,
                "#1=NODE('nested zip',$);"),
        });
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            [plainIdentity] = new(
                new Uri(plainIdentity),
                new Dictionary<string, ReadOnlyMemory<byte>>
                {
                    ["ISO-10303.p21"] = Utf8(Exchange(
                        "ANCHOR;<target>=#1;ENDSEC;",
                        string.Empty,
                        "#1=NODE('plain directory',$);")),
                }),
            [nestedIdentity] = new(
                new Uri(nestedIdentity),
                new Dictionary<string, ReadOnlyMemory<byte>>
                {
                    ["ISO-10303.p21"] = Utf8(Exchange(
                        "ANCHOR;<target>=<nested.zip#target>;ENDSEC;",
                        string.Empty,
                        "#1=NODE('directory root',$);")),
                    ["nested.zip"] = nestedZip,
                }),
        });

        var plain = Read(
            Exchange(string.Empty, $"REFERENCE;#90=<{plainIdentity}#target>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(
                resourceProvider: provider,
                resourceLimits: new Part21ResourceLimits(maximumCompressionRatio: 0.1)));
        var nested = Read(
            Exchange(string.Empty, $"REFERENCE;#90=<{nestedIdentity}#target>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(
                resourceProvider: provider,
                resourceLimits: new Part21ResourceLimits(maximumArchiveDepth: 1)));

        using (Assert.Multiple())
        {
            await Assert.That(plain.References.Single().ResolutionStatus)
                .IsEqualTo(Part21ReferenceResolutionStatus.Resolved);
            await Assert.That(nested.References.Single().ResolutionStatus)
                .IsEqualTo(Part21ReferenceResolutionStatus.Resolved);
        }
    }

    /// <summary>Nulls an invalid subsidiary without discarding a valid directory or ZIP root.</summary>
    [Test]
    public async Task Should_keep_valid_container_roots_when_unrelated_subsidiaries_are_invalid()
    {
        var root = Exchange(
            "ANCHOR;<local>=#1;ENDSEC;",
            "REFERENCE;#90=<bad.p21#target>;ENDSEC;",
            "#1=NODE('valid root',$);#2=HOLDER(#90);");
        var invalidAfterHydration = Exchange(
            "ANCHOR;<target>=#1;ENDSEC;",
            string.Empty,
            "#1=NODE('invalid',$);");
        _ = Assert.Throws<ExchangeStructureReadValidationException>(() => Read(
            invalidAfterHydration,
            new ExchangeStructureReadOptions()));
        const string directoryIdentity = "https://example.test/invalid-subsidiary/";
        const string zipIdentity = "https://example.test/invalid-subsidiary.zip";
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            [directoryIdentity] = new(
                new Uri(directoryIdentity),
                new Dictionary<string, ReadOnlyMemory<byte>>
                {
                    ["ISO-10303.p21"] = Utf8(root),
                    ["bad.p21"] = Utf8("not an ISO 10303-21 exchange structure"),
                }),
            [zipIdentity] = new(
                new Uri(zipIdentity),
                Part21ResourceContentKind.ZipArchive,
                CreateZip(new Dictionary<string, string>
                {
                    ["ISO-10303.p21"] = root,
                    ["bad.p21"] = invalidAfterHydration,
                })),
        });

        var structure = Read(
            Exchange(
                string.Empty,
                $"REFERENCE;#90=<{directoryIdentity}#local>;#91=<{zipIdentity}#local>;ENDSEC;",
                "#1=HOLDER(#90);#2=HOLDER(#91);"),
            new ExchangeStructureReadOptions(resourceProvider: provider));

        using (Assert.Multiple())
        {
            await Assert.That(structure.References.All(reference =>
                    reference.ResolutionStatus == Part21ReferenceResolutionStatus.Resolved))
                .IsTrue();
            await Assert.That(structure.Validate().IsValid).IsTrue();
        }
    }

    /// <summary>Reads a ZIP root and subsidiary entirely from supplied memory.</summary>
    [Test]
    public async Task Should_resolve_zip_root_and_subsidiary_in_memory()
    {
        var zip = CreateZip(new Dictionary<string, string>
        {
            ["ISO-10303.p21"] = Exchange(
                "ANCHOR;<published>=<parts/child.p21#target>;<local>=#1;<self>=#90;ENDSEC;",
                "REFERENCE;#90=<https://example.test/package.zip#local>;ENDSEC;",
                "#1=NODE('root',$);",
                "4;2"),
            ["parts/child.p21"] = Exchange(
                "ANCHOR;<target>=#1;ENDSEC;",
                string.Empty,
                "#1=NODE('zip child',$);",
                "4;2"),
        });
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            ["https://example.test/package.zip"] = new(
                new Uri("https://example.test/package.zip"),
                Part21ResourceContentKind.ZipArchive,
                zip),
        });
        var structure = Read(
            Exchange(
                string.Empty,
                "REFERENCE;#90=<https://example.test/package.zip#published>;#91=<https://example.test/package.zip#self>;ENDSEC;",
                "#1=HOLDER(#90);#2=HOLDER(#91);"),
            new ExchangeStructureReadOptions(resourceProvider: provider));

        using (Assert.Multiple())
        {
            await Assert.That(structure.References.All(reference =>
                reference.ResolutionStatus == Part21ReferenceResolutionStatus.Resolved)).IsTrue();
            await Assert.That(provider.Requests).IsEquivalentTo(["https://example.test/package.zip"]);
        }
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

        var archiveEntries = CreateZip(new Dictionary<string, string>
        {
            ["folder/"] = string.Empty,
            ["ISO-10303.p21"] = Exchange(string.Empty, string.Empty, "#1=NODE('root',$);"),
        });
        var archiveEntryProvider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            ["https://example.test/entries.zip"] = new(
                new Uri("https://example.test/entries.zip"),
                Part21ResourceContentKind.ZipArchive,
                archiveEntries),
        });
        var archiveEntryQuota = Assert.Throws<ExchangeStructureCapabilityException>(() => Read(
            Exchange(string.Empty, "REFERENCE;#90=<https://example.test/entries.zip#x>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(
                resourceProvider: archiveEntryProvider,
                resourceLimits: new Part21ResourceLimits(maximumArchiveEntryCount: 1))));

        var relative = Assert.Throws<ExchangeStructureCapabilityException>(() => Read(
            Exchange(string.Empty, "REFERENCE;#90=<child.p21#x>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(resourceProvider: quotaProvider)));

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
            await Assert.That(archiveEntryQuota.Diagnostics.Single().Code).IsEqualTo("P21-RESOURCE-LIMIT");
            await Assert.That(relative.Diagnostics.Single().Code).IsEqualTo("P21-CAP-RESOURCE-BASE-URI");
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

        ReenteringProvider? first = null;
        ReenteringProvider? second = null;
        first = new ReenteringProvider(() => Read(
            Exchange(string.Empty, "REFERENCE;#90=<https://example.test/second.p21#target>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(resourceProvider: second)));
        second = new ReenteringProvider(() => Read(
            Exchange(string.Empty, "REFERENCE;#90=<https://example.test/first.p21#target>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(resourceProvider: first)));
        var crossProviderReentry = Assert.Throws<ExchangeStructureCapabilityException>(() => Read(
            Exchange(string.Empty, "REFERENCE;#90=<https://example.test/first.p21#target>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(resourceProvider: first)));

        using (Assert.Multiple())
        {
            await Assert.That(converted.References.Single().ResolutionStatus)
                .IsEqualTo(Part21ReferenceResolutionStatus.Resolved);
            await Assert.That(converter.CallCount).IsEqualTo(1);
            await Assert.That(reentry.Diagnostics.Single().Code).IsEqualTo("P21-RESOURCE-PROVIDER-REENTRY");
            await Assert.That(crossProviderReentry.Diagnostics.Single().Code)
                .IsEqualTo("P21-RESOURCE-PROVIDER-REENTRY");
        }
    }

    /// <summary>Rejects direct and transitive converter callback re-entry across nested reads.</summary>
    [Test]
    public async Task Should_reject_direct_and_cross_converter_reentry()
    {
        const string firstIdentity = "https://example.test/first.jt";
        const string secondIdentity = "https://example.test/second.jt";
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            [firstIdentity] = new(new Uri(firstIdentity), Part21ResourceContentKind.Other, Utf8("first")),
            [secondIdentity] = new(new Uri(secondIdentity), Part21ResourceContentKind.Other, Utf8("second")),
        });

        ReenteringConverter? direct = null;
        direct = new ReenteringConverter(() => Read(
            Exchange(string.Empty, $"REFERENCE;#90=<{firstIdentity}#target>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(resourceProvider: provider, resourceConverter: direct)));
        var directFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => Read(
            Exchange(string.Empty, $"REFERENCE;#90=<{firstIdentity}#target>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(resourceProvider: provider, resourceConverter: direct)));

        ReenteringConverter? first = null;
        ReenteringConverter? second = null;
        first = new ReenteringConverter(() => Read(
            Exchange(string.Empty, $"REFERENCE;#90=<{secondIdentity}#target>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(resourceProvider: provider, resourceConverter: second)));
        second = new ReenteringConverter(() => Read(
            Exchange(string.Empty, $"REFERENCE;#90=<{firstIdentity}#target>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(resourceProvider: provider, resourceConverter: first)));
        var crossFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => Read(
            Exchange(string.Empty, $"REFERENCE;#90=<{firstIdentity}#target>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(resourceProvider: provider, resourceConverter: first)));

        using (Assert.Multiple())
        {
            await Assert.That(directFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-RESOURCE-CONVERTER-REENTRY");
            await Assert.That(crossFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-RESOURCE-CONVERTER-REENTRY");
        }
    }

    /// <summary>Reconciles a converter's canonical output identity before parsing duplicate aliases.</summary>
    [Test]
    public async Task Should_share_canonical_converter_output_identity()
    {
        const string firstAlias = "https://example.test/alias-a.jt";
        const string secondAlias = "https://example.test/alias-b.jt";
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            [firstAlias] = new(new Uri(firstAlias), Part21ResourceContentKind.Other, Utf8("first")),
            [secondAlias] = new(new Uri(secondAlias), Part21ResourceContentKind.Other, Utf8("second")),
        });
        var converter = new StaticConverter(ClearText(
            "https://example.test/canonical-converted.p21",
            Exchange("ANCHOR;<target>=#1;ENDSEC;", string.Empty, "#1=NODE('converted',$);")));
        var structure = Read(
            Exchange(
                string.Empty,
                $"REFERENCE;#90=<{firstAlias}#target>;#91=<{secondAlias}#target>;ENDSEC;",
                "#1=HOLDER(#90);#2=HOLDER(#91);"),
            new ExchangeStructureReadOptions(resourceProvider: provider, resourceConverter: converter));

        var targets = structure.Registrations.Select(registration =>
        {
            var entity = registration.Entity;
            return entity.GetType().GetProperty("Target")!.GetValue(entity);
        }).ToArray();
        using (Assert.Multiple())
        {
            await Assert.That(targets[0]).IsNotNull();
            await Assert.That(targets[1]).IsSameReferenceAs(targets[0]);
            await Assert.That(converter.CallCount).IsEqualTo(2);
        }
    }

    /// <summary>Counts opaque input before conversion and does not invoke a converter past quota.</summary>
    [Test]
    public async Task Should_count_other_format_input_before_conversion()
    {
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            ["https://example.test/model.jt"] = new(
                new Uri("https://example.test/model.jt"),
                Part21ResourceContentKind.Other,
                Utf8("opaque")),
        });
        var converter = new StaticConverter(ClearText(
            "https://example.test/model.jt",
            Exchange("ANCHOR;<target>=#1;ENDSEC;", string.Empty, "#1=NODE('converted',$);")));

        var failure = Assert.Throws<ExchangeStructureCapabilityException>(() => Read(
            Exchange(string.Empty, "REFERENCE;#90=<https://example.test/model.jt#target>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(
                resourceProvider: provider,
                resourceConverter: converter,
                resourceLimits: new Part21ResourceLimits(maximumTotalBytes: 5))));

        using (Assert.Multiple())
        {
            await Assert.That(failure.Diagnostics.Single().Code).IsEqualTo("P21-RESOURCE-LIMIT");
            await Assert.That(converter.CallCount).IsEqualTo(0);
        }
    }

    /// <summary>Enforces every public resource-limit partition at its processing boundary.</summary>
    [Test]
    public async Task Should_enforce_reference_byte_archive_and_compression_limits()
    {
        var referenceDepth = Assert.Throws<ExchangeStructureCapabilityException>(() => Read(
            Exchange(
                "ANCHOR;<first>=<#second>;<second>=#1;ENDSEC;",
                "REFERENCE;#90=<#first>;ENDSEC;",
                "#1=NODE('local',$);#2=HOLDER(#90);"),
            new ExchangeStructureReadOptions(
                resourceLimits: new Part21ResourceLimits(maximumReferenceDepth: 1))));

        var clearIdentity = "https://example.test/large.p21";
        var clearProvider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            [clearIdentity] = ClearText(
                clearIdentity,
                Exchange("ANCHOR;<target>=#1;ENDSEC;", string.Empty, "#1=NODE('large',$);")),
        });
        var clearBytes = Assert.Throws<ExchangeStructureCapabilityException>(() => Read(
            Exchange(string.Empty, $"REFERENCE;#90=<{clearIdentity}#target>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(
                resourceProvider: clearProvider,
                resourceLimits: new Part21ResourceLimits(maximumTotalBytes: 5))));

        var directoryIdentity = "https://example.test/large/";
        var directoryProvider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            [directoryIdentity] = new(
                new Uri(directoryIdentity),
                new Dictionary<string, ReadOnlyMemory<byte>>
                {
                    ["ISO-10303.p21"] = Utf8(Exchange(string.Empty, string.Empty, "#1=NODE('large',$);")),
                }),
        });
        var directoryBytes = Assert.Throws<ExchangeStructureCapabilityException>(() => Read(
            Exchange(string.Empty, $"REFERENCE;#90=<{directoryIdentity}#target>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(
                resourceProvider: directoryProvider,
                resourceLimits: new Part21ResourceLimits(maximumTotalBytes: 5))));

        var convertedIdentity = "https://example.test/expanded.jt";
        var convertedProvider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            [convertedIdentity] = new(
                new Uri(convertedIdentity),
                Part21ResourceContentKind.Other,
                Utf8("x")),
        });
        var converted = new StaticConverter(ClearText(
            "https://example.test/expanded.p21",
            Exchange("ANCHOR;<target>=#1;ENDSEC;", string.Empty, "#1=NODE('expanded',$);")));
        var convertedBytes = Assert.Throws<ExchangeStructureCapabilityException>(() => Read(
            Exchange(string.Empty, $"REFERENCE;#90=<{convertedIdentity}#target>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(
                resourceProvider: convertedProvider,
                resourceConverter: converted,
                resourceLimits: new Part21ResourceLimits(maximumTotalBytes: 5))));

        var archiveIdentity = "https://example.test/large.zip";
        var archive = CreateZip(new Dictionary<string, string>
        {
            ["ISO-10303.p21"] = Exchange(
                string.Empty,
                string.Empty,
                $"#1=NODE('{new string('A', 1000)}',$);"),
        });
        var archiveProvider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            [archiveIdentity] = new(new Uri(archiveIdentity), Part21ResourceContentKind.ZipArchive, archive),
        });
        var uncompressedBytes = Assert.Throws<ExchangeStructureCapabilityException>(() => Read(
            Exchange(string.Empty, $"REFERENCE;#90=<{archiveIdentity}#target>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(
                resourceProvider: archiveProvider,
                resourceLimits: new Part21ResourceLimits(maximumArchiveUncompressedBytes: 100))));
        var compressionRatio = Assert.Throws<ExchangeStructureCapabilityException>(() => Read(
            Exchange(string.Empty, $"REFERENCE;#90=<{archiveIdentity}#target>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(
                resourceProvider: archiveProvider,
                resourceLimits: new Part21ResourceLimits(maximumCompressionRatio: 1))));

        using (Assert.Multiple())
        {
            await Assert.That(referenceDepth.Diagnostics.Single().Code).IsEqualTo("P21-RESOURCE-LIMIT");
            await Assert.That(clearBytes.Diagnostics.Single().Code).IsEqualTo("P21-RESOURCE-LIMIT");
            await Assert.That(directoryBytes.Diagnostics.Single().Code).IsEqualTo("P21-RESOURCE-LIMIT");
            await Assert.That(convertedBytes.Diagnostics.Single().Code).IsEqualTo("P21-RESOURCE-LIMIT");
            await Assert.That(uncompressedBytes.Diagnostics.Single().Code).IsEqualTo("P21-RESOURCE-LIMIT");
            await Assert.That(compressionRatio.Diagnostics.Single().Code)
                .IsEqualTo("P21-RESOURCE-LIMIT-COMPRESSION-RATIO");
        }
    }

    /// <summary>Charges every supplied canonical alias representation before cache reuse.</summary>
    [Test]
    public async Task Should_charge_clear_directory_zip_and_converted_canonical_alias_bytes()
    {
        var childSource = Exchange("ANCHOR;<target>=#1;ENDSEC;", string.Empty, "#1=NODE('canonical',$);");
        var childBytes = Utf8(childSource);
        var archive = CreateZip(new Dictionary<string, string> { ["ISO-10303.p21"] = childSource });

        var clearFailure = ReadCanonicalAliasesPastLimit(
            "https://example.test/clear-a.p21",
            "https://example.test/clear-b.p21",
            ClearText("https://example.test/clear-canonical.p21", childSource),
            ClearText("https://example.test/clear-canonical.p21", childSource),
            maximumTotalBytes: childBytes.Length * 2L - 1);
        var directoryFailure = ReadCanonicalAliasesPastLimit(
            "https://example.test/directory-a/",
            "https://example.test/directory-b/",
            new Part21ResourceContent(
                new Uri("https://example.test/directory-canonical/"),
                new Dictionary<string, ReadOnlyMemory<byte>> { ["ISO-10303.p21"] = childBytes }),
            new Part21ResourceContent(
                new Uri("https://example.test/directory-canonical/"),
                new Dictionary<string, ReadOnlyMemory<byte>> { ["ISO-10303.p21"] = childBytes }),
            maximumTotalBytes: childBytes.Length * 2L - 1);
        var zipFailure = ReadCanonicalAliasesPastLimit(
            "https://example.test/archive-a.zip",
            "https://example.test/archive-b.zip",
            new Part21ResourceContent(
                new Uri("https://example.test/archive-canonical.zip"),
                Part21ResourceContentKind.ZipArchive,
                archive),
            new Part21ResourceContent(
                new Uri("https://example.test/archive-canonical.zip"),
                Part21ResourceContentKind.ZipArchive,
                archive),
            maximumTotalBytes: archive.Length * 2L - 1);

        const string convertedA = "https://example.test/converted-a.jt";
        const string convertedB = "https://example.test/converted-b.jt";
        var convertedProvider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            [convertedA] = new(new Uri(convertedA), Part21ResourceContentKind.Other, Utf8("a")),
            [convertedB] = new(new Uri(convertedB), Part21ResourceContentKind.Other, Utf8("b")),
        });
        var converter = new StaticConverter(ClearText("https://example.test/converted-canonical.p21", childSource));
        var convertedFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => Read(
            Exchange(
                string.Empty,
                $"REFERENCE;#90=<{convertedA}#target>;#91=<{convertedB}#target>;ENDSEC;",
                "#1=HOLDER(#90);#2=HOLDER(#91);"),
            new ExchangeStructureReadOptions(
                resourceProvider: convertedProvider,
                resourceConverter: converter,
                resourceLimits: new Part21ResourceLimits(maximumTotalBytes: childBytes.Length + 2L))));

        await Assert.That(new[] { clearFailure, directoryFailure, zipFailure, convertedFailure }
                .Select(failure => failure.Diagnostics.Single().Code))
            .IsEquivalentTo(Enumerable.Repeat("P21-RESOURCE-LIMIT", 4));
    }

    private static ExchangeStructureCapabilityException ReadCanonicalAliasesPastLimit(
        string firstAlias,
        string secondAlias,
        Part21ResourceContent firstContent,
        Part21ResourceContent secondContent,
        long maximumTotalBytes)
    {
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            [firstAlias] = firstContent,
            [secondAlias] = secondContent,
        });
        return Assert.Throws<ExchangeStructureCapabilityException>(() => Read(
            Exchange(
                string.Empty,
                $"REFERENCE;#90=<{firstAlias}#target>;#91=<{secondAlias}#target>;ENDSEC;",
                "#1=HOLDER(#90);#2=HOLDER(#91);"),
            new ExchangeStructureReadOptions(
                resourceProvider: provider,
                resourceLimits: new Part21ResourceLimits(maximumTotalBytes: maximumTotalBytes))));
    }

    /// <summary>Reconciles provider aliases to one canonical model and terminates canonical self-cycles.</summary>
    [Test]
    public async Task Should_share_canonical_provider_identity_and_resolve_canonical_cycles_to_null()
    {
        const string canonical = "https://example.test/canonical.p21";
        const string canonicalLoop = "https://example.test/canonical-loop.p21";
        var shared = Exchange("ANCHOR;<target>=#1;ENDSEC;", string.Empty, "#1=NODE('shared',$);");
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            ["https://example.test/alias-a.p21"] = ClearText(canonical, shared),
            ["https://example.test/alias-b.p21"] = ClearText(canonical, shared),
            ["https://example.test/alias-loop.p21"] = ClearText(
                canonicalLoop,
                Exchange($"ANCHOR;<loop>=<{canonicalLoop}#loop>;ENDSEC;", string.Empty, "#1=NODE('loop',$);")),
        });
        var structure = Read(
            Exchange(
                string.Empty,
                "REFERENCE;#90=<https://example.test/alias-a.p21#target>;#91=<https://example.test/alias-b.p21#target>;#92=<https://example.test/alias-loop.p21#loop>;ENDSEC;",
                "#1=HOLDER(#90);#2=HOLDER(#91);#3=HOLDER(#92);"),
            new ExchangeStructureReadOptions(resourceProvider: provider));

        var holders = structure.Registrations
            .OrderBy(item => item.Name.CanonicalDigits, StringComparer.Ordinal)
            .Select(item => item.Entity)
            .ToArray();
        var first = holders[0].GetType().GetProperty("Target")!.GetValue(holders[0]);
        var second = holders[1].GetType().GetProperty("Target")!.GetValue(holders[1]);
        var loop = holders[2].GetType().GetProperty("Target")!.GetValue(holders[2]);

        using (Assert.Multiple())
        {
            await Assert.That(first).IsNotNull();
            await Assert.That(second).IsSameReferenceAs(first);
            await Assert.That(loop).IsNull();
            await Assert.That(provider.Requests).IsEquivalentTo([
                "https://example.test/alias-a.p21",
                "https://example.test/alias-b.p21",
                "https://example.test/alias-loop.p21",
            ]);
        }
        _ = structure.References.Remove(structure.References.Single(reference => reference.CanonicalDigits == "90"));
        using var output = new StringWriter();
        structure.Write(output);
        using (Assert.Multiple())
        {
            await Assert.That(structure.Validate().IsValid).IsTrue();
            await Assert.That(output.ToString()).Contains("#1=HOLDER(#91);");
            await Assert.That(output.ToString()).DoesNotContain("#90=<");
        }
    }

    /// <summary>Rolls back descendants loaded through an invalid ancestor before later direct reuse.</summary>
    [Test]
    public async Task Should_rollback_transitive_documents_when_an_ancestor_fails_validation()
    {
        const string aIdentity = "https://example.test/a-invalid.p21";
        const string bIdentity = "https://example.test/b-valid.p21";
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            [aIdentity] = ClearText(
                aIdentity,
                Exchange(
                    "ANCHOR;<target>=#1;ENDSEC;",
                    $"REFERENCE;#90=<{bIdentity}#target>;ENDSEC;",
                    "#1=NODE('invalid',#90);")),
            [bIdentity] = ClearText(
                bIdentity,
                Exchange(
                    "ANCHOR;<target>=#1;ENDSEC;",
                    $"REFERENCE;#90=<{aIdentity}#target>;ENDSEC;",
                    "#1=NODE('valid b',#90);")),
        });

        var structure = Read(
            Exchange(
                string.Empty,
                $"REFERENCE;#90=<{aIdentity}#target>;#91=<{bIdentity}#target>;ENDSEC;",
                "#1=HOLDER(#90);#2=HOLDER(#91);"),
            new ExchangeStructureReadOptions(resourceProvider: provider));
        var holders = structure.Registrations
            .OrderBy(registration => registration.Name.CanonicalDigits, StringComparer.Ordinal)
            .Select(registration => registration.Entity)
            .ToArray();
        var firstTarget = holders[0].GetType().GetProperty("Target")!.GetValue(holders[0]);
        var secondTarget = holders[1].GetType().GetProperty("Target")!.GetValue(holders[1]);
        var secondNext = secondTarget!.GetType().GetProperty("NextNode")!.GetValue(secondTarget);

        using (Assert.Multiple())
        {
            await Assert.That(structure.References.Single(reference => reference.CanonicalDigits == "90").ResolutionStatus)
                .IsEqualTo(Part21ReferenceResolutionStatus.Null);
            await Assert.That(structure.References.Single(reference => reference.CanonicalDigits == "91").ResolutionStatus)
                .IsEqualTo(Part21ReferenceResolutionStatus.Resolved);
            await Assert.That(firstTarget).IsNull();
            await Assert.That(secondNext).IsNull();
            await Assert.That(provider.Requests.Count(request => request == aIdentity)).IsEqualTo(1);
            await Assert.That(provider.Requests.Count(request => request == bIdentity)).IsEqualTo(2);
            await Assert.That(structure.Validate().IsValid).IsTrue();
        }
    }

    /// <summary>Rejects ZIP features excluded from the Annex A.4 PKZip 2.04g transport.</summary>
    [Test]
    public async Task Should_reject_zip64_encryption_unicode_names_and_deflate64()
    {
        var valid = CreateZip(new Dictionary<string, string>
        {
            ["ISO-10303.p21"] = Exchange(string.Empty, string.Empty, "#1=NODE('root',$);"),
        });
        var invalidArchives = new[]
        {
            MutateZip(
                valid,
                versionNeeded: 45,
                compressedSize: uint.MaxValue,
                uncompressedSize: uint.MaxValue),
            MutateZip(valid, flags: 0x0001),
            MutateZip(valid, flags: 0x0800),
            MutateZip(valid, method: 9),
            InjectExtraField(valid, 0x0001),
            InjectExtraField(valid, 0x7075),
        };
        var codes = invalidArchives.Select((archive, index) =>
        {
            var identity = $"https://example.test/invalid-{index}.zip";
            var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
            {
                [identity] = new(new Uri(identity), Part21ResourceContentKind.ZipArchive, archive),
            });
            return Assert.Throws<ExchangeStructureCapabilityException>(() => Read(
                Exchange(string.Empty, $"REFERENCE;#90=<{identity}#target>;ENDSEC;", "#1=HOLDER(#90);"),
                new ExchangeStructureReadOptions(resourceProvider: provider))).Diagnostics.Single().Code;
        }).ToArray();

        await Assert.That(codes).IsEquivalentTo(Enumerable.Repeat("P21-RESOURCE-ARCHIVE-FORMAT", 6));
    }

    /// <summary>Rejects normalized root aliases, corrupt payloads, size lies, and offsets outside the archive.</summary>
    [Test]
    public async Task Should_reject_deceptive_roots_and_invalid_archive_integrity()
    {
        var source = Exchange(string.Empty, string.Empty, "#1=NODE('root',$);");
        var valid = CreateZip(new Dictionary<string, string> { ["ISO-10303.p21"] = source });
        var resources = new Dictionary<string, Part21ResourceContent>
        {
            ["https://example.test/deceptive.zip"] = new(
                new Uri("https://example.test/deceptive.zip"),
                Part21ResourceContentKind.ZipArchive,
                CreateZip(new Dictionary<string, string> { ["./ISO-10303.p21"] = source })),
            ["https://example.test/deceptive/"] = new(
                new Uri("https://example.test/deceptive/"),
                new Dictionary<string, ReadOnlyMemory<byte>> { ["folder/../ISO-10303.p21"] = Utf8(source) }),
            ["https://example.test/crc.zip"] = new(
                new Uri("https://example.test/crc.zip"),
                Part21ResourceContentKind.ZipArchive,
                MutateZip(valid, crc: 0)),
            ["https://example.test/length.zip"] = new(
                new Uri("https://example.test/length.zip"),
                Part21ResourceContentKind.ZipArchive,
                MutateZip(valid, uncompressedSize: 1)),
            ["https://example.test/offset.zip"] = new(
                new Uri("https://example.test/offset.zip"),
                Part21ResourceContentKind.ZipArchive,
                MutateZip(valid, localOffset: 0x80000000)),
            ["https://example.test/payload.zip"] = new(
                new Uri("https://example.test/payload.zip"),
                Part21ResourceContentKind.ZipArchive,
                MutateZip(valid, compressedSize: (uint)valid.Length)),
        };

        var codes = resources.Select(resource =>
        {
            var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
            {
                [resource.Key] = resource.Value,
            });
            return Assert.Throws<ExchangeStructureCapabilityException>(() => Read(
                Exchange(string.Empty, $"REFERENCE;#90=<{resource.Key}#target>;ENDSEC;", "#1=HOLDER(#90);"),
                new ExchangeStructureReadOptions(resourceProvider: provider))).Diagnostics.Single().Code;
        }).ToArray();

        await Assert.That(codes).IsEquivalentTo([
            "P21-RESOURCE-ARCHIVE-ROOT",
            "P21-RESOURCE-ARCHIVE-ROOT",
            "P21-RESOURCE-ARCHIVE-FORMAT",
            "P21-RESOURCE-ARCHIVE-FORMAT",
            "P21-RESOURCE-ARCHIVE-FORMAT",
            "P21-RESOURCE-ARCHIVE-FORMAT",
        ]);
    }

    /// <summary>Accepts valid ZIP data descriptors and rejects descriptor or local-range corruption.</summary>
    [Test]
    public async Task Should_validate_zip_data_descriptors_and_non_overlapping_local_ranges()
    {
        var source = Exchange("ANCHOR;<target>=#1;ENDSEC;", string.Empty, "#1=NODE('root',$);");
        var valid = CreateZip(new Dictionary<string, string> { ["ISO-10303.p21"] = source });
        var signedIdentity = "https://example.test/signed-descriptor.zip";
        var unsignedIdentity = "https://example.test/unsigned-descriptor.zip";
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            [signedIdentity] = new(
                new Uri(signedIdentity),
                Part21ResourceContentKind.ZipArchive,
                AddDataDescriptor(valid, includeSignature: true)),
            [unsignedIdentity] = new(
                new Uri(unsignedIdentity),
                Part21ResourceContentKind.ZipArchive,
                AddDataDescriptor(valid, includeSignature: false)),
        });
        var resolved = Read(
            Exchange(
                string.Empty,
                $"REFERENCE;#90=<{signedIdentity}#target>;#91=<{unsignedIdentity}#target>;ENDSEC;",
                "#1=HOLDER(#90);#2=HOLDER(#91);"),
            new ExchangeStructureReadOptions(resourceProvider: provider));

        var overlap = CreateZip(new Dictionary<string, string>
        {
            ["ISO-10303.p21"] = source,
            ["OTHER-103.p21"] = source,
        });
        var invalidArchives = new[]
        {
            AddDataDescriptor(valid, includeSignature: true, corruptValues: true),
            AddDataDescriptor(valid, includeSignature: false, descriptorValueLength: 8),
            OverlapSecondLocalEntry(overlap),
        };
        var failures = invalidArchives.Select((archive, index) =>
        {
            var identity = $"https://example.test/descriptor-invalid-{index}.zip";
            var invalidProvider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
            {
                [identity] = new(new Uri(identity), Part21ResourceContentKind.ZipArchive, archive),
            });
            return Assert.Throws<ExchangeStructureCapabilityException>(() => Read(
                Exchange(string.Empty, $"REFERENCE;#90=<{identity}#target>;ENDSEC;", "#1=HOLDER(#90);"),
                new ExchangeStructureReadOptions(resourceProvider: invalidProvider)));
        }).ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(resolved.References.All(reference =>
                    reference.ResolutionStatus == Part21ReferenceResolutionStatus.Resolved))
                .IsTrue();
            await Assert.That(failures.Select(failure => failure.Diagnostics.Single().Code))
                .IsEquivalentTo(Enumerable.Repeat("P21-RESOURCE-ARCHIVE-FORMAT", 3));
            await Assert.That(failures[2].Diagnostics.Single().Message)
                .Contains("ranges overlap");
        }
    }

    /// <summary>Uses a UUID registry, nulls an invalid external schema, and preserves type mismatch evidence.</summary>
    [Test]
    public async Task Should_use_uuid_registry_null_invalid_external_schema_and_report_type_mismatch()
    {
        const string uuid = "97c6e1f0-3544-11e5-a2cb-0800200c9a66";
        const string nonUuidNeighbor = "97c6e1f0354411e5a2cb0800200c9a66";
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            ["#" + uuid] = ClearText(
                "https://example.test/registry-result.p21",
                Exchange($"ANCHOR;<{uuid}>=#1;ENDSEC;", string.Empty, "#1=NODE('uuid',$);")),
            ["https://example.test/wrong.p21"] = ClearText(
                "https://example.test/wrong.p21",
                Exchange("ANCHOR;<target>=#1;ENDSEC;", string.Empty, "#1=OTHER('wrong type');")),
            ["https://example.test/wrong-schema.p21"] = ClearText(
                "https://example.test/wrong-schema.p21",
                Exchange("ANCHOR;<target>=#1;ENDSEC;", string.Empty, "#1=NODE('wrong schema',$);")
                    .Replace("distributed_resource", "unknown_schema", StringComparison.Ordinal)),
        });
        var resolved = Read(
            Exchange(
                $"ANCHOR;<{nonUuidNeighbor}>=#1;ENDSEC;",
                $"REFERENCE;#90=<#{uuid}>;#91=<#{nonUuidNeighbor}>;ENDSEC;",
                "#1=NODE('local neighbor',$);#2=HOLDER(#90);#3=HOLDER(#91);"),
            new ExchangeStructureReadOptions(resourceProvider: provider));
        var mismatch = Assert.Throws<ExchangeStructureReadValidationException>(() => Read(
            Exchange(string.Empty, "REFERENCE;#90=<https://example.test/wrong.p21#target>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(resourceProvider: provider)));
        var schemaMismatch = Read(
            Exchange(string.Empty, "REFERENCE;#90=<https://example.test/wrong-schema.p21#target>;ENDSEC;", "#1=HOLDER(#90);"),
            new ExchangeStructureReadOptions(resourceProvider: provider));

        using (Assert.Multiple())
        {
            await Assert.That(resolved.References.Single(reference => reference.CanonicalDigits == "90").ResolutionStatus)
                .IsEqualTo(Part21ReferenceResolutionStatus.Resolved);
            await Assert.That(provider.Requests).Contains("#" + uuid);
            await Assert.That(provider.Requests).DoesNotContain("#" + nonUuidNeighbor);
            await Assert.That(resolved.Registrations.Single(item => item.Name.CanonicalDigits == "3").Entity
                    .GetType().GetProperty("Target")!.GetValue(
                        resolved.Registrations.Single(item => item.Name.CanonicalDigits == "3").Entity))
                .IsSameReferenceAs(resolved.Registrations.Single(item => item.Name.CanonicalDigits == "1").Entity);
            await Assert.That(mismatch.ValidationResult.Failures.Single().Code)
                .IsEqualTo("P21.READ.REFERENCE.TYPE");
            await Assert.That(schemaMismatch.References.Single().ResolutionStatus)
                .IsEqualTo(Part21ReferenceResolutionStatus.Null);
            await Assert.That(schemaMismatch.Registrations.Single().Entity
                    .GetType().GetProperty("Target")!.GetValue(schemaMismatch.Registrations.Single().Entity))
                .IsNull();
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

    private static string Exchange(
        string anchors,
        string references,
        string records,
        string? implementationLevel = null)
    {
        implementationLevel ??= references.Contains('@') ? "4;3"
            : references.Length > 0 ? "4;2"
            : "4;1";
        return $$"""
        ISO-10303-21;
        HEADER;
        FILE_DESCRIPTION(('distributed resource test'),'{{implementationLevel}}');
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
    }

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

    private static ReadOnlyMemory<byte> AddDataDescriptor(
        ReadOnlyMemory<byte> archive,
        bool includeSignature,
        int descriptorValueLength = 12,
        bool corruptValues = false)
    {
        var bytes = archive.ToArray();
        var local = FindSignature(bytes, [0x50, 0x4b, 0x03, 0x04]);
        var central = FindSignature(bytes, [0x50, 0x4b, 0x01, 0x02]);
        var descriptorValues = new byte[12];
        WriteUInt32(descriptorValues, 0, ReadUInt32(bytes, central + 16));
        WriteUInt32(descriptorValues, 4, ReadUInt32(bytes, central + 20));
        WriteUInt32(descriptorValues, 8, ReadUInt32(bytes, central + 24));
        if (corruptValues)
            descriptorValues[0] ^= 0xff;

        var descriptor = new byte[(includeSignature ? 4 : 0) + descriptorValueLength];
        var valueOffset = 0;
        if (includeSignature)
        {
            WriteUInt32(descriptor, 0, 0x08074b50);
            valueOffset = 4;
        }
        Buffer.BlockCopy(descriptorValues, 0, descriptor, valueOffset, descriptorValueLength);

        WriteUInt16(bytes, local + 6, (ushort)(ReadUInt16(bytes, local + 6) | 0x0008));
        WriteUInt16(bytes, central + 8, (ushort)(ReadUInt16(bytes, central + 8) | 0x0008));
        WriteUInt32(bytes, local + 14, 0);
        WriteUInt32(bytes, local + 18, 0);
        WriteUInt32(bytes, local + 22, 0);
        bytes = InsertBytes(bytes, central, descriptor);

        var end = FindSignature(bytes, [0x50, 0x4b, 0x05, 0x06]);
        WriteUInt32(bytes, end + 16, ReadUInt32(bytes, end + 16) + (uint)descriptor.Length);
        return bytes;
    }

    private static ReadOnlyMemory<byte> OverlapSecondLocalEntry(ReadOnlyMemory<byte> archive)
    {
        var bytes = archive.ToArray();
        var firstCentral = FindSignature(bytes, [0x50, 0x4b, 0x01, 0x02]);
        var secondCentral = FindSignature(bytes, [0x50, 0x4b, 0x01, 0x02], firstCentral + 4);
        var firstNameLength = ReadUInt16(bytes, firstCentral + 28);
        var secondNameLength = ReadUInt16(bytes, secondCentral + 28);
        if (firstNameLength != secondNameLength)
            throw new InvalidOperationException("The overlap fixture requires equal-length entry names.");
        Buffer.BlockCopy(bytes, firstCentral + 46, bytes, secondCentral + 46, firstNameLength);
        WriteUInt32(bytes, secondCentral + 42, ReadUInt32(bytes, firstCentral + 42));
        return bytes;
    }

    private static ReadOnlyMemory<byte> MutateZip(
        ReadOnlyMemory<byte> archive,
        ushort? versionNeeded = null,
        ushort? flags = null,
        ushort? method = null,
        uint? crc = null,
        uint? compressedSize = null,
        uint? uncompressedSize = null,
        uint? localOffset = null)
    {
        var bytes = archive.ToArray();
        var local = FindSignature(bytes, [0x50, 0x4b, 0x03, 0x04]);
        var central = FindSignature(bytes, [0x50, 0x4b, 0x01, 0x02]);
        if (versionNeeded is { } version)
        {
            WriteUInt16(bytes, local + 4, version);
            WriteUInt16(bytes, central + 6, version);
        }
        if (flags is { } generalFlags)
        {
            WriteUInt16(bytes, local + 6, generalFlags);
            WriteUInt16(bytes, central + 8, generalFlags);
        }
        if (method is { } compressionMethod)
        {
            WriteUInt16(bytes, local + 8, compressionMethod);
            WriteUInt16(bytes, central + 10, compressionMethod);
        }
        if (crc is { } expectedCrc)
        {
            WriteUInt32(bytes, local + 14, expectedCrc);
            WriteUInt32(bytes, central + 16, expectedCrc);
        }
        if (compressedSize is { } declaredCompressedSize)
        {
            WriteUInt32(bytes, local + 18, declaredCompressedSize);
            WriteUInt32(bytes, central + 20, declaredCompressedSize);
        }
        if (uncompressedSize is { } declaredUncompressedSize)
        {
            WriteUInt32(bytes, local + 22, declaredUncompressedSize);
            WriteUInt32(bytes, central + 24, declaredUncompressedSize);
        }
        if (localOffset is { } declaredLocalOffset)
            WriteUInt32(bytes, central + 42, declaredLocalOffset);
        return bytes;
    }

    private static ReadOnlyMemory<byte> InjectExtraField(ReadOnlyMemory<byte> archive, ushort identifier)
    {
        var bytes = archive.ToArray();
        var field = new byte[4];
        WriteUInt16(field, 0, identifier);

        var local = FindSignature(bytes, [0x50, 0x4b, 0x03, 0x04]);
        var localNameLength = ReadUInt16(bytes, local + 26);
        var localExtraLength = ReadUInt16(bytes, local + 28);
        WriteUInt16(bytes, local + 28, checked((ushort)(localExtraLength + field.Length)));
        bytes = InsertBytes(bytes, local + 30 + localNameLength + localExtraLength, field);

        var central = FindSignature(bytes, [0x50, 0x4b, 0x01, 0x02]);
        var centralNameLength = ReadUInt16(bytes, central + 28);
        var centralExtraLength = ReadUInt16(bytes, central + 30);
        WriteUInt16(bytes, central + 30, checked((ushort)(centralExtraLength + field.Length)));
        bytes = InsertBytes(bytes, central + 46 + centralNameLength + centralExtraLength, field);

        var end = FindSignature(bytes, [0x50, 0x4b, 0x05, 0x06]);
        WriteUInt32(bytes, end + 12, ReadUInt32(bytes, end + 12) + (uint)field.Length);
        WriteUInt32(bytes, end + 16, ReadUInt32(bytes, end + 16) + (uint)field.Length);
        return bytes;
    }

    private static byte[] InsertBytes(byte[] source, int offset, byte[] inserted)
    {
        var result = new byte[source.Length + inserted.Length];
        Buffer.BlockCopy(source, 0, result, 0, offset);
        Buffer.BlockCopy(inserted, 0, result, offset, inserted.Length);
        Buffer.BlockCopy(source, offset, result, offset + inserted.Length, source.Length - offset);
        return result;
    }

    private static int FindSignature(byte[] bytes, ReadOnlySpan<byte> signature, int start = 0)
    {
        for (var index = start; index <= bytes.Length - signature.Length; index++)
        {
            if (bytes.AsSpan(index, signature.Length).SequenceEqual(signature))
                return index;
        }
        throw new InvalidOperationException("The ZIP fixture does not contain the expected record.");
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
        bytes[offset + 2] = (byte)(value >> 16);
        bytes[offset + 3] = (byte)(value >> 24);
    }

    private static ushort ReadUInt16(byte[] bytes, int offset) =>
        (ushort)(bytes[offset] | bytes[offset + 1] << 8);

    private static uint ReadUInt32(byte[] bytes, int offset) =>
        (uint)(bytes[offset]
            | bytes[offset + 1] << 8
            | bytes[offset + 2] << 16
            | bytes[offset + 3] << 24);

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

    private sealed class ReenteringConverter(Func<ExchangeStructure> reenter) : IPart21ResourceConverter
    {
        public Part21ResourceContent Convert(Part21ResourceContent content)
        {
            _ = reenter();
            return content;
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
