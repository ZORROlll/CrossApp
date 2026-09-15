# CrossApp

## Команди збірки, запуску та публікації

### Збірка
dotnet build CrossApp.sln

### Запуск
dotnet run --project src/Cli

### Публікація win-x64 self-contained
dotnet publish src/Cli -c Release -r win-x64 --self-contained true -o publish/self

### Публікація win-x64 framework-dependent
dotnet publish src/Cli -c Release -r win-x64 --self-contained false -o publish/framework

### Публікація linux-x64 self-contained
dotnet publish src/Cli -c Release -r linux-x64 --self-contained true -o publish/linux

### Запуск опублікованого застосунку
./publish/self/Cli.exe

## Порівняння варіантів публікації

| RID | Режим | Розмір publish | Потрібен runtime |
| :--- | :--- | :--- | :--- |
| win-x64 | self-contained | 71 МБ | ні |
| win-x64 | framework-dependent | 197 КБ | так (.NET 8) |
| linux-x64 | self-contained | 71 МБ | ні |
