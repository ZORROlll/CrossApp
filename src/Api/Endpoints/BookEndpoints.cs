using System.Security.Cryptography;
using Api.Contracts;
using Api.Filters;
using Core;
using Core.Abstractions;
using Core.Domain;
using Core.Dto;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

public static class BookEndpoints
{
    public static void MapBooks(this WebApplication app)
    {
        var group = app.MapGroup("/api/books").WithTags("Books");

        // 1. GET списку (пагінація ?page=1&pageSize=20&search=... + фільтр ?author=...)
        group.MapGet("/", Results<Ok<PagedResult<BookDto>>, ValidationProblem> (
            IBookStore store,
            int page = 1,
            int pageSize = 20,
            string? search = null,
            string? author = null) =>
        {
            var errors = new Dictionary<string, string[]>();

            if (page < 1)
                errors["page"] = ["Номер сторінки (page) має бути >= 1."];

            if (pageSize is < 1 or > 100)
                errors["pageSize"] = ["Розмір сторінки (pageSize) має бути в межах від 1 до 100."];

            if (errors.Count > 0)
                return TypedResults.ValidationProblem(errors);

            var (items, total) = store.GetPaged(page, pageSize, search, author);
            var dtoItems = items.Select(b => b.ToDto()).ToList();

            return TypedResults.Ok(new PagedResult<BookDto>(dtoItems, total));
        });

        // 2. GET /{id}
        group.MapGet("/{id}", Results<Ok<BookDto>, NotFound> (string id, IBookStore store) =>
            store.GetById(id) is { } found
                ? TypedResults.Ok(found.ToDto())
                : TypedResults.NotFound());

        // 3. POST (валідація через FluentValidation + EndpointFilter)
        group.MapPost("/", Results<Created<BookDto>, ValidationProblem, Conflict<ProblemDetails>> (SaveBookRequest request, IBookStore store) =>
        {
            var normalizedIsbn = request.Isbn.Trim();
            if (store.Find(b => string.Equals(b.Isbn, normalizedIsbn, StringComparison.OrdinalIgnoreCase)).Count > 0)
            {
                throw new DomainException($"Книга з ISBN '{normalizedIsbn}' уже існує в каталозі.");
            }

            var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
            var created = BookCopy.Create(id, request.Isbn, request.Title, request.Year, request.Author);
            store.Add(created);

            return TypedResults.Created($"/api/books/{created.Id}", created.ToDto());
        })
        .AddEndpointFilter<ValidationFilter<SaveBookRequest>>();

        // 4. PUT /{id} (валідація через FluentValidation + EndpointFilter)
        group.MapPut("/{id}", Results<Ok<BookDto>, NotFound, ValidationProblem> (string id, SaveBookRequest request, IBookStore store) =>
        {
            var existing = store.GetById(id);
            if (existing is null)
                return TypedResults.NotFound();

            var replacement = BookCopy.Create(id, request.Isbn, request.Title, request.Year, request.Author, existing.IsIssued);
            store.Update(replacement);

            return TypedResults.Ok(replacement.ToDto());
        })
        .AddEndpointFilter<ValidationFilter<SaveBookRequest>>();

        // 5. DELETE /{id}
        group.MapDelete("/{id}", Results<NoContent, NotFound> (string id, IBookStore store) =>
            store.Remove(id)
                ? TypedResults.NoContent()
                : TypedResults.NotFound());
    }
}