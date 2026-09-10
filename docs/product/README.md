# TedToolkit.Step21 product intent

## 📌 Status

Approved

- Product owner or maintainer: repository maintainer
- Approval owner: repository maintainer
- Last reviewed: 2026-09-10

## 📝 Clarification and decision log

| ID | Question and why it mattered | Recommended answer | User decision and source | Affected sections | Status |
| --- | --- | --- | --- | --- | --- |
| PI-01 | Whether STEP or IFC defines the product boundary | Let ISO 10303-21 govern the product; use application protocols only as evidence | The maintainer stated on 2026-08-21 that the repository is an ISO 10303-21 library, not a STEP- or IFC-specific parser | Positioning, consumers, non-goals, success evidence | Resolved |
| PI-02 | Whether generated objects need general-purpose serialization | Support ISO 10303-21 writing only | The maintainer explicitly excluded JSON, XML, their extension points, and their dependencies on 2026-08-21 | Intended value, non-goals, downstream implications | Resolved |
| PI-03 | Whether the .NET library should ship the optional Annex F language binding | Exclude ECMAScript/JavaScript bindings and keep the product focused on .NET read, edit, validation, and write workflows | The maintainer explicitly removed the binding on 2026-09-10 because no JavaScript integration is needed | Non-goals, conformance boundary, package surface | Resolved |

## 🎯 Positioning

TedToolkit.Step21 is a C# library for .NET consumers that need to read, interpret against EXPRESS schemas, and write ISO 10303-21 exchange structures without replacing standard concepts with application-protocol-specific or library-invented domain abstractions. Its integration surface is C#/.NET API; the presence of a language binding in the ISO standard does not make that other language part of this product. When the standards leave a representation choice open, the library uses idiomatic C# without changing the governed semantics.

## 👥 Target consumers and situations

- .NET library and application developers exchanging product data encoded according to ISO 10303-21.
- Consumers using any EXPRESS schema whose instances are mapped through ISO 10303-21, including but not limited to schemas used by STEP application protocols or IFC.
- Maintainers who need a schema-neutral physical-file model and compile-time-generated schema types.

The runtime and maintained precompiled schema packages target .NET Standard 2.0 and .NET 8; the
Roslyn component targets .NET Standard 2.0. Repository-only build and verification programs may use
the current repository SDK without raising the consumer package minimum.

## ⚠️ Problem and evidence

ISO 10303-21 defines syntax and EXPRESS-to-exchange-structure mappings, while concrete schemas define entity types, inheritance, attributes, selects, and aggregates. A grammar-only parser cannot provide schema-bound reference safety, generated .NET types, or schema-aware writing. The delivered library closes that gap through a public mutable `ExchangeStructure`, compile-time-generated schema types and descriptors, atomic schema-bound reading, explicit validation, and canonical semantic round-trip writing.

## ✨ Intended value

Consumers can use one schema-neutral runtime for ISO 10303-21 and obtain generated, strongly typed .NET representations for a selected EXPRESS schema. Parsed or newly constructed schema-bound models can be written back as ISO 10303-21 exchange structures with explicit diagnostics when the implemented conformance requirements are not satisfied.

## 🚫 Deliberate non-goals and boundaries

- The library does not define domain behavior for a particular STEP application protocol, IFC release, CAD system, or BIM workflow.
- STEP, IFC, AP203, AP242, and similar artifacts are test and interoperability evidence, not alternate public data models.
- Generated objects are not designed for JSON, XML, ORM, database, or general object-graph serialization, and the library does not reference those serialization stacks for extensibility.
- EXPRESS is consumed as the schema language required for ISO 10303-21 mapping; the library is not a general EXPRESS execution environment.
- ECMAScript/JavaScript language bindings, script engines, and script-host bridges are outside the product boundary because this repository delivers a C# library, not a multi-language SDK.
- Unsupported ISO clauses or EXPRESS constraints are reported as unsupported; the library does not claim full conformance from syntax-only acceptance.
- Public domain concepts must trace to ISO 10303-21 or the selected EXPRESS schema. Parser, diagnostic, generated-mapping, and writer machinery may exist only as clearly identified implementation infrastructure, not as additional exchange-structure semantics.

## ✅ Success evidence and review triggers

Success is evidenced when:

- standard-clause-backed fixtures and independent authoritative corpora parse with the declared conformance scope;
- generated schema types compile deterministically and reject invalid entity relationships;
- read-write-read preserves the represented exchange structure semantics;
- writers refuse unresolved, structurally invalid, or unsupported schema mappings instead of emitting output claimed to be conforming.

Review this intent if the library adopts a domain-specific public model, adds a non-ISO serialization contract, stops treating ISO 10303-21 as the governing boundary, or changes its target consumers beyond .NET library and application developers.

## 🧭 Downstream implications

- Principles to establish: ISO/EXPRESS semantics first, idiomatic C# second, clause-backed grammar evolution, schema-neutral runtime boundaries, and semantic round-trip fidelity.
- Architecture constraints: generated schema code depends on the runtime; the runtime never depends on a specific schema or generator implementation.
- Active changes affected: strongly typed schema generation, entity-reference resolution, and ISO 10303-21 writing.
- README summary/link required: the root README summarizes this positioning and links here.
