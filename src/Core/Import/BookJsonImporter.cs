using System.Text.Json; 
using Core.Dto;         

namespace Core.Import;

public static class BookJsonImporter
{
    // Налаштування парсера: ігноруємо регістр літер у назвах властивостей (наприклад, "title" чи "Title")
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Головний метод зчитування та валідації списку книг із JSON-файлу
    public static ImportResult<BookDto> Load(string path)
    {
        var items = new List<BookDto>();  // Список валідних книг, готових до збереження
        var errors = new List<string>(); 

        try
        {
            // Зчитуємо весь вміст файлу в один текстовий рядок
            string json = File.ReadAllText(path);

            var books = JsonSerializer.Deserialize<List<BookDto>>(json, Options) ?? [];

            // Перевіряємо кожну книгу на коректність даних
            for (int i = 0; i < books.Count; i++)
            {
                int number = i + 1;      
                var book = books[i];     // Поточна книга з масиву

                // Перевірка 1: чи заповнені обов'язкові поля Id та Title
                if (string.IsNullOrWhiteSpace(book.Id) || string.IsNullOrWhiteSpace(book.Title))
                {
                    errors.Add($"об'єкт {number}: Id або Title порожні");
                    continue; // Пропускаємо цю книгу і йдемо до наступної
                }

                // Перевірка 2: чи рік видання потрапляє в реалістичний діапазон (від 1450 до поточного)
                if (book.Year < 1450 || book.Year > DateTime.Now.Year)
                {
                    errors.Add($"об'єкт {number}: рік '{book.Year}' поза допустимими межами (1450-{DateTime.Now.Year})");
                    continue; // Пропускаємо цю книгу
                }

                // Якщо всі перевірки пройдено успішно — додаємо книгу до списку прийнятих
                items.Add(book);
            }
        }
        catch (JsonException ex) // Ловимо виняток, якщо сам файл містить синтаксично битий JSON (не вистачає дужки чи коми)
        {
            errors.Add($"Синтаксична помилка JSON: {ex.Message}");
        }

        // Повертаємо контейнер із двома списками: валідні книги та список помилок
        return new ImportResult<BookDto>(items, errors);
    }
}


 

