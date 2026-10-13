using System.Diagnostics;
using System.Text;
using Core;
using Core.Abstractions;
using Core.Domain;
using Core.Dto;
using Core.Import;
using Core.Services;
using Core.Storage;

Console.OutputEncoding = Encoding.UTF8;

// ==================== ІМПОРТ ДАНИХ ІЗ ФАЙЛУ ====================
string path = args.FirstOrDefault(a => !a.StartsWith("--")) ?? Path.Combine("data", "mixed.csv");

if (File.Exists(path))
{
    string extension = Path.GetExtension(path).ToLowerInvariant();

    ImportResult<object> result = extension switch
    {
        ".csv" => BookCsvImporter.Load(path),
        ".json" => BookJsonImporter.Load(path) is var jsonRes
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

    DomainImportResult<BookCopy> domainResult = BookImportProcessor.ToDomainEntities(result);

    Console.WriteLine($"Створено доменних сутностей: {domainResult.Entities.Count}");
    foreach (BookCopy entity in domainResult.Entities.Take(3))
    {
        Console.WriteLine($"  [Сутність] {entity}");
    }

    if (domainResult.InvariantErrors.Count > 0)
    {
        Console.WriteLine($"Відхилено записів через порушення інваріантів: {domainResult.InvariantErrors.Count}");
        foreach (string err in domainResult.InvariantErrors.Take(3))
        {
            Console.WriteLine($"  [Помилка] {err}");
        }
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

// Створення сховища делеговано фабриці StoreFactory (Додаткове завдання 3)
IBookStore store = StoreFactory.Create(args);
var service = new LendingService(store);

Console.WriteLine($"Активне сховище (через StoreFactory): {store.GetType().Name}");

var created = service.AddBook("BC-101", "978-0132350884", "Clean Architecture", 2017, "Robert Martin");
service.IssueCopy(created.Id);

foreach (var b in service.All())
{
    string status = b.IsIssued ? "Видана" : "В наявності";
    Console.WriteLine($" {b.Id,-8} {b.Isbn,-16} {b.Title,-22} {b.Year,5} [{status}]");
}

// ==================== ДОДАТКОВЕ ЗАВДАННЯ 1: ДЕКОРАТОР ====================
Console.WriteLine("\n=== Демонстрація роботи CachingBookStore ===");
Console.WriteLine("1. Перший пошук (читання з базового сховища та збереження в кеш):");
var firstRead = service.Find("BC-101");
Console.WriteLine($"   Знайдено: {firstRead?.Title}");

Console.WriteLine("2. Другий пошук (миттєве повернення екземпляра з кешу):");
var cachedRead = service.Find("BC-101");
Console.WriteLine($"   Знайдено з кешу: {cachedRead?.Title}");
Console.WriteLine($"   Один і той самий об'єкт у пам'яті: {ReferenceEquals(firstRead, cachedRead)}");

// ==================== ДОДАТКОВЕ ЗАВДАННЯ 2: ПОШУК З ПАРАМЕТРОМ Func ====================
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

// ==================== СЦЕНАРІЇ ВІДМОВИ ====================
Console.WriteLine("\n=== Сценарії відмови (обробка винятків) ===");

// 1. Спроба видачі примірника з неіснуючим ID
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

// 2. Спроба додати примірник із дублікатом ID
try
{
    Console.WriteLine($"Спроба повторного додавання книги з існуючим ID '{created.Id}'...");
    service.AddBook(created.Id, "978-0000000000", "Duplicate Book", 2024, "Unknown");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($" [Очікувана помилка]: {ex.Message}");
}

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