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

Sprint day: **14 / 14** (see technical design doc for the full day-by-day plan). See
[`go-live-checklist.md`](go-live-checklist.md) for the Day 14 go/no-go writeup.
**Walking skeleton milestone reached** (Day 4): Order → Checkout → Inventory deducted
via Recipe → balanced Finance journal posted, all in one atomic transaction. Verified
down to raw SQL, not just API responses. **Day 5**: stress-tested the concurrent paths
and found + fixed 2 real race conditions (see Concurrency section below) — this is the
part of the codebase with the most scrutiny so far.

**Explicit decision after Day 3**: backend-first. Everything built so far is API-only,
tested via `curl`/integration tests — no frontend exists yet. UI is a deliberately
separate block of work, not interleaved per day as the original sprint table implied.

Done: Identity/Auth, multi-tenant isolation, Catalog + Recipe, Table/Shift/Order (cart),
Payment (Cash + QRIS-manual), Inventory deduction, Finance journal (background, no UI),
station ticket routing/formatting (content only — no physical printer wired yet,
pending hardware confirmation), full table occupancy lifecycle (Available <-> Occupied,
concurrency-safe), Void (PIN-gated, reverses stock + journal, releases table, audited),
Stock adjustment/opname (Owner/Manager, writes StockMovement + AuditLog),
Shift close with cash reconciliation, basic Reporting (daily sales, stock levels).
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
