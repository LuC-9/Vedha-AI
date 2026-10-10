# Bug Fix: Modal Occlusion and Popup Hang on Non-Job Pages

## Classification
Bug Fix

## Issues
- [i-m-akshat/Vedha-AI#4](https://github.com/i-m-akshat/Vedha-AI/issues/4) — application dialogs covered by the copilot dock
- [i-m-akshat/Vedha-AI#5](https://github.com/i-m-akshat/Vedha-AI/issues/5) — popup stuck detecting a job on ordinary pages
- [i-m-akshat/Vedha-AI#3](https://github.com/i-m-akshat/Vedha-AI/issues/3) — already implemented on this fork (`waitForEasyApplySurface`, chat-only `isMsgOrChatElement`, `clickElementNaturally`). No further code in this change.
- [i-m-akshat/Vedha-AI#11](https://github.com/i-m-akshat/Vedha-AI/issues/11) — left untouched.

## Root Cause
`manageModalStacking` forced `#vedha-floating-copilot-root` to `z-index: 2147483647` whenever a dialog was open and did not collapse an unpinned card. Dialogs were raised, then the dock was painted above them, so Next, Review, Submit, and Discard stayed covered.

`popup.js` awaited `GET /api/auth/me` with no timeout before painting a job, and `chrome.tabs.sendMessage` had no timeout. A stored token plus a down API, or a page with no content-script reply, left the title on "Detecting target job…". The toolbar action also had no `default_popup`, so the icon did not open `popup.html`. Profile rendering substituted "30 Days" and a visa authorization sentence when those fields were unknown.

## Proposed Fix
- Unpinned dock: remember whether the card was expanded, collapse to the pill, set `z-index: 2147482000`, restore expansion and `2147483647` when no dialog remains.
- Pinned dock (`sessionStorage vedha_dock_pinned === "true"`): stay expanded at `2147483647`.
- Order dialogs by a document walk. First dialog `2147483100`, each later dialog `+100`.
- Paint Universal Careers Mode before any await. Skip the content-script handshake on restricted and non-job URLs.
- Auth and tab messages time out at 2.5s. HTTP 401/403 clears the token and shows login. Network failure keeps the cached copilot.
- Notice, salary, and visa display "Not Specified" when the profile has no value.
- `manifest.json` `action.default_popup` is `popup.html`. The side panel path remains `popup.html`.

## Files Affected
- `extension/content.js`
- `extension/popup.js`
- `extension/popup.html`
- `extension/manifest.json`
- `tests/e2e/open_extension_issues.test.js`
- `tests/e2e/tier1_features.test.js`
- `tests/e2e/tier2_boundaries.test.js`
- `tests/e2e/tier3_pairwise.test.js`
- `tests/e2e/tier4_realworld.test.js`
- `tests/e2e/runner.js`

## Regression Risks
- Pin-over-modals is intentional and still covers buttons when the user pins. That matches the pin control copy.
- `default_popup` means the toolbar icon opens the popup. `chrome.action.onClicked` no longer runs, so the icon does not auto-open the side panel.
- Known job URLs that fail extraction fall back to the tab title inside the job card, not Universal Careers Mode.

## Test Strategy
`node tests/e2e/runner.js` — dock demotion/restore, pin override, dialog order, popup mode, and cached profile labels.

## Verification Steps
1. On a LinkedIn Easy Apply dialog with the dock unpinned and expanded, the card becomes the pill and sits under the dialog.
2. Closing the dialog restores the expanded card and the top z-index.
3. Pinning the dock keeps the card above the dialog.
4. Opening the toolbar icon on google.com shows Universal Careers Mode with Copilot, AI Match, Cover Letter, and Profile tabs enabled.
5. With the API down and a cached profile that has no notice or visa, those rows read "Not Specified".

## Changelog
- **2026-10-10 19:20:00 UTC**: Recorded the fix for dialog occlusion and the non-job popup hang. Issue #11 was not in scope. Issue #3 needed no new code.
