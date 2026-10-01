using Restaurant.Domain.Catalog;

namespace Restaurant.Api.Contracts;

public record CategoryResponse(Guid Id, string Name);
public record CreateCategoryRequest(string Name);
public record UpdateCategoryRequest(string Name);

public record IngredientResponse(Guid Id, string Name, string Unit, decimal CurrentStock, decimal MinimumStock, decimal AverageCost);
public record CreateIngredientRequest(string Name, string Unit, decimal MinimumStock = 0);
public record UpdateMinimumStockRequest(decimal MinimumStock);

public record RecipeItemRequest(Guid IngredientId, decimal Quantity, string Unit);
public record RecipeItemResponse(Guid Id, Guid IngredientId, string IngredientName, decimal Quantity, string Unit);

/// <summary>Id is null for a brand-new Variant being added; set (and preserved) for
/// an existing one being edited — see ProductsController.Update for why that
/// distinction matters (preserving OrderItem's FK to past orders).</summary>
public record ProductVariantRequest(Guid? Id, string Name, decimal Price, List<RecipeItemRequest> RecipeItems);

public record ProductVariantResponse(Guid Id, string Name, decimal Price, List<RecipeItemResponse> RecipeItems);

public record CreateProductRequest(
    string Name,
    Guid CategoryId,
    decimal Price,
    Station Station,
    List<RecipeItemRequest> RecipeItems,
    List<ProductVariantRequest>? Variants = null);

public record ProductResponse(
    Guid Id,
    string Name,
    Guid CategoryId,
    decimal Price,
    Station Station,
    bool IsActive,
    string? ImageUrl,
    List<RecipeItemResponse> RecipeItems,
    List<ProductVariantResponse> Variants);

public record UpdateProductRequest(
    string Name,
    Guid CategoryId,
    decimal Price,
    Station Station,
    bool IsActive,
    List<RecipeItemRequest> RecipeItems,
    List<ProductVariantRequest>? Variants = null);
