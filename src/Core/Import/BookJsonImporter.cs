namespace Core.Import;

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Core;
using Core.Abstractions;
using Core.Domain;
using Core.Dto;

public sealed class BookJsonImporter : IBookImporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<BookCopy>> LoadAsync(string path, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        var dtos = await JsonSerializer.DeserializeAsync<List<BookDto>>(
            stream,
            Options,
            cancellationToken: ct)
            ?? throw new DomainException($"Файл {Path.GetFileName(path)} не містить списку.");

        var result = new List<BookCopy>(dtos.Count);

        for (var i = 0; i < dtos.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                result.Add(BookCopy.FromDto(dtos[i]));
            }
            catch (ArgumentException ex)
            {
                throw new DomainException($"Запис {i + 1}: {ex.Message}", ex);
            }
        }

        return result;
    }

    // ========================================================================
    // ДОДАТКОВЕ ЗАВДАННЯ: Потоковий імпорт через IAsyncEnumerable
    // ========================================================================
    public async IAsyncEnumerable<BookCopy> StreamAsync(
        string path,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        var index = 0;
        await foreach (var dto in JsonSerializer.DeserializeAsyncEnumerable<BookDto>(stream, Options, ct))
        {
            ct.ThrowIfCancellationRequested();

            if (dto is null) 
                continue;

            BookCopy copy;
            try
            {
                copy = BookCopy.FromDto(dto);
            }
            catch (ArgumentException ex)
            {
                throw new DomainException($"Запис {index + 1}: {ex.Message}", ex);
            }

            index++;
            yield return copy;
        }
    }
}