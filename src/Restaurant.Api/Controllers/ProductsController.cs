using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Common;
using Restaurant.Api.Contracts;
using Restaurant.Domain.Catalog;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantProvider _tenant;

    public ProductsController(AppDbContext db, ICurrentTenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<List<ProductResponse>>> List([FromQuery] bool includeInactive = false)
    {
        var query = _db.Products.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        var products = await query
            .Include(p => p.RecipeItems)
            .OrderBy(p => p.Name)
            .ToListAsync();

        var ingredientIds = products.SelectMany(p => p.RecipeItems).Select(r => r.IngredientId).Distinct().ToList();
        var ingredientNames = await _db.Ingredients
            .Where(i => ingredientIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Name);

        var response = products.Select(p => new ProductResponse(
            p.Id,
            p.Name,
            p.CategoryId,
            p.Price,
            p.Station,
            p.IsActive,
            p.RecipeItems.Select(r => new RecipeItemResponse(
                r.Id, r.IngredientId, ingredientNames.GetValueOrDefault(r.IngredientId, "?"), r.Quantity, r.Unit)).ToList()
        )).ToList();

        return Ok(response);
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<ProductResponse>> Create(CreateProductRequest request)
    {
        var categoryExists = await _db.Categories.AnyAsync(c => c.Id == request.CategoryId);
        if (!categoryExists)
        {
            return BadRequest(new { message = "CategoryId not found." });
        }

        var ingredientIds = request.RecipeItems.Select(r => r.IngredientId).ToList();
        var validIngredientCount = await _db.Ingredients.CountAsync(i => ingredientIds.Contains(i.Id));
        if (validIngredientCount != ingredientIds.Distinct().Count())
        {
            return BadRequest(new { message = "One or more IngredientId not found." });
        }

        var product = new Product
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            CategoryId = request.CategoryId,
            Name = request.Name,
            Price = request.Price,
            Station = request.Station
        };

        foreach (var item in request.RecipeItems)
        {
            product.RecipeItems.Add(new RecipeItem
            {
                TenantId = _tenant.TenantId!.Value,
                ProductId = product.Id,
                IngredientId = item.IngredientId,
                Quantity = item.Quantity,
                Unit = item.Unit
            });
        }

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var ingredientNames = await _db.Ingredients
            .Where(i => ingredientIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Name);

        return Ok(new ProductResponse(
            product.Id,
            product.Name,
            product.CategoryId,
            product.Price,
            product.Station,
            product.IsActive,
            product.RecipeItems.Select(r => new RecipeItemResponse(
                r.Id, r.IngredientId, ingredientNames.GetValueOrDefault(r.IngredientId, "?"), r.Quantity, r.Unit)).ToList()
        ));
    }

    /// <summary>
    /// Updates a Product's own fields and replaces its Recipe wholesale (delete all
    /// existing RecipeItems, insert the ones given) rather than diffing line-by-line —
    /// a menu item's recipe is small and edited as a whole in the UI, so there's no
    /// need for per-line add/remove endpoints. IsActive doubles as this domain's
    /// "delete": Product.Id is a required FK on OrderItem and RecipeItem, so a
    /// Product that has ever been ordered can't be hard-deleted without breaking that
    /// order's history — deactivating (hiding it from ProductsController.List, which
    /// already filters IsActive) is the only safe removal path, and the field already
    /// existed for this from Day 1 of the catalog.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<ProductResponse>> Update(Guid id, UpdateProductRequest request)
    {
        var product = await _db.Products.Include(p => p.RecipeItems).SingleOrDefaultAsync(p => p.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        var categoryExists = await _db.Categories.AnyAsync(c => c.Id == request.CategoryId);
        if (!categoryExists)
        {
            return BadRequest(new { message = "CategoryId not found." });
        }

        var ingredientIds = request.RecipeItems.Select(r => r.IngredientId).ToList();
        var validIngredientCount = await _db.Ingredients.CountAsync(i => ingredientIds.Contains(i.Id));
        if (validIngredientCount != ingredientIds.Distinct().Count())
        {
            return BadRequest(new { message = "One or more IngredientId not found." });
        }

        product.Name = request.Name;
        product.CategoryId = request.CategoryId;
        product.Price = request.Price;
        product.Station = request.Station;
        product.IsActive = request.IsActive;

        // Add new items to their own DbSet, not product.RecipeItems.Add() — EF Core's
        // collection-navigation fixup doesn't reliably mark an item Added when the
        // parent (product) was already tracked before the child was added (see
        // implementation-notes.md's "Adding a child entity to an already-tracked
        // parent's collection" gotcha). Using _db.RecipeItems.Add() directly avoids
        // it; fixup still appends the new item into product.RecipeItems in memory for
        // the response below, since its ProductId matches this tracked Product.
        _db.RecipeItems.RemoveRange(product.RecipeItems);

        foreach (var item in request.RecipeItems)
        {
            _db.RecipeItems.Add(new RecipeItem
            {
                TenantId = _tenant.TenantId!.Value,
                ProductId = product.Id,
                IngredientId = item.IngredientId,
                Quantity = item.Quantity,
                Unit = item.Unit
            });
        }

        await _db.SaveChangesAsync();

        var ingredientNames = await _db.Ingredients
            .Where(i => ingredientIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Name);

        return Ok(new ProductResponse(
            product.Id,
            product.Name,
            product.CategoryId,
            product.Price,
            product.Station,
            product.IsActive,
            product.RecipeItems.Select(r => new RecipeItemResponse(
                r.Id, r.IngredientId, ingredientNames.GetValueOrDefault(r.IngredientId, "?"), r.Quantity, r.Unit)).ToList()
        ));
    }
}
