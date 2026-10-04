namespace PlaywrightLab.Api.Models;

/// <summary>
/// 整筆簽核申請目前的總體狀態。
/// </summary>
public enum ApprovalRequestStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>
/// 單一簽核關卡的狀態；Waiting 表示尚未輪到該關卡。
/// </summary>
public enum ApprovalStepStatus
{
    Waiting,
    Pending,
    Approved,
    Rejected
}

/// <summary>
/// 固定簽核流程中的一個關卡及其簽核者。
/// </summary>
public sealed record ApprovalWorkflowStep(int Order, string ApproverId, string ApproverName);

/// <summary>
/// 申請中的實際簽核關卡，包含目前狀態、意見與決定時間。
/// </summary>
public sealed record ApprovalStep(
    int Order,
    string ApproverId,
    string ApproverName,
    ApprovalStepStatus Status,
    string? Comment,
    DateTimeOffset? DecidedAt);

/// <summary>
/// 記憶體中保存的簽核申請完整資料。
/// </summary>
public sealed record ApprovalRequest(
    Guid Id,
    string Title,
    string Description,
    string ApplicantName,
    DateTimeOffset CreatedAt,
    ApprovalRequestStatus Status,
    int CurrentStepOrder,
    IReadOnlyList<ApprovalStep> Steps);

/// <summary>
/// 建立簽核申請時由 API 接收的資料。
/// </summary>
public sealed class CreateApprovalRequestRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(120)]
    public string Title { get; init; } = string.Empty;

    [System.ComponentModel.DataAnnotations.StringLength(2000)]
    public string Description { get; init; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(120)]
    public string ApplicantName { get; init; } = string.Empty;
}

/// <summary>
/// 核准或駁回時由 API 接收的簽核者與意見。
/// </summary>
public sealed class ApprovalDecisionRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    public string ApproverId { get; init; } = string.Empty;

    [System.ComponentModel.DataAnnotations.StringLength(1000)]
    public string? Comment { get; init; }
}