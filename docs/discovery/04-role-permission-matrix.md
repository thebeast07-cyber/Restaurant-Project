# 04 - Role & Permission Matrix

This document outlines the actors in the system, distinguishing between human operators, end customers, and machine integrations.

## Actor Taxonomy

### Human Actors (Internal to Business)
1. **Owner / Super Admin:** Holds ultimate authority over the Tenant.
2. **Manager / Branch Admin:** Oversees specific branches. Authorizes sensitive POS actions via PIN.
3. **Cashier:** Frontline staff. Executes transactions, opens/closes shifts.
4. **Warehouse / Logistics Staff:** Manages inventory, executes stock opname, receives POs.
5. **Kitchen / Production Staff:** Interacts with KDS, manages raw material production.
6. **Finance / Accountant:** Views ledgers, manages AP/AR, runs payroll.

### Customer Actor (External)
7. **Customer:** End-user purchasing goods, accumulating loyalty points, viewing digital receipts.

### Machine / Integration Actors
8. **System (Internal Job):** Background processors (e.g., automated payroll calculation, auto-settlement).
9. **Webhook / API Client (External System):** E.g., majoo Pay callbacks, Tokopedia order sync.

## Preliminary Permission Matrix

| Capability / Module | Owner (H) | Manager (H) | Cashier (H) | Warehouse (H) | Kitchen (H) | Finance (H) | Customer (C) | System/API (M) |
|---------------------|-----------|-------------|-------------|---------------|-------------|-------------|--------------|----------------|
| **POS Transaction** | View | Auth/PIN | Execute | None | View (KDS) | View | None | None |
| **Void / Refund** | Execute | Execute (PIN)| Denied | None | None | View | None | View/Sync |
| **Open/Close Shift**| View | Audit | Execute | None | None | View | None | System Auto-Close |
| **View COGS / P&L** | Execute | Configurable| Denied | Denied | Denied | Execute | None | None |
| **Adjust Stock** | Execute | Approve | Denied | Execute | Deduct(WIP)| View | None | Sync (API) |
| **Create/Receive PO**| Execute | Approve | Denied | Execute | None | View | None | None |
| **Manage Payroll** | Execute | Approve | Denied | None | None | Execute | None | Auto-Calculate |
| **Redeem Points** | Config | Config | Auth | None | None | None | Request | Sync (API) |
| **Post Journal Entry**| View | View | None | None | None | Execute | None | Execute (Auto) |

*(H) = Human, (C) = Customer, (M) = Machine*
*(Source: FACT/INFERENCE - Based on Majoo's RBAC feature list and standard ERP practices)*
