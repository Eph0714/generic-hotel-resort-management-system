# One-time initialization of the project's MySQL 8.4 instance on Windows.
#
# Creates a data directory under the user profile (no elevation required),
# writes my.ini, starts the server on port 3309, and provisions the application
# databases and user. Safe to re-run: it stops if the data directory exists.
#
# Nothing here touches any other MySQL installation on the machine.

. "$PSScriptRoot\mysql-env.ps1"

if (-not (Test-Path $script:MySqlD)) {
    Write-Host "mysqld.exe not found at $script:MySqlD" -ForegroundColor Red
    Write-Host "Install MySQL Server 8.4, or edit scripts/mysql-env.ps1 to point at your install."
    exit 1
}

if (Test-Path $script:DataDir) {
    Write-Host "Data directory already exists: $script:DataDir" -ForegroundColor Yellow
    Write-Host "Nothing to do. Use scripts/mysql-start.ps1 to start the server."
    exit 0
}

if (Test-MySqlListening) {
    Write-Host "Port $script:Port is already in use. Free it or change the port in mysql-env.ps1 and my.ini." -ForegroundColor Red
    exit 1
}

New-Item -ItemType Directory -Force -Path $script:InstanceDir | Out-Null

$iniTemplate = Join-Path $PSScriptRoot "mysql-my.ini.template"
if (-not (Test-Path $script:DefaultsFile)) {
    if (-not (Test-Path $iniTemplate)) {
        Write-Host "Missing $iniTemplate" -ForegroundColor Red
        exit 1
    }
    $iniContent = (Get-Content $iniTemplate -Raw).
        Replace('{{BASEDIR}}', $script:MySqlBase.Replace('\', '/')).
        Replace('{{INSTANCEDIR}}', $script:InstanceDir.Replace('\', '/')).
        Replace('{{PORT}}', $script:Port)
    # mysqld's config parser chokes on a UTF-8 BOM ("option without preceding
    # group" at line 1), so write plain UTF-8 without BOM explicitly.
    [System.IO.File]::WriteAllText($script:DefaultsFile, $iniContent, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "Wrote $script:DefaultsFile"
}

Write-Host "Initializing data directory..."
& $script:MySqlD --defaults-file="$script:DefaultsFile" --initialize-insecure --console
if ($LASTEXITCODE -ne 0) {
    Write-Host "Initialization failed (exit $LASTEXITCODE). See $script:ErrorLog" -ForegroundColor Red
    exit 1
}

Write-Host "Starting server..."
Start-Process -FilePath $script:MySqlD -ArgumentList "--defaults-file=`"$script:DefaultsFile`"" -WindowStyle Hidden

$up = $false
for ($i = 0; $i -lt 40; $i++) {
    if (Test-MySqlListening) { $up = $true; break }
    Start-Sleep -Milliseconds 500
}
if (-not $up) {
    Write-Host "Server did not start. See $script:ErrorLog" -ForegroundColor Red
    exit 1
}

# --initialize-insecure leaves root with an empty password, so this first
# connection needs none. Passwords are set below, before anything else can
# reach the server (it is bound to 127.0.0.1 only).
Write-Host "Provisioning databases and application user..."

$appPassword  = if ($env:HOTELMS_DB_PASSWORD)  { $env:HOTELMS_DB_PASSWORD }  else { 'HotelMsApp!2026' }
$rootPassword = if ($env:MYSQL_ROOT_PASSWORD)  { $env:MYSQL_ROOT_PASSWORD } else { 'HotelMsRoot!2026' }

$sql = @"
CREATE DATABASE IF NOT EXISTS hotel_resort_ms CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER IF NOT EXISTS 'hotelms_app'@'localhost' IDENTIFIED BY '$appPassword';
CREATE USER IF NOT EXISTS 'hotelms_app'@'127.0.0.1' IDENTIFIED BY '$appPassword';
GRANT ALL PRIVILEGES ON hotel_resort_ms.* TO 'hotelms_app'@'localhost';
GRANT ALL PRIVILEGES ON hotel_resort_ms.* TO 'hotelms_app'@'127.0.0.1';
FLUSH PRIVILEGES;
ALTER USER 'root'@'localhost' IDENTIFIED BY '$rootPassword';
"@

$sql | & $script:MySqlCli --host=127.0.0.1 --port=$script:Port --user=root --skip-password
if ($LASTEXITCODE -ne 0) {
    Write-Host "Provisioning failed (exit $LASTEXITCODE)." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "MySQL 8.4 ready on 127.0.0.1:$script:Port" -ForegroundColor Green
Write-Host "Database: hotel_resort_ms   User: hotelms_app"
Write-Host "Remember to URL-encode special characters in the password if used in a connection URL (! becomes %21)."
