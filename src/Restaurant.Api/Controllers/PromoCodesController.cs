using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Contracts;
using Restaurant.Domain.Common;
using Restaurant.Domain.Promotion;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Api.Controllers;

/// <summary>
/// Admin management of PromoCode rules (docs/architecture/02-ui-roadmap.md, CRM &
/// Promotion phase) — applying a code to an Order lives on OrdersController and
/// SelfOrderController instead, since that's a cashier/customer action, not an
/// admin one.
/// </summary>
[ApiController]
[Route("api/promo-codes")]
[Authorize(Roles = "Owner,Manager")]
public class PromoCodesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantProvider _tenant;

    public PromoCodesController(AppDbContext db, ICurrentTenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<List<PromoCodeResponse>>> List()
    {
        var promoCodes = await _db.PromoCodes
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PromoCodeResponse(
                p.Id, p.Code, p.PercentageOff, p.MinimumPurchase, p.ExpiresAt, p.UsageLimit, p.UsageCount, p.IsActive))
            .ToListAsync();

        return Ok(promoCodes);
    }

    [HttpPost]
    public async Task<ActionResult<PromoCodeResponse>> Create(CreatePromoCodeRequest request)
    {
        if (request.PercentageOff is <= 0 or > 100)
        {
            return BadRequest(new { message = "PercentageOff must be between 0 and 100." });
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        if (normalizedCode.Length == 0)
        {
            return BadRequest(new { message = "Code is required." });
        }

        var codeExists = await _db.PromoCodes.AnyAsync(p =>
            p.TenantId == _tenant.TenantId!.Value && p.BranchId == _tenant.BranchId!.Value && p.Code == normalizedCode);
        if (codeExists)
        {
            return BadRequest(new { message = "A promo code with this Code already exists." });
        }

        var promoCode = new PromoCode
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            Code = normalizedCode,
            PercentageOff = request.PercentageOff,
            MinimumPurchase = request.MinimumPurchase,
            ExpiresAt = request.ExpiresAt,
            UsageLimit = request.UsageLimit
        };

        _db.PromoCodes.Add(promoCode);
        await _db.SaveChangesAsync();

        return Ok(new PromoCodeResponse(
            promoCode.Id, promoCode.Code, promoCode.PercentageOff, promoCode.MinimumPurchase,
            promoCode.ExpiresAt, promoCode.UsageLimit, promoCode.UsageCount, promoCode.IsActive));
    }

    /// <summary>
    /// Code itself is immutable after creation — an Order that already applied this
    /// code snapshots PromoCodeId, not the Code string, so renaming wouldn't corrupt
    /// anything technically, but a changed Code would confuse whoever handed out the
    /// old one. Deactivating (IsActive = false) is the intended way to retire a code.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PromoCodeResponse>> Update(Guid id, UpdatePromoCodeRequest request)
    {
        if (request.PercentageOff is <= 0 or > 100)
        {
            return BadRequest(new { message = "PercentageOff must be between 0 and 100." });
        }

        var promoCode = await _db.PromoCodes.SingleOrDefaultAsync(p => p.Id == id);
        if (promoCode is null)
        {
            return NotFound();
        }

        promoCode.PercentageOff = request.PercentageOff;
        promoCode.MinimumPurchase = request.MinimumPurchase;
        promoCode.ExpiresAt = request.ExpiresAt;
        promoCode.UsageLimit = request.UsageLimit;
        promoCode.IsActive = request.IsActive;

        await _db.SaveChangesAsync();

        return Ok(new PromoCodeResponse(
            promoCode.Id, promoCode.Code, promoCode.PercentageOff, promoCode.MinimumPurchase,
            promoCode.ExpiresAt, promoCode.UsageLimit, promoCode.UsageCount, promoCode.IsActive));
    }
}
