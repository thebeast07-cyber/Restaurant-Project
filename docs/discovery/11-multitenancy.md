# 11 - Multi-Tenancy Architecture

As a SaaS ERP, the system must support multiple independent businesses (Tenants) on the same infrastructure.

## Hierarchical Model
The domain model requires at least a two-level hierarchy:
1. **Tenant (Merchant / Business Owner)**
   - Owns the Subscription, Product Catalog, Customer Database, and Global Settings.
2. **Sub-Tenant (Branch / Outlet)**
   - Owns the physical Stock, POS Transactions, Shifts, and Attendance records.

## Data Isolation Strategies

When building the system, the architecture must choose one of the following isolation strategies:

### Option A: Shared Database, Shared Schema (Row-Level Isolation)
- **Mechanism:** Every table has a `TenantId` column. All queries must include `WHERE TenantId = @id`.
- **Pros:** Simplest to build, easiest schema migrations, lowest initial infrastructure cost.
- **Cons:** High risk of data leakage if a developer forgets the `TenantId` clause. Noisy neighbor problems (one heavy tenant slows down others). Harder to restore a single tenant's data.

### Option B: Shared Database, Separate Schema (Schema-per-Tenant)
- **Mechanism:** PostgreSQL schemas (e.g., `tenant_a.Products`, `tenant_b.Products`).
- **Pros:** Stronger isolation at the DB engine level. Easy to backup/restore a specific tenant.
- **Cons:** Connection pooling becomes complex. Schema migrations must be looped over thousands of schemas, which is slow and prone to partial failures.

### Option C: Database-per-Tenant
- **Mechanism:** Each tenant gets their own physical database instance.
- **Pros:** Ultimate isolation, security, and scalability.
- **Cons:** Very high infrastructure cost and maintenance overhead. Not suitable for a massive SME customer base where most tenants are small.

## Architecture Recommendation for Multi-Tenancy
Given that the target is a small development team and the customer base scales from micro-MSMEs to mid-sized businesses, **Option A (Shared Database, Shared Schema)** is the most practical starting point.

To mitigate the risks of Option A, the application framework (e.g., Entity Framework Core in .NET) must enforce Global Query Filters automatically, ensuring that `TenantId` is appended to every query at the ORM level without requiring developers to manually write the clause.
