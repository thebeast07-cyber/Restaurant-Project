# 02 - UI Roadmap & Post-MVP Plan

> Living roadmap, not a fixed spec — update this as decisions get made and phases
> complete, the same way `implementation-notes.md` tracks reality against
> `01-mvp-technical-design.md`'s original plan.

## 0. Where We Are (updated after Day 4)

Backend MVP sprint (14 days, see `01-mvp-technical-design.md` §4) is done and
concurrency-hardened — see [`go-live-checklist.md`](../engineering/go-live-checklist.md)
for the full accounting. After that, a Purchasing extension landed (Supplier,
Purchase Request/Approval, Purchase, Purchase Payments, COGS via Weighted-Average
costing, Waste tracking, low-stock threshold) — see `implementation-notes.md` for
what it is and how it deviates from PRD §11.

**Everything so far is API-only.** That was the deliberate Day 3 decision
(backend-first), and it's the reason this roadmap exists: **UI is the single
biggest blocker to this system being usable by an actual restaurant**, bigger than
printer integration or a real payment gateway — without it, none of the backend
work matters to a cashier standing at a counter.

## 1. Decision: UI-First

This roadmap prioritizes UI over the remaining backend gaps (EDC, formal Refund,
real payment gateway). Rationale: every day spent perfecting backend that nobody can
touch is a day the system stays non-operational; the remaining backend gaps are
either genuinely deferrable (payment gateway — vendor still TBD per PRD §14) or
small enough to slot in once UI work surfaces the actual need for them.

## 2. Open Decisions Needed Before Phase 1 Starts

Per this project's working agreement (discuss before building into unfamiliar
territory), these need an explicit answer — not an assumption — before UI work
begins:

| # | Decision | Why it matters | Status |
|---|---|---|---|
| 1 | **Platform**: web (responsive, runs in a browser on any device) vs. dedicated tablet app (Android/iOS) vs. desktop app | Changes framework choice, deployment story, and whether staff need a specific device | **Open** |
| 2 | **Frontend framework** (if web): React, Blazor (pairs naturally with the existing ASP.NET Core backend), plain HTML/JS, etc. | Affects dev speed and who can maintain it later | **Open**, depends on #1 |
| 3 | **Hosting/deployment** for the UI | Local network only (POS terminal + LAN) vs. cloud-accessible | **Open** |
| 4 | Does Kitchen/Bar need their own screen (a KDS-style ticket display), or is a printed ticket (once the printer is wired) sufficient for v1? | Changes Phase 2 scope significantly | **Resolved (2026-09-29): printed ticket for v1** — no KDS screen for now, cheaper hardware (just a thermal printer) and familiar to kitchen staff already used to working off paper. Revisit KDS later if paper turns out to be a bottleneck. |

**Recommendation**: resolve #1-#3 together as one conversation before writing any
frontend code — they're coupled (e.g. "tablet app" implies native or a
cross-platform framework, which rules out plain server-rendered pages).

## 3. Phases

Each phase should get its own "discuss the approach, agree, then build" pass, same
model as the Purchasing extension — not built autonomously end-to-end like the
original Day 1-14 sprint.

### Phase 1 — UI Foundation + Core POS Flow — **Done**
The minimum a Cashier needs to run a shift without touching `curl`:
- Login (Day 1)
- Open/close Shift (with cash reconciliation display) (Day 2)
- Table view → seat a party → build an order (cart) → checkout (Cash/QRIS) (Day 2)
- Void (PIN-gated), for Manager/Owner (Day 2)

This alone makes the system operable for the core sales loop — see
`implementation-notes.md`'s "Web UI — Day 2" section for what was built and what's
deliberately deferred (quantity stepper, remove-item). Verified live against the
dev API, not just compiled.

### Phase 2 — Kitchen/Bar + Station Routing — **Merged into Phase 5**
- Decision #4 resolved: printed ticket, no KDS screen. This phase needs no UI work
  of its own — it merges with Phase 5 (Printer Integration) below. `Order.Items`
  already carry a `Station` (Kitchen/Bar) and `POST /api/orders/{id}/send-to-station`
  already formats the ticket text; all that's left is the printer adapter.

### Phase 3 — Back-Office: Purchasing & Suppliers — **Done**
- Supplier management, Purchase Request creation (Manager) + Approval (Owner),
  recording a Purchase, recording Purchase Payments.
- Stock Adjustment/Opname screen, Waste recording.
- Built Day 3 (2026-09-29) — see `implementation-notes.md`'s "Web UI — Day 3"
  section for what was built, two pre-existing backend concurrency bugs found
  and fixed along the way, and what's deliberately still deferred (menu/recipe
  CRUD — raised mid-session, scoped to its own follow-up discussion rather than
  folded in here).

### Menu CRUD (Category rename, Product + Recipe edit) — **Done**, not originally a numbered phase
- Added Day 4 (2026-09-29), picked up immediately after the Day 3 deferral
  above once the owner asked how Recipe ties into stock deduction. Scoped
  ahead of Phase 4 (Reporting) since reports read this same data.
- New endpoints: `PUT /api/categories/{id}` (rename only), `PUT
  /api/products/{id}` (full update incl. wholesale Recipe replace). No hard
  Delete for either — Product uses its existing `IsActive` flag as the safe
  removal path (Product.Id is a required FK on order history), Category is
  rename-only (Product.CategoryId is a required FK). See
  `implementation-notes.md`'s "Web UI — Day 4" section for what was built, a
  live EF Core bug caught before shipping, and end-to-end verification
  (edited a product's price+recipe through the UI, then ran an actual
  Checkout to confirm the edited Recipe still drives stock deduction
  correctly).

### Phase 4 — Reporting Dashboard
- Daily sales, stock levels (with low-stock flagging), monthly waste value.
- Lowest urgency of the four UI phases — numbers a manager checks periodically, not
  something blocking daily operation.

### Phase 5 — Printer Integration
- Independent of the UI phases above (it's a hardware/adapter concern, not a
  frontend concern) — can run in parallel once printer unit + connection type
  (LAN/USB) is confirmed (see `go-live-checklist.md` Gap 1).
- `StationTicketFormatter`'s output doesn't need to change; only a new adapter that
  sends it to the printer instead of just returning it in the API response.

### Phase 6 — Deferred Backend Decisions (pick up only when actually needed)
Not started, not urgent, each gated on an external decision:
- **EDC** (card payment terminal) — 0% built, PRD P0 item. Needs a decision on
  whether this restaurant actually needs card payments at launch.
- **Real payment gateway** (bank/e-wallet, replacing QRIS-manual) — vendor TBD
  (PRD §14). See the "jembatan bank/e-wallet" discussion in project history:
  additive when it happens, not a rewrite, so no rush.

  **Hosting decision (§3 above: one office computer on the restaurant's LAN,
  never exposed to the internet) means the server can't directly receive a
  webhook** — a webhook is the bank/e-wallet *initiating* a connection to us,
  which needs a reachable public address. This does not conflict with the LAN
  hosting choice and does not require re-architecting it; it needs one small
  **additive** infrastructure piece when this phase actually starts:

  ```
  [Bank / E-wallet]
         |  sends payment notification
         v
  [Cloud "mailbox" — the only thing with a public address]
         |  relayed over a tunnel that OUR server opens outbound
         |  (e.g. Cloudflare Tunnel) — the restaurant's router never
         |  needs an inbound port opened, so the LAN's security
         |  posture from §3 doesn't change
         v
  [POS server — stays exactly where it is, on the restaurant LAN]
  ```

  Application-side, this needs exactly one new endpoint
  (`POST /api/payments/webhook/{provider}`) that verifies the request is
  genuinely from the provider (signature check) and then reuses the existing
  `Payment.Status: Pending → Confirmed` transition — the same state machine
  already sitting unused since Day 4, because Cash/QRIS-manual currently skip
  straight to `Confirmed`. No existing controller, table, or journal-posting
  logic needs to change; Order/Stock/Journal handling is untouched.
- **Formal Refund flow** (post-settlement credit, distinct from same-day Void) —
  explicitly out of MVP scope from the start (PRD §11), revisit if the business
  actually needs post-settlement refunds.
- **FIFO costing** (replacing Weighted-Average) — only if Weighted-Average's
  accuracy turns out to be insufficient in practice; see `implementation-notes.md`
  for why Weighted-Average was chosen first.

### Phase 7 — Reporting Enhancements (Charts, Export, Period Comparison) — **Done**
Raised during the Phase 4 design discussion (2026-09-30), scoped the same day, and
built Day 6 (2026-09-30) — see `implementation-notes.md`'s "Web UI — Day 6" section
for what was built and how the stock-level time-series was reconstructed from the
`StockMovement` ledger without a new table.

- **Charts on all three Phase 4 reports**: Sales trend (daily, over a picked
  range), Stock levels (both frequency-below-minimum *and* depletion-rate, per
  the owner's answer — originally an either/or open question, resolved to "both"),
  Waste trend (monthly, over a picked range).
- **Export**: Excel, CSV, and PDF, on all three trend cards.
- **Period comparison**: automatic "this period vs. previous period" (equal-length,
  immediately preceding, non-overlapping), extended to all three reports including
  Stock Levels — also resolved from open to "yes" per the owner's answer, not just
  Sales/Waste as originally scoped.
- Three new backend endpoints (`sales-range`, `waste-range`, `stock-trend`), all
  read-only aggregates — no new tables. Comparison needed no dedicated endpoint on
  top of those: the frontend calls the same range endpoint twice and diffs
  client-side.

## 3b. Majoo-Parity Expansion (raised 2026-09-30)

The owner wants this system to grow toward feature parity with Majoo (the
benchmark used since discovery — see `docs/discovery/12-competitor-gap-analysis.md`
and `02-feature-inventory.md` for the full comparison this section is sourced
from), not stay capped at the original MVP scope. This replaces the old "Not
Planned (P1/P2/P3)" list below — those items are now real phases with an agreed
sequence, not a permanent out-of-scope list.

**Sequencing logic**, agreed 2026-09-30: prioritize by what's actually felt
day-to-day at the current single-outlet scale, not by "completeness." Multi-outlet
infra was initially considered for an earlier slot (the data model already carries
`TenantId`/`BranchId` on every table via `IBranchScoped`, so it's cheaper than it
looks), but the owner confirmed there's no concrete second-outlet plan yet — so it
stays deferred rather than built speculatively ahead of need.

1. **Finance Reports (P&L, per-product margin)** — **Done**, scoped and built
   2026-09-30. See `implementation-notes.md`'s "Web UI — Day 7" section for what
   was built and a real dev-data hygiene issue (poisoned `Ingredient.AverageCost`
   from old stress tests) found and fixed while verifying it live.
   - **P&L**: Revenue, COGS, Gross Profit, Operating Expenses (new — see below),
     Net Profit. No tax/PPN in this pass — see the dedicated Tax item below.
   - **New `OperatingExpense` domain**, added because a P&L without Operating
     Expenses can only ever show Gross Profit, not genuine Net Profit. Follows
     the exact same Accounts-Payable pattern Purchase already uses (recording an
     expense posts `Debit OperatingExpense(6000) / Credit AccountsPayable`, not
     an immediate cash assumption; a separate "bayar" action later posts
     `Debit AccountsPayable / Credit Cash`) — matches standard accrual-basis
     accounting (an expense is recognized when incurred, not when paid) and
     reuses a pattern the codebase already has, rather than inventing a
     cash-only shortcut.
   - **Per-product margin**: `OrderItem` gains a new nullable `EstimatedCogs`,
     snapshotted at Checkout per line item (not just the order-level aggregate
     `JournalEntry` COGS line that already exists) — mirrors the
     `StockMovement.UnitCostAtTime` snapshot pattern already established for
     Waste. Historical `OrderItem`s predating this field report margin using a
     same-request fallback (today's `Ingredient.AverageCost` × Recipe), clearly
     distinguishable from the snapshotted figure — the report never goes empty
     for old data, it's just less precise for it.
   - Confirmed during design: Weighted-Average costing (PSAK 14-compliant) and
     the multi-step Revenue → COGS → Gross Profit → OpEx → Net Profit format are
     both standard, not an invented methodology — checked against the Majoo
     benchmark before building, not after.
2. **HR** (Employee master data, Attendance, Kasbon, Payroll) — scoped in a design
   discussion 2026-10-01 before any implementation (same process Purchasing and
   Finance Reports got). **Commission explicitly deferred out of this phase** —
   the owner confirmed restaurant staff don't need per-transaction commission
   right now (unlike Majoo's salon/barbershop-oriented multi-commission split);
   revisit as its own phase if it becomes relevant later.
   - **New `Employee` entity**, separate from `User` (the POS login account).
     Reason: kitchen staff, servers, and cleaning staff need HR records (salary,
     attendance, Kasbon) but most will never log into the POS. `Employee` gets
     its own `UserId` (nullable) to link to a POS login for the subset of staff
     who are also cashiers/managers — `User` stays exactly what it is today,
     nothing about it changes. Fields: `TenantId`, `BranchId`, `UserId?`, `Name`,
     `Position` (free-text job title, not an enum — titles vary too much
     restaurant to restaurant to hardcode), `BaseSalary` (monthly), `HireDate`,
     `IsActive`, `PinHash` (Employee's own PIN for self-service attendance
     clock-in, distinct from `User.PinHash` which authorizes POS manager
     actions — an Employee with no POS login still needs a PIN to clock in).
   - **`Attendance`**: one record per `Employee` per calendar `Date` (unique
     constraint on `TenantId`+`BranchId`+`EmployeeId`+`Date`, same pattern as
     `AddOpenShiftUniqueConstraint`). `Status`: `Present`, `Alpha` (unexcused
     absence), `Izin` (permission), `Sakit` (sick), `Cuti` (leave). Two entry
     paths, both supported per the owner's answer:
     - **Self-service**: employee enters their PIN at the kiosk/cashier device
       to clock in/out, same UX pattern as the existing Manager PIN. Creates/
       updates a `Present` record with `ClockInAt`/`ClockOutAt`.
     - **Manual entry**: Manager/Owner can create or correct any day's record
       (forgot to clock in, or marking Alpha/Izin/Sakit/Cuti — these non-Present
       statuses have no clock-in by definition, so they're always manager-entered).
   - **Payroll calculation**, monthly periods only (no weekly/biweekly — simplest
     fit for Day-1, matches standard Indonesian restaurant practice):
     - `DailyRate = BaseSalary / <calendar days in the period month>`.
     - **Only `Alpha` deducts pay** (`Deduction = DailyRate × AlphaDayCount`).
       `Izin`/`Sakit`/`Cuti` are paid in full — confirmed as the common small-
       restaurant policy, not guessed.
     - `GrossPay = BaseSalary - AttendanceDeduction`.
     - Outstanding `Kasbon` (see below) deducts from `GrossPay` to produce
       `NetPay`.
   - **`Kasbon`** (salary advance): `Employee` requests an amount → Owner/Manager
     approves (`Status`: `Pending` → `Approved`/`Rejected`, with
     `ApprovedByUserId`). Repayment defaults to lump-sum per the owner's answer,
     but modeled as `InstallmentCount` (default `1`) rather than hardcoding
     single-payment — this is deliberately forward-compatible: management
     hasn't signed off on this yet, and if they later want multi-month
     installments instead, that's a config value (`InstallmentCount > 1`) and a
     loop in the existing deduction logic, not a schema rework. Each payroll run
     deducts `OutstandingAmount / RemainingInstallments` (which collapses to
     "deduct it all" when `InstallmentCount` is 1) and the Kasbon is marked
     `Settled` once the balance reaches zero. Edge case worth flagging now
     rather than discovering it live: if a scheduled installment is larger than
     that period's `GrossPay`, the deduction floors at `GrossPay` (never pushes
     `NetPay` negative) and the remainder carries forward automatically to the
     next payroll run.
   - **Payroll → Finance ledger integration**: running payroll for an `Employee`
     posts `Debit Beban Gaji (Salary Expense) / Credit Hutang Gaji (Salary
     Payable)` — the exact same accrual pattern `OperatingExpense` already
     uses (expense recognized when incurred, not when paid). A separate "Bayar
     Gaji" action later posts `Debit Hutang Gaji / Credit Kas`, matching how
     Purchase/OperatingExpense already separate "incur" from "pay". This keeps
     the P&L (built in Finance Reports) accurate without inventing a new
     accounting pattern. Also deliberately forward-compatible, same reasoning
     as `Kasbon` above: this two-step accrual flow is a superset of "pay
     immediately" (run payroll + Bayar Gaji back-to-back the same day gets that
     result with zero rework) — not locked in ahead of management sign-off.
   - **Fingerprint attendance hardware** (raised 2026-10-01, not yet purchased —
     management hasn't decided): **not pre-built speculatively**, same reasoning
     as Omnichannel below — every vendor (ZKTeco and similar) has its own log
     export format/SDK, unknown until a specific device is actually bought, so
     writing an import integration now risks guessing wrong. The one thing that
     *is* safe to note without knowing the vendor: `Attendance.Source` is
     already an enum separate from the attendance data itself, so adding
     `Fingerprint` as a third value later is a one-line addition with no
     migration (enums persist as an int — new values don't change the schema).
     Once rows land with that Source, nothing else in the system needs to
     change — Payroll's Alpha-day counting, attendance reports, etc. all read
     `Attendance` by EmployeeId+Date regardless of Source already. The only new
     work when a device is actually chosen is a vendor-specific import job/
     endpoint, deferred until then.
3. **CRM & Promotion** (customer database, loyalty points, discount/voucher
   engine) — deliberately pushed behind HR per the owner's explicit call 2026-09-30,
   despite being an earlier pick — touches the Checkout flow directly (discount
   calculation, customer attach), needs its own design pass when its turn comes.
4. **Multi-Outlet** (tenant-scoped login, outlet management UI, branch selector)
   — on hold, not urgent at current single-outlet scale. Revisit when a second
   outlet is actually planned, not before — see Known Standing Risks below for
   the specific gap (login is username-only) this eventually forces closed.
5. **Inventory Lanjutan** (multi-branch stock transfer, batch/expiry tracking,
   Production/raw→semi-finished conversion) — gated on Multi-Outlet for the
   transfer piece specifically; batch/expiry and Production could in principle
   move independently, but grouped here since none are urgent at single-outlet
   scale either.
6. **Omnichannel** (GrabFood/GoFood, Tokopedia/Shopee) — **explicitly not
   pre-built speculatively.** Considered "build the bridge now" per the owner's
   suggestion, but rejected for this one specifically (unlike Payment Gateway,
   below): each platform's webhook payload shape is different and unknown until
   a specific vendor is actually chosen, so speculative integration code risks
   guessing the wrong shape and needing a rewrite anyway — the opposite of what
   "build the bridge early" was meant to achieve. The only prep that's actually
   safe to do without knowing the vendor is conceptual, not code: an `Order.Source`
   field (Dine-in vs. Online) so the data model has somewhere to record an order's
   origin whenever this phase actually starts. No endpoint, no webhook handler,
   no background-job engine choice until a specific platform is picked.
   - **Refund** (distinct from the already-built `Void`) deliberately bundled
     into this phase rather than built standalone, per the owner's call
     2026-10-01: Void handles the common case (same-day mistake, pre-settlement
     — reverses stock + journal because the sale is treated as never having
     happened). Refund is a post-settlement money-only reversal (stock is NOT
     restored — ingredients were already used) and is a rare edge case for pure
     dine-in, but becomes a routine operational need once Omnichannel exists
     (payment already settled through a third-party gateway/platform, order
     cancelled after the fact, money has to go back through that platform) — so
     it ships together with Omnichannel instead of being designed in isolation
     now for a dine-in case that barely comes up.
7. **Payment Gateway** (real bank/e-wallet, EDC) — unlike Omnichannel, the bridge
   for this one genuinely is already built and doesn't need vendor-specific
   knowledge to exist safely: `PaymentMethod` is already generic (not
   hardcoded to Cash/QRIS) and `Payment.Status` already has an unused `Pending`
   state, sitting there since Day 4 specifically for this. Nothing left to
   prepare — purely blocked on a management vendor decision (see Phase 6 above,
   which this folds into).
8. **Tax on Order/Checkout** — raised 2026-09-30 during Finance Reports design,
   deliberately split out and **blocked on the owner consulting a tax advisor**
   first, not scoped further until then. Important distinction surfaced during
   that discussion, worth preserving: a restaurant in Indonesia is typically
   subject to **Pajak Restoran / PBJT** (a *regional* tax collected by
   Pemda/Bapenda, no PKP status needed), not **PPN** (the *national* VAT, which
   needs PKP status and is a different mechanism/rate entirely) — these are
   easy to conflate but have different compliance requirements, so this wasn't
   guessed at. Do not build anything tax-related off an assumption; wait for the
   owner to confirm which actually applies to this business.
9. **Recipe Variants** (Majoo's feature: one Product with multiple
   variants — e.g. size/spice level — each with its own price and ingredient
   deduction) — raised 2026-10-01, **blocked on the RnD team's menu structure**,
   not the owner's decision to make alone this time. Today's workaround
   (duplicate `Product` rows, e.g. "Es Teh Manis Reguler" / "Es Teh Manis
   Besar" as two separate Products) already works correctly — stock deducts
   right, pricing is right — so this is a UX/reporting cleanliness gap (menu
   list and per-product margin report show duplicates instead of one grouped
   product), not a missing capability. Deliberately not designed off a guess:
   whether a variant's price is absolute or a delta from a base price, whether
   it has its own full recipe or overrides on a shared base recipe, and
   whether variants are single-dimension (size only) or multi-dimension
   (size × spice level × topping) all come from RnD's actual menu, not
   invented here — getting this schema wrong is far more expensive to unwind
   than Omnichannel's webhook-shape risk, since it cuts through Product, Order,
   Pricing, and Reporting rather than sitting in one isolated integration. No
   cost to waiting either: the duplicate-Product workaround isn't broken,
   just not pretty.
10. **Self-Order (QR) + Digital Receipt** — raised 2026-10-01 from a management
    insight (reduce paper receipt usage, capture customer contact for future
    marketing), prioritized ahead of the rest of CRM & Promotion specifically
    because the owner wants it soon, not bundled behind the full CRM design
    pass. Scoped the same day:
    - **New `Customer`** (name, phone — phone is the dedupe key, upserted on
      every self-order submission so a repeat customer accumulates under one
      record instead of a fresh row each visit). Deliberately placed in the
      domain as the first real slice of the deferred CRM phase, not a
      throwaway table — the full CRM phase later builds loyalty/segmentation
      on top of this same `Customer`, not a replacement for it.
    - **Self-Order flow**: scan a per-table QR → enter name/phone (upserts
      `Customer`) → browse menu → submit. Reuses the *exact* table-claiming
      mechanism `OrdersController.Create` already has (`Table.Status`
      `Available` → `Occupied` via an atomic conditional UPDATE) rather than
      inventing a new one — this is what makes "two people at the same table
      submit at once" safe for free. A table already `Occupied` means an
      Order already exists for it; the submission finds that Order (must be
      `Draft`/`Open` — a `PendingPayment`/`Paid` table starts a fresh Order
      instead of corrupting a bill already being settled) and appends items
      to it, so one table accumulates one running tab across multiple
      submissions, same as a cashier adding items to an open dine-in order
      manually. The first submission for a table also calls the existing
      `SendToStation` transition (`Draft` → `Open`) so self-ordered items
      flow through the exact same kitchen-ticket path as staff-entered ones —
      no parallel kitchen-notification system invented.
    - **Explicitly scoped out of this pass, deferred to Payment Gateway**:
      paying for the order from inside the self-order page (QRIS from the
      customer's phone). That requires the same payment vendor decision
      already blocking Payment Gateway (Phase 7) — a self-order-specific
      payment integration would just be guessing at the same unknown twice.
      For now, self-order only gets the order to the kitchen; settling the
      bill still happens exactly like today (customer flags staff, staff
      runs the existing Checkout). The self-order page is built so a "Bayar
      dari HP" button is a pure addition later — the Order/Payment model
      underneath doesn't change when that day comes.
    - **Digital receipt lives entirely on the web, not WhatsApp** — corrected
      2026-10-01 after an initial `wa.me`-sends-the-receipt-text draft turned
      out to be the wrong shape (plain WA text doesn't read like a receipt,
      and isn't kept anywhere). WhatsApp's only role in this phase is what
      Self-Order already does — capturing the phone number into `Customer`
      for CRM. The receipt itself is a public page (`/receipt/{orderId}`),
      viewable and downloadable as PDF (reusing `jspdf`, already a dependency
      from the Reporting Enhancements phase) any time, not a message that
      disappears into a chat thread. A customer reaches it either through
      their own still-open Self-Order page (once their table's Order is
      marked Paid) or by scanning a QR code shown on the cashier's Checkout
      screen — covering both self-ordered and manually-entered customers
      without needing WhatsApp, an API key, or a vendor decision at all.
    - **Known pre-existing risk this inherits, not a new one**: if staff
      forget to close out a table's Order before new guests sit down and
      scan the QR, the new order appends onto the previous guests' unpaid
      tab. This risk already exists in the manual dine-in flow today
      (nothing stops a cashier from doing the same by mistake) — self-order
      doesn't make it worse, so no extra guard was added specifically for it
      here.

## 4. Known Standing Risks (carried over, not re-litigated here)

- Login is username-only, breaks with a 2nd Tenant (see `implementation-notes.md`,
  Identity & Auth) — not a blocker for a single-outlet launch, but must be fixed
  before onboarding a second outlet.
- Test-suite isolation issue (shared dev Postgres across parallel test classes) —
  a CI/dev-workflow risk, not a production risk, but should be fixed before setting
  up CI. See `implementation-notes.md`'s Fast-follow section.
