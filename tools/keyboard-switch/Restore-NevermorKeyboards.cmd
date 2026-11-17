@echo off
setlocal
set "NEVERMOR_ACTION=Enable"
if /i "%~1"=="--status" set "NEVERMOR_ACTION=Status"
if not "%~1"=="" if /i not "%~1"=="--status" (
    echo Usage: %~nx0 [--status]
    exit /b 2
)

rem For changes, use Run as administrator. The --status option is read-only.
rem Match four present Keyboard collections under the confirmed display's USB parent.
rem PnPUtil returns 0xE0000231 (-536870351) when it refuses to disable a critical device.
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -NonInteractive -Command ^
    "$ErrorActionPreference = 'Stop';" ^
    "$action = $env:NEVERMOR_ACTION;" ^
    "$vidPid = 'VID_1A2C&PID_4E84';" ^
    "function Get-UsbDeviceId([string]$instanceId) {" ^
    "    $key = 'DEVPKEY_Device_Parent';" ^
    "    $parent = (Get-PnpDeviceProperty -InstanceId $instanceId -KeyName $key).Data;" ^
    "    (Get-PnpDeviceProperty -InstanceId $parent -KeyName $key).Data;" ^
    "};" ^
    "function Get-KeyboardStates([string[]]$ids) {" ^
    "    foreach ($device in (Get-PnpDevice -InstanceId $ids)) {" ^
    "        $flags = (Get-PnpDeviceProperty -InstanceId $device.InstanceId -KeyName 'DEVPKEY_Device_ConfigFlags').Data;" ^
    "        if ($null -eq $flags) { throw ('Missing configuration flags: ' + $device.InstanceId) };" ^
    "        [pscustomobject]@{" ^
    "            Status = $device.Status;" ^
    "            Problem = [string]$device.ConfigManagerErrorCode;" ^
    "            DisableConfigured = ($flags -band 1) -ne 0;" ^
    "            InstanceId = $device.InstanceId" ^
    "        };" ^
    "    };" ^
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
    "$allKeyboards = @(Get-PnpDevice -Class Keyboard -PresentOnly);" ^
    "$keyboards = @($allKeyboards | Where-Object {" ^
    "    $_.InstanceId -match $keyboardPattern -and (Get-UsbDeviceId $_.InstanceId) -eq $root" ^
    "});" ^
    "$collections = @($keyboards | ForEach-Object { ($_.InstanceId -split '\\')[1] } | Sort-Object -Unique);" ^
    "if ($keyboards.Count -ne 4 -or $collections.Count -ne 4) { throw 'Could not confirm all four keyboard entries.' };" ^
    "if ($action -eq 'Disable' -and $display[0].Status -ne 'OK') { throw 'The display HID channel is not active.' };" ^
    "$otherKeyboards = @($allKeyboards | Where-Object {" ^
    "    $_.Status -eq 'OK' -and $_.InstanceId -notmatch ('^HID\\' + $vidPid + '&')" ^
    "});" ^
    "$instanceIds = $keyboards.InstanceId;" ^
    "$states = @(Get-KeyboardStates $instanceIds);" ^
    "if ($states.Count -ne 4) { throw 'Could not read the state of all four entries.' };" ^
    "$failed = $false;" ^
    "$restartRequested = @{};" ^
    "if ($action -ne 'Status') {" ^
    "    $pnpUtil = Join-Path $env:SystemRoot 'System32\pnputil.exe';" ^
    "    $verb = if ($action -eq 'Disable') { '/disable-device' } else { '/enable-device' };" ^
    "    $previousEncoding = [Console]::OutputEncoding;" ^
    "    try {" ^
    "        [Console]::OutputEncoding = [Text.Encoding]::Default;" ^
    "        foreach ($entry in $states) {" ^
    "            if ($action -eq 'Disable' -and $entry.DisableConfigured) { continue };" ^
    "            if ($action -eq 'Enable' -and -not $entry.DisableConfigured -and $entry.Status -eq 'OK') { continue };" ^
    "            $output = & $pnpUtil $verb $entry.InstanceId;" ^
    "            $exitCode = $LASTEXITCODE;" ^
    "            if ($action -eq 'Disable' -and $exitCode -eq -536870351) {" ^
    "                if ($otherKeyboards.Count -gt 0) {" ^
    "                    Write-Host ('Retrying the confirmed display entry with /force: ' + $entry.InstanceId);" ^
    "                    $output = & $pnpUtil /disable-device $entry.InstanceId /force;" ^
    "                    $exitCode = $LASTEXITCODE;" ^
    "                } else { Write-Warning 'Connect a working keyboard before retrying.' };" ^
    "            };" ^
    "            $output;" ^
    "            if ($exitCode -eq 3010) { $restartRequested[$entry.InstanceId] = $true }" ^
    "            elseif ($exitCode -ne 0) { $failed = $true; Write-Warning ('PnPUtil exit code: ' + $exitCode) };" ^
    "        };" ^
    "    } finally {" ^
    "        [Console]::OutputEncoding = $previousEncoding;" ^
    "    };" ^
    "    $states = @(Get-KeyboardStates $instanceIds);" ^
    "};" ^
    "$states | Format-Table Status, Problem, DisableConfigured, InstanceId -AutoSize;" ^
    "if ($action -eq 'Status') {" ^
    "    if (@($states | Where-Object { $_.DisableConfigured -and $_.Status -eq 'OK' }).Count) {" ^
    "        Write-Host 'Disable is configured. Restart Windows to apply it.';" ^
    "    };" ^
    "    exit 0;" ^
    "};" ^
    "if ($action -eq 'Disable' -and (Get-PnpDevice -InstanceId $display[0].InstanceId).Status -ne 'OK') {" ^
    "    throw 'Windows did not confirm the display HID channel. Run Restore-NevermorKeyboards.cmd.';" ^
    "};" ^
    "$disable = $action -eq 'Disable';" ^
    "$unsaved = @($states | Where-Object { $_.DisableConfigured -ne $disable });" ^
    "$pending = @($states | Where-Object {" ^
    "    if ($disable) { $_.Problem -ne 'CM_PROB_DISABLED' }" ^
    "    else { $_.Status -ne 'OK' }" ^
    "});" ^
    "$unexpected = @($pending | Where-Object {" ^
    "    -not $restartRequested.ContainsKey($_.InstanceId) -and -not ($disable -and $_.Status -eq 'OK')" ^
    "});" ^
    "if ($failed -or $states.Count -ne 4 -or $unsaved.Count -or $unexpected.Count) {" ^
    "    throw 'Windows has not confirmed the requested state for all four entries.';" ^
    "};" ^
    "if ($restartRequested.Count -or $pending.Count) {" ^
    "    Write-Host ('Done: ' + $action + ' saved for all four entries. Restart Windows to apply the change.');" ^
    "    exit 3010;" ^
    "};" ^
    "Write-Host ('Done: ' + $action + ' confirmed for all four entries.');"

set "SWITCH_EXIT=%ERRORLEVEL%"
if /i "%NEVERMOR_ACTION%"=="Status" exit /b %SWITCH_EXIT%
if not "%SWITCH_EXIT%"=="0" if not "%SWITCH_EXIT%"=="3010" (
    echo.
    echo If access was denied, right-click this file and select Run as administrator.
)
pause
exit /b %SWITCH_EXIT%
