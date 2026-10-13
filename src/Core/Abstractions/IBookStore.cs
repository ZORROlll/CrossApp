namespace Core.Abstractions;

using Core.Domain;

public interface IBookStore
{
    // Отримання списку всіх примірників
    // IReadOnlyList захищає колекцію від змін в обхід методів сховища
    IReadOnlyList<BookCopy> List();

    // Пошук за ідентифікатором; повертає nullable (BookCopy?), бо запису може не бути
    BookCopy? GetById(string id);

    // Додавання нового примірника до сховища
    void Add(BookCopy item);

    // Оновлення стану примірника
    void Update(BookCopy item);

    // Видалення примірника за Id
    bool Remove(string id);

    // Додаткове завдання 2: пошук за довільним предикатом
    IReadOnlyList<BookCopy> Find(Func<BookCopy, bool> predicate);
}