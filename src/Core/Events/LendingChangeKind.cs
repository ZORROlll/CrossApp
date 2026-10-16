namespace Core.Events;

/// <summary>
/// Види доменних подій обліку видачі книг.
/// </summary>
public enum LendingChangeKind
{
    BookAdded,
    CopyIssued,
    CopyReturned
}