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

log "=================================================="
log "Starting ROVEN Production Deployment (roven-eg.online)"
log "=================================================="

# Step 1: Pull latest source
log "Pulling latest source code from GitHub..."
cd "$PROJECT_ROOT"
git reset --hard HEAD >> "$LOG_FILE" 2>&1
git pull origin $(git rev-parse --abbrev-ref HEAD) >> "$LOG_FILE" 2>&1

# Step 2: Ensure environment and database setup
log "Configuring environment and verifying database..."
chmod +x "$PROJECT_ROOT/scripts/secure-env.sh"
sudo "$PROJECT_ROOT/scripts/secure-env.sh" >> "$LOG_FILE" 2>&1

# Step 3: Restore & Publish .NET Application
log "Restoring NuGet packages..."
dotnet restore Stockify.csproj >> "$LOG_FILE" 2>&1

log "Publishing .NET project in Release mode..."
dotnet publish Stockify.csproj -c Release -o "$PUBLISH_DIR" >> "$LOG_FILE" 2>&1

# Step 4: Preserve persistent uploads
log "Configuring persistent uploads directory..."
# Copy initial uploads if persistent folder is empty
if [ ! "$(ls -A $UPLOADS_DIR/banners 2>/dev/null)" ] && [ -d "$PUBLISH_DIR/wwwroot/uploads/banners" ]; then
    cp -rn "$PUBLISH_DIR/wwwroot/uploads/banners/"* "$UPLOADS_DIR/banners/" 2>/dev/null || true
fi
if [ ! "$(ls -A $UPLOADS_DIR/products 2>/dev/null)" ] && [ -d "$PUBLISH_DIR/wwwroot/uploads/products" ]; then
    cp -rn "$PUBLISH_DIR/wwwroot/uploads/products/"* "$UPLOADS_DIR/products/" 2>/dev/null || true
fi

# Symlink uploads directory to published wwwroot/uploads
rm -rf "$PUBLISH_DIR/wwwroot/uploads"
ln -sfn "$UPLOADS_DIR" "$PUBLISH_DIR/wwwroot/uploads"

# Ensure proper permissions for www-data
sudo chown -R www-data:www-data "$PUBLISH_DIR"
sudo chown -R www-data:www-data "$UPLOADS_DIR"
sudo chmod -R 775 "$UPLOADS_DIR"

# Step 5: Restart Systemd service
log "Restarting roven-web systemd service..."
sudo systemctl daemon-reload
sudo systemctl restart roven-web >> "$LOG_FILE" 2>&1
sleep 5

# Step 6: Health check
log "Running health check on port 5050..."
if curl --fail --silent --show-error --connect-timeout 10 http://127.0.0.1:5050/health > /dev/null 2>&1; then
    log "Health Check: PASSED (http://127.0.0.1:5050/health is alive)"
else
    log "Health Check: FAILED! Checking service status..."
    sudo systemctl status roven-web --no-pager >> "$LOG_FILE" 2>&1 || true
    exit 1
fi

log "=================================================="
log "ROVEN Deployment Completed Successfully!"
log "=================================================="

