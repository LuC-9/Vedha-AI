using ResumeTailor.Domain.Enums;

namespace ResumeTailor.Application.Features.Aksh;

public class AkshTodoItemDto
{
    public string Step { get; set; } = string.Empty;
    public string? Tool { get; set; }
    public string? Detail { get; set; }
    public string State { get; set; } = "pending"; // pending | active | done | failed
}

public class AkshSessionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Goal { get; set; } = string.Empty;
    public string? JobUrl { get; set; }
    public AutonomyLevel AutonomyLevel { get; set; }
    public AkshSessionStatus Status { get; set; }
    public List<AkshTodoItemDto> Todos { get; set; } = new();
    public long TokenInputTotal { get; set; }
    public long TokenOutputTotal { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class AkshMessageDto
{
    public Guid Id { get; set; }
    public string Role { get; set; } = "user";
    public string? ToolName { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public string? ArtifactRef { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class AkshApprovalDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid? QueueItemId { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string ArgumentsJson { get; set; } = "{}";
    public AkshApprovalStatus Status { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
}

public class AkshAuditDto
{
    public AkshSessionDto Session { get; set; } = new();
    public List<AkshMessageDto> Messages { get; set; } = new();
    public List<AkshApprovalDto> Approvals { get; set; } = new();
}

/// <summary>
/// A streamed run event. Types: session | plan | status | message | approval-request | ledger | error | done.
/// Never carries secrets, keys, or full PII blobs — summaries and references only.
/// </summary>
public class AkshStreamEvent
{
    public string Type { get; set; } = string.Empty;
    public object? Data { get; set; }

    public AkshStreamEvent()
    {
    }

    public AkshStreamEvent(string type, object? data)
    {
        Type = type;
        Data = data;
    }
}

public class AkshDecisionResultDto
{
    public bool Approved { get; set; }
    public string? ReasonCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? QueueItemId { get; set; }
    public string? QueueStatus { get; set; }
}

/// <summary>
/// Deterministic plan builder (zero tokens): the visible, editable todo list.
/// The agent may reorder/extend at runtime, but every run starts from this contract.
/// </summary>
public static class AkshPlanner
{
    public static List<AkshTodoItemDto> BuildInitialPlan(string goal, string? jobUrl)
    {
        var goalLower = (goal ?? string.Empty).ToLowerInvariant();
        var hasUrl = !string.IsNullOrWhiteSpace(jobUrl);

        // Explicit Application Pipeline Intent:
        var isApplyIntent = hasUrl && (goalLower.Contains("apply") || goalLower.Contains("submit") || goalLower.Contains("application") || goalLower.Contains("pipeline"));

        if (isApplyIntent)
        {
            return new List<AkshTodoItemDto>
            {
                new() { Step = "Scrape job posting", Tool = "scrape_job", Detail = jobUrl, State = "pending" },
                new() { Step = "Tailor resume + cover + answers + queue package", Tool = "prepare_package", State = "pending" },
                new() { Step = "Independently re-verify truth", Tool = "check_truth", State = "pending" },
                new() { Step = "Supervised apply (needs approval)", Tool = "launch_apply", State = "pending" },
            };
        }

        // Resume & ATS Optimization:
        if (goalLower.Contains("resume") || goalLower.Contains("cv") || goalLower.Contains("bullet") || goalLower.Contains("ats"))
        {
            return new List<AkshTodoItemDto>
            {
                new() { Step = "Inspect Master Resume & Verified Experience", Tool = "get_master_resume", State = "pending" },
                new() { Step = "Audit ATS Keywords & Google XYZ Bullet Formulas", Tool = null, State = "pending" },
                new() { Step = "Draft Optimized Resume & Export ATS PDF", Tool = "prepare_package", State = "pending" },
            };
        }

        // Mock Interview / Prep:
        if (goalLower.Contains("interview") || goalLower.Contains("prep") || goalLower.Contains("star") || goalLower.Contains("mock"))
        {
            return new List<AkshTodoItemDto>
            {
                new() { Step = "Assess Target Role Context & Profile Experience", Tool = "get_candidate_profile", State = "pending" },
                new() { Step = "Execute Behavioral STAR & Technical Drill", Tool = null, State = "pending" },
                new() { Step = "Synthesize Performance Scorecard & Recommendations", Tool = null, State = "pending" },
            };
        }

        // Salary Negotiation:
        if (goalLower.Contains("salary") || goalLower.Contains("negotiat") || goalLower.Contains("offer") || goalLower.Contains("compensation"))
        {
            return new List<AkshTodoItemDto>
            {
                new() { Step = "Benchmark Target Role Compensation & Equity", Tool = "get_candidate_profile", State = "pending" },
                new() { Step = "Draft Counter-Offer Talking Points & Email Script", Tool = null, State = "pending" },
            };
        }

        // Profile & Application Queue Audit:
        if (goalLower.Contains("queue") || goalLower.Contains("profile") || goalLower.Contains("audit") || goalLower.Contains("status"))
        {
            return new List<AkshTodoItemDto>
            {
                new() { Step = "Inspect Candidate Profile & Screening Memory", Tool = "get_candidate_profile", State = "pending" },
                new() { Step = "Audit Application Queue & Recent Pipeline Runs", Tool = "get_application_queue", State = "pending" },
            };
        }

        // General Career Strategist:
        return new List<AkshTodoItemDto>
        {
            new() { Step = "Ground Career Context & Profile Data", Tool = "recall_memory", Detail = goal, State = "pending" },
            new() { Step = "Synthesize Actionable Career Strategy", Tool = null, State = "pending" },
        };
    }
}
