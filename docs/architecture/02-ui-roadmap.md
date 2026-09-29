# 02 - UI Roadmap & Post-MVP Plan

> Living roadmap, not a fixed spec — update this as decisions get made and phases
> complete, the same way `implementation-notes.md` tracks reality against
> `01-mvp-technical-design.md`'s original plan.

## 0. Where We Are

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
| 4 | Does Kitchen/Bar need their own screen (a KDS-style ticket display), or is a printed ticket (once the printer is wired) sufficient for v1? | Changes Phase 2 scope significantly | **Open** |

**Recommendation**: resolve #1-#3 together as one conversation before writing any
frontend code — they're coupled (e.g. "tablet app" implies native or a
cross-platform framework, which rules out plain server-rendered pages).

## 3. Phases

Each phase should get its own "discuss the approach, agree, then build" pass, same
model as the Purchasing extension — not built autonomously end-to-end like the
original Day 1-14 sprint.

### Phase 1 — UI Foundation + Core POS Flow
The minimum a Cashier needs to run a shift without touching `curl`:
- Login
- Open/close Shift (with cash reconciliation display)
- Table view → seat a party → build an order (cart) → checkout (Cash/QRIS)
- Void (PIN-gated), for Manager/Owner

This alone would make the system operable for the core sales loop — the highest-value
slice to ship first.

### Phase 2 — Kitchen/Bar + Station Routing
- Depends on Decision #4 above.
- If a KDS screen is wanted: a simple ticket-list view per station (Kitchen/Bar),
  reading from `POST /api/orders/{id}/send-to-station`'s existing output.
- If printed tickets are sufficient: this phase merges with the printer integration
  work below instead of needing its own UI screen.

### Phase 3 — Back-Office: Purchasing & Suppliers
- Supplier management, Purchase Request creation (Manager) + Approval (Owner),
  recording a Purchase, recording Purchase Payments.
- Stock Adjustment/Opname screen, Waste recording.
- This phase can lag behind Phase 1-2 without blocking daily sales operation — a
  Manager/Owner could still use `curl`/Postman for Purchasing a while longer if
  Phase 1 ships first and this needs more time.

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
- **Formal Refund flow** (post-settlement credit, distinct from same-day Void) —
  explicitly out of MVP scope from the start (PRD §11), revisit if the business
  actually needs post-settlement refunds.
- **FIFO costing** (replacing Weighted-Average) — only if Weighted-Average's
  accuracy turns out to be insufficient in practice; see `implementation-notes.md`
  for why Weighted-Average was chosen first.

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
