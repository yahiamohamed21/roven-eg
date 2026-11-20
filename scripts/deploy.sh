#!/bin/bash
set -e

PROJECT_ROOT="/var/www/roven-eg.online"
PUBLISH_DIR="$PROJECT_ROOT/published"
STAGING_DIR="$PROJECT_ROOT/published_staging"
LOGS_DIR="$PROJECT_ROOT/logs/deploy"
UPLOADS_DIR="$PROJECT_ROOT/uploads"
ROVEN_MEDIA_DIR="$PROJECT_ROOT/roven_media"

mkdir -p "$LOGS_DIR"
mkdir -p "$PUBLISH_DIR"
mkdir -p "$UPLOADS_DIR/banners"
mkdir -p "$UPLOADS_DIR/products"
mkdir -p "$UPLOADS_DIR/categories"
mkdir -p "$ROVEN_MEDIA_DIR/banners"
mkdir -p "$ROVEN_MEDIA_DIR/products"
mkdir -p "$ROVEN_MEDIA_DIR/categories"

LOG_FILE="$LOGS_DIR/deploy_$(date +%Y%m%d_%H%M%S).log"

log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

trap 'echo "❌ Deployment failed at line $LINENO! Attempting service recovery..."; sudo systemctl start roven-web >> "$LOG_FILE" 2>&1 || true; [ -f "$LOG_FILE" ] && tail -n 50 "$LOG_FILE"; exit 1' ERR

log "=================================================="
log "Starting ROVEN Production Deployment (roven-eg.online)"
log "=================================================="

# Step 1: Pull latest source
TARGET_BRANCH="${1:-main}"
if [ -z "$TARGET_BRANCH" ] || [ "$TARGET_BRANCH" = "HEAD" ]; then
    TARGET_BRANCH="main"
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

# Step 3: Atomic Build & Publish (Zero-Downtime Staging)
if ! dotnet --list-runtimes 2>/dev/null | grep -q "Microsoft.AspNetCore.App 9\."; then
    log "Ensuring .NET 9 ASP.NET Core runtime is installed on host..."
    sudo apt-get update -qq && sudo apt-get install -y -qq aspnetcore-runtime-9.0 2>/dev/null || true
fi

log "Cleaning staging directory..."
rm -rf "$PROJECT_ROOT/bin" "$PROJECT_ROOT/obj" "$STAGING_DIR"
mkdir -p "$STAGING_DIR"

# Ensure deploy user has write permissions to staging directory
CURRENT_USER=$(whoami)
sudo chown -R "$CURRENT_USER":www-data "$STAGING_DIR" 2>/dev/null || true
sudo chmod -R 775 "$STAGING_DIR" 2>/dev/null || true

log "Restoring NuGet packages..."
dotnet restore Stockify.csproj >> "$LOG_FILE" 2>&1

log "Publishing .NET project in Release mode to staging..."
dotnet publish Stockify.csproj -c Release -o "$STAGING_DIR" >> "$LOG_FILE" 2>&1

# Remove any build-generated uploads folder in staging to prevent symlink collision
rm -rf "$STAGING_DIR/wwwroot/uploads"

# Step 4: Swap Published Binaries with Minimal Downtime
log "Stopping roven-web service to apply published update..."
sudo systemctl stop roven-web >> "$LOG_FILE" 2>&1 || true

log "Swapping published files into production directory..."
sudo chown -R "$CURRENT_USER":www-data "$PUBLISH_DIR" 2>/dev/null || true
sudo chmod -R 775 "$PUBLISH_DIR" 2>/dev/null || true

# Explicitly remove existing uploads symlink before copying to avoid cp collision
rm -rf "$PUBLISH_DIR/wwwroot/uploads"

# Remove old files except wwwroot
find "$PUBLISH_DIR" -mindepth 1 -maxdepth 1 ! -name 'wwwroot' -exec rm -rf {} + >> "$LOG_FILE" 2>&1 || true

# Save any dynamic media uploaded to wwwroot/roven into persistent ROVEN_MEDIA_DIR
if [ -d "$PUBLISH_DIR/wwwroot/roven" ]; then
    sudo cp -rn "$PUBLISH_DIR/wwwroot/roven/"* "$ROVEN_MEDIA_DIR/" 2>/dev/null || true
fi

# In wwwroot, remove old static assets
if [ -d "$PUBLISH_DIR/wwwroot" ]; then
    find "$PUBLISH_DIR/wwwroot" -mindepth 1 -maxdepth 1 -exec rm -rf {} + >> "$LOG_FILE" 2>&1 || true
fi

# Copy newly published files into published directory
cp -a "$STAGING_DIR/." "$PUBLISH_DIR/"
rm -rf "$STAGING_DIR"

# Step 5: Preserve persistent uploads and roven media
log "Configuring persistent uploads and roven media directories..."
# Copy initial uploads if persistent folder is empty
if [ ! "$(ls -A $UPLOADS_DIR/banners 2>/dev/null)" ] && [ -d "$PROJECT_ROOT/wwwroot/uploads/banners" ]; then
    sudo cp -rn "$PROJECT_ROOT/wwwroot/uploads/banners/"* "$UPLOADS_DIR/banners/" 2>/dev/null || true
fi
if [ ! "$(ls -A $UPLOADS_DIR/products 2>/dev/null)" ] && [ -d "$PROJECT_ROOT/wwwroot/uploads/products" ]; then
    sudo cp -rn "$PROJECT_ROOT/wwwroot/uploads/products/"* "$UPLOADS_DIR/products/" 2>/dev/null || true
fi
if [ ! "$(ls -A $UPLOADS_DIR/categories 2>/dev/null)" ] && [ -d "$PROJECT_ROOT/wwwroot/uploads/categories" ]; then
    sudo cp -rn "$PROJECT_ROOT/wwwroot/uploads/categories/"* "$UPLOADS_DIR/categories/" 2>/dev/null || true
fi

# Sync repo roven assets into ROVEN_MEDIA_DIR
if [ -d "$PROJECT_ROOT/wwwroot/roven" ]; then
    sudo cp -rf "$PROJECT_ROOT/wwwroot/roven/"* "$ROVEN_MEDIA_DIR/" 2>/dev/null || true
fi

# Symlink uploads directory to published wwwroot/uploads
mkdir -p "$PUBLISH_DIR/wwwroot"
sudo rm -rf "$PUBLISH_DIR/wwwroot/uploads"
sudo ln -sfn "$UPLOADS_DIR" "$PUBLISH_DIR/wwwroot/uploads"

# Restore all persistent roven media (categories, products, banners) into published wwwroot/roven
mkdir -p "$PUBLISH_DIR/wwwroot/roven"
sudo cp -rf "$ROVEN_MEDIA_DIR/"* "$PUBLISH_DIR/wwwroot/roven/" 2>/dev/null || true

# Ensure proper permissions for www-data
sudo chown -R www-data:www-data "$PUBLISH_DIR"
sudo chown -R www-data:www-data "$UPLOADS_DIR"
sudo chown -R www-data:www-data "$ROVEN_MEDIA_DIR"
sudo chmod -R 775 "$UPLOADS_DIR"
sudo chmod -R 775 "$ROVEN_MEDIA_DIR"

# Step 6: Restart Systemd service
log "Restarting roven-web systemd service..."
sudo systemctl daemon-reload
sudo systemctl restart roven-web >> "$LOG_FILE" 2>&1

# Step 7: Health check with retry loop (waiting up to 30s for Kestrel & DB)
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

# Step 8: Storefront Verification
if curl --fail --silent --show-error --connect-timeout 5 http://127.0.0.1:5050/ > /dev/null 2>&1; then
    log "Storefront Check: PASSED (http://127.0.0.1:5050/ returned HTTP 200)"
else
    log "Storefront Check: WARNING - Home page returned non-200. Check logs."
fi

log "=================================================="
log "ROVEN Deployment Completed Successfully!"
log "=================================================="
