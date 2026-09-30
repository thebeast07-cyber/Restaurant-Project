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

### Phase 7 — Reporting Enhancements (Charts, Export, Period Comparison)
Raised during the Phase 4 design discussion (2026-09-30). Scoped 2026-09-30 per the
owner's answers below — still needs an implementation design pass (this is scope,
not a build plan) before coding starts.

- **Charts wanted on all three Phase 4 reports:**
  - **Sales trend** — daily sales over a range (e.g. last 30 days), not just the
    single-day view Phase 4 has. **Needs a new backend endpoint**:
    `GET /api/reports/sales-range?from=&to=` returning one row per day
    (reuses `SalesDailyReport`'s shape, just looped server-side instead of one
    query per day from the frontend).
  - **Stock levels** — which ingredients run low most often / deplete fastest.
    **No new table needed** — `StockMovement` is already a full timestamped
    ledger (every change, with `Reason` and `CreatedAt`), so a stock-level-over-time
    series can be reconstructed by summing `ChangeQuantity` up to each point in
    time, without a new snapshot mechanism. Still needs its own query design
    (this is a running-balance reconstruction, not a simple `SELECT`) — open
    question to resolve at design time: chart the *frequency* of dropping below
    minimum, or the *rate of depletion* (steepness of the decline)? Different
    queries, different chart.
  - **Waste trend** — waste value per month over a range, analogous to sales
    trend. **Needs a new endpoint**: `GET /api/reports/waste-range?from=&to=`
    (year/month pairs), looping the existing monthly aggregation instead of
    one query per month from the frontend.
- **Export**: both Excel/CSV and PDF, for all three reports. Frontend-only —
  export from data already fetched for display, no new backend needed. New
  frontend dependencies required (not yet in `web/package.json`): an Excel/CSV
  library (e.g. `xlsx` or `papaparse`) and a PDF library (e.g. `jspdf` +
  `jspdf-autotable`) — flagged here so pulling them in isn't a surprise
  mid-implementation.
- **Period comparison**: "this period vs. previous period" (e.g. this week vs.
  last week, this month vs. last month), automatic — not a free two-range picker.
  Applies to Sales and Waste (Stock Levels is a point-in-time snapshot, comparison
  doesn't apply the same way — needs its own call at design time whether "compare"
  means anything for stock, or whether it's Sales/Waste only). Built on top of the
  same `sales-range`/`waste-range` endpoints above: fetch both periods, diff
  client-side — no separate "comparison" endpoint needed.
- **Not yet decided, needs its own design pass before build**: exact query shape
  for the stock-level time-series (frequency vs. depletion-rate), and whether
  Stock Levels gets a comparison view at all.

### Not Planned (P1/P2/P3 — explicitly out of scope for this roadmap)
HR/attendance, CRM/loyalty, Promotion, multi-branch admin UI, custom RBAC builder,
Production (raw→semi-finished conversion), Batch/Serial tracking, Marketplace/
Omnichannel integration. See `01-mvp-technical-design.md`'s "Ditunda" list — nothing
here has changed since that was written.

## 4. Known Standing Risks (carried over, not re-litigated here)

- Login is username-only, breaks with a 2nd Tenant (see `implementation-notes.md`,
  Identity & Auth) — not a blocker for a single-outlet launch, but must be fixed
  before onboarding a second outlet.
- Test-suite isolation issue (shared dev Postgres across parallel test classes) —
  a CI/dev-workflow risk, not a production risk, but should be fixed before setting
  up CI. See `implementation-notes.md`'s Fast-follow section.
