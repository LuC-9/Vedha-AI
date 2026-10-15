# Bug Fix: Gemini Function Calling `thought_signature` Missing in Multi-Turn Tool History

## 1. Issue Description & Symptoms
In the backend logs (`podman logs vedha-backend`), an Aksh AI conversational session failed with HTTP 400 Bad Request during multi-turn function calling:
```text
[20:49:42 WRN] Gemini function-calling path failed (Gemini API error (BadRequest): {
  "error": {
    "code": 400,
    "message": "Function call is missing a thought_signature in functionCall parts. This is required for tools to work correctly, and missing thought_signature may lead to degraded model performance. Additional data, function call `default_api:todos_add` , position 3."
  }
}); no silent text fallback for tool-bound turns.
[20:49:42 WRN] Aksh model failure (reasonCode=function_calling_unavailable) for session ce95af06-8180-468b-bdaa-0e6cb59e2561
```

## 2. Root Cause Analysis
1. **Gemini Thinking Models Cryptographic Tokens**:
   When frontier reasoning models (such as `gemini-2.5-flash` or models with thinking enabled) emit tool calls, the API response attaches an opaque cryptographic verification token (`thoughtSignature` or `thought_signature`) to the response `part`.
2. **Sibling Property Mismatch in Response Parsing**:
   In `GeminiFunctionCallingClient.cs`, the parsing logic extracted `call["thoughtSignature"]` and `call["thought_signature"]`. However, in the Google Gemini REST specification, `thoughtSignature` is a **sibling property** on the `part` object containing `functionCall` (i.e. `part["thoughtSignature"]`), not inside the inner `call` object. As a result, `thoughtSignature` was parsed as null.
3. **Improper Request Serialization**:
   When multi-turn history was serialized to send the function response back to Gemini on subsequent turns, the client placed `thoughtSignature` inside the `callDict` (`{ "functionCall": { "name": "...", "thoughtSignature": "..." } }`). Gemini strictly expects `thoughtSignature` on the outer `part` object (`{ "functionCall": { "name": "..." }, "thoughtSignature": "..." }`).
4. **Validation Failure at Position 3**:
   When the Microsoft Agent Framework ReAct loop invoked `todos_add` and re-sent the history for the next turn, Gemini received `functionCall` at position 3 with no sibling `thought_signature`, causing Google's API to reject the turn with a `400 Bad Request`.

## 3. Implemented Fix
In [`backend/src/ResumeTailor.Infrastructure/Aksh/GeminiFunctionCallingClient.cs`](file:///A:/AIProjects/Resumebuilder/backend/src/ResumeTailor.Infrastructure/Aksh/GeminiFunctionCallingClient.cs):
1. **Candidate-Wide and Part-Level Extraction**:
   Updated the response parser to extract `thoughtSignature` directly from `part?["thoughtSignature"]`, `part?["thought_signature"]`, or candidate-level reasoning parts, attaching it cleanly to the call arguments.
2. **Clean Sibling Serialization**:
   Updated request serialization for `FunctionCallContent`:
   - Strips `thoughtSignature` from tool arguments (`cleanArgs`) so undeclared arguments are never sent inside `callDict["args"]`.
   - Serializes `thoughtSignature` as a sibling property directly inside the `part` dictionary (`partDict["thoughtSignature"]`).
3. **Resilient Fallback Cascade**:
   Added error inspection in the HTTP send loop: if a `400 Bad Request` occurs containing `"thought_signature"`, the client automatically logs a warning and cascades to the next model in `ModelFallbackSequence` (`gemini-2.0-flash` &rarr; `gemini-1.5-flash` &rarr; `gemini-flash-lite-latest`).
4. **Container Image Rebuilt & Hot Reloaded**:
   Rebuilt `localhost/infra-backend:latest` and restarted `vedha-backend`. Verified healthy connection to PostgreSQL and Redis.

## 4. Verification
- **Unit Tests**: `dotnet test backend/ResumeTailor.sln`: 179/179 passed (100.0%).
- **Health Check**: `http://localhost:5000/health`: HTTP 200 OK (`{"status":"Healthy","database":"Connected"}`).
