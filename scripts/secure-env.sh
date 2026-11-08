#!/bin/bash
set -e

ENV_FILE="/etc/roven.env"
PROJECT_ROOT="/var/www/roven-eg.online"
SERVICE_FILE="/etc/systemd/system/roven-web.service"
NGINX_AVAILABLE="/etc/nginx/sites-available/roven-eg.online"
NGINX_ENABLED="/etc/nginx/sites-enabled/roven-eg.online"

echo "============================================="
echo "ROVEN Security & Environment Setup Script"
echo "============================================="

# 1. Create secure environment file if not exists
if [ ! -f "$ENV_FILE" ]; then
    echo "Creating secure environment file: $ENV_FILE"
    sudo touch "$ENV_FILE"
    sudo chmod 600 "$ENV_FILE"
    
    sudo bash -c "cat << EOF > $ENV_FILE
# --- Database Connection (Shared SQL Server 2022 Docker Container) ---
ConnectionStrings__DefaultConnection=Server=127.0.0.1,1433;Database=rovendb;User Id=sa;Password=2Y5u7YSnh#;TrustServerCertificate=True;MultipleActiveResultSets=true;

# --- ASP.NET Core Settings ---
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5050
EOF"
    echo "Secure environment file created successfully."
else
    echo "Environment file $ENV_FILE already exists."
fi

# Ensure permissions
sudo chown root:root "$ENV_FILE"
sudo chmod 600 "$ENV_FILE"

# 2. Extract DB password & ensure database exists in SQL Server container
DB_PASSWORD=$(grep "ConnectionStrings__DefaultConnection" "$ENV_FILE" | sed -n 's/.*Password=\([^;]*\);.*/\1/p')
DB_USER=$(grep "ConnectionStrings__DefaultConnection" "$ENV_FILE" | sed -n 's/.*User Id=\([^;]*\);.*/\1/p')

if [ -z "$DB_PASSWORD" ]; then
    DB_PASSWORD="2Y5u7YSnh#"
fi
if [ -z "$DB_USER" ]; then
    DB_USER="sa"
fi

echo "Verifying database 'rovendb' in sqlserver container..."
SQLCMD_PATH="/opt/mssql-tools/bin/sqlcmd"
if docker exec sqlserver ls /opt/mssql-tools18/bin/sqlcmd >/dev/null 2>&1; then
    SQLCMD_PATH="/opt/mssql-tools18/bin/sqlcmd"
fi

# Execute database existence check and creation if missing
docker exec -i sqlserver $SQLCMD_PATH -S localhost -U "$DB_USER" -P "$DB_PASSWORD" -C -Q "IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'rovendb') BEGIN CREATE DATABASE rovendb; PRINT 'Created database rovendb'; END ELSE PRINT 'Database rovendb already exists';" || {
    echo "Notice: Direct database check via sqlcmd exited. EF Core Migrate will also ensure creation on application startup."
}

# 3. Install Systemd Service if missing
if [ ! -f "$SERVICE_FILE" ]; then
    echo "Registering roven-web.service in systemd..."
    if [ -f "$PROJECT_ROOT/systemd/roven-web.service" ]; then
        sudo cp "$PROJECT_ROOT/systemd/roven-web.service" "$SERVICE_FILE"
    else
        sudo bash -c "cat << EOF > $SERVICE_FILE
[Unit]
Description=ROVEN Luxury Storefront & ERP Application (.NET 10)
After=network.target

[Service]
WorkingDirectory=$PROJECT_ROOT/published
ExecStart=/usr/bin/dotnet Stockify.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=roven-web
User=www-data
EnvironmentFile=$ENV_FILE
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5050

[Install]
WantedBy=multi-user.target
EOF"
    fi
    sudo systemctl daemon-reload
    sudo systemctl enable roven-web
    echo "Systemd service roven-web registered and enabled."
fi

# 4. Configure Nginx if site is not configured
if [ -d "/etc/nginx" ] && [ ! -f "$NGINX_AVAILABLE" ] && [ -f "$PROJECT_ROOT/nginx/roven-eg.online.conf" ]; then
    echo "Setting up Nginx configuration for roven-eg.online..."
    sudo cp "$PROJECT_ROOT/nginx/roven-eg.online.conf" "$NGINX_AVAILABLE"
    if [ ! -f "$NGINX_ENABLED" ]; then
        sudo ln -s "$NGINX_AVAILABLE" "$NGINX_ENABLED"
    fi
    sudo nginx -t && sudo systemctl reload nginx || echo "Nginx test pending SSL setup."
fi

echo "============================================="
echo "Security Setup Completed!"
echo "============================================="

