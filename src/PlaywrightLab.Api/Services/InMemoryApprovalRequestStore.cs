using PlaywrightLab.Api.Models;

namespace PlaywrightLab.Api.Services;

/// <summary>
/// Store 執行簽核決定後回傳給 Controller 的結果。
/// </summary>
public enum ApprovalDecisionResult
{
    Succeeded,
    NotFound,
    WrongApprover,
    AlreadyCompleted
}

/// <summary>
/// 管理簽核申請的記憶體資料服務。
/// </summary>
public sealed class InMemoryApprovalRequestStore
{
    // Dictionary 不是執行緒安全的，因此所有讀寫都透過同一把鎖保護。
    private readonly object syncRoot = new();
    private readonly Dictionary<Guid, ApprovalRequest> requests = new();

    // 本範例使用固定的兩關流程：直屬主管完成後，才輪到部門主管。
    private readonly ApprovalWorkflowStep[] workflow =
    [
        new(1, "supervisor-01", "Direct Supervisor"),
        new(2, "department-manager-01", "Department Manager")
    ];

    /// <summary>
    /// 建立啟動時的範例資料；若已有資料則不重複 Seed。
    /// </summary>
    public void Seed()
    {
        lock (syncRoot)
        {
            if (requests.Count > 0)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            // 第一筆停在第一關，方便測試第一位簽核者的流程。
            requests.Add(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), CreateSeedRequest(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                "Office equipment purchase",
                "Purchase a replacement monitor.",
                "Alex Chen",
                currentStepOrder: 1,
                now));
            // 第二筆已完成第一關，方便直接測試第二位簽核者。
            requests.Add(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), CreateSeedRequest(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                "Training reimbursement",
                "Reimbursement for a technical training course.",
                "Jamie Lin",
                currentStepOrder: 2,
                now.AddMinutes(-30)));
        }
    }

    /// <summary>
    /// 依建立時間由新到舊回傳所有申請的副本。
    /// </summary>
    public IReadOnlyList<ApprovalRequest> GetAll()
    {
        lock (syncRoot)
        {
            return requests.Values
                .OrderByDescending(request => request.CreatedAt)
                .Select(Copy)
                .ToArray();
        }
    }

    /// <summary>
    /// 依識別碼查詢申請；找不到時回傳 null。
    /// </summary>
    public ApprovalRequest? Get(Guid id)
    {
        lock (syncRoot)
        {
            return requests.TryGetValue(id, out var request) ? Copy(request) : null;
        }
    }

    /// <summary>
    /// 回傳固定簽核流程的副本，避免外部修改內部陣列。
    /// </summary>
    public IReadOnlyList<ApprovalWorkflowStep> GetWorkflow() => workflow.ToArray();

    /// <summary>
    /// 建立一筆新申請，並讓第一個關卡進入 Pending，其餘關卡維持 Waiting。
    /// </summary>
    public ApprovalRequest Create(CreateApprovalRequestRequest input)
    {
        lock (syncRoot)
        {
            var request = new ApprovalRequest(
                Guid.NewGuid(),
                input.Title,
                input.Description,
                input.ApplicantName,
                DateTimeOffset.UtcNow,
                ApprovalRequestStatus.Pending,
                workflow[0].Order,
                workflow.Select((step, index) => new ApprovalStep(
                    step.Order,
                    step.ApproverId,
                    step.ApproverName,
                    index == 0 ? ApprovalStepStatus.Pending : ApprovalStepStatus.Waiting,
                    null,
                    null)).ToArray());

            requests.Add(request.Id, request);
            return Copy(request);
        }
    }

    /// <summary>
    /// 執行核准或駁回，並確認操作者是目前關卡指定的簽核者。
    /// </summary>
    public ApprovalDecisionResult Decide(Guid id, string approverId, string? comment, bool approve)
    {
        lock (syncRoot)
        {
            if (!requests.TryGetValue(id, out var request))
            {
                return ApprovalDecisionResult.NotFound;
            }

            if (request.Status != ApprovalRequestStatus.Pending)
            {
                return ApprovalDecisionResult.AlreadyCompleted;
            }

            var currentStepIndex = request.CurrentStepOrder - 1;
            var currentStep = request.Steps[currentStepIndex];

            // 只允許目前關卡的簽核者操作，避免跳關或由錯誤人員簽核。
            if (!string.Equals(currentStep.ApproverId, approverId, StringComparison.OrdinalIgnoreCase))
            {
                return ApprovalDecisionResult.WrongApprover;
            }

            var decidedAt = DateTimeOffset.UtcNow;
            var steps = request.Steps.ToArray();
            steps[currentStepIndex] = currentStep with
            {
                Status = approve ? ApprovalStepStatus.Approved : ApprovalStepStatus.Rejected,
                Comment = comment,
                DecidedAt = decidedAt
            };

            var status = approve ? request.Status : ApprovalRequestStatus.Rejected;
            var nextStepOrder = request.CurrentStepOrder;
            if (approve && currentStepIndex < steps.Length - 1)
            {
                // 尚有下一關時，申請保持 Pending，並將下一關設為可簽核。
                nextStepOrder++;
                steps[currentStepIndex + 1] = steps[currentStepIndex + 1] with
                {
                    Status = ApprovalStepStatus.Pending
                };
            }
            else if (approve)
            {
                // 最後一關核准後，整筆申請才算完成。
                status = ApprovalRequestStatus.Approved;
            }

            requests[id] = request with
            {
                Status = status,
                CurrentStepOrder = nextStepOrder,
                Steps = steps
            };

            return ApprovalDecisionResult.Succeeded;
        }
    }

    /// <summary>
    /// 依指定的目前關卡建立 Seed 申請，之前的關卡視為已核准。
    /// </summary>
    private ApprovalRequest CreateSeedRequest(
        Guid id,
        string title,
        string description,
        string applicantName,
        int currentStepOrder,
        DateTimeOffset createdAt)
    {
        var steps = workflow.Select(step =>
        {
            // 透過關卡順序推導每個 Seed 關卡的狀態，避免手動重複建立資料。
            var isCompleted = step.Order < currentStepOrder;
            return new ApprovalStep(
                step.Order,
                step.ApproverId,
                step.ApproverName,
                isCompleted
                    ? ApprovalStepStatus.Approved
                    : step.Order == currentStepOrder
                        ? ApprovalStepStatus.Pending
                        : ApprovalStepStatus.Waiting,
                isCompleted ? "Seeded approval" : null,
                isCompleted ? createdAt : null);
        }).ToArray();

        return new ApprovalRequest(
            id,
            title,
            description,
            applicantName,
            createdAt,
            ApprovalRequestStatus.Pending,
            currentStepOrder,
            steps);
    }

            /// <summary>
            /// 複製申請及其 Steps 陣列，避免呼叫端持有內部集合的可變參考。
            /// </summary>
    private static ApprovalRequest Copy(ApprovalRequest request) => request with
    {
        Steps = request.Steps.ToArray()
    };
}