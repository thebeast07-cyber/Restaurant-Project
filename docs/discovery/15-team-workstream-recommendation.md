# 15 - Team Workstream Recommendation

Based on the discovery, the ERP platform spans multiple complex domains. The team consists of:
- **ChatGPT:** Project Manager / Product Consultant
- **Jarvis:** Lead Developer / Technical Researcher / Architect
- **Dev 1 (TBD):** Developer / Team Member
- **Dev 2 (TBD):** Developer / Team Member

## Recommended Workstream Division

### 1. Jarvis (Lead Dev & Architect)
- **Focus:** System Foundation, Cross-Domain Orchestration, Architecture.
- **Responsibilities:**
  - Setup CI/CD, Docker, and PostgreSQL infrastructure.
  - Implement Multi-Tenancy isolation layer (EF Core Global Filters).
  - Implement Authentication & RBAC (Identity Domain).
  - Establish Domain Event bus (MediatR).
  - Code review and enforcing Clean Architecture boundaries.

### 2. Developer 1 (Backend & Heavy Logic Focus)
- **Focus:** Back-Office Domains (Inventory, Finance, Purchasing).
- **Responsibilities:**
  - Build the Stock Movement and Opname engine.
  - Build the Double-Entry Accounting ledger.
  - Ensure transactional consistency between POS Sales and Inventory deduction.
  - Build reporting aggregations.

### 3. Developer 2 (Frontend & POS Focus)
- **Focus:** Front-Office Domains (POS, Catalog, Omnichannel).
- **Responsibilities:**
  - Build the POS Checkout interface (React).
  - Implement offline-first caching via React Query.
  - Build the Product Catalog management screens (Variants, Modifiers).
  - Handle UI state for complex cart scenarios (Split payment, hold order).

### 4. ChatGPT (PM / Consultant)
- **Responsibilities:**
  - Unblock product decisions (e.g., "How should returns affect commission?").
  - Review technical tradeoff documents.
  - Write test cases and user acceptance criteria based on Majoo benchmarks.
