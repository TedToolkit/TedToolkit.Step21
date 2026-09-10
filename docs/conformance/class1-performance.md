# Class-1 performance and feature isolation

`build/verify-class1-allocation.ps1` compares one fixed `4;1` read-write-reread journey on the same Release runtime.
It archives baseline `63b2757`, copies the identical probe into that source tree, warms each process, and compares the
median of 20 `GC.GetAllocatedBytesForCurrentThread` samples. The gate is the approved
`max(baseline * 1.05, baseline + 16 KiB)` bound.

The committed candidate probe result is:

```text
CLASS1_ALLOCATION_OK baseline=105728 candidate=114992 maximum=122112 iterations=20 baselineRevision=63b2757
```

The fixed journey contains one INTEGER entity and validates its value after read, canonical write, and reread. Both
measurements compile the same probe and descriptor source, so generated/AP schema size does not distort the runtime
comparison.

The probe deliberately uses the ordinary `ExchangeStructure.Read(TextReader, descriptors)` and `Write(TextWriter)`
overloads. The read overload enters `ExchangeStructureReader.Read` with a null resolution context, and the write
overload enters `ExchangeStructureWriter` without signing options. Therefore the measured path constructs no resource
resolver, archive, CMS verification/signing, domain-equivalence, or Annex F adapter object. Annex F remains a separate
optional assembly with a one-way dependency on the core runtime. Existing focused provider, archive, signature, and
Annex F tests cover activation only when those capabilities are explicitly supplied or present in the input.

Run from the repository root:

```powershell
pwsh -File build/verify-class1-allocation.ps1 -Baseline 63b2757 -Iterations 20
```
