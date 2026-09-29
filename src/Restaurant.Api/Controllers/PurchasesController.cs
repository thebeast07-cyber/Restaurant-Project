using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Auth;
using Restaurant.Api.Contracts;
using Restaurant.Domain.Common;
using Restaurant.Domain.Finance;
using Restaurant.Domain.Inventory;
using Restaurant.Domain.Purchasing;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Api.Controllers;

/// <summary>
/// The "Receiving + Invoice" half of the PRD §11 Purchasing Flow, combined into one
/// step for this MVP extension — see Purchase.cs for why. Restricted to Owner/Manager
/// (there's no Warehouse role, same substitution as Stock Adjustment/Day 10).
/// </summary>
[ApiController]
[Route("api/purchases")]
[Authorize(Roles = "Owner,Manager")]
public class PurchasesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantProvider _tenant;

    public PurchasesController(AppDbContext db, ICurrentTenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<List<PurchaseResponse>>> List()
    {
        var purchases = await _db.Purchases
            .Include(p => p.Items)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var response = await Task.WhenAll(purchases.Select(p => BuildResponseAsync(p, journalEntryId: null)));
        return Ok(response.ToList());
    }

    /// <summary>
    /// Records a Purchase: increments Stock per Ingredient (atomic, same
    /// upsert-by-catch + conditional-increment discipline as Stock Adjustment/Day 10 —
    /// no read-then-write on a shared counter, ever), writes one StockMovement per
    /// item (Reason = Purchase), and posts a single balanced JournalEntry
    /// (Debit Inventory Asset, Credit Accounts Payable) for the whole Purchase. All in
    /// one transaction — a failure partway through must not leave stock incremented
    /// without the matching liability recorded, or vice versa.
    ///
    /// PurchaseRequestId is optional: if given, the referenced request must be
    /// Approved (not Pending/Rejected) — but a Purchase can also be recorded with no
    /// PurchaseRequestId at all, for a purchase that never went through the
    /// Request/Approval flow (e.g. an emergency walk-in buy). The approval flow is a
    /// control, not a hard gate on whether stock/AP can move.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PurchaseResponse>> Create(CreatePurchaseRequest request)
    {
        if (request.Items.Count == 0)
        {
            return BadRequest(new { message = "A purchase needs at least one item." });
        }

        var supplierExists = await _db.Suppliers.AnyAsync(s => s.Id == request.SupplierId);
        if (!supplierExists)
        {
            return BadRequest(new { message = "SupplierId not found." });
        }

        if (request.PurchaseRequestId is not null)
        {
            var pr = await _db.PurchaseRequests.SingleOrDefaultAsync(pr => pr.Id == request.PurchaseRequestId);
            if (pr is null)
            {
                return BadRequest(new { message = "PurchaseRequestId not found." });
            }

            if (pr.Status != PurchaseRequestStatus.Approved)
            {
                return BadRequest(new { message = $"Cannot record a Purchase against a request in status {pr.Status}; it must be Approved." });
            }
        }

        var ingredientIds = request.Items.Select(i => i.IngredientId).Distinct().ToList();
        var validIngredientCount = await _db.Ingredients.CountAsync(i => ingredientIds.Contains(i.Id));
        if (validIngredientCount != ingredientIds.Count)
        {
            return BadRequest(new { message = "One or more IngredientId values are invalid." });
        }

        if (request.Items.Any(i => i.Quantity <= 0 || i.UnitCost < 0))
        {
            return BadRequest(new { message = "Item Quantity must be positive and UnitCost cannot be negative." });
        }

        var userId = User.GetUserId();

        var purchase = new Purchase
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            SupplierId = request.SupplierId,
            PurchaseRequestId = request.PurchaseRequestId,
            RecordedByUserId = userId
        };

        foreach (var item in request.Items)
        {
            var subtotal = item.Quantity * item.UnitCost;
            purchase.Items.Add(new PurchaseItem
            {
                TenantId = _tenant.TenantId!.Value,
                PurchaseId = purchase.Id,
                IngredientId = item.IngredientId,
                Quantity = item.Quantity,
                Unit = item.Unit,
                UnitCost = item.UnitCost,
                Subtotal = subtotal
            });
        }
        purchase.RecalculateTotal();

        await using var transaction = await _db.Database.BeginTransactionAsync();

        // Upsert-by-catch for any Ingredient that has no Stock row yet — same pattern
        // as IngredientsController.AdjustStock (Day 10) and ShiftsController.Open.
        var missingStockIngredientIds = ingredientIds.Except(
            await _db.Stocks.Where(s => ingredientIds.Contains(s.IngredientId)).Select(s => s.IngredientId).ToListAsync()
        ).ToList();

        if (missingStockIngredientIds.Count > 0)
        {
            foreach (var ingredientId in missingStockIngredientIds)
            {
                _db.Stocks.Add(new Stock
                {
                    TenantId = _tenant.TenantId!.Value,
                    BranchId = _tenant.BranchId!.Value,
                    IngredientId = ingredientId,
                    Quantity = 0
                });
            }

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _db.ChangeTracker.Clear();
            }
        }

        foreach (var item in purchase.Items)
        {
            // A pure increment needs no compare-and-swap — same reasoning as
            // ManualAdjustment in IngredientsController.AdjustStock.
            await _db.Stocks
                .Where(s => s.IngredientId == item.IngredientId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Quantity, s => s.Quantity + item.Quantity));

            // Weighted-average cost update — MUST run after the stock increment above,
            // in the same transaction: the formula below reads stocks."Quantity" as the
            // POST-increment value and reconstructs the pre-increment quantity as
            // (post-increment - this item's quantity), so it never needs a separate
            // "read quantity before" step that a concurrent Purchase could race with.
            // Both statements touch the same Stock row inside one transaction, so
            // Postgres' row lock from the first UPDATE covers this one too — no
            // interleaving is possible. See implementation-notes.md for why this is
            // Weighted Average rather than FIFO.
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE ingredients AS i
                SET ""AverageCost"" = ((s.""Quantity"" - {item.Quantity}) * i.""AverageCost"" + {item.Quantity} * {item.UnitCost}) / s.""Quantity""
                FROM stocks AS s
                WHERE i.""Id"" = {item.IngredientId} AND s.""IngredientId"" = {item.IngredientId}");

            _db.StockMovements.Add(new StockMovement
            {
                TenantId = _tenant.TenantId!.Value,
                BranchId = _tenant.BranchId!.Value,
                IngredientId = item.IngredientId,
                ChangeQuantity = item.Quantity,
                Reason = StockMovementReason.Purchase,
                ReferenceType = "Purchase",
                ReferenceId = purchase.Id,
                CreatedByUserId = userId
            });
        }

        // Debit Inventory Asset / Credit Accounts Payable — the first real use of the
        // Inventory Asset account seeded since Day 4 (Checkout's journal never used
        // it, since COGS isn't modeled yet; see implementation-notes.md).
        var inventoryAssetAccount = await _db.Accounts.SingleAsync(a => a.Code == "1100");
        var accountsPayableAccount = await _db.Accounts.SingleAsync(a => a.Code == "2000");

        var journalEntry = new JournalEntry
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            ReferenceType = "Purchase",
            ReferenceId = purchase.Id
        };
        journalEntry.Lines.Add(new JournalLine { TenantId = _tenant.TenantId!.Value, JournalEntryId = journalEntry.Id, AccountId = inventoryAssetAccount.Id, Debit = purchase.TotalAmount, Credit = 0 });
        journalEntry.Lines.Add(new JournalLine { TenantId = _tenant.TenantId!.Value, JournalEntryId = journalEntry.Id, AccountId = accountsPayableAccount.Id, Debit = 0, Credit = purchase.TotalAmount });
        journalEntry.AssertBalanced();
        _db.JournalEntries.Add(journalEntry);

        _db.Purchases.Add(purchase);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(await BuildResponseAsync(purchase, journalEntry.Id));
    }

    /// <summary>
    /// Records one installment against a Purchase's Accounts Payable balance
    /// (Debit Accounts Payable, Credit Cash — the mirror image of the original
    /// Purchase's journal lines). Supports partial payment: the atomic conditional
    /// UPDATE below allows any Amount up to the remaining balance, and PaymentStatus
    /// moves Unpaid -> PartiallyPaid -> Paid as AmountPaid climbs toward TotalAmount.
    /// The WHERE clause (AmountPaid + Amount <= TotalAmount) is what prevents
    /// overpaying — checked atomically against the current committed row, not a
    /// stale read, so two concurrent payments against the same Purchase can't
    /// together exceed the total even if both pass a naive "enough remaining?" check
    /// in application code first.
    /// </summary>
    [HttpPost("{id:guid}/payments")]
    public async Task<ActionResult<PurchasePaymentResponse>> RecordPayment(Guid id, RecordPurchasePaymentRequest request)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Payment Amount must be positive." });
        }

        var purchase = await _db.Purchases.SingleOrDefaultAsync(p => p.Id == id);
        if (purchase is null)
        {
            return NotFound();
        }

        var userId = User.GetUserId();
        var amount = request.Amount;

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var claimed = await _db.Purchases
            .Where(p => p.Id == id && p.AmountPaid + amount <= p.TotalAmount)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.AmountPaid, p => p.AmountPaid + amount)
                .SetProperty(p => p.PaymentStatus, p =>
                    p.AmountPaid + amount >= p.TotalAmount ? PurchasePaymentStatus.Paid : PurchasePaymentStatus.PartiallyPaid));

        if (claimed == 0)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { message = "Payment would exceed the remaining balance on this Purchase." });
        }

        _db.PurchasePayments.Add(new PurchasePayment
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            PurchaseId = id,
            Amount = amount,
            PaidByUserId = userId
        });

        var cashAccount = await _db.Accounts.SingleAsync(a => a.Code == "1000");
        var accountsPayableAccount = await _db.Accounts.SingleAsync(a => a.Code == "2000");

        var journalEntry = new JournalEntry
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            ReferenceType = "PurchasePayment",
            ReferenceId = id
        };
        journalEntry.Lines.Add(new JournalLine { TenantId = _tenant.TenantId!.Value, JournalEntryId = journalEntry.Id, AccountId = accountsPayableAccount.Id, Debit = amount, Credit = 0 });
        journalEntry.Lines.Add(new JournalLine { TenantId = _tenant.TenantId!.Value, JournalEntryId = journalEntry.Id, AccountId = cashAccount.Id, Debit = 0, Credit = amount });
        journalEntry.AssertBalanced();
        _db.JournalEntries.Add(journalEntry);

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        var updated = await _db.Purchases.AsNoTracking().SingleAsync(p => p.Id == id);
        return Ok(new PurchasePaymentResponse(id, updated.AmountPaid, updated.TotalAmount - updated.AmountPaid, updated.PaymentStatus, journalEntry.Id));
    }

    private async Task<PurchaseResponse> BuildResponseAsync(Purchase purchase, Guid? journalEntryId)
    {
        var supplierName = await _db.Suppliers.Where(s => s.Id == purchase.SupplierId).Select(s => s.Name).SingleAsync();

        var ingredientIds = purchase.Items.Select(i => i.IngredientId).ToList();
        var ingredientNames = await _db.Ingredients
            .Where(i => ingredientIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Name);

        var items = purchase.Items
            .Select(i => new PurchaseItemResponse(
                i.IngredientId, ingredientNames.GetValueOrDefault(i.IngredientId, "?"), i.Quantity, i.Unit, i.UnitCost, i.Subtotal))
            .ToList();

        var resolvedJournalEntryId = journalEntryId ?? await _db.JournalEntries
            .Where(j => j.ReferenceType == "Purchase" && j.ReferenceId == purchase.Id)
            .Select(j => j.Id)
            .SingleAsync();

        return new PurchaseResponse(
            purchase.Id, purchase.SupplierId, supplierName, purchase.PurchaseRequestId,
            purchase.TotalAmount, purchase.AmountPaid, purchase.TotalAmount - purchase.AmountPaid, purchase.PaymentStatus,
            resolvedJournalEntryId, items);
    }
}
