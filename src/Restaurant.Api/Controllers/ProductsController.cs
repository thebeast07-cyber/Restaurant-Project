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
    private static readonly Dictionary<string, string> AllowedImageTypes = new()
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };
    private const long MaxImageBytes = 2 * 1024 * 1024;

    private readonly AppDbContext _db;
    private readonly ICurrentTenantProvider _tenant;
    private readonly IWebHostEnvironment _env;

    public ProductsController(AppDbContext db, ICurrentTenantProvider tenant, IWebHostEnvironment env)
    {
        _db = db;
        _tenant = tenant;
        _env = env;
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
            .Include(p => p.Variants).ThenInclude(v => v.RecipeItems)
            .OrderBy(p => p.Name)
            .ToListAsync();

        var ingredientIds = products
            .SelectMany(p => p.RecipeItems.Select(r => r.IngredientId)
                .Concat(p.Variants.SelectMany(v => v.RecipeItems.Select(r => r.IngredientId))))
            .Distinct()
            .ToList();
        var ingredientNames = await _db.Ingredients
            .Where(i => ingredientIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Name);

        var response = products.Select(p => BuildResponse(p, ingredientNames)).ToList();

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

        var variants = request.Variants ?? [];
        if (variants.Count > 0 && request.RecipeItems.Count > 0)
        {
            return BadRequest(new { message = "A product with variants keeps its recipe per-variant, not at the product level — pass RecipeItems on each variant instead." });
        }

        var allIngredientIds = request.RecipeItems.Select(r => r.IngredientId)
            .Concat(variants.SelectMany(v => v.RecipeItems.Select(r => r.IngredientId)))
            .Distinct()
            .ToList();
        var validIngredientCount = await _db.Ingredients.CountAsync(i => allIngredientIds.Contains(i.Id));
        if (validIngredientCount != allIngredientIds.Count)
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

        foreach (var variantRequest in variants)
        {
            var variant = new ProductVariant
            {
                TenantId = _tenant.TenantId!.Value,
                ProductId = product.Id,
                Name = variantRequest.Name,
                Price = variantRequest.Price
            };

            foreach (var item in variantRequest.RecipeItems)
            {
                variant.RecipeItems.Add(new RecipeItem
                {
                    TenantId = _tenant.TenantId!.Value,
                    ProductId = product.Id,
                    ProductVariantId = variant.Id,
                    IngredientId = item.IngredientId,
                    Quantity = item.Quantity,
                    Unit = item.Unit
                });
            }

            product.Variants.Add(variant);
        }

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var ingredientNames = await _db.Ingredients
            .Where(i => allIngredientIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Name);

        return Ok(BuildResponse(product, ingredientNames));
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
    ///
    /// Variants follow the same soft-delete discipline for the same FK reason
    /// (OrderItem.ProductVariantId): a Variant whose Id is included in the request is
    /// updated in place (preserving its Id, so past orders still resolve); a Variant
    /// that was active but is missing from the request is deactivated, never deleted;
    /// a request entry with no Id is a brand-new Variant.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<ProductResponse>> Update(Guid id, UpdateProductRequest request)
    {
        var product = await _db.Products
            .Include(p => p.RecipeItems)
            .Include(p => p.Variants).ThenInclude(v => v.RecipeItems)
            .SingleOrDefaultAsync(p => p.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        var categoryExists = await _db.Categories.AnyAsync(c => c.Id == request.CategoryId);
        if (!categoryExists)
        {
            return BadRequest(new { message = "CategoryId not found." });
        }

        var variants = request.Variants ?? [];
        if (variants.Count > 0 && request.RecipeItems.Count > 0)
        {
            return BadRequest(new { message = "A product with variants keeps its recipe per-variant, not at the product level — pass RecipeItems on each variant instead." });
        }

        var requestVariantIds = variants.Where(v => v.Id is not null).Select(v => v.Id!.Value).ToList();
        var unknownVariantIds = requestVariantIds.Except(product.Variants.Select(v => v.Id)).ToList();
        if (unknownVariantIds.Count > 0)
        {
            return BadRequest(new { message = "One or more Variant Id does not belong to this product." });
        }

        var allIngredientIds = request.RecipeItems.Select(r => r.IngredientId)
            .Concat(variants.SelectMany(v => v.RecipeItems.Select(r => r.IngredientId)))
            .Distinct()
            .ToList();
        var validIngredientCount = await _db.Ingredients.CountAsync(i => allIngredientIds.Contains(i.Id));
        if (validIngredientCount != allIngredientIds.Count)
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

        foreach (var existingVariant in product.Variants)
        {
            var matching = variants.SingleOrDefault(v => v.Id == existingVariant.Id);
            if (matching is null)
            {
                // Present before, absent from this request — deactivate, don't delete
                // (see method summary for why).
                existingVariant.IsActive = false;
                continue;
            }

            existingVariant.Name = matching.Name;
            existingVariant.Price = matching.Price;
            existingVariant.IsActive = true;

            _db.RecipeItems.RemoveRange(existingVariant.RecipeItems);
            foreach (var item in matching.RecipeItems)
            {
                _db.RecipeItems.Add(new RecipeItem
                {
                    TenantId = _tenant.TenantId!.Value,
                    ProductId = product.Id,
                    ProductVariantId = existingVariant.Id,
                    IngredientId = item.IngredientId,
                    Quantity = item.Quantity,
                    Unit = item.Unit
                });
            }
        }

        foreach (var newVariantRequest in variants.Where(v => v.Id is null))
        {
            var variant = new ProductVariant
            {
                TenantId = _tenant.TenantId!.Value,
                ProductId = product.Id,
                Name = newVariantRequest.Name,
                Price = newVariantRequest.Price
            };

            foreach (var item in newVariantRequest.RecipeItems)
            {
                variant.RecipeItems.Add(new RecipeItem
                {
                    TenantId = _tenant.TenantId!.Value,
                    ProductId = product.Id,
                    ProductVariantId = variant.Id,
                    IngredientId = item.IngredientId,
                    Quantity = item.Quantity,
                    Unit = item.Unit
                });
            }

            _db.ProductVariants.Add(variant);
            product.Variants.Add(variant);
        }

        await _db.SaveChangesAsync();

        var ingredientNames = await _db.Ingredients
            .Where(i => allIngredientIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Name);

        return Ok(BuildResponse(product, ingredientNames));
    }

    /// <summary>
    /// Stores the file on local disk under wwwroot/uploads/products (served back via
    /// app.UseStaticFiles() in Program.cs) rather than a cloud object store — no
    /// storage vendor has been chosen yet (production hosting is still
    /// discovery-stage, see docs/architecture/02-ui-roadmap.md), so this keeps the
    /// contract to the frontend (ImageUrl is just a URL) unchanged whenever that
    /// decision lands; only this method's storage call would need to move.
    /// Overwrites any previous image for this Product (named by Product.Id, not the
    /// original filename) — no history/versioning needed for a menu photo.
    /// </summary>
    [HttpPost("{id:guid}/image")]
    [Authorize(Roles = "Owner,Manager")]
    [RequestSizeLimit(MaxImageBytes)]
    public async Task<ActionResult<ProductResponse>> UploadImage(Guid id, IFormFile file)
    {
        var product = await _db.Products
            .Include(p => p.RecipeItems)
            .Include(p => p.Variants).ThenInclude(v => v.RecipeItems)
            .SingleOrDefaultAsync(p => p.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        if (file.Length == 0)
        {
            return BadRequest(new { message = "File is empty." });
        }

        if (file.Length > MaxImageBytes)
        {
            return BadRequest(new { message = "File exceeds the 2 MB limit." });
        }

        if (!AllowedImageTypes.TryGetValue(file.ContentType, out var extension))
        {
            return BadRequest(new { message = "Only JPEG, PNG, or WebP images are allowed." });
        }

        var uploadsDir = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "uploads", "products");
        Directory.CreateDirectory(uploadsDir);

        var filePath = Path.Combine(uploadsDir, $"{product.Id}{extension}");
        await using (var stream = System.IO.File.Create(filePath))
        {
            await file.CopyToAsync(stream);
        }

        product.ImageUrl = $"/uploads/products/{product.Id}{extension}";
        await _db.SaveChangesAsync();

        var ingredientIds = product.RecipeItems.Select(r => r.IngredientId)
            .Concat(product.Variants.SelectMany(v => v.RecipeItems.Select(r => r.IngredientId)))
            .Distinct()
            .ToList();
        var ingredientNames = await _db.Ingredients
            .Where(i => ingredientIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Name);

        return Ok(BuildResponse(product, ingredientNames));
    }

    private static ProductResponse BuildResponse(Product product, Dictionary<Guid, string> ingredientNames)
    {
        return new ProductResponse(
            product.Id,
            product.Name,
            product.CategoryId,
            product.Price,
            product.Station,
            product.IsActive,
            product.ImageUrl,
            product.RecipeItems.Select(r => new RecipeItemResponse(
                r.Id, r.IngredientId, ingredientNames.GetValueOrDefault(r.IngredientId, "?"), r.Quantity, r.Unit)).ToList(),
            product.Variants.Where(v => v.IsActive).Select(v => new ProductVariantResponse(
                v.Id, v.Name, v.Price,
                v.RecipeItems.Select(r => new RecipeItemResponse(
                    r.Id, r.IngredientId, ingredientNames.GetValueOrDefault(r.IngredientId, "?"), r.Quantity, r.Unit)).ToList()))
                .ToList());
    }
}
