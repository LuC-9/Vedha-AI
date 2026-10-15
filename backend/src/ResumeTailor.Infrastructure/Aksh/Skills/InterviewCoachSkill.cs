using ResumeTailor.Domain.Entities;

namespace ResumeTailor.Infrastructure.Aksh.Skills;

public class InterviewCoachSkill : IAkshSkill
{
    public string Id => "interview-coach";
    public string DisplayName => "Behavioral & Technical Interview Coach";
    public string Description => "Simulates mock interviews, structures answers with STAR format, and coaches on technical & system design questions.";
    public IReadOnlyList<string> RequiredTools => new[] { AkshTools.GetMasterResume, AkshTools.RecallMemory };

    private static readonly string[] TriggerKeywords =
    [
        "interview", "star", "behavioral", "mock interview", "system design",
        "tell me about yourself", "leadership principles", "coding question",
        "hiring manager", "questions to ask", "technical round"
    ];

    public bool CanHandle(string message, AkshSession session)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        var lower = message.ToLowerInvariant();
        return TriggerKeywords.Any(k => lower.Contains(k));
    }

    public string GetInstructions(AkshSession session)
    {
        return
            "SKILL ACTIVE: Behavioral & Technical Interview Coach\n" +
            "Specialized Directives:\n" +
            "1. Ground interview answers in the candidate's real master resume (`get_master_resume`) and screening memories (`recall_memory`).\n" +
            "2. For behavioral questions, format answers strictly in the STAR framework:\n" +
            "   - **Situation**: Context, scale, and problem.\n" +
            "   - **Task**: The specific responsibility and objective assigned.\n" +
            "   - **Action**: Detailed steps taken, technologies utilized, decisions made.\n" +
            "   - **Result**: Measurable outcomes, learnings, and production impact.\n" +
            "3. For system design or technical questions, provide clear architectural trade-offs, scalability considerations, bottlenecks, and failure modes.\n" +
            "4. Be an encouraging, rigorous coach: highlight strengths, pinpoint ambiguities or missing metrics, and provide polished sample responses.";
    }
}
