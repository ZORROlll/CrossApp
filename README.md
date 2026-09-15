# CrossApp
Наскрізний проєкт з крос-платформного програмування.
Предметна область: Бібліотека. Сутності: Book, BookCopy, Reader, Loan.
Призначення: облік видач примірників книг читачам.

## Запуск
dotnet build
dotnet run --project src/Cli

## Середовище
.NET SDK 8.0, Windows 11 x64 / Debian 12 x64 (Docker)

## Розміри автономної збірки (Self-Contained)
* Windows (win-x64): ~71 MB
* Linux (linux-x64): ~71 MB

## Розміри контейнерів
* Образ `mcr.microsoft.com/dotnet/sdk:8.0`: ~850 MB
* Автономний застосунок у чистому контейнері: ~71 MB
