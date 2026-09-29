# Phase 6 — Gap audit, security, deployment & acceptance

## Gap audit (requirement → status)

| Requirement | Status | Location |
|-------------|--------|----------|
| Audit price / stock / void / payments | Done | `AuditService`, `ProductService` PriceChange, `InventoryService`, `SalesService.VoidInvoiceAsync`, `PaymentAllocationService` |
| Authorized invoice correction (not silent delete) | Done | Owner/Manager void with reason + stock/ledger/cash reverse |
| Cash ≠ credit sales | Done | Phase 4–5 daily sales + cash book |
| Linked cash movements (no duplicate keying) | Done | `CashBankService.PostLinkedAsync` |
| Import costs not double-counted as opex | Done | `ExpenseCategory.IsDirectImportCost` + acknowledgement |
| Role policies on controllers | Done | `AppPolicies` + controller attributes; manual allocation Owner/Manager only |
| Idempotent sale / finalize | Done | `Invoice.IdempotencyKey` unique; finalize blocked if `IsFinalized`/batches exist |
| Upload hardening | Done | `FileStorageService` path containment + extension allowlist |
| Exception handling / logging | Done | Production `UseExceptionHandler`; request logging middleware |
| User management | Done | `UsersController` (Owner) |
| Audit UI | Done | `AuditLogsController` |
| Automated tests | Done | `UD_Autos.Tests` |
| Production secrets | Done | `appsettings.Production.json` disables seed; UserSecretsId on project |

## Authorization matrix (server-side)

| Area | Policy / roles |
|------|----------------|
| Products | ManageProducts (Owner/Manager/Salesman) |
| Purchases / Shipments / Inventory | Owner/Manager |
| Sales / Returns | ManageSales (incl. Salesman); **Void** Owner/Manager only |
| Customers / Khata | ManageCustomers |
| Payments / CashBank | ManagePayments; **manual allocation** Owner/Manager |
| Expenses | ManageExpenses (Owner/Accountant) |
| Reports | ViewOperationalReports; profit/cost Owner/Manager/Accountant |
| Settings / Users / Audit | Owner (`ManageSettings` / `ManageUsers`) |

## Tests

```bash
dotnet test UD_Autos.Tests/UD_Autos.Tests.csproj
```

Coverage: landed-cost allocation rounding, aging buckets, FIFO payment math, double-finalization prevention.

## Migration

```bash
dotnet ef database update --project UD_Autos --startup-project UD_Autos
```

Migration: `Phase6_HardeningIdempotencyIndexes` (IdempotencyKey, void fields, report indexes).

## Deployment checklist

1. Set `ASPNETCORE_ENVIRONMENT=Production`.
2. Store connection string via env `ConnectionStrings__DefaultConnection` or Azure Key Vault / user secrets — never commit production passwords.
3. Keep `SeedOwner:Enabled=false` in production after first Owner exists.
4. Run `dotnet ef database update`.
5. Enable HTTPS + HSTS (already on for non-Development).
6. Backup SQL Server with native jobs / managed backups — do not rely on in-app SQL dump.
7. Export operational data via Reports → Export CSV.
8. Smoke-test login, sale, payment, finalize, expense, dashboard KPIs.

## Reconciliation checklist (owner)

- [ ] Sale → invoice + stock down + ledger debit + cash (if paid) + profit COGS match batch history
- [ ] Finalize → batches + stock up + landed history preserved; second finalize blocked
- [ ] Payment → cash in + balance down + FIFO/manual alloc + aging
- [ ] Return → original invoice link + stock/khata/cash path
- [ ] Expense on import-flagged category requires acknowledgement
- [ ] Dashboard sales ≠ collections
- [ ] Old Khata before date + 30/60/90/120 aging

## Assumptions

- Hawala local PKR uses `PaymentAccount` (default Cash) for cash book.
- Invoice void blocked when returns exist (use return workflow first).
- Production seed disabled by default; create users via Users screen.
