# 06 - Domain Map

This document outlines the bounded contexts and domains within the ERP ecosystem.

## 1. Identity & Access Domain
- **Responsibility:** User authentication, RBAC, PIN authorization, API Key management.
- **Owned Entities:** User, Role, Permission, Session, APIKey.
- **Dependencies:** None (Foundation layer).

## 2. Organization Domain
- **Responsibility:** Managing multi-tenancy, branches, business profiles, and operating hours.
- **Owned Entities:** Tenant, Branch (Outlet), BranchGroup, Terminal.
- **Dependencies:** Identity.

## 3. Catalog Domain
- **Responsibility:** Managing products, variants, modifiers, pricing, and recipes.
- **Owned Entities:** Product, Category, Variant, Modifier, Recipe.
- **Dependencies:** Organization.

## 4. Inventory Domain
- **Responsibility:** Tracking physical stock, stock mutations, opname, serials, and batches.
- **Owned Entities:** Stock, StockMovement, Warehouse, Batch, Serial.
- **Dependencies:** Catalog, Organization.

## 5. Purchasing Domain
- **Responsibility:** Procuring goods, managing suppliers, handling POs and receiving.
- **Owned Entities:** Supplier, PurchaseOrder, POItem, GoodsReceipt.
- **Dependencies:** Inventory, Catalog, Organization.

## 6. Sales & POS Domain
- **Responsibility:** Managing carts, orders, shifts, and checkout processes.
- **Owned Entities:** Order, OrderItem, Shift, KDS_Ticket.
- **Dependencies:** Catalog, Inventory, Promotion, Organization.

## 7. Payment Domain
- **Responsibility:** Handling transaction settlements, QRIS generation, and refunds.
- **Owned Entities:** Payment, Refund, Settlement, PaymentMethod.
- **Dependencies:** Sales, External Payment Gateways.

## 8. Finance Domain
- **Responsibility:** Accounting, double-entry journaling, COGS calculation, AP/AR.
- **Owned Entities:** Account, JournalEntry, Ledger, Invoice.
- **Dependencies:** Sales, Purchasing, Payment, Inventory.

## 9. CRM & Promotion Domain
- **Responsibility:** Customer profiles, loyalty points, membership, and discounts.
- **Owned Entities:** Customer, LoyaltyPoint, Voucher, PromoCampaign.
- **Dependencies:** Sales.

## 10. HR Domain
- **Responsibility:** Attendance, scheduling, commission calculation, and payroll.
- **Owned Entities:** Employee, Attendance, Schedule, Payroll, Commission.
- **Dependencies:** Identity, Sales (for commission).

## 11. Omnichannel Integration Domain
- **Responsibility:** Syncing catalogs, orders, and stock with external marketplaces.
- **Owned Entities:** SyncLog, ExternalMapping.
- **Dependencies:** Sales, Catalog, Inventory.
