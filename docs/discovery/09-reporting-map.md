# 09 - Reporting Map

This document outlines the reporting capabilities required based on the product discovery.

## 1. Sales & Revenue Reports
- **Metrics:** Gross Revenue, Net Revenue, Average Basket Size, Total Transactions, COGS, Gross Margin.
- **Dimensions:** By Branch, By Product, By Category, By Employee (Cashier/Server), By Time (Hourly/Daily/Monthly).
- **Filters:** Date range, Branch, Payment Method, Order Type (Dine-in vs Online).
- **Source Data:** Orders, OrderItems, Payments.

## 2. Inventory Reports
- **Metrics:** Current Stock Level, Stock Valuation (Quantity x HPP), Low Stock Alerts, Moving Average Capital Cost.
- **Dimensions:** By Branch, By Warehouse, By Category.
- **Filters:** Date range, Branch.
- **Source Data:** Stock, StockMovement.

## 3. Financial & Accounting Reports
- **Metrics:** Assets, Liabilities, Equity, Net Profit, Operational Expenses.
- **Standard Reports:**
  - Balance Sheet (Neraca)
  - Profit & Loss (Laba Rugi)
  - Cash Flow Statement (Arus Kas)
  - Accounts Payable / Receivable Aging.
- **Filters:** Fiscal period, Branch (if tracking cost centers).
- **Source Data:** JournalEntries, Accounts.

## 4. Employee & HR Reports
- **Metrics:** Attendance Rate, Late Days, Total Commission Earned, Overtime Hours.
- **Dimensions:** By Employee, By Branch.
- **Filters:** Date range, Department.
- **Source Data:** Attendance, Commission, Payroll.

## 5. CRM & Marketing Reports
- **Metrics:** Customer Acquisition Rate, Retention Rate, Points Redeemed, Voucher Utilization Rate.
- **Dimensions:** By Customer Segment, By Campaign.
- **Source Data:** Customers, LoyaltyPoints, Voucher.

## Technical Considerations
- **Reporting Database:** Due to the volume of transactional data, generating P&L and Sales reports on the fly from the primary operational database will cause performance degradation.
- **Requirement:** A reporting infrastructure (e.g., Read Replica, Materialized Views, or a separate Data Warehouse / OLAP cube) is necessary for high-volume tenants.
