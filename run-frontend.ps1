$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$frontendDir = Join-Path $repoRoot "frontend"
$npmCmd = Join-Path $env:ProgramFiles "nodejs\npm.cmd"
$watcherMutex = New-Object System.Threading.Mutex($false, "Global\HoanMakeupBeautyFrontendWatcher")
$watcherMutexOwned = $watcherMutex.WaitOne(0, $false)

if (-not $watcherMutexOwned) {
    Write-Host "Frontend watcher da dang chay o mot cua so khac. Khong tao them tien trinh trung." -ForegroundColor Yellow
    exit 0
}

if (-not (Test-Path $frontendDir)) {
    Write-Host "Khong tim thay thu muc frontend: $frontendDir" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $npmCmd)) {
    $npmCmd = "npm.cmd"
}

trap {
    if ($script:watcherMutexOwned -and $null -ne $script:watcherMutex) {
        $script:watcherMutex.ReleaseMutex()
        $script:watcherMutex.Dispose()
    }
    break
}

Set-Location $frontendDir

while ($true) {
    Write-Host "Dang chay Beauty Frontend tai http://localhost:3000 ..." -ForegroundColor Green
    & $npmCmd run dev

    $exitCode = $LASTEXITCODE
    Write-Host "Frontend da dung voi ma $exitCode. Tu restart sau 2 giay..." -ForegroundColor Yellow
    Start-Sleep -Seconds 2
}
