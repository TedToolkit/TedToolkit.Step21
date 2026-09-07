# CMS signature conformance

The runtime implements ISO 10303-21:2016 clause 14 signature sections as detached CMS SignedData encoded with
canonical RFC 4648 Base64. Each signature covers every preceding character that belongs to the Part 21 basic
alphabet, including any earlier signature section; excluded control characters do not affect the digest.

## Explicit boundaries

- Reading always decodes CMS structure and rejects malformed, non-canonical Base64, signer-less, or embedded-content
  values atomically. Without verification options, a complete structure is published with `NotEvaluated` results.
- `Part21SignatureVerificationOptions` snapshots the verification time, custom trust roots, additional certificates,
  caller-declared revoked certificates, and evaluated-state acceptance policy. Verification disables certificate
  downloads, rejects any chain containing a certificate absent from explicit or embedded inputs, uses only the
  supplied custom roots, and never performs online revocation checks.
- `SignatureReports` publishes each signed root or external resource's identity and complete signature results in
  deterministic resolution order, including results accepted by policy.
- Cryptographic status and trust status are independent. Results distinguish invalid signatures, unknown signers,
  expired/not-yet-valid certificates, caller-declared revocation, untrusted chains, and trusted chains.
- Writing a signed structure requires explicit `ExchangeStructureWriteOptions` signers. The writer buffers and validates
  every returned detached CMS value before touching the destination, and every later signer receives content that
  includes earlier signature sections.

## Executable evidence

`CmsSignatureConformanceTests` proves structural-only publication, valid and invalid detached signatures, trusted,
untrusted, expired, revoked, and unknown-signer states, the rejection matrix, embedded-content rejection, exact
basic-alphabet coverage, multiple-signature ordering, required signing capability, and zero-output failure. Its fixed,
checked-in test certificates and keys have a bounded 2025–2030 validity interval and are never production credentials.

Normative basis: ISO 10303-21:2016 clauses 3.8, 3.9, 5.2, 6.5.6, and 14.1; RFC 4648 and RFC 5652.
