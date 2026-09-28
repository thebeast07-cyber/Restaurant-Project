namespace Restaurant.Api.Contracts;

public record LoginRequest(string Username, string Password);

public record LoginResponse(string Token, string Name, string Role, Guid TenantId, Guid BranchId);
