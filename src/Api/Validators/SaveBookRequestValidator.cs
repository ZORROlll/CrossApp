using Api.Contracts;
using FluentValidation;

namespace Api.Validators;

public sealed class SaveBookRequestValidator : AbstractValidator<SaveBookRequest>
{
    public SaveBookRequestValidator()
    {
        RuleFor(x => x.Isbn)
            .NotEmpty().WithMessage("ISBN обов'язковий, до 32 символів.")
            .MaximumLength(32).WithMessage("ISBN обов'язковий, до 32 символів.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Назва книги обов'язкова, до 200 символів.")
            .MaximumLength(200).WithMessage("Назва книги обов'язкова, до 200 символів.");

        RuleFor(x => x.Year)
            .InclusiveBetween(1, DateTime.UtcNow.Year)
            .WithMessage($"Рік видання має бути в межах від 1 до {DateTime.UtcNow.Year}.");

        RuleFor(x => x.Author)
            .MaximumLength(150)
            .WithMessage("Ім'я автора не може перевищувати 150 символів.")
            .When(x => !string.IsNullOrWhiteSpace(x.Author));
    }
}