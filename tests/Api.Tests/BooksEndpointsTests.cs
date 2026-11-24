using System.Net;
using System.Net.Http.Json;
using Api.Contracts;
using Core.Dto;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests;

public sealed class BooksEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PostBook_Returns201Created_AndGetByLocation_Returns200Ok()
    {
        // Arrange
        var newBook = new SaveBookRequest(
            Isbn: "978-617-679-145-4",
            Title: "Місто",
            Year: 1928,
            Author: "Валерʼян Підмогильний");

        // Act 1: POST /api/books
        var postResponse = await _client.PostAsJsonAsync("/api/books", newBook);

        // Assert 1: 201 Created + валідний заголовок Location
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        Assert.NotNull(postResponse.Headers.Location);

        var createdBook = await postResponse.Content.ReadFromJsonAsync<BookDto>();
        Assert.NotNull(createdBook);
        Assert.False(string.IsNullOrWhiteSpace(createdBook.Id));

        // Act 2: GET за отриманим Location
        var getResponse = await _client.GetAsync(postResponse.Headers.Location);

        // Assert 2: 200 OK та збіг даних створеної книги
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetchedBook = await getResponse.Content.ReadFromJsonAsync<BookDto>();
        Assert.NotNull(fetchedBook);
        Assert.Equal(createdBook.Id, fetchedBook.Id);
        Assert.Equal(newBook.Isbn, fetchedBook.Isbn);
        Assert.Equal(newBook.Title, fetchedBook.Title);
        Assert.Equal(newBook.Year, fetchedBook.Year);
        Assert.Equal(newBook.Author, fetchedBook.Author);
    }
}