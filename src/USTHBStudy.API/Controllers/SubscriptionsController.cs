namespace USTHBStudy.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Common;
using USTHBStudy.Application.Subscriptions;

[Route("api/subscriptions")]
public sealed class SubscriptionsController : ApiControllerBase
{
    private readonly ISubscriptionPlanService _plans;
    private readonly ISubscriptionService _subscriptions;
    private readonly ICurrentUser _currentUser;

    public SubscriptionsController(
        ISubscriptionPlanService plans, ISubscriptionService subscriptions, ICurrentUser currentUser)
    {
        _plans = plans;
        _subscriptions = subscriptions;
        _currentUser = currentUser;
    }

    private Guid UserId => _currentUser.UserId ?? throw new UnauthorizedAppException();

    /// <summary>Public list of active plans (PRD §22). Prices come from the DB, never hard-coded.</summary>
    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SubscriptionPlanDto>>>> Plans(CancellationToken ct) =>
        Ok(ApiResponse.Data(await _plans.ListAsync(includeInactive: false, ct)));

    /// <summary>Starts checkout for a plan: a pending subscription + payment with instructions (PRD §25).</summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ApiResponse<CheckoutResult>>> Subscribe(CreateSubscriptionRequest request, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _subscriptions.CheckoutAsync(UserId, request.PlanId, ct),
            "Subscription created. Complete the payment and an administrator will activate it."));

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<MySubscriptionsDto>>> Mine(CancellationToken ct) =>
        Ok(ApiResponse.Data(await _subscriptions.GetMineAsync(UserId, ct)));
}
