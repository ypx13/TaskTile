Stop-Process -Name SystemSettings -Force -ErrorAction SilentlyContinue
Remove-Item 'C:\Users\yassinh\.gemini\TaskTile\diag.log' -ErrorAction SilentlyContinue
Start-Process 'ms-settings:personalization'
$p = $null
for ($i = 0; $i -lt 30; $i++) {
    $p = Get-Process -Name SystemSettings -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p) { break }
    Start-Sleep -Milliseconds 200
}
if ($p) {
    Write-Host "Found PID $($p.Id). Waiting 2s for UI..."
    Start-Sleep -Seconds 2
    & 'C:\Users\yassinh\.gemini\TaskTile\inject.ps1' 'C:\Users\yassinh\.gemini\TaskTile\SettingsCardBridge.dll'
} else {
    Write-Host "Timeout waiting for SystemSettings"
}
