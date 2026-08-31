namespace USTHBStudy.API.Controllers.Admin;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USTHBStudy.API.Controllers;
using USTHBStudy.Application.Authorization;
using USTHBStudy.Application.Common;
using USTHBStudy.Application.Subscriptions;

[Route("api/admin/subscription-plans")]
[Authorize(Policy = Permissions.Subscriptions.Manage)]
public sealed class AdminSubscriptionPlansController : ApiControllerBase
{
    private readonly ISubscriptionPlanService _plans;

    public AdminSubscriptionPlansController(ISubscriptionPlanService plans) => _plans = plans;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SubscriptionPlanDto>>>> List(CancellationToken ct) =>
        Ok(ApiResponse.Data(await _plans.ListAsync(includeInactive: true, ct)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<SubscriptionPlanDto>>> Create(SubscriptionPlanInput input, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _plans.CreateAsync(input, ct), "Plan created."));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SubscriptionPlanDto>>> Update(Guid id, SubscriptionPlanInput input, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _plans.UpdateAsync(id, input, ct), "Plan updated."));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        await _plans.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Plan deactivated."));
    }
}

[Route("api/admin")]
[Authorize(Policy = Permissions.Subscriptions.Manage)]
public sealed class AdminPaymentsController : ApiControllerBase
{
    private readonly ISubscriptionService _subscriptions;

    public AdminPaymentsController(ISubscriptionService subscriptions) => _subscriptions = subscriptions;

    /// <summary>Payments awaiting verification (PRD §25). Filter with <c>?status=Pending</c>.</summary>
    [HttpGet("payments")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminPaymentDto>>>> Payments(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse.Page(await _subscriptions.ListPaymentsAsync(new PaymentQuery(status, page, pageSize), ct)));

    [HttpPost("payments/{id:guid}/approve")]
    public async Task<ActionResult<ApiResponse<AdminPaymentDto>>> Approve(Guid id, ResolvePaymentRequest request, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _subscriptions.ApprovePaymentAsync(id, request.Note, ct), "Payment approved; Premium activated."));

    [HttpPost("payments/{id:guid}/reject")]
    public async Task<ActionResult<ApiResponse<AdminPaymentDto>>> Reject(Guid id, ResolvePaymentRequest request, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _subscriptions.RejectPaymentAsync(id, request.Note, ct), "Payment rejected."));

    [HttpPost("subscriptions/{id:guid}/extend")]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> Extend(Guid id, ExtendSubscriptionRequest request, CancellationToken ct) =>
        Ok(ApiResponse.Data(await _subscriptions.ExtendSubscriptionAsync(id, request.Days, ct), "Subscription extended."));
}
