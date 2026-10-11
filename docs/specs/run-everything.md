# Feature Specification: One-command full stack

## Overview
Developers need one command that builds and starts every Vedha AI service. Windows already has `infra/build_and_start.ps1`. Linux and macOS only had `docker compose up`, and the WSL script `infra/start_services.sh` skips the crawler and reads a hardcoded Windows path.

## Business Goal
A new machine can run the product with `./run.sh` after copying `infra/.env`.

## User Stories
As a developer, I want one script to build and start the database, cache, queue, object storage, crawler, API, worker, and web app, so that I do not start each container by hand.

## Acceptance Criteria
- `./run.sh` builds images and starts all eight compose services.
- `./run.sh --no-build` starts existing images.
- `./run.sh down` stops containers and keeps database volumes.
- `./run.sh down --volumes` deletes those volumes.
- `./run.sh logs` and `./run.sh ps` inspect the stack.
- The script uses Docker Compose when it is installed, and Podman Compose otherwise.
- `infra/.env` is created from `infra/.env.example` when it is missing, and it is never committed.

## Functional Requirements
- Services: Postgres, Redis, NATS, MinIO, Crawl4AI, backend, Playwright worker, frontend.
- Health wait: `http://localhost:5000/health` and `http://localhost:3000/`.
- CRLF line endings in `infra/.env` are stripped before Compose reads the file.

## Non-functional Requirements
- The script does not print secret values.
- Failure to find a container engine exits with a clear message.

## Architecture
`run.sh` is a host entrypoint. It does not change service boundaries. Compose remains the source of container topology in `docker-compose.yml`.

## API Changes
None.

## Database Changes
None. Existing named volumes are reused.

## UI Changes
None.

## Edge Cases
- Missing container engine.
- Missing `infra/.env` on first run.
- Windows-edited `.env` files with carriage returns.
- Crawler health can lag behind the API. The script still prints URLs if the crawler check times out.

## Risks
Image builds need network access the first time. A port already taken by another process fails the Compose start.

## Future Extensions
A `--test` flag could run the .NET suite and the extension harness after the stack is healthy.

## Changelog
### 2026-10-11T05:55:00Z
- **Changes Made**: Added `./run.sh` as the single host command for the full Compose stack.
- **Rationale**: Starting the product required a Windows Podman script or a partial shell script that omitted the crawler.
- **Impacted Components**: Host entrypoint, README.
