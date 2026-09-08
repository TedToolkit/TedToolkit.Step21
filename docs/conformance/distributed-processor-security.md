# Distributed processor security and atomicity

`Part21ProcessingLimits` is the immutable shared quota policy for root input, canonical output, URI text,
CMS sections/signers, one archive entry, and the optional Annex F bridge. `Part21ProcessingLimits.Default` is a
single finite instance reused by ordinary reads, writes, and bridges. Existing graph-wide resource limits remain in
`Part21ResourceLimits`; the two immutable objects are snapshotted by each operation and never grant I/O or trust.

The machine-readable threat matrix and exact defaults are in
[`distributed-processor-security.json`](distributed-processor-security.json). Every matrix row names focused test
methods that are mechanically resolved to discovered tests by the primary security gate. Library-controlled read
failures return no model. Library-controlled write failures occur while output is privately staged and therefore
produce zero destination characters. Annex F parses and validates a complete candidate before replacing any model
collection.
Exceptions raised by caller-owned readers, writers, providers, converters, or signers remain caller failures.

URI resolution accepts caller-defined schemes because the runtime never dereferences them; only the explicitly
supplied provider can acquire bytes. Raw base/provider/converter URI spellings are length-bounded before URI
canonicalization, and the canonical identity is checked again before cache/path work. URI and archive-entry names are
likewise checked before composite keys are created. ZIP data is retained only in memory, with pre/post expansion
checks and a per-entry ceiling in addition to graph-wide count, size, depth, and compression-ratio limits. Base64
length is rejected before allocating decoded CMS bytes. Every `TextReader`, including a partially consumed
`StringReader`, is consumed through the same bounded `maximum + 1` chunk path.

Canonical Part 21 and Annex F JSON output share one source implementation of a capped text builder. Each character
segment, escape, formatted numeric value, occurrence-name/resource component, entity record, and CMS Base64 value
reserves its remaining quota before it is retained; a limit failure therefore performs work and allocation
proportional to the configured bound rather than materializing an unbounded intermediate value. CMS Base64 preflight
uses both the per-envelope limit and the remaining aggregate byte budget before decoding.

Standard clause-10.2 reference cycles are not security recursion and retain the required null result. Provider or
converter re-entry, nested-archive recursion, and signer re-entry are capability failures because they cross a
caller-owned callback or transport boundary.
