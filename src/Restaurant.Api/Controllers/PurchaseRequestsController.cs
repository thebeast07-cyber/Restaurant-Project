using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Auth;
using Restaurant.Api.Contracts;
using Restaurant.Domain.Common;
using Restaurant.Domain.Purchasing;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Api.Controllers;

/// <summary>
/// The "Purchase Request -> Approval" half of the PRD §11 Purchasing Flow. Create is
/// Manager-only (not Owner) and Approve/Reject is Owner-only (not Manager) —
/// deliberately asymmetric, matching the separation of duties from project
/// discussion: Kitchen/Bar don't have their own accounts, so a Manager keys in the
/// request on their behalf; Owner reviews it, doesn't create it. See
/// implementation-notes.md for why this doesn't match PRD's literal "Warehouse
/// Staff" actor.
/// </summary>
[ApiController]
[Route("api/purchase-requests")]
[Authorize]
public class PurchaseRequestsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantProvider _tenant;

    public PurchaseRequestsController(AppDbContext db, ICurrentTenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<List<PurchaseRequestResponse>>> List([FromQuery] PurchaseRequestStatus? status)
    {
        var query = _db.PurchaseRequests.Include(pr => pr.Items).AsQueryable();
        if (status is not null)
        {
            query = query.Where(pr => pr.Status == status);
        }

        var requests = await query.OrderByDescending(pr => pr.CreatedAt).ToListAsync();

        // Sequential, not Task.WhenAll: BuildResponseAsync queries _db, and DbContext
        // is not thread-safe for concurrent operations on the same instance — running
        // these in parallel throws "A second operation was started on this context
        // instance before a previous operation completed" as soon as List returns more
        // than one row (only surfaced once a caller had 2+ PurchaseRequests to list).
        var response = new List<PurchaseRequestResponse>();
        foreach (var request in requests)
        {
            response.Add(await BuildResponseAsync(request));
        }
        return Ok(response);
    }

    [HttpPost]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<PurchaseRequestResponse>> Create(CreatePurchaseRequestRequest request)
    {
        if (request.Items.Count == 0)
        {
            return BadRequest(new { message = "A purchase request needs at least one item." });
        }

        var ingredientIds = request.Items.Select(i => i.IngredientId).ToList();
        var validIngredientCount = await _db.Ingredients.CountAsync(i => ingredientIds.Contains(i.Id));
        if (validIngredientCount != ingredientIds.Distinct().Count())
        {
            return BadRequest(new { message = "One or more IngredientId values are invalid." });
        }

        var purchaseRequest = new PurchaseRequest
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            RequestedByUserId = User.GetUserId(),
            RequestedFor = request.RequestedFor,
            Notes = request.Notes
        };

        foreach (var item in request.Items)
        {
            purchaseRequest.Items.Add(new PurchaseRequestItem
            {
                TenantId = _tenant.TenantId!.Value,
                PurchaseRequestId = purchaseRequest.Id,
                IngredientId = item.IngredientId,
                Quantity = item.Quantity,
                Unit = item.Unit
            });
        }

        _db.PurchaseRequests.Add(purchaseRequest);
        await _db.SaveChangesAsync();

        return Ok(await BuildResponseAsync(purchaseRequest));
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<PurchaseRequestResponse>> Approve(Guid id, ReviewPurchaseRequestRequest request)
    {
        return await Review(id, PurchaseRequestStatus.Approved, request.Notes);
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<PurchaseRequestResponse>> Reject(Guid id, ReviewPurchaseRequestRequest request)
    {
        return await Review(id, PurchaseRequestStatus.Rejected, request.Notes);
    }

    /// <summary>
    /// Atomic claim, same guarded-transition pattern used everywhere else in this
    /// codebase for a status a request can only leave once (Pending -> Approved or
    /// Pending -> Rejected) — two concurrent Approve/Reject calls on one request must
    /// not both succeed.
    /// </summary>
    private async Task<ActionResult<PurchaseRequestResponse>> Review(Guid id, PurchaseRequestStatus newStatus, string? notes)
    {
        var exists = await _db.PurchaseRequests.AnyAsync(pr => pr.Id == id);
        if (!exists)
        {
            return NotFound();
        }

        var reviewedAt = DateTimeOffset.UtcNow;
        var reviewerId = User.GetUserId();

        var claimed = await _db.PurchaseRequests
            .Where(pr => pr.Id == id && pr.Status == PurchaseRequestStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(pr => pr.Status, newStatus)
                .SetProperty(pr => pr.ReviewedByUserId, reviewerId)
                .SetProperty(pr => pr.ReviewedAt, reviewedAt)
                .SetProperty(pr => pr.ReviewNotes, notes));

        if (claimed == 0)
        {
            return BadRequest(new { message = "This request was already reviewed." });
        }

        var updated = await _db.PurchaseRequests.AsNoTracking().Include(pr => pr.Items).SingleAsync(pr => pr.Id == id);
        return Ok(await BuildResponseAsync(updated));
    }

    private async Task<PurchaseRequestResponse> BuildResponseAsync(PurchaseRequest pr)
    {
        var ingredientIds = pr.Items.Select(i => i.IngredientId).ToList();
        var ingredientNames = await _db.Ingredients
            .Where(i => ingredientIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Name);

        var items = pr.Items
            .Select(i => new PurchaseRequestItemResponse(i.IngredientId, ingredientNames.GetValueOrDefault(i.IngredientId, "?"), i.Quantity, i.Unit))
            .ToList();

        return new PurchaseRequestResponse(pr.Id, pr.RequestedFor, pr.Notes, pr.Status, pr.ReviewedByUserId, pr.ReviewedAt, pr.ReviewNotes, items);
    }
}
