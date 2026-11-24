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


---

## Доменна модель: сутності, інваріанти, інкапсуляція

### Предметна область: Бібліотека
- **Основна сутність**: `BookCopy` (примірник книги у фонді)
- **Сутність видачі**: `Loan` (фіксація видачі книги читачу)
- **Доменний сервіс**: `LoanPolicyService` (міжсутнісна бізнес-логіка)

### Перелік доменних інваріантів:
1. **Ідентифікатор книги обов'язковий і не може бути порожнім**:
   - Тип винятку: `ArgumentException`
   - Локація: `BookCopy.Create(...)`
2. **ISBN обов'язковий і не може бути порожнім**:
   - Тип винятку: `ArgumentException`
   - Локація: `BookCopy.Create(...)`
3. **Назва книги обов'язкова**:
   - Тип винятку: `ArgumentException`
   - Локація: `BookCopy.Create(...)`
4. **Рік видання книги має бути в межах від 1 до поточного року**:
   - Тип винятку: `ArgumentOutOfRangeException`
   - Локація: `BookCopy.Create(...)`
5. **Заборона повторної видачі книги, яка вже перебуває на руках**:
   - Тип винятку: `InvalidOperationException`
   - Локація: `BookCopy.Issue()`
6. **Заборона повернення книги, яка не була видана**:
   - Тип винятку: `InvalidOperationException`
   - Локація: `BookCopy.Return()`
7. **Заборона повторного закриття вже закритої видачі (перехід станів `Active -> Returned`)**:
   - Тип винятку: `InvalidOperationException`
   - Локація: `Loan.Close(...)`
8. **Дата повернення не може бути ранішою за дату видачі**:
   - Тип винятку: `ArgumentOutOfRangeException`
   - Локація: `Loan.Close(...)`
9. **Обмеження кількості одночасно відкритих видач для одного читача (максимум 5)**:
   - Тип винятку: `InvalidOperationException`
   - Локація: `LoanPolicyService.IssueBookToReader(...)`
   - *Обґрунтування винесення в сервіс*: правило вимагає інформації про дві різні сутності (`BookCopy` та історію `Loan` читача). Сама сутність книги не повинна мати доступу до списку видач читача, тому міжсутнісний інваріант винесено в доменний сервіс.

   ## Відкладене виконання (Deferred Execution) у LINQ

У моєму коді запит конструюється викликом `Where()`, проте сама фільтрація відкладається і фактично виконується лише в момент виклику термінального оператора `Count()` або матеріалізації через `ToList()`. 
Два виклики `Count()` повернули різні результати (2 та 3), оскільки `query` не зберігає зафіксовані дані, а заново обходить поточну колекцію `demoList` при кожному зверненні, враховуючи доданий елемент. 
Натомість метод `ToList()` створює знімок результату в пам'яті (матеріалізація), тому наступне очищення джерела `demoList.Clear()` уже ніяк не впливає на зафіксовану кількість елементів у `snapshot`.


## Порівняння синтаксису методів (Method Syntax) та синтаксису запитів (Query Syntax)

Для звіту групування видань за авторами порівняно два підходи реалізації:

1. **Синтаксис методів (Method / Fluent Syntax):**
   ```csharp
   source.GroupBy(b => b.Author ?? "Невідомий автор", StringComparer.OrdinalIgnoreCase)
         .Select(g => new GroupSummary(g.Key, g.Count()))
         .OrderByDescending(s => s.Count)
         .ThenBy(s => s.Key, StringComparer.Ordinal)
         .ToList();

2. 
```csharp
 var query = from b in source
            group b by b.Author ?? "Невідомий автор" into g
            orderby g.Count() descending, g.Key
            select new GroupSummary(g.Key, g.Count());

return query.ToList();

## Порівняння читабельності:
Query Syntax (from ... group ... into ...) візуально нагадує декларативні SQL-запити. Він зручний для складних групувань з проміжним контекстом (into g), проте не дозволяє напряму передавати кастомні правила порівняння рядків (наприклад, StringComparer.OrdinalIgnoreCase).

Method Syntax природніший для повсякденної розробки на C#. Він вибудовує прозорий конвеєр викликів (pipeline), є компактнішим і підтримує методи, яких немає серед ключових слів Query Syntax (Take, DistinctBy, ThenBy).





## Конфігурація, профілі та Secrets (ЛР №9)

Застосунок використовує `Microsoft.Extensions.Configuration` із каскадом джерел конфігурації (від найнижчого до найвищого пріоритету):
1. `appsettings.json` — базові налаштування для всіх середовищ.
2. `appsettings.{CROSSAPP_ENVIRONMENT}.json` — налаштування активного профілю (`Development` за замовчуванням або `Production`).
3. `dotnet user-secrets` — локальне сховище конфіденційних ключів поза репозиторієм Git.
4. Змінні середовища з префіксом `CROSSAPP_` (а також окрема змінна `CROSSAPP_DATA_PATH`).
5. Аргументи командного рядка (`--Section:Key=Value` або `--file`).

### Профілі середовища

* **`Development`** (за замовчуванням, якщо `CROSSAPP_ENVIRONMENT` не задано): `Logging:MinimumLevel = Debug`, `Library:LoanDays = 14`, зберігання даних у `data/books.json` відносно каталогу збірки (`AppContext.BaseDirectory`).
* **`Production`** (`CROSSAPP_ENVIRONMENT=Production`): використовує базові налаштування з `appsettings.json` (`Logging:MinimumLevel = Information`, `Library:LoanDays = 14`) без секретів розробника.

### Таблиця ключів конфігурації

| Ключ конфігурації      | Змінна середовища                                 | Значення за замовчуванням    | Опис                                                                                       |
| :--------------------- | :------------------------------------------------ | :--------------------------- | :----------------------------------------------------------------------------------------- |
| `Storage:DataPath`     | `CROSSAPP_Storage__DataPath` (`CROSSAPP_DATA_PATH`) | `data`                       | Відносний або абсолютний шлях до каталогу збереження даних                                 |
| `Storage:FileName`     | `CROSSAPP_Storage__FileName`                      | `books.json`                 | Ім'я JSON-файлу каталогу примірників бібліотеки                                            |
| `Logging:MinimumLevel` | `CROSSAPP_Logging__MinimumLevel`                  | `Information` (`Debug` у Dev)| Мінімальний рівень журналювання застосунку                                                 |
| `Library:LoanDays`     | `CROSSAPP_Library__LoanDays`                      | `14`                         | Стандартний термін видачі книги читачеві (у днях)                                          |
| `Integration:ApiKey`   | `CROSSAPP_Integration__ApiKey` / `user-secrets`   | *(не задано)*                | Секретний ключ зовнішньої інтеграції (у консоль виводиться лише факт наявності та довжина) |

### Приклади запуску зі змінними середовища та секретами

#### Git Bash / Linux / macOS (bash)

```bash
# Налаштування секретного ключа локально (один раз)
dotnet user-secrets set "Integration:ApiKey" "local-dev-key" --project src/Cli

# Запуск у профілі Production зі зміненим терміном позики та шляхом даних
CROSSAPP_ENVIRONMENT=Production CROSSAPP_Library__LoanDays=30 CROSSAPP_DATA_PATH=custom_data dotnet run --project src/Cli -- export

# Імпорт даних із JSON-сховища
dotnet run --project src/Cli -- import


# Налаштування секретного ключа локально (один раз)
dotnet user-secrets set "Integration:ApiKey" "local-dev-key" --project src/Cli

# Запуск у профілі Production зі зміненим терміном позики та шляхом даних
$env:CROSSAPP_ENVIRONMENT = "Production"
$env:CROSSAPP_Library__LoanDays = "30"
$env:CROSSAPP_DATA_PATH = "custom_data"
dotnet run --project src/Cli -- export

# Очищення тимчасових змінних сесії (за потреби)
Remove-Item Env:CROSSAPP_ENVIRONMENT, Env:CROSSAPP_Library__LoanDays, Env:CROSSAPP_DATA_PATH -ErrorAction SilentlyContinue





## Web API (Minimal API)

Сервер налаштовано на фіксований порт **`5080`** (`http://localhost:5080`).

### Запуск сервера

```powershell
dotnet run --project src/Api
# або явно вказати URL:
dotnet run --project src/Api --urls http://localhost:5080