# Stops the project's MySQL 8.4 instance (port 3309) only.
#
# Deliberately targets this instance by port and defaults-file. No other
# MySQL instance on the machine (3306, 3307) is touched.

. "$PSScriptRoot\mysql-env.ps1"

if (-not (Test-MySqlListening)) {
    Write-Host "MySQL 8.4 is not running on port $script:Port."
    exit 0
}

$service = Get-Service -Name $script:ServiceName -ErrorAction SilentlyContinue
if ($service -and $service.Status -eq 'Running') {
    Write-Host "Stopping service $script:ServiceName..."
    Stop-Service -Name $script:ServiceName
} else {
    Write-Host "Requesting clean shutdown..."

    $rootPassword = $env:MYSQL_ROOT_PASSWORD
    if (-not $rootPassword) {
        $secure = Read-Host -Prompt "MySQL root password" -AsSecureString
        $rootPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
            [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
    }

    $env:MYSQL_PWD = $rootPassword
    try {
        & $script:MySqlAdm --host=127.0.0.1 --port=$script:Port --user=root shutdown
    } finally {
        Remove-Item Env:\MYSQL_PWD -ErrorAction SilentlyContinue
    }
}

for ($i = 0; $i -lt 30; $i++) {
    if (-not (Test-MySqlListening)) {
        Write-Host "MySQL 8.4 stopped." -ForegroundColor Green
        exit 0
    }
    Start-Sleep -Milliseconds 500
}

Write-Host "MySQL is still listening on port $script:Port." -ForegroundColor Red
exit 1
