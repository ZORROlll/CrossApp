using System.Text;
using Core.Domain;
using Core.Dto;
using Core.Import;

Console.OutputEncoding = Encoding.UTF8;

// Імпорт даних із файлу (Лабораторна 3 + Додаткове 1)
string path = args.Length > 0 ? args[0] : Path.Combine("data", "mixed.csv");

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

    // Трансляція сирих даних у доменні сутності (Додаткове завдання 1)
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