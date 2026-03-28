$ErrorActionPreference = 'Stop'

$gameExe = 'F:\SteamLibrary\steamapps\common\Slay the Spire 2\SlayTheSpire2.exe'
Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Process -FilePath $gameExe | Out-Null

function Wait-Url($url, $name, $timeoutSec = 90) {
  $deadline = (Get-Date).AddSeconds($timeoutSec)
  while ((Get-Date) -lt $deadline) {
    try {
      Invoke-RestMethod -Uri $url -Method Get | Out-Null
      Write-Host "READY $name"
      return
    } catch {
      Start-Sleep -Milliseconds 750
    }
  }

  throw "Timeout waiting for $name"
}

function Post-Json($url, $obj) {
  Invoke-RestMethod -Uri $url -Method Post -ContentType 'application/json' -Body ($obj | ConvertTo-Json -Depth 10)
}

Wait-Url 'http://localhost:8081/health' 'menu'
Wait-Url 'http://localhost:15526/' 'mcp'

$menu = Invoke-RestMethod 'http://localhost:8081/api/v1/menu'
$deadline = (Get-Date).AddSeconds(60)
while ($menu.screen -ne 'MAIN_MENU' -and (Get-Date) -lt $deadline) {
  Write-Host "MENU waiting screen=$($menu.screen)"
  Start-Sleep -Milliseconds 750
  $menu = Invoke-RestMethod 'http://localhost:8081/api/v1/menu'
}
Write-Host "MENU screen=$($menu.screen) actions=$([string]::Join(',', $menu.available_actions))"
if ($menu.screen -ne 'MAIN_MENU') { throw "Not on main menu" }

Post-Json 'http://localhost:8081/api/v1/menu' @{ action = 'open_character_select' } | Out-Null
Start-Sleep -Milliseconds 500
$menu = Invoke-RestMethod 'http://localhost:8081/api/v1/menu'
$char = $menu.characters | Where-Object { $_.character_id -eq 'IRONCLAD' } | Select-Object -First 1
Post-Json 'http://localhost:8081/api/v1/menu' @{ action = 'select_character'; option_index = $char.index } | Out-Null
Post-Json 'http://localhost:8081/api/v1/menu' @{ action = 'embark' } | Out-Null

$deadline = (Get-Date).AddSeconds(60)
while ((Get-Date) -lt $deadline) {
  try {
    $state = Invoke-RestMethod 'http://localhost:15526/api/v1/singleplayer?format=json'
    Write-Host "STATE $($state.state_type)"
    if ($state.state_type -ne 'menu' -and $state.state_type -ne 'unknown') {
      break
    }
  } catch {
  }

  Start-Sleep -Milliseconds 750
}

$menu = Invoke-RestMethod 'http://localhost:8081/api/v1/menu'
Write-Host "INGAME screen=$($menu.screen) actions=$([string]::Join(',', $menu.available_actions))"
$resp = Post-Json 'http://localhost:8081/api/v1/menu' @{ action = 'force_enter_encounter'; data = @{ encounter_id = 'SHRINKER_BEETLE_WEAK' } }
Write-Host ('FORCE ' + ($resp | ConvertTo-Json -Depth 8 -Compress))

for ($i = 0; $i -lt 30; $i++) {
  try {
    $state = Invoke-RestMethod 'http://localhost:15526/api/v1/singleplayer?format=json'
    Write-Host "AFTER[$i] $($state.state_type)"
  } catch {
    Write-Host "AFTER[$i] ERR $($_.Exception.Message)"
  }

  Start-Sleep -Milliseconds 1000
}
