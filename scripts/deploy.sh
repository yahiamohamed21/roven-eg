#!/bin/bash
set -e

PROJECT_ROOT="/var/www/roven-eg.online"
PUBLISH_DIR="$PROJECT_ROOT/published"
LOGS_DIR="$PROJECT_ROOT/logs/deploy"
UPLOADS_DIR="$PROJECT_ROOT/uploads"

mkdir -p "$LOGS_DIR"
mkdir -p "$PUBLISH_DIR"
mkdir -p "$UPLOADS_DIR/banners"
mkdir -p "$UPLOADS_DIR/products"

LOG_FILE="$LOGS_DIR/deploy_$(date +%Y%m%d_%H%M%S).log"

log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

trap 'echo "❌ Deployment failed at line $LINENO! Attempting service recovery..."; sudo systemctl start roven-web >> "$LOG_FILE" 2>&1 || true; [ -f "$LOG_FILE" ] && tail -n 50 "$LOG_FILE"; exit 1' ERR

log "=================================================="
log "Starting ROVEN Production Deployment (roven-eg.online)"
log "=================================================="

# Step 1: Pull latest source
TARGET_BRANCH="${1:-Production}"
if [ -z "$TARGET_BRANCH" ] || [ "$TARGET_BRANCH" = "HEAD" ]; then
    TARGET_BRANCH="Production"
fi

cd "$PROJECT_ROOT"
log "Pulling latest source code from GitHub ($TARGET_BRANCH branch)..."
git reset --hard HEAD >> "$LOG_FILE" 2>&1
git fetch origin "$TARGET_BRANCH" >> "$LOG_FILE" 2>&1
git checkout -B "$TARGET_BRANCH" "origin/$TARGET_BRANCH" >> "$LOG_FILE" 2>&1 || git checkout "$TARGET_BRANCH" >> "$LOG_FILE" 2>&1
git pull origin "$TARGET_BRANCH" >> "$LOG_FILE" 2>&1
chmod +x "$PROJECT_ROOT/scripts/"*.sh

# Step 2: Ensure environment and database setup
log "Configuring environment and verifying database..."
chmod +x "$PROJECT_ROOT/scripts/secure-env.sh"
sudo "$PROJECT_ROOT/scripts/secure-env.sh" >> "$LOG_FILE" 2>&1

# Step 3: Clean, Restore & Publish .NET 10 Application
log "Stopping roven-web service before publish to release file locks..."
sudo systemctl stop roven-web >> "$LOG_FILE" 2>&1 || true

log "Cleaning old build and published binaries..."
rm -rf "$PROJECT_ROOT/bin" "$PROJECT_ROOT/obj"
find "$PUBLISH_DIR" -mindepth 1 -maxdepth 1 ! -name 'wwwroot' -exec rm -rf {} + >> "$LOG_FILE" 2>&1 || true

# Ensure deploy user has write permissions to publish directory
CURRENT_USER=$(whoami)
sudo chown -R "$CURRENT_USER":www-data "$PUBLISH_DIR" 2>/dev/null || true
sudo chmod -R 775 "$PUBLISH_DIR" 2>/dev/null || true

log "Restoring NuGet packages..."
dotnet restore Stockify.csproj >> "$LOG_FILE" 2>&1

log "Publishing .NET 10 project in Release mode..."
dotnet publish Stockify.csproj -c Release -o "$PUBLISH_DIR" >> "$LOG_FILE" 2>&1

# Step 4: Preserve persistent uploads
log "Configuring persistent uploads directory..."
# Copy initial uploads if persistent folder is empty
if [ ! "$(ls -A $UPLOADS_DIR/banners 2>/dev/null)" ] && [ -d "$PUBLISH_DIR/wwwroot/uploads/banners" ]; then
    sudo cp -rn "$PUBLISH_DIR/wwwroot/uploads/banners/"* "$UPLOADS_DIR/banners/" 2>/dev/null || true
fi
if [ ! "$(ls -A $UPLOADS_DIR/products 2>/dev/null)" ] && [ -d "$PUBLISH_DIR/wwwroot/uploads/products" ]; then
    sudo cp -rn "$PUBLISH_DIR/wwwroot/uploads/products/"* "$UPLOADS_DIR/products/" 2>/dev/null || true
fi

# Symlink uploads directory to published wwwroot/uploads
sudo rm -rf "$PUBLISH_DIR/wwwroot/uploads"
sudo ln -sfn "$UPLOADS_DIR" "$PUBLISH_DIR/wwwroot/uploads"

# Ensure proper permissions for www-data
sudo chown -R www-data:www-data "$PUBLISH_DIR"
sudo chown -R www-data:www-data "$UPLOADS_DIR"
sudo chmod -R 775 "$UPLOADS_DIR"

# Step 5: Restart Systemd service
log "Restarting roven-web systemd service..."
sudo systemctl daemon-reload
sudo systemctl restart roven-web >> "$LOG_FILE" 2>&1

# Step 6: Health check with retry loop (waiting up to 30s for Kestrel & DB)
log "Running health check on port 5050 (waiting for startup)..."
HEALTH_PASSED=false
for i in {1..15}; do
    if curl --fail --silent --show-error --connect-timeout 2 http://127.0.0.1:5050/health > /dev/null 2>&1; then
        HEALTH_PASSED=true
        break
    fi
    sleep 2
done

if [ "$HEALTH_PASSED" = true ]; then
    log "Health Check: PASSED (http://127.0.0.1:5050/health is alive)"
else
    log "Health Check: FAILED! Checking service status..."
    sudo systemctl status roven-web --no-pager >> "$LOG_FILE" 2>&1 || true
    echo "❌ Deployment failed! Recent service logs:"
    sudo journalctl -u roven-web -n 30 --no-pager
    exit 1
fi

log "=================================================="
log "ROVEN Deployment Completed Successfully!"
log "=================================================="

