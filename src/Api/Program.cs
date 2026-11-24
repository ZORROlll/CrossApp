using Api.Endpoints;
using Api.Handlers;
using Api.Validators;
using Core.Abstractions;
using Core.Domain;
using Core.Storage;
using FluentValidation;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddValidatorsFromAssemblyContaining<SaveBookRequestValidator>();

// Реєстрація сховища в DI (з двома початковими книгами для перевірки)
builder.Services.AddSingleton<IBookStore>(_ => new InMemoryBookStore(
[
    BookCopy.Create("b101", "978-966-01-0580-5", "Кобзар", 1840, "Тарас Шевченко"),
    BookCopy.Create("b102", "978-966-03-7745-5", "Тіні забутих предків", 1911, "Михайло Коцюбинський")
]));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.MapScalarApiReference(options => options.WithOpenApiRoutePattern("/openapi/{documentName}.json"));
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Підключення твоїх 5 маршрутів для /api/books
app.MapBooks();

app.Run();

public partial class Program;