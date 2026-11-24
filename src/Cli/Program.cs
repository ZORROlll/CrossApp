using System.Diagnostics;
using System.Text;
using Cli;
using Core;
using Core.Abstractions;
using Core.Domain;
using Core.Dto;
using Core.Events;
using Core.Import;
using Core.Services;
using Core.Storage;
using Core.Data;
using System.Reflection;
using Core.Configuration;
using Microsoft.Extensions.Configuration;
Console.OutputEncoding = Encoding.UTF8;

// Зчитуємо назву середовища (Development, Production тощо) зі змінної оточення; якщо вона не задана — ставимо "Development" за замовчуванням
var envName = Environment.GetEnvironmentVariable("CROSSAPP_ENVIRONMENT") ?? "Development";

// Ініціалізуємо будівельник конфігурації та задаємо пріоритет джерел (кожен наступний рівень перевизначає попередній)
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    // Базові налаштування для всіх середовищ; файл обов'язковий (optional: false), зміни під час роботи не відстежуємо
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    //Специфічні налаштування для поточного середовища (наприклад, appsettings.Development.json); файл необов'язковий
    .AddJsonFile($"appsettings.{envName}.json", optional: true)
    //Локальні секрети розробника з профілю користувача Windows (UserSecretsId поточної збірки); працюють навіть у Production, якщо є
    .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
    // 4. Змінні середовища ОС із префіксом "CROSSAPP_" (префікс обрізається, а "__" перетворюється на ":" для вкладених секцій)
    .AddEnvironmentVariables(prefix: "CROSSAPP_")
    // 5. Аргументи командного рядка (найвищий пріоритет — перебивають усі попередні джерела)
    .AddCommandLine(args)
    .Build();

// Прив'язуємо (мапимо) зібрану конфігурацію до типізованого об'єкта налаштувань AppOptions; якщо конфіг порожній — створюємо дефолтний екземпляр
var options = config.Get<AppOptions>() ?? new AppOptions();

// Перевіряємо застарілу (legacy) плоску змінну середовища CROSSAPP_DATA_PATH для зворотної сумісності
var legacy = Environment.GetEnvironmentVariable("CROSSAPP_DATA_PATH");

// Формуємо фінальні налаштування сховища: якщо задано стару змінну CROSSAPP_DATA_PATH — беремо її, інакше беремо шлях із options.Storage
var storage = new StorageOptions
{
    DataPath = string.IsNullOrWhiteSpace(legacy) ? options.Storage.DataPath : legacy,
    FileName = options.Storage.FileName
};

// Обчислюємо повний абсолютний шлях до файлу даних відносно базової директорії програми
var fullDataPath = storage.ResolveFullPath(AppContext.BaseDirectory);

// Зчитуємо секретний ключ інтеграції за ключем "Integration:ApiKey" (із UserSecrets, змінних середовища або CLI-аргументів)
var apiKey = config["Integration:ApiKey"]; // пункт 6

Console.WriteLine($"Файл даних: {fullDataPath}");
Console.WriteLine(apiKey is null ? "ApiKey: не заданий" : $"ApiKey: присутній, довжина {apiKey.Length}");

// ==================== ОБРОБКА ПІДКОМАНД ЗВІТІВ ====================
string subCmd = args.FirstOrDefault(a => !a.StartsWith("--"))?.ToLowerInvariant() ?? "";

if (subCmd is "report-low" or "report-top" or "report-groups" or "report-totals")

{
    var reportBooks = SampleData.Books();

    if (subCmd == "report-low")
    {
        Console.WriteLine("==============================================================================");
        Console.WriteLine("ЗВІТ: ВИДАННЯ, СТАРІШІ ЗА 2000 РІК (report-low)");
        Console.WriteLine("==============================================================================");
        Console.WriteLine($" {"ISBN",-16} | {"Назва книги",-28} | {"Автор",-20} | {"Рік"}");
        Console.WriteLine(new string('-', 78));
        var older = Core.Reports.ReportService.OlderThan(reportBooks, 2000);
        foreach (var b in older)
            Console.WriteLine($" {b.Isbn,-16} | {b.Title,-28} | {b.Author,-20} | {b.Year}");
        Console.WriteLine(new string('-', 78));
        Console.WriteLine($"Всього знайдено: {older.Count} запис(ів).\n");
    }
    else if (subCmd == "report-top")
    {
        Console.WriteLine("==============================================================================");
        Console.WriteLine("ЗВІТ: ТОП НАЙНОВІШИХ ВИДАНЬ (report-top)");
        Console.WriteLine("==============================================================================");
        Console.WriteLine($" {"ISBN",-16} | {"Назва книги",-28} | {"Автор",-20} | {"Рік"}");
        Console.WriteLine(new string('-', 78));
        var top = Core.Reports.ReportService.TopNewest(reportBooks, 5);
        foreach (var b in top)
            Console.WriteLine($" {b.Isbn,-16} | {b.Title,-28} | {b.Author,-20} | {b.Year}");
        Console.WriteLine(new string('-', 78));
        Console.WriteLine($"Всього у топі: {top.Count} запис(ів).\n");
    }
    else if (subCmd == "report-groups")
    {
        Console.WriteLine("==============================================================================");
        Console.WriteLine("ЗВІТ: РОЗПОДІЛ КНИГ ЗА АВТОРАМИ (report-groups)");
        Console.WriteLine("==============================================================================");
        Console.WriteLine($" {"Автор",-30} | {"Кількість книг"}");
        Console.WriteLine(new string('-', 78));
        var groups = Core.Reports.ReportService.ByAuthor(reportBooks);
        foreach (var g in groups)
            Console.WriteLine($" {g.Key,-30} | {g.Count}");
        Console.WriteLine(new string('-', 78));
        Console.WriteLine($"Всього унікальних авторів: {groups.Count}.\n");
    }
    else if (subCmd == "report-totals")
    {
        Console.WriteLine("==============================================================================");
        Console.WriteLine("ЗВІТ: ЗАГАЛЬНІ АГРЕГОВАНІ ПОКАЗНИКИ (report-totals)");
        Console.WriteLine("==============================================================================");
        var tRes = Core.Reports.ReportService.Totals(reportBooks);
        Console.WriteLine($" Загальна кількість примірників : {tRes.Items}");
        Console.WriteLine($" Середній рік видання          : {tRes.AverageYear:F1}");
        Console.WriteLine($" Найдавніший рік видання       : {tRes.MinYear}");
        Console.WriteLine($" Найсвіжіший рік видання       : {tRes.MaxYear}");
        Console.WriteLine("==============================================================================\n");
    }

    return 0; // Завершуємо роботу підкоманди, не запускаючи повний пайплайн
}


// ==================== ЗЧИТУВАННЯ АРГУМЕНТІВ ====================
// Шукаємо прапорець --file
int fileFlagIndex = Array.FindIndex(args, a => string.Equals(a, "--file", StringComparison.OrdinalIgnoreCase));
bool useFile = fileFlagIndex != -1;

string? customFilePath = null;
if (useFile && fileFlagIndex + 1 < args.Length && !args[fileFlagIndex + 1].StartsWith("--"))
{
    customFilePath = args[fileFlagIndex + 1];
}

// Файл для блоку імпорту (ігноруємо значення після --file, щоб не ламало імпорт)
string importPath = args
    .Where((a, i) => !a.StartsWith("--") && (fileFlagIndex == -1 || i != fileFlagIndex + 1))
    .FirstOrDefault() ?? Path.Combine("data", "catalog.json");

// ==================== РОБОТА ЗІ СХОВИЩЕМ ТА СЕРВІСОМ (COMPOSITION ROOT) ====================
// [Крок 5.9 - ЛР №9] Шлях до файлу даних надходить із конфігурації (fullDataPath), якщо не перевизначений прапорцем --file
string catalogDataPath = customFilePath ?? fullDataPath;

// Вибір сховища залежно від прапорця --file (або за замовчуванням у конфігурації)
IBookStore baseStore = useFile
    ? new FileBookStore(catalogDataPath)
    : new InMemoryBookStore(SampleData.Books());

// Обгортання базового сховища декоратором
IBookStore store = new CachingBookStore(baseStore);
var service = new LendingService(store);
var lendingService = service;

// ==================== СЦЕНАРІЇ РОБОТИ ====================

Console.WriteLine("=== Сценарій 1: успішне створення та зміна стану сутності ===");
BookCopy book = BookCopy.Create("BC-001", "978-0132350884", "Clean Code", 2008, "Robert Martin");
Console.WriteLine(book);
book.Issue();
Console.WriteLine(book);
book.Return();
Console.WriteLine(book);
Console.WriteLine();

Console.WriteLine("=== Сценарій 2: збереження та відновлення через DTO ===");
BookDto dto = book.ToDto();
BookCopy restoredBook = BookCopy.FromDto(dto);
Console.WriteLine($"Оригінал:    {book}");
Console.WriteLine($"Відновлений: {restoredBook}");
Console.WriteLine();

Console.WriteLine("=== Сценарій 3: життєвий цикл та зміна станів видачі (Loan) ===");
Loan loan = Loan.Open("L-001", book.Id, "R-101", DateTime.UtcNow.AddDays(-5));
Console.WriteLine(loan);
loan.Close(DateTime.UtcNow);
Console.WriteLine(loan);
Console.WriteLine();

Console.WriteLine("=== Сценарій 4: порушення інваріантів сутностей ===");
TryDo("порожній ISBN", () =>
{
    BookCopy.Create("BC-002", "", "Refactoring", 1999, "Martin Fowler");
});

TryDo("рік видання з майбутнього", () =>
{
    BookCopy.Create("BC-003", "978-1111", "Future Book", 2099, "Unknown");
});

TryDo("повернення невиданої книги", () =>
{
    book.Return();
});

book.Issue();
TryDo("повторна видача вже виданої книги", () =>
{
    book.Issue();
});

TryDo("повторне закриття вже закритої видачі", () =>
{
    loan.Close(DateTime.UtcNow);
});

Loan invalidDateLoan = Loan.Open("L-002", "BC-001", "R-102", DateTime.UtcNow);
TryDo("дата повернення раніша за видачу", () =>
{
    invalidDateLoan.Close(DateTime.UtcNow.AddDays(-2));
});
Console.WriteLine();

Console.WriteLine("=== Сценарій 5: міжсутнісний інваріант (ліміт видач у сервісі) ===");
string readerWithManyBooks = "R-777";
var activeLoansList = new List<Loan>();
for (int i = 1; i <= 5; i++)
{
    activeLoansList.Add(Loan.Open($"L-00{i}", $"BC-10{i}", readerWithManyBooks, DateTime.UtcNow.AddDays(-i)));
}

BookCopy bookForLimitTest = BookCopy.Create("BC-999", "978-0000000000", "Domain-Driven Design", 2003, "Eric Evans");

TryDo("перевищення ліміту видач читача (>5)", () =>
{
    LoanPolicyService.IssueBookToReader(
        "L-006",
        bookForLimitTest,
        readerWithManyBooks,
        activeLoansList,
        DateTime.UtcNow);
});
Console.WriteLine();

// ==================== РОБОТА ЗІ СХОВИЩЕМ ТА СЕРВІСОМ ====================
Console.WriteLine("=== Робота з каталогом примірників ===");

Console.WriteLine($"[Конфігурація] Прапорець --file: {(useFile ? "АКТИВНИЙ" : "ВІДСУТНІЙ")}");
Console.WriteLine($"[Конфігурація] Базове сховище: {baseStore.GetType().Name} -> {catalogDataPath}");
Console.WriteLine($"[Конфігурація] Активне сховище: {store.GetType().Name}\n");

// Перевіряємо та демонструємо зміну стану
var existingBook = service.Find("BC-101");

if (existingBook == null)
{
    Console.WriteLine(" [Дія]: Книги BC-101 ще немає в сховищі. Створюємо новий запис...");
    var added = service.AddBook("BC-101", "978-0132350884", "Clean Architecture", 2017, "Robert Martin");
    Console.WriteLine($" [Збережено]: {added.Title} (ID: {added.Id}) — Стан: [В наявності]");
}
else
{
    Console.WriteLine($" [Дія]: Знайдено існуючу книгу {existingBook.Id}. Поточний стан: {(existingBook.IsIssued ? "Видана" : "В наявності")}");
    
    // Перемикаємо стан: видана -> повертаємо, вільна -> видаємо
    if (existingBook.IsIssued)
    {
        service.ReturnCopy(existingBook.Id);
        Console.WriteLine($" [Зміна стану]: Книгу {existingBook.Id} повернуто в бібліотеку.");
    }
    else
    {
        service.IssueCopy(existingBook.Id);
        Console.WriteLine($" [Зміна стану]: Книгу {existingBook.Id} видано читачеві.");
    }
}

// Виводимо актуальний каталог
Console.WriteLine("\nПоточний каталог у сховищі:");
foreach (var b in service.All())
{
    string status = b.IsIssued ? "Видана (На руках)" : "В наявності";
    Console.WriteLine($" {b.Id,-8} {b.Isbn,-16} {b.Title,-22} {b.Year,5} [{status}]");
}

// ==================== ЗАВДАННЯ 1: ДЕКОРАТОР ====================
Console.WriteLine("\n=== Демонстрація роботи CachingBookStore ===");
Console.WriteLine("1. Перший пошук (читання з базового сховища):");
var firstRead = service.Find("BC-101");
Console.WriteLine($"   Знайдено: {firstRead?.Title}");

Console.WriteLine("2. Другий пошук (читання з кешу):");
var cachedRead = service.Find("BC-101");
Console.WriteLine($"   Знайдено з кешу: {cachedRead?.Title}");
Console.WriteLine($"   Один і той самий об'єкт у пам'яті: {ReferenceEquals(firstRead, cachedRead)}");

// ==================== ЗАВДАННЯ: ПОШУК З ПАРАМЕТРОМ Func ====================
Console.WriteLine("\n=== Демонстрація пошуку за предикатом (Func<BookCopy, bool>) ===");

Console.WriteLine("1. Пошук книг автора 'Martin':");
var byAuthor = service.Search(b => b.Author?.Contains("Martin", StringComparison.OrdinalIgnoreCase) == true);
foreach (var b in byAuthor)
{
    Console.WriteLine($"   -> [{b.Year}] {b.Title} ({b.Author})");
}
// Пошук примірників за умовою: рік видання >= 2010
// Використовується предикат Func<BookCopy, bool> у формі лямбда-виразу
Console.WriteLine("2. Пошук сучасних книг (рік >= 2015):");
var modernBooks = service.Search(b => b.Year >= 2015);
foreach (var b in modernBooks)
{
    string status = b.IsIssued ? "Видана" : "В наявності";
    Console.WriteLine($"   -> [{b.Year}] {b.Title} [{status}]");
}

// ==================== ЗАВДАННЯ 3: ДЕМОНСТРАЦІЯ StoreFactory ====================
Console.WriteLine("\n=== Демонстрація створення через StoreFactory ===");
IBookStore factoryStore = StoreFactory.Create(args);
Console.WriteLine($"Сховище, створене фабрикою StoreFactory: {factoryStore.GetType().Name}");

// ==================== СЦЕНАРІЇ ВІДМОВИ ====================
Console.WriteLine("\n=== Сценарії відмови (обробка винятків) ===");

try
{
    string nonExistentId = "BC-99999";
    Console.WriteLine($"Спроба видачі книги з неіснуючим ID '{nonExistentId}'...");
    service.IssueCopy(nonExistentId);
}
catch (DomainException ex)
{
    Console.WriteLine($" [Очікувана помилка]: {ex.Message}");
}

try
{
    var currentItem = service.Find("BC-101");
    if (currentItem != null)
    {
        Console.WriteLine($"Спроба повторного додавання книги з існуючим ID '{currentItem.Id}'...");
        service.AddBook(currentItem.Id, "978-0000000000", "Duplicate Book", 2024, "Unknown");
    }
}
catch (Exception ex)
{
    Console.WriteLine($" [Очікувана помилка]: {ex.Message}");
}

// ==================== ЛАБОРАТОРНА РОБОТА 6: ДОМЕННІ ПОДІЇ ТА ДЕЛЕГАТИ ====================
Console.WriteLine("\n=== Доменні події, делегати Action/Func, лямбди ===");

// Крок 3: Налаштування шляху для лог-файлу та створення каталогу через Path.Combine
var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "app.log");
Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

// Локальний метод підписника (щоб згодом відписатися через -=). перший підписник
void OnLendingChanged(object? sender, LendingChangedEventArgs e)
{
    string details = e.Kind switch
    {
        LendingChangeKind.CopyIssued => $"видано читачу {e.ReaderId ?? "reader-01"} до {e.DueDate:yyyy-MM-dd}",
        LendingChangeKind.CopyReturned => "повернуто до фонду",
        LendingChangeKind.BookAdded => "додано новий примірник",
        _ => "зміна стану"
    };

    var line = $"{e.At:HH:mm:ss} {e.Kind,-14} {e.CopyId,-10} {details}";
    Console.WriteLine(line);
    File.AppendAllText(logPath, line + Environment.NewLine);
}

// Підписка першого обробника
service.Changed += OnLendingChanged; //+= бере адресу методу OnLendingChanged 
                                     //і додає її до внутрішнього списку викликів (InvocationList) події Changed

//  Другий підписник — лямбда із замиканням над лічильником changes
int changes = 0;
//оператор, що бере делегат і додає його другим у чергу викликів (InvocationList) події
service.Changed += (_, _) => changes++; //реєстрація другого підписника, зовнішня змінна changes

// Крок 5: Демонстраційний сценарій у Cli
var createdBook = service.AddBook("978-0321125217", "Patterns of Enterprise Architecture", 2002, "Martin Fowler");
service.IssueCopy(createdBook.Id, "reader-42", DateTimeOffset.Now.AddDays(21));
service.ReturnCopy(createdBook.Id);

Console.WriteLine($"Подій отримано: {changes}");

// Керована відписка першого обробника
service.Changed -= OnLendingChanged; // Відписка іменованого обробника: видаляє метод зі списку викликів (InvocationList) для запобігання витоку пам'яті

// Тиха операція: перший обробник мовчить, але дані змінюються і лямбда фіксує +1
service.IssueCopy(createdBook.Id, "reader-99"); //операцію видачі книги читачеві
Console.WriteLine($"Подій отримано: {changes}"); // +1 лише від лямбди-лічильника

// Крок 6: Сценарій відмови (порушення бізнес-правила)
int beforeFailCount = changes; //запяматовує значення лічильника перед операцією
try
{
    service.IssueCopy("non-existent-id", "reader-00");//метод доменного сервісу для видачі примірника читачу
}
catch (Exception ex) // або KeyNotFoundException, залежно від реалізації в LendingService
{
    Console.WriteLine($"[Очікувана помилка]: {ex.Message}");
}

// Перевірка інваріанта: події не було, лічильник не змінився
if (changes == beforeFailCount) // Перевіряємо, що лічильник не змінився і подія через помилку не спрацювала
{
    Console.WriteLine("Перевірка пройдена: при винятку подія не виникла.");
}

// Крок 7: Використання делегата Func там, де потрібне значення
Console.WriteLine("\n--- Використання FindAll(Func<BookCopy, bool>) ---");
var modernCopies = service.FindAll(b => b.Year >= 2010);
Console.WriteLine($"Знайдено примірників (рік >= 2010): {modernCopies.Count}");
foreach (var b in modernCopies)
{
    Console.WriteLine($"  * [{b.Year}] {b.Title} (ID: {b.Id})");
}

// ==================== ДОДАТКОВЕ ЗАВДАННЯ 1: INotificationSink ====================
Console.WriteLine("\n=== Демонстрація INotificationSink ===");
var sinkLogPath = Path.Combine(AppContext.BaseDirectory, "logs", "sink_app.log");
var compositeSink = new CompositeNotificationSink(
    new ConsoleSink(),
    new FileSink(sinkLogPath)
);

// Створення сервісу з передачею INotificationSink через конструктор (ручний DI)
var sinkService = new LendingService(store, compositeSink);
var sinkBook = sinkService.AddBook("978-0131103627", "The C Programming Language", 1988, "Brian Kernighan");//
sinkService.IssueCopy(sinkBook.Id, "student-01");

// Допоміжний метод перехоплення помилок
static void TryDo(string title, Action action)
{
    try
    {
        action();
        Console.WriteLine($" {title}: виняток НЕ спрацював — інваріант відсутній!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($" {title}: {ex.GetType().Name} — {ex.Message}");
    }
}

// --- Демо відкладеного виконання (Крок 3) ---
var demoList = new List<Core.Domain.BookCopy>
{
    Core.Domain.BookCopy.Create("d1", "978-001", "Кобзар", 1840, "Тарас Шевченко"),
    Core.Domain.BookCopy.Create("d2", "978-002", "Тіні забутих предків", 1911, "Михайло Коцюбинський")
};

var query = demoList.Where(b => b.Year < 1920);
Console.WriteLine($"[ДЕМО] До змін: {query.Count()}");

demoList.Add(Core.Domain.BookCopy.Create("d3", "978-003", "Лісова пісня", 1911, "Леся Українка"));
Console.WriteLine($"[ДЕМО] Після змін: {query.Count()}");

var snapshot = query.ToList();
demoList.Clear();
Console.WriteLine($"[ДЕМО] У знімку лишилось: {snapshot.Count}\n");


// --- Звіти через LINQ (Крок 4) ---
var books = service.All();

Console.WriteLine("=== ТОП-5 НАЙНОВІШИХ ВИДАНЬ ===");
foreach (var b in Core.Reports.ReportService.TopNewest(books, 5))
{
    Console.WriteLine($" {b.Isbn,-12} {b.Title,-26} {b.Year,5}  {b.Author}");
}

Console.WriteLine("\n=== ГРУПУВАННЯ ЗА АВТОРАМИ ===");
foreach (var g in Core.Reports.ReportService.ByAuthor(books))
{
    Console.WriteLine($" {g.Key,-30} книг: {g.Count,3}");
}

Console.WriteLine("\n=== ПІДСУМКИ ===");
var t = Core.Reports.ReportService.Totals(books);
Console.WriteLine($" Всього: {t.Items}, Середній рік: {t.AverageYear:F1}, Найстаріша: {t.MinYear}, Найновіша: {t.MaxYear}");


// ============================================================================
//               ДОДАТКОВЕ ЗАВДАННЯ: ЗВЕДЕНИЙ ЗВІТ ЧЕРЕЗ LINQ JOIN
// ============================================================================
Console.WriteLine("\n" + new string('*', 78));
Console.WriteLine(">>> ЗВЕДЕНИЙ ЗВІТ ВИДАЧ (LINQ JOIN) <<<");
Console.WriteLine(new string('*', 78));

// Отримуємо книги прямо з SampleData
var loanBooks = Core.Data.SampleData.Books();

// 1. Тестові читачі (ReaderDto)
var demoReaders = new List<Core.Dto.ReaderDto>
{
    new("r1", "Іван Франко", "T-101"),
    new("r2", "Леся Українка", "T-102")
};

// 2. Зв'язки видач (BookId -> ReaderId)
var availableBooks = loanBooks.Take(2).ToList();
var demoLoans = new List<(string BookId, string ReaderId)>();

if (availableBooks.Count >= 2)
{
    demoLoans.Add((availableBooks[0].Id, "r1"));
    demoLoans.Add((availableBooks[1].Id, "r2"));
}

// 3. Виклик Join у ReportService
var loanReport = Core.Reports.ReportService.GetIssuedBooksWithReaders(loanBooks, demoReaders, demoLoans);

Console.WriteLine($" {"Книга",-28} | {"ISBN",-16} | {"Читач",-18} | {"Квиток"}");
Console.WriteLine(new string('-', 78));
foreach (var row in loanReport)
{
    Console.WriteLine($" {row.BookTitle,-28} | {row.Isbn,-16} | {row.ReaderName,-18} | {row.TicketNumber}");
}
Console.WriteLine(new string('-', 78));
Console.WriteLine("[INFO] Об'єднано дві колекції (Книги × Читачі) у зведений DTO BookLoanLine.");
Console.WriteLine(new string('*', 78) + "\n");


Console.WriteLine("\n=== ТОП-3 АВТОРИ (ВИКОРИСТАННЯ TopBy) ===");
foreach (var author in Core.Reports.ReportService.TopAuthors(Core.Data.SampleData.Books(), 3))
{
    Console.WriteLine($" {author.Key,-30} | {author.Count} книг");
}


// Демонстрація синтаксису запитів (Query Syntax)
Console.WriteLine("\n=== ЗВІТ ЗА АВТОРАМИ (QUERY SYNTAX) ===");
foreach (var item in Core.Reports.ReportService.ByAuthorQuerySyntax(Core.Data.SampleData.Books()))
{
    Console.WriteLine($" {item.Key,-30} | {item.Count} книг");
}


// ============================================================================
//               ДОДАТКОВЕ ЗАВДАННЯ: БЕНЧМАРК ЗВІТУ (STOPWATCH НА 100 000 ЗАПИСІВ)
// ============================================================================
Console.WriteLine("\n" + new string('*', 78));
Console.WriteLine(">>> : ТЕСТ ПРОДУКТИВНОСТІ LINQ (100 000 ЗАПИСІВ) <<<");
Console.WriteLine(new string('*', 78));

// Генеруємо тестову базу даних прямо в оперативній пам'яті:
var bigDataset = Enumerable.Range(1, 100_000)
    .Select(i => Core.Domain.BookCopy.Create( // Перетворюємо кожне число на об'єкт книги
        $"b_{i}",                            
        $"978-{i:D6}",                       
        $"Book Title {i}",                  
        1950 + (i % 75),                     // Задаємо рік видання (в діапазоні від 1950 до 2024 року)
        $"Author {i % 500}"))                // Розподіляємо книги порівну між 500 різними авторами
    .ToList();

// Виконуємо швидкий необов'язковий LINQ-запит, щоб середовище .NET скомпілювало методи в машинний код
_ = bigDataset.Where(b => b.Year > 2000).Take(10).ToList();

// Створюємо об'єкт секундоміра для точного вимірювання часу в мілісекундах
var sw = new Stopwatch();

// Варіант А: Чистий конвеєр (фільтрація + взяття перших 500 БЕЗ вивантаження всіх 66 тисяч у проміжний список)
sw.Restart();
var resultA = bigDataset
    .Where(b => b.Year > 2000)
    .Select(b => new { b.Title, b.Year })
    .Take(500) // Оптимізація: беремо перші 500 знайдених і ОДРАЗУ зупиняємо обхід
    .ToList();
sw.Stop();
var timeWithoutIntermediateList = sw.ElapsedMilliseconds;

// Варіант Б: Із зайвим ToList() посередині ланцюжка (змушує виділити пам'ять під ~66 000 елементів)
sw.Restart();
var resultB = bigDataset
    .Where(b => b.Year > 2000)
    .ToList() // <-- Примусова непотрібна матеріалізація всього списку!
    .Select(b => new { b.Title, b.Year })
    .Take(500)
    .ToList();
sw.Stop();
var timeWithIntermediateList = sw.ElapsedMilliseconds;

Console.WriteLine($" 1. Чистий конвеєр (без ToList посередині) : {timeWithoutIntermediateList,5} ms");
Console.WriteLine($" 2. Із проміжним ToList() посередині       : {timeWithIntermediateList,5} ms");
Console.WriteLine(new string('-', 78));
Console.WriteLine($"[ВИСНОВОК]: Проміжний ToList() збільшив час виконання та навантаження на пам'ять.");
Console.WriteLine(new string('*', 78) + "\n");

// ============================================================================
//   КРОК 5: CLI, TOP-LEVEL AWAIT, CTRL+C ТА РОЗДІЛЬНА ОБРОБКА ВИНЯТКІВ
// ============================================================================
Console.WriteLine("==============================================================================");
Console.WriteLine("АСИНХРОННИЙ ІМПОРТ");
Console.WriteLine("==============================================================================");


//2) CLI перехоплює Ctrl+C і створює джерело скасування
using var cts = new CancellationTokenSource();//об'єкт, який створює сигнал скасування та керує ним.
//системна подія класу Console, яка спрацьовує, коли користувач у терміналі натискає комбінацію клавіш Ctrl+C
//додає наш обробник до списку дій, які мають виконатися в момент натискання клавіш.
Console.CancelKeyPress += (_, e) => //e — це змінна, яка містить дані про подію натискання Ctrl+C
{
    e.Cancel = true; //спрацьовує після натискання комбінації клавіш
    cts.Cancel(); // Подає сигнал скасування всім асинхронним операціям через CancellationToken
};

// Якщо в команді передано прапорець --cancel — скасовуємо операцію для перевірки
if (args.Contains("--cancel"))
{
    cts.Cancel();
}

string filePath = args.FirstOrDefault(a => !a.StartsWith("--") && a != "import") 
                  ?? Path.Combine("data", "catalog.json");

try
{
    var importer = new BookJsonImporter();

    // 1. Таймаут ставимо на 60 секунд (щоб він не обганяв паузу)
    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, timeoutCts.Token);
    //ми їх обєднуємо бо імпорт може прийняти тільки один токен     
    // Пауза на 10 секунд (якщо не натискати Ctrl+C — піде імпорт)
    Console.WriteLine("Очікування 10 секунд... Натисніть Ctrl+C для скасування або зачекайте для виводу таблиці:");
    await Task.Delay(10000, linkedCts.Token);

    // 2. Асинхронне читання сутностей із linked-токеном
    var loadedBooks = await importer.LoadAsync(filePath, linkedCts.Token); //метод конкретного завантажувача (наприклад, BookJsonImporter),
                                                                             //який відкриває файловий потік, парсить вміст і перетворює DTO на доменні сутності BookCopy.

    // ========================================================================
    // ДОДАТКОВЕ ЗАВДАННЯ 3: Підписник на прогрес (малює відсотки у CLI)
    // ========================================================================
    var progress = new Progress<int>(percent =>
    {
        Console.Write($"\r[ПРОГРЕС ІМПОРТУ]: {percent,3}%");
        if (percent == 100) Console.WriteLine();
    });

    // 3. Імпорт у сервіс із linked-токеном та передачею progress
    // Асинхронний виклик імпорту: передаємо обраний завантажувач (importer), шлях до файлу (filePath),
// токен для можливості скасування за Ctrl+C або таймаутом (linkedCts.Token) 
// та об'єкт для відстеження відсотків виконання (progress)
    int added = await lendingService.ImportAsync(importer, filePath, linkedCts.Token, progress);

    // 4. Таблиця результатів
    Console.WriteLine($"\nФайл: {filePath}\n");
    Console.WriteLine($" {"ID",-10} | {"ISBN",-16} | {"Назва книги",-30} | {"Рік"}");
    Console.WriteLine(new string('-', 78));
    foreach (var b in loadedBooks)
    {
        Console.WriteLine($" {b.Id,-10} | {b.Isbn,-16} | {b.Title,-30} | {b.Year}");
    }
    Console.WriteLine(new string('-', 78));
    Console.WriteLine($"[УСПІХ] Імпортовано записів: {added}\n");
}

catch (OperationCanceledException) when (cts.IsCancellationRequested)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("[СКАСОВАНО] Імпорт перервано користувачем (Ctrl+C або --cancel).");
    Console.ResetColor();
}
catch (OperationCanceledException)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("[ТАЙМАУТ] Перевищено ліміт часу очікування операції імпорту.");
    Console.ResetColor();
}
catch (DomainException ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[ПОМИЛКА ДАНИХ] Дані відхилено: {ex.Message}");
    Console.ResetColor();
}
catch (FileNotFoundException)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[ПОМИЛКА] Файл не знайдено: {filePath}");
    Console.ResetColor();
}
await Task.Delay(100); 

// [Крок 5.8 - ЛР №9] Підкоманди export та import для роботи з JSON-файлом конфігурації
if (subCmd is "export" or "import")
{
    var targetPath = customFilePathFromArgs(args) ?? fullDataPath;
    var jsonStore = new FileBookStore(targetPath);

    if (subCmd == "export")
    {
        // Наповнюємо сховище тестовими книгами (включно з кирилицею), якщо воно ще порожнє
        if (jsonStore.List().Count == 0)
        {
            foreach (var sample in SampleData.Books())
            {
                jsonStore.Add(sample);
            }
            jsonStore.Add(BookCopy.Create("BC-UA1", "978-966-03-4567-8", "Тіні забутих предків", 1911, "Михайло Коцюбинський"));
        }

        Console.WriteLine($"[EXPORT] Експортовано {jsonStore.List().Count} записів у файл: {jsonStore.FilePath}");
        return 0;
    }

    if (subCmd == "import")
    {
        try
        {
            var imported = jsonStore.List();
            Console.WriteLine($"[IMPORT] Успішно зчитано {imported.Count} записів із файлу: {jsonStore.FilePath}");
            foreach (var b in imported)
            {
                Console.WriteLine($"  {b.Id,-8} | {b.Isbn,-18} | {b.Title,-26} | {b.Year} | {b.Author}");
            }
            return 0;
        }
        catch (DomainException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ПОМИЛКА JSON] {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }
}

static string? customFilePathFromArgs(string[] arguments)
{
    int idx = Array.FindIndex(arguments, a => string.Equals(a, "--file", StringComparison.OrdinalIgnoreCase));
    return (idx != -1 && idx + 1 < arguments.Length && !arguments[idx + 1].StartsWith("--"))
        ? arguments[idx + 1]
        : null;
} 
// ==================== ЛАБОРАТОРНА РОБОТА №9 ====================
Console.WriteLine("\n==============================================================================");
Console.WriteLine("ШЛЯХИ, JSON, КОНФІГУРАЦІЯ, SECRETS");
Console.WriteLine("==============================================================================");
Console.WriteLine($"Профіль середовища (EnvironmentName): {options.EnvironmentName}");
Console.WriteLine($"Рівень логування (Logging:MinimumLevel): {options.Logging.MinimumLevel}");
Console.WriteLine($"Термін позики (Library:LoanDays): {options.Library.LoanDays} дн.");
Console.WriteLine($"Файл даних: {fullDataPath}");
Console.WriteLine(apiKey is null ? "ApiKey: не заданий" : $"ApiKey: присутній, довжина {apiKey.Length}");
Console.WriteLine("==============================================================================\n");

return 0;