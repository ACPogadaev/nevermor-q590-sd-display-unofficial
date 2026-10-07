# Nevermor Display 1.1 — независимое неофициальное ПО Q590-SD / SD-Q590

Проект является независимым неофициальным программным обеспечением и не связан с Nevermor. Все товарные знаки принадлежат их владельцам. Использование осуществляется на собственный риск. Перед применением на реальном оборудовании необходимо проверить совместимость и требования производителя.

Independent unofficial software, not affiliated with Nevermor. All trademarks belong to their respective owners. Use at your own risk; verify compatibility and manufacturer requirements before using on real hardware.

Portable Windows x64 display controller for the Nevermor Q590-SD CPU cooler, also known as SD-Q590.

Download `NevermorDisplay-v1.1.0-win-x64.zip`, extract the entire archive, close the OEM Digital app, and run `NevermorDisplay.exe`. .NET Framework 4.7.2 or later is required. Keep bundled DLLs beside the EXE. This release starts with standard settings and autostart disabled.

Choose CPU/GPU values, RAM use, fan RPM, a 12/24-hour clock, date DDMM, separate seconds, or a fixed number. Configure the refresh interval, tray startup, and autostart in settings. The current UI is in Russian; bilingual instructions are included.

Для Nevermor Q590-SD / SD-Q590: распакуйте архив целиком, закройте Digital, запустите EXE. Выберите показатели и нажмите «Сохранить». Для температуры CPU и RPM требуется запуск от администратора. Часы поддерживают формат Windows, 12 и 24 часа.

USB report updates have been tested on one Q590-SD. The protocol sends numeric digits only; physical labels stay fixed. CPU/RPM availability and actual login autostart require separate verification on the target account/board. See the repository validation notes. Public dependencies are restored from official NuGet packages and distributed unmodified with notices.

`SHA256SUMS.txt` contains the SHA-256 checksum of the portable ZIP.
