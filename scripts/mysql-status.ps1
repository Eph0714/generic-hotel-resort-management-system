# Reports the state of the project's MySQL 8.4 instance.

. "$PSScriptRoot\mysql-env.ps1"

Write-Host "GENERIC HOTEL AND RESORT MANAGEMENT SYSTEM - MySQL 8.4 instance" -ForegroundColor Cyan
Write-Host "  defaults file : $script:DefaultsFile"
Write-Host "  data dir      : $script:DataDir"
Write-Host "  error log     : $script:ErrorLog"
Write-Host "  port          : $script:Port"

$service = Get-Service -Name $script:ServiceName -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "  service       : $($service.Name) [$($service.Status)]"
} else {
    Write-Host "  service       : not installed (runs as a detached process)"
}

if (Test-MySqlListening) {
    Write-Host "  state         : LISTENING" -ForegroundColor Green
} else {
    Write-Host "  state         : not running" -ForegroundColor Yellow
}
