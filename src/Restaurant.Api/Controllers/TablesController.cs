using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Contracts;
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
        var tables = await _db.Tables
            .OrderBy(t => t.Number)
            .Select(t => new TableResponse(t.Id, t.Number, t.Status))
            .ToListAsync();

        return Ok(tables);
    }
}
