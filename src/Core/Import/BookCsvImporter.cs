using System.Text; 
using Core.Dto;    
using System.Globalization;

namespace Core.Import;

public static class BookCsvImporter
{
    
    private const char Separator = ';';

    // Головний метод завантаження даних за шляхом до файлу
    public static ImportResult<object> Load(string path)
    {
        var items = new List<object>();  // Список для успішно створених об'єктів (книг і читачів)
        var errors = new List<string>(); // Список для повідомлень про помилки в рядках

        // Зчитуємо всі рядки файлу у масив із примусовим UTF-8 кодуванням
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);

        // Проходимо по кожному рядку файлу
        for (int i = 0; i < lines.Length; i++)
        {
            int number = i + 1;       // Номер рядка для користувача (починається з 1, а не з 0)
            string line = lines[i];   // Поточний текстовий рядок

            // Пропускаємо порожні рядки або рядки-коментарі (що починаються з '#')
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            // Пропускаємо перший рядок, якщо це заголовок таблиці (починається зі слова "type")
            if (number == 1 && line.StartsWith("type", StringComparison.OrdinalIgnoreCase))
                continue;

            // Перевіряємо результат парсингу рядка через pattern matching по типу
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

        // Повертаємо єдиний об'єкт із двома готовими списками
        return new ImportResult<object>(items, errors);
    }

    // Допоміжний метод: розбирає один конкретний рядок на об'єкт або помилку
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
    // Базовий тип для повернення результату (успіх або помилка)
    private abstract record ParseOutcome;
    
    // Результат успіху: несе всередині створений об'єкт (книгу або читача)
    private sealed record ParseOk(object Value) : ParseOutcome;
    
    // Результат невдачі: несе всередині текстову причину помилки
    private sealed record ParseFailed(string Reason) : ParseOutcome;
}