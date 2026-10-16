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
    // Метод приймає результат з тижня 3 та Action<string> для сповіщення про пропущені рядки в момент їх виявлення
    public static DomainImportResult<BookCopy> ToDomainEntities(
        ImportResult<object> importResult,
        Action<string>? onSkipped = null)
    {
        if (importResult is null)
            throw new ArgumentNullException(nameof(importResult), "Результат імпорту не може бути null");

        var validEntities = new List<BookCopy>();
        var invariantErrors = new List<string>();

        // 1. Помилки етапу парсингу файлу
        foreach (var parseError in importResult.Errors)
        {
            invariantErrors.Add(parseError);
            onSkipped?.Invoke($"[Пропущено на етапі парсингу]: {parseError}");
        }

        // 2. Обробка кожного успішно розпарсеного об'єкта
        foreach (object item in importResult.Items)
        {
            if (item is BookDto bookDto)
            {
                try
                {
                    // Спроба відновити сутність через FromDto (перевірка інваріантів)
                    BookCopy entity = BookCopy.FromDto(bookDto);
                    validEntities.Add(entity);
                }
                catch (Exception ex)
                {
                    // Фіксація порушення інваріантів доменної сутності
                    var errorMessage = $"Запис книги [Id: '{bookDto.Id}', ISBN: '{bookDto.Isbn}'] відхилено доменом: {ex.Message}";
                    invariantErrors.Add(errorMessage);

                    // Миттєвий звіт через делегат Action<string>
                    onSkipped?.Invoke($"[Пропущено через інваріант]: {errorMessage}");
                }
            }
        }

        return new DomainImportResult<BookCopy>(
            validEntities.AsReadOnly(),
            invariantErrors.AsReadOnly());
    }
}