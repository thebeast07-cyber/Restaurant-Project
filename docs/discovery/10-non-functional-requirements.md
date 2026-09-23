# 10 - Non-Functional Requirements

This document outlines the system-level requirements inferred from an ERP platform operating at Majoo's scale.

## 1. Security
- **Tenant Isolation:** Data must be strictly isolated. A user from Tenant A must never be able to access Tenant B's data via URL manipulation (IDOR) or API spoofing.
- **Authentication:** Secure JWT/Session management. APIs must be authenticated via Bearer tokens.
- **Authorization:** Fine-grained RBAC. Sensitive endpoints (Refund, View COGS) must validate the user's specific permissions.
- **Audit Logging:** All sensitive actions (Void, Refund, Stock Adjustment, Journal Edit) MUST be logged with the UserID, Timestamp, and previous/new state.

## 2. Reliability & Consistency
- **Transactional Integrity:** POS transactions span multiple domains (Sales, Payment, Inventory, Finance). The system must use transaction blocks (ACID) or reliable distributed transactions (Outbox Pattern/Sagas) to ensure a payment is never recorded without the corresponding inventory deduction.
- **Idempotency:** Webhooks from payment gateways (majoo Pay, external E-wallets) must be idempotent. Retries from the gateway must not double-count revenue.
- **Offline Mode (POS):** POS applications must be able to operate (cache menu, calculate totals, store encrypted transactions locally) during brief network outages and sync when connectivity returns.

## 3. Performance & Scalability
- **POS Latency:** Barcode scanning and checkout operations must respond in < 500ms to prevent queues at the cashier.
- **High Concurrency:** The system must handle peak hours (e.g., Lunch rush 12:00-13:00, Dinner 18:00-20:00) where thousands of outlets are transacting simultaneously.
- **Read-Heavy Catalog:** The product catalog is read far more often than it is written. Aggressive caching (e.g., Redis) is required for product lookups.
- **Reporting Queries:** Complex aggregations (e.g., Yearly P&L across 50 branches) should not block the main transactional database.

## 4. Availability
- **Uptime:** Expected SLA of 99.9% for POS operations.
- **Disaster Recovery:** Automated daily backups and point-in-time recovery for the database.
