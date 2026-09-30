using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Auth;
using Restaurant.Api.Contracts;
using Restaurant.Domain.Common;
using Restaurant.Domain.Finance;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Api.Controllers;

/// <summary>
/// Operating Expense — the piece that turns a P&L's Gross Profit into a real Net
/// Profit. Restricted to Owner/Manager, matching every other financial write
/// endpoint (Purchases, Purchase Payments).
/// </summary>
[ApiController]
[Route("api/expenses")]
[Authorize(Roles = "Owner,Manager")]
public class ExpensesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantProvider _tenant;

    public ExpensesController(AppDbContext db, ICurrentTenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<List<OperatingExpenseResponse>>> List()
    {
        var expenses = await _db.OperatingExpenses.OrderByDescending(e => e.IncurredAt).ToListAsync();

        var response = new List<OperatingExpenseResponse>();
        foreach (var expense in expenses)
        {
            response.Add(await BuildResponseAsync(expense, journalEntryId: null));
        }
        return Ok(response);
    }

    /// <summary>
    /// Posts Debit OperatingExpense(6000) / Credit AccountsPayable(2000) — accrual
    /// basis: the expense counts against the period it was IncurredAt, not whenever
    /// it eventually gets paid. See OperatingExpense.cs for why this isn't a
    /// cash-only shortcut.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<OperatingExpenseResponse>> Create(CreateOperatingExpenseRequest request)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount must be positive." });
        }

        var userId = User.GetUserId();

        var expense = new OperatingExpense
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            Category = request.Category,
            Description = request.Description,
            Amount = request.Amount,
            IncurredAt = request.IncurredAt,
            RecordedByUserId = userId
        };

        var operatingExpenseAccount = await _db.Accounts.SingleAsync(a => a.Code == "6000");
        var accountsPayableAccount = await _db.Accounts.SingleAsync(a => a.Code == "2000");

        var journalEntry = new JournalEntry
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            ReferenceType = "OperatingExpense",
            ReferenceId = expense.Id
        };
        journalEntry.Lines.Add(new JournalLine { TenantId = _tenant.TenantId!.Value, JournalEntryId = journalEntry.Id, AccountId = operatingExpenseAccount.Id, Debit = request.Amount, Credit = 0 });
        journalEntry.Lines.Add(new JournalLine { TenantId = _tenant.TenantId!.Value, JournalEntryId = journalEntry.Id, AccountId = accountsPayableAccount.Id, Debit = 0, Credit = request.Amount });
        journalEntry.AssertBalanced();

        _db.OperatingExpenses.Add(expense);
        _db.JournalEntries.Add(journalEntry);
        await _db.SaveChangesAsync();

        return Ok(await BuildResponseAsync(expense, journalEntry.Id));
    }

    /// <summary>
    /// Records one installment against an OperatingExpense's Accounts Payable
    /// balance — mirrors PurchasesController.RecordPayment exactly, including the
    /// atomic conditional UPDATE that prevents overpaying under concurrent payments.
    /// </summary>
    [HttpPost("{id:guid}/payments")]
    public async Task<ActionResult<ExpensePaymentResponse>> RecordPayment(Guid id, RecordExpensePaymentRequest request)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Payment Amount must be positive." });
        }

        var expense = await _db.OperatingExpenses.SingleOrDefaultAsync(e => e.Id == id);
        if (expense is null)
        {
            return NotFound();
        }

        var userId = User.GetUserId();
        var amount = request.Amount;

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var claimed = await _db.OperatingExpenses
            .Where(e => e.Id == id && e.AmountPaid + amount <= e.Amount)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.AmountPaid, e => e.AmountPaid + amount)
                .SetProperty(e => e.PaymentStatus, e =>
                    e.AmountPaid + amount >= e.Amount ? ExpensePaymentStatus.Paid : ExpensePaymentStatus.PartiallyPaid));

        if (claimed == 0)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { message = "Payment would exceed the remaining balance on this Expense." });
        }

        _db.OperatingExpensePayments.Add(new OperatingExpensePayment
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            OperatingExpenseId = id,
            Amount = amount,
            PaidByUserId = userId
        });

        var cashAccount = await _db.Accounts.SingleAsync(a => a.Code == "1000");
        var accountsPayableAccount = await _db.Accounts.SingleAsync(a => a.Code == "2000");

        var journalEntry = new JournalEntry
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            ReferenceType = "OperatingExpensePayment",
            ReferenceId = id
        };
        journalEntry.Lines.Add(new JournalLine { TenantId = _tenant.TenantId!.Value, JournalEntryId = journalEntry.Id, AccountId = accountsPayableAccount.Id, Debit = amount, Credit = 0 });
        journalEntry.Lines.Add(new JournalLine { TenantId = _tenant.TenantId!.Value, JournalEntryId = journalEntry.Id, AccountId = cashAccount.Id, Debit = 0, Credit = amount });
        journalEntry.AssertBalanced();
        _db.JournalEntries.Add(journalEntry);

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        var updated = await _db.OperatingExpenses.AsNoTracking().SingleAsync(e => e.Id == id);
        return Ok(new ExpensePaymentResponse(id, updated.AmountPaid, updated.Amount - updated.AmountPaid, updated.PaymentStatus, journalEntry.Id));
    }

    private async Task<OperatingExpenseResponse> BuildResponseAsync(OperatingExpense expense, Guid? journalEntryId)
    {
        var resolvedJournalEntryId = journalEntryId ?? await _db.JournalEntries
            .Where(j => j.ReferenceType == "OperatingExpense" && j.ReferenceId == expense.Id)
            .Select(j => j.Id)
            .SingleAsync();

        return new OperatingExpenseResponse(
            expense.Id, expense.Category, expense.Description, expense.Amount, expense.IncurredAt,
            expense.AmountPaid, expense.Amount - expense.AmountPaid, expense.PaymentStatus, resolvedJournalEntryId);
    }
}
