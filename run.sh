#!/usr/bin/env bash
# Start the full Vedha AI stack: Postgres, Redis, NATS, MinIO, Crawl4AI,
# the API, the Playwright worker, and the web app.
#
# Usage:
#   ./run.sh                  build images and start everything
#   ./run.sh --no-build       start without rebuilding images
#   ./run.sh down             stop containers (keeps database volumes)
#   ./run.sh down --volumes   stop and delete database volumes
#   ./run.sh logs             follow container logs
#   ./run.sh ps               show container status
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_FILE="$ROOT/docker-compose.yml"
EXAMPLE_ENV="$ROOT/infra/.env.example"
ENV_FILE="$ROOT/infra/.env"

log() { printf '\n==> %s\n' "$*"; }
die() { printf 'error: %s\n' "$*" >&2; exit 1; }

pick_compose() {
  if command -v docker >/dev/null 2>&1 && docker compose version >/dev/null 2>&1; then
    COMPOSE=(docker compose)
  elif command -v podman >/dev/null 2>&1 && podman compose version >/dev/null 2>&1; then
    COMPOSE=(podman compose)
  elif command -v podman-compose >/dev/null 2>&1; then
    COMPOSE=(podman-compose)
  else
    die "Install Docker Compose or Podman Compose, then run ./run.sh again."
  fi
}

prepare_env() {
  if [[ ! -f "$ENV_FILE" ]]; then
    [[ -f "$EXAMPLE_ENV" ]] || die "Missing $EXAMPLE_ENV"
    cp "$EXAMPLE_ENV" "$ENV_FILE"
    log "Created infra/.env from infra/.env.example. Set GEMINI_API_KEY before tailoring resumes."
  fi
  RUNTIME_ENV="$(mktemp)"
  trap 'rm -f "$RUNTIME_ENV"' EXIT
  tr -d '\r' < "$ENV_FILE" > "$RUNTIME_ENV"
}

compose() {
  "${COMPOSE[@]}" --project-directory "$ROOT" -p vedha -f "$COMPOSE_FILE" --env-file "$RUNTIME_ENV" "$@"
}

wait_for() {
  local url="$1" name="$2" tries="${3:-90}"
  local i
  for i in $(seq 1 "$tries"); do
    if curl -fsS --max-time 3 "$url" >/dev/null 2>&1; then
      printf '    ready  %s  %s\n' "$name" "$url"
      return 0
    fi
    sleep 2
  done
  echo "    $name did not become ready: $url" >&2
  compose logs --tail 30 || true
  return 1
}

print_urls() {
  cat <<'EOF'

Vedha AI is running
  Web app          http://localhost:3000
  API health       http://localhost:5000/health
  Swagger          http://localhost:5000/swagger
  MinIO console    http://localhost:9001   (minioadmin / minioadmin)
  Crawler health   http://localhost:11235/health

Stop with:  ./run.sh down
Logs with:  ./run.sh logs
EOF
}

ACTION="up"
BUILD=1
VOLUMES=0

for arg in "$@"; do
  case "$arg" in
    up) ACTION="up" ;;
    down|stop) ACTION="down" ;;
    logs) ACTION="logs" ;;
    ps|status) ACTION="ps" ;;
    --no-build) BUILD=0 ;;
    --volumes) VOLUMES=1 ;;
    -h|--help)
      sed -n '2,11p' "$0"
      exit 0
      ;;
    *) die "Unknown argument: $arg. Run ./run.sh --help" ;;
  esac
done

pick_compose
prepare_env

case "$ACTION" in
  down)
    log "Stopping Vedha AI"
    if [[ "$VOLUMES" -eq 1 ]]; then
      compose down --volumes --remove-orphans
    else
      compose down --remove-orphans
    fi
    ;;
  logs)
    compose logs -f --tail 100
    ;;
  ps)
    compose ps
    ;;
  up)
    if [[ "$BUILD" -eq 1 ]]; then
      log "Building and starting Postgres, Redis, NATS, MinIO, Crawl4AI, API, worker, and web app"
      compose up --build -d --remove-orphans
    else
      log "Starting the stack without rebuilding images"
      compose up -d --remove-orphans
    fi
    log "Waiting for health checks"
    wait_for "http://localhost:5000/health" "API"
    wait_for "http://localhost:3000/" "Web app"
    wait_for "http://localhost:11235/health" "Crawler" 30 || true
    print_urls
    ;;
esac
