<!-- delivery-map -->
## Delivery map

This file is the only mutable ISO21 work-item status source.

<!-- approval-source: user-approved-revised-delivery-map-2026-09-07 -->

| ID | Outcome | Contract ownership | Real prerequisites and supplied input | Status | Document |
| --- | --- | --- | --- | --- | --- |
| ISO21-001 | 交付 anchor、value/constant occurrence 与 UUID 的 schema-neutral 双向语义 | Owns AC-03 / Supports AC-02 / Supports AC-07 / Supports AC-08 | None | Verified | `work-items/ISO21-001-anchor-occurrence-uuid.md` |
| ISO21-002 | 交付显式资源边界下的 external、directory 和 ZIP reference 解析 | Owns AC-04 / Supports AC-02 / Supports AC-06 / Supports AC-09 | ISO21-001: 已验证的 anchor/occurrence identity 与 URI 映射 | Verified | `work-items/ISO21-002-distributed-resource-resolution.md` |
| ISO21-003 | 交付 CMS 签名、验证、信任结果与原子写出 | Owns AC-05 / Supports AC-02 / Supports AC-09 | None | Verified | `work-items/ISO21-003-cms-signatures.md` |
| ISO21-004 | 交付 Annex E 全部 schema-population 确定方法与显式 domain equivalence | Owns AC-06 / Supports AC-02 / Supports AC-08 / Supports AC-09 | ISO21-001: 已验证的 occurrence 模型；ISO21-002: 已验证的外部结构与身份解析；ISO21-003: 已验证的签名摘要验证结果 | Verified | `work-items/ISO21-004-schema-populations.md` |
| ISO21-005 | 交付 clause 12 全映射与验证可达的 Part 11 语义闭包 | Owns AC-07 / Supports AC-01 / Supports AC-02 | ISO21-001: 已验证的 occurrence/constant-name contract 与 generated lookup seam；Authorized ISO 10303-11:2004 reference: 可合法用于逐条核对的完整规范输入 | Approved | `work-items/ISO21-005-express-mapping-closure.md` |
| ISO21-006 | 交付不污染核心 runtime 的 Annex F ECMAScript binding | Owns AC-08 / Supports AC-02 / Supports AC-09 | ISO21-001: 已验证的 anchor/value 模型；ISO21-004: 已验证的 population 模型 | Approved | `work-items/ISO21-006-ecmascript-binding.md` |
| ISO21-007 | 交付 distributed/archive/signature/binding 的配额、威胁与原子性闭环 | Owns AC-09 / Supports AC-01 / Supports AC-02 | ISO21-002: 已验证的 resource/archive 边界；ISO21-003: 已验证的 CMS policy；ISO21-004: 已验证的 population 边界；ISO21-006: 已验证的核心隔离 | Approved | `work-items/ISO21-007-security-atomicity.md` |
| ISO21-008 | 交付可机械验证的 PICS、classes 1/2/3、兼容、性能与 Native AOT 最终闭环 | Owns AC-01 / Owns AC-02 | ISO21-001 至 ISO21-007: 所有能力边界、语义闭包与安全证明均已 Verified | Approved | `work-items/ISO21-008-conformance-closure.md` |
