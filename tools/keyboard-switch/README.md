# Nevermor HID Keyboard Switch

Отдельная утилита для Windows 10/11 x64. Отключает четыре клавиатурные записи Nevermor Q590-SD / SD-Q590, которые Windows показывает в «Диспетчере устройств». Утилита запускается без установки и запрашивает права администратора для изменения состояния устройств.

## Скачать командные файлы

- [Отключить клавиатурные записи](Disable-NevermorKeyboards.cmd)
- [Откатить отключение и вернуть записи](Restore-NevermorKeyboards.cmd)

На странице нужного файла нажмите **Download raw file**. В Проводнике щёлкните скачанный `.cmd` правой кнопкой и выберите **Запуск от имени администратора**. Оба файла используют системный `PnPUtil` и проверяют, что четыре записи относятся к тому же USB-устройству, что и HID-канал дисплея.

На Windows 11 Smart App Control может блокировать скачанный `.cmd` из-за метки интернет-загрузки. В этом случае используйте [команды для Windows PowerShell](#если-windows-блокирует-exe) ниже.

Если Windows отклоняет отключение как критическое (код `0xE0000231`), файл отключения повторяет команду с `/force` только для проверенных записей и при наличии другой активной записи класса Keyboard. Перед запуском убедитесь, что другая физическая клавиатура работает. `/force` поддерживается начиная с Windows 11 22H2. [Синтаксис PnPUtil](https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/pnputil-command-syntax#disable-device)

Параметр `--status` в командной строке показывает состояние записей. По окончании отключения или восстановления файл показывает результат и ждёт нажатия клавиши.

## Графическая утилита

[Скачать NevermorKeyboardSwitch.exe](NevermorKeyboardSwitch.exe?raw=1)

1. Запустите `NevermorKeyboardSwitch.exe`.
2. Нажмите «Отключить записи». Windows сохраняет это состояние после перезагрузки.
3. Чтобы вернуть записи, нажмите «Включить обратно».

Утилита ищет дисплей `1A2C:4E84` с HID-коллекцией `MI_01&Col07` и назначением `FF01:0001`. Она меняет состояние только четырёх записей класса Keyboard с тем же родительским USB-устройством. Для обновления показаний дисплея по-прежнему используется Nevermor Display.

## Если Windows блокирует EXE

Текущая сборка EXE не подписана, поэтому Windows 11 с включённым Smart App Control может заблокировать запуск. Для отключения и восстановления записей можно использовать встроенный `PnPUtil`. [Как Smart App Control проверяет приложения](https://support.microsoft.com/en-us/windows/security/threat-malware-protection/smart-app-control-frequently-asked-questions)

Откройте **Windows PowerShell от имени администратора** и вставьте весь блок. В режиме `Status` он выводит состояние четырёх проверенных записей. Для отключения замените `Status` в последней строке на `Disable` и выполните весь блок ещё раз. Для восстановления укажите `Enable`.

```powershell
& {
    param([ValidateSet('Status', 'Disable', 'Enable')][string]$Action = 'Status')
    $ErrorActionPreference = 'Stop';
    $vidPid = 'VID_1A2C&PID_4E84';
    function Get-UsbDeviceId([string]$instanceId) {
        $key = 'DEVPKEY_Device_Parent';
        $parent = (Get-PnpDeviceProperty -InstanceId $instanceId -KeyName $key).Data;
        (Get-PnpDeviceProperty -InstanceId $parent -KeyName $key).Data;
    };
    $displayPattern = '^HID\\' + $vidPid + '&MI_01&COL07\\[^\\]+$';
    $keyboardPattern = '^HID\\' + $vidPid + '&(MI_00|MI_01&COL0[456])\\[^\\]+$';
    $display = @(Get-PnpDevice -Class HIDClass -PresentOnly | Where-Object {
        $_.InstanceId -match $displayPattern -and
        (Get-PnpDeviceProperty -InstanceId $_.InstanceId -KeyName 'DEVPKEY_Device_HardwareIds').Data -contains 'HID_DEVICE_UP:FF01_U:0001'
    });
    if ($display.Count -ne 1) { throw 'Connect exactly one supported Nevermor Q590-SD display.' };
    $root = Get-UsbDeviceId $display[0].InstanceId;
    $rootPattern = '^USB\\' + $vidPid + '\\[^\\]+$';
    if ($root -notmatch $rootPattern) { throw 'Could not confirm the display USB device.' };
    $allKeyboards = @(Get-PnpDevice -Class Keyboard -PresentOnly);
    $keyboards = @($allKeyboards | Where-Object {
        $_.InstanceId -match $keyboardPattern -and (Get-UsbDeviceId $_.InstanceId) -eq $root
    });
    $collections = @($keyboards | ForEach-Object { ($_.InstanceId -split '\\')[1] } | Sort-Object -Unique);
    if ($keyboards.Count -ne 4 -or $collections.Count -ne 4) { throw 'Could not confirm all four keyboard entries.' };
    if ($action -eq 'Disable' -and $display[0].Status -ne 'OK') { throw 'The display HID channel is not active.' };
    $otherKeyboards = @($allKeyboards | Where-Object {
        $_.Status -eq 'OK' -and $_.InstanceId -notmatch ('^HID\\' + $vidPid + '&')
    });
    $instanceIds = $keyboards.InstanceId;
    $failed = $false;
    if ($action -ne 'Status') {
        $pnpUtil = Join-Path $env:SystemRoot 'System32\pnputil.exe';
        $verb = if ($action -eq 'Disable') { '/disable-device' } else { '/enable-device' };
        foreach ($keyboard in $keyboards) {
            $disabled = [string]$keyboard.ConfigManagerErrorCode -eq 'CM_PROB_DISABLED';
            if (($action -eq 'Disable' -and $disabled) -or ($action -eq 'Enable' -and $keyboard.Status -eq 'OK')) { continue };
            $output = & $pnpUtil $verb $keyboard.InstanceId;
            $exitCode = $LASTEXITCODE;
            if ($action -eq 'Disable' -and $exitCode -eq -536870351) {
                if ($otherKeyboards.Count -gt 0) {
                    Write-Host ('Retrying the confirmed display entry with /force: ' + $keyboard.InstanceId);
                    $output = & $pnpUtil /disable-device $keyboard.InstanceId /force;
                    $exitCode = $LASTEXITCODE;
                } else { Write-Warning 'Connect a working keyboard before retrying.' };
            };
            $output;
            if ($exitCode -eq 3010) { Write-Warning 'Windows requires a restart.' }
            elseif ($exitCode -ne 0) { $failed = $true; Write-Warning ('PnPUtil exit code: ' + $exitCode) };
        };
    };
    $states = @(Get-PnpDevice -InstanceId $instanceIds);
    $states | Format-Table Status, ConfigManagerErrorCode, InstanceId -AutoSize;
    if ($action -eq 'Status') { return };
    if ($action -eq 'Disable' -and (Get-PnpDevice -InstanceId $display[0].InstanceId).Status -ne 'OK') {
        throw 'Windows did not confirm the display HID channel. Run Restore-NevermorKeyboards.cmd.';
    };
    $pending = @($states | Where-Object {
        if ($action -eq 'Disable') { [string]$_.ConfigManagerErrorCode -ne 'CM_PROB_DISABLED' }
        else { $_.Status -ne 'OK' }
    });
    if ($failed -or $states.Count -ne 4 -or $pending.Count) { throw 'Windows has not confirmed the requested state for all four entries.' };
    Write-Host ('Done: ' + $action + ' confirmed for all four entries.');
} Status
```

Блок подтверждает общий USB-родитель всех четырёх записей и служебной коллекции дисплея. После выполнения `Disable` можно проверить обновление цифр на дисплее, например в режиме секунд в Nevermor Display. [Команды PnPUtil](https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/pnputil-command-syntax#disable-device)

## Сборка

В PowerShell из этой папки выполните `.\build.ps1`. Файл `dist\NevermorKeyboardSwitch.exe` собирается компилятором из .NET Framework. Код распространяется по лицензии MIT из корня репозитория.
