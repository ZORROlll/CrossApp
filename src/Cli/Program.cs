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

Console.OutputEncoding = Encoding.UTF8;

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
    .FirstOrDefault() ?? Path.Combine("data", "mixed.csv");

// ==================== ІМПОРТ ДАНИХ ІЗ ФАЙЛУ ====================
if (File.Exists(importPath))
{
    string extension = Path.GetExtension(importPath).ToLowerInvariant();

    ImportResult<object> result = extension switch
    {
        ".csv" => BookCsvImporter.Load(importPath),
        ".json" => BookJsonImporter.Load(importPath) is var jsonRes
            ? new ImportResult<object>(jsonRes.Items.Cast<object>().ToList(), jsonRes.Errors)
            : new ImportResult<object>([], []),
        _ => new ImportResult<object>([], [$"Непідтримуване розширення '{extension}'. Дозволено: .csv, .json"])
    };

    Console.WriteLine($"Завантажено записів із файлу: {result.Items.Count}");

    foreach (object item in result.Items.Take(5))
    {
        switch (item)
        {
            case BookDto b:
                Console.WriteLine($" [Книга]  {b.Id,-6} {b.Isbn,-16} {b.Title,-25} {b.Year,5}  {b.Author ?? "-"}");
                break;
            case ReaderDto r:
                Console.WriteLine($" [Читач]  {r.Id,-6} {r.TicketNumber,-16} {r.FullName,-25}");
                break;
        }
    }

    if (result.Errors.Count > 0)
    {
        Console.WriteLine($"Пропущено записів на етапі парсингу: {result.Errors.Count}");
    }

    // ДОДАТКОВЕ ЗАВДАННЯ 2: передача Action<string> для оперативних сповіщень про відхилені рядки
    Console.WriteLine("\n[Додаткове завдання 2]: Звіт процесора імпорту через Action<string>:");
    DomainImportResult<BookCopy> domainResult = BookImportProcessor.ToDomainEntities(
        result,
        skippedMessage => Console.WriteLine($"   ! {skippedMessage}")
    );

    Console.WriteLine($"\nСтворено доменних сутностей: {domainResult.Entities.Count}");
    foreach (BookCopy entity in domainResult.Entities.Take(3))
    {
        Console.WriteLine($"  [Сутність] {entity}");
    }

    if (domainResult.InvariantErrors.Count > 0)
    {
        Console.WriteLine($"Загалом відхилено записів через порушення інваріантів/парсингу: {domainResult.InvariantErrors.Count}");
    }

    Console.WriteLine();
}

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

// ==================== РОБОТА ЗІ СХОВИЩЕМ ТА СЕРВІСОМ (COMPOSITION ROOT) ====================
Console.WriteLine("=== Робота з каталогом примірників ===");

// Шлях береться або введений вручну, або локальний catalog.json прямо в поточній папці запуску
string catalogDataPath = customFilePath ?? Path.Combine("data", "catalog.json");

// Вибір сховища залежно від прапорця --file
IBookStore baseStore = useFile
    ? new FileBookStore(catalogDataPath)
    : new InMemoryBookStore(SampleData.Books());

// Обгортання базового сховища декоратором
IBookStore store = new CachingBookStore(baseStore);
var service = new LendingService(store);

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

// ==================== ЗАВДАННЯ 2: ПОШУК З ПАРАМЕТРОМ Func ====================
Console.WriteLine("\n=== Демонстрація пошуку за предикатом (Func<BookCopy, bool>) ===");

Console.WriteLine("1. Пошук книг автора 'Martin':");
var byAuthor = service.Search(b => b.Author?.Contains("Martin", StringComparison.OrdinalIgnoreCase) == true);
foreach (var b in byAuthor)
{
    Console.WriteLine($"   -> [{b.Year}] {b.Title} ({b.Author})");
}

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
catch (InvalidOperationException ex)
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
catch (InvalidOperationException ex)
{
    Console.WriteLine($" [Очікувана помилка]: {ex.Message}");
}

// ==================== ЛАБОРАТОРНА РОБОТА 6: ДОМЕННІ ПОДІЇ ТА ДЕЛЕГАТИ ====================
Console.WriteLine("\n=== Доменні події, делегати Action/Func, лямбди ===");

// Крок 3: Налаштування шляху для лог-файлу та створення каталогу через Path.Combine
var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "app.log");
Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

// Локальний метод підписника (щоб згодом відписатися через -=)
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
service.Changed += OnLendingChanged;

// Крок 4: Другий підписник — лямбда із замиканням над лічильником changes
int changes = 0;
service.Changed += (_, _) => changes++;

// Крок 5: Демонстраційний сценарій у Cli
var createdBook = service.AddBook("978-0321125217", "Patterns of Enterprise Architecture", 2002, "Martin Fowler");
service.IssueCopy(createdBook.Id, "reader-42", DateTimeOffset.Now.AddDays(21));
service.ReturnCopy(createdBook.Id);

Console.WriteLine($"Подій отримано: {changes}");

// Керована відписка першого обробника
service.Changed -= OnLendingChanged;

// Тиха операція: перший обробник мовчить, але дані змінюються і лямбда фіксує +1
service.IssueCopy(createdBook.Id, "reader-99");
Console.WriteLine($"Подій отримано: {changes}"); // +1 лише від лямбди-лічильника

// Крок 6: Сценарій відмови (порушення бізнес-правила)
int beforeFailCount = changes;
try
{
    service.IssueCopy("non-existent-id", "reader-00");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"[Очікувана помилка]: {ex.Message}");
}

// Перевірка інваріанта: події не було, лічильник не змінився
if (changes == beforeFailCount)
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
var sinkBook = sinkService.AddBook("978-0131103627", "The C Programming Language", 1988, "Brian Kernighan");
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

return 0;