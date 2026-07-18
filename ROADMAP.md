# HostelPro Roadmap

This roadmap tracks the requested feature set and current implementation status.

## 1. Advanced Billing & Payment Features

- **Implemented:** Generate monthly bills with invoice numbers, email invoice actions, partial payment tracking, overdue status, and late-fee calculation.
- **Implemented:** Expense tracker with monthly expense and net-profit analytics.
- **Manual payments:** Verified UPI, cash, and bank-transfer records are supported. Test mode does not alter invoice balances or revenue. Live gateway checkout remains a future integration requiring provider credentials and signed webhook verification.

## 2. Tenant Management Enhancements

- **Implemented:** Digital onboarding/KYC uploads for ID proofs, agreements, and signatures with admin review.
- **Implemented:** Tenant portal for billing history, receipts, KYC upload, maintenance requests, and mess preferences.

## 3. Operational & Maintenance Tools

- **Implemented:** Maintenance ticketing with priority, assignment, status, and completion tracking.
- **Implemented:** Food/mess management with menus and tenant opt-in/opt-out preferences.

## 4. Smart Dashboard Analytics

- **Implemented:** Expected revenue, collected revenue, monthly expenses, and net profit dashboard metrics.
- **Implemented:** Defaulters list for overdue/pending bills past due date.

## Implementation Priority

1. Add a background scheduler for automatic monthly invoice emails on the configured billing day.
2. Add Razorpay/Paytm server-side checkout creation and signed webhook verification.
3. Add tenant receipt PDF download from the portal.
4. Add reminder email/WhatsApp workflows for the dashboard defaulters list.
