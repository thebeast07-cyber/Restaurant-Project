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

Sprint day: **4 / 14** (see technical design doc for the full day-by-day plan).
**Walking skeleton milestone reached**: Order → Checkout (Cash) → Inventory
deducted via Recipe → balanced Finance journal posted, all in one atomic transaction.
Verified down to raw SQL (stock_movements, journal_lines), not just API responses.

**Explicit decision after Day 3**: backend-first. Everything built so far is API-only,
tested via `curl` — no frontend exists yet. UI is a deliberately separate block of
work, not interleaved per day as the original sprint table implied.

Done: Identity/Auth, multi-tenant isolation, Catalog + Recipe, Table/Shift/Order (cart),
Payment (Cash only), Inventory deduction, Finance journal (background, no UI).
Not started: QRIS, Void, Station ticket printing, Table occupancy locking, Shift close,
Reporting, any UI.

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
  single in-process method (not a message/broker) that does everything in one
  `SaveChangesAsync`: validate → deduct stock via Recipe → post journal → complete
  order. Atomic by construction (one unit of work), not by wrapping multiple calls in
  an explicit transaction.
- Stock check happens **before** any mutation (compute required-per-Ingredient first,
  compare against `Stock.Quantity`, reject with a full shortage list if anything's
  short). Verified: an over-quantity order that would drain multiple ingredients at
  once gets rejected with zero partial deduction — checked at the DB, not just via the
  API response.
- Journal posts a simple Cash/Revenue pair only — **no COGS/InventoryAsset lines**.
  That needs a per-Ingredient unit cost, which isn't modeled yet (`Ingredient` has no
  cost field). This is a known, deliberate gap, not an oversight — flagged again here
  so it doesn't get silently assumed "done" later.
- Cash and QRIS (once built) both post to the same `"1000" Cash` account for now —
  there's no separate bank/e-wallet clearing account because neither goes through a
  real payment gateway yet.
- Enums are serialized as strings in JSON now (`"Cash"`, `"Kitchen"`, `"Completed"`),
  not raw ints — added `JsonStringEnumConverter` globally in `Program.cs` while
  building this. Applies retroactively to every endpoint, not just Payment/Checkout.

### Concurrency
- "At most one open Shift per user" is enforced by a **partial unique index**
  (`shifts (UserId) WHERE Status = 0`), not just the `AnyAsync` check in
  `ShiftsController.Open`. The application-level check alone is check-then-insert and
  race-prone; verified with 5 concurrent open-shift requests for the same user — the
  DB constraint let exactly 1 through and the other 4 got a clean `409`, not a crash.
  This is the pattern to follow for any other "at most one X" invariant going forward
  (don't trust an `AnyAsync` check alone under concurrency — back it with a DB
  constraint and catch `DbUpdateException` to turn the violation into a clean 4xx).

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
- `Shift` close/reconciliation UI doesn't exist — a shift can be opened but never
  closed yet.
