# 03 - Feature Decomposition

This document breaks down the major capabilities into implementable sub-features.

```text
1. Identity & Access
├── 1.1 Authentication
│   ├── Login (Email/Phone, Password)
│   ├── PIN Authorization (Manager overrides)
│   └── Screen Lock/Session timeout
└── 1.2 Authorization
    ├── Role Management (Default: Admin, Cashier, Manager)
    ├── Custom Role Creation (Kustom Akses)
    └── Data Masking (Hide COGS, Hide Profit)

2. Organization
├── 2.1 Tenant Management
│   ├── Business Profile
│   └── Subscription & Terminal Licenses
└── 2.2 Branch / Outlet Management
    ├── Outlet Creation & Grouping
    ├── Outlet Settings (Hours, Logo, Status)
    └── "Tutup Toko" (Emergency Close)

3. Product / Catalog
├── 3.1 Master Data
│   ├── Product Entity (Name, SKU, Price, Image)
│   ├── Category & Subcategory
│   └── Variants (Size, Color, Price delta)
└── 3.2 F&B Specifics
    ├── Modifiers / Toppings
    └── Recipe Variants (Raw material mapping)

4. POS & Sales
├── 4.1 Order Management
│   ├── Cart & Item Selection
│   ├── Variant & Modifier Selection
│   ├── Hold / Draft Order
│   └── Kitchen Display Routing
└── 4.2 Transaction State
    ├── Void Transaction (Requires PIN)
    ├── Refund (Requires PIN)
    └── Complimentary / Gratisan (Requires PIN)

5. Payment
├── 5.1 Integrated Payment Gateway
│   ├── QRIS Generation (Dynamic on Bill)
│   └── EDC Integration (Cards)
└── 5.2 Settlement
    ├── Split Payment
    ├── Auto-Settlement via Webhook
    └── Automatic AP/AR ledger update

6. Inventory
├── 6.1 Stock Management
│   ├── Real-time Deduction (Triggered by POS/Online Order)
│   ├── Stock Opname (Physical vs System)
│   └── Multi-warehouse Transfer (Mutasi Stok)
└── 6.2 Advanced Tracking
    ├── Serial Number Tracking
    ├── Batch & Expiry Tracking
    └── Production Stock (Raw Material -> Finished Good)

7. Purchasing
├── 7.1 Procurement
│   ├── Purchase Request / Needs Planning
│   ├── Purchase Order (PO) Generation
│   └── PO Approval Workflow
└── 7.2 Fulfillment
    ├── Receiving (Goods Receipt)
    └── Purchase Invoice (To Accounts Payable)

8. CRM & Marketing
├── 8.1 Customer Database
│   ├── Profile & Transaction History
│   └── Segmentation
└── 8.2 Loyalty & Promotion
    ├── Point Accumulation & Redemption Rules
    ├── Membership Tiers
    ├── Voucher Engine (Time-based, percentage, fixed amount)
    └── Buy X Get Y logic

9. Human Resources
├── 9.1 Attendance & Time
│   ├── Shift Scheduling
│   └── Clock-in/out (Selfie verification)
└── 9.2 Payroll & Compensation
    ├── Multi-commission calculation
    ├── Payroll execution (Salary, Allowances)
    └── Kasbon / Early Salary

10. Finance & Accounting
├── 10.1 Journal & Ledgers
│   ├── Automated Double-Entry from POS/Purchasing
│   ├── General Ledger & Chart of Accounts
│   └── COGS (Cost of Goods Sold) automation
└── 10.2 Reporting
    ├── Balance Sheet
    ├── Profit & Loss
    └── Cash Flow Statement

11. Omnichannel Integration
├── 11.1 Marketplace
│   ├── Tokopedia/Shopee Product Sync
│   └── Centralized Order Fulfillment
└── 11.2 Food Delivery
    ├── GrabFood/GoFood Menu Sync
    └── Auto-accept POS routing
```
