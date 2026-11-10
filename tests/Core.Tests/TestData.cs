namespace Core.Tests;

using System.Collections.Generic;
using Core.Domain;

public static class TestData
{
    public static IReadOnlyList<BookCopy> Items() => new List<BookCopy>
    {
        BookCopy.Create("T-01", "978-0132350884", "Clean Architecture", 2017, "Robert Martin"),
        BookCopy.Create("T-02", "978-0321125217", "Patterns of Enterprise", 2002, "Martin Fowler"),
        BookCopy.Create("T-03", "978-0131103627", "The C Programming Language", 1988, "Brian Kernighan"),
        BookCopy.Create("T-04", "978-0201616224", "The Pragmatic Programmer", 1999, "Andrew Hunt")
    };
}