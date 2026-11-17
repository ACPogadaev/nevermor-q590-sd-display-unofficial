# Nevermor HID Keyboard Switch

Отдельная утилита для Windows 10/11 x64. Отключает четыре клавиатурные записи Nevermor Q590-SD / SD-Q590, которые Windows показывает в «Диспетчере устройств». Утилита запускается без установки и запрашивает права администратора для изменения состояния устройств.

## Скачать командные файлы

- [Отключить клавиатурные записи](Disable-NevermorKeyboards.cmd)
- [Откатить отключение и вернуть записи](Restore-NevermorKeyboards.cmd)

На странице нужного файла нажмите **Download raw file**. В Проводнике щёлкните скачанный `.cmd` правой кнопкой и выберите **Запуск от имени администратора**. Оба файла используют системный `PnPUtil` и проверяют, что четыре записи относятся к тому же USB-устройству, что и HID-канал дисплея.

На Windows 11 Smart App Control может блокировать скачанный `.cmd` из-за метки интернет-загрузки. В этом случае используйте [команды для Windows PowerShell](#если-windows-блокирует-exe) ниже.

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
    $ErrorActionPreference = 'Stop'

    function Get-UsbDeviceId([string]$InstanceId) {
        $key = 'DEVPKEY_Device_Parent'
        $parent = (Get-PnpDeviceProperty -InstanceId $InstanceId -KeyName $key).Data
        (Get-PnpDeviceProperty -InstanceId $parent -KeyName $key).Data
    }

    $vidPid = 'VID_1A2C&PID_4E84'
    $display = @(Get-PnpDevice -Class HIDClass -PresentOnly | Where-Object {
        $_.InstanceId -match "^HID\\$vidPid&MI_01&COL07\\[^\\]+$" -and
        (Get-PnpDeviceProperty -InstanceId $_.InstanceId -KeyName 'DEVPKEY_Device_HardwareIds').Data -contains 'HID_DEVICE_UP:FF01_U:0001'
    })
    if ($display.Count -ne 1) { throw 'Должен быть подключён ровно один дисплей Q590-SD.' }
    $root = Get-UsbDeviceId $display[0].InstanceId
    if ($root -notmatch "^USB\\$vidPid\\[^\\]+$") { throw 'USB-устройство дисплея не подтверждено.' }

    $keyboards = @(Get-PnpDevice -Class Keyboard -PresentOnly | Where-Object {
        $_.InstanceId -match "^HID\\$vidPid&(MI_00|MI_01&COL0[456])\\[^\\]+$" -and
        (Get-UsbDeviceId $_.InstanceId) -eq $root
    })
    $collections = @($keyboards | ForEach-Object { ($_.InstanceId -split '\\')[1] } | Sort-Object -Unique)
    if ($keyboards.Count -ne 4 -or $collections.Count -ne 4) {
        throw 'Не удалось подтвердить все четыре клавиатурные записи дисплея.'
    }
    if ($Action -eq 'Disable' -and $display[0].Status -ne 'OK') {
        throw 'HID-канал дисплея неактивен.'
    }

    $instanceIds = $keyboards.InstanceId
    if ($Action -ne 'Status') {
        $verb = if ($Action -eq 'Disable') { '/disable-device' } else { '/enable-device' }
        foreach ($id in $instanceIds) {
            pnputil.exe $verb $id
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Запись $id. Код завершения PnPUtil: $LASTEXITCODE."
            }
        }
    }
    Get-PnpDevice -InstanceId $instanceIds |
        Format-Table Status, ConfigManagerErrorCode, InstanceId -AutoSize
} Status
```

Блок подтверждает общий USB-родитель всех четырёх записей и служебной коллекции дисплея. После выполнения `Disable` можно проверить обновление цифр на дисплее, например в режиме секунд в Nevermor Display. [Команды PnPUtil](https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/pnputil-command-syntax#disable-device)

## Сборка

В PowerShell из этой папки выполните `.\build.ps1`. Файл `dist\NevermorKeyboardSwitch.exe` собирается компилятором из .NET Framework. Код распространяется по лицензии MIT из корня репозитория.
