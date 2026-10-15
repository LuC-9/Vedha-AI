using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ResumeTailor.Infrastructure.Aksh;

/// <summary>
/// Phase 0 spike: constructs the Aksh <see cref="AIAgent"/> as a Microsoft Agent
/// Framework HarnessAgent over <see cref="AkshChatClientAdapter"/>.
/// Server-harness hardening for the spike: web search OFF (job data comes from
/// Vedha tools, not the open web) and file memory OFF (Postgres is the memory
/// of record until Phase 2 wires a per-user file store).
/// </summary>
public static class HarnessAgentFactory
{
    /// <summary>
    /// Per-turn agent: the full tool catalog with userId/sessionId bound server-side
    /// via closure, so the model can never reach another user's data or session.
    /// NOTE: the Phase-0 shared agent (model-supplied userId recall) was deleted:
    /// it was registered but never resolved in production, and its recall tool
    /// was a cross-user read primitive. No shared/model-scoped agent may exist.
    /// </summary>
    public static AIAgent CreateTurnAgent(IServiceProvider services, Guid userId, Guid sessionId)
    {
        var chatClient = services.GetRequiredService<AkshChatClientAdapter>();
        return CreateTurnAgent(services, userId, sessionId, chatClient);
    }

    /// <summary>
    /// Per-turn agent over a caller-supplied chat client — used to bind the
    /// candidate's own provider, stored API key, model, and skill-specific tools for the turn.
    /// When allowedToolNames is empty, no tools are bound, allowing pure conversational chat.
    /// </summary>
    public static AIAgent CreateTurnAgent(
        IServiceProvider services, 
        Guid userId, 
        Guid sessionId, 
        IChatClient chatClient, 
        IReadOnlyList<string>? allowedToolNames = null)
    {
        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        var fullCatalog = AkshTools.CreateCatalog(scopes, userId, sessionId);

        IReadOnlyList<AIFunction> filteredTools;
        if (allowedToolNames == null)
        {
            filteredTools = fullCatalog;
        }
        else if (allowedToolNames.Count == 0)
        {
            filteredTools = Array.Empty<AIFunction>();
        }
        else
        {
            var allowedSet = new HashSet<string>(allowedToolNames, StringComparer.OrdinalIgnoreCase);
            filteredTools = fullCatalog.Where(t => allowedSet.Contains(t.Name)).ToList();
        }

        return Build(chatClient, services, filteredTools);
    }

    private static AIAgent Build(IChatClient chatClient, IServiceProvider services, IReadOnlyList<AIFunction> tools)
    {
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();

        return ((IChatClient)chatClient).AsHarnessAgent(
            new HarnessAgentOptions
            {
                Name = "aksh",
                Description = "Vedha AI career copilot: plans and supervises job applications over Vedha tools.",
                HarnessInstructions = AkshPrompts.Harness,
                // NOTE (spike): MaxContextWindowTokens/MaxOutputTokens are experimental
                // in MAF 1.24.0 (MAAI001) — omitted here; output is bounded by
                // AI_MAX_TOKENS on the Vedha provider side plus MaximumIterationsPerRequest.
                MaximumIterationsPerRequest = 12,
                DisableWebSearch = true,
                DisableFileMemory = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = AkshPrompts.Core,
                    Tools = tools.Count > 0 ? [.. tools] : null,
                    Temperature = 0.2f,
                    // MEAI-level cap (not the experimental harness option): tool-heavy
                    // turns must not truncate mid-chain. Provider-side AI_MAX_TOKENS still applies.
                    MaxOutputTokens = 16_384,
                },
            },
            loggerFactory,
            services);
    }
}
