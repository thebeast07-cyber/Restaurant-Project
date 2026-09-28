using Restaurant.Domain.Catalog;

namespace Restaurant.Api.Contracts;

public record CategoryResponse(Guid Id, string Name);
public record CreateCategoryRequest(string Name);

public record IngredientResponse(Guid Id, string Name, string Unit);
public record CreateIngredientRequest(string Name, string Unit);

public record RecipeItemRequest(Guid IngredientId, decimal Quantity, string Unit);
public record RecipeItemResponse(Guid Id, Guid IngredientId, string IngredientName, decimal Quantity, string Unit);

public record CreateProductRequest(
    string Name,
    Guid CategoryId,
    decimal Price,
    Station Station,
    List<RecipeItemRequest> RecipeItems);

public record ProductResponse(
    Guid Id,
    string Name,
    Guid CategoryId,
    decimal Price,
    Station Station,
    bool IsActive,
    List<RecipeItemResponse> RecipeItems);
