# Shared configuration for the MySQL 8.4 helper scripts.
#
# This project runs its own MySQL 8.4 instance on port 3309. Port 3306 belongs
# to a separate MySQL 5.6 installation (TAM-AN FMS) and port 3307 belongs to the
# TWINS SYSTEM project's MySQL 8.4 instance - nothing in these scripts touches
# either of those.

$script:MySqlBase = "C:\Program Files\MySQL\MySQL Server 8.4"
$script:MySqlD    = Join-Path $MySqlBase "bin\mysqld.exe"
$script:MySqlCli  = Join-Path $MySqlBase "bin\mysql.exe"
$script:MySqlAdm  = Join-Path $MySqlBase "bin\mysqladmin.exe"

$script:InstanceDir = Join-Path $env:LOCALAPPDATA "GenericHotelResortMS\mysql84"
$script:DefaultsFile = Join-Path $InstanceDir "my.ini"
$script:DataDir      = Join-Path $InstanceDir "data"
$script:ErrorLog     = Join-Path $InstanceDir "mysql84-error.log"

$script:Port = 3309
$script:ServiceName = "MySQL84HotelResortMS"

function Test-MySqlListening {
    $conn = Get-NetTCPConnection -LocalPort $script:Port -State Listen -ErrorAction SilentlyContinue
    return $null -ne $conn
}

# Whether the server will actually answer a query.
#
# Not the same question as whether the port is open, and the difference is not
# academic: mysqld binds the socket several seconds before it finishes starting,
# so anything that treats "listening" as "ready" races it.
function Test-MySqlReady {
    if (-not (Test-MySqlListening)) { return $false }

    & $script:MySqlAdm --protocol=TCP --host=127.0.0.1 --port=$script:Port `
        --user=root --password=$env:MYSQL_ROOT_PASSWORD ping 2>&1 | Out-Null

    # A refused *authentication* still proves the server is answering, which is
    # all this needs to establish. Only a connection failure means "not ready".
    return $LASTEXITCODE -eq 0 -or $LASTEXITCODE -eq 1
}

function Wait-MySqlReady {
    param([int]$TimeoutSeconds = 60)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-MySqlReady) { return $true }
        Start-Sleep -Milliseconds 500
    }
    return $false
}

function Assert-MySqlInstalled {
    if (-not (Test-Path $script:MySqlD)) {
        throw "mysqld.exe not found at $script:MySqlD. Install MySQL Server 8.4 or update scripts/mysql-env.ps1."
    }
    if (-not (Test-Path $script:DataDir)) {
        throw "MySQL data directory not found at $script:DataDir. Run scripts/mysql-init.ps1 first."
    }
}
