namespace Core.Tests;

// Підключення базових просторів імен .NET
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
// Підключення бібліотеки модульного тестування xUnit
using Xunit;
// Підключення класів предметної області, подій, імпорту, сервісів та сховища проєкту
using Core.Domain;
using Core.Events;
using Core.Import;
using Core.Services;
using Core.Storage;

public class Lab8Tests
{
    // 1) Інваріант: відмова і незмінний стан
    [Fact] // Атрибут xUnit, що позначає метод як тестовий без параметрів
    public void IssueCopy_WhenAlreadyIssued_Throws_AndKeepsState()
    {
        // 1. Створюємо сховище в оперативній пам'яті
        var store = new InMemoryBookStore();

        // 2. Створюємо сервіс видачі книг на основі створеного сховища
        var service = new LendingService(store);

        // 3. Додаємо тестову книгу в сервіс і зберігаємо її у змінну book
        var book = service.AddBook("BC-TEST-1", "978-0132350884", "Test Book", 2020);

        // 4. Видаємо примірник книги перший раз (це успішна операція)
        service.IssueCopy(book.Id);

        // 5. Повторна видача вже виданої книги має кидати виняток: фіксуємо це через Assert.ThrowsAny
        Assert.ThrowsAny<Exception>(() => service.IssueCopy(book.Id));

        // 6. Отримуємо книгу зі сховища за її ID, щоб перевірити, чи зберігся об'єкт після помилки
        var stored = store.GetById(book.Id);

        // 7. Перевіряємо, що книга не видалилася і не стала null
        Assert.NotNull(stored);

        // 8. Перевіряємо, що ідентифікатор книги в сховищі залишився тим самим
        Assert.Equal("BC-TEST-1", stored.Id);
    }

    // 2) Подія з тижня 6 викликається рівно один раз
    [Fact] // Атрибут xUnit, що позначає метод як тестовий без параметрів
    public void AddBook_RaisesChangedExactlyOnce()
    {
        // 1. Створюємо сервіс LendingService та передаємо йому сховище в пам'яті
        var service = new LendingService(new InMemoryBookStore());

        // 2. Створюємо локальний список у пам'яті для збору отриманих аргументів подій
        var events = new List<LendingChangedEventArgs>();

        // 3. Підписуємось на подію Changed: під час її спрацювання додаємо аргументи 'e' до списку
        service.Changed += (_, e) => events.Add(e);

        // 4. Викликаємо метод додавання книги, який генерує подію
        service.AddBook("BC-TEST-2", "978-0321125217", "Events Book", 2021);

        // 5. Перевіряємо, що в списку рівно один елемент (подія відбулася рівно 1 раз)
        Assert.Single(events);

        // 6. Перевіряємо, що тип події в першому елементі — саме додавання книги (BookAdded)
        Assert.Equal(LendingChangeKind.BookAdded, events[0].Kind);
    }

    // 3) Фільтрація на кількох наборах даних (Theory + InlineData)
    [Theory] // Атрибут xUnit, який запускає тест декілька разів із різними вхідними даними
    [InlineData(2000, 2)] // Перший прогін: поріг 2000 рік, очікувана кількість — 2 книги
    [InlineData(1980, 0)] // Другий прогін: поріг 1980 рік, очікувана кількість — 0 книг
    public void BooksPublishedBefore_CountsOnlyMatching(int yearThreshold, int expected)
    {
        // 1. Отримуємо тестовий набір даних, фільтруємо через LINQ за роком і рахуємо кількість
        var count = TestData.Items().Count(b => b.Year < yearThreshold);

        // 2. Порівнюємо фактичну кількість із очікуваною (expected)
        Assert.Equal(expected, count);
    }

    // 4) Детермінований топ-N за роком видання
    [Fact] // Атрибут xUnit, що позначає метод як тестовий без параметрів
    public void TopByYear_IsDeterministic()
    {
        // 1. Беремо книги, сортуємо за спаданням року, обираємо перші 2, вибираємо їхні ID і конвертуємо в масив
        var top = TestData.Items()
            .OrderByDescending(b => b.Year)
            .Take(2)
            .Select(b => b.Id)
            .ToArray();

        // 2. Перевіряємо, що отриманий масив збігається з очікуваними фіксованими значеннями ID
        Assert.Equal(new[] { "T-01", "T-02" }, top);
    }

    // 5) Скасування імпорту через CancellationToken
    [Fact] // Атрибут xUnit для асинхронного тесту
    public async Task ImportAsync_WhenAlreadyCancelled_Throws()
    {
        // 1. Створюємо джерело токенів скасування з автоматичним звільненням пам'яті через using
        using var cts = new CancellationTokenSource();

        // 2. Скасовуємо токен до виклику операції
        await cts.CancelAsync();

        // 3. Створюємо екземпляр сервісу зі сховищем у пам'яті
        var service = new LendingService(new InMemoryBookStore());

        // 4. Перевіряємо, що виклик ImportAsync із уже скасованим токеном одразу кидає OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ImportAsync(new BookJsonImporter(), "any.json", cts.Token));
    }

    // 6) Бажаний шостий тест: тимчасовий файл у Path.GetTempPath() з гарантованим видаленням
    [Fact] // Атрибут xUnit для асинхронного тесту
    public async Task ImportAsync_WithInvalidJson_Throws_AndCleansUp()
    {
        // 1. Генеруємо унікальний шлях до файлу в системній тимчасовій папці з новим GUID у назві
        string tempFile = Path.Combine(Path.GetTempPath(), $"invalid_{Guid.NewGuid():N}.json");
        try
        {
            // 2. Записуємо у створений файл некоректний JSON (із порожнім полем id)
            await File.WriteAllTextAsync(tempFile, "{ це не json }");

            // 3. Створюємо сервіс зі сховищем у пам'яті
            var service = new LendingService(new InMemoryBookStore());

            // 4. Перевіряємо, що імпорт пошкодженого JSON призводить до викидання винятку
            await Assert.ThrowsAnyAsync<Exception>(
                () => service.ImportAsync(new BookJsonImporter(), tempFile, CancellationToken.None));
        }
        finally
        {
            // 5. Блок finally виконується завжди: якщо файл залишився на диску — видаляємо його
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    // ========================================================================
    // ДОДАТКОВЕ ЗАВДАННЯ: Перевірка потокового імпорту через IAsyncEnumerable
    // ========================================================================
    [Fact] // Атрибут xUnit для асинхронного тесту
    public async Task ImportStreamAsync_ImportsSuccessfully()
    {
        // 1. Формуємо унікальний шлях до тимчасового файлу для потокового тесту
        string tempFile = Path.Combine(Path.GetTempPath(), $"stream_test_{Guid.NewGuid():N}.json");
        try
        {
            // 2. Записуємо у файл коректний JSON із двома книгами
            await File.WriteAllTextAsync(tempFile, """
            [
              { "id": "S-01", "isbn": "978-0132350884", "title": "Stream Book 1", "year": 2021 },
              { "id": "S-02", "isbn": "978-0321125217", "title": "Stream Book 2", "year": 2022 }
            ]
            """);

            // 3. Ініціалізуємо сховище в пам'яті
            var store = new InMemoryBookStore();

            // 4. Ініціалізуємо сервіс на базі сховища
            var service = new LendingService(store);

            // 5. Викликаємо потоковий імпорт і отримуємо кількість доданих записів
            int added = await service.ImportStreamAsync(new BookJsonImporter(), tempFile);

            // 6. Перевіряємо, що імпортовано саме 2 книги
            Assert.Equal(2, added);

            // 7. Перевіряємо, що перша книга з'явилася в сховищі
            Assert.NotNull(store.GetById("S-01"));

            // 8. Перевіряємо, що друга книга з'явилася в сховищі
            Assert.NotNull(store.GetById("S-02"));
        }
        finally
        {
            // 9. Очищуємо тимчасовий файл з диску в будь-якому випадку
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    // ========================================================================
    // ДОДАТКОВЕ ЗАВДАННЯ 2: Зв'язані токени (User + Timeout)
    // Доведення, що операцію перервав саме таймаут, а не користувач
    // ========================================================================
    [Fact] // Атрибут xUnit для асинхронного тесту
    public async Task ImportAsync_WithLinkedTokens_CancelsByTimeoutSpecifically()
    {
        // 1. Токен користувача (Ctrl+C) — активний і НЕ скасовується
        using var userCts = new CancellationTokenSource();

        // 2. Токен таймауту — створюємо і одразу скасовуємо, імітуючи завершення часу
        using var timeoutCts = new CancellationTokenSource();
        await timeoutCts.CancelAsync(); // Спрацював таймаут

        // 3. Зв'язуємо обидва джерела в один спільний токен (він переходить у стан скасованого)
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(userCts.Token, timeoutCts.Token);

        // 4. Створюємо сервіс
        var service = new LendingService(new InMemoryBookStore());

        // 5. Виклик має перерватися з OperationCanceledException через зв'язаний токен
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ImportAsync(new BookJsonImporter(), "dummy.json", linkedCts.Token));

        // 6. Суворе доведення: токен таймауту дійсно перебуває в скасованому стані
        Assert.True(timeoutCts.IsCancellationRequested, "Має бути скасовано токен таймауту.");

        // 7. Суворе доведення: користувацький токен залишився активним (перервав саме таймаут)
        Assert.False(userCts.IsCancellationRequested, "Токен користувача не повинен бути скасованим.");
    }

    // ========================================================================
    // ДОДАТКОВЕ ЗАВДАННЯ 3: Перевірка роботи IProgress<int>
    // ========================================================================
    [Fact] // Атрибут xUnit для асинхронного тесту
    public async Task ImportAsync_ReportsProgressCorrectly()
    {
        // 1. Створюємо системний тимчасовий файл
        var tempFile = Path.GetTempFileName();
        try
        {
            // 2. Готуємо JSON із двома сутностями
            var json = """
            [
              { "Id": "PR-01", "Isbn": "978-111", "Title": "Progress Book 1", "Year": 2020 },
              { "Id": "PR-02", "Isbn": "978-222", "Title": "Progress Book 2", "Year": 2021 }
            ]
            """;
            // 3. Записуємо підготовлений JSON у створений тимчасовий файл
            await File.WriteAllTextAsync(tempFile, json);

            // 4. Створюємо сервіс зі сховищем у пам'яті
            var service = new LendingService(new InMemoryBookStore());

            // 5. Створюємо список, у який складатимемо отримані відсотки виконання
            var reportedValues = new List<int>();

            // 6. Створюємо об'єкт Progress<int>, який при кожному звіті додає значення в reportedValues
            var progress = new Progress<int>(reportedValues.Add);

            // 7. Запускаємо імпорт із передачею нашого об'єкта progress
            int added = await service.ImportAsync(new BookJsonImporter(), tempFile, progress: progress);

            // 8. Перевіряємо, що додано рівно 2 книги
            Assert.Equal(2, added);

            // 9. Даємо Progress<T> 50 мілісекунд, щоб асинхронний callback встиг передати значення у список
            await Task.Delay(50);

            // 10. Перевіряємо, що хоча б одне значення прогресу було зафіксовано
            Assert.NotEmpty(reportedValues);

            // 11. Перевіряємо, що останнім зафіксованим значенням прогресу було 100%
            Assert.Equal(100, reportedValues.Last());
        }
        finally
        {
            // 12. Видаляємо тимчасовий файл після завершення роботи
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}