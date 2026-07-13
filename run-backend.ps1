$ErrorActionPreference = "Stop"
$OutputEncoding = [System.Text.UTF8Encoding]::new()
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$envFile = Join-Path $repoRoot ".env.local"
$projectFile = Join-Path $repoRoot "backend\Beauty.Api\Beauty.Api.csproj"
$logDir = Join-Path $repoRoot ".codex\logs"
$backendOutLog = Join-Path $logDir "backend-run.out.log"
$backendErrLog = Join-Path $logDir "backend-run.err.log"
$placeholderKey = "PASTE_YOUR_AQ_GEMINI_API_KEY_HERE"
$backendProcess = $null
$currentEnvSignature = ""
$watcherMutex = New-Object System.Threading.Mutex($false, "Global\HoanMakeupBeautyApiBackendWatcher")
$watcherMutexOwned = $watcherMutex.WaitOne(0, $false)

if (-not $watcherMutexOwned) {
    Write-Host "Backend watcher đang chạy ở một cửa sổ hoặc tiến trình nền khác." -ForegroundColor Yellow
    Write-Host "Không tạo backend trùng. Terminal này sẽ chuyển sang xem log backend live." -ForegroundColor Cyan

    if (-not (Test-Path $backendOutLog)) {
        New-Item -ItemType Directory -Force -Path $logDir | Out-Null
        New-Item -ItemType File -Force -Path $backendOutLog | Out-Null
    }

    Write-Host "Đang theo dõi log: $backendOutLog" -ForegroundColor Green
    Get-Content $backendOutLog -Wait -Tail 100
    exit 0
}

function Ensure-EnvFile {
    if (Test-Path $envFile) {
        return
    }

    @"
GEMINI_API_KEY=$placeholderKey
GEMINI_MODEL=gemini-3.1-flash-lite
"@ | Set-Content -Path $envFile -Encoding UTF8
}

function Load-LocalEnv {
    Get-Content $envFile | ForEach-Object {
        $line = $_.Trim()

        if ($line.Length -eq 0 -or $line.StartsWith("#")) {
            return
        }

        $parts = $line.Split("=", 2)
        if ($parts.Count -ne 2) {
            return
        }

        $name = $parts[0].Trim()
        $value = $parts[1].Trim().Trim('"').Trim("'")

        if ($name.Length -gt 0) {
            [Environment]::SetEnvironmentVariable($name, $value, "Process")
        }
    }
}

function Has-ValidGeminiKey {
    Load-LocalEnv
    return -not [string]::IsNullOrWhiteSpace($env:GEMINI_API_KEY) -and $env:GEMINI_API_KEY -ne $placeholderKey -and $env:GEMINI_API_KEY.StartsWith("AQ", [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-EnvSignature {
    if (-not (Test-Path $envFile)) {
        return ""
    }

    return (Get-Content $envFile -Raw)
}

function Stop-OldBeautyApi {
    $projectBinPath = Join-Path $repoRoot "backend\Beauty.Api\bin"
    $oldProcesses = Get-Process -Name "Beauty.Api" -ErrorAction SilentlyContinue | Where-Object {
        $_.Path -and $_.Path.StartsWith($projectBinPath, [System.StringComparison]::OrdinalIgnoreCase)
    }

    foreach ($process in $oldProcesses) {
        Write-Host "Đang tắt backend cũ PID $($process.Id)..." -ForegroundColor Yellow
        Stop-Process -Id $process.Id -Force
        Start-Sleep -Milliseconds 500
    }

    try {
        $oldDotnetRuns = Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" | Where-Object {
            $_.CommandLine -and $_.CommandLine.Contains($projectFile, [System.StringComparison]::OrdinalIgnoreCase)
        }

        foreach ($process in $oldDotnetRuns) {
            if ($null -ne $script:backendProcess -and $process.ProcessId -eq $script:backendProcess.Id) {
                continue
            }

            Write-Host "Đang tắt dotnet run backend cũ PID $($process.ProcessId)..." -ForegroundColor Yellow
            Stop-Process -Id $process.ProcessId -Force
            Start-Sleep -Milliseconds 500
        }
    }
    catch {
        Write-Host "Không quét được dotnet run cũ, tiếp tục với backend hiện tại." -ForegroundColor DarkYellow
    }
}

function Stop-BackendProcess {
    if ($null -ne $script:backendProcess -and -not $script:backendProcess.HasExited) {
        Write-Host "Đang tắt backend hiện tại PID $($script:backendProcess.Id)..." -ForegroundColor Yellow
        Stop-Process -Id $script:backendProcess.Id -Force
        Start-Sleep -Milliseconds 800
    }

    Stop-OldBeautyApi
    $script:backendProcess = $null
}

function Start-BackendProcess {
    Load-LocalEnv
    Stop-BackendProcess

    Set-Location $repoRoot
    while ($true) {
        Write-Host "Đã nhận Gemini API key. Đang chạy Beauty API tại http://127.0.0.1:5000 ..." -ForegroundColor Green
        Write-Host "Log CONTENT sẽ hiển thị bằng tiếng Việt với tiền tố [CONTENT]." -ForegroundColor Cyan
        & dotnet run --project $projectFile

        $exitCode = $LASTEXITCODE
        Write-Host "Backend đã dừng với mã $exitCode. Tự restart sau 2 giây..." -ForegroundColor Yellow
        Start-Sleep -Seconds 2
        Load-LocalEnv
    }
}

trap {
    Stop-BackendProcess
    if ($script:watcherMutexOwned -and $null -ne $script:watcherMutex) {
        $script:watcherMutex.ReleaseMutex()
        $script:watcherMutex.Dispose()
    }
    break
}

Ensure-EnvFile

while ($true) {
    if (Has-ValidGeminiKey) {
        $nextEnvSignature = Get-EnvSignature

        if ($currentEnvSignature -ne $nextEnvSignature -or $null -eq $backendProcess -or $backendProcess.HasExited) {
            $currentEnvSignature = $nextEnvSignature
            Start-BackendProcess
            Write-Host "Đang theo dõi .env.local. Nếu backend dừng, watcher sẽ tự chạy lại." -ForegroundColor Cyan
        }

        Start-Sleep -Seconds 2
        continue
    }

    Stop-BackendProcess
    $currentEnvSignature = ""

    Write-Host "Đang chờ Gemini API key..." -ForegroundColor Yellow
    Write-Host "Mở file .env.local, dán API key vào GEMINI_API_KEY, rồi bấm Ctrl + S."
    Write-Host "Sau khi bạn lưu file, backend sẽ tự chạy. Không cần gõ lại lệnh."
    Start-Sleep -Seconds 2
}
