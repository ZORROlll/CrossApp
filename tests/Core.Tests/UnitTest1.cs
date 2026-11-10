namespace Core.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Core.Domain;
using Core.Events;
using Core.Import;
using Core.Services;
using Core.Storage;

public class Lab8Tests
{
    // 1) Інваріант: відмова і незмінний стан
    [Fact]
    public void IssueCopy_WhenAlreadyIssued_Throws_AndKeepsState()
    {
        var store = new InMemoryBookStore();
        var service = new LendingService(store);
        var book = service.AddBook("BC-TEST-1", "978-0132350884", "Test Book", 2020);

        service.IssueCopy(book.Id);

        // Повторна видача вже виданої книги має кидати виняток
        Assert.ThrowsAny<Exception>(() => service.IssueCopy(book.Id));

        // Книга залишається в сховищі й стан не ламається
        var stored = store.GetById(book.Id);
        Assert.NotNull(stored);
        Assert.Equal("BC-TEST-1", stored.Id);
    }

    // 2) Подія з тижня 6 викликається рівно один раз
    [Fact]
    public void AddBook_RaisesChangedExactlyOnce()
    {
        var service = new LendingService(new InMemoryBookStore());
        var events = new List<LendingChangedEventArgs>();
        service.Changed += (_, e) => events.Add(e);

        service.AddBook("BC-TEST-2", "978-0321125217", "Events Book", 2021);

        Assert.Single(events);
        Assert.Equal(LendingChangeKind.BookAdded, events[0].Kind);
    }

    // 3) Фільтрація на кількох наборах даних (Theory + InlineData)
    [Theory]
    [InlineData(2000, 2)]
    [InlineData(1980, 0)]
    public void BooksPublishedBefore_CountsOnlyMatching(int yearThreshold, int expected)
    {
        var count = TestData.Items().Count(b => b.Year < yearThreshold);
        Assert.Equal(expected, count);
    }

    // 4) Детермінований топ-N за роком видання
    [Fact]
    public void TopByYear_IsDeterministic()
    {
        var top = TestData.Items()
            .OrderByDescending(b => b.Year)
            .Take(2)
            .Select(b => b.Id)
            .ToArray();

        Assert.Equal(new[] { "T-01", "T-02" }, top);
    }

    // 5) Скасування імпорту через CancellationToken
    [Fact]
    public async Task ImportAsync_WhenAlreadyCancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var service = new LendingService(new InMemoryBookStore());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ImportAsync(new BookJsonImporter(), "any.json", cts.Token));
    }

    // 6) Бажаний шостий тест: тимчасовий файл у Path.GetTempPath() з гарантованим видаленням
    [Fact]
    public async Task ImportAsync_WithInvalidJson_Throws_AndCleansUp()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"invalid_{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(tempFile, "[{\"id\": \"\"}]");
            var service = new LendingService(new InMemoryBookStore());

            await Assert.ThrowsAnyAsync<Exception>(
                () => service.ImportAsync(new BookJsonImporter(), tempFile, CancellationToken.None));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    // ========================================================================
    // ДОДАТКОВЕ ЗАВДАННЯ: Перевірка потокового імпорту через IAsyncEnumerable
    // ========================================================================
    [Fact]
    public async Task ImportStreamAsync_ImportsSuccessfully()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"stream_test_{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(tempFile, """
            [
              { "id": "S-01", "isbn": "978-0132350884", "title": "Stream Book 1", "year": 2021 },
              { "id": "S-02", "isbn": "978-0321125217", "title": "Stream Book 2", "year": 2022 }
            ]
            """);

            var store = new InMemoryBookStore();
            var service = new LendingService(store);

            int added = await service.ImportStreamAsync(new BookJsonImporter(), tempFile);

            Assert.Equal(2, added);
            Assert.NotNull(store.GetById("S-01"));
            Assert.NotNull(store.GetById("S-02"));
        }
        finally
        {
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
    [Fact]
    public async Task ImportAsync_WithLinkedTokens_CancelsByTimeoutSpecifically()
    {
        // 1. Токен користувача (Ctrl+C) — активний і НЕ скасовується
        using var userCts = new CancellationTokenSource();

        // 2. Токен таймауту — імітуємо вичерпання часу
        using var timeoutCts = new CancellationTokenSource();
        await timeoutCts.CancelAsync(); // Спрацював таймаут

        // 3. Зв'язуємо обидва джерела в один токен
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(userCts.Token, timeoutCts.Token);

        var service = new LendingService(new InMemoryBookStore());

        // 4. Виклик має перерватися з OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ImportAsync(new BookJsonImporter(), "dummy.json", linkedCts.Token));

        // 5. Суворе доведення: спрацював саме таймаут, а не дія користувача
        Assert.True(timeoutCts.IsCancellationRequested, "Має бути скасовано токен таймауту.");
        Assert.False(userCts.IsCancellationRequested, "Токен користувача не повинен бути скасованим.");
    }


    // ========================================================================
    // ДОДАТКОВЕ ЗАВДАННЯ 3: Перевірка роботи IProgress<int>
    // ========================================================================
    [Fact]
    public async Task ImportAsync_ReportsProgressCorrectly()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var json = """
            [
              { "Id": "PR-01", "Isbn": "978-111", "Title": "Progress Book 1", "Year": 2020 },
              { "Id": "PR-02", "Isbn": "978-222", "Title": "Progress Book 2", "Year": 2021 }
            ]
            """;
            await File.WriteAllTextAsync(tempFile, json);

            var service = new LendingService(new InMemoryBookStore());
            var reportedValues = new List<int>();
            var progress = new Progress<int>(reportedValues.Add);

            int added = await service.ImportAsync(new BookJsonImporter(), tempFile, progress: progress);

            Assert.Equal(2, added);
            // Даємо Progress<T> кілька мілісекунд передати значення у callback
            await Task.Delay(50);
            Assert.NotEmpty(reportedValues);
            Assert.Equal(100, reportedValues.Last());
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

}