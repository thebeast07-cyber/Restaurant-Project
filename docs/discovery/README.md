# Majoo ERP Discovery - Executive Summary

## 1. Discovery Status
- **Status:** **Revision Complete - Ready for Architecture/Product Review.**
- Do NOT begin implementation. The blueprint must be approved by stakeholders first.

## 2. What We Know (Known Facts)
- Majoo is a cloud-based SaaS ERP targeting MSMEs.
- It operates a Tenant -> Branch hierarchy.
- It requires robust RBAC (PIN authorizations, data masking).
- It executes real double-entry accounting.
- It features an omnichannel inventory engine.

## 3. What We Infer (Inferences)
- **Multi-Tenancy Model:** Shared Database, Shared Schema with row-level `TenantId` isolation.
- **Stock Transfers:** Implemented via an "In-Transit" virtual state to prevent stock duplication across branches.
- **Refunds:** Post-settlement refunds are treated as separate credit transactions rather than raw database row deletions (Voids).

## 4. Unknowns / TBD
- **API Webhook Mechanics:** The exact polling/webhook specifications for third-party integrations (Xero, Jurnal, Tokopedia) are currently TBD.
- **Kasbon Implementation:** Whether Early Salary (Kasbon) uses an internal ledger or a third-party embedded finance provider is unknown.

## 5. Decisions Required from Product Owner
1. **UI Scope vs Arch Scope:** Do we launch the MVP UI for single-outlet merchants only, despite the backend supporting multi-branch?
2. **Accounting Ledger:** Are we comfortable delaying the *Finance UI* to Phase 4, provided the backend logs journal events from Phase 2?
3. **Payment Provider:** Which payment gateway will we use to mirror `majoo Pay` functionality (e.g., Midtrans, Xendit)?

## 6. Provisional Architecture Decisions Ready for Approval
- **Modular Monolith** pattern instead of Microservices.
- **ASP.NET Core + PostgreSQL + React** stack.
- **Global Query Filters** for Tenant isolation.
- **Event-Driven Ledger:** Sales/Inventory emit events that an Accounting module consumes, ensuring double-entry readiness.

## 7. Recommended Next Phase
Once stakeholders approve this blueprint and the architecture decisions, the next phase is **Technical System Design**, which includes creating the initial database migration schemas and defining the core API contracts for Phase 1 (Foundation).
