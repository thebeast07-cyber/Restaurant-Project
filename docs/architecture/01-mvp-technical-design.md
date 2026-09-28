# 01 - MVP Technical Design (Day-1 Operational Scope)

## Status
- **Status**: DRAFT
- **Scope**: 2-minggu Day-1 Operational MVP untuk 1 outlet.
- **Bukan**: ADR formal, bukan final schema/migration. Ini acuan kerja untuk sprint saat ini.
- **Prasyarat**: `docs/product/PRD.md`, kesepakatan scope di sesi alignment (lihat catatan di bawah).

## Scope Recap (hasil alignment sesi ini)
**Masuk MVP:**
- Auth sederhana, 3 role tetap: Owner, Manager, Cashier
- 1 Tenant + 1 Branch (seed, tanpa UI admin multi-outlet)
- Catalog: Product, Category, Recipe (bahan baku wajib ada — bukan opsional)
- Table management (nomor meja)
- Order: cart, hold/draft, checkout
- Station routing (Kitchen / Bar) via tiket cetak (printer thermal)
- Payment: Cash + QRIS (manual/statis, tanpa integrasi gateway API)
- Inventory: deduksi otomatis via Recipe saat `OrderPaid`, stock adjustment/opname manual
- Finance: jurnal double-entry auto-posting di background, **tanpa UI**
- Void (dengan PIN) + reverse stok & jurnal
- Audit log: void & stock adjustment (who/when/what/before-after)
- Shift open/close (rekonsiliasi cash)
- Reporting: sales harian + stock level

**Ditunda (fast-follow, bukan dihapus):**
HR (attendance/payroll/commission), Purchasing/PO, CRM/loyalty/voucher, Production (konversi bahan mentah→setengah jadi), Batch/Serial tracking, Refund formal post-settlement, multi-branch admin UI, custom RBAC builder, Finance UI (P&L/Neraca), EDC fisik, KDS digital, Complimentary item sebagai flow terpisah, Marketplace/Omnichannel, **Customer self-order via QR code per meja + payment gateway bank/wallet asli** (menunggu management settle akun payment gateway; lihat catatan Table di §1.4).

---

## 1. Data Model (Logical)

> Catatan desain: setiap tabel transaksional membawa `TenantId` dan `BranchId` sejak awal, walau hanya 1 tenant/branch aktif sekarang. Ini bukan opsional — ini fondasi scalability yang sudah disepakati.

### 1.1 Organization
```
Tenant
- Id, Name, CreatedAt

Branch
- Id, TenantId, Name, Address, OperatingHours
```

### 1.2 Identity
```
User
- Id, TenantId, BranchId, Name, Username, PasswordHash, Role (enum: Owner|Manager|Cashier), PinHash (untuk otorisasi Manager), IsActive
```
*Role sengaja fixed-enum dulu, bukan Role/Permission table terpisah — custom RBAC builder ditunda ke fast-follow.*

### 1.3 Catalog
```
Category
- Id, TenantId, BranchId, Name

Product (menu item)
- Id, TenantId, BranchId, CategoryId, Name, Price, Station (enum: Kitchen|Bar), IsActive

Ingredient (raw material — item di Gudang)
- Id, TenantId, BranchId, Name, Unit (gram, pcs, ml, dst.)

Recipe / RecipeItem
- Id, ProductId, IngredientId, Quantity, Unit
```
*Station adalah atribut data di Product, bukan logic hardcoded — nambah station baru = nambah enum value + config, bukan redesign.*

### 1.4 Table Management
```
Table
- Id, TenantId, BranchId, Number, Status (enum: Available|Occupied)
```
*Order dientry 100% oleh Cashier di MVP ini. `QrToken` per meja untuk customer self-order **sengaja tidak dibuat sekarang** — ditunda sampai management settle akun payment gateway bank/wallet. Table & Order sudah didesain terpisah dari "siapa yang membuat Order", jadi nambah customer self-order nanti = nambah cara baru bikin Order, bukan redesign entity ini.*

### 1.5 Sales & POS
```
Order
- Id, TenantId, BranchId, TableId (nullable — takeaway), ShiftId, Status (enum: Draft|Open|PendingPayment|Paid|Completed|Voided), TotalAmount, CreatedAt

OrderItem
- Id, OrderId, ProductId, Quantity, UnitPrice, Subtotal, Station (denormalized dari Product saat order dibuat)

Shift
- Id, TenantId, BranchId, UserId (cashier), OpenedAt, ClosedAt, OpeningCash, ClosingCash, Status (enum: Open|Closed)
```

### 1.6 Payment
```
PaymentMethod
- Id, Code (enum: Cash|QRIS), Name
```
*Entity generik, bukan hardcoded if-else — provider lain (EDC, e-wallet) tinggal nambah row + adapter nanti.*
```
Payment
- Id, OrderId, PaymentMethodId, Amount, Status (enum: Pending|Confirmed), ConfirmedByUserId (untuk QRIS manual — cashier yang konfirmasi), ConfirmedAt
```

### 1.7 Inventory
```
Stock
- Id, TenantId, BranchId, IngredientId, Quantity

StockMovement
- Id, TenantId, BranchId, IngredientId, ChangeQuantity (+/-), Reason (enum: Sale|Void|ManualAdjustment|Opname), ReferenceType, ReferenceId, CreatedByUserId, CreatedAt
```
*Invariant: Stock.Quantity di titik waktu manapun harus sama dengan penjumlahan seluruh StockMovement terkait — ini yang dicek di integration test.*

### 1.8 Finance (background only, no UI)
```
Account (Chart of Accounts — minimal set untuk MVP)
- Id, Code, Name, Type (enum: Asset|Liability|Equity|Revenue|Expense)
  Seed minimal: Cash, InventoryAsset, Revenue, COGS

JournalEntry
- Id, TenantId, BranchId, ReferenceType (Order|Void), ReferenceId, CreatedAt, IsReversal, ReversalOfId (nullable)

JournalLine
- Id, JournalEntryId, AccountId, Debit, Credit
```
*Invariant keras: `SUM(Debit) = SUM(Credit)` per JournalEntry. Dicek di application layer sebelum commit, bukan cuma harapan.*

### 1.9 Audit
```
AuditLog
- Id, TenantId, BranchId, UserId, Action (enum: Void|StockAdjustment), EntityType, EntityId, BeforeValue (json), AfterValue (json), Reason, CreatedAt
```

---

## 2. Event Contract (Domain Events — in-process untuk MVP, bukan message broker)

| Event | Producer | Trigger | Consumers | Efek |
|---|---|---|---|---|
| `OrderPaid` | Sales | Payment dikonfirmasi (cash diterima / QRIS ditandai lunas) | Inventory, Finance | Inventory: deduksi Stock per Ingredient sesuai Recipe tiap OrderItem. Finance: post JournalEntry (Debit Cash/Bank, Credit Revenue; Debit COGS, Credit InventoryAsset berdasar harga bahan). |
| `OrderVoided` | Sales | Manager approve void (PIN valid) | Inventory, Finance, Audit | Inventory: reverse StockMovement (kembalikan qty). Finance: buat JournalEntry reversal (`IsReversal=true`, refer ke entry asal) — **tidak** edit/hapus entry lama. Audit: catat log. |
| `StockAdjusted` | Inventory | Manual adjustment / opname oleh Warehouse/Manager | Audit | Audit: catat before/after quantity + alasan. |

*Catatan implementasi: untuk skala 1 outlet, event ini cukup diimplementasi sebagai in-process call (misal MediatR notification atau method call langsung dalam 1 transaction/unit-of-work) — **tidak perlu** message broker/outbox pattern dulu. Tapi kontrak event (nama, payload, siapa consumer) tetap didefinisikan eksplisit seperti tabel di atas, supaya kalau nanti pindah ke broker (multi-service/SaaS scale), tinggal ganti transport-nya, bukan re-desain siapa-dengar-apa.*

---

## 3. Flow Sketch (Alur Operasional)

### 3.1 Alur Kasir (happy path)
```
1. Cashier pilih Table (atau Takeaway) → Order dibuat (Status: Draft)
2. Tambah OrderItem dari Catalog → hitung Subtotal & TotalAmount
3. Cashier "Send to Station" → sistem group OrderItem by Station →
   cetak tiket per station (Kitchen ticket, Bar ticket) ke printer
   → Order.Status = Open
4. Customer selesai makan → Cashier "Checkout" → Order.Status = PendingPayment
5. Pilih PaymentMethod:
   - Cash: Cashier input jumlah diterima → Payment.Status = Confirmed langsung
   - QRIS: tampilkan QR statis → Cashier tekan "Konfirmasi Lunas" setelah verifikasi manual → Payment.Status = Confirmed
6. Payment Confirmed → emit `OrderPaid`
   → Order.Status = Paid → Completed
   → Inventory deduksi via Recipe
   → Finance post JournalEntry
7. Cetak struk customer
```

### 3.2 Alur Void
```
1. Manager pilih Order (status Paid/Completed, same-day, belum settlement)
2. Input PIN → validasi
3. Emit `OrderVoided`
   → Order.Status = Voided
   → Inventory reverse StockMovement
   → Finance post JournalEntry reversal
   → Audit log tercatat
```

### 3.3 Alur Stock Opname / Adjustment
```
1. Warehouse/Manager buka layar Stock Adjustment
2. Input Ingredient + quantity fisik hasil hitung manual
3. Sistem hitung selisih (system qty vs input) → buat StockMovement (Reason: Opname/ManualAdjustment)
4. Emit `StockAdjusted` → Audit log tercatat (before/after)
```

---

## 4. Sprint Breakdown (14 Hari, Solo Dev)

Pendekatan: **walking skeleton dulu** — alur inti (order → bayar cash → stok berkurang → jurnal tercatat) harus hidup end-to-end secepat mungkin (target Hari 5), baru dilebarkan. Ini supaya risiko integrasi ketahuan lebih awal, bukan di hari terakhir.

| Hari | Fokus | Output terukur |
|---|---|---|
| 1 | Project scaffolding, DB setup, seed Tenant/Branch/User, Auth (login + role) | Bisa login sebagai Owner/Manager/Cashier |
| 2 | Catalog minimal: Category, Product, Ingredient, Recipe (CRUD dasar + seed menu contoh) | Menu contoh tampil, ada mapping ke bahan baku |
| 3 | Order: cart, Table select, tambah item, hitung total | Order draft bisa dibuat dari UI |
| 4 | Checkout Cash-only → `OrderPaid` → Inventory deduction + Finance journal | **Milestone: walking skeleton hidup** — order jadi paid, stok turun, jurnal balance |
| 5 | Buffer/fix walking skeleton + demo ke Anda | Anda bisa coba alur inti end-to-end |
| 6 | Payment QRIS (statis + konfirmasi manual) | Checkout mendukung 2 metode bayar |
| 7 | Station routing (Kitchen/Bar) + integrasi print tiket | Order tercetak otomatis ke printer sesuai station |
| 8 | Table management penuh (status Available/Occupied, multi-table concurrent) | Beberapa meja bisa aktif bersamaan tanpa bentrok |
| 9 | Void flow + PIN + reverse stok/jurnal | Void teruji, stok & jurnal ter-reverse benar |
| 10 | Stock adjustment/opname screen + Audit log | Warehouse bisa koreksi stok, tercatat di audit |
| 11 | Shift open/close + rekonsiliasi cash | Cashier buka/tutup shift, cash cocok |
| 12 | Reporting: sales harian + stock level | Manager bisa lihat laporan dasar |
| 13 | Integration testing: race condition stok (2 order rebutan item terakhir), idempotency payment | Test skenario concurrent tidak menghasilkan stok negatif/duplikasi |
| 14 | Buffer UAT, bug fix, checklist go-live | Siap dipakai staff resto sungguhan |

**Checkpoint yang saya minta dari Anda:**
- **Hari 5**: review walking skeleton (alur inti) — kalau ada yang meleset dari ekspektasi, masih ada 9 hari untuk koreksi arah.
- **Hari 14**: go/no-go untuk operasional live.

---

## 5. Hal yang Masih Perlu Dipastikan Sebelum/Selama Build
- Ketersediaan printer thermal (dikonfirmasi "bisa diusahakan" — perlu kepastian **kapan** unit fisik siap, karena Hari 7 saya butuh printer nyata untuk integrasi & test).
- Daftar menu awal (Product + Recipe riil, bukan data contoh) — dibutuhkan sebelum Hari 2 selesai agar testing pakai data asli.
- Harga bahan baku awal (untuk COGS/journal Hari 4) — kalau belum ada, saya pakai placeholder dan tandai perlu update sebelum go-live.
