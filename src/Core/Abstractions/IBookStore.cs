namespace Core.Abstractions;

using Core.Domain;
//інтерфейс IBookStore описує контракт взаємодії зі сховищем книг
public interface IBookStore
{
    IReadOnlyList<BookCopy> List();   // Отримання списку всіх примірників

    BookCopy? GetById(string id);   // Пошук за ідентифікатором; повертає nullable (BookCopy?), бо запису може не бути

    void Add(BookCopy item); //додай новий примірник до сховища

    void Update(BookCopy item); //оновлює стан

    bool Remove(string id);     // Видалення примірника за Id

    // (д2)
    IReadOnlyList<BookCopy> Find(Func<BookCopy, bool> predicate); //виконує гнучкий пошук книг за довільною умовою за допомогою делегата
}