#!/bin/bash
set -e

PROJECT_ROOT="/var/www/roven-eg.online"
BACKUP_DIR="$PROJECT_ROOT/backups"

usage() {
    echo "Usage: $0 <RovenEG_backup.bak> <uploads_backup.tar.gz>"
    echo "Example: $0 $BACKUP_DIR/database/RovenEG_backup_20261108_020000.bak $BACKUP_DIR/uploads/uploads_backup_20261108_020000.tar.gz"
    exit 1
}

if [ "$#" -ne 2 ]; then
    usage
fi

DB_BACKUP_FILE="$1"
UPLOADS_BACKUP_FILE="$2"

if [ ! -f "$DB_BACKUP_FILE" ]; then
    echo "Error: Database backup file does not exist: $DB_BACKUP_FILE"
    exit 1
fi

if [ ! -f "$UPLOADS_BACKUP_FILE" ]; then
    echo "Error: Uploads backup file does not exist: $UPLOADS_BACKUP_FILE"
    exit 1
fi

ENV_FILE="/etc/roven.env"
if [ -f "$ENV_FILE" ]; then
    DB_PASSWORD=$(grep "ConnectionStrings__DefaultConnection" "$ENV_FILE" | sed -n 's/.*Password=\([^;]*\);.*/\1/p')
    DB_USER=$(grep "ConnectionStrings__DefaultConnection" "$ENV_FILE" | sed -n 's/.*User Id=\([^;]*\);.*/\1/p')
    DB_NAME=$(grep "ConnectionStrings__DefaultConnection" "$ENV_FILE" | sed -n 's/.*Database=\([^;]*\);.*/\1/p')
fi

DB_PASSWORD=${DB_PASSWORD:-"sCdADns@s123s"}
DB_USER=${DB_USER:-"RovenEGAdmin"}
DB_NAME=${DB_NAME:-"RovenEG"}

echo "=================================================="
echo "Starting ROVEN Disaster Recovery Restoration"
echo "=================================================="

# Step 1: Stop roven-web service
echo "Stopping roven-web service during restore..."
sudo systemctl stop roven-web || true

# Step 2: Restore Database
echo "Restoring database '$DB_NAME' from $DB_BACKUP_FILE..."
SQLCMD_PATH="/opt/mssql-tools/bin/sqlcmd"
if docker exec sqlserver ls /opt/mssql-tools18/bin/sqlcmd >/dev/null 2>&1; then
    SQLCMD_PATH="/opt/mssql-tools18/bin/sqlcmd"
fi

TEMP_BAK_FILE="/var/opt/mssql/${DB_NAME}_restore.bak"
docker cp "$DB_BACKUP_FILE" sqlserver:$TEMP_BAK_FILE

docker exec -i sqlserver $SQLCMD_PATH -S localhost -U "$DB_USER" -P "$DB_PASSWORD" -C -Q "
ALTER DATABASE [$DB_NAME] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [$DB_NAME] FROM DISK = '$TEMP_BAK_FILE' WITH REPLACE;
ALTER DATABASE [$DB_NAME] SET MULTI_USER;"

docker exec -i sqlserver rm -f $TEMP_BAK_FILE
echo "Database restoration completed successfully."

# Step 3: Restore Uploads
echo "Restoring uploads directory..."
rm -rf "$PROJECT_ROOT/uploads"
tar -xzf "$UPLOADS_BACKUP_FILE" -C "$PROJECT_ROOT"
sudo chown -R www-data:www-data "$PROJECT_ROOT/uploads"
sudo chmod -R 775 "$PROJECT_ROOT/uploads"
echo "Uploads restored successfully."

# Step 4: Restart Service
echo "Restarting roven-web service..."
sudo systemctl start roven-web
echo "=================================================="
echo "Restoration Completed Successfully!"
echo "=================================================="

