# 14 - Architecture Recommendation

This document outlines the architectural strategy for the ERP platform.

## 1. Architectural Principles
These principles must govern the system design to ensure long-term viability:
- **Cloud Agnosticism:** The architecture must not lock into proprietary cloud services (e.g., AWS DynamoDB or GCP Spanner). It must be deployable on any container orchestrator.
- **Tenant Isolation from Day 1:** The system MUST support Multi-Tenancy (Tenant -> Branch) at the architectural level. Every query must be scoped. (Note: The MVP UI scope may only expose single-outlet management, but the database and backend must be multi-branch ready).
- **Double-Entry Finance Readiness:** The system must capture transactional events (Sales, Purchasing) and post them to a Journal from Day 1. The full Finance UI does not need to be built initially, but the data exhaust must be structurally sound for a ledger.
- **Simplicity for a Small Team:** The architecture must avoid distributed systems complexity (microservices) until scaling demands it.

## 2. Implementation Choices (Provisional)
These are candidate technologies and patterns that fulfill the principles above. They are recommended but open to substitution if specific requirements change.

### Backend Strategy
- **Pattern:** Modular Monolith.
- **Framework:** ASP.NET Core (.NET 8/9) C#.
- **Data Access:** Entity Framework Core (highly recommended for its built-in Global Query Filters to handle `TenantId`).
- **Internal Communication:** In-memory Domain Events (e.g., using MediatR, though native C# events/channels can suffice if MediatR is deemed unnecessary).

### Database Strategy
- **Relational DB:** PostgreSQL.
- **Caching:** Redis (recommended for catalog caching to maintain POS latency).

### Frontend Strategy
- **Application:** React with TypeScript.
- **Containerization:** Docker.

### Background Processing
- A background job runner (like Hangfire or Quartz.NET) is recommended to handle marketplace polling and payroll calculations without blocking the main web threads.
