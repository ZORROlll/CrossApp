using System.Text;
using Core.Dto;

namespace Core.Import;

public static class BookCsvImporter
{
    private const char Separator = ';';

    public static ImportResult<object> Load(string path)
    {
        var items = new List<object>();
        var errors = new List<string>();

        string[] lines = File.ReadAllLines(path, Encoding.UTF8);

        for (int i = 0; i < lines.Length; i++)
        {
            int number = i + 1;
            string line = lines[i];

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            if (number == 1 && line.StartsWith("type", StringComparison.OrdinalIgnoreCase))
                continue;

            switch (ParseLine(line))
            {
                case ParseOk ok:
                    items.Add(ok.Value);
                    break;
                case ParseFailed failed:
                    errors.Add($"рядок {number}: {failed.Reason}");
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
            // 1. Книга за префіксом "B" -> створює BookDto
            ["B", _, "", _, ..] or ["B", _, _, "", ..] =>
                new ParseFailed("[Книга] ISBN або назва книги порожні"),

            ["B", _, _, _, var year, ..] when !int.TryParse(year, out int y) || y < 1450 || y > DateTime.Now.Year =>
                new ParseFailed($"[Книга] рік '{year}' поза допустимими межами (1450-{DateTime.Now.Year})"),

            ["B", var id, var isbn, var title, var year] =>
                new ParseOk(new BookDto(id, isbn, title, int.Parse(year))),

            ["B", var id, var isbn, var title, var year, var author] =>
                new ParseOk(new BookDto(id, isbn, title, int.Parse(year), string.IsNullOrWhiteSpace(author) ? null : author)),

            ["B", ..] =>
                new ParseFailed($"[Книга] очікую 5-6 колонок, отримав {parts.Length}"),

            // 2. Читач за префіксом "R" -> створює ReaderDto
            ["R", _, "", _] or ["R", _, _, ""] =>
                new ParseFailed("[Читач] ПІБ читача або номер квитка порожні"),

            ["R", var id, var name, var ticket] =>
                new ParseOk(new ReaderDto(id, name, ticket)),

            ["R", ..] =>
                new ParseFailed($"[Читач] очікую 4 колонки (R;id;name;ticket), отримав {parts.Length}"),

            // Невідомий префікс
            [var prefix, ..] =>
                new ParseFailed($"невідомий тип запису з префіксом '{prefix}'"),

            _ => new ParseFailed("порожній рядок або невідомий формат")
        };
    }

    private abstract record ParseOutcome;
    private sealed record ParseOk(object Value) : ParseOutcome;
    private sealed record ParseFailed(string Reason) : ParseOutcome;
}
