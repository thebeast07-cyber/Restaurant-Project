# Implementation Notes

Living notes on what's actually been built, how it works, and gotchas hit along the
way — for whoever joins this codebase next (including future-us).

- **What this is not**: a restatement of the plan. See
  [`docs/architecture/01-mvp-technical-design.md`](../architecture/01-mvp-technical-design.md)
  for the intended design. This file tracks reality — what landed, what changed from
  plan, and what surprised us.
- **How to use it**: skim "Current State" first, then jump to the module section
  you're touching. Read "Gotchas" before you hit the same bug we already fixed.

---

## Current State

Sprint day: **14 / 14 + Purchasing extension** (see technical design doc for the full
day-by-day plan). See [`go-live-checklist.md`](go-live-checklist.md) for the Day 14
go/no-go writeup. Purchasing (Supplier, PurchaseRequest/Approval, Purchase, Waste,
low-stock threshold) landed after the original 14-day plan closed — see its own
section below for what it is and, importantly, **how and why it deviates from
PRD §11's formal workflow**.
**Walking skeleton milestone reached** (Day 4): Order → Checkout → Inventory deducted
via Recipe → balanced Finance journal posted, all in one atomic transaction. Verified
down to raw SQL, not just API responses. **Day 5**: stress-tested the concurrent paths
and found + fixed 2 real race conditions (see Concurrency section below) — this is the
part of the codebase with the most scrutiny so far.

**Explicit decision after Day 3**: backend-first. Everything built through the
Purchasing extension was API-only, tested via `curl`/integration tests. That UI gap
started closing right after: see
[`docs/architecture/02-ui-roadmap.md`](../architecture/02-ui-roadmap.md) for the
phase plan (`web/` — React + Vite, Day 1 of that roadmap landed: Login + a
Dashboard placeholder, end-to-end against the real API).

Done: Identity/Auth, multi-tenant isolation, Catalog + Recipe, Table/Shift/Order (cart),
Payment (Cash + QRIS-manual), Inventory deduction, Finance journal (background, no UI),
station ticket routing/formatting (content only — no physical printer wired yet,
pending hardware confirmation), full table occupancy lifecycle (Available <-> Occupied,
concurrency-safe), Void (PIN-gated, reverses stock + journal, releases table, audited),
Stock adjustment/opname (Owner/Manager, writes StockMovement + AuditLog),
Shift close with cash reconciliation, basic Reporting (daily sales, stock levels),
Purchasing (Supplier, PurchaseRequest/Approval, Purchase — see below).
Not started: physical printer integration, any UI.

## Running Locally

```bash
docker compose up -d                       # Postgres
export PATH="$HOME/.dotnet:$PATH"          # .NET SDK is user-local, not system-wide
cd src/Restaurant.Api && dotnet run        # applies migrations + seeds data automatically
```

API listens on `http://localhost:5291` (see `Properties/launchSettings.json`).
Seeded dev-only accounts: `owner` / `Owner#12345`, `manager` / `Manager#12345`,
`cashier` / `Cashier#12345` (passwords are local test data, not real secrets).

To wipe and reseed (e.g. after a schema change during dev): `docker compose down -v && docker compose up -d`,
then run the Api again — migrations + seed re-apply from scratch since the DB is gone.

## Solution Structure

```
Restaurant.Domain          — entities only, no EF/ASP.NET dependency. Organized by
                              bounded context folder (Organization, Identity, Catalog,
                              Sales, ...), not by technical layer.
Restaurant.Infrastructure   — AppDbContext, EF Core migrations, tenant query filter.
Restaurant.Api              — controllers, auth (JWT), DI wiring (Program.cs), seed data.
Restaurant.Tests            — not populated yet (xUnit scaffold only).
```

Dependency direction: `Api → Infrastructure → Domain`, and `Api → Domain` directly for
types. `Domain` depends on nothing — this is intentional (see architecture doc §14
"Simplicity for a Small Team" / Clean Architecture boundary).

## Multi-Tenancy

Every entity that implements `ITenantScoped` (or `IBranchScoped`, which extends it)
automatically gets `WHERE TenantId = @current` appended by EF Core's Global Query
Filter — see `AppDbContext.ApplyTenantQueryFilters()`. This is reflection-based: it
scans the model for any CLR type implementing `ITenantScoped` and builds the filter
expression generically, so **new entities get tenant isolation for free just by
implementing the interface** — no per-entity wiring needed in `OnModelCreating`.

`ICurrentTenantProvider.TenantId == null` bypasses the filter entirely. This is only
ever true for two trusted contexts: `SystemTenantProvider` (migrations/seeding) and
`HttpCurrentTenantProvider` when there's no authenticated user (e.g. the login
endpoint itself, before we know who the user is). **Never** make this reachable from
an authenticated request path.

## Module Notes

### Identity & Auth
- 3 fixed roles (`Owner`, `Manager`, `Cashier`) as an enum, not a Role/Permission
  table — custom RBAC builder is explicitly out of scope for the MVP.
- JWT claims: `sub` (UserId), `name`, `role`, `tenant_id`, `branch_id`.
- Login looks up `User` by `Username` alone (not tenant-scoped) — fine with exactly
  one seeded Tenant. **Breaks** the moment a second Tenant exists; needs a tenant
  selector (subdomain/org code) before that happens. Flagged in code as a comment on
  `AuthController.Login`.

### Catalog
- `Product` never holds stock. It's linked to `Ingredient` (the actual stock unit) via
  `RecipeItem`. This was a deliberate correction mid-planning — see design doc for the
  reasoning (a menu item like "Nasi Goreng" isn't a warehouse item; rice/egg/chicken
  are).
- `Station` (Kitchen/Bar) is a plain enum field on `Product`, not a separate table —
  routing logic elsewhere reads this, no hardcoded per-product branching.

### Sales (Order/Shift/Table)
- `Shift` is intentionally minimal right now — open only, no close/reconciliation yet
  (that's Day 11). But `Order.ShiftId` is required (not nullable), so a cashier must
  open a shift before creating any order. This coupling was a deliberate choice made
  during Day 3 rather than deferring it, to avoid retrofitting `Order` later.
- `Order.Status` starts at `Draft` and stays there through item add/remove — the
  transition to `Open` happens at "send to station" (Day 7, not built yet).
- `RestaurantTable` is a full table (not shared with anything SQL-related); named
  `RestaurantTable` instead of `Table` specifically to avoid confusion with SQL/EF
  terminology in code reviews.
- `Order.Status` goes straight from `Draft`/`Open` to `Completed` on successful
  checkout — there's no separately-persisted `Paid` state even though the enum has
  one. The design doc's flow sketch describes Paid → Completed as two steps, but
  nothing in the MVP scope differentiates them (no "mark as served" action exists),
  so persisting both would just be dead state. Revisit if that changes.

### Payment / Inventory / Finance (Day 4 — walking skeleton)
- `OrdersController.Checkout` is the `OrderPaid` event handler, implemented as a
  single in-process method (not a message/broker) wrapped in one explicit DB
  transaction: deduct stock (atomic per-Ingredient, see Concurrency section below) →
  write StockMovement audit rows → record Payment → post journal → complete order →
  commit. If any Ingredient is short, the transaction rolls back — no partial
  deduction, verified at the DB level, not just via the API response.
- Journal posts a simple Cash/Revenue pair only — **no COGS/InventoryAsset lines**.
  That needs a per-Ingredient unit cost, which isn't modeled yet (`Ingredient` has no
  cost field). This is a known, deliberate gap, not an oversight — flagged again here
  so it doesn't get silently assumed "done" later.
- Cash and QRIS both post to the same `"1000" Cash` account for now — there's no
  separate bank/e-wallet clearing account because neither goes through a real payment
  gateway yet.
- Enums are serialized as strings in JSON now (`"Cash"`, `"Kitchen"`, `"Completed"`),
  not raw ints — added `JsonStringEnumConverter` globally in `Program.cs` while
  building this. Applies retroactively to every endpoint, not just Payment/Checkout.

### QRIS (Day 6)
Required **zero code changes**. `Checkout` already took a generic `PaymentMethodCode`
and looked up the matching seeded `PaymentMethod` row — QRIS was already in that enum
and seeded from Day 4, just unverified until now. Tested end-to-end (`paymentMethod:
"Qris"`) and confirmed correct in the DB (payment row, balanced journal).

This matches the "QRIS statis, tanpa gateway" decision made earlier: the QR code
itself is a physical/pre-existing asset the merchant already owns, not something the
system generates per-transaction — so there's no "initiate payment, wait for webhook,
then confirm" flow to build. The cashier shows the static QR, verifies payment
happened by eye, then presses what is mechanically the same "Checkout" action as Cash.
If/when a real QRIS gateway integration replaces this (fast-follow, needs a vendor
decision — see PRD §24), that's an *additive* change: a new async confirmation path
alongside this one, not a rewrite of it.

### Station Ticket Routing (Day 7)
- New endpoint `POST /api/orders/{id}/send-to-station`: locks the cart (atomic
  Draft→Open claim, same guarded-transition pattern as Checkout's Order claim — two
  concurrent calls must not both succeed, or the kitchen gets duplicate tickets), then
  groups `OrderItem`s by `Station` and formats one ticket per station.
- `StationTicketFormatter` (in `Restaurant.Api/Printing/`) is pure text formatting with
  **zero dependency on an actual printer**. It returns ticket content as strings in the
  API response; nothing is sent to hardware. Tested: a 2-item order (1 Kitchen, 1 Bar
  product) correctly produces exactly 2 tickets, each listing only its own items.
- **Blocked on hardware**: wiring this content to a real thermal printer needs a
  confirmed physical unit + connection type. Unit is available, but **Bluetooth is
  ruled out on the dev machine (Fedora Linux, no working BT stack for this)**. Waiting
  on which of LAN/Ethernet (preferred — raw ESC/POS over a TCP socket to port 9100,
  no special driver needed, and works the same if the server later moves off this
  machine) or USB (works, but ties the printer to whichever machine physically runs
  the API) the unit actually supports, plus the model name. `StationTicketFormatter`
  itself won't need to change either way — only the adapter that consumes its output.
- Checkout still accepts an order in `Draft` (never sent to station) or `Open` (sent)
  — sending to station isn't a prerequisite for payment in this implementation, only
  a prerequisite for the kitchen/bar knowing what to prepare.

### Table Occupancy (Day 8)
- `RestaurantTable.Status` is now actually maintained, not just a schema field nobody
  touched: `OrdersController.Create` atomically claims it (`Available` → `Occupied`,
  same `ExecuteUpdateAsync`-with-WHERE pattern as every other claim in this codebase)
  when a `TableId` is given, and `Checkout` releases it back to `Available`
  unconditionally once the order completes — safe because `Create`'s claim guarantees
  only one Order can ever hold a given Table at a time, so there's nothing to check
  before releasing it.
- Verified with a dedicated stress test (`TableClaimRaceTests`): 15 concurrent
  "create order for table X" requests, exactly 1 succeeds, table ends up `Occupied`
  (not double-booked). This one passed on the first run — built the atomic claim in
  from the start this time instead of retrofitting it after a failing test, applying
  the lesson from Day 5/7 directly.
- ~~Void isn't built yet~~ — done Day 9, see below; it releases the table.

### Void (Day 9)
- `POST /api/orders/{id}/void`: gated to `Owner,Manager` roles via `[Authorize]`, and
  separately requires the calling user's own PIN (`BCrypt.Verify` against their
  `PinHash`) as a second factor — matches the permission matrix (discovery/04:
  Cashier is denied outright, Manager/Owner execute with PIN). Only voids `Completed`
  orders on the **same calendar day** (UTC date comparison) — matches the Void vs
  Refund distinction from discovery/05 (Void = same-day pre-settlement undo, Refund =
  separate post-settlement credit, not built).
- Reverses everything Checkout did, without touching the original records:
  - **Stock**: restores exactly what was deducted, by reading back the original
    `StockMovement` rows (`Reason == Sale`) for this order and writing new ones with
    `Reason == Void` and the negated quantity — the Sale movements stay in the table
    forever, this doesn't edit or delete them. Restoring the actual `Stock.Quantity`
    still goes through the same atomic `ExecuteUpdateAsync` pattern as Checkout.
  - **Journal**: posts a **new** `JournalEntry` with `IsReversal = true` and
    `ReversalOfId` pointing at the original, with every line's Debit/Credit swapped —
    never edits the original entry (docs/product/PRD.md §12, hard requirement).
    Verified: original + reversal lines sum to exactly zero per account when combined.
  - **Table**: released back to `Available`, same unconditional-release reasoning as
    Checkout (Create's claim already guarantees exclusive ownership).
  - **AuditLog**: new entity (`Restaurant.Domain.Audit`), records who/when/what
    (`EntityType`/`EntityId`) plus before/after status snapshots and the manager's
    stated reason. Append-only — nothing in this codebase updates or deletes an
    `AuditLog` row after creation.
- All of the above happens in one transaction with the same atomic Order-status claim
  pattern as Checkout (`Completed` → `Voided`, checking rows-affected) guarding the
  whole thing — two concurrent void attempts on one order must not both succeed, or
  stock gets double-restored and two reversal journals get posted for one sale.
  Verified with `VoidRaceTests` (10 concurrent void attempts on one order, exactly 1
  succeeds) — built the atomic claim in from the start again, consistent with Day 8;
  passed on the first run.

### Stock Adjustment / Opname (Day 10)
- New endpoint `POST /api/ingredients/{id}/stock-adjustment`, restricted to
  `Owner,Manager` — there's no separate `Warehouse` role in the MVP's fixed 3-role
  enum (discovery/04 assigns opname to "Warehouse Staff", but that role doesn't exist
  in code; Owner/Manager cover it here).
- Request carries **either** `CountedQuantity` (Opname — the physically counted
  absolute value; the API computes the delta against current `Stock.Quantity`) **or**
  `DeltaQuantity` (ManualAdjustment — a known +/- correction, e.g. spoilage or an
  under-logged delivery), never both — which one is set decides the
  `StockMovementReason` written, so the reason isn't a separate field a caller could
  get out of sync with the actual quantities.
- If no `Stock` row exists yet for the Ingredient (possible — `IngredientsController.Create`
  doesn't create one; only seed data does), it's created on the fly starting at 0
  rather than treated as an error, so Opname can also be used to establish the very
  first count for a newly-added ingredient.
- Guards: rejects a no-op (`changeQuantity == 0`) and rejects anything that would
  drive `Quantity` negative — same invariant Checkout's stock deduction enforces.
- Writes a `StockMovement` (`ReferenceType = "Ingredient"`, `ReferenceId` = the
  ingredient) and an `AuditLog` (`Action = StockAdjustment`, before/after quantity,
  caller-supplied `Reason`) in the same transaction — same who/when/what/before-after
  shape as Void's audit row (PRD §12).
- **Revised to use the same atomic-conditional-`ExecuteUpdateAsync` discipline as
  Checkout/Void**, after initially shipping this endpoint as plain read-then-write
  under the assumption that opname is a low-concurrency, single-operator action.
  Correct on functional tests, but the same class of bug Day 5 found doesn't need
  "hot path" traffic to trigger — two people running opname on the same ingredient at
  once is enough. Two different mutation shapes, so two different fixes:
  - `ManualAdjustment` (a known delta) needs no compare-and-swap — it's applied as a
    single conditional increment,
    `WHERE Quantity + delta >= 0` → `SET Quantity = Quantity + delta`, correct
    regardless of what Quantity currently is. Verified with `StockAdjustmentRaceTests`:
    20 concurrent `+1` deltas on one Ingredient land exactly 20 `StockMovement` rows
    and the final `Quantity` is exactly `starting + 20` — no lost updates.
  - `Opname` (an absolute counted value) is different: "the count was 4800" is only
    valid against the Quantity it was counted against, so it's applied
    compare-and-swap style — `WHERE Quantity == quantityJustRead` → `SET Quantity =
    countedValue` — and a concurrent write in between makes it fail with a `409`
    telling the caller to re-count and retry, rather than silently overwriting
    whatever the other write just did. Verified: 10 concurrent opname counts against
    the same ingredient → exactly 1 succeeds, the rest get `409`.
  - Creating the `Stock` row on first use follows the same upsert-by-catch pattern as
    `ShiftsController.Open`'s partial unique index: try the insert, and if the unique
    index on `(TenantId, BranchId, IngredientId)` rejects it because another request
    won the race, just proceed against the row that already exists.
- `GET /api/ingredients` now also returns `CurrentStock` per ingredient (left-joined
  against `Stock`, `0` if no row exists) — needed so a future opname screen can
  prefill "system says X" before staff key in the physical count.

### Shift Close & Cash Reconciliation (Day 11)
- New endpoint `POST /api/shifts/{id}/close`: only the shift's own owner may close it
  (`shift.UserId != caller → 403`) — matches the permission matrix (discovery/04:
  Cashier has "Execute" on Open/Close Shift, Manager/Owner only "Audit"/"View", so
  they can't close someone else's shift through this endpoint).
- Reconciliation aggregates `Payment` rows for the shift's Orders, filtered to
  `Order.Status == Completed` and `Payment.Status == Confirmed`, split by
  `PaymentMethod.Code` into `CashSalesTotal` vs `NonCashSalesTotal`. Filtering on
  **Order** status (not touching `Payment` at all) is what excludes a voided sale's
  cash from the drawer count — Void (Day 9) never edits or deletes the original
  `Payment` row, it only flips `Order.Status` to `Voided` and posts a reversal
  journal, so `Payment` alone can't tell completed from voided.
  `ExpectedCash = OpeningCash + CashSalesTotal`; `CashVariance = ClosingCash -
  ExpectedCash` is returned so the cashier/manager immediately sees over/short
  without a separate reconciliation step. Verified manually end-to-end: seeded
  Cash + QRIS checkouts plus one Void, confirmed the returned `cashSalesTotal` /
  `nonCashSalesTotal` matched a direct SQL aggregate over `payments`/`orders`
  (voided order's cash correctly excluded).
- The atomic claim (`Status == Open → Closed`, checking rows-affected) is the same
  guarded-transition pattern as every other status change in this codebase — two
  concurrent close requests on one shift must not both report success.
- No new `AuditLog` entry for shift close — PRD §12 / discovery/10's explicit list of
  audited actions is Void, Refund, Stock Adjustment, Journal Edit; shift close isn't
  on it, and the closed `Shift` row itself already records who/when/opening/closing.
- Closing a shift has no explicit interaction needed with `OrdersController.Create`:
  that endpoint already looks up `Status == Open` to find the caller's shift, so once
  a shift is `Closed` it simply stops being found — no separate "is my shift closed"
  check was needed. Verified: creating an order right after closing returns the
  existing "Open a shift before creating an order" `400`, unchanged from Day 3.

### Reporting (Day 12)
- New `ReportsController`, `[Authorize(Roles = "Owner,Manager")]` at the class level
  — matches PRD §10.12/§17's P0 scope exactly: daily sales + stock levels, Manager
  view, nothing else. Both endpoints are read-only aggregates over data other
  controllers already wrote; no new state or invariants here.
- `GET /api/reports/sales-daily?date=yyyy-MM-dd` (defaults to today, UTC): revenue is
  **Confirmed-Payment based, not `Order.TotalAmount`** — same source Shift Close
  (Day 11) reconciles against, so the two reports can never disagree about what a
  "sale" is. Split into `CashTotal`/`NonCashTotal` by `PaymentMethod.Code`, plus
  `CompletedOrderCount`/`VoidedOrderCount` for the day. Filtering is on
  `Order.Status` (`Completed` vs `Voided`), not the `Payment` row, for the same
  reason as Shift Close: Void never edits/deletes the original `Payment`, so
  `Payment` alone can't distinguish a real sale from a voided one.
  Date range is built as explicit `DateTimeOffset` bounds (`>= rangeStart <
  rangeStart.AddDays(1)`) rather than comparing `.Date` in the query, since the
  latter doesn't reliably translate to SQL across providers — this does.
  Verified against a direct SQL aggregate on real stress-test data (111 Completed +
  2 Voided orders on one date): endpoint total matched exactly, and the 2 voided
  orders' original payments were correctly excluded from `CashTotal`.
- `GET /api/reports/stock-levels`: same ingredient/stock left-join shape as
  `GET /api/ingredients` (Day 10), just role-gated and framed as a report response
  (`AsOf` timestamp + list) rather than a catalog listing.

### Integration Testing (Day 13)
No new production code — this day's deliverable is the two scenarios the sprint plan
calls out by name, verified with dedicated tests rather than assumed covered by the
broader stress tests already written on the days the underlying fixes landed:
- **"2 order rebutan item terakhir"**: `StockRaceConditionTests.TwoOrders_CompetingForTheLastUnit_OnlyOneSucceeds`
  — a minimal, literal repro (1 unit of stock, exactly 2 concurrent orders) alongside
  the existing 20-vs-5 stress test, so a future regression here points straight at the
  last-unit edge case instead of needing to be inferred from a larger scenario.
- **Idempotency payment**: `PaymentIdempotencyTests.RetryingCheckoutAfterSuccess_FailsCleanly_WithoutDuplicatingPaymentOrJournalOrStock`
  covers the *sequential* retry (client times out, retries the same checkout call
  after the first already landed) — the case `OrderCheckoutRaceTests` (Day 5
  follow-up) doesn't, since that one fires both calls concurrently. Both land on the
  same atomic Order-status claim in `OrdersController.Checkout`
  (`Draft/Open → Completed`, checking rows-affected), so a retry after success gets a
  clean `400` ("already checked out by another request") with zero duplicate
  Payment/JournalEntry/StockMovement rows — **not** a `200` replaying the original
  result. This is a deliberate scope line: true idempotency-key semantics (retry
  returns the *same* success response) matter once a real async payment gateway
  exists (see the "jembatan bank/e-wallet" discussion — still vendor-TBD per PRD §14),
  not for the current synchronous Cash/QRIS-manual flow where a clean rejection is a
  perfectly safe outcome for a retried request.

### Purchasing, Waste, and Low-Stock Threshold (post-sprint extension)

**This whole module is out of the original 14-day sprint scope.** It was identified
as a gap during the Day 14 go-live review — PRD §20 lists Purchasing/receiving as P0,
but it's absent from the actual sprint breakdown table
(`01-mvp-technical-design.md` §4) — and built afterward by explicit agreement,
following the collaboration model established at that point: explain the domain
in plain terms, surface trade-offs, get an explicit go-ahead, *then* build, rather
than the more autonomous day-by-day execution used for Day 1-14.

**Deviation from PRD §11's formal Purchasing Flow** (`Purchase Request → Approval →
PO → Receiving → Inventory (Increment) & AP (Trigger)`) — deliberate, not
accidental, and the same *kind* of simplification already used elsewhere in this
codebase:
- **No separate PO / GoodsReceipt entities.** `PurchaseRequest` (the Request +
  Approval half) is real and matches the PRD. But once approved, `Purchase` records
  Receiving and Invoicing as a **single combined step** — Stock increments and the
  Accounts Payable journal line post together, in one transaction, not as two
  separate documents at two separate points in time.
  - **The one deliberate seam for future extension**: if "goods physically received"
    ever needs to be split from "invoice recorded" (e.g. supplier delivers before
    sending the invoice), that split is additive — add a `GoodsReceipt` entity that
    `Purchase`/a future `Invoice` references, move the stock-increment there. Existing
    `Purchase` rows just mean "received and invoiced in the same step," which stays a
    valid state — no backfill, no rewrite. See the code comment on `Purchase.cs` for
    the same reasoning in more detail.
- **Actor substitution, same pattern as Day 10.** PRD's actor for PO/Receiving is
  "Warehouse Staff" — a role that doesn't exist in this system's fixed 3-role enum
  (Owner/Manager/Cashier). `PurchaseRequestsController.Create` is Manager-only,
  `Approve`/`Reject` is Owner-only, `PurchasesController.Create` is Owner-or-Manager.
  This asymmetry (Manager creates, Owner reviews — Owner *cannot* create a PR) was an
  explicit product decision from project discussion: Kitchen/Bar staff don't have
  system accounts, so a Manager keys in requests on their behalf (tagged via
  `RequestedFor: Kitchen|Bar|General`, a label only, not a real per-station account);
  Owner's role is to approve/revise, not to originate requests. `Purchase.Create`
  allows Owner too, since a `Purchase` doesn't require a `PurchaseRequestId` at all —
  an emergency walk-in buy with no prior request is a valid, unlinked `Purchase`.
- **First real use of the "Inventory Asset" account** (seeded since Day 4, unused
  until now) — `Purchase` posts Debit Inventory Asset / Credit Accounts Payable
  (new account, code `2000`). Checkout's journal still doesn't touch Inventory Asset
  or COGS (that gap is unchanged, still needs per-Ingredient cost modeling).

**Two additions that are not in the PRD at all** — added purely from project
discussion, not derived from any requirement document, flagged here so nobody
mistakes them for pre-existing scope:
- **`StockMovementReason.Waste`**: split out from `ManualAdjustment` so spoilage/
  breakage/expiry can be reported on separately from an ordinary counting
  correction. No new endpoint — `IngredientsController.AdjustStock`
  (Day 10) gained an optional `DeltaReason` field (`ManualAdjustment` or `Waste`)
  that must accompany `DeltaQuantity`; `CountedQuantity` (Opname) is unaffected and
  always reason `Opname`.
- **`Ingredient.MinimumStock` + `GET /api/reports/stock-levels`'s
  `IsBelowMinimum`/`BelowMinimumCount`**: the data foundation for a low-stock signal.
  `0` means "no threshold configured," never flagged — there's no separate
  enabled/disabled flag, since a genuine minimum of exactly 0 isn't a real business
  case worth distinguishing from "not set up yet." **Deliberately report-only**: no
  active notification (WhatsApp/email/push) is wired to it. That's a separate,
  explicitly deferred decision pending a channel/vendor choice — the same shape as
  the payment-gateway question (see the "jembatan bank/e-wallet" discussion):
  building a notification channel before a vendor/method is chosen risks being
  redone once that choice is made.

**Concurrency**: `Purchase`'s stock increment uses the same conditional
`ExecuteUpdateAsync` (`Quantity = Quantity + delta`, no compare-and-swap needed for a
pure additive delta) as `ManualAdjustment` in Stock Adjustment — correct by
construction under concurrency, verified anyway with `PurchaseRaceTests` (15
concurrent Purchases against one Ingredient, exactly 15 successes, zero lost
updates). `PurchaseRequest` Approve/Reject uses the same atomic
`Pending → Approved/Rejected` claim pattern as every other single-transition status
in this codebase.

### COGS via Weighted-Average Costing, and Purchase Payments (immediate follow-up)

Landed right after the Purchasing module above, closing two gaps found while
reviewing "is the business flow actually complete" — not just "does the happy path
work": Checkout never posted COGS, and a recorded Purchase's Accounts Payable could
never actually be paid off. Also not in the original PRD, added by explicit
agreement, same as Waste/low-stock threshold above.

**Why Weighted Average instead of FIFO.** FIFO was raised as a possible management
preference, but explicitly deferred after laying out the trade-off, for one concrete
reason: FIFO requires tracking cost **per purchase batch** (which specific delivery's
units are being consumed), which means Checkout's stock deduction — the single most
heavily race-tested, most carefully hardened piece of code in this entire codebase
(Day 5's atomic `ExecuteUpdateAsync`, re-verified in Day 13's last-unit test) — would
need to walk multiple batch rows in oldest-first order instead of one atomic
conditional `UPDATE` against a single `Stock` row. That's a materially harder
concurrency problem (locking/consuming across an ordered set of rows atomically) on
top of the part of the app with the least room for a mistake. Weighted Average needs
only one new field (`Ingredient.AverageCost`) and touches Checkout as a **read only**
— the stock deduction logic is completely unchanged. If FIFO turns out to be a hard
requirement later, switching is additive: old Purchases simply have no batch data and
are treated as pre-FIFO consumption under the old method; nothing needs to be
rewritten or backfilled to make that transition, so starting simpler here carried no
real lock-in risk.

- **`Ingredient.AverageCost`** is recalculated on every `Purchase`:
  `NewAverageCost = (QuantityBeforePurchase × AverageCost + PurchaseQuantity × UnitCost) / QuantityAfterPurchase`.
  Implemented as a single raw-SQL `UPDATE ingredients ... FROM stocks ...`
  (`PurchasesController.Create`), run **after** the Stock quantity increment in the
  same transaction and same row lock — it reconstructs the pre-purchase quantity as
  `(post-increment quantity − this item's quantity)` so it never needs a separate
  "read quantity before" step a concurrent Purchase could race against. This is the
  one place in the Purchasing module that couldn't just reuse
  `ExecuteUpdateAsync`, since the formula spans two tables (`ingredients` and
  `stocks`) and `ExecuteUpdateAsync` only targets one.
  **Known accuracy artifact, not a bug**: pre-existing stock that was never bought
  through a recorded `Purchase` (seed data, or anything only ever adjusted via
  Opname) has `AverageCost = 0`. The *first* real Purchase for that Ingredient
  dilutes the weighted average down using that phantom "free" stock — verified
  directly: 5000g of seeded Beras (cost 0) + a 10000g purchase @ 10/g produced an
  average of 6.67, not 10, exactly as the formula predicts. This self-corrects as
  more real purchases layer on top (verified: a second purchase brought it to
  exactly 10), but is worth knowing about when sanity-checking early numbers.
- **Checkout** (`OrdersController.Checkout`) now reads each consumed Ingredient's
  current `AverageCost`, computes `TotalCogs = Σ(quantity consumed × AverageCost)`,
  and — only if `TotalCogs > 0` — adds `Debit COGS (5000) / Credit Inventory Asset
  (1100)` lines to the **same** JournalEntry as the Cash/Revenue lines (not a second
  entry), so Void's existing "swap every line's Debit/Credit" reversal logic
  automatically reverses the COGS effect too, with no changes needed to Void.
  This read is deliberately **not** guarded against a concurrent Purchase updating
  the same Ingredient's `AverageCost` — a momentarily-stale cost is a minor valuation
  inaccuracy, not an integrity violation like `Stock.Quantity` going negative, so it
  doesn't need the compare-and-swap discipline the stock deduction itself uses.
  Verified end-to-end: 1x product consuming 200g of an Ingredient with
  `AverageCost = 10` posted exactly a 2000 COGS line, journal still balanced.
- **`PurchasePayment`** (`POST /api/purchases/{id}/payments`): records one
  installment against a Purchase's AP balance — **partial payment (cicilan) by
  design**, per explicit requirement. `Purchase.AmountPaid`/`PaymentStatus`
  (`Unpaid → PartiallyPaid → Paid`) are updated via the same atomic
  conditional-`ExecuteUpdateAsync` pattern as everywhere else in this codebase:
  `WHERE AmountPaid + Amount <= TotalAmount`, so an overpay attempt is rejected
  atomically against the current committed balance, not a stale read — verified with
  `PurchasePaymentRaceTests` (20 concurrent Rp10 payments against a Rp100 Purchase:
  exactly 10 succeed, `AmountPaid` lands at exactly 100, never more). Posts
  `Debit Accounts Payable / Credit Cash` — the mirror image of the original
  Purchase's `Debit Inventory Asset / Credit Accounts Payable` lines.

### Closing the loop: PurchaseRequest.Fulfilled and the Waste value report

Found during an explicit "is the business flow actually complete" review (not a bug
report) right after COGS/Purchase Payments landed: two things had been *set up* to
be closed later but never actually were.

- **`PurchaseRequestStatus.Fulfilled`**: previously, an approved `PurchaseRequest`
  stayed `Approved` forever, even after a `Purchase` was recorded against it — there
  was no way to tell "which approved requests still need buying" from "which are
  done." `PurchasesController.Create` now atomically claims
  `Approved → Fulfilled` (same pattern as every other single-transition status in
  this codebase — Checkout claiming the Order, `PurchaseRequestsController.Review`
  claiming `Pending`) as the first thing it does inside the transaction, right after
  beginning it. This doubles as a genuine concurrency guard, not just a status
  label: two concurrent Purchases both trying to reference the same approved request
  can't both succeed — verified manually (approve a PR, record a Purchase against
  it → `Fulfilled`; try a second Purchase against the same now-`Fulfilled`
  request → clean `400`, same message path as trying to buy against a `Pending` one).
  **Known simplification, not fixed here**: this models "the whole request was
  fulfilled by one Purchase," not partial fulfillment — there's no per-item
  requested-vs-fulfilled-quantity tracking, so a PR for 50kg satisfied by a 30kg
  Purchase still flips straight to `Fulfilled`.
- **`GET /api/reports/waste`**: the `Waste` StockMovementReason existed since the
  Purchasing extension specifically "so it could be reported on separately," but
  nothing ever read it into a value — this closes that. Defaults to the **current
  month**, not a single day like `sales-daily`, because "how much did we lose to
  waste" is naturally a monthly question. Needed one new field,
  `StockMovement.UnitCostAtTime` — a snapshot of the Ingredient's `AverageCost` at
  the moment the movement was recorded (populated in
  `IngredientsController.AdjustStock` for `Waste`/`ManualAdjustment` only; `Opname`
  is a count correction, not a valued loss, so it's left `null`). Using a snapshot
  rather than "multiply today's `AverageCost` by historical quantity" matters:
  `AverageCost` changes over time as new Purchases land, so without a snapshot this
  report would silently drift every time someone looks at a past month. Verified
  end-to-end: 2000g of Waste recorded when `AverageCost` was 8/unit reported exactly
  `16000` — consistent with the same weighted-average dilution behavior documented
  above, since that 8 already reflects the seeded-stock-at-cost-0 artifact.
  **Known gap**: any `Waste`/`ManualAdjustment` movement recorded *before* this field
  existed has `UnitCostAtTime = null` and contributes `0` to this report — it won't
  retroactively backfill history.

### Web UI — Day 1 (Login + Dashboard foundation)

First slice of [`02-ui-roadmap.md`](../architecture/02-ui-roadmap.md)'s Phase 1.
Platform/framework/hosting decisions were made explicitly before writing any
frontend code (see roadmap §2): **web app, React + Vite, single office-LAN server**
— not a native tablet app, not Blazor, not cloud hosting.

- **`web/`**: a separate Vite + React + TypeScript project, not part of the .NET
  solution — it's a pure API consumer, calling the existing REST endpoints exactly
  like the `curl` commands used throughout this file, just from a browser instead.
  `src/api/client.ts` is a thin `fetch` wrapper: attaches the JWT from
  `localStorage`, and turns a non-2xx response into an `ApiError` carrying the
  backend's own `{ message }` — every controller in this API already returns that
  shape on error, so the UI never needs a second error-message convention.
- **CORS** (`Program.cs`): added because the Vite dev server runs on a different
  port (`5173`) than the API (`5291`) during development — the browser's
  same-origin policy blocks the call without it. Configurable via
  `Cors:AllowedOrigins` in config, defaulting to the Vite dev ports. **Not needed
  in production** under the current hosting plan (UI served from the same
  origin/server as the API, per the roadmap's hosting decision) — this is
  dev-only plumbing, not a permanent cross-origin architecture.
- **`AuthContext`**: stores the JWT + a minimal user object (name/role/tenantId/
  branchId, taken directly from the login response — no separate `/api/me` round
  trip needed) in `localStorage`, restored on page load so a refresh doesn't force
  a re-login. `ProtectedRoute` redirects to `/login` when there's no user in
  context; there's no token-expiry handling yet (a 401 from an expired token
  during use isn't caught and redirected — flagged here, not fixed, since Day 1's
  scope is Login itself).
- **antislop** (`web/antislop.md`, `web/DESIGN.md`): adopted by explicit request
  partway through Day 1 — a rules filter against generic/AI-shaped UI, copy, and
  code (source: `github.com/miqdadbadjuber/anti-slop`), run in "during" mode
  (applied while building, not audited after). `DESIGN.md` records the owner's
  actual direction (transcribed, not invented): a modern/casual cafe, Chinese-
  Indonesian fusion concept, "modern, clean, efficient" personality, no fixed
  palette yet, typography delegated to the agent (a single clean sans-serif,
  deliberately not a stereotyped "Asian-style" display font — see `DESIGN.md`'s
  reasoning). Dial: `ENERGY 2 / RHYTHM 2 / MOTION 1`. Every UI screen going
  forward should be built against this same file, updating it if the owner's
  direction changes, rather than drifting on a per-screen basis.
  Verified the Delivery Gate this file requires before shipping: real
  login/logout/error-state behavior (not dead controls), keyboard focus visible,
  WCAG AA contrast checked numerically (10:1+ in light mode, 6.9:1+ in dark), no
  horizontal overflow at a 375px mobile width, both light and dark themes
  rendered correctly (`prefers-color-scheme`, never forced) — all via an actual
  browser session against the running dev servers, not just a build that compiled.

### Web UI — Day 2 (rest of Phase 1: Shift, Tables, Order/Cart, Checkout, Void)

Completes [`02-ui-roadmap.md`](../architecture/02-ui-roadmap.md)'s Phase 1 — the
core sales loop a Cashier needs to run a shift without `curl`. Built against the
existing API 1:1, no backend behavior changes beyond one small contract addition
(below).

- **`ShiftPage`**: one screen, two modes driven by `GET /api/shifts/current` (open
  shift → close form with cash reconciliation summary; no open shift → open form).
  A shift is a per-user singleton (enforced by the backend's partial unique index,
  see Concurrency below) — this page doesn't need its own concurrency handling,
  just surfaces the 409/400 the API already returns.
- **`TablesPage`**: grid of tables from `GET /api/tables`, colored by `Status`.
  Tapping an `Available` tile calls `POST /api/orders` with that `TableId` and
  navigates into `OrderPage`; tapping an `Occupied` tile resumes the existing order.
  **Backend addition**: `TableResponse` gained `CurrentOrderId` (nullable) —
  `Table.Status` alone doesn't say *which* Order occupies it, so resuming an
  occupied table's cart needed the active Order's id. `TablesController.List` now
  joins `Orders` where `Status` is `Draft` or `Open` (the two pre-checkout
  statuses) per `TableId`. This is additive (new response field, same endpoint),
  not a new domain, so it didn't need its own roadmap discussion pass.
- **`OrderPage`**: category tabs (`GET /api/categories`) filter a product grid
  (`GET /api/products`); tapping a product calls `POST /api/orders/{id}/items`
  with `Quantity: 1` and re-renders the cart from the response `Order` (the API
  is the source of truth for the cart, not local state — avoids the cart drifting
  out of sync with server-computed `Subtotal`/`TotalAmount`). Checkout buttons
  call `POST /api/orders/{id}/checkout` with `Cash` or `Qris` and return to
  `TablesPage` on success (table is freed server-side as part of that same
  transaction, so no client-side table-state juggling needed). A `Completed`
  order shows a Void trigger instead of the cart, gated client-side on
  `role in [Owner, Manager]` — matches the backend's `[Authorize(Roles = "Owner,
  Manager")]` on `POST /api/orders/{id}/void`; the client check is only a UX
  nicety (hide a control a Cashier can't use), the backend role check is what
  actually enforces it.
- **Void modal**: PIN + optional reason, calls `POST /api/orders/{id}/void`. No
  new PIN-verification logic on the frontend — the backend does the
  `BCrypt.Verify` against `User.PinHash` and returns `401` on a wrong PIN, which
  surfaces as the same inline error pattern used everywhere else.
- **Dashboard** (Day 1's placeholder) now links into `/tables` and `/shift`
  instead of the "fondasi Hari 1" placeholder text.
- Fixed a pre-existing `tsc` build failure in `client.ts` (`ApiError`'s
  constructor used a TS parameter-property shorthand that trips
  `erasableSyntaxOnly` in this project's `tsconfig`) — unrelated to Day 2's
  scope, but it blocked `npm run build` entirely so it had to be fixed to verify
  anything.
- Verified the full loop live against the dev API + Postgres (not just a
  compile): open shift → seat a table → add item → checkout Cash → table freed →
  void the completed order with the Owner's PIN → close shift, reconciliation
  numbers matched the actual Confirmed payments for that shift.
- **Not done in Day 2, still open**: Kitchen/Bar routing (`send-to-station`) has
  no UI yet — deliberately deferred, it's gated on roadmap Decision #4 (KDS
  screen vs. printed ticket), which is still open. Quantity is fixed at 1 per
  tap (no quantity stepper or remove-item control on the cart) — smallest slice
  that makes the sales loop usable; a fast-follow, not a scope gap that blocks
  anything.

### Web UI — Day 3 (Phase 3: Purchasing/Suppliers UI)

Completes roadmap Phase 3 — Supplier, Ingredient (stock + Opname/ManualAdjustment/
Waste), Purchase Request (create/approve/reject), and Purchase (record + partial
payments), all built against the existing Purchasing-extension API 1:1.

- **`/purchasing`** hub page links to four sub-screens (`SuppliersPage`,
  `IngredientsPage`, `PurchaseRequestsPage`, `PurchasesPage`); only visible on the
  Dashboard for Owner/Manager (Cashier has no use for any of it, and two of the
  four endpoints are role-gated `[Authorize(Roles = "Owner,Manager")]` server-side
  anyway — the Dashboard link and each page's own client-side role check are just
  UX, not the actual enforcement).
- **`SuppliersPage`**: list + create. Any authenticated role can list (read-only
  for Cashier), create is Owner/Manager.
- **`IngredientsPage`**: list with current stock, inline-editable minimum-stock
  input (`onBlur` triggers `PUT .../minimum-stock`), and a "Sesuaikan Stok" modal
  that maps to `POST .../stock-adjustment`'s two mutually-exclusive shapes —
  Opname (absolute counted value) vs. a signed delta tagged `ManualAdjustment` or
  `Waste`. The UI enforces the same "exactly one of the two" shape the backend
  contract requires, so a bad combination never reaches the API.
  A low-stock row (`currentStock < minimumStock`) gets a "Rendah" badge — reads
  the same two fields `GET /api/reports/stock-level` (Phase 4, not built yet)
  will eventually summarize, so this is a preview of that report at the row
  level, not a separate calculation.
- **`PurchaseRequestsPage`**: Manager sees a create form (`RequestedFor` +
  ingredient lines), Owner sees Approve/Reject on `Pending` rows; both see the
  full list. A Cashier hitting this route gets an inline "khusus Manager/Owner"
  message client-side — the `useEffect` that calls `GET /api/purchase-requests`
  is itself gated on role, so a Cashier's browser never even fires the request
  that would 403.
- **`PurchasesPage`**: record a Purchase (optionally linked to an `Approved`
  request — the dropdown only lists `Approved` ones, since the backend rejects
  anything else) with per-ingredient quantity/unit/unit-cost lines, and a
  "Bayar" action per unpaid/partially-paid row that calls
  `POST .../payments`. Whole controller is `[Authorize(Roles = "Owner,Manager")]`
  server-side, so this page is the strictest of the four — gated the same way as
  Purchase Requests.
- **Backend bugs found and fixed while wiring this up** (both pre-existing,
  neither introduced by the UI work — they'd have hit any caller that listed
  2+ rows, `curl` included, but nothing before this had exercised that):
  - `PurchaseRequestsController.List` and `PurchasesController.List` both built
    their response with `Task.WhenAll(rows.Select(BuildResponseAsync))` —
    running `BuildResponseAsync` concurrently for each row. `BuildResponseAsync`
    queries the shared `_db` `DbContext`, which is **not thread-safe** for
    concurrent operations on the same instance; EF Core throws `"A second
    operation was started on this context instance before a previous operation
    completed"` as soon as there are 2+ rows to build. Both fixed the same way:
    a plain sequential `foreach` awaiting `BuildResponseAsync` one row at a
    time instead of `Task.WhenAll`. **Pattern to watch for elsewhere in this
    codebase**: never `Task.WhenAll` (or any concurrent-await) a set of calls
    that all close over the same injected `DbContext` — DI registers it scoped
    per-request, so nothing stops you from writing this bug, but EF Core will
    throw at the first row count that overlaps two of those tasks in time.
- Verified live: Manager creates a Purchase Request → Owner approves it →
  Manager (or Owner) records a Purchase against it (auto-flips the request to
  `Fulfilled`, increments stock, updates the Ingredient's weighted-average
  cost) → partial payment recorded against the Purchase (`Unpaid` →
  `PartiallyPaid`, remaining balance correct). Also verified Opname on an
  existing Ingredient (Beras: 12800 → 12500 gram) updates the row without
  touching `AverageCost` (Opname is a count correction, not a valued
  movement — matches `IngredientsController.AdjustStock`'s existing comment on
  this).
- **Not done in Day 3, deferred by explicit agreement** (see
  `docs/architecture/02-ui-roadmap.md`'s "Where We Are"): CRUD for the menu
  itself (Category/Product + Recipe) — the backend only has Create + List for
  both, no Update/Delete yet, and there's no UI for it either. Raised mid-Day-3
  by the project owner; deliberately scoped out to keep this session's Purchasing
  work from sprawling into a second domain that also needs new backend
  endpoints. Next thing to discuss once Phase 3 ships.

### Web UI — Day 4 (Menu CRUD: Category rename, Product + Recipe edit)

Picked up the Day 3 deferral above immediately, by explicit agreement, once the
owner asked how Recipe ties into stock deduction (confirmed: `RecipeItem` maps
Product → Ingredient quantities, and `Checkout` reads it to compute how much of
each Ingredient a sale consumes — see `OrdersController.Checkout` step 1-2, unchanged
by this work). Since correcting a wrong recipe or price previously required
editing the database directly, this was scoped ahead of Phase 4 (Reporting) —
reports read this same data, so a shaky source was worth fixing first.

- **New backend endpoints** (`Owner,Manager` only, matching Create's existing
  gate):
  - `PUT /api/categories/{id}` — rename only, no Delete. `Product.CategoryId` is
    a required FK, so a Category still referenced by any Product can't be
    removed without cascading or orphaning — deferred rather than building an
    unused "delete only if empty" guard.
  - `PUT /api/products/{id}` — updates Name/CategoryId/Price/Station/IsActive
    and replaces the Recipe wholesale (delete all existing `RecipeItem` rows,
    insert the ones given) rather than diffing line-by-line; a menu item's
    recipe is small and edited as a whole in the UI.
  - **No hard Delete for Product.** `Product.Id` is a required FK on both
    `OrderItem` and `RecipeItem` — a Product that has ever been ordered can't
    be removed without breaking that order's history. `IsActive` (already on
    the entity since Day 1 of the catalog) is the only safe removal path;
    `ProductsController.List` already filtered on it, so deactivating a
    product now correctly makes it disappear from the cashier's menu
    immediately. `List` gained an `includeInactive` query flag so the new
    admin UI can still see (and reactivate) deactivated products — the
    cashier-facing call omits it and keeps seeing only active ones.
  - **Bug caught before it shipped**: the first version of `Update` called
    `product.RecipeItems.Add(new RecipeItem {...})` for the replacement
    lines — this is the exact "adding a child to an already-tracked parent's
    collection doesn't reliably mark it Added" gotcha already written up
    earlier in this file (from Day 5-ish), and it reproduced immediately: a
    live edit through the UI threw `DbUpdateConcurrencyException` ("expected
    to affect 1 row, but affected 0"), because EF Core generated an `UPDATE`
    for a `RecipeItem` row that didn't exist yet instead of an `INSERT`.
    Fixed the same way the existing writeup prescribes: add the new items to
    `_db.RecipeItems` directly instead of the navigation collection. Confirms
    that gotcha is a real, currently-live trap in this codebase, not just
    historical.
- **Frontend**: `/menu` hub linking to `CategoriesPage` (list, create, inline
  rename) and `ProductsAdminPage` (list including inactive, create form, and
  an edit modal reusing the same recipe-line editor UI pattern as
  `PurchaseRequestsPage`/`PurchasesPage`'s ingredient-line editors — pick an
  Ingredient, quantity, unit auto-filled from the Ingredient's own unit).
  Dashboard link and both pages' create/edit controls are gated to
  Owner/Manager client-side, matching the backend's actual enforcement.
- **Verified live, not just that the form saves**: edited Nasi Goreng Ayam's
  price (Rp25.000 → Rp27.000) through the UI, confirmed via direct API read
  that its Recipe still had exactly 3 lines (Ayam/Telur/Beras, no duplicates
  or stale rows) after the replace, then ran a full Checkout against a fresh
  Order for that product — stock deduction (`Ayam` −80g, matching the
  Recipe) and the charged total (Rp27.000, the new price) were both correct,
  confirming an edited Recipe still drives Checkout's stock math correctly,
  not just that the edit screen displays right. Also deactivated Es Teh Manis
  through the UI and confirmed via direct API read that it disappeared from
  the default (cashier-facing) `GET /api/products` response while still
  showing up with `includeInactive=true`.

### Bug fix — orphaned Draft Order permanently occupies its Table

Reported by the project owner after live-using the Day 2 flow: tapping into a
Table just to look (not to seat a party) left it stuck `Occupied` forever, even
after backing out without ordering anything.

- **Root cause**: `TablesController.Create` claims the Table (`Available` →
  `Occupied`) the instant an Order is created — correct for an actual "seat a
  party" action, but `TablesPage.handleTableClick` calls `createOrder` on
  *every* tap of an `Available` tile, including a cashier just checking what's
  on a table. There was no way to abandon that Order afterward: `Void` only
  works on `Completed` orders (PIN-gated, reverses a settled sale), and
  nothing existed for a pre-checkout Draft/Open order with nothing to reverse
  (no Payment, no Stock movement, no Journal entry — Checkout is the only
  thing that touches any of those).
- **Fix**: new `OrderStatus.Cancelled` value and `POST /api/orders/{id}/cancel`
  — atomically claims `Draft`/`Open` → `Cancelled` (same guarded-transition
  pattern as every other status change in this controller) and releases the
  Table in the same request. No PIN/role gate, unlike Void: cancelling can
  only ever discard an order nobody has paid for, so it isn't a sensitive
  action. Frontend: a "Batal Order" button on `OrderPage` while the order is
  still Draft/Open.
- **Also fixed the same session**: Checkout previously redirected straight
  back to `TablesPage` the instant payment succeeded, with zero confirmation
  — reported as QRIS "just blinking" (Cash had the identical behavior, but it
  read as more obviously broken for QRIS since there's no real scan/gateway
  step to visually anchor the wait, see the "QRIS-manual" note in the Payment
  section above). `OrderPage` now shows a payment-confirmation screen (amount
  + method) with an explicit "Kembali ke Meja" button instead of navigating
  immediately.
- **Dev-data cleanup**: while investigating, found several `Table`s (`6`, `8`)
  already stuck `Occupied` by empty Draft Orders from earlier sessions —
  pre-existing instances of this same bug, not new occurrences. Cancelled them
  via the new endpoint once it shipped. Also bulk-deleted the accumulated
  stress/race/load-test fixture data (`RaceTest-*`, `StressTest-*`,
  `LoadTest-*`, `IdempotencyTest-*`, `Scarce/PlentifulIngredient-*`, etc. —
  see the Concurrency section below for what generated them) from the dev
  Postgres via a one-off transactional SQL script, in dependency order
  (children before parents, reversal `JournalEntry` rows before the ones they
  reverse). Not a schema or code change — just clearing accumulated test
  noise so the UI reflects only real menu/catalog data during manual
  end-to-end testing.

### Inline "+ Bahan Baru" in every ingredient picker

Requested after the owner noticed the ingredient `<select>` (Product recipe
editor, Purchase Request lines, Purchase lines) only ever listed *existing*
Ingredients — adding a new one meant leaving the form, going to
`/purchasing/ingredients`, then coming back. New shared component
`web/src/components/IngredientSelect.tsx` wraps that `<select>` with a
`+ Bahan Baru` option that opens a small inline create-Ingredient modal
(name + unit; `MinimumStock` defaults to 0, editable later same as always)
without losing the form the cashier/manager was in the middle of filling out.
Dropped into all three pickers (`ProductsAdminPage`'s recipe editor,
`PurchaseRequestsPage`, `PurchasesPage`) in place of their inline `<select>`.

- **Bug caught before it shipped, twice, same underlying cause**: this
  modal's `<form>` needed `React.createPortal(..., document.body)` — without
  it, the `<select>` (rendered inside whatever `<form>` hosts the picker,
  e.g. `ProductsAdminPage`'s edit-Product form) put an inner `<form>` inside
  an outer `<form>`, which HTML forbids; the browser silently repaired the
  invalid markup, and submitting the inner form ended up submitting the
  outer one instead. **After** adding the portal, the exact same symptom
  reappeared (saving a new Ingredient here still closed and saved the whole
  Product edit) — because **React re-dispatches a portaled element's events
  along the React component tree, not the DOM tree**: even placed in
  `document.body`, this form is still a React-tree descendant of the page's
  outer `<form>`, so a `submit` event bubbles into `onSubmit={handleEditSave}`
  regardless of where the portal physically renders it. Fixed with
  `event.stopPropagation()` in the inner form's submit handler, in addition
  to the portal (the portal alone only fixes the DOM-nesting violation, not
  the event-bubbling one — both were needed). **Pattern to remember**: any
  portaled `<form>`/interactive element nested (in the *component* tree) inside
  another `<form>` needs both fixes together, not just one.
- Verified live: opened the Ingredient picker inside the Product edit
  modal for Nasi Goreng Ayam, created "Kopi Arabika" without leaving the
  modal, confirmed only the create-Ingredient modal closed (Product edit
  modal stayed open, no premature save), and the new Ingredient was
  auto-selected in the recipe line that triggered it.

## Concurrency
- "At most one open Shift per user" is enforced by a **partial unique index**
  (`shifts (UserId) WHERE Status = 0`), not just the `AnyAsync` check in
  `ShiftsController.Open`. The application-level check alone is check-then-insert and
  race-prone; verified with 5 concurrent open-shift requests for the same user — the
  DB constraint let exactly 1 through and the other 4 got a clean `409`, not a crash.
  This is the pattern to follow for any other "at most one X" invariant going forward
  (don't trust an `AnyAsync` check alone under concurrency — back it with a DB
  constraint and catch `DbUpdateException` to turn the violation into a clean 4xx).

- **Stock deduction is an atomic conditional `UPDATE` per Ingredient, not
  read-modify-write.** The first version of `Checkout` read `Stock.Quantity`, computed
  the new value in C#, and wrote it back via the normal change tracker — the textbook
  lost-update race. A stress test (`StockRaceConditionTests`, see `tests/`) fired 20
  concurrent checkouts against 5 units of stock: **all 20 "succeeded"**, massively
  overselling, with zero errors anywhere — this is the dangerous kind of bug, since
  nothing looks wrong until someone reconciles stock later. Fixed by replacing the
  read-then-write with
  `_db.Stocks.Where(s => s.IngredientId == id && s.Quantity >= required).ExecuteUpdateAsync(...)`
  inside an explicit transaction: this is a single atomic SQL statement, so Postgres
  row-locks the matched row for its duration — a concurrent request re-evaluates
  `Quantity >= required` against the value just written, never a stale read. If 0 rows
  are affected, that Ingredient was short (checked without a separate read
  beforehand). Re-ran the same stress test after the fix: exactly 5 of 20 succeed, the
  rest get a clean `400`, final stock is exactly 0, never negative.
  **Pattern to reuse anywhere else in this codebase that decrements a shared
  counter/quantity under potential concurrency**: never "SELECT then subtract then
  SAVE" — use a conditional `ExecuteUpdateAsync` (or raw atomic SQL) and check rows
  affected instead.
- A second stress test (`CheckoutLoadTests`) covers the complementary failure mode:
  plentiful stock, 50 genuinely concurrent full checkout flows, asserting the **exact**
  final quantity (not just "no errors") — a lost update here would under-deduct
  silently while every request still reports `200 OK`. Also passes post-fix.
- **A third, independent race: two concurrent checkouts on the SAME order.** The stock
  fix above says nothing about whether the *Order* itself can be checked out twice —
  it only guards the Ingredient quantity. Wrote a targeted follow-up stress test
  (`OrderCheckoutRaceTests`) that fires 10 concurrent checkout calls at one Order: all
  10 "succeeded" before the fix — 10x Payment, 10x JournalEntry, 10x stock deduction
  for what should be a single sale. Fixed the same way as the stock race: atomically
  claim the Order via
  `_db.Orders.Where(o => o.Id == orderId && (Status == Draft || Status == Open)).ExecuteUpdateAsync(SetProperty(o => o.Status, Completed))`
  as the very first write inside the transaction, checking rows-affected, before any
  stock/payment/journal work happens. Only the request that wins the claim proceeds;
  everyone else gets a clean 400 immediately. Re-verified 3x for stability: exactly 1
  of 10 succeeds every time.
  - **Follow-on bug this fix introduced, caught by manual smoke test (not the stress
    test)**: `ExecuteUpdateAsync` writes directly to the DB and bypasses EF Core's
    change tracker, so the `Order` instance already loaded earlier in the same request
    keeps its stale pre-checkout `Status` in memory. `BuildOrderResponse` then
    re-queried on the *same* `DbContext`, and EF's identity map handed back that
    already-tracked (stale) instance instead of re-reading the column — so a
    successful checkout's response reported `"status": "Draft"` even though the DB
    correctly said `Completed`. Fixed with `.AsNoTracking()` on the query in
    `BuildOrderResponse`. **Lesson**: after any `ExecuteUpdateAsync`/raw SQL mutation
    within a request, don't trust a tracked re-query on the same context to reflect
    it — use `AsNoTracking()` (or re-attach/reload explicitly) for anything read back
    afterward.

### Web UI — Day 5 (Phase 4: Reporting Dashboard)

Completes roadmap Phase 4. All three report endpoints (`sales-daily`,
`stock-levels`, `waste`) already existed and needed zero backend changes —
this was purely a frontend pass over already-correct, already-tested
aggregates.

- **`ReportsPage`**, one page, three cards, matching the design agreed before
  building (no separate sub-routes — each card is light enough not to need
  its own navigation level):
  - **Penjualan Harian**: date picker (defaults to today), completed/voided
    order counts, Cash/Non-Cash/Total revenue.
  - **Level Stok**: full ingredient table with a "Rendah" badge on rows
    below `MinimumStock` — reuses the exact badge/table classes already
    established in `IngredientsPage` (`purchasing-table`,
    `purchasing-badge--warn`) rather than inventing new ones.
  - **Waste Bulanan**: month/year picker (defaults to current month),
    per-ingredient quantity + value lost, total at the bottom. Empty state
    ("Tidak ada waste bulan ini") verified live against a month with no
    waste movements.
- Gated Owner/Manager only, matching the backend's
  `[Authorize(Roles = "Owner,Manager")]` on the whole `ReportsController` —
  same pattern as `PurchaseRequestsPage`: the role check happens *before*
  the `useEffect`s fire, so a Cashier's browser never calls the report
  endpoints at all (would 403 anyway, but the point is not to fire a
  request that's going to fail). First pass skipped this and let the 403
  surface as a raw "Request failed with status 403" — caught during live
  verification, fixed to match the existing page-level gate pattern instead
  of inventing a new one.
- Verified live: Owner sees all three cards populate with real data
  (matched a manual checkout done earlier in the same session); switching
  the sales-daily date to a day with no orders correctly zeroes every
  field instead of erroring; switching the waste month to one with no
  waste movements shows the empty state, not a crash; Cashier gets the
  "khusus Manager/Owner" message with no network call, both via direct
  navigation and via the Dashboard (no "Laporan" link shown at all).
- **Not done, deliberately split into its own phase**: charts, export
  (PDF/Excel), and period-over-period comparison — raised during the design
  discussion for this phase, but none of the three report endpoints carry
  the data those need (e.g. comparison needs a date-range query, not a
  single date/month). Tracked as Phase 7 in `02-ui-roadmap.md`, not scoped
  yet.

### Web UI — Day 6 (Phase 7: Reporting Enhancements — charts, export, comparison)

Completes the scope agreed on 2026-09-30 (see `02-ui-roadmap.md`'s Phase 7): charts
on all three Phase 4 reports, Excel/CSV/PDF export, and automatic this-period-vs-
previous-period comparison — including for Stock Levels, which was originally an
open question and got resolved (owner wants both frequency *and* depletion-rate
charts, and wants comparison for Stock too, not just Sales/Waste).

- **Three new backend endpoints**, all read-only aggregates over existing data —
  no new tables, no new invariants:
  - `GET /api/reports/sales-range?from=&to=` — same per-day revenue logic as
    `sales-daily`, just returned as one row per day across a range instead of one
    call per day from the frontend.
  - `GET /api/reports/waste-range?from=&to=` — same monthly aggregation as
    `waste`, grouped across a range of months instead of one call per month.
  - `GET /api/reports/stock-trend?from=&to=` — the interesting one. There's no
    stock-history table; a daily balance series per Ingredient is *reconstructed*
    from the `StockMovement` ledger: `currentStock` (always "now") minus every
    movement that happened after `from`'s start gives the balance exactly as it
    stood at the start of `from` (every later movement is what turned that
    starting balance into today's `currentStock`, so subtracting all of them
    "rewinds" it) — this needs movements all the way up to *today*, not just up
    to `to`, since a movement between `to` and today still happened "after
    `from`" and must be rewound too. From that starting balance, the visible
    series is walked forward day-by-day using only movements inside `[from, to]`.
    Returns, per Ingredient: `belowMinimumDays` (days where the reconstructed
    balance was under `MinimumStock`, only counted when a threshold is actually
    configured — same "0 means not configured" rule as every other stock report)
    and `netChangePerDay` ((endBalance − startBalance) / days — a *net* trend,
    not gross consumption; a Purchase and a Sale on the same day partially offset
    by design, since what matters operationally is whether the ingredient is
    trending down overall, not a breakdown of why).
  - Comparison ("this period vs. previous") needed no new endpoint for any of the
    three — the frontend just calls the same range endpoint twice (current range,
    and an equal-length immediately-preceding range) and diffs client-side.
  - Verified live via `curl`: `stock-trend` against real dev data correctly
    reconstructed Telur's balance dropping 100 → 92 across 3 days (matching what
    the UI had shown), and temporarily setting Telur's `MinimumStock` to 95
    correctly flagged `belowMinimumDays: 2` (the two days its reconstructed
    balance was 93/92, not the one day it was still 100) — confirms the ledger
    rewind math, not just that it returns *a* number.
- **Frontend**: extended `ReportsPage` (not a new route) with a "Tren ..." card
  under each of the three Phase 4 point-in-time cards — date-range pickers, a
  "Bandingkan periode sebelumnya" checkbox, a Chart.js line/bar chart, and Excel/
  CSV/PDF export buttons (new `web/src/lib/export.ts` helper, new `chart.js`,
  `xlsx`, `jspdf`, `jspdf-autotable` dependencies). New reusable `ReportChart`
  component wraps Chart.js directly (no `react-chartjs-2` wrapper — one extra
  dependency avoided for what's a thin `useEffect` + `useRef` either way).
  - **Known, accepted supply-chain note**: the npm-published `xlsx` (SheetJS)
    package is frozen at 0.18.5 and carries two disclosed CVEs (prototype
    pollution, ReDoS) — both in the *parsing* path (`XLSX.read`/`readFile`).
    This app only ever calls `XLSX.utils.json_to_sheet`/`writeFile` on data it
    already fetched and rendered itself — never on a user-supplied file — so the
    vulnerable code path is never reached. Documented in `export.ts` directly so
    this isn't re-discovered as a surprise later.
- Gated the same Owner/Manager check as the rest of the page (no separate
  gate needed — the new cards live inside the same already-gated component).
- Verified live: sales trend chart matched the manual-checkout total from an
  earlier session; toggling comparison correctly showed a second series and a
  delta line; stock trend's two charts (frequency, net-change) rendered
  per-ingredient bars matching the `stock-trend` API values exactly; waste trend
  correctly showed Rp0 bars for empty months and the real total for September;
  all three Export buttons (Excel/CSV/PDF) fired without a console error or a
  page crash on every card.

## Gotchas (bugs already hit — read before you hit them again)

### JWT `sub` claim silently disappears
ASP.NET Core's `JwtBearerHandler` remaps short claim types (`sub`, `role`, ...) to
long XML-schema URIs by default when validating an incoming token
(`ClaimTypes.NameIdentifier` instead of `"sub"`). If you read claims back with
`JwtRegisteredClaimNames.Sub`, you'll get nothing and it'll look like the claim was
never issued. **Fix applied**: `options.MapInboundClaims = false;` in the JwtBearer
setup in `Program.cs`. If you ever see `"Token has no sub claim"` despite the token
clearly having one (check on jwt.io), this is almost certainly why.

### Adding a child entity to an already-tracked parent's collection doesn't reliably mark it `Added`
When you load a tracked entity (e.g. `Order` via `.Include(o => o.Items)`) and then do
`order.Items.Add(newItem)`, EF Core does **not** reliably detect `newItem` as a new row
to insert. In our case it generated an `UPDATE order_items ... WHERE Id = @id` for a
row that didn't exist yet, which naturally affected 0 rows and threw
`DbUpdateConcurrencyException`. This only bites when the *parent* was already tracked
(fetched from DB) before the child was added — it does **not** happen when the parent
itself is newly `Add()`-ed in the same operation (e.g. `Product` + its `RecipeItems` on
creation work fine, because `Product` is new too and EF cascades `Added` state through
the whole graph from the root).

**Fix**: add the child explicitly to its own `DbSet` (`_db.OrderItems.Add(item)`)
instead of relying on collection-navigation fixup to mark it `Added`.

**Related trap**: once you do that, EF Core's relationship fixup *also* auto-appends
the item into `order.Items` in memory (because the FK now matches a tracked parent).
If you *also* manually call `order.Items.Add(item)` "to be safe", the item ends up
**twice** in the in-memory collection (same object reference, so same `Id` shown
twice in any response built from it) — and anything summing over `order.Items` (like
`RecalculateTotal()`) double-counts it. Only one of the two `.Add()` calls is ever
needed; use the explicit `DbSet.Add()` and let fixup handle the navigation.

## Fast-follow (deferred, tracked so nothing gets silently forgotten)

See `docs/architecture/01-mvp-technical-design.md` §"Scope Recap" for the full list.
Two worth calling out here because they're partial/coupled to what's built:
- Login-by-username-only breaks with a second Tenant (see Identity notes above).
- `Shift` close now exists (Day 11) but there's still no UI for it — API only.
- **Test-class isolation**: `TestWebApplicationFactory` runs every test class against
  the same live dev Postgres (already flagged in that file as a fast-follow), and
  xUnit runs different test classes in parallel by default. Confirmed while verifying
  Day 11: running the full suite normally intermittently fails a *different*
  concurrency-heavy test each time (`StockRaceConditionTests`, `CheckoutLoadTests`,
  `TableClaimRaceTests`) with a `500` from two test classes' `EnsureShiftOpenAsync`/
  table-claim logic genuinely colliding on the same seeded `cashier` account and
  Tables — this reproduces identically on commits before Day 11 too, so it's
  pre-existing, not a regression. Running with test-collection parallelization off
  (`dotnet test -- xUnit.ParallelizeTestCollections=false`) passes 7/7 reliably.
  Also: resetting the dev DB (`docker compose down -v`) right before running the full
  suite can trigger a *second*, sharper failure — multiple test classes' parallel
  `WebApplicationFactory` startups race `DataSeeder`'s check-then-insert seed guard on
  a truly empty DB, seeding several `Tenant` rows instead of one (seen: 6). Seeding
  once via a single `dotnet run` before running tests avoids it. Proper fix is
  disabling xUnit collection parallelization for this assembly (or giving each test
  class its own tenant/user), not done yet.
