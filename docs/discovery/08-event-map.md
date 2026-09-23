# 08 - Event Map

This document outlines the crucial domain events. Every event has a clear, singular Domain Owner (Producer).

## 1. `OrderPaid`
- **Producer (Owner):** Sales Domain
- **Trigger:** A POS order is successfully paid.
- **Consumers:**
  - *Inventory Domain:* Decrements stock for sold items.
  - *Finance Domain:* Generates double-entry Journal Entry (Debit Cash, Credit Revenue).
  - *CRM Domain:* Adds loyalty points to the Customer.
  - *HR Domain:* Attributes commission to the Employee.

## 2. `PaymentReceived`
- **Producer (Owner):** Payment Domain
- **Trigger:** Webhook received from majoo Pay or EDC confirming funds capture.
- **Consumers:**
  - *Sales Domain:* Triggers the `OrderPaid` event (state transition to PAID).

## 3. `OrderVoided`
- **Producer (Owner):** Sales Domain
- **Trigger:** Manager authorizes a void on a same-day transaction.
- **Consumers:**
  - *Payment Domain:* Initiates void request to Gateway.
  - *Inventory Domain:* Reverses stock deduction.
  - *Finance Domain:* Reverses the Journal Entry.

## 4. `GoodsReceived`
- **Producer (Owner):** Purchasing Domain
- **Trigger:** Warehouse staff completes a Goods Receipt against a Purchase Order.
- **Consumers:**
  - *Inventory Domain:* Increments stock. Updates moving average capital cost (HPP).
  - *Finance Domain:* Creates an Accounts Payable (AP) invoice.

## 5. `StockTransferDispatched` / `StockTransferReceived`
- **Producer (Owner):** Inventory Domain
- **Trigger / Lifecycle (Reconciled):**
  1. Branch B (Source) dispatches stock -> Emits `StockTransferDispatched`.
     - *Consumer (Inventory):* Source stock is deducted. Quantity is moved to a virtual "In-Transit" bucket.
  2. Branch A (Destination) receives stock -> Emits `StockTransferReceived`.
     - *Consumer (Inventory):* "In-Transit" bucket is cleared. Destination stock is increased.
- *(Source: INFERENCE - Required to prevent stock duplication during transit).*

## 6. `EmployeeClockedIn` / `EmployeeClockedOut`
- **Producer (Owner):** HR Domain
- **Trigger:** Employee submits attendance selfie.
- **Consumers:**
  - *HR Domain:* Updates timesheet, calculates late penalties for payroll.

## 7. `MarketplaceOrderPulled`
- **Producer (Owner):** Omnichannel Domain
- **Trigger:** Integration API pulls a new order from Tokopedia/Shopee.
- **Consumers:**
  - *Sales Domain:* Creates a Pending order.
  - *Inventory Domain:* Decrements stock to prevent physical overselling.
