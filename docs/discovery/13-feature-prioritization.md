# 13 - Feature Prioritization

Building an ERP is a massive undertaking. This document outlines a logical implementation phasing strategy. 
*Note: Time estimates are excluded; timelines will be determined after technical design and capacity planning.*

## Phase 1: Foundation
*Prerequisite for everything else. Establishes the core data isolation and identity models.*
1. **Identity & Access:** Authentication, RBAC, User Management.
2. **Organization:** Tenant, Branch, Operating Hours.
3. **Product Catalog (Basic):** Master Products, Categories.

## Phase 2: Core Transaction Engine
*Enables the system to function as a basic cash register and track stock movement.*
1. **Inventory (Basic):** Stock Entity, manual Stock Adjustment.
2. **Sales & POS:** Cart, Checkout (Cash only), Receipts, Shift Open/Close.
3. **Core Event Bus:** Wiring `OrderPaid` to Inventory deduction.

## Phase 3: Back Office
*Elevates the system from a POS to a Business Management Tool.*
1. **Catalog (Advanced):** Variants, Modifiers, Recipes.
2. **Purchasing:** Purchase Orders, Receiving.
3. **Inventory (Advanced):** Multi-warehouse transfer (In-Transit tracking), Batch/Serial tracking.
4. **Payment Gateway:** QRIS Integration, E-wallet webhooks.

## Phase 4: Advanced ERP
*Matches Majoo's premium tiers.*
1. **Finance (UI & Ledgers):** Double-entry ledger, automated COGS, P&L generation. *(Note: Event tracking for finance begins in Phase 2, but the UI and complex reporting is built here).*
2. **HR:** Attendance, Shift, Basic Payroll.
3. **CRM:** Loyalty Points, Discount/Promo Engine.

## Phase 5: External Ecosystem
*External connections and scale.*
1. **Omnichannel:** GrabFood/GoFood POS integration.
2. **Marketplaces:** Tokopedia/Shopee sync.
3. **Third-party Accounting:** Xero/Jurnal sync.

## Dependency Graph
```text
Identity ──> Organization ──> Catalog ──> POS ──> Payment
                               │           │
                               v           v
                          Inventory ──> Finance ──> Reporting
                               │
                               v
                           Purchasing
```
