# 16 - Capability Traceability

This document maps core capabilities across the ecosystem to ensure full traceability from feature to architectural event.

| Capability | Domain | Actor | Workflow | Entity | Event | Dependency | Source | Confidence |
|------------|--------|-------|----------|--------|-------|------------|--------|------------|
| **Multi-Outlet Admin** | Organization | Owner | Setup Outlet | Branch | `BranchCreated` | Identity | OFFICIAL | HIGH |
| **RBAC / Kustom Akses** | Identity | Owner | Setup Role | Role/Permission | `RoleUpdated` | None | OFFICIAL | HIGH |
| **PIN Authorization** | Identity | Manager | Void/Refund Flow | User | `AuthSuccess` | Sales | OFFICIAL | HIGH |
| **Hide Sensitive Data** | Identity | Owner | Setup Role | Permission | N/A | Finance | OFFICIAL | HIGH |
| **Omnichannel Catalog**| Catalog | Admin | Add/Edit Product| Product | `ProductCreated` | Organization | OFFICIAL | HIGH |
| **Product Variants** | Catalog | Admin | Add/Edit Product| Variant | `VariantCreated` | Catalog | OFFICIAL | HIGH |
| **Recipe Variants** | Catalog | Kitchen | Link Raw Mats | Recipe | `RecipeCreated` | Catalog, Inv | OFFICIAL | HIGH |
| **POS Checkout (Cash)**| Sales | Cashier | Checkout to Paid| Order | `OrderPaid` | Catalog, Inv | OFFICIAL | HIGH |
| **Hold / Draft Order** | Sales | Cashier | POS Workflow | Order | `OrderDrafted` | Catalog | OFFICIAL | HIGH |
| **Complimentary Item** | Sales | Manager | POS Workflow | OrderItem | `ItemComplimented`| Identity, Inv | OFFICIAL | HIGH |
| **Void Transaction** | Sales | Manager | Void Flow | Order | `OrderVoided` | Identity | OFFICIAL | HIGH |
| **Refund Transaction** | Payment | Manager | Refund Flow | Payment/Refund| `PaymentRefunded`| Sales, Identity| OFFICIAL | HIGH |
| **Integrated majoo Pay**| Payment | Customer | Checkout to Paid| Payment | `PaymentReceived`| Sales | OFFICIAL | HIGH |
| **Auto-Settlement** | Payment | System | Settlement Flow | Settlement | `SettlementDone` | Payment, Fin | OFFICIAL | HIGH |
| **Real-Time Stock Ded.**| Inventory | System | Checkout to Paid| Stock | `StockDeducted` | Sales, Org | OFFICIAL | HIGH |
| **Stock Opname** | Inventory | Warehouse| Stock Opname | StockMovement | `OpnameCompleted`| Org | OFFICIAL | HIGH |
| **Multi-Branch Transf.**| Inventory | Warehouse| Transfer Flow | StockMovement | `StockTransferred`| Org | OFFICIAL | HIGH |
| **Batch/Expiry Track** | Inventory | Warehouse| Receiving | Batch | `BatchCreated` | Inventory | OFFICIAL | HIGH |
| **Production Stock** | Inventory | Kitchen | Production Flow | StockMovement | `StockProduced` | Catalog (Recipe)| OFFICIAL | HIGH |
| **Purchase Order (PO)**| Purchasing| Warehouse| PO to Stock | PurchaseOrder | `POGenerated` | Inventory | OFFICIAL | HIGH |
| **PO Receiving** | Purchasing| Warehouse| PO to Stock | GoodsReceipt | `GoodsReceived` | Purchasing, Inv| OFFICIAL | HIGH |
| **Customer DB** | CRM | Cashier | POS Workflow | Customer | `CustomerCreated`| Sales | OFFICIAL | HIGH |
| **Loyalty Points** | CRM | System | Checkout to Paid| LoyaltyPoint | `PointsEarned` | Sales | OFFICIAL | HIGH |
| **Promo/Voucher Engine**| Promotion | System | Checkout to Paid| Voucher | `VoucherRedeemed`| Sales | OFFICIAL | HIGH |
| **Double-Entry Journal**| Finance | System | Event to Ledger | JournalEntry | `JournalCreated` | Sales, Inv, Pur| OFFICIAL | HIGH |
| **Automated COGS** | Finance | System | Event to Ledger | Account | `COGSUpdated` | Inv (HPP) | OFFICIAL | HIGH |
| **Online Attendance** | HR | Employee | Open/Close Shift| Attendance | `ClockedIn/Out` | Identity | OFFICIAL | HIGH |
| **Multi-Commission** | HR | System | Checkout to Paid| Commission | `CommissionCalc` | Sales | OFFICIAL | HIGH |
| **Kasbon / Early Sal.** | HR | Employee | Payroll Flow | Payroll | TBD | Finance | OFFICIAL | HIGH |
| **GrabFood/GoFood Sync**| Omnichannel| System | Online to Fulfill| SyncLog | `OnlineOrderRcvd`| Catalog, Sales | OFFICIAL | HIGH |
| **Marketplace Sync** | Omnichannel| System | Online to Fulfill| SyncLog | `MarketplaceOrder`| Catalog, Sales | OFFICIAL | HIGH |
| **Xero/Jurnal Sync** | Integration| System | Event to Ledger | SyncLog | TBD | Finance | OFFICIAL | MEDIUM |

*Note: Any item marked TBD requires further investigation into Majoo's exact webhook or polling mechanisms.*
