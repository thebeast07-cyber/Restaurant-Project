using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Contracts;
using Restaurant.Domain.Sales;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Api.Controllers;

[ApiController]
[Route("api/tables")]
[Authorize]
public class TablesController : ControllerBase
{
    private readonly AppDbContext _db;

    public TablesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<TableResponse>>> List()
    {
        // Table.Status alone says Occupied but not which Order — the UI needs the
        // active Order's id to resume a cart when a cashier taps an occupied table.
        // Draft/Open are the two pre-checkout statuses (see Order.cs); a table stays
        // Occupied only while its Order is in one of those (Checkout/Void both
        // release the table in the same transaction that leaves this window).
        var activeOrdersByTable = await _db.Orders
            .Where(o => o.TableId != null && (o.Status == OrderStatus.Draft || o.Status == OrderStatus.Open))
            .Select(o => new { o.TableId, o.Id })
            .ToDictionaryAsync(o => o.TableId!.Value, o => o.Id);

        var tables = await _db.Tables
            .OrderBy(t => t.Number)
            .Select(t => new { t.Id, t.Number, t.Status })
            .ToListAsync();

        var response = tables
            .Select(t => new TableResponse(t.Id, t.Number, t.Status, activeOrdersByTable.GetValueOrDefault(t.Id)))
            .ToList();

        return Ok(response);
    }
}
