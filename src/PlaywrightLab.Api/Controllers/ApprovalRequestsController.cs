using Microsoft.AspNetCore.Mvc;
using PlaywrightLab.Api.Models;
using PlaywrightLab.Api.Services;

namespace PlaywrightLab.Api.Controllers;

[ApiController]
[Route("api/approval-requests")]
/// <summary>
/// 提供簽核申請查詢、建立、核准與駁回功能。
/// </summary>
public sealed class ApprovalRequestsController(InMemoryApprovalRequestStore store) : ControllerBase
{
    /// <summary>
    /// 取得所有簽核申請。
    /// </summary>
    [HttpGet]
    public ActionResult<IReadOnlyList<ApprovalRequest>> GetAll() => Ok(store.GetAll());

    /// <summary>
    /// 取得目前使用中的固定簽核流程。
    /// </summary>
    [HttpGet("workflow")]
    public ActionResult<IReadOnlyList<ApprovalWorkflowStep>> GetWorkflow() => Ok(store.GetWorkflow());

    /// <summary>
    /// 依識別碼取得單筆簽核申請。
    /// </summary>
    [HttpGet("{id:guid}")]
    public ActionResult<ApprovalRequest> GetById(Guid id)
    {
        var request = store.Get(id);
        return request is null ? NotFound() : Ok(request);
    }

    /// <summary>
    /// 建立新簽核申請，並套用固定的兩關流程。
    /// </summary>
    [HttpPost]
    public ActionResult<ApprovalRequest> Create(CreateApprovalRequestRequest input)
    {
        var request = store.Create(input);
        return CreatedAtAction(nameof(GetById), new { id = request.Id }, request);
    }

    /// <summary>
    /// 核准目前關卡；成功後可能進入下一關或完成整筆申請。
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public ActionResult<ApprovalRequest> Approve(Guid id, ApprovalDecisionRequest input) =>
        ApplyDecision(id, input, approve: true);

    /// <summary>
    /// 駁回目前關卡，駁回後整筆申請即結束。
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    public ActionResult<ApprovalRequest> Reject(Guid id, ApprovalDecisionRequest input) =>
        ApplyDecision(id, input, approve: false);

    /// <summary>
    /// 共用核准與駁回的結果轉換邏輯，將 Store 結果轉成 HTTP 回應。
    /// </summary>
    private ActionResult<ApprovalRequest> ApplyDecision(
        Guid id,
        ApprovalDecisionRequest input,
        bool approve)
    {
        var result = store.Decide(id, input.ApproverId, input.Comment, approve);
        return result switch
        {
            ApprovalDecisionResult.Succeeded => Ok(store.Get(id)),
            ApprovalDecisionResult.NotFound => NotFound(),
            ApprovalDecisionResult.WrongApprover => StatusCode(StatusCodes.Status403Forbidden),
            ApprovalDecisionResult.AlreadyCompleted => Conflict(),
            _ => Problem()
        };
    }
}