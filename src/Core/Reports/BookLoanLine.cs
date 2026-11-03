namespace Core.Reports;

public sealed record BookLoanLine(
    string BookTitle,
    string Isbn,
    string ReaderName,
    string TicketNumber);