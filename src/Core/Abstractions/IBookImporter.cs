namespace Core.Abstractions;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Core.Domain;

public interface IBookImporter
{
    // Базовий асинхронний метод імпорту (Крок 4)
    Task<IReadOnlyList<BookCopy>> LoadAsync(string path, CancellationToken ct = default);

    // ========================================================================
    // ДОДАТКОВЕ ЗАВДАННЯ: Потоковий імпорт через IAsyncEnumerable
    // Атрибут [EnumeratorCancellation] зв'язує токен виклику з токеном ітератора
    // ========================================================================
    IAsyncEnumerable<BookCopy> StreamAsync(
        string path, 
        CancellationToken ct = default);
}