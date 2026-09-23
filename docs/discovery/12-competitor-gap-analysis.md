# 12 - Competitor Gap Analysis

This document distinguishes Majoo-specific functionality from common industry-standard ERP capabilities, identifying gaps and unique selling points.

## 1. Industry-Standard Capabilities
These features are expected in almost any modern ERP/POS system. We must build these as the foundation.
- **POS Checkout & Receipt Printing:** Standard.
- **Master Data Management:** Product, Category, Variant. Standard.
- **Basic Inventory Tracking:** Stock deduction on sale. Standard.
- **Purchasing (PO to Receipt):** Standard.
- **Reporting:** Basic Sales and Inventory reports. Standard.

## 2. Majoo-Specific / Differentiating Features
These features set Majoo apart in the Indonesian SME market and are heavily tailored to local workflows.
- **Kasbon (Early Salary):** Providing short-term loans/salary advances to employees directly through the HR module. *(Differentiator / Embedded Finance)*
- **SATUSEHAT Integration:** Specific integration for healthcare clinics/pharmacies to sync with the Indonesian Ministry of Health. *(Niche Vertical Integration)*
- **Multi-Commission Splitting:** Ability to split a single transaction's commission among multiple staff members (common in Salons/Barbershops). *(Differentiator)*
- **Recipe Variants:** Advanced F&B feature linking raw material usage dynamically to variant sizes (e.g., deducting different milk volumes for Medium vs Large lattes). *(Advanced Feature)*
- **Dynamic QR on Bill:** Showing a dynamic QRIS code on a physical printout/tablet screen without the cashier entering the nominal amount on an EDC. *(Local Standard, high priority in Indonesia)*

## 3. Inferred Requirements (Our Gap)
To build a system "selengkap mungkin dibandingkan Majoo", we must ensure our architectural foundation can support:
- **Marketplace Aggregation:** The ability to pull orders from Tokopedia/Shopee requires a highly robust background job processing engine (like Hangfire/Quartz) to poll APIs or listen to webhooks reliably.
- **Financial Ledger Double-Entry:** Many simple POS systems fake accounting by just summing up orders. Majoo actually executes double-entry journaling. We must build a true ledger, not just a sales tally.
