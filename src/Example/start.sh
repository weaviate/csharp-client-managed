#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCKER_MODE=false
FORCE=false
RESET=false
BACKEND_PID=""
FRONTEND_PID=""

for arg in "$@"; do
    case $arg in
        --docker) DOCKER_MODE=true ;;
        --force)  FORCE=true ;;
        --reset)  RESET=true ;;
    esac
done

cleanup() {
    trap - EXIT INT TERM  # Prevent re-entry
    echo ""
    echo "Stopping..."
    if [ "$DOCKER_MODE" = "false" ]; then
        if [ -n "$BACKEND_PID" ]; then
            # Kill children first (e.g. the app process spawned by dotnet run), then the parent
            pkill -P "$BACKEND_PID" 2>/dev/null || true
            kill "$BACKEND_PID" 2>/dev/null || true
        fi
        if [ -n "$FRONTEND_PID" ]; then
            pkill -P "$FRONTEND_PID" 2>/dev/null || true
            kill "$FRONTEND_PID" 2>/dev/null || true
        fi
        docker compose -f "$SCRIPT_DIR/docker-compose.yml" down
    fi
}

# Prints info about who is listening on PORT.
# Returns 1 if the port is occupied, 0 if free.
report_port() {
    local port="$1"
    local info
    info="$(lsof -nP -i:"$port" -sTCP:LISTEN 2>/dev/null | tail -n +2 || true)"
    if [ -n "$info" ]; then
        local name pid
        name="$(echo "$info" | awk '{print $1}')"
        pid="$(echo "$info" | awk '{print $2}')"
        echo "  Port $port is in use by $name (pid $pid)"
        return 1
    fi
    return 0
}

kill_port() {
    local port="$1"
    local pids
    pids="$(lsof -ti:"$port" 2>/dev/null || true)"
    if [ -n "$pids" ]; then
        echo "$pids" | xargs kill 2>/dev/null || true
        sleep 1
    fi
}

trap cleanup EXIT INT TERM

if [ "$DOCKER_MODE" = "true" ]; then
    echo "Building backend and frontend..."
    "$SCRIPT_DIR/build-for-docker.sh"
    echo "Starting all services in Docker..."
    docker compose -f "$SCRIPT_DIR/docker-compose.dev.yml" up
else
    # Check ports before doing any heavy work
    PORT_CONFLICT=false
    report_port 5001 || PORT_CONFLICT=true
    report_port 5173 || PORT_CONFLICT=true

    if [ "$PORT_CONFLICT" = "true" ]; then
        if [ "$FORCE" = "false" ]; then
            echo ""
            echo "Run ./start.sh --force to stop conflicting processes and start fresh."
            exit 1
        fi
        echo "Stopping conflicting processes..."
        kill_port 5001
        kill_port 5173
    fi

    echo "Starting Weaviate and text2vec-transformers..."
    docker compose -f "$SCRIPT_DIR/docker-compose.yml" up -d

    echo "Waiting for Weaviate to be ready..."
    ATTEMPTS=0
    until curl -sf http://localhost:8080/v1/.well-known/ready > /dev/null 2>&1; do
        ATTEMPTS=$((ATTEMPTS + 1))
        if [ "$ATTEMPTS" -ge 60 ]; then
            echo "Weaviate did not become ready within 2 minutes. Check Docker logs."
            exit 1
        fi
        sleep 2
    done
    echo "Weaviate is ready."

    if [ ! -d "$SCRIPT_DIR/WebApp/node_modules" ]; then
        echo "Installing frontend dependencies..."
        (cd "$SCRIPT_DIR/WebApp" && npm install --silent)
    fi

    echo "Starting backend..."
    DOTNET_ENV="ASPNETCORE_URLS=http://+:5001"
    [ "$RESET" = "true" ] && DOTNET_ENV="$DOTNET_ENV Seed__Reset=true"
    (cd "$SCRIPT_DIR/WebApi" && exec env $DOTNET_ENV dotnet run) &
    BACKEND_PID=$!

    echo "Starting frontend..."
    (cd "$SCRIPT_DIR/WebApp" && exec npm run dev) &
    FRONTEND_PID=$!

    echo ""
    echo "All services running:"
    echo "  Frontend: http://localhost:5173"
    echo "  Backend:  http://localhost:5001"
    echo "  Weaviate: http://localhost:8080"
    echo ""
    echo "Press Ctrl+C to stop."

    wait "$BACKEND_PID" "$FRONTEND_PID"
fi
