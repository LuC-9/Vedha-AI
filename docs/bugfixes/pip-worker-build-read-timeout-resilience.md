# Bug Fix: Podman Worker Build Pip Read Timeout Resilience

## Problem Description
During local container stack provisioning via [build_and_start.ps1](file:///A:/AIProjects/Resumebuilder/infra/build_and_start.ps1), image compilation fails on step:
```text
RUN pip install --no-cache-dir -r requirements.txt
```
with an unhandled urllib3 exception:
```text
File "/usr/local/lib/python3.10/dist-packages/pip/_vendor/urllib3/response.py", line 443, in _error_catcher
    raise ReadTimeoutError(self._pool, None, "Read timed out.")
pip._vendor.urllib3.exceptions.ReadTimeoutError: HTTPSConnectionPool(host='files.pythonhosted.org', port=443): Read timed out.
Error: building at STEP "RUN pip install --no-cache-dir -r requirements.txt": while running runtime: exit status 2

Podman worker build failed.
At A:\AIProjects\Resumebuilder\infra\build_and_start.ps1:28 char:28
+ if ($LASTEXITCODE -ne 0) { throw "Podman worker build failed." }
+                            ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    + CategoryInfo          : OperationStopped: (Podman worker build failed.:String) [], RuntimeException
    + FullyQualifiedErrorId : Podman worker build failed.
```

---

## Root Cause Analysis
1. **Aggressive Pip Default Timeout (15s)**: By default, Python's `pip` socket connection and chunk read timeout is only 15 seconds (`--default-timeout=15`).
2. **Heavy Wheel Artifacts on PyPI**: [requirements.txt](file:///A:/AIProjects/Resumebuilder/workers/playwright-agent/requirements.txt) requires multiple multi-megabyte wheels, including `playwright`, `agentql`, `patchright`, `camoufox`, `pydantic`, `pydantic-core`, `botocore`, and `boto3`.
3. **WSL2 / Podman Virtualized Network Latency**: Under Windows Podman / WSL2 network bridge translation (gvproxy / slirp4netns), streaming large binary packages from `files.pythonhosted.org` can experience transient packet jitter or throttled throughput that exceeds the 15-second socket read window.
4. **Lack of Upgraded Packaging Tooling & Command Retries**: The base image `mcr.microsoft.com/playwright/python:v1.49.0-jammy` bundles an older pip version lacking modern streaming retry heuristics, and [Dockerfile.worker](file:///A:/AIProjects/Resumebuilder/infra/Dockerfile.worker) passed neither environment variables nor CLI arguments for timeout resilience.
5. **Fail-Fast Build Script**: In [build_and_start.ps1](file:///A:/AIProjects/Resumebuilder/infra/build_and_start.ps1), a single network hiccup during `podman build` immediately terminated the entire local orchestrator without automated retries.

---

## Proposed Fix
1. **Containerfile Pip Hardening**:
   - In [Dockerfile.worker](file:///A:/AIProjects/Resumebuilder/infra/Dockerfile.worker), set environment variables:
     ```dockerfile
     ENV PIP_DEFAULT_TIMEOUT=300
     ENV PIP_RETRIES=10
     ```
   - Upgrade packaging tools (`pip`, `setuptools`, `wheel`) prior to installing application requirements to ensure modern streaming and retry handling.
   - Explicitly supply `--default-timeout=300 --retries=10` to `RUN pip install -r requirements.txt`.
2. **Orchestrator Build Retries**:
   - In [build_and_start.ps1](file:///A:/AIProjects/Resumebuilder/infra/build_and_start.ps1), implement `Invoke-PodmanBuildWithRetry` with up to 3 attempts and an exponential backoff sleep to safeguard against transient ISP or PyPI CDN hiccups.
3. **Architecture Documentation Sync**:
   - Documented the `vedha-worker` container in [system-design.html](file:///A:/AIProjects/Resumebuilder/docs/architecture/system-design.html) with timeout specifications.

---

## Files Affected
- [infra/Dockerfile.worker](file:///A:/AIProjects/Resumebuilder/infra/Dockerfile.worker) (modified)
- [infra/build_and_start.ps1](file:///A:/AIProjects/Resumebuilder/infra/build_and_start.ps1) (modified)
- [docs/architecture/system-design.html](file:///A:/AIProjects/Resumebuilder/docs/architecture/system-design.html) (modified)
- [docs/bugfixes/pip-worker-build-read-timeout-resilience.md](file:///A:/AIProjects/Resumebuilder/docs/bugfixes/pip-worker-build-read-timeout-resilience.md) (created)

---

## Regression Risks
- **Zero Business Logic Regressions**: Package versions, API contracts, database schemas, and runtime logic remain completely untouched.
- **Improved Build Robustness**: Builds on slower broadband or congested networks will now succeed reliably instead of failing at the 15-second mark.

---

## Test Strategy & Verification
1. Verify `Dockerfile.worker` syntax: validated environment definitions and command chaining syntax.
2. Verify `build_and_start.ps1` PowerShell syntax: confirmed `Invoke-PodmanBuildWithRetry` cleanly wraps backend, frontend, and worker builds.
3. Architecture documentation updated in [system-design.html](file:///A:/AIProjects/Resumebuilder/docs/architecture/system-design.html).
