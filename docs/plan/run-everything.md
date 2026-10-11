# Implementation Plan: One-command full stack

## Files to create
- `run.sh`
- `docs/specs/run-everything.md`
- `docs/plan/run-everything.md`

## Files to modify
- `README.md`
- `docs/architecture/system-design.html`

## Database migrations
None.

## API changes
None.

## Configuration changes
None. The script reads `infra/.env` and creates it from `infra/.env.example` when absent.

## Testing strategy
- `bash -n run.sh`
- `./run.sh --help`

A live `./run.sh` depends on Docker or Podman and is not run in environments that do not have a container engine.

## Deployment considerations
Local development only. Production deploys still use the same `docker-compose.yml`.

## Rollback strategy
Delete `run.sh` and start with `docker compose up --build -d` from the repository root.

## Changelog
### 2026-10-11T05:55:00Z
- **Changes Made**: Implemented `run.sh` with up, down, logs, and ps.
- **Rationale**: One command should start every service the compose file defines.
- **Impacted Components**: Host entrypoint, README, system design note.
