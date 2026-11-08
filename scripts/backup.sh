#!/bin/bash
set -e

PROJECT_ROOT="/var/www/roven-eg.online"
BACKUP_DIR="$PROJECT_ROOT/backups"
LOGS_DIR="$PROJECT_ROOT/logs/backup"

mkdir -p "$BACKUP_DIR/database"
mkdir -p "$BACKUP_DIR/uploads"
mkdir -p "$LOGS_DIR"

LOG_FILE="$LOGS_DIR/backup_$(date +%Y%m%d_%H%M%S).log"

log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

log "Starting Daily ROVEN Backup Process"

# Retrieve DB credentials
ENV_FILE="/etc/roven.env"
if [ -f "$ENV_FILE" ]; then
    DB_PASSWORD=$(grep "ConnectionStrings__DefaultConnection" "$ENV_FILE" | sed -n 's/.*Password=\([^;]*\);.*/\1/p')
    DB_USER=$(grep "ConnectionStrings__DefaultConnection" "$ENV_FILE" | sed -n 's/.*User Id=\([^;]*\);.*/\1/p')
fi

DB_PASSWORD=${DB_PASSWORD:-"2Y5u7YSnh#"}
DB_USER=${DB_USER:-"sa"}

# Step 1: SQL Server Database Backup
log "Backing up database 'rovendb' from sqlserver container..."
SQLCMD_PATH="/opt/mssql-tools/bin/sqlcmd"
if docker exec sqlserver ls /opt/mssql-tools18/bin/sqlcmd >/dev/null 2>&1; then
    SQLCMD_PATH="/opt/mssql-tools18/bin/sqlcmd"
fi

TEMP_BAK_FILE="/var/opt/mssql/rovendb_backup.bak"
docker exec -i sqlserver $SQLCMD_PATH -S localhost -U "$DB_USER" -P "$DB_PASSWORD" -C -Q "BACKUP DATABASE rovendb TO DISK = '$TEMP_BAK_FILE' WITH FORMAT, INIT;" >> "$LOG_FILE" 2>&1

BACKUP_FILE_NAME="rovendb_backup_$(date +%Y%m%d_%H%M%S).bak"
docker cp sqlserver:$TEMP_BAK_FILE "$BACKUP_DIR/database/$BACKUP_FILE_NAME" >> "$LOG_FILE" 2>&1
docker exec -i sqlserver rm -f $TEMP_BAK_FILE >> "$LOG_FILE" 2>&1

log "Database backup created: $BACKUP_FILE_NAME"

# Step 2: Backup Uploads (Banners and Products)
log "Compressing uploads directory..."
UPLOADS_BACKUP_NAME="uploads_backup_$(date +%Y%m%d_%H%M%S).tar.gz"
tar -czf "$BACKUP_DIR/uploads/$UPLOADS_BACKUP_NAME" -C "$PROJECT_ROOT" uploads >> "$LOG_FILE" 2>&1
log "Uploads backup created: $UPLOADS_BACKUP_NAME"

# Step 3: Cleanup old backups (> 30 days)
log "Cleaning up old backups (> 30 days)..."
DB_BACKUP_COUNT=$(find "$BACKUP_DIR/database" -name "*.bak" -type f | wc -l)
if [ "$DB_BACKUP_COUNT" -gt 1 ]; then
    find "$BACKUP_DIR/database" -name "*.bak" -type f -mtime +30 -delete >> "$LOG_FILE" 2>&1
fi

UPLOADS_BACKUP_COUNT=$(find "$BACKUP_DIR/uploads" -name "*.tar.gz" -type f | wc -l)
if [ "$UPLOADS_BACKUP_COUNT" -gt 1 ]; then
    find "$BACKUP_DIR/uploads" -name "*.tar.gz" -type f -mtime +30 -delete >> "$LOG_FILE" 2>&1
fi

log "Backup Process Finished Successfully."

