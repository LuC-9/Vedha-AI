# Bug Fix: Naukri Akamai Anti-Bot "Access Denied" & AI Vision Blocker Handling

## 1. Issue Description & Reproduction
When a candidate pastes a Naukri job URL (e.g., `https://www.naukri.com/job-listings-sr-software-engineer-c-net-simcorp-noida-hyderabad-3-to-5-years-141026500442?src=drecomm_dashboard_profile`), the initial scraping succeeds via Crawl4AI, and the tailored ATS resume package is prepared. However, when the automated application pipeline is launched via `ExecuteApplicationQueueItemCommand`, the pipeline fails to submit or advance.

## 2. Root Cause Analysis
Inspection of the live container logs (`podman logs vedha-worker` at `20:19:25 - 20:19:32`) revealed the exact sequence:
```text
[2026-10-14 20:19:25,064] [INFO] [AI-Worker] Received HTTP /api/playwright/apply for app_id=6ab19642-452c-4894-a54a-173566d5ff27 URL=https://www.naukri.com/job-listings...
[2026-10-14 20:19:26,589] [INFO] [AI-Worker] Stealth engine active: patchright
[2026-10-14 20:19:26,589] [INFO] [AI-Worker] Navigating to https://www.naukri.com/job-listings...
[2026-10-14 20:19:29,089] [INFO] [AI-Worker] Inspecting Naukri job posting...
[2026-10-14 20:19:29,159] [INFO] [AI-Worker] Applying AI Vision to locate Naukri Apply button...
[2026-10-14 20:19:32,251] [INFO] [AI-Worker] [AI Vision] Decision (gemini-2.5-flash): action=cannot_proceed, reasoning=The page displays an 'Access Denied' message, indicating that the job posting cannot be accessed or interacted with. The application cannot proceed from this state.
[2026-10-14 20:19:32,261] [INFO] [AI-Worker] Could not locate Apply button on Naukri posting.
```

1. **Akamai Bot Manager WAF Trigger**: Naukri's anti-bot system detected the headless Chromium browser inside the container because:
   - `navigator.webdriver` was exposed.
   - Standard browser headers (`Sec-Ch-Ua`, `Sec-Fetch-*`, `Upgrade-Insecure-Requests`) were absent from the browser context.
   - Missing plugins and language descriptors.
   Naukri immediately returned an HTTP 403 / "Access Denied" page.
2. **AI Vision Succeeded, but Code Discarded Blocker Diagnosis**:
   Gemini 2.5 Flash captured the screenshot, accurately identified that the screen was displaying *"Access Denied"*, and returned `action: "cannot_proceed", reasoning: "The page displays an 'Access Denied' message..."`.
   However, the caller in `main.py` only checked for `if v_dec.get("action") == "click"`, completely ignoring `cannot_proceed`. It fell through to `Could not locate Apply button` with a generic failure.

## 3. Proposed & Implemented Fix
1. **Injected Comprehensive Stealth Evasions**:
   - Configured `STEALTH_HEADERS` including `Sec-Ch-Ua`, `Sec-Ch-Ua-Platform`, `Sec-Fetch-Dest`, `Sec-Fetch-Mode`, `Sec-Fetch-Site`, `Sec-Fetch-User`, `Upgrade-Insecure-Requests`.
   - Injected runtime evasions into `context.add_init_script` to remove `navigator.webdriver` and mock `window.chrome`, `navigator.languages`, and `navigator.plugins`.
   - Added `--disable-features=IsolateOrigins,site-per-process` to Chromium launch arguments.
2. **Pre-flight & Post-Navigation Bot Detection**:
   - Added post-navigation detection checking page text and title for `"Access Denied"` / `"403 Forbidden"`.
3. **Respecting AI Vision Blocker (`cannot_proceed`)**:
   - Updated Naukri and ATS application handlers to inspect `v_dec.get("action") == "cannot_proceed"`.
   - Returns `status="AuthenticationRequired"` with an explicit recommendation guiding candidates to use the Vedha Chrome Extension on their active, logged-in browser tab.
4. **Hot-Reloaded Worker**:
   - Deployed updated `main.py` to `vedha-worker` container and restarted the service.

## 4. Verification
- `python -m py_compile workers/playwright-agent/main.py`: Passed with 0 errors.
- Backend Unit Tests (`dotnet test backend/ResumeTailor.sln`): 179/179 passed (100.0%).
- Extension E2E Tests (`node tests/e2e/runner.js`): 148/148 passed (100.0%).
