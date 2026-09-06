<!-- delivery-map -->
## Delivery map

This file is the only mutable AP242 work-item status source.

<!-- approval-source: user-explicit-approval-2026-09-04 -->

| ID | Outcome | Contract ownership | Real prerequisites and supplied input | Status | Document |
| --- | --- | --- | --- | --- | --- |
| AP242-001 | 固定 AP242 source/provenance，并交付可独立消费且防重复输入的预编译包 | Owns AC-01 / Owns AC-02 / Owns AC-08 / Supports AC-05 / Supports AC-06 / Supports AC-07 | None | Verified | `work-items/AP242-001-baseline-and-package.md` |
| AP242-002 | 用固定 OCCT AP242DIS fixture 证明完整语义旅程及失败原子性 | Owns AC-03 / Owns AC-04 / Supports AC-07 | AP242-001: 已验证的 package assembly、descriptor 与 package-only consumer 边界 | Verified | `work-items/AP242-002-occt-semantic-journey.md` |
| AP242-003 | 关闭双包共存、离线重现、版本分类、Native AOT 与最终分发证据 | Owns AC-05 / Owns AC-06 / Owns AC-07 | AP242-001: 已验证的 package/source/API 输入；AP242-002: 已验证的 fixture journey 与失败边界 | Verified | `work-items/AP242-003-package-aot-reproducibility-closure.md` |
