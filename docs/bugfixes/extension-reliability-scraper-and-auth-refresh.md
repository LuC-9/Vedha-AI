# Bug Fix Plan: Extension Reliability, Portal Scraper, Dynamic Timeout & Auth Token Refresh

## Classification
- **Type**: Bug Fix & Enhancement
- **Target**: Chrome Extension (MV3) & Platform Integration
- **Status**: Completed / Verified

---

## 1. Problem Statement & Root Cause

### Issue A: Premature Message Timeout on Multi-Step Form Fills
- **Symptom**: Triggering autonomous form filling or Easy Apply from the extension popup frequently rejected with `"Timed out waiting for this page. Career tools stay available."` even though the background content script was still filling fields.
- **Root Cause**: `sendMessageToActiveTab` in [`extension/popup.js`](file:///A:/AIProjects/Resumebuilder/extension/popup.js) had a hardcoded `TAB_MESSAGE_TIMEOUT_MS = 2500` (2.5 seconds). Multi-step form completion (`AUTONOMOUS_MULTI_STEP_FILL`), LinkedIn Easy Apply, or deep form filling with human-like keystroke intervals naturally take 5 to 30 seconds.
- **Resolution**: Introduce dynamic timeout resolution: long-running actions (`AUTONOMOUS_MULTI_STEP_FILL`, `AUTO_APPLY_LINKEDIN`, `SAFE_FILL_FORM`, `TAILOR_JOB`) receive a 60,000ms window, while brief operational pings and extractions retain responsive 3,000ms budgets.

### Issue B: Primary Button DOM Clobbering
- **Symptom**: Clicking `#autoFillPrimaryBtn` in the popup permanently cleared the lightning bolt icon `<span>⚡</span>` and destroyed child element `#autoFillBtnLabel`. The button was left with raw text and could not reset properly.
- **Root Cause**: `autoFillPrimaryBtn.innerText = "Auto-filling application..."` directly assigned to the button's `innerText`, replacing its entire child node tree. On completion, `autoFillBtnLabel` was detached from the DOM, causing subsequent label restoration to fail silently.
- **Resolution**: Guard and update `autoFillBtnLabel.innerText` specifically, preserving button structure, styling, and icon spans.

### Issue C: Premature Logout on Expired JWT without Token Refresh
- **Symptom**: When a user's access token expired, `checkAuthSession()` immediately dropped user session storage and forced a login redirect, requiring manual re-authentication.
- **Root Cause**: On `401 Unauthorized` from `/api/auth/me`, `popup.js` did not attempt token refresh using the backend's existing `POST /api/auth/refresh` endpoint. In addition, `AUTH_STORAGE_KEYS` in [`extension/background.js`](file:///A:/AIProjects/Resumebuilder/extension/background.js) was missing `vedha_refresh_token` and `refreshToken`.
- **Resolution**: Added `attemptTokenRefresh()` to call `POST /api/auth/refresh` with `{ accessToken, refreshToken }` upon encountering 401s, rotating tokens automatically in `chrome.storage.local`. Expanded `AUTH_STORAGE_KEYS` across the extension ecosystem.

### Issue D: Missing Dedicated Side Panel Button in Popup Header
- **Symptom**: Users operating in popup mode had no direct button to dock the assistant into Chrome's persistent native Side Panel.
- **Root Cause**: While `chrome.sidePanel` APIs and message handlers existed in [`extension/background.js`](file:///A:/AIProjects/Resumebuilder/extension/background.js), the [`extension/popup.html`](file:///A:/AIProjects/Resumebuilder/extension/popup.html) header lacked an explicit trigger button.
- **Resolution**: Added `#openSidePanelBtn` in the header actions next to the Pin button, invoking `chrome.sidePanel.open()` with fallback messaging.

### Issue E: Job Details Scraper Omission of Modern Portals
- **Symptom**: Modern LinkedIn job posts (using `#job-details` and `.jobs-description-content__text`) and Workday portals were parsed with generic fallbacks or incomplete metadata.
- **Root Cause**: `extractJobDetails()` in [`extension/content.js`](file:///A:/AIProjects/Resumebuilder/extension/content.js) omitted Workday selectors (`data-automation-id='jobPostingHeader'`, `data-automation-id='jobPostingDescription'`) and modern LinkedIn container classes.
- **Resolution**: Updated `extractJobDetails()` with comprehensive multi-strategy selectors for LinkedIn, Workday, Greenhouse, Lever, Ashby, and Indeed, plus metadata fallbacks.

---

## 2. Affected Components & Files
1. [`extension/content.js`](file:///A:/AIProjects/Resumebuilder/extension/content.js) — Job scraper selectors & portal coverage.
2. [`extension/popup.js`](file:///A:/AIProjects/Resumebuilder/extension/popup.js) — Dynamic timeout, button DOM protection, refresh token rotation, side panel trigger.
3. [`extension/popup.html`](file:///A:/AIProjects/Resumebuilder/extension/popup.html) — Side panel button in header action toolbar.
4. [`extension/background.js`](file:///A:/AIProjects/Resumebuilder/extension/background.js) — `AUTH_STORAGE_KEYS` sync with `vedha_refresh_token`, safe `setPopup`.
5. [`tests/e2e/harness_env.js`](file:///A:/AIProjects/Resumebuilder/tests/e2e/harness_env.js) — Export `extractJobDetails` for test suite verification.

---

## 3. Regression Risks & Test Strategy
- **Regression Risks**: Low. All changes preserve existing message formats and event models while widening timeouts and selector fallback chains.
- **Test Strategy**:
  - Run the full 148-test E2E suite (`tests/e2e/runner.js`).
  - Run the complete backend test suite (`dotnet test backend/ResumeTailor.sln`).
