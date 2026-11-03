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

    // 2) ВИКОРИСТАННЯ 1: Топ-N найновіших книг через наш метод-розширення TopBy
    public static IReadOnlyList<BookLine> TopNewest(IEnumerable<BookCopy> source, int n) =>
        source.DistinctBy(b => b.Isbn)
              .TopBy(b => b.Year, n) // <-- Власний метод-розширення
              .Select(b => new BookLine(b.Isbn, b.Title, b.Author ?? "Невідомий автор", b.Year))
              .ToList();

    // 3) Групування за автором із підрахунком книг
    public static IReadOnlyList<GroupSummary> ByAuthor(IEnumerable<BookCopy> source) =>
        source.GroupBy(b => b.Author ?? "Невідомий автор", StringComparer.OrdinalIgnoreCase)
              .Select(g => new GroupSummary(g.Key, g.Count()))
              .OrderByDescending(s => s.Count)
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

    // 6) Join книг і читачів
    public static IReadOnlyList<BookLoanLine> GetIssuedBooksWithReaders(
        IEnumerable<BookCopy> books,
        IEnumerable<ReaderDto> readers,
        IEnumerable<(string BookId, string ReaderId)> loans)
    {
        return loans
            .Join(
                books,
                loan => loan.BookId,
                book => book.Id,
                (loan, book) => new { loan.ReaderId, book.Title, book.Isbn })
            .Join(
                readers,
                lb => lb.ReaderId,
                reader => reader.Id,
                (lb, reader) => new BookLoanLine(
                    lb.Title,
                    lb.Isbn,
                    reader.FullName,
                    reader.TicketNumber))
            .ToList();
    }
}