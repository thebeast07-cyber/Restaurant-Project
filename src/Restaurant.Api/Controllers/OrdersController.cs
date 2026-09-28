using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Auth;
using Restaurant.Api.Contracts;
using Restaurant.Domain.Common;
using Restaurant.Domain.Finance;
using Restaurant.Domain.Inventory;
using Restaurant.Domain.Sales;
using Restaurant.Infrastructure.Persistence;
using DomainPayment = Restaurant.Domain.Payment;

namespace Restaurant.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantProvider _tenant;

    public OrdersController(AppDbContext db, ICurrentTenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(CreateOrderRequest request)
    {
        var userId = User.GetUserId();
        var shift = await _db.Shifts.SingleOrDefaultAsync(s => s.UserId == userId && s.Status == ShiftStatus.Open);
        if (shift is null)
        {
            return BadRequest(new { message = "Open a shift before creating an order." });
        }

        if (request.TableId is not null)
        {
            var tableExists = await _db.Tables.AnyAsync(t => t.Id == request.TableId);
            if (!tableExists)
            {
                return BadRequest(new { message = "TableId not found." });
            }
        }

        var order = new Order
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            TableId = request.TableId,
            ShiftId = shift.Id
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        return Ok(await BuildOrderResponse(order.Id));
    }

    [HttpGet("{orderId:guid}")]
    public async Task<ActionResult<OrderResponse>> Get(Guid orderId)
    {
        var response = await BuildOrderResponse(orderId);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("{orderId:guid}/items")]
    public async Task<ActionResult<OrderResponse>> AddItem(Guid orderId, AddOrderItemRequest request)
    {
        if (request.Quantity <= 0)
        {
            return BadRequest(new { message = "Quantity must be greater than zero." });
        }

        var order = await _db.Orders.Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == orderId);
        if (order is null)
        {
            return NotFound();
        }

        if (order.Status is not (OrderStatus.Draft or OrderStatus.Open))
        {
            return BadRequest(new { message = $"Cannot add items to an order in status {order.Status}." });
        }

        var product = await _db.Products.SingleOrDefaultAsync(p => p.Id == request.ProductId && p.IsActive);
        if (product is null)
        {
            return BadRequest(new { message = "ProductId not found or inactive." });
        }

        var item = new OrderItem
        {
            TenantId = _tenant.TenantId!.Value,
            OrderId = order.Id,
            ProductId = product.Id,
            Quantity = request.Quantity,
            UnitPrice = product.Price,
            Subtotal = product.Price * request.Quantity,
            Station = product.Station
        };

        // EF Core relationship fixup automatically appends `item` into order.Items
        // once it's tracked (its OrderId matches the already-tracked Order) — adding
        // it to both places here would double-count it in RecalculateTotal below.
        _db.OrderItems.Add(item);
        order.RecalculateTotal();
        await _db.SaveChangesAsync();

        return Ok(await BuildOrderResponse(order.Id));
    }

    /// <summary>
    /// The OrderPaid event, implemented as a single in-process transaction rather than
    /// a message/broker (see docs/architecture/01-mvp-technical-design.md §2 — no
    /// outbox/broker needed at this scale, but the "who consumes what" contract there
    /// still applies): deduct Inventory via Recipe, post a balanced Finance journal,
    /// and complete the Order — all in one SaveChangesAsync so it's atomic. Either all
    /// three happen or none do; there is no state where payment is recorded but stock
    /// or the journal silently didn't move.
    /// </summary>
    [HttpPost("{orderId:guid}/checkout")]
    public async Task<ActionResult<CheckoutResponse>> Checkout(Guid orderId, CheckoutRequest request)
    {
        var order = await _db.Orders.Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == orderId);
        if (order is null)
        {
            return NotFound();
        }

        if (order.Status is not (OrderStatus.Draft or OrderStatus.Open))
        {
            return BadRequest(new { message = $"Cannot checkout an order in status {order.Status}." });
        }

        if (order.Items.Count == 0)
        {
            return BadRequest(new { message = "Cannot checkout an order with no items." });
        }

        var paymentMethod = await _db.PaymentMethods.SingleOrDefaultAsync(pm => pm.Code == request.PaymentMethod);
        if (paymentMethod is null)
        {
            return BadRequest(new { message = $"Payment method {request.PaymentMethod} is not configured." });
        }

        // --- 1. Compute Ingredient requirements from Recipe, aggregated across items ---
        var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
        var recipeItems = await _db.RecipeItems.Where(r => productIds.Contains(r.ProductId)).ToListAsync();
        var quantityByProduct = order.Items.ToDictionary(i => i.ProductId, i => i.Quantity);

        var requiredByIngredient = new Dictionary<Guid, decimal>();
        foreach (var recipeItem in recipeItems)
        {
            var orderedQuantity = quantityByProduct[recipeItem.ProductId];
            var required = recipeItem.Quantity * orderedQuantity;
            requiredByIngredient[recipeItem.IngredientId] =
                requiredByIngredient.GetValueOrDefault(recipeItem.IngredientId) + required;
        }

        // --- 2 & 3. Deduct stock atomically, validating sufficiency in the same step ---
        // Deliberately NOT "read Quantity, subtract in memory, write it back" — under
        // concurrent checkouts racing for the same stock, that pattern loses updates
        // silently (verified with a stress test: 20 concurrent checkouts against 5
        // units of stock all "succeeded"). Each UPDATE below is its own atomic,
        // conditional statement — Postgres row-locks the matched row for the duration,
        // so a second concurrent request re-evaluates `Quantity >= required` against
        // the value this one just wrote, never the stale value it originally read.
        var userId = User.GetUserId();
        await using var transaction = await _db.Database.BeginTransactionAsync();

        var shortages = new List<object>();
        var deducted = new List<(Guid IngredientId, decimal Quantity)>();

        foreach (var (ingredientId, requiredQuantity) in requiredByIngredient)
        {
            var rowsAffected = await _db.Stocks
                .Where(s => s.IngredientId == ingredientId && s.Quantity >= requiredQuantity)
                .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Quantity, s => s.Quantity - requiredQuantity));

            if (rowsAffected == 0)
            {
                var available = await _db.Stocks
                    .Where(s => s.IngredientId == ingredientId)
                    .Select(s => (decimal?)s.Quantity)
                    .SingleOrDefaultAsync();
                shortages.Add(new { IngredientId = ingredientId, Required = requiredQuantity, Available = available ?? 0m });
                continue;
            }

            deducted.Add((ingredientId, requiredQuantity));
        }

        if (shortages.Count > 0)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { message = "Insufficient stock.", shortages });
        }

        foreach (var (ingredientId, requiredQuantity) in deducted)
        {
            _db.StockMovements.Add(new StockMovement
            {
                TenantId = _tenant.TenantId!.Value,
                BranchId = _tenant.BranchId!.Value,
                IngredientId = ingredientId,
                ChangeQuantity = -requiredQuantity,
                Reason = StockMovementReason.Sale,
                ReferenceType = "Order",
                ReferenceId = order.Id,
                CreatedByUserId = userId
            });
        }

        // --- 4. Record payment ---
        var payment = new DomainPayment.Payment
        {
            TenantId = _tenant.TenantId!.Value,
            OrderId = order.Id,
            PaymentMethodId = paymentMethod.Id,
            Amount = order.TotalAmount,
            Status = DomainPayment.PaymentStatus.Confirmed,
            ConfirmedByUserId = userId,
            ConfirmedAt = DateTimeOffset.UtcNow
        };
        _db.Payments.Add(payment);

        // --- 5. Post balanced journal entry (Debit Cash/Bank, Credit Revenue) ---
        // NOTE: no COGS/InventoryAsset lines yet — that requires a per-Ingredient unit
        // cost, which isn't modeled in the MVP (see implementation-notes.md). This is a
        // known, deliberate gap, not an oversight.
        // Both Cash and (manual/static) QRIS settle to the same "Cash" account for the
        // MVP — there's no separate bank/e-wallet clearing account yet since neither
        // payment method goes through a real gateway. Revisit once QRIS has a real
        // provider integration (fast-follow, see technical design doc §"Ditunda").
        var cashAccount = await _db.Accounts.SingleAsync(a => a.Code == "1000");
        var revenueAccount = await _db.Accounts.SingleAsync(a => a.Code == "4000");

        var journalEntry = new JournalEntry
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            ReferenceType = "Order",
            ReferenceId = order.Id
        };
        journalEntry.Lines.Add(new JournalLine { TenantId = _tenant.TenantId!.Value, JournalEntryId = journalEntry.Id, AccountId = cashAccount.Id, Debit = order.TotalAmount, Credit = 0 });
        journalEntry.Lines.Add(new JournalLine { TenantId = _tenant.TenantId!.Value, JournalEntryId = journalEntry.Id, AccountId = revenueAccount.Id, Debit = 0, Credit = order.TotalAmount });
        journalEntry.AssertBalanced();
        _db.JournalEntries.Add(journalEntry);

        // --- 6. Complete the order ---
        order.Status = OrderStatus.Completed;

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        var orderResponse = await BuildOrderResponse(order.Id);
        return Ok(new CheckoutResponse(orderResponse!, payment.Id, payment.Amount, journalEntry.Id));
    }

    private async Task<OrderResponse?> BuildOrderResponse(Guid orderId)
    {
        var order = await _db.Orders.Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == orderId);
        if (order is null)
        {
            return null;
        }

        var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
        var productNames = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name);

        return new OrderResponse(
            order.Id,
            order.TableId,
            order.ShiftId,
            order.Status,
            order.TotalAmount,
            order.Items.Select(i => new OrderItemResponse(
                i.Id, i.ProductId, productNames.GetValueOrDefault(i.ProductId, "?"), i.Quantity, i.UnitPrice, i.Subtotal, i.Station))
                .ToList());
    }
}
