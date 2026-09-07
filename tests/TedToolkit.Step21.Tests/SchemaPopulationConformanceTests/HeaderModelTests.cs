using Microsoft.CodeAnalysis;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.SchemaPopulationConformanceTests;

/// <summary>Proves standard schema-population declarations before resource-graph expansion.</summary>
public sealed class HeaderModelTests
{
    private const string SCHEMA = """
        SCHEMA population_model;
        ENTITY node;
          name : STRING;
        END_ENTITY;
        RULE at_most_one FOR (node);
        WHERE
          single : SIZEOF(node) <= 1;
        END_RULE;
        END_SCHEMA;
        """;

    private const string OTHER_SCHEMA = """
        SCHEMA other_model;
        ENTITY alternate;
          name : STRING;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string LONGA_SCHEMA = """
        SCHEMA longa;
        ENTITY a;
          range : REAL;
        END_ENTITY;
        ENTITY b;
          name : STRING;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string LONGB_SCHEMA = """
        SCHEMA longb;
        ENTITY a;
          range : REAL;
        END_ENTITY;
        ENTITY b;
          name : STRING;
        END_ENTITY;
        ENTITY c;
          addressed_item : b;
          address : STRING;
        END_ENTITY;
        RULE no_a FOR (a);
        WHERE
          none : SIZEOF(a) = 0;
        END_RULE;
        END_SCHEMA;
        """;

    /// <summary>Retains public SCHEMA_POPULATION and FILE_POPULATION models through canonical writing.</summary>
    [Test]
    public async Task Should_read_edit_write_and_reread_population_declarations()
    {
        var descriptor = CreateDescriptor();
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>());
        var options = new ExchangeStructureReadOptions(
            new Uri("https://example.test/population/root.p21"),
            provider);
        var structure = ExchangeStructure.Read(
            new StringReader(Exchange("""
                SCHEMA_POPULATION((('child.p21','2026-09-07T01:00:00Z',$),
                  ('https://example.test/other.p21',$,$)));
                FILE_POPULATION('population_model','SECTION_BOUNDARY',('main'));
                """)),
            [descriptor],
            options);

        using (Assert.Multiple())
        {
            await Assert.That(structure.SchemaPopulation.Count).IsEqualTo(2);
            await Assert.That(structure.SchemaPopulation[0].Location.OriginalString).IsEqualTo("child.p21");
            await Assert.That(structure.SchemaPopulation[0].TimeStamp).IsEqualTo("2026-09-07T01:00:00Z");
            await Assert.That(structure.SchemaPopulation[0].TimestampStatus)
                .IsEqualTo(SchemaPopulationTimestampStatus.NotVerified);
            await Assert.That(structure.SchemaPopulation[1].MessageDigest).IsNull();
            await Assert.That(structure.SchemaPopulation[1].DigestStatus)
                .IsEqualTo(SchemaPopulationDigestStatus.NotProvided);
            await Assert.That(structure.FilePopulations.Count).IsEqualTo(1);
            await Assert.That(structure.FilePopulations[0].Determination)
                .IsEqualTo(SchemaPopulationDetermination.SectionBoundary);
            await Assert.That(structure.FilePopulations[0].GovernedSectionNames).IsEquivalentTo(["main"]);
        }

        structure.FilePopulations.Add(new SchemaPopulationDefinition(
            new SchemaName("population_model"),
            SchemaPopulationDetermination.IncludeReferenced));
        var writer = new StringWriter();
        structure.Write(writer);
        var output = writer.ToString();
        var reread = ExchangeStructure.Read(new StringReader(output), [descriptor], options);

        using (Assert.Multiple())
        {
            await Assert.That(output).Contains(
                "SCHEMA_POPULATION((('child.p21','2026-09-07T01:00:00Z',$),('https://example.test/other.p21',$,$)));\n");
            await Assert.That(output).Contains("FILE_POPULATION('population_model','INCLUDE_REFERENCED',$);\n");
            await Assert.That(reread.SchemaPopulation.Count).IsEqualTo(2);
            await Assert.That(reread.FilePopulations.Count).IsEqualTo(2);
        }
    }

    /// <summary>Requires an explicit resource capability before claiming conformance for an external population.</summary>
    [Test]
    public async Task Should_require_a_resource_provider_for_schema_population_reading()
    {
        var failure = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(Exchange("SCHEMA_POPULATION((('child.p21',$,$)));")),
            [CreateDescriptor()]));

        using (Assert.Multiple())
        {
            await Assert.That(failure.Diagnostics.Select(item => item.Code))
                .IsEquivalentTo(["P21-CAP-RESOURCE-PROVIDER"]);
            await Assert.That(failure.Diagnostics.Single().Message).Contains("SCHEMA_POPULATION");
        }
    }

    /// <summary>Aggregates malformed SCHEMA_POPULATION triples as located binding evidence.</summary>
    [Test]
    public async Task Should_reject_invalid_population_headers_atomically()
    {
        var descriptor = CreateDescriptor();
        var failure = Assert.Throws<ExchangeStructureBindingException>(() => ExchangeStructure.Read(
            new StringReader(Exchange("""
                SCHEMA_POPULATION((('child.p21','not-a-time',$),('other.p21',$,'not base64')));
                SCHEMA_POPULATION((('duplicate.p21',$,$)));
                """)),
            [descriptor]));

        using (Assert.Multiple())
        {
            await Assert.That(failure.Diagnostics.Count(diagnostic =>
                    diagnostic.Code == "P21-BIND-SCHEMA-POPULATION"))
                .IsEqualTo(3);
            await Assert.That(failure.Diagnostics.All(diagnostic => diagnostic.SourceLocation is
            { FilePath: "<reader>", Line: > 0, Column: > 0, })).IsTrue();
        }
    }

    /// <summary>Uses only ISO extended timestamps and canonical RFC 4648 Base64 in public population entries.</summary>
    [Test]
    public async Task Should_enforce_population_timestamp_and_digest_lexical_forms()
    {
        var location = new Uri("https://example.test/population.p21");
        var accepted = new[]
        {
            new SchemaPopulationExternalFile(location, "2026-09-07T00:00:00"),
            new SchemaPopulationExternalFile(location, "2026-09-07T24:00:00Z"),
            new SchemaPopulationExternalFile(location, "2024-02-29T23:59:60,125+08:00", "AQID"),
        };
        var localized = Assert.Throws<ArgumentException>(() =>
            _ = new SchemaPopulationExternalFile(location, "2026/09/07 00:00:00"));
        var compactOffset = Assert.Throws<ArgumentException>(() =>
            _ = new SchemaPopulationExternalFile(location, "2026-09-07T00:00:00+0800"));
        var invalidDay = Assert.Throws<ArgumentException>(() =>
            _ = new SchemaPopulationExternalFile(location, "2026-02-29T00:00:00Z"));
        var invalidEndOfDay = Assert.Throws<ArgumentException>(() =>
            _ = new SchemaPopulationExternalFile(location, "2026-09-07T24:00:01Z"));
        var whitespaceDigest = Assert.Throws<ArgumentException>(() =>
            _ = new SchemaPopulationExternalFile(location, messageDigest: "AQ ID"));
        var nonCanonicalDigest = Assert.Throws<ArgumentException>(() =>
            _ = new SchemaPopulationExternalFile(location, messageDigest: "AB=="));

        using (Assert.Multiple())
        {
            await Assert.That(accepted.Length).IsEqualTo(3);
            await Assert.That(localized.ParamName).IsEqualTo("timeStamp");
            await Assert.That(compactOffset.ParamName).IsEqualTo("timeStamp");
            await Assert.That(invalidDay.ParamName).IsEqualTo("timeStamp");
            await Assert.That(invalidEndOfDay.ParamName).IsEqualTo("timeStamp");
            await Assert.That(whitespaceDigest.ParamName).IsEqualTo("messageDigest");
            await Assert.That(nonCanonicalDigest.ParamName).IsEqualTo("messageDigest");
        }
    }

    /// <summary>Resolves explicit population resources and reports timestamp and missing-resource state.</summary>
    [Test]
    public async Task Should_resolve_population_resources_through_the_explicit_resource_graph()
    {
        const string childIdentity = "https://example.test/models/child.p21";
        const string grandchildIdentity = "https://example.test/models/grandchild.p21";
        var descriptor = CreateDescriptor();
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            [childIdentity] = new(
                new Uri(childIdentity),
                Part21ResourceContentKind.ClearText,
                System.Text.Encoding.UTF8.GetBytes(Exchange(
                        "SCHEMA_POPULATION((('grandchild.p21',$,$)));\n")
                    .Replace("2026-09-07T00:00:00Z", "2026-09-07T00:30:00Z", StringComparison.Ordinal))),
            [grandchildIdentity] = new(
                new Uri(grandchildIdentity),
                Part21ResourceContentKind.ClearText,
                System.Text.Encoding.UTF8.GetBytes(Exchange(string.Empty))),
        });
        var structure = ExchangeStructure.Read(
            new StringReader(Exchange("""
                SCHEMA_POPULATION((('child.p21','2026-09-07T01:00:00Z',$),('missing.p21',$,$)));
                """)),
            [descriptor],
            new ExchangeStructureReadOptions(
                new Uri("https://example.test/models/root.p21"),
                provider));

        using (Assert.Multiple())
        {
            await Assert.That(structure.SchemaPopulation[0].ResourceStatus)
                .IsEqualTo(SchemaPopulationResourceStatus.Resolved);
            await Assert.That(structure.SchemaPopulation[0].TimestampStatus)
                .IsEqualTo(SchemaPopulationTimestampStatus.Verified);
            await Assert.That(structure.SchemaPopulation[0].Structure).IsNotNull();
            await Assert.That(structure.SchemaPopulationEntities.Count()).IsEqualTo(3);
            await Assert.That(structure.SchemaPopulation[0].Structure!.SchemaPopulation[0].Structure).IsNotNull();
            await Assert.That(structure.SchemaPopulation[1].ResourceStatus)
                .IsEqualTo(SchemaPopulationResourceStatus.Missing);
            await Assert.That(provider.Requests).IsEquivalentTo([
                childIdentity,
                grandchildIdentity,
                "https://example.test/models/missing.p21",
            ]);
        }
    }

    /// <summary>Compares population timestamps without culture, local-time, or offset ambiguity.</summary>
    [Test]
    public async Task Should_verify_only_deterministically_later_population_timestamps()
    {
        const string rootIdentity = "https://example.test/timestamps/root.p21";
        var cases = new[]
        {
            (Name: "later-offset.p21", Visited: "2026-09-07T00:31:00+08:00", Created: "2026-09-07T00:30:00+08:00"),
            (Name: "equal.p21", Visited: "2026-09-07T00:30:00Z", Created: "2026-09-07T00:30:00Z"),
            (Name: "before.p21", Visited: "2026-09-07T00:29:59Z", Created: "2026-09-07T00:30:00Z"),
            (Name: "mixed-zone.p21", Visited: "2026-09-07T00:31:00", Created: "2026-09-07T00:30:00Z"),
            (Name: "fraction.p21", Visited: "2026-09-07T00:30:00.101", Created: "2026-09-07T00:30:00.100"),
            (Name: "normalized-offset.p21", Visited: "2026-09-06T16:31:00Z", Created: "2026-09-07T00:30:00+08:00"),
        };
        var resources = cases.ToDictionary(
            item => new Uri(new Uri(rootIdentity), item.Name).AbsoluteUri,
            item => new Part21ResourceContent(
                new Uri(new Uri(rootIdentity), item.Name),
                Part21ResourceContentKind.ClearText,
                System.Text.Encoding.UTF8.GetBytes(Exchange(string.Empty).Replace(
                    "2026-09-07T00:00:00Z",
                    item.Created,
                    StringComparison.Ordinal))),
            StringComparer.Ordinal);
        var entries = string.Join(",", cases.Select(item => $"('{item.Name}','{item.Visited}',$)"));

        var structure = ExchangeStructure.Read(
            new StringReader(Exchange($"SCHEMA_POPULATION(({entries}));")),
            [CreateDescriptor()],
            new ExchangeStructureReadOptions(new Uri(rootIdentity), new DictionaryProvider(resources)));

        await Assert.That(structure.SchemaPopulation.Select(item => item.TimestampStatus)).IsEquivalentTo([
            SchemaPopulationTimestampStatus.Verified,
            SchemaPopulationTimestampStatus.NotVerified,
            SchemaPopulationTimestampStatus.NotVerified,
            SchemaPopulationTimestampStatus.NotVerified,
            SchemaPopulationTimestampStatus.Verified,
            SchemaPopulationTimestampStatus.Verified,
        ]);
    }

    /// <summary>Uses the first signature's hash algorithm to verify referenced-file message digests.</summary>
    [Test]
    public async Task Should_verify_population_digests_and_reject_missing_signatures_or_mismatches()
    {
        const string rootIdentity = "https://example.test/digests/root.p21";
        const string childIdentity = "https://example.test/digests/child.p21";
        const string sha384Oid = "2.16.840.1.101.3.4.2.2";
        var child = Exchange(string.Empty);
        var correctDigest = Convert.ToBase64String(SHA384.HashData(EncodePart21Alphabet(child)));
        var wrongDigest = Convert.ToBase64String(new byte[SHA384.HashSizeInBytes]);
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            [childIdentity] = new(
                new Uri(childIdentity),
                Part21ResourceContentKind.ClearText,
                Encoding.UTF8.GetBytes(child)),
        });
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Schema Population Digest",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var validSource = Sign(
            Exchange($"SCHEMA_POPULATION(((\'child.p21\',$,\'{correctDigest}\')));"),
            new Part21CmsSigner(certificate, new Oid(sha384Oid)),
            new Part21CmsSigner(certificate));
        var valid = ExchangeStructure.Read(
            new StringReader(validSource),
            [CreateDescriptor()],
            new ExchangeStructureReadOptions(new Uri(rootIdentity), provider));
        var rewritten = new StringWriter();
        valid.Write(
            rewritten,
            new ExchangeStructureWriteOptions([
                new Part21CmsSigner(certificate, new Oid(sha384Oid)),
            ]));
        var reread = ExchangeStructure.Read(
            new StringReader(rewritten.ToString()),
            [CreateDescriptor()],
            new ExchangeStructureReadOptions(new Uri(rootIdentity), provider));
        var mismatch = Assert.Throws<ExchangeStructureReadValidationException>(() => ExchangeStructure.Read(
            new StringReader(Sign(
                Exchange($"SCHEMA_POPULATION(((\'child.p21\',$,\'{wrongDigest}\')));"),
                new Part21CmsSigner(certificate, new Oid(sha384Oid)))),
            [CreateDescriptor()],
            new ExchangeStructureReadOptions(new Uri(rootIdentity), provider)));
        var missingSignature = Assert.Throws<ExchangeStructureReadValidationException>(() => ExchangeStructure.Read(
            new StringReader(Exchange($"SCHEMA_POPULATION(((\'child.p21\',$,\'{correctDigest}\')));")),
            [CreateDescriptor()],
            new ExchangeStructureReadOptions(new Uri(rootIdentity), provider)));

        using (Assert.Multiple())
        {
            await Assert.That(valid.SchemaPopulation.Single().DigestStatus)
                .IsEqualTo(SchemaPopulationDigestStatus.Verified);
            await Assert.That(reread.SchemaPopulation.Single().DigestStatus)
                .IsEqualTo(SchemaPopulationDigestStatus.Verified);
            await Assert.That(mismatch.ValidationResult.Failures.Select(failure => failure.Code))
                .Contains("P21.STRUCTURE.SCHEMA_POPULATION.DIGEST.MISMATCH");
            await Assert.That(missingSignature.ValidationResult.Failures.Select(failure => failure.Code))
                .Contains("P21.STRUCTURE.SCHEMA_POPULATION.DIGEST.SIGNATURE_REQUIRED");
        }
    }

    /// <summary>Completes every structure's transitive population after a normative resource cycle closes.</summary>
    [Test]
    public async Task Should_complete_schema_populations_across_resource_cycles()
    {
        const string rootIdentity = "https://example.test/cycles/root.p21";
        const string childIdentity = "https://example.test/cycles/child.p21";
        var descriptor = CreateDescriptors().Single(candidate =>
            candidate.Name.Equals(new SchemaName("other_model")));
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            [childIdentity] = new(
                new Uri(childIdentity),
                Part21ResourceContentKind.ClearText,
                System.Text.Encoding.UTF8.GetBytes(OtherExchange(
                    "SCHEMA_POPULATION(((\'root.p21\',$,$)));"))),
        });

        var root = ExchangeStructure.Read(
            new StringReader(OtherExchange("SCHEMA_POPULATION(((\'child.p21\',$,$)));")),
            [descriptor],
            new ExchangeStructureReadOptions(new Uri(rootIdentity), provider));
        var child = root.SchemaPopulation.Single().Structure!;

        using (Assert.Multiple())
        {
            await Assert.That(root.SchemaPopulationEntities.Count()).IsEqualTo(2);
            await Assert.That(child.SchemaPopulationEntities.Count()).IsEqualTo(2);
            await Assert.That(child.SchemaPopulation.Single().Structure).IsSameReferenceAs(root);
            await Assert.That(provider.Requests).IsEquivalentTo([childIdentity]);
        }
    }

    /// <summary>Applies an all-sections FILE_POPULATION to the complete external schema population.</summary>
    [Test]
    public async Task Should_validate_file_population_against_external_sections()
    {
        const string childIdentity = "https://example.test/combined/child.p21";
        var descriptor = CreateDescriptor();
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            [childIdentity] = new(
                new Uri(childIdentity),
                Part21ResourceContentKind.ClearText,
                System.Text.Encoding.UTF8.GetBytes(Exchange(string.Empty))),
        });

        var failure = Assert.Throws<ExchangeStructureReadValidationException>(() => ExchangeStructure.Read(
            new StringReader(Exchange("""
                SCHEMA_POPULATION((('child.p21',$,$)));
                FILE_POPULATION('population_model','SECTION_BOUNDARY',$);
                """)),
            [descriptor],
            new ExchangeStructureReadOptions(
                new Uri("https://example.test/combined/root.p21"),
                provider)));

        await Assert.That(failure.ValidationResult.Failures.Select(item => item.Code))
            .Contains("POPULATION_MODEL.RULE.AT_MOST_ONE.WHERE.SINGLE");
    }

    /// <summary>Includes an exchange structure named by a reference URI even when the URI has no fragment.</summary>
    [Test]
    public async Task Should_include_fragmentless_reference_resources_in_the_schema_population()
    {
        const string childIdentity = "https://example.test/references/child.p21";
        var descriptor = CreateDescriptor();
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            [childIdentity] = new(
                new Uri(childIdentity),
                Part21ResourceContentKind.ClearText,
                System.Text.Encoding.UTF8.GetBytes(Exchange(string.Empty))),
        });
        var source = Exchange(string.Empty).Replace(
            "ENDSEC;\nDATA('main'",
            "ENDSEC;\nREFERENCE;\n#90=<child.p21>;\nENDSEC;\nDATA('main'",
            StringComparison.Ordinal);

        var structure = ExchangeStructure.Read(
            new StringReader(source),
            [descriptor],
            new ExchangeStructureReadOptions(
                new Uri("https://example.test/references/root.p21"),
                provider));

        using (Assert.Multiple())
        {
            await Assert.That(structure.References.Single().ResolutionStatus)
                .IsEqualTo(Part21ReferenceResolutionStatus.Null);
            await Assert.That(structure.SchemaPopulationEntities.Count()).IsEqualTo(2);
            await Assert.That(provider.Requests).IsEquivalentTo([childIdentity]);
        }
    }

    /// <summary>Uses a complete caller relation for cross-schema reference validity without name heuristics.</summary>
    [Test]
    public async Task Should_apply_explicit_domain_equivalence_to_external_reference_validity()
    {
        const string targetIdentity = "https://example.test/domain/target.p21";
        var descriptors = CreateDescriptors();
        var target = $$"""
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('domain target'),'4;2');
            FILE_NAME('target.p21','2026-09-07T00:00:00Z',('Author'),('Org'),'Pre','System','Auth');
            FILE_SCHEMA(('other_model'));
            ENDSEC;
            ANCHOR;
            <target>=#1;
            ENDSEC;
            DATA;
            #1=ALTERNATE('equivalent');
            ENDSEC;
            END-ISO-10303-21;
            """;
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            [targetIdentity] = new(
                new Uri(targetIdentity),
                Part21ResourceContentKind.ClearText,
                System.Text.Encoding.UTF8.GetBytes(target)),
        });
        var source = Exchange(string.Empty).Replace(
            "ENDSEC;\nDATA('main'",
            $"ENDSEC;\nREFERENCE;\n#90=<{targetIdentity}#target>;\nENDSEC;\nDATA('main'",
            StringComparison.Ordinal);
        var populationNode = new SchemaEntityType(new SchemaName("population_model"), "node");
        var otherAlternate = new SchemaEntityType(new SchemaName("other_model"), "alternate");
        SchemaDomainEquivalence[] equivalences = [
            new(populationNode, otherAlternate),
            new(otherAlternate, populationNode),
        ];

        var withoutEquivalence = ExchangeStructure.Read(
            new StringReader(source),
            descriptors,
            new ExchangeStructureReadOptions(resourceProvider: provider));
        var withEquivalence = ExchangeStructure.Read(
            new StringReader(source),
            descriptors,
            ExchangeStructureReadOptions.WithDomainEquivalenceProvider(
                new IdentityDomainEquivalenceProvider(equivalences),
                resourceProvider: provider));

        using (Assert.Multiple())
        {
            await Assert.That(withoutEquivalence.References.Single().ResolutionStatus)
                .IsEqualTo(Part21ReferenceResolutionStatus.Null);
            await Assert.That(withEquivalence.References.Single().ResolutionStatus)
                .IsEqualTo(Part21ReferenceResolutionStatus.Resolved);
            await Assert.That(withEquivalence.SchemaPopulationEntities.Count()).IsEqualTo(2);
        }
    }

    /// <summary>
    /// Applies Annex E.1.2 reference validity and E.2 governing-schema constraints to domain-equivalent instances.
    /// </summary>
    [Test]
    public async Task Should_hydrate_and_validate_the_annex_e_domain_equivalence_example()
    {
        var descriptors = CreateAnnexEDescriptors();
        var longaA = new SchemaEntityType(new SchemaName("longa"), "a");
        var longbA = new SchemaEntityType(new SchemaName("longb"), "a");
        var longaB = new SchemaEntityType(new SchemaName("longa"), "b");
        var longbB = new SchemaEntityType(new SchemaName("longb"), "b");
        SchemaDomainEquivalence[] equivalences = [
            new(longaA, longbA),
            new(longbA, longaA),
            new(longaB, longbB),
            new(longbB, longaB),
        ];
        var source = """
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('Annex E domain equivalence'),'4;2');
            FILE_NAME('annex-e.p21','2026-09-07T00:00:00Z',('Author'),('Org'),'Pre','System','Auth');
            FILE_SCHEMA(('LONGA','LONGB'));
            FILE_POPULATION('LONGB','INCLUDE_ALL_COMPATIBLE',('TWO'));
            ENDSEC;
            DATA('ONE',('LONGA'));
            #1=A(-3.5);
            #2=B('Sam Smith');
            ENDSEC;
            DATA('TWO',('LONGB'));
            #3=C(#2,'100 Main Street');
            ENDSEC;
            END-ISO-10303-21;
            """;

        var equivalenceProvider = new IdentityDomainEquivalenceProvider(equivalences);
        var failure = Assert.Throws<ExchangeStructureReadValidationException>(() => ExchangeStructure.Read(
            new StringReader(source),
            descriptors,
            ExchangeStructureReadOptions.WithDomainEquivalenceProvider(equivalenceProvider)));

        using (Assert.Multiple())
        {
            await Assert.That(string.Join("|", failure.ValidationResult.Failures.Select(item => item.Code)))
                .IsEqualTo("LONGB.RULE.NO_A.WHERE.NONE");
            await Assert.That(equivalenceProvider.ProjectionCount).IsGreaterThan(0);
        }
    }

    /// <summary>Rejects invalid caller parameter projections before publishing the exchange structure.</summary>
    [Test]
    public async Task Should_reject_invalid_domain_equivalence_parameter_projections_atomically()
    {
        var longaB = new SchemaEntityType(new SchemaName("longa"), "b");
        var longbB = new SchemaEntityType(new SchemaName("longb"), "b");
        var provider = new IdentityDomainEquivalenceProvider(
            [
                new SchemaDomainEquivalence(longaB, longbB),
                new SchemaDomainEquivalence(longbB, longaB),
            ],
            _ => [null!]);
        var source = """
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('invalid projection'),'4;2');
            FILE_NAME('invalid.p21','2026-09-07T00:00:00Z',('Author'),('Org'),'Pre','System','Auth');
            FILE_SCHEMA(('LONGA','LONGB'));
            ENDSEC;
            DATA('ONE',('LONGA'));
            #1=B('Sam Smith');
            ENDSEC;
            DATA('TWO',('LONGB'));
            #2=C(#1,'100 Main Street');
            ENDSEC;
            END-ISO-10303-21;
            """;

        var failure = Assert.Throws<ExchangeStructureBindingException>(() => ExchangeStructure.Read(
            new StringReader(source),
            CreateAnnexEDescriptors(),
            ExchangeStructureReadOptions.WithDomainEquivalenceProvider(provider)));

        await Assert.That(failure.Diagnostics.Select(diagnostic => diagnostic.Code))
            .IsEquivalentTo(["P21-BIND-DOMAIN-PROJECTION"]);
    }

    /// <summary>Rejects asymmetric, non-transitive, and unknown domain-equivalence metadata.</summary>
    [Test]
    public async Task Should_reject_invalid_domain_equivalence_relations()
    {
        var a = new SchemaEntityType(new SchemaName("a"), "item");
        var b = new SchemaEntityType(new SchemaName("b"), "item");
        var c = new SchemaEntityType(new SchemaName("c"), "item");
        var bOther = new SchemaEntityType(new SchemaName("b"), "other");
        var asymmetric = Assert.Throws<ArgumentException>(() =>
            _ = ExchangeStructureReadOptions.WithDomainEquivalenceProvider(new IdentityDomainEquivalenceProvider(
                [new SchemaDomainEquivalence(a, b)])));
        var nonTransitive = Assert.Throws<ArgumentException>(() =>
            _ = ExchangeStructureReadOptions.WithDomainEquivalenceProvider(new IdentityDomainEquivalenceProvider([
                new SchemaDomainEquivalence(a, b),
                new SchemaDomainEquivalence(b, a),
                new SchemaDomainEquivalence(b, c),
                new SchemaDomainEquivalence(c, b),
            ])));
        var unknown = Assert.Throws<ArgumentException>(() => ExchangeStructure.Read(
            new StringReader(Exchange(string.Empty)),
            [CreateDescriptor()],
            ExchangeStructureReadOptions.WithDomainEquivalenceProvider(new IdentityDomainEquivalenceProvider([
                new SchemaDomainEquivalence(a, b),
                new SchemaDomainEquivalence(b, a),
            ]))));
        var contradictory = Assert.Throws<ArgumentException>(() =>
            _ = ExchangeStructureReadOptions.WithDomainEquivalenceProvider(new IdentityDomainEquivalenceProvider([
                new SchemaDomainEquivalence(a, b),
                new SchemaDomainEquivalence(a, bOther),
                new SchemaDomainEquivalence(b, a),
                new SchemaDomainEquivalence(b, bOther),
                new SchemaDomainEquivalence(bOther, a),
                new SchemaDomainEquivalence(bOther, b),
            ])));

        using (Assert.Multiple())
        {
            await Assert.That(asymmetric.Message).Contains("asymmetric");
            await Assert.That(nonTransitive.Message).Contains("transitively closed");
            await Assert.That(unknown.Message).Contains("not defined by the supplied descriptors");
            await Assert.That(contradictory.Message).Contains("contradictory");
        }
    }

    /// <summary>Aggregates null public population entries before canonical writing touches the destination.</summary>
    [Test]
    public async Task Should_reject_null_population_model_entries_atomically()
    {
        var structure = ExchangeStructure.Read(
            new StringReader(Exchange(string.Empty)),
            [CreateDescriptor()]);
        structure.SchemaPopulation.Add(null!);
        structure.FilePopulations.Add(null!);
        var destination = new StringWriter();

        var failure = Assert.Throws<ExchangeStructureWriteValidationException>(() => structure.Write(destination));

        using (Assert.Multiple())
        {
            await Assert.That(failure.ValidationResult.Failures.Select(item => item.Code))
                .IsEquivalentTo([
                    "P21.STRUCTURE.SCHEMA_POPULATION.EXTERNAL_FILE.REQUIRED",
                    "P21.STRUCTURE.FILE_POPULATION.REQUIRED",
                ]);
            await Assert.That(destination.ToString()).IsEmpty();
        }
    }

    private static string Exchange(string populations) => $$"""
        ISO-10303-21;
        HEADER;
        FILE_DESCRIPTION(('schema population test'),'4;2');
        FILE_NAME('population.p21','2026-09-07T00:00:00Z',('Author'),('Org'),'Pre','System','Auth');
        FILE_SCHEMA(('population_model'));
        {{populations}}
        ENDSEC;
        DATA('main',('population_model'));
        #1=NODE('root');
        ENDSEC;
        END-ISO-10303-21;
        """;

    private static string OtherExchange(string populations) => $$"""
        ISO-10303-21;
        HEADER;
        FILE_DESCRIPTION(('schema population cycle'),'4;2');
        FILE_NAME('population.p21','2026-09-07T00:00:00Z',('Author'),('Org'),'Pre','System','Auth');
        FILE_SCHEMA(('other_model'));
        {{populations}}
        ENDSEC;
        DATA('main',('other_model'));
        #1=ALTERNATE('node');
        ENDSEC;
        END-ISO-10303-21;
        """;

    private static string Sign(string source, params IPart21SignatureSigner[] signers)
    {
        var result = new StringBuilder(source);
        foreach (var signer in signers)
        {
            _ = result.Append('\n');
            var cms = signer.Sign(EncodePart21Alphabet(result.ToString()));
            _ = result.Append("SIGNATURE ")
                .Append(Convert.ToBase64String(cms.Span))
                .Append(" ENDSEC;");
        }
        return result.ToString();
    }

    private static byte[] EncodePart21Alphabet(string source)
    {
        var result = new StringBuilder(source.Length);
        foreach (var rune in source.EnumerateRunes())
        {
            if (rune.Value is >= 0x20 and <= 0x7e or >= 0x80 and <= 0x10ffff)
                _ = result.Append(rune.ToString());
        }
        return Encoding.UTF8.GetBytes(result.ToString());
    }

    private static SchemaDescriptor CreateDescriptor() => CreateDescriptors().Single(descriptor =>
        descriptor.Name.Equals(new SchemaName("population_model")));

    private static IReadOnlyCollection<SchemaDescriptor> CreateDescriptors()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/population.exp", SCHEMA),
            ("schemas/other.exp", OTHER_SCHEMA));
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
        return new[]
        {
            "TedToolkit.Step21.Generated.PopulationModel.SchemaDescriptor",
            "TedToolkit.Step21.Generated.OtherModel.SchemaDescriptor",
        }.Select(typeName => (SchemaDescriptor)assembly.GetType(
            typeName,
            throwOnError: true)!.GetProperty("Instance")!.GetValue(null)!).ToArray();
    }

    private static IReadOnlyCollection<SchemaDescriptor> CreateAnnexEDescriptors()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/longa.exp", LONGA_SCHEMA),
            ("schemas/longb.exp", LONGB_SCHEMA));
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
        return new[]
        {
            "TedToolkit.Step21.Generated.Longa.SchemaDescriptor",
            "TedToolkit.Step21.Generated.Longb.SchemaDescriptor",
        }.Select(typeName => (SchemaDescriptor)assembly.GetType(
            typeName,
            throwOnError: true)!.GetProperty("Instance")!.GetValue(null)!).ToArray();
    }

    private sealed class DictionaryProvider(IReadOnlyDictionary<string, Part21ResourceContent> resources)
        : IPart21ResourceProvider
    {
        internal List<string> Requests { get; } = [];

        public Part21ResourceContent? GetResource(Uri resourceIdentity)
        {
            Requests.Add(resourceIdentity.AbsoluteUri);
            return resources.GetValueOrDefault(resourceIdentity.AbsoluteUri);
        }
    }

    private sealed class IdentityDomainEquivalenceProvider(
        IEnumerable<SchemaDomainEquivalence> equivalences,
        Func<IReadOnlyList<ParameterValue>, IReadOnlyList<ParameterValue>>? projection = null)
        : ISchemaDomainEquivalenceProvider
    {
        private readonly IReadOnlyCollection<SchemaDomainEquivalence> _equivalences =
            Array.AsReadOnly(equivalences.ToArray());
        private readonly Func<IReadOnlyList<ParameterValue>, IReadOnlyList<ParameterValue>> _projection =
            projection ?? (parameters => parameters);

        internal int ProjectionCount { get; private set; }

        public IReadOnlyCollection<SchemaDomainEquivalence> GetEquivalences() => _equivalences;

        public IReadOnlyList<ParameterValue> ProjectParameters(
            SchemaEntityType source,
            SchemaEntityType target,
            IReadOnlyList<ParameterValue> sourceParameters)
        {
            ProjectionCount++;
            return _projection(sourceParameters);
        }
    }
}