using System.Text.Json;
using Core.Dto;

namespace Core.Import;

public static class BookJsonImporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static ImportResult<BookDto> Load(string path)
    {
        var items = new List<BookDto>();
        var errors = new List<string>();

        try
        {
            string json = File.ReadAllText(path);
            var books = JsonSerializer.Deserialize<List<BookDto>>(json, Options) ?? [];

            for (int i = 0; i < books.Count; i++)
            {
                int number = i + 1;
                var book = books[i];

                if (string.IsNullOrWhiteSpace(book.Id) || string.IsNullOrWhiteSpace(book.Title))
                {
                    errors.Add($"об'єкт {number}: Id або Title порожні");
                    continue;
                }

                if (book.Year < 1450 || book.Year > DateTime.Now.Year)
                {
                    errors.Add($"об'єкт {number}: рік '{book.Year}' поза допустимими межами (1450-{DateTime.Now.Year})");
                    continue;
                }

                items.Add(book);
            }
        }
        catch (JsonException ex)
        {
            errors.Add($"Синтаксична помилка JSON: {ex.Message}");
        }

        return new ImportResult<BookDto>(items, errors);
    }
}
