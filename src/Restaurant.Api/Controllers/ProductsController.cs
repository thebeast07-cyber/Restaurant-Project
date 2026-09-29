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
    public async Task<ActionResult<List<ProductResponse>>> List()
    {
        var products = await _db.Products
            .Where(p => p.IsActive)
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
}
