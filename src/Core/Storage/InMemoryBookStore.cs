namespace Core.Storage;

using Core.Abstractions;
using Core.Domain;

public sealed class InMemoryBookStore(IEnumerable<BookCopy>? seed = null) : IBookStore
{
    // Ініціалізація словника: seed ?? [] захищає від null, 
    // StringComparer.OrdinalIgnoreCase забезпечує нечутливість до регістру ідентифікатора
    private readonly Dictionary<string, BookCopy> _items =
        (seed ?? []).ToDictionary(b => b.Id, StringComparer.OrdinalIgnoreCase);
//seed первинне наповнення програми або бази даних тестовими записами при старті, щоб система не була порожньою. 
    // Повертає список як IReadOnlyList, щоб унеможливити зміну колекції напряму
    public IReadOnlyList<BookCopy> List() => _items.Values.ToList();

    // GetValueOrDefault повертає null, якщо ключ відсутній (для типу BookCopy?)
    public BookCopy? GetById(string id) => _items.GetValueOrDefault(id);

    // Додавання нової книги з перевіркою на null та дублікат id
    public void Add(BookCopy item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (_items.ContainsKey(item.Id))
        {
            throw new InvalidOperationException($"Примірник з id={item.Id} уже існує.");
        }

        _items.Add(item.Id, item);
    }

    // Оновлення посилання у словнику
    public void Update(BookCopy item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items[item.Id] = item;
    }

    // Видалення зі словника
    public bool Remove(string id) => _items.Remove(id);

    // Додаткове завдання 2: пошук за довільним предикатом
    public IReadOnlyList<BookCopy> Find(Func<BookCopy, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return _items.Values.Where(predicate).ToList();
    }
}