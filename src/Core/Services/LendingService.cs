namespace Core.Services;

using Core.Abstractions;
using Core.Domain;

public sealed class LendingService(IBookStore store)
{
    private readonly IBookStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public BookCopy AddBook(string id, string isbn, string title, int year, string? author = null)
    {
        var book = BookCopy.Create(id, isbn, title, year, author);
        _store.Add(book);
        return book;
    }

    public BookCopy AddBook(string isbn, string title, int year, string? author = null)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        return AddBook(id, isbn, title, year, author);
    }

    public void IssueCopy(string id)
    {
        var book = _store.GetById(id)
            ?? throw new InvalidOperationException($"Примірник з id={id} не знайдено.");

        book.Issue();
        _store.Update(book);
    }

    public void ReturnCopy(string id)
    {
        var book = _store.GetById(id)
            ?? throw new InvalidOperationException($"Примірник з id={id} не знайдено.");

        book.Return();
        _store.Update(book);
    }

    public IReadOnlyList<BookCopy> All() => _store.List();

    public BookCopy? Find(string id) => _store.GetById(id);

    public IReadOnlyList<BookCopy> Search(Func<BookCopy, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return _store.Find(predicate);
    }
}