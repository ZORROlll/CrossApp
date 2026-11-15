namespace Core.Services;

using Core;
using Core.Abstractions;
using Core.Domain;
using Core.Events;
using Core.Import;

public sealed class LendingService(IBookStore store, INotificationSink? sink = null)
{
    private readonly IBookStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly INotificationSink? _sink = sink;

    // Подія сповіщення про зміну стану книги
    public event EventHandler<LendingChangedEventArgs>? Changed;

    // Приватний метод для безпечного підняття події та відправки в sink
    private void OnChanged(string copyId, LendingChangeKind kind, string? readerId = null, DateTimeOffset? dueDate = null)
    {
        // Основна доменна подія (1) зміна стану
        Changed?.Invoke(this, new LendingChangedEventArgs(copyId, kind, readerId, dueDate));

        // Сповіщення через абстракцію приймача (Додаткове завдання)
        _sink?.Notify($"{DateTimeOffset.Now:HH:mm:ss} {kind} {copyId} (reader: {readerId ?? "none"})");
    }

    public BookCopy AddBook(string id, string isbn, string title, int year, string? author = null)
    {
        var book = BookCopy.Create(id, isbn, title, year, author);
        _store.Add(book); // стан збережено успішно

        // Підняття події після успішного додавання до сховища
        OnChanged(book.Id, LendingChangeKind.BookAdded);
        return book;
    }

    public BookCopy AddBook(string isbn, string title, int year, string? author = null)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        return AddBook(id, isbn, title, year, author);
    }

    public void IssueCopy(string id, string? readerId = "reader-01", DateTimeOffset? dueDate = null)
    {
        var book = _store.GetById(id)
            ?? throw new DomainException($"Примірник з id={id} не знайдено.");

        book.Issue();
        _store.Update(book);

        // Якщо book.Issue() кине виняток — виконання перерветься, і цей рядок не спрацює
        OnChanged(book.Id, LendingChangeKind.CopyIssued, readerId, dueDate ?? DateTimeOffset.Now.AddDays(14));
    }

    public void ReturnCopy(string id)
    {
        var book = _store.GetById(id)
            ?? throw new DomainException($"Примірник з id={id} не знайдено.");

        book.Return();
        _store.Update(book);

        // Підняття події після успішного повернення
        OnChanged(book.Id, LendingChangeKind.CopyReturned);
    }

    public IReadOnlyList<BookCopy> All() => _store.List();

    public BookCopy? Find(string id) => _store.GetById(id);

    public IReadOnlyList<BookCopy> Search(Func<BookCopy, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return _store.Find(predicate);
    }

    //  пошук за предикатом за допомогою Func
    // Фільтрує примірники книг за переданою умовою-предикатом
    public IReadOnlyList<BookCopy> FindAll(Func<BookCopy, bool> predicate) =>
        _store.List()
        .Where(predicate)
        .ToList();

// ==================== ЛАБОРАТОРНА РОБОТА 8: КРОК 4 ====================
    // Оголошення асинхронного методу імпорту, який повертає кількість успішно доданих книг Task<int>
    public async Task<int> ImportAsync(
        IBookImporter importer, // Параметр: інтерфейс імпортера, який читає файл певного формату (наприклад, JSON)
        string path, // Параметр: повний шлях до файлу з даними на диску
        CancellationToken ct = default, // Токен для можливості скасування тривалої асинхронної операції (за замовчуванням порожній)
        // >>> ДОДАТКОВЕ ЗАВДАННЯ 3: IProgress (опціональний параметр зі значенням default) <<<
        IProgress<int>? progress = null) // Опціональний об'єкт для надсилання звітів про прогрес (відсотки від 0 до 100)
    {
        // Перевіряємо вхідний параметр importer: якщо передали null — одразу викидаємо виняток ArgumentNullException
        ArgumentNullException.ThrowIfNull(importer);

        // Асинхронно вичитуємо весь список книг із файлу через переданий імпортер із підтримкою скасування через токен ct
        var items = await importer.LoadAsync(path, ct);
        // Лічильник фактично збережених у сховище нових книг (початкове значення 0)
        var added = 0;
        // Загальна кількість записів, отриманих із файлу (потрібна для обчислення відсотків)
        var total = items.Count;

        // Звичайний цикл for для послідовної обробки кожного прочитаного запису
        for (int i = 0; i < total; i++)
        {
            // Перевіряємо токен скасування на кожній ітерації: якщо надійшов сигнал переривання — кидаємо OperationCanceledException
            ct.ThrowIfCancellationRequested();

            // Отримуємо поточну книгу з масиву/списку за індексом i
            var item = items[i];

            // Ідемпотентність імпорту: перевіряємо у сховищі, чи книга з таким ID вже існує
            if (_store.GetById(item.Id) is not null) 
            {
                // >>> ДОДАТКОВЕ ЗАВДАННЯ 3: Звітуємо прогрес навіть якщо елемент пропущено <<<
                // Розраховуємо відсоток виконаної роботи: (поточний номер кроку * 100) / загальна кількість
                int currentPercent = (int)((i + 1) * 100.0 / total);
                // Якщо об'єкт progress був переданий (не null) — надсилаємо обчислений відсоток підписнику
                progress?.Report(currentPercent);
                // Пропускаємо поточну ітерацію циклу, щоб не дублювати вже наявну книгу
                continue;
            }

            // Додаємо нову валідну книгу в сховище даних
            _store.Add(item);
            // Піднімаємо доменну подію Changed із типом BookAdded, сповіщаючи підписників про додавання книги
            OnChanged(item.Id, LendingChangeKind.BookAdded);
            // Збільшуємо лічильник успішно імпортованих унікальних книг
            added++;

            // >>> ДОДАТКОВЕ ЗАВДАННЯ 3: Відправляємо поточний відсоток виконання (0..100%) <<<
            // Розраховуємо прогрес після успішного збереження поточного елемента
            int percent = (int)((i + 1) * 100.0 / total);
            // Надсилаємо новий відсоток підписнику (наприклад, у CLI для оновлення рядка прогресу)
            progress?.Report(percent);
        }

        // Повертаємо загальну кількість фактично доданих у сховище книг
        return added;
    }

// ==================== ДОДАТКОВЕ ЗАВДАННЯ 1: ПОТОКОВИЙ ІМПОРТ ====================
// Додаткове завдання: імпорт через await foreach та IAsyncEnumerable
    // Оголошення асинхронного методу потокового імпорту без завантаження всього файлу в пам'ять
    public async Task<int> ImportStreamAsync(IBookImporter importer, string path, CancellationToken ct = default)
    {
        // Перевірка на null для об'єкта імпортера на вході в метод
        ArgumentNullException.ThrowIfNull(importer);

        // Лічильник успішно збережених книг
        var added = 0;

        // Цикл await foreach: асинхронно отримує елементи по одному з IAsyncEnumerable, а WithCancellation передає токен скасування в ітератор
        await foreach (var item in importer.StreamAsync(path, ct).WithCancellation(ct))
        {
            // Якщо книга з таким ID уже є в базі/сховищі — пропускаємо її
            if (_store.GetById(item.Id) is not null)
                continue;

            // Зберігаємо нову книгу в оперативне сховище
            _store.Add(item);
            // Генеруємо подію додавання книги для зовнішніх підписників
            OnChanged(item.Id, LendingChangeKind.BookAdded);
            // Збільшуємо лічильник доданих книг
            added++;
        }

        // Повертаємо підсумкову кількість імпортованих записів
        return added;
    }
}