using Core.Domain;

namespace Core.Data;

public static class SampleData
{
    // Метод Books(), якого вимагає StoreFactory
    public static IReadOnlyList<BookCopy> Books() => CreateBooks();

    public static IReadOnlyList<BookCopy> CreateBooks() => new List<BookCopy>
    {
        // 3 старіших видання (раніше 2000 року) — гарантують, що report-low не буде порожнім
        BookCopy.Create("b01", "978-0131103627", "The C Programming Language", 1988, "Brian Kernighan"),
        BookCopy.Create("b02", "978-0201633610", "Design Patterns", 1994, "Gang of Four"),
        BookCopy.Create("b03", "978-0201616224", "The Pragmatic Programmer", 1999, "Andrew Hunt"),

        // Видання 2000–2015 років із різними авторами для перевірки групування
        BookCopy.Create("b04", "978-0321125217", "Patterns of Enterprise Architecture", 2002, "Martin Fowler"),
        BookCopy.Create("b05", "978-0321146533", "Test Driven Development", 2002, "Kent Beck"),
        BookCopy.Create("b06", "978-0321125218", "Patterns of Enterprise Architecture (Copy 2)", 2002, "Martin Fowler"),
        BookCopy.Create("b07", "978-0321200686", "Domain-Driven Design", 2003, "Eric Evans"),
        BookCopy.Create("b08", "978-0596007126", "Head First Design Patterns", 2004, "Eric Freeman"),
        BookCopy.Create("b09", "978-0735619678", "Code Complete", 2004, "Steve McConnell"),
        BookCopy.Create("b10", "978-0132350884", "Clean Code", 2008, "Robert C. Martin"),
        BookCopy.Create("b11", "978-0137081073", "The Clean Coder", 2011, "Robert C. Martin"),
        BookCopy.Create("b12", "978-1617290695", "Dependency Injection in .NET", 2011, "Mark Seemann"),

        // Сучасні видання для перевірки report-top (ТОП-5 найновіших)
        BookCopy.Create("b13", "978-1449373320", "Designing Data-Intensive Apps", 2017, "Martin Kleppmann"),
        BookCopy.Create("b14", "978-0134494166", "Clean Architecture", 2017, "Robert C. Martin"),
        BookCopy.Create("b15", "978-0134757599", "Refactoring (2nd Edition)", 2018, "Martin Fowler"),
        BookCopy.Create("b16", "978-1617294532", "Unit Testing Principles", 2020, "Vladimir Khorikov"),
        BookCopy.Create("b17", "978-1484254394", "Pro C# 9 with .NET 5", 2021, "Andrew Troelsen"),
        BookCopy.Create("b18", "978-1492040798", "C# 10 in a Nutshell", 2022, "Joseph Albahari")
    };
}