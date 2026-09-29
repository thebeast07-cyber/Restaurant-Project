# Go-Live Checklist (Day 14)

Sprint day 14/14 — buffer UAT, bug fix, checklist go-live (see
[`01-mvp-technical-design.md`](../architecture/01-mvp-technical-design.md) §4 Sprint
Breakdown). This is the checkpoint the sprint plan asks for: go/no-go for
operasional live.

**How to read this**: every "Done" item below is backed by a passing test or a
manual verification already logged in
[`implementation-notes.md`](implementation-notes.md) — not a self-assessment. Every
"Gap" item is a real, named limitation, not a hidden assumption.

## 1. Functional Scope — MVP Scope Recap Checklist

Cross-checked against the MVP Scope Recap in
[`01-mvp-technical-design.md`](../architecture/01-mvp-technical-design.md#scope-recap-hasil-alignment-sesi-ini).

| Capability | Status | Evidence |
|---|---|---|
| Auth, 3 fixed roles (Owner/Manager/Cashier) | ✅ Done | Day 1; role-gated on every sensitive endpoint (verified in this session's controller sweep) |
| 1 Tenant + 1 Branch (seeded) | ✅ Done | Day 1 — see Gap: breaks with a 2nd Tenant |
| Catalog (Product/Category/Recipe, ingredient mapping mandatory) | ✅ Done | Day 2 |
| Table management | ✅ Done | Day 3, full lifecycle Day 8 |
| Order: cart, draft, checkout | ✅ Done | Day 3-4 |
| Station routing (Kitchen/Bar) via printed ticket | ⚠️ Partial | Day 7 — ticket **content** only; no physical printer wired (hardware/connection type still pending, see Gap) |
| Payment: Cash + QRIS (manual/static) | ✅ Done | Day 4, Day 6 |
| Inventory: auto-deduction via Recipe on OrderPaid | ✅ Done | Day 4; race-condition-hardened Day 5 (`StockRaceConditionTests`, `CheckoutLoadTests`) |
| Stock adjustment/opname | ✅ Done | Day 10; race-hardened same day (`StockAdjustmentRaceTests`) |
| Finance: double-entry journal, auto-posted, no UI | ✅ Done | Day 4 — see Gap: no COGS/InventoryAsset lines |
| Void (PIN) + reverse stock/journal | ✅ Done | Day 9 (`VoidRaceTests`) |
| Audit log: void & stock adjustment | ✅ Done | Day 9-10 |
| Shift open/close + cash reconciliation | ✅ Done | Day 3 (open), Day 11 (close) |
| Reporting: daily sales + stock level | ✅ Done | Day 12 |
| Any UI | ❌ Not built | Explicit backend-first decision after Day 3 — separate work block, not scheduled |

## 2. Race-Condition / Concurrency Coverage

Every "at most one X" or "deduct a shared counter" invariant in the system is backed
by an atomic conditional `ExecuteUpdateAsync` (never read-then-write) and a dedicated
stress test — not assumed safe by inspection:

| Invariant | Test | Status |
|---|---|---|
| Stock never oversold/negative under concurrent checkouts | `StockRaceConditionTests` (20 vs 5, and literal 2-vs-1 last-unit repro) | ✅ Pass |
| Exact stock deduction under plentiful-stock concurrent load | `CheckoutLoadTests` (50 concurrent) | ✅ Pass |
| One Order can't be checked out twice (concurrent) | `OrderCheckoutRaceTests` (10 concurrent) | ✅ Pass |
| One Order can't be checked out twice (sequential retry) | `PaymentIdempotencyTests` | ✅ Pass |
| One Table can't be double-booked | `TableClaimRaceTests` (15 concurrent) | ✅ Pass |
| One Order can't be voided twice | `VoidRaceTests` (10 concurrent) | ✅ Pass |
| Stock adjustment/opname never loses an update | `StockAdjustmentRaceTests` (20 deltas + 10 opname counts) | ✅ Pass |
| At most one open Shift per user | DB partial unique index + `ShiftsController.Open` | ✅ Verified (5 concurrent opens, Day 3) |

**Full suite: 9/9 pass** with `dotnet test -- xUnit.ParallelizeTestCollections=false`.
See Gap below on why that flag matters right now.

## 3. Known Gaps — Require an Explicit Decision Before Real Launch

These are not bugs; they're scope lines drawn deliberately, each with a reason. Listed
so go-live is a conscious choice about each one, not a silent assumption.

1. **No physical printer integration.** `StationTicketFormatter` produces correct
   ticket content (verified), but nothing sends it to hardware. Blocked on: which
   connection type the printer unit supports (LAN preferred, USB works but ties the
   printer to one machine; Bluetooth ruled out on the dev machine). **Needs**: unit
   model + connection type confirmed before this can be finished — it's the single
   biggest gap between "backend done" and "usable in a real kitchen."
2. **Login is by username only, not tenant-scoped.** Works today because exactly one
   Tenant is seeded. **Breaks** the moment a second Tenant/outlet is onboarded — needs
   a tenant selector (subdomain or org code) added to login before multi-outlet
   rollout. Not a risk for a single-outlet go-live.
3. **No UI.** Every capability above is API-only, verified via `curl`/xUnit — nobody
   can actually run a shift on this without a client. This was an explicit,
   management-confirmed decision after Day 3, but it means **this backend alone is
   not launchable** — a UI (or a re-scoped "backend-only" definition of "launch",
   e.g. wired up to an existing POS terminal client) is a hard prerequisite, not a
   nice-to-have.
4. **No real payment gateway (bank/e-wallet).** Cash and QRIS-manual both post to the
   same `"1000" Cash` account; there's no async confirm flow, no clearing account, no
   webhook idempotency. Deliberately deferred — vendor/acquirer is still "TBD, needs
   a management decision" (PRD §14). Foundation for adding it without a rewrite is
   already in place (`PaymentMethod` is generic, `Payment.Status` has a `Pending`
   state) — see this session's "jembatan bank/e-wallet" discussion. Fine to launch
   without it if Cash/QRIS-manual covers real day-one payment methods.
5. **No COGS/InventoryAsset journal lines.** Journal posts a simple Cash/Revenue pair
   only — needs a per-Ingredient unit cost, which isn't modeled yet. Fine for
   go-live if Finance doesn't need COGS on day one; flagged so it isn't silently
   assumed done later.
6. **Purchasing/receiving not built**, despite being listed as P0 in the discovery
   docs (PRD §20). It's absent from this sprint's actual 14-day scope table — this is
   a discrepancy between the discovery-phase P0 list and what management actually
   scoped into this sprint, not an oversight during the sprint. Worth confirming
   explicitly with management before go-live: is Purchasing required for day-one
   operation (e.g. is initial stock only ever set via Opname, with no formal PO/GRN
   flow), or was it meant to be in this sprint and got dropped?
7. **Test-suite isolation.** All test classes share the single dev Postgres instance
   and one seeded `cashier`/`owner`/`manager` account; xUnit's default parallel
   test-class execution causes intermittent unrelated failures (confirmed
   pre-existing, not a Day 10-13 regression). Doesn't affect production behavior —
   it's a CI/dev-workflow risk, not a shipped-code risk — but will bite whoever sets
   up CI next if not fixed first. Workaround documented in `implementation-notes.md`.

## 4. Operational Prerequisites (Non-Code)

Carried over from the original sprint plan's open items — re-confirming none were
silently resolved during the sprint itself:
- Real menu (Product + Recipe) data, not sample data — needed before any real service.
- Real ingredient prices, for when COGS journal lines are eventually added.
- Printer hardware unit + confirmed connection type (see Gap 1).
- Payment gateway vendor decision, if bank/e-wallet is wanted at launch (see Gap 4).

## 5. Recommendation

**Backend P0 scope is functionally complete and concurrency-hardened** — every
scenario the sprint plan asked to be tested (last-unit stock race, payment
idempotency) passes, and every write path that touches a shared counter or a
single-owner status uses the atomic-claim pattern established on Day 5.

**Not launchable as-is**, for one structural reason (Gap 3, no UI) and one
operational reason (Gap 1, no printer) — neither is a code defect, both are known,
named, and require a decision/resource outside this codebase to close. Recommend:
treat this sprint's output as "backend P0: done," and scope the UI + printer
integration as the next explicit work block before scheduling an actual go-live date,
rather than treating Day 14 as the last day before real operation.
