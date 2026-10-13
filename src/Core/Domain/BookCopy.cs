using Core.Dto;

namespace Core.Domain;

public sealed class BookCopy
{ //зберігає конкретне значення чи стан
    private bool _isIssued; //приватне поле для інкапсуляції стану, ця змінна може змінювати тільки цей клас
                            //поле private ми можемо змінювати стан об'єкта тільки за допомогою спеціальних методів
    public string Id { get; }
    public string Isbn { get; }
    public string Title { get; }
    public int Year { get; }
    public string? Author { get; }
    public bool IsIssued => _isIssued;

    //записує початкові значення об'єкта, забороняє створення об'єкта через new в обхід перевірок
    private BookCopy(string id, string isbn, string title, int year, string? author, bool isIssued)
    {
        Id = id;
        Isbn = isbn;
        Title = title;
        Year = year;
        Author = author;
        _isIssued = isIssued;
    }

    // Фабричний метод: перевіряє ВСІ аргументи ДО створення об'єкта
    public static BookCopy Create(
        string id, 
        string isbn, 
        string title, 
        int year, 
        string? author = null, 
        bool isIssued = false,
        int? currentYear = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Ідентифікатор книги не може бути порожнім", nameof(id));

        if (string.IsNullOrWhiteSpace(isbn))
            throw new ArgumentException("ISBN не може бути порожнім", nameof(isbn));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Назва книги не може бути порожньою", nameof(title));

        // Фіксуємо поріг року для перевірки інваріанта
        int maxValidYear = currentYear ?? DateTime.UtcNow.Year;
        if (year <= 0 || year > maxValidYear)
        {
            throw new ArgumentOutOfRangeException(
                nameof(year), 
                year, 
                $"Рік видання має бути в межах від 1 до {maxValidYear}");
        }

        return new BookCopy(id.Trim(), isbn.Trim().ToUpperInvariant(), title.Trim(), year, author?.Trim(), isIssued); //виклик приватного контструктора
    }

    // Бізнес-метод: видача примірника
    public void Issue() //доменний метод зміни стану для сутності BookCopy
    {
        if (_isIssued) //_isIssued вик. суто всередині цього класу, і показує чи видана книга
            throw new InvalidOperationException($"Примірник {Id} ('{Title}') уже виданий, повторна видача неможлива");//виклик методу є неприпустимим через поточний стан об'єкта

        _isIssued = true; //видана 
    }

    // Бізнес-метод: повернення примірника
    public void Return()
    {
        if (!_isIssued)
            throw new InvalidOperationException($"Примірник {Id} ('{Title}') не був виданий, повернення неможливе");

        _isIssued = false;
    }

    // Мапінг сутності в DTO, BookDto - просто містить лише голі поля/властивості - id...
    public BookDto ToDto() => new(Id, Isbn, Title, Year, Author);

    // Відновлення сутності з DTO з проходженням усіх інваріантів
    public static BookCopy FromDto(BookDto dto) //вхідний параметр: об'єкт із сирими даними.
    {
        if (dto is null)
            throw new ArgumentNullException(nameof(dto), "DTO не може бути null");//nameof повертає точне ім'я змінної як рядок  без помилок у назвах

        return Create(dto.Id, dto.Isbn, dto.Title, dto.Year, dto.Author);//перевіряє чи не порожній title
    }

    public override string ToString() =>
        $"{Id} [{Isbn}] \"{Title}\" ({Year} р., {Author ?? "Автор невідомий"}) - Стан: {(_isIssued ? "На руках" : "В наявності")}";
} 