namespace Core.Import;

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Core;
using Core.Abstractions;
using Core.Domain;
using Core.Dto;

public sealed class BookJsonImporter : IBookImporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<BookCopy>> LoadAsync(string path, CancellationToken ct = default)
    {
        // Перериває операцію (кидає OperationCanceledException), якщо надійшов сигнал скасування
        ct.ThrowIfCancellationRequested();

        // Відкриття файлового потоку з асинхронним звільненням ресурсів (DisposeAsync)
        await using var stream = new FileStream(
            path,
            FileMode.Open, // вказує ос відкрити вже існуючий файл. Якщо файлу немає — буде виняток FileNotFoundException.
            FileAccess.Read, 
            FileShare.Read, // спільний доступ: дозволяє іншим процесам у системі одночасно читати цей файл, поки він відкритий у твоїй програмі.
            bufferSize: 4096,
            useAsync: true); // відкриття файлу в асинхронному режимі, щоб не блокувати фонові потоки

        var dtos = await JsonSerializer.DeserializeAsync<List<BookDto>>( // (список проміжних об'єктів для передачі даних — Data Transfer Objects). 
            stream, // це файловий потік (FileStream),
            Options, // Налаштування серіалізатора (наприклад, нечутливість до регістру назв полів)
            cancellationToken: ct) // Перериває зчитування та парсинг JSON, якщо надійшов сигнал скасування
            ?? throw new DomainException($"Файл {Path.GetFileName(path)} не містить списку."); // якщо файл порожній або некоректний за змістом

        // Створення результуючого списку з попереднім виділенням пам'яті під розмір вхідного масиву
        var result = new List<BookCopy>(dtos.Count);

        


        for (var i = 0; i < dtos.Count; i++)
{
    ct.ThrowIfCancellationRequested();

    try
    {
        result.Add(BookCopy.FromDto(dtos[i]));
    }
    catch (Exception ex)
    {
        // Це зловить БУДЬ-ЯКУ помилку
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Дані відхилено: запис #{i + 1} ({ex.Message})");
        Console.ResetColor();
    }
}
        return result;
    }

    // ========================================================================
    // ДОДАТКОВЕ ЗАВДАННЯ: Потоковий імпорт через IAsyncEnumerable
    // ========================================================================
    // Оголошення асинхронного методу-генератора, який повертає потік об'єктів BookCopy частинами через IAsyncEnumerable
    public async IAsyncEnumerable<BookCopy> StreamAsync(
        string path, // Вхідний параметр: шлях до файлу JSON на диску
        [EnumeratorCancellation] CancellationToken ct = default) // Атрибут зв'язує зовнішній токен із циклу await foreach із локальною змінною ct
    {
        // Перевіряємо токен перед початком роботи: якщо операцію вже скасовано — одразу кидаємо OperationCanceledException
        ct.ThrowIfCancellationRequested();

        // Асинхронно відкриваємо файловий потік для читання (await using гарантує коректне закриття потоку навіть при помилці)
        await using var stream = new FileStream(
            path, // Шлях до цільового файлу
            FileMode.Open, // Режим відкриття: відкрити вже існуючий файл
            FileAccess.Read, // Доступ тільки на читання
            FileShare.Read, // Дозволяємо іншим процесам паралельно читати цей файл
            bufferSize: 4096, // Розмір буфера читання — 4 КБ
            useAsync: true); // Вмикає справжній асинхронний ввід-вивід на рівні операційної системи (без блокування потоку)

        // Лічильник прочитаних записів для логування порядкового номера
        var index = 0;

        // Потокове десеріалізування JSON: читає з диска і повертає по одному DTO за раз, не завантажуючи весь файл у RAM
        await foreach (var dto in JsonSerializer.DeserializeAsyncEnumerable<BookDto>(stream, Options, ct))
        {
            // Перевіряємо, чи не надійшов сигнал скасування перед обробкою чергового елемента
            ct.ThrowIfCancellationRequested();

            // Якщо десеріалізатор повернув null (порожній запис у JSON) — пропускаємо його
            if (dto is null) 
                continue;

            // Змінна для збереження валідного доменного об'єкта
            BookCopy copy;
            try
            {
                // Створюємо доменну сутність із DTO (тут спрацьовує перевірка валідності даних)
                copy = BookCopy.FromDto(dto);
            }
            // Перехоплюємо виключно помилки валідації аргументів або порушення доменних бізнес-правил
            catch (Exception ex) when (ex is ArgumentException or DomainException)
            {
                // Виводимо в консоль повідомлення про те, який саме запис було відхилено та чому
                Console.WriteLine($"Дані відхилено: запис #{index + 1} ({ex.Message})");
                // Збільшуємо лічильник записів
                index++;
                // Пропускаємо цей некоректний запис і переходимо до наступного в потоці
                continue;
            }

            // Збільшуємо лічильник для успішно обробленого запису
            index++;
            // Повертаємо створений об'єкт тому, хто викликав метод, і призупиняємо виконання до наступного запиту
            yield return copy;
        }
    }
}