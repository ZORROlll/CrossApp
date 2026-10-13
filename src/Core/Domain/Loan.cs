namespace Core.Domain;

// Перелічування станів видачі книги
public enum LoanStatus
{
    Active = 1,
    Returned = 2,
    Lost = 3
}

public sealed class Loan
{
    public string Id { get; }
    public string BookCopyId { get; }
    public string ReaderId { get; }
    public DateTime IssuedOn { get; }
    public DateTime? ReturnedOn { get; private set; }
    public LoanStatus Status { get; private set; }

    private Loan(string id, string bookCopyId, string readerId, DateTime issuedOn, LoanStatus status)
    {
        Id = id;
        BookCopyId = bookCopyId;
        ReaderId = readerId;
        IssuedOn = issuedOn;
        Status = status;
    }

    // Фабричний метод: відкриття видачі
    public static Loan Open(string id, string bookCopyId, string readerId, DateTime issuedOn)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Ідентифікатор видачі не може бути порожнім", nameof(id));

        if (string.IsNullOrWhiteSpace(bookCopyId))
            throw new ArgumentException("Ідентифікатор книги обов'язковий", nameof(bookCopyId));

        if (string.IsNullOrWhiteSpace(readerId))
            throw new ArgumentException("Ідентифікатор читача обов'язковий", nameof(readerId));

        return new Loan(id.Trim(), bookCopyId.Trim(), readerId.Trim(), issuedOn, LoanStatus.Active);
    }

    // Метод закриття видачі через перевірку переходів станів у switch expression (Додаткове завдання 3)
    public void Close(DateTime returnedOn)
    {
        // 1. Перевірка валідності вхідної дати до будь-яких змін стану
        if (returnedOn < IssuedOn)
        {
            throw new ArgumentOutOfRangeException(
                nameof(returnedOn),
                returnedOn,
                $"Дата повернення ({returnedOn:yyyy-MM-dd}) не може бути ранішою за дату видачі ({IssuedOn:yyyy-MM-dd})");
        }

        // 2. Валідація та перехід стану через switch expression
        Status = Status switch
        {
            LoanStatus.Active => LoanStatus.Returned,
            LoanStatus.Returned => throw new InvalidOperationException($"Видача {Id} уже була закрита раніше"),
            LoanStatus.Lost => throw new InvalidOperationException($"Книга за видачею {Id} втрачена, закрити неможливо"),
            _ => throw new InvalidOperationException("Невідомий статус видачі")
        };

        // 3. Фіксація дати повернення
        ReturnedOn = returnedOn;
    }

    public override string ToString() =>
        $"Видача {Id}: Книга {BookCopyId}, Читач {ReaderId}, Дата: {IssuedOn:yyyy-MM-dd}, Стан: {Status}";
}