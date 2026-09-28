using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Common;
using Restaurant.Api.Contracts;
using Restaurant.Domain.Catalog;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Api.Controllers;

[ApiController]
[Route("api/ingredients")]
[Authorize]
public class IngredientsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantProvider _tenant;

    public IngredientsController(AppDbContext db, ICurrentTenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<List<IngredientResponse>>> List()
    {
        var ingredients = await _db.Ingredients
            .OrderBy(i => i.Name)
            .Select(i => new IngredientResponse(i.Id, i.Name, i.Unit))
            .ToListAsync();

        return Ok(ingredients);
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<IngredientResponse>> Create(CreateIngredientRequest request)
    {
        var ingredient = new Ingredient
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            Name = request.Name,
            Unit = request.Unit
        };

        _db.Ingredients.Add(ingredient);
        await _db.SaveChangesAsync();

        return Ok(new IngredientResponse(ingredient.Id, ingredient.Name, ingredient.Unit));
    }
}
