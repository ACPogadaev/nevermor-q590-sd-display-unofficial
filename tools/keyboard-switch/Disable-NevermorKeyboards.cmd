@echo off
setlocal
set "NEVERMOR_ACTION=Disable"
if /i "%~1"=="--status" set "NEVERMOR_ACTION=Status"
if not "%~1"=="" if /i not "%~1"=="--status" (
    echo Usage: %~nx0 [--status]
    exit /b 2
)

rem For changes, use Run as administrator. The --status option is read-only.
rem Match four present Keyboard collections under the confirmed display's USB parent.
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -NonInteractive -Command ^
    "$ErrorActionPreference = 'Stop';" ^
    "$action = $env:NEVERMOR_ACTION;" ^
    "$vidPid = 'VID_1A2C&PID_4E84';" ^
    "function Get-UsbDeviceId([string]$instanceId) {" ^
    "    $key = 'DEVPKEY_Device_Parent';" ^
    "    $parent = (Get-PnpDeviceProperty -InstanceId $instanceId -KeyName $key).Data;" ^
    "    (Get-PnpDeviceProperty -InstanceId $parent -KeyName $key).Data;" ^
    "};" ^
    "$displayPattern = '^HID\\' + $vidPid + '&MI_01&COL07\\[^\\]+$';" ^
    "$keyboardPattern = '^HID\\' + $vidPid + '&(MI_00|MI_01&COL0[456])\\[^\\]+$';" ^
    "$display = @(Get-PnpDevice -Class HIDClass -PresentOnly | Where-Object {" ^
    "    $_.InstanceId -match $displayPattern -and" ^
    "    (Get-PnpDeviceProperty -InstanceId $_.InstanceId -KeyName 'DEVPKEY_Device_HardwareIds').Data -contains 'HID_DEVICE_UP:FF01_U:0001'" ^
    "});" ^
    "if ($display.Count -ne 1) { throw 'Connect exactly one supported Nevermor Q590-SD display.' };" ^
    "$root = Get-UsbDeviceId $display[0].InstanceId;" ^
    "$rootPattern = '^USB\\' + $vidPid + '\\[^\\]+$';" ^
    "if ($root -notmatch $rootPattern) { throw 'Could not confirm the display USB device.' };" ^
    "$keyboards = @(Get-PnpDevice -Class Keyboard -PresentOnly | Where-Object {" ^
    "    $_.InstanceId -match $keyboardPattern -and (Get-UsbDeviceId $_.InstanceId) -eq $root" ^
    "});" ^
    "$collections = @($keyboards | ForEach-Object { ($_.InstanceId -split '\\')[1] } | Sort-Object -Unique);" ^
    "if ($keyboards.Count -ne 4 -or $collections.Count -ne 4) { throw 'Could not confirm all four keyboard entries.' };" ^
    "if ($action -eq 'Disable' -and $display[0].Status -ne 'OK') { throw 'The display HID channel is not active.' };" ^
    "$instanceIds = $keyboards.InstanceId;" ^
    "$failed = $false;" ^
    "if ($action -ne 'Status') {" ^
    "    $pnpUtil = Join-Path $env:SystemRoot 'System32\pnputil.exe';" ^
    "    $verb = if ($action -eq 'Disable') { '/disable-device' } else { '/enable-device' };" ^
    "    foreach ($keyboard in $keyboards) {" ^
    "        $disabled = [string]$keyboard.ConfigManagerErrorCode -eq 'CM_PROB_DISABLED';" ^
    "        if (($action -eq 'Disable' -and $disabled) -or ($action -eq 'Enable' -and $keyboard.Status -eq 'OK')) { continue };" ^
    "        & $pnpUtil $verb $keyboard.InstanceId;" ^
    "        if ($LASTEXITCODE -ne 0) { $failed = $true; Write-Warning ('PnPUtil exit code: ' + $LASTEXITCODE) };" ^
    "    };" ^
    "};" ^
    "$states = @(Get-PnpDevice -InstanceId $instanceIds);" ^
    "$states | Format-Table Status, ConfigManagerErrorCode, InstanceId -AutoSize;" ^
    "if ($action -eq 'Status') { exit 0 };" ^
    "$pending = @($states | Where-Object {" ^
    "    if ($action -eq 'Disable') { [string]$_.ConfigManagerErrorCode -ne 'CM_PROB_DISABLED' }" ^
    "    else { $_.Status -ne 'OK' }" ^
    "});" ^
    "if ($failed -or $states.Count -ne 4 -or $pending.Count) { throw 'Windows has not confirmed the requested state for all four entries.' };" ^
    "Write-Host ('Done: ' + $action + ' confirmed for all four entries.');"

set "SWITCH_EXIT=%ERRORLEVEL%"
if /i "%NEVERMOR_ACTION%"=="Status" exit /b %SWITCH_EXIT%
if not "%SWITCH_EXIT%"=="0" (
    echo.
    echo If access was denied, right-click this file and select Run as administrator.
)
pause
exit /b %SWITCH_EXIT%
