using Core.Domain;
using Core.Dto;

namespace Core.Reports;

public static class ReportService
{
    // 1) Фільтр + стабільне сортування + проєкція у DTO (старіші за вказаний рік)
    public static IReadOnlyList<BookLine> OlderThan(IEnumerable<BookCopy> source, int year) =>
        source.Where(b => b.Year < year)
              .OrderBy(b => b.Year)
              .ThenBy(b => b.Isbn, StringComparer.Ordinal)
              .Select(b => new BookLine(b.Isbn, b.Title, b.Author ?? "Невідомий автор", b.Year))
              .ToList();

    // 2) ВИКОРИСТАННЯ 1: Топ-N найновіших книг через метод-розширення TopBy
    public static IReadOnlyList<BookLine> TopNewest(IEnumerable<BookCopy> source, int n) =>
        source.DistinctBy(b => b.Isbn) // Прибираємо дублікати примірників одного видання
              .TopBy(b => b.Year, n) 
              .Select(b => new BookLine(b.Isbn, b.Title, b.Author ?? "Невідомий автор", b.Year))
              .ToList();

       //static означає, що метод належить самому класу, а не його екземпляру.
    // 3) Групування за автором із підрахунком книг                     //на вхід у метод приходить джерело даних - колекція книг
    public static IReadOnlyList<GroupSummary> ByAuthor(IEnumerable<BookCopy> source) => //Приймає будь-яку послідовність сутностей BookCopy, яка підтримує перерахування
        source.GroupBy(b => b.Author ?? "Невідомий автор", StringComparer.OrdinalIgnoreCase) //лямбда-вираз, де b позначає одну поточну книгу (об'єкт BookCopy) у процесі обходу колекції.
              .Select(g => new GroupSummary(g.Key, g.Count())) //бере кожен вхідний елемент і перетворює його на щось інше.
                                                               //g ключ цієї групи (рядок з ім'ям автора, наприклад "Martin Fowler"
                                                               //g.Count метод LINQ, який підраховує, скільки всього книг лежить усередині цієї підгрупи g. Передається другим аргументом.
              .OrderByDescending(s => s.Count) //сортування за спаданням 
                                             //s позначає один наш об'єкт GroupSummary (підсумок по автору), отриманий із попереднього кроку Select
              .ThenBy(s => s.Key, StringComparer.Ordinal)
              .ToList();

// Додаткове завдання 3: Групування в синтаксисі запитів (Query Syntax)
    public static IReadOnlyList<GroupSummary> ByAuthorQuerySyntax(IEnumerable<BookCopy> source)
    {
        var query = from b in source
                    group b by b.Author ?? "Невідомий автор" into g
                    orderby g.Count() descending, g.Key
                    select new GroupSummary(g.Key, g.Count());

        return query.ToList();
    }
    // 4) ВИКОРИСТАННЯ 2: Топ-N найпродуктивніших авторів через наш метод-розширення TopBy
    public static IReadOnlyList<GroupSummary> TopAuthors(IEnumerable<BookCopy> source, int n) =>
        source.GroupBy(b => b.Author ?? "Невідомий автор", StringComparer.OrdinalIgnoreCase)
              .Select(g => new GroupSummary(g.Key, g.Count()))
              .TopBy(g => g.Count, n) // <-- Власний метод-розширення
              .ToList();

    // 5) Агрегати
    public static (int Items, double AverageYear, int MinYear, int MaxYear) Totals(IEnumerable<BookCopy> source)
    {
        var list = source.ToList();
        return list.Count == 0
            ? (0, 0d, 0, 0)
            : (list.Count, list.Average(b => b.Year), list.Min(b => b.Year), list.Max(b => b.Year));
    }

  // 6) Метод для отримання зведеного списку: 
public static IReadOnlyList<BookLoanLine> GetIssuedBooksWithReaders(
    IEnumerable<BookCopy> books,                         
    IEnumerable<ReaderDto> readers,                     
    IEnumerable<(string BookId, string ReaderId)> loans) // Список фактів видачі: пари (ID_книги, ID_читача)
{
    return loans
       
        // З'єднуємо список видач (loans) зі списком книг (books).
        // Це операція INNER JOIN: якщо ID книги з видачі є в базі книг — запис проходить далі.

        .Join(
            books,                                      //з якою колекцією з'єднуємо
            loan => loan.BookId,                        //ключ зв'язку з лівої таблиці (ID книги у факті видачі)
            book => book.Id,                            //ключ зв'язку з правої таблиці (ID у списку книг)
            (loan, book) => new                       
            { 
                loan.ReaderId,                         
                book.Title,                            
                book.Isbn                             
            })

     
        //  До отриманого проміжного результату приєднуємо читачів (readers).
        
        .Join(
            readers,                                    // колекція читачів
            lb => lb.ReaderId,                          // ключ із попереднього кроку (збережений ReaderId)
            reader => reader.Id,                        
            (lb, reader) => new BookLoanLine(           // 4-й аргумент: формуємо фінальний готовий об'єкт (DTO):
                lb.Title,                               
                lb.Isbn,                                
                reader.FullName,                       
                reader.TicketNumber))                   

        .ToList();
}

}