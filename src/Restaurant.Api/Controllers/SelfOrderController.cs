using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Contracts;
using Restaurant.Domain.CRM;
using Restaurant.Domain.Sales;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Api.Controllers;

/// <summary>
/// Public, unauthenticated endpoints for the QR self-order flow (docs/architecture/
/// 02-ui-roadmap.md item 10) — a customer at a table has no login, so this
/// deliberately does not use [Authorize] anywhere (there is no global fallback auth
/// policy in Program.cs, so omitting it is enough; no [AllowAnonymous] needed).
///
/// Because there's no JWT, ICurrentTenantProvider resolves to null here and the
/// ITenantScoped query filter is bypassed entirely (see HttpCurrentTenantProvider) —
/// every query in this controller explicitly filters by the TenantId/BranchId read
/// off the Table row instead of trusting ambient tenant context, so this stays
/// correct even once a 2nd Tenant exists (see "Known Standing Risks").
///
/// Reuses the exact same Table-claiming and Order/Payment mechanics
/// OrdersController already has — this is a thin public front door onto that
/// machinery, not a parallel ordering system.
/// </summary>
[ApiController]
[Route("api/self-order")]
public class SelfOrderController : ControllerBase
{
    private readonly AppDbContext _db;

    public SelfOrderController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("tables/{tableId:guid}")]
    public async Task<ActionResult<SelfOrderTableStateResponse>> GetTableState(Guid tableId)
    {
        var table = await _db.Tables.SingleOrDefaultAsync(t => t.Id == tableId);
        if (table is null)
        {
            return NotFound();
        }

        var isOpen = await _db.Shifts.AnyAsync(s =>
            s.TenantId == table.TenantId && s.BranchId == table.BranchId && s.Status == ShiftStatus.Open);

        OrderResponse? activeOrder = null;
        if (table.Status == TableStatus.Occupied)
        {
            var order = await _db.Orders.SingleOrDefaultAsync(o =>
                o.TenantId == table.TenantId && o.TableId == tableId &&
                (o.Status == OrderStatus.Draft || o.Status == OrderStatus.Open));
            if (order is not null)
            {
                activeOrder = await BuildOrderResponse(order.Id);
            }
        }

        var menuProducts = await _db.Products
            .Where(p => p.TenantId == table.TenantId && p.BranchId == table.BranchId && p.IsActive)
            .Include(p => p.Variants)
            .ToListAsync();
        var categoryNames = await _db.Categories
            .Where(c => menuProducts.Select(p => p.CategoryId).Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name);

        var menu = menuProducts
            .Select(p => new SelfOrderMenuItem(
                p.Id, p.Name, categoryNames.GetValueOrDefault(p.CategoryId, "?"), p.Price, p.ImageUrl,
                p.Variants.Where(v => v.IsActive).Select(v => new SelfOrderMenuVariant(v.Id, v.Name, v.Price)).ToList()))
            .ToList();

        return Ok(new SelfOrderTableStateResponse(table.Number, isOpen, activeOrder, menu));
    }

    /// <summary>
    /// Upserts the Customer by phone (dedupe key, see Customer.cs), then either claims
    /// the Table (Available -> Occupied, same atomic pattern as
    /// OrdersController.Create) and starts a new Order, or — if the Table is already
    /// Occupied, meaning someone at this table already started a tab — attaches this
    /// Customer to that existing Order only if it doesn't have one yet (first scan
    /// wins; see Order.CustomerId).
    /// </summary>
    [HttpPost("tables/{tableId:guid}/start")]
    public async Task<ActionResult<OrderResponse>> Start(Guid tableId, StartSelfOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Phone))
        {
            return BadRequest(new { message = "Name and Phone are required." });
        }

        var table = await _db.Tables.SingleOrDefaultAsync(t => t.Id == tableId);
        if (table is null)
        {
            return NotFound();
        }

        var openShift = await _db.Shifts.FirstOrDefaultAsync(s =>
            s.TenantId == table.TenantId && s.BranchId == table.BranchId && s.Status == ShiftStatus.Open);
        if (openShift is null)
        {
            return BadRequest(new { message = "Resto belum buka — silakan panggil staff." });
        }

        // Upsert-by-catch, same pattern as Stock rows elsewhere in this codebase: two
        // self-orders racing on a brand-new phone number both try to create the
        // Customer, the unique index on (TenantId, BranchId, Phone) lets one through.
        var customer = await _db.Customers.SingleOrDefaultAsync(c =>
            c.TenantId == table.TenantId && c.BranchId == table.BranchId && c.Phone == request.Phone);
        if (customer is null)
        {
            customer = new Customer
            {
                TenantId = table.TenantId,
                BranchId = table.BranchId,
                Name = request.Name,
                Phone = request.Phone
            };
            _db.Customers.Add(customer);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _db.ChangeTracker.Clear();
                customer = await _db.Customers.SingleAsync(c =>
                    c.TenantId == table.TenantId && c.BranchId == table.BranchId && c.Phone == request.Phone);
            }
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var claimed = await _db.Tables
            .Where(t => t.Id == tableId && t.Status == TableStatus.Available)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.Status, TableStatus.Occupied));

        Order order;
        if (claimed == 1)
        {
            order = new Order
            {
                TenantId = table.TenantId,
                BranchId = table.BranchId,
                TableId = table.Id,
                ShiftId = openShift.Id,
                CustomerId = customer.Id
            };
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();
        }
        else
        {
            // Table was already Occupied (by someone else at this table, or a race we
            // lost) — find that Order and attach the Customer if it's still unset.
            var existing = await _db.Orders.SingleOrDefaultAsync(o =>
                o.TenantId == table.TenantId && o.TableId == tableId &&
                (o.Status == OrderStatus.Draft || o.Status == OrderStatus.Open));

            if (existing is null)
            {
                await transaction.RollbackAsync();
                return Conflict(new { message = "Meja berstatus terisi tapi tidak ada pesanan aktif — panggil staff." });
            }

            if (existing.CustomerId is null)
            {
                existing.CustomerId = customer.Id;
                await _db.SaveChangesAsync();
            }

            order = existing;
        }

        await transaction.CommitAsync();
        return Ok(await BuildOrderResponse(order.Id));
    }

    /// <summary>
    /// Adds an item to a self-order's Order — same validation as
    /// OrdersController.AddItem. The first item added while still Draft also flips
    /// the Order to Open (the same transition OrdersController.SendToStation does),
    /// so a self-ordered ticket reaches the kitchen through the identical path a
    /// staff-entered one does, without requiring a staff member to press "send to
    /// station" on the customer's behalf.
    /// </summary>
    [HttpPost("orders/{orderId:guid}/items")]
    public async Task<ActionResult<OrderResponse>> AddItem(Guid orderId, AddOrderItemRequest request)
    {
        if (request.Quantity <= 0)
        {
            return BadRequest(new { message = "Quantity must be greater than zero." });
        }

        var order = await _db.Orders.SingleOrDefaultAsync(o => o.Id == orderId);
        if (order is null)
        {
            return NotFound();
        }

        if (order.Status is not (OrderStatus.Draft or OrderStatus.Open))
        {
            return BadRequest(new { message = $"Cannot add items to an order in status {order.Status}." });
        }

        var product = await _db.Products.Include(p => p.Variants).SingleOrDefaultAsync(p =>
            p.Id == request.ProductId && p.TenantId == order.TenantId && p.IsActive);
        if (product is null)
        {
            return BadRequest(new { message = "ProductId not found or inactive." });
        }

        var activeVariants = product.Variants.Where(v => v.IsActive).ToList();
        decimal unitPrice;
        Guid? variantId = null;

        if (activeVariants.Count > 0)
        {
            var variant = activeVariants.SingleOrDefault(v => v.Id == request.ProductVariantId);
            if (variant is null)
            {
                return BadRequest(new { message = "ProductVariantId is required and must be an active variant of this product." });
            }

            unitPrice = variant.Price;
            variantId = variant.Id;
        }
        else
        {
            if (request.ProductVariantId is not null)
            {
                return BadRequest(new { message = "This product has no variants." });
            }

            unitPrice = product.Price;
        }

        _db.OrderItems.Add(new OrderItem
        {
            TenantId = order.TenantId,
            OrderId = order.Id,
            ProductId = product.Id,
            ProductVariantId = variantId,
            Quantity = request.Quantity,
            UnitPrice = unitPrice,
            Subtotal = unitPrice * request.Quantity,
            Station = product.Station
        });

        var newTotal = await _db.OrderItems.Where(i => i.OrderId == order.Id).SumAsync(i => i.Subtotal)
            + unitPrice * request.Quantity;
        order.TotalAmount = newTotal;

        if (order.Status == OrderStatus.Draft)
        {
            order.Status = OrderStatus.Open;
        }

        await _db.SaveChangesAsync();
        return Ok(await BuildOrderResponse(order.Id));
    }

    [HttpGet("orders/{orderId:guid}")]
    public async Task<ActionResult<OrderResponse>> GetOrder(Guid orderId)
    {
        var response = await BuildOrderResponse(orderId);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("receipts/{orderId:guid}")]
    public async Task<ActionResult<SelfOrderReceiptResponse>> GetReceipt(Guid orderId)
    {
        var order = await _db.Orders.AsNoTracking().Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == orderId);
        if (order is null)
        {
            return NotFound();
        }

        if (order.Status != OrderStatus.Completed)
        {
            return BadRequest(new { message = "Struk belum tersedia — pesanan belum selesai dibayar." });
        }

        string? tableNumber = null;
        if (order.TableId is not null)
        {
            tableNumber = await _db.Tables.Where(t => t.Id == order.TableId).Select(t => t.Number).SingleOrDefaultAsync();
        }

        var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
        var productNames = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name);

        var items = order.Items
            .Select(i => new SelfOrderReceiptItem(productNames.GetValueOrDefault(i.ProductId, "?"), i.Quantity, i.UnitPrice, i.Subtotal))
            .ToList();

        return Ok(new SelfOrderReceiptResponse(order.Id, tableNumber, order.CreatedAt, items, order.TotalAmount));
    }

    private async Task<OrderResponse?> BuildOrderResponse(Guid orderId)
    {
        // AsNoTracking: same reasoning as OrdersController.BuildOrderResponse — Status
        // here can be mutated via ExecuteUpdateAsync elsewhere, and the identity map
        // must not hand back a stale tracked instance.
        var order = await _db.Orders.AsNoTracking().Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == orderId);
        if (order is null)
        {
            return null;
        }

        var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
        var productNames = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name);
        var variantIds = order.Items.Where(i => i.ProductVariantId is not null).Select(i => i.ProductVariantId!.Value).Distinct().ToList();
        var variantNames = await _db.ProductVariants.Where(v => variantIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.Name);

        return new OrderResponse(
            order.Id,
            order.TableId,
            order.ShiftId,
            order.Status,
            order.TotalAmount,
            order.Items.Select(i => new OrderItemResponse(
                i.Id, i.ProductId, productNames.GetValueOrDefault(i.ProductId, "?"),
                i.ProductVariantId, i.ProductVariantId is null ? null : variantNames.GetValueOrDefault(i.ProductVariantId.Value, "?"),
                i.Quantity, i.UnitPrice, i.Subtotal, i.Station, i.Notes))
                .ToList());
    }
}
