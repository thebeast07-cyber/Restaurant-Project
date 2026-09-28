using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Auth;
using Restaurant.Api.Contracts;
using Restaurant.Domain.Common;
using Restaurant.Domain.Sales;
using Restaurant.Infrastructure.Persistence;

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
