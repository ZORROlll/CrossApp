namespace Api.Contracts;

public sealed record SaveBookRequest(string Isbn, string Title, int Year, string? Author)
{
    public bool TryValidate(out Dictionary<string, string[]> errors)
    {
        var found = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(Isbn) || Isbn.Trim().Length > 32)
            found[nameof(Isbn)] = ["ISBN обов'язковий, до 32 символів."];

        if (string.IsNullOrWhiteSpace(Title) || Title.Trim().Length > 200)
            found[nameof(Title)] = ["Назва книги обов'язкова, до 200 символів."];

        int maxValidYear = DateTime.UtcNow.Year;
        if (Year <= 0 || Year > maxValidYear)
            found[nameof(Year)] = [$"Рік видання має бути в межах від 1 до {maxValidYear}."];

        if (Author is not null && Author.Trim().Length > 150)
            found[nameof(Author)] = ["Ім'я автора не може перевищувати 150 символів."];

        errors = found;
        return found.Count == 0;
    }
}