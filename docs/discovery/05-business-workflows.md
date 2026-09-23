# 05 - Business Workflows

This document outlines the core business workflows. State transitions are clearly marked based on their evidentiary backing.

## Workflow Status Dictionary (Clarification)
- **CANCEL:** Occurs *before* payment is captured. The order is simply discarded. (Stock was never fully deducted).
- **VOID:** Occurs *after* payment is recorded, usually on the same business day before batch settlement. Completely nullifies the transaction as if it never happened. (Stock is returned).
- **REFUND:** Occurs *after* the payment is fully settled (often a different day). Returns money to the customer via a new transaction (Credit). (Stock may or may not be returned depending on physical return of goods).
- **REVERSAL:** The systemic action in the Payment or Finance domain to negate a previous entry.

## 1. Sales & POS Checkout Flow
```text
Customer Orders -> Cashier inputs to POS
-> System calculates Promo/Discount
-> Cashier hits 'Pay' -> Selects Payment Method (majoo Pay QRIS)
-> System generates Dynamic QR
-> Webhook confirms payment
-> System deducts Inventory
-> System records Sales Revenue in Ledger
```
*Business States (Order):* DRAFT -> PENDING_PAYMENT -> PAID -> COMPLETED
*(Source: FACT - Official Majoo Documentation)*

## 2. Void Flow (Pre-Settlement)
```text
Original Sale (PAID, Same Day) -> Cashier initiates Void
-> System prompts for Manager PIN
-> Manager inputs PIN
-> System Voids Order
-> Inventory System returns Stock (Stock = Stock + Voided Qty)
-> Finance System reverses Journal Entry
```
*Business States (Order):* COMPLETED -> VOIDED
*(Source: FACT - Official Majoo Documentation)*

## 3. Refund Flow (Post-Settlement)
```text
Customer requests Refund -> Manager approves via PIN
-> System processes Refund via Payment Gateway API
-> Refund recorded as a separate negative revenue entry.
```
*Business States (Refund):* REQUESTED -> AUTHORIZED -> PROCESSING -> REFUNDED
*(Source: INFERENCE - Standard ERP financial flows)*

## 4. Purchasing & Receiving Flow
```text
Staff creates Purchase Request -> Approved by Manager
-> System generates Purchase Order (PO)
-> PO sent to Supplier -> Supplier delivers Goods
-> Warehouse Staff marks PO as Received (Goods Receipt)
-> System increases Inventory
-> System generates Purchase Invoice (AP)
```
*Business States (PO):* DRAFT -> APPROVED -> SENT -> PARTIALLY_RECEIVED -> FULLY_RECEIVED -> CLOSED
*(Source: FACT - Official Majoo Documentation)*

## 5. Multi-Branch Stock Transfer (Mutasi Stok)
```text
Branch A needs stock -> Requests from Branch B
-> Branch B approves -> Branch B ships items
-> System deducts Branch B Stock (State: IN_TRANSIT)
-> Branch A receives items -> System increases Branch A Stock
```
*Business States (Transfer):* REQUESTED -> APPROVED -> IN_TRANSIT -> RECEIVED
*(Source: INFERENCE - Standard multi-branch logistics behavior)*

## 6. Omnichannel Fulfillment Flow
```text
Tokopedia integration pulls Order -> Majoo Dashboard creates Order
-> System globally deducts Stock
-> Staff packs item -> Courier picks up -> SHIPPED
-> Tokopedia settlement -> Majoo records Revenue
```
*Business States (Online Order):* ONLINE_NEW -> PACKING -> READY_TO_SHIP -> SHIPPED -> DELIVERED
*(Source: ASSUMPTION - Based on typical marketplace aggregator APIs)*
