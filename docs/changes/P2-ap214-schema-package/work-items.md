<!-- delivery-map -->
## Delivery map

This map exists only for a Controlled change with at least two necessary delivery
items. Independent rows may run in any order; prerequisites name concrete supplied inputs rather
than preferred sequencing. This file is the only mutable work-item status source.

<!-- approval-source: user-explicit-approval-2026-08-27 -->

| ID | Outcome | Contract ownership | Real prerequisites and supplied input | Status | Document |
| --- | --- | --- | --- | --- | --- |
| AP214-001 | 固定并可重现地编译 AP214 schema baseline，交付来源、许可、descriptor 与完整生成 API 基线 | Owns AC-02 / Supports AC-06 | None | Approved | `work-items/AP214-001-reproducible-schema-baseline.md` |
| AP214-002 | 交付可独立消费的 `TedToolkit.Step21.Ap214` 包，并明确预防同 schema 的重复 Analyzer 输入 | Owns AC-01 / Owns AC-08 / Supports AC-05 / Supports AC-07 | AP214-001: 已检入的固定 source/provenance 与验证通过的生成 surface | Approved | `work-items/AP214-002-precompiled-package.md` |
| AP214-003 | 用固定 OCCT AP214 fixture 证明完整语义旅程及失败原子性 | Owns AC-03 / Owns AC-04 / Supports AC-07 | AP214-002: 已验证的 package assembly 与 package-only consumer 边界 | Approved | `work-items/AP214-003-occt-semantic-journey.md` |
| AP214-004 | 关闭双包共存、离线重现、版本分类、Native AOT 与最终分发证据 | Owns AC-05 / Owns AC-06 / Owns AC-07 | AP214-002: 已验证的包与消费者输入；AP214-003: 已验证的 fixture 旅程与失败边界 | Approved | `work-items/AP214-004-package-aot-reproducibility-closure.md` |
