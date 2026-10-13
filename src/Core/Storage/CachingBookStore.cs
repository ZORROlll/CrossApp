namespace Core.Storage;

using Core.Abstractions;
using Core.Domain;

/// <summary>
/// Декоратор для сховища книг, що додає проміжне кешування в оперативній пам'яті.
/// </summary>
public sealed class CachingBookStore : IBookStore
{
    private readonly IBookStore _inner;
    private readonly Dictionary<string, BookCopy> _cache = new(StringComparer.OrdinalIgnoreCase);

    public CachingBookStore(IBookStore inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    // Читання списку: заповнює кеш отриманими сутностями
    public IReadOnlyList<BookCopy> List()
    {
        var items = _inner.List();
        foreach (var item in items)
        {
            _cache[item.Id] = item;
        }
        return items;
    }

    // Читання за ID: якщо є в кеші — повертає з пам'яті, інакше звертається до inner і кешує
    public BookCopy? GetById(string id)
    {
        if (_cache.TryGetValue(id, out var cachedBook))
        {
            return cachedBook;
        }

        var book = _inner.GetById(id);
        if (book != null)
        {
            _cache[id] = book;
        }

        return book;
    }

    // Додавання: додає в основне сховище та оновлює кеш
    public void Add(BookCopy item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _inner.Add(item);
        _cache[item.Id] = item;
    }

    // Оновлення: оновлює стан у сховищі та інвалідує/перезаписує кеш
    public void Update(BookCopy item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _inner.Update(item);
        _cache[item.Id] = item;
    }

    // Видалення: видаляє зі сховища та прибирає з кешу
    public bool Remove(string id)
    {
        var removed = _inner.Remove(id);
        if (removed)
        {
            _cache.Remove(id);
        }
        return removed;
    }

    // Пошук: делегує внутрішньому сховищу
    public IReadOnlyList<BookCopy> Find(Func<BookCopy, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return _inner.Find(predicate);
    }
}