using Core.Domain;
using Core.Dto;

namespace Core.Import;

// Результат доменної валідації: валідні сутності + помилки інваріантів
public sealed record DomainImportResult<TEntity>(
    IReadOnlyList<TEntity> Entities,
    IReadOnlyList<string> InvariantErrors
);

public static class BookImportProcessor
{
    // Метод приймає результат з тижня 3 та повертає чисті доменні сутності та список відхилених рядків
    public static DomainImportResult<BookCopy> ToDomainEntities(ImportResult<object> importResult)
    {
        if (importResult is null)
            throw new ArgumentNullException(nameof(importResult), "Результат імпорту не може бути null");

        var validEntities = new List<BookCopy>();
        var invariantErrors = new List<string>();

        // Переносимо помилки рівня парсингу файлу, якщо вони вже були
        invariantErrors.AddRange(importResult.Errors);

        // Обробляємо кожен успішно розпарсений об'єкт
        foreach (object item in importResult.Items)
        {
            if (item is BookDto bookDto)
            {
                try
                {
                    // Спроба відновити сутність через FromDto (проходить усі інваріанти Create)
                    BookCopy entity = BookCopy.FromDto(bookDto);
                    validEntities.Add(entity);
                }
                catch (Exception ex)
                {
                    // Якщо сутність відмовилася створюватися — фіксуємо рядок та правило, що зламалося
                    invariantErrors.Add(
                        $"Запис книги [Id: '{bookDto.Id}', ISBN: '{bookDto.Isbn}'] відхилено доменом: {ex.Message}");
                }
            }
        }

        return new DomainImportResult<BookCopy>(
            validEntities.AsReadOnly(),
            invariantErrors.AsReadOnly());
    }
}