# 02 - Comprehensive Publicly Verifiable Capability Inventory

This document serves as the exhaustive inventory of Majoo's capabilities, classified by domain.

## Identity & Access
- **Role-Based Access Control (RBAC):** Assign permissions based on default or custom roles. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **PIN Authorization:** Requires Manager/Admin PIN for overrides (Voids, Refunds, Compliments). (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **Screen Lock:** Prevents unauthorized access when POS is unattended. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **Sensitive Data Masking:** Hiding COGS/Gross Profit from operational staff on Prime tiers. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*

## Organization (Multi-Branch)
- **Multi-Outlet Management:** Centralized dashboard for multiple physical/online branches. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **Outlet Groups:** Logical grouping for reporting and catalog mapping. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **Outlet-Specific Configuration:** Custom operating hours, "Store Close" toggle, custom logos. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*

## Product / Catalog
- **Omnichannel Catalog:** Centralized product database synced across physical and online channels. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **Product Variants:** Size, Color, Model with specific pricing and SKUs. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **Recipe Variants (F&B):** Link specific raw material deductions to specific variants. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*

## POS (Point of Sale) & Sales
- **Transaction Management:** Cart creation, Hold Order (Draft), Void, Refund. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **F&B specific:** Kitchen Display System (KDS), Table layout view. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **Complimentary Items:** "Gratisan" logging requiring authorization. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*

## Payment
- **Integrated Payment Gateway (majoo Pay):** Native QRIS, Card, and E-wallet aggregation. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **Dynamic QR on Bill:** QR code auto-generated on screen/receipt based on exact amount. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **Auto-Settlement & Reconciliation:** Automatic webhook verification and ledger updates. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **Third-Party Integrations:** GoPay, OVO, ShopeePay, GPN, Visa, Mastercard via EDC. (Current, Third-Party) | *Source: OFFICIAL | Confidence: HIGH*

## Inventory
- **Real-Time Stock Movement:** Auto-deduction upon POS transaction. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **Multi-Warehouse / Multi-Branch:** Stock mutations and consolidation across locations. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **Stock Opname:** Manual reconciliation of physical stock vs system. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **Batch / Expiry / Serial Tracking:** Advanced tracking for perishables/electronics. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **Production Stock:** Converting raw materials to finished goods. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*

## Purchasing
- **Purchase Orders (PO):** Bidding, negotiation, and digital PO generation. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **Receiving & Invoicing:** Converting POs to Purchase Invoices, updating Accounts Payable. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*

## CRM & Promotion
- **Customer Database:** History, visits, segmentation. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **Loyalty Points & Membership:** Tiered memberships, point earning and redemption. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **Promo Engine:** Buy 1 Get 1, Time-based vouchers, general discounts. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **Marketing Campaigns:** SMS/WhatsApp outreach. (Current, Third-party integration) | *Source: OFFICIAL | Confidence: HIGH*

## Finance & Accounting
- **Native Financial Ledger:** General Ledger, Balance Sheet, P&L, Cash Flow, AP/AR. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **Automated COGS:** Dynamic cost calculations based on inventory. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **Third-Party Accounting:** Sync to Jurnal/Xero. (Current, Third-Party) | *Source: OFFICIAL | Confidence: MEDIUM*

## Human Resources
- **Online Attendance:** Clock-in/out with selfie verification. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **Shift & Schedule Management:** Visible via the employee app. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **Automated Payroll & E-Payslips:** Basic salary, allowances, digital disbursement. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **Multi-Commission:** Splitting transaction commissions. (Current, Native) | *Source: OFFICIAL | Confidence: HIGH*
- **Kasbon (Early Salary):** Employee cash advance. (Current, Native/Embedded Finance) | *Source: OFFICIAL | Confidence: HIGH*

## Omnichannel
- **Marketplace Sync:** Centralized stock and order pulling from Tokopedia, Shopee. (Current, Third-party integration) | *Source: OFFICIAL | Confidence: HIGH*
- **Food Delivery Sync:** Direct POS integration with GrabFood, GoFood. (Current, Third-party integration) | *Source: OFFICIAL | Confidence: HIGH*
