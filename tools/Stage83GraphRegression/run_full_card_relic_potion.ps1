$ErrorActionPreference = 'Stop'

$repoRoot = 'F:\sts2_mod\mod_projects\STS2_editor'
$runnerExe = Join-Path $repoRoot 'tools\Stage83GraphRegression\bin\Debug\net9.0\Stage83GraphRegression.exe'
$logDir = Join-Path $repoRoot 'coverage\graph-regression\logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$logPath = Join-Path $logDir "full_card_relic_potion_$stamp.log"
$latestPath = Join-Path $logDir 'full_card_relic_potion_latest.txt'
Set-Content -Path $latestPath -Value $logPath

function Write-Log($message) {
  $line = "[{0}] {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $message
  $line | Tee-Object -FilePath $logPath -Append
}

function Run-Batch($kind) {
  Write-Log "START batch kind=$kind"
  $stdoutPath = Join-Path $logDir ("{0}_{1}_stdout.log" -f $kind.ToLowerInvariant(), $stamp)
  $stderrPath = Join-Path $logDir ("{0}_{1}_stderr.log" -f $kind.ToLowerInvariant(), $stamp)
  $proc = Start-Process -FilePath $runnerExe `
    -ArgumentList '--run-batch', $kind, $repoRoot `
    -PassThru -Wait `
    -RedirectStandardOutput $stdoutPath `
    -RedirectStandardError $stderrPath

  if (Test-Path $stdoutPath) {
    Get-Content $stdoutPath | Tee-Object -FilePath $logPath -Append | Out-Null
  }
  if (Test-Path $stderrPath) {
    Get-Content $stderrPath | Tee-Object -FilePath $logPath -Append | Out-Null
  }

  if ($proc.ExitCode -ne 0) {
    Write-Log "FAIL batch kind=$kind exit=$($proc.ExitCode)"
    throw "Batch $kind failed with exit code $($proc.ExitCode)"
  }

  Write-Log "END batch kind=$kind"
}

Write-Log "BEGIN full Card/Relic/Potion regression"
Run-Batch 'Card'
Run-Batch 'Relic'
Run-Batch 'Potion'
Write-Log 'DONE full Card/Relic/Potion regression'
