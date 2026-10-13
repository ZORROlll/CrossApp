namespace Core;

using Core.Domain;

public static class SampleData
{
    // Генерує фіксований набір примірників для ініціалізації сховища в пам'яті
    public static IReadOnlyList<BookCopy> Books() =>
    [
        BookCopy.Create("B001", "978-0132350884", "Clean Code", 2008, "Robert C. Martin"),
        BookCopy.Create("B002", "978-0137081073", "The Clean Coder", 2011, "Robert C. Martin"),
        BookCopy.Create("B003", "978-0134494166", "Clean Architecture", 2017, "Robert C. Martin"),
        BookCopy.Create("B004", "978-0201633610", "Design Patterns", 1994, "Gang of Four"),
        BookCopy.Create("B005", "978-0321125217", "Domain-Driven Design", 2003, "Eric Evans"),
        BookCopy.Create("B006", "978-0134757599", "Refactoring", 2018, "Martin Fowler"),
        BookCopy.Create("B007", "978-0321278654", "Extreme Programming Explained", 2004, "Kent Beck"),
        BookCopy.Create("B008", "978-0596007126", "Head First Design Patterns", 2004, "Eric Freeman"),
        BookCopy.Create("B009", "978-1491950357", "Designing Data-Intensive Applications", 2017, "Martin Kleppmann"),
        BookCopy.Create("B010", "978-0735619678", "Code Complete", 2004, "Steve McConnell"),
        BookCopy.Create("B011", "978-0201616224", "The Pragmatic Programmer", 1999, "Andrew Hunt"),
        BookCopy.Create("B012", "978-1617294532", "Unit Testing Principles, Practices", 2020, "Vladimir Khorikov"),
        BookCopy.Create("B013", "978-1492040798", "C# 10 in a Nutshell", 2022, "Joseph Albahari"),
        BookCopy.Create("B014", "978-1484254394", "Pro C# 9 with .NET 5", 2021, "Andrew Troelsen"),
        BookCopy.Create("B015", "978-1617290695", "Dependency Injection in .NET", 2011, "Mark Seemann")
    ];
}