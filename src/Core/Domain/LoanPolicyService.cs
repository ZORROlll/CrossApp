namespace Core.Domain;

public static class LoanPolicyService
{
    public const int MaxActiveLoansPerReader = 5;

    // Інваріант двох сутностей: перевірка чи можна видати примірник конкретному читачу
    public static Loan IssueBookToReader(
        string loanId,
        BookCopy book,
        string readerId,
        IEnumerable<Loan> existingLoans,
        DateTime issuedOn)
    {
        if (book is null)
            throw new ArgumentNullException(nameof(book));

        if (string.IsNullOrWhiteSpace(readerId))
            throw new ArgumentException("Ідентифікатор читача обов'язковий", nameof(readerId));

        // 1. Інваріант сутності 1: чи вільна сама книга
        if (book.IsIssued)
        {
            throw new InvalidOperationException($"Примірник {book.Id} вже виданий і не може бути оформлений повторно.");
        }

        // 2. Міжсутнісний інваріант: підрахунок активних видач читача
        int activeLoansCount = existingLoans.Count(l => l.ReaderId == readerId && l.Status == LoanStatus.Active);
        if (activeLoansCount >= MaxActiveLoansPerReader)
        {
            throw new InvalidOperationException(
                $"Читач {readerId} вже має {activeLoansCount} активних видач (ліміт: {MaxActiveLoansPerReader}). Видача заборонена.");
        }

        // Якщо правило виконано — змінюємо стан книги та створюємо нову видачу
        book.Issue();
        return Loan.Open(loanId, book.Id, readerId, issuedOn);
    }
}