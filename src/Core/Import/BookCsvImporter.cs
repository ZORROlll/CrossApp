using System.Text; 
using Core.Dto;    
using System.Globalization;

namespace Core.Import;

// 1. Модель даних для звіту про пропущений рядок
public record SkippedRowDto(int LineNumber, string RawLine, string Reason);

public static class BookCsvImporter
{
    private const char Separator = ';';

    // 2. Додаємо Action як необов'язковий параметр
    public static ImportResult<object> Load(
        string path, 
        Action<SkippedRowDto>? onRowSkipped = null)
    {
        var items = new List<object>();
        var errors = new List<string>();

        string[] lines = File.ReadAllLines(path, Encoding.UTF8);

        for (int i = 0; i < lines.Length; i++)
        {
            int number = i + 1;
            string line = lines[i];

            // Пропуск порожніх рядків або коментарів
            if (string.IsNullOrWhiteSpace(line))
            {
                onRowSkipped?.Invoke(new SkippedRowDto(number, line, "Порожній рядок"));
                continue;
            }

            if (line.StartsWith('#'))
            {
                onRowSkipped?.Invoke(new SkippedRowDto(number, line, "Рядок-коментар"));
                continue;
            }

            // Пропуск заголовка
            if (number == 1 && line.StartsWith("type", StringComparison.OrdinalIgnoreCase))
            {
                onRowSkipped?.Invoke(new SkippedRowDto(number, line, "Заголовок таблиці"));
                continue;
            }

            // Парсинг рядка
            switch (ParseLine(line))
            {
                case ParseOk ok: 
                    items.Add(ok.Value);
                    break;

                case ParseFailed failed: 
                    string errorMsg = $"рядок {number}: {failed.Reason}";
                    errors.Add(errorMsg);
                    
                    // 3. Сповіщаємо про некоректний рядок через Action
                    onRowSkipped?.Invoke(new SkippedRowDto(number, line, failed.Reason));
                    break;
            }
        }

        return new ImportResult<object>(items, errors);
    }

    private static ParseOutcome ParseLine(string line)
    {
        string[] parts = line.Split(Separator, StringSplitOptions.TrimEntries);

        return parts switch
        {
            ["B", _, "", _, ..] or ["B", _, _, "", ..] =>
                new ParseFailed("[Книга] ISBN або назва книги порожні"),

            ["B", _, _, _, var year, ..] when !int.TryParse(year, CultureInfo.InvariantCulture, out int y) || y < 1450 || y > DateTime.Now.Year =>
                new ParseFailed($"[Книга] рік '{year}' поза допустимими межами (1450-{DateTime.Now.Year})"),

            ["B", var id, var isbn, var title, var year] =>
                new ParseOk(new BookDto(id, isbn, title, int.Parse(year, CultureInfo.InvariantCulture))),

            ["B", var id, var isbn, var title, var year, var author] =>
                new ParseOk(new BookDto(id, isbn, title, int.Parse(year, CultureInfo.InvariantCulture), string.IsNullOrWhiteSpace(author) ? null : author)),

            ["B", ..] =>
                new ParseFailed($"[Книга] очікую 5-6 колонок, отримав {parts.Length}"),

            ["R", _, "", _] or ["R", _, _, ""] =>
                new ParseFailed("[Читач] ПІБ читача або номер квитка порожні"),

            ["R", var id, var name, var ticket] =>
                new ParseOk(new ReaderDto(id, name, ticket)),

            ["R", ..] =>
                new ParseFailed($"[Читач] очікую 4 колонки (R;id;name;ticket), отримав {parts.Length}"),

            [var prefix, ..] =>
                new ParseFailed($"невідомий тип запису з префіксом '{prefix}'"),

            _ => new ParseFailed("порожній рядок або невідомий формат")
        };
    }

    private abstract record ParseOutcome;
    private sealed record ParseOk(object Value) : ParseOutcome;
    private sealed record ParseFailed(string Reason) : ParseOutcome;
}