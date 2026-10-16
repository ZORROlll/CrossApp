namespace Core.Services;

using Core.Abstractions;
using Core.Domain;
using Core.Events;

public sealed class LendingService(IBookStore store, INotificationSink? sink = null)
{
    private readonly IBookStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly INotificationSink? _sink = sink;

    // Подія сповіщення про зміну стану книги
    public event EventHandler<LendingChangedEventArgs>? Changed;

    // Приватний метод для безпечного підняття події та відправки в sink
    private void OnChanged(string copyId, LendingChangeKind kind, string? readerId = null, DateTimeOffset? dueDate = null)
    {
        // Основна доменна подія (Лабораторна 6)
        Changed?.Invoke(this, new LendingChangedEventArgs(copyId, kind, readerId, dueDate));

        // Сповіщення через абстракцію приймача (Додаткове завдання)
        _sink?.Notify($"{DateTimeOffset.Now:HH:mm:ss} {kind} {copyId} (reader: {readerId ?? "none"})");
    }

    public BookCopy AddBook(string id, string isbn, string title, int year, string? author = null)
    {
        var book = BookCopy.Create(id, isbn, title, year, author);
        _store.Add(book);

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
            ?? throw new InvalidOperationException($"Примірник з id={id} не знайдено.");

        book.Issue();
        _store.Update(book);

        // Якщо book.Issue() кине виняток — виконання перерветься, і цей рядок не спрацює
        OnChanged(book.Id, LendingChangeKind.CopyIssued, readerId, dueDate ?? DateTimeOffset.Now.AddDays(14));
    }

    public void ReturnCopy(string id)
    {
        var book = _store.GetById(id)
            ?? throw new InvalidOperationException($"Примірник з id={id} не знайдено.");

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

    // Вимога методички: пошук за предикатом за допомогою Func
    public IReadOnlyList<BookCopy> FindAll(Func<BookCopy, bool> predicate) =>
        _store.List().Where(predicate).ToList();
}