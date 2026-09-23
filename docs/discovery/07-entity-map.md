# 07 - Entity Map (Logical Domain Model)

This document outlines the logical domain entities, their ownership, responsibilities, and key invariants. 
**[INFERENCE] Note: This is a logical domain model mapped from functional capabilities, NOT a physical database schema.**

## 1. Organization Domain
- **Tenant**
  - **Purpose:** Root boundary of a business entity.
  - **Lifecycle:** Created -> Active -> Suspended -> Deleted
  - **Relationships:** Has many Branches.
  - **Invariants:** All transactional data in the system MUST belong to a specific Tenant.
- **Branch / Outlet**
  - **Purpose:** Physical/logical location belonging to a Tenant.
  - **Lifecycle:** Created -> Open -> Closed
  - **Relationships:** Belongs to Tenant. Has many Inventories, Terminals, Shifts.

## 2. Identity Domain
- **User**
  - **Purpose:** A human actor (Owner, Manager, Cashier, Warehouse) interacting with the system.
  - **Lifecycle:** Created -> Active -> Deactivated
  - **Relationships:** Belongs to Tenant. Assigned to specific Branches. Has many Roles.
- **Role & Permission**
  - **Purpose:** RBAC configuration for access control.
  - **Relationships:** Belongs to Tenant.

## 3. Catalog Domain
- **Product**
  - **Purpose:** Master item sold or used.
  - **Lifecycle:** Draft -> Active -> Archived
  - **Relationships:** Has many Variants. Has one Recipe (optional).
  - **Cross-Domain:** Referenced by Sales (Order), Inventory (Stock).
- **Variant / SKU**
  - **Purpose:** Specific purchasable iteration of a Product (e.g., Size Large).
  - **Invariants:** Must have a unique SKU per Tenant.
- **Recipe**
  - **Purpose:** F&B definition linking a Variant to Raw Materials.

## 4. Inventory Domain
- **Stock**
  - **Purpose:** Tracks quantity and capital cost (HPP) of a Variant at a Branch.
  - **Lifecycle:** Initialized -> Adjusted -> Depleted
  - **Relationships:** Belongs to Branch and Variant.
  - **Invariants:** Quantity should remain >= 0 unless explicitly configured for negative inventory.
- **StockMovement**
  - **Purpose:** Audit log of why Stock changed (Sale, PO, Opname, Transfer).
  - **Invariants:** Must balance exactly with Stock quantity changes.

## 5. Purchasing Domain
- **PurchaseOrder (PO)**
  - **Purpose:** Procurement request sent to a Supplier.
  - **Lifecycle:** Draft -> Approved -> Sent -> Partially Received -> Fully Received -> Closed
  - **Relationships:** Has many POItems, GoodsReceipts.
- **GoodsReceipt**
  - **Purpose:** Acknowledges physical receipt of goods against a PO.
  - **Cross-Domain:** Triggers Stock creation in Inventory Domain and AP Invoice in Finance Domain.

## 6. Sales & POS Domain
- **Order**
  - **Purpose:** Transaction initiated at the POS or Omnichannel.
  - **Lifecycle:** Draft -> Pending_Payment -> Paid -> Completed -> Voided (Terminal state)
  - **Relationships:** Has many OrderItems. Has one Payment.
  - **Invariants:** Total amount must equal sum of items + tax - discounts.
- **Shift**
  - **Purpose:** Timebound session for a Cashier/Terminal to reconcile cash.
  - **Lifecycle:** Opened -> Closed -> Reconciled

## 7. Payment Domain
- **Payment**
  - **Purpose:** The actual monetary transfer satisfying an Order.
  - **Lifecycle:** Pending -> Authorized -> Captured -> Failed
  - **Cross-Domain:** Triggers Journaling in Finance Domain upon Capture.
- **Refund**
  - **Purpose:** Reversal of a captured payment.
  - **Lifecycle:** Requested -> Authorized -> Reversed

## 8. Finance Domain
- **JournalEntry**
  - **Purpose:** Double-entry accounting log triggered by business events.
  - **Lifecycle:** Created (Immutable)
  - **Relationships:** Has many JournalLines (Debit/Credit).
  - **Invariants:** Sum of Debits MUST equal Sum of Credits.
- **Account (Ledger)**
  - **Purpose:** Chart of Accounts (Cash, Revenue, AP, AR, Inventory Asset).

## 9. CRM Domain
- **Customer**
  - **Purpose:** End-consumer (Customer Actor).
  - **Lifecycle:** Registered -> Active -> Inactive
  - **Cross-Domain:** Referenced by Sales (Orders) for LoyaltyPoints.

## 10. HR Domain
- **Employee**
  - **Purpose:** Extension of User entity specific to payroll/HR.
  - **Relationships:** Has many Attendances, Payrolls, Commissions.
- **Attendance**
  - **Purpose:** Clock-in/out log for shift verification.

## 11. Omnichannel Domain
- **SyncLog**
  - **Purpose:** Tracks integration health with Tokopedia, GrabFood, etc.
  - **Lifecycle:** Pending -> Synced -> Failed
