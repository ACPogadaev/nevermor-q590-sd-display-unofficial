# Nevermor Q590-SD / SD-Q590 — неофициальное ПО 1.3.1

Проект является независимым неофициальным программным обеспечением и не связан с Nevermor. Все товарные знаки принадлежат их владельцам. Использование осуществляется на собственный риск. Перед применением на реальном оборудовании необходимо проверить совместимость и требования производителя.

Download **NevermorDisplay-1.3.1-Setup.exe** below. One offline installation includes the application, LibreHardwareMonitor 0.9.6 and the unchanged official signed PawnIO 2.2.0 installer. AIDA64, Digital and separate monitoring software are not required. Windows 10/11 x64 with .NET Framework 4.7.2 or later; current UI is Russian. Our app and combined installer are unsigned; the embedded original PawnIO installer/components are signed by their publisher.

Закройте Digital через трей. Запустите единый установщик, подтвердите UAC, установите и при необходимости перезагрузите Windows. Выберите показатели, интервал обновления и автозапуск в окне программы. По умолчанию загрузка CPU сверху, температура CPU снизу, опрос раз в 2 секунды, автозапуск выключен.

CPU/GPU readings, RAM usage, fan RPM, 12/24-hour clock, seconds, DDMM date and fixed numbers. Physical limits: four digits above, two below; printed labels remain. CPU/RPM require elevation and the bundled driver. PawnIO is a third-party component by namazso, not this project's own driver.

The old WinRing0 backend has been replaced. See the repository's explanation of Defender's confirmed Digital detection. Windows security settings are not weakened. Cyrillic Start menu shortcut installation is fixed. The owner confirmed CPU temperature and physical display operation after restart on one Q590-SD; fan mapping, actual login autostart and other revisions need further verification. No general compatibility guarantee.

`SHA256SUMS.txt` verifies the installer. Source is in the repository. The first published release contains the same 1.3.1 installer tested by the owner; no experimental USB transport changes are included.
