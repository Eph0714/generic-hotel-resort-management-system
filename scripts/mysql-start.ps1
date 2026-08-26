# Starts the project's MySQL 8.4 instance on port 3309.
#
# Prefers the Windows service if one has been installed; otherwise starts
# mysqld as a detached process.

. "$PSScriptRoot\mysql-env.ps1"

Assert-MySqlInstalled

if (Test-MySqlReady) {
    Write-Host "MySQL 8.4 is already serving on port $script:Port." -ForegroundColor Green
    exit 0
}

$service = Get-Service -Name $script:ServiceName -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "Starting service $script:ServiceName..."
    Start-Service -Name $script:ServiceName
} else {
    Write-Host "No Windows service installed; starting mysqld detached."
    Start-Process -FilePath $script:MySqlD -ArgumentList "--defaults-file=`"$script:DefaultsFile`"" -WindowStyle Hidden
}

if (Wait-MySqlReady -TimeoutSeconds 60) {
    Write-Host "MySQL 8.4 is up and serving on 127.0.0.1:$script:Port" -ForegroundColor Green
    exit 0
}

Write-Host "MySQL did not become ready within 60 seconds. Check the error log:" -ForegroundColor Red
Write-Host "  $script:ErrorLog"
exit 1
