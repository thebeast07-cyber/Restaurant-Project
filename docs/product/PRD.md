# Restaurant ERP Product Requirements Document

## 1. Document Control
- **Status**: DRAFT
- **Version**: 1.0.0
- **Owner**: Technology Manager
- **Last Updated**: 2026-09-24
- **Related Documents**:
  - `docs/discovery/README.md`
  - `docs/discovery/01-16.md`
  - `docs/proposal/01-application-development-proposal.md`

## 2. Product Overview
Restaurant ERP adalah *Integrated Restaurant Business Management Platform* yang mensinergikan proses pemesanan (POS), pemrosesan pembayaran, pengendalian persediaan barang, proses pembelian, serta pembukuan (Finance) menjadi satu alur data terpusat (*single source of truth*). Sistem ini dirancang untuk operasional restoran fisik secara menyeluruh, tidak sekadar berfungsi sebagai pencatat transaksi kasir.

## 3. Problem Statement
Sistem silo (POS terpisah dari modul stok dan akuntansi) sering menimbulkan kebocoran pendapatan, selisih persediaan fisik, duplikasi entri data, serta kesulitan melacak rekonsiliasi pembayaran. Restoran membutuhkan satu sistem terpadu agar setiap transaksi dapat secara otomatis memicu pembaruan pada stok dan buku besar (ledger) tanpa intervensi manual.

## 4. Product Vision
Membangun platform ekosistem restoran terpadu yang meminimalisir intervensi manual untuk pencatatan *back-office*, sehingga manajemen dapat mengambil keputusan bisnis secara *real-time* dengan landasan data operasional dan finansial yang akurat.

## 5. Business Goals
- Mencapai tingkat kesiapan operasional sistem sebesar **80% (Operational Readiness)** bersamaan dengan timeline penyelesaian rekonstruksi fisik restoran.
- Menyediakan mesin kasir dan pembayaran elektronik (termasuk EDC) yang cepat, andal, dan dapat melacak status pesanan secara akurat.
- Mengotomatiskan alur pendebitan stok fisik dan pembentukan jurnal finansial berdasarkan *business events* (seperti checkout pesanan atau penerimaan PO).

## 6. Non-Goals
- Tidak mengembangkan *payment gateway* / *payment provider* internal (semua pembayaran elektronik menggunakan vendor, *vendor TBD*).
- Tidak merancang fungsionalitas akuntansi tingkat *enterprise/corporate* kompleks di luar kebutuhan siklus operasi restoran.

## 7. Target Users & Actors
1. **Owner / Super Admin**: Pemilik bisnis yang memegang otorisasi tertinggi di tingkat Tenant.
2. **Manager / Branch Admin**: Pengelola cabang dengan wewenang memberikan otorisasi tindakan sensitif (mis. *Void/Refund*).
3. **Cashier**: Pelaksana transaksi garis depan di terminal POS.
4. **Warehouse / Logistics Staff**: Pengelola fisik persediaan, pembuatan PO, dan penerimaan barang.
5. **Kitchen / Production Staff**: Pengguna *Kitchen Display System (KDS)* dan pengelola produksi bahan mentah.
6. **Finance / Accountant**: Pemantau arus kas, utang-piutang, dan *ledgers*.

## 8. Product Scope
Cakupan awal berfokus pada fitur kritikal yang menjamin restoran dapat beroperasi di hari pembukaan (P0). Sistem meliputi: akses pengguna, pengaturan cabang, katalog produk, POS kasir, pembayaran elektronik, manajemen inventaris dasar, pengadaan (purchasing), fondasi penjurnalan keuangan, dan pelaporan esensial.

## 9. Capability Map
- Identity & Access
- Organization
- Catalog
- Sales & POS
- Payment
- Inventory
- Purchasing
- Finance
- CRM & Promotion
- HR
- Omnichannel Integration
- Reporting

## 10. Functional Requirements

### 10.1 Identity & Access
#### FR-ID-001 — Role-Based Access Control
- **Status**: CONFIRMED
- **Priority**: P0
- **Actor**: Owner / Admin
- **Domain**: Identity
- **Requirement**: The system shall provide access restrictions based on predefined roles (e.g., Manager, Cashier).
- **Business Rules**: Access to specific modules and actions must be strictly validated against the user's role.
- **Acceptance Criteria**: Users cannot access modules or perform actions outside their assigned role.
- **Traceability**: FR-ID-001 → RBAC → Identity → Setup Role → `discovery/02`, `discovery/16`

#### FR-ID-002 — Manager PIN Authorization
- **Status**: CONFIRMED
- **Priority**: P0
- **Actor**: Manager
- **Domain**: Identity
- **Requirement**: The system shall require Manager PIN authorization for sensitive actions like transaction Void, Refund, or Complimentary items.
- **Business Rules**: A valid PIN must be supplied to override restrictions on the POS.
- **Acceptance Criteria**: System blocks voids until a correct PIN is provided.
- **Traceability**: FR-ID-002 → PIN Authorization → Identity → Void/Refund Flow → `discovery/02`, `discovery/16`

### 10.2 Organization & Branch
#### FR-ORG-001 — Branch & Tenant Structure
- **Status**: CONFIRMED
- **Priority**: P0
- **Actor**: Owner
- **Domain**: Organization
- **Requirement**: The system shall allow managing business profiles under a Tenant and creating multiple Branches (Outlets).
- **Business Rules**: Transactional data must be isolated per Tenant. Branch configurations (e.g., operating hours) belong to specific branches.
- **Acceptance Criteria**: A tenant can have multiple branches. Users assigned to one branch cannot view another branch's transactions without proper access.
- **Traceability**: FR-ORG-001 → Multi-Outlet Admin → Organization → Setup Outlet → `discovery/02`, `discovery/16`

### 10.3 Catalog
#### FR-CAT-001 — Master Catalog & Variants
- **Status**: CONFIRMED
- **Priority**: P0
- **Actor**: Admin
- **Domain**: Catalog
- **Requirement**: The system shall manage products, categories, and multiple variants (e.g., sizes, modifications) with distinct pricing.
- **Business Rules**: Each variant must have a unique identifier.
- **Acceptance Criteria**: POS correctly displays and prices products based on selected variants and modifiers.
- **Traceability**: FR-CAT-001 → Product Variants → Catalog → Add/Edit Product → `discovery/02`, `discovery/16`

### 10.4 Sales & POS
#### FR-POS-001 — Order Management & Checkout
- **Status**: CONFIRMED
- **Priority**: P0
- **Actor**: Cashier
- **Domain**: Sales
- **Requirement**: The system shall allow cashiers to create orders, hold/draft orders, and proceed to checkout.
- **Business Rules**: Orders follow states: DRAFT → PENDING_PAYMENT → PAID → COMPLETED.
- **Acceptance Criteria**: Cashier can successfully ring up an order and transition it to checkout.
- **Traceability**: FR-POS-001 → POS Checkout → Sales → Checkout Workflow → `discovery/03`, `discovery/05`, `discovery/16`

#### FR-POS-002 — Void Transaction
- **Status**: CONFIRMED
- **Priority**: P0
- **Actor**: Manager
- **Domain**: Sales
- **Requirement**: The system shall allow voiding an order that was completed on the same business day before settlement.
- **Business Rules**: Voids require PIN. Voids return physical stock and reverse the related financial journal entry.
- **Acceptance Criteria**: Voiding successfully restores stock and reverses the financial impact.
- **Traceability**: FR-POS-002 → Void Transaction → Sales → Void Flow → `discovery/02`, `discovery/05`, `discovery/16`

### 10.5 Payment
#### FR-PAY-001 — Electronic & EDC Payment Processing
- **Status**: CONFIRMED
- **Priority**: P0
- **Actor**: Cashier
- **Domain**: Payment
- **Requirement**: The system shall support payment capture via configured providers, including cash, QRIS, and EDC-based electronic payments where supported.
- **Business Rules**:
  - Payment provider / Specific EDC Vendor = TBD.
  - Payment must generate a unique reference.
  - Payment state must be trackable.
- **Acceptance Criteria**: Cashier can select EDC/QRIS method. System records the payment intent and tracks completion state.
- **Traceability**: FR-PAY-001 → Integrated Payment → Payment → Checkout to Paid → `discovery/02`, `discovery/16`

#### FR-PAY-002 — Refund Transaction
- **Status**: CONFIRMED
- **Priority**: P0
- **Actor**: Manager
- **Domain**: Payment
- **Requirement**: The system shall allow initiating a refund for settled transactions.
- **Business Rules**: Refund is a separate action causing a financial reversal. It may or may not return physical goods depending on business policy.
- **Acceptance Criteria**: System logs the refund as a distinct financial event without destroying original transaction records.
- **Traceability**: FR-PAY-002 → Refund Transaction → Payment → Refund Flow → `discovery/03`, `discovery/05`, `discovery/16`

### 10.6 Inventory
#### FR-INV-001 — Real-Time Stock Deduction
- **Status**: CONFIRMED
- **Priority**: P0
- **Actor**: System
- **Domain**: Inventory
- **Requirement**: The system shall deduct available stock immediately upon successful payment of an order.
- **Business Rules**: Must accurately decrease stock for correct variants based on order items.
- **Acceptance Criteria**: Finalized checkout permanently reduces physical stock count on the branch.
- **Traceability**: FR-INV-001 → Real-Time Stock Deduction → Inventory → Checkout to Paid → `discovery/02`, `discovery/16`

#### FR-INV-002 — Stock Adjustment & Opname
- **Status**: CONFIRMED
- **Priority**: P0
- **Actor**: Warehouse Staff
- **Domain**: Inventory
- **Requirement**: The system shall allow authorized staff to adjust stock levels (Opname) for physical reconciliation.
- **Business Rules**: Must require authorization and generate a stock movement log.
- **Acceptance Criteria**: Physical stock numbers match adjusted numbers in the system.
- **Traceability**: FR-INV-002 → Stock Opname → Inventory → Stock Opname Flow → `discovery/02`, `discovery/16`

### 10.7 Purchasing
#### FR-PUR-001 — Procurement & Receiving
- **Status**: CONFIRMED
- **Priority**: P0
- **Actor**: Warehouse Staff
- **Domain**: Purchasing
- **Requirement**: The system shall support creating Purchase Orders (PO) and recording Goods Receipts.
- **Business Rules**: Receiving goods must automatically increment stock levels and generate an Accounts Payable (AP) intent.
- **Acceptance Criteria**: A completed Goods Receipt raises stock inventory.
- **Traceability**: FR-PUR-001 → PO Receiving → Purchasing → PO to Stock Flow → `discovery/02`, `discovery/05`, `discovery/16`

### 10.8 Finance
#### FR-FIN-001 — Foundation of Double-Entry Accounting
- **Status**: CONFIRMED
- **Priority**: P0
- **Actor**: System
- **Domain**: Finance
- **Requirement**: The system shall automatically record operational business events (Sales, Purchasing) as double-entry journal postings.
- **Business Rules**: Total Debit must strictly equal Total Credit for every journal entry.
- **Acceptance Criteria**: System properly logs balanced journal entries behind the scenes upon transaction completions.
- **Traceability**: FR-FIN-001 → Double-Entry Journal → Finance → Event to Ledger → `discovery/03`, `discovery/16`

### 10.9 CRM & Promotion
#### FR-CRM-001 — Basic Promotions
- **Status**: ASSUMPTION
- **Priority**: P1
- **Actor**: System
- **Domain**: Promotion
- **Requirement**: The system shall automatically calculate discounts based on basic configured rules.
- **Business Rules**: Promotions must be validated during the Draft/Pending_Payment stages.
- **Acceptance Criteria**: Orders receive correct discount values upon adding applicable items.
- **Traceability**: FR-CRM-001 → Promo Engine → Promotion → Checkout to Paid → `discovery/02`, `discovery/16`

### 10.10 HR
#### FR-HR-001 — Shift Management
- **Status**: CONFIRMED
- **Priority**: P0
- **Actor**: Cashier / System
- **Domain**: HR
- **Requirement**: The system shall support opening and closing operational shifts for cashiers.
- **Business Rules**: System enforces shift reconciliations.
- **Acceptance Criteria**: Shift data encapsulates all transactions handled within its timeframe.
- **Traceability**: FR-HR-001 → Shift Management → HR → Open/Close Shift → `discovery/02`, `discovery/16`

### 10.11 Omnichannel
*(See Section 23: Out of Scope for Phase 1 / P0)*

### 10.12 Reporting
#### FR-RPT-001 — Basic Operational Reporting
- **Status**: CONFIRMED
- **Priority**: P0
- **Actor**: Manager
- **Domain**: Reporting
- **Requirement**: The system shall present core operational reports (Sales, Stock Levels).
- **Business Rules**: Aggregated per branch.
- **Acceptance Criteria**: Branch managers can view daily revenue and stock status.
- **Traceability**: FR-RPT-001 → Reporting → Reporting → Manager View → `discovery/09`

## 11. Core Business Workflows
1. **Sales Flow**: Draft → Pending_Payment → Paid → Completed.
2. **Void Flow**: Completed → Voided (Restores stock, reverses financial ledger; Must be same day before settlement).
3. **Refund Flow**: Requested → Authorized → Processed (Separate financial credit).
4. **Purchasing Flow**: Purchase Request → Approval → PO → Receiving → Inventory (Increment) & AP (Trigger).
5. **Stock Transfer**: Requested → Approved → In_Transit → Received.

## 12. Business Rules
- **Finance**: Double-entry accounting is enforced at the core level. Total Debit = Total Credit.
- **Tenant Isolation**: Transactional data strictly isolated per Tenant.
- **Voids vs Refunds**: Voids are for neutralizing same-day mistakes. Refunds are separate transactional activities executed post-settlement.
- **Payment Capability**: Payment provider is abstracted; the system is responsible for tracking intent, capturing capability (Cash, EDC, QRIS), and verifying final state.

## 13. Roles & Permissions
*(Refer to Section 7 for actor mapping. Permissions enforce FR-ID-001).*

## 14. Payment & Settlement Requirements
- **Capability**: Confirmed to support EDC and QRIS.
- **Vendor/Acquirer**: TBD (Need management decision).
- **Contract/API**: TBD. System requires tracking of intent and capability to interface via abstract payment gateway patterns.

## 15. Inventory Requirements
- Must prevent race conditions where two simultaneous transactions deduct the same last item.
- Stock transfers must securely log "In-Transit" states.

## 16. Finance Requirements
- Foundation of double-entry ledger is critical (P0).
- Deep financial UI and complex tax analytics are P2/P3.

## 17. Reporting Requirements
- Focus on end-of-shift reconciliation and daily sales for P0.

## 18. Integration Requirements
- **Specific third-party APIs (Xero, Marketplace)**: TBD / Planned for P2/P3.

## 19. Non-Functional Requirements
- Ensure data consistency (ACID logic constraints applied on business rule layer).
- Ensure idempotency on payment event captures.

## 20. Operational Readiness Definition
Target: 80% operational readiness ketika rekonstruksi/operasional fisik restaurant sudah matang. Prioritas dibangun berdasarkan urgensi kapabilitas bisnis:

### P0 — Critical Launch Operations
Kapabilitas yang menjamin operasional dasar restoran bisa berjalan:
- Identity & access
- Organization / Branch
- Product / Catalog
- POS
- Shift
- Payment
- EDC
- QRIS / electronic payment capability
- Inventory deduction
- Stock adjustment
- Purchasing / receiving
- Finance posting foundation
- Basic operational reporting
- Audit / authorization untuk critical operations

### P1 — Important Operations
Kapabilitas pendukung esensial:
- Recipe
- Production
- Stock transfer
- CRM dasar
- Promotion dasar
- HR attendance
- Commission
- Richer reporting

### P2/P3 — Expansion
Peluang eskalasi dan pengembangan jangka panjang:
- Marketplace
- Omnichannel expansion
- Advanced CRM
- Advanced payroll
- Third-party accounting integrations
- Advanced analytics
- Other non-critical ecosystem capabilities

## 21. Prioritization
Pengembangan secara berurut dijalankan menurut definisi kesiapan operasional: P0 → P1 → P2/P3.

## 22. Acceptance Criteria
- Semua alur transaksi (P0) terekam secara konsisten tanpa *orphan records*.
- Integrasi kasir memangkas jumlah persediaan dengan akurat dan merekam mutasi keuangan dengan seimbang.

## 23. Out of Scope
- **SATUSEHAT**
  - **Status**: OUT OF SCOPE
  - **Reason**: Tidak termasuk scope restaurant ERP saat ini.
  - **Source**: discovery competitor-gap context
- UI Finance / Accounting yang lengkap (Balance Sheet, P&L generation) di luar pencatatan ledger dasar (P2/P3).
- Marketplace / Food Delivery Integration (P2/P3).

## 24. Open Decisions / TBD
- **Payment Provider**: Vendor spesifik, bank, EDC provider, atau API payment aggregator (TBD).
- **External Integration Contract**: Format kontrak webhook, polling (TBD).
- **Split Payment Limits**: Mekanisme spesifik split payment (TBD).
- **Kasbon (Early Salary)**: Mekanisme dan vendor (TBD).

## 25. Traceability
Setiap functional requirement dipetakan dengan format:
`Requirement → Capability → Domain → Workflow → Source`. Refer to Section 10 for individual traceabilities.

## 26. Change Control
- **Version**: 1.0.0
- **Status**: DRAFT (FOR REVIEW)
- **Prosedur**: Segala perubahan terhadap scope, prioritas, dan kapabilitas (capability) yang berdampak pada timeline pengembangan harus didiskusikan ulang.
- **Decision Maker**: Technology Manager adalah final decision maker untuk semua perubahan terkait dokumen ini.
