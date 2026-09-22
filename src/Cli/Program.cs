using Core.Dto;
using Core.Import;

string path = args.Length > 0 ? args[0] : Path.Combine("data", "mixed.csv");

if (!File.Exists(path))
{
    Console.WriteLine($"Файл не знайдено: {Path.GetFullPath(path)}");
    return 1;
}

string extension = Path.GetExtension(path).ToLowerInvariant();

ImportResult<object> result = extension switch
{
    ".csv" => BookCsvImporter.Load(path),
    ".json" => BookJsonImporter.Load(path) is var jsonRes 
        ? new ImportResult<object>(jsonRes.Items.Cast<object>().ToList(), jsonRes.Errors)
        : new ImportResult<object>([], []),
    _ => new ImportResult<object>([], [$"Непідтримуване розширення '{extension}'. Дозволено: .csv, .json"])
};

Console.WriteLine($"Завантажено записів: {result.Items.Count}");
foreach (object item in result.Items.Take(10))
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
    Console.WriteLine($"Пропущено записів: {result.Errors.Count}");
    foreach (string e in result.Errors)
    {
        Console.WriteLine($" ! {e}");
    }
}

// Пункт 3: Розрахунок статистики імпорту одним рядком
int accepted = result.Items.Count;
int skipped = result.Errors.Count;
int total = accepted + skipped;
double errorPercent = total > 0 ? (double)skipped / total * 100.0 : 0.0;

Console.WriteLine($"Статистика: усього: {total} | прийнято: {accepted} | пропущено: {skipped} | % помилок: {errorPercent:F1}%");

return 0;
