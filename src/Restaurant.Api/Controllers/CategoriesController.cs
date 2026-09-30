using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Common;
using Restaurant.Api.Contracts;
using Restaurant.Domain.Catalog;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantProvider _tenant;

    public CategoriesController(AppDbContext db, ICurrentTenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<List<CategoryResponse>>> List()
    {
        var categories = await _db.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryResponse(c.Id, c.Name))
            .ToListAsync();

        return Ok(categories);
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<CategoryResponse>> Create(CreateCategoryRequest request)
    {
        var category = new Category
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            Name = request.Name
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return Ok(new CategoryResponse(category.Id, category.Name));
    }

    /// <summary>
    /// Rename only — no Delete endpoint. Category.Id is a required FK on Product
    /// (CategoryId), so a Category still referenced by any Product can't be removed
    /// without either cascading or orphaning those Products; deferred rather than
    /// building a "delete only if unused" guard nobody asked for yet.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<IActionResult> Update(Guid id, UpdateCategoryRequest request)
    {
        var updated = await _db.Categories
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.Name, request.Name));

        return updated == 0 ? NotFound() : NoContent();
    }
}
