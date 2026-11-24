namespace Core.Storage;

// [Крок 5.1 - ЛР №9] Додано простір імен для JavaScriptEncoder (щоб вимкнути екранування кирилиці у \uXXXX)
using System.Text.Encodings.Web;
using System.Text.Json;
using Core.Abstractions;
using Core.Domain;
using Core.Dto;

public sealed class FileBookStore : IBookStore
{
    // [Крок 5.2 - ЛР №9] Налаштовано серіалізацію: camelCase для властивостей та UnsafeRelaxedJsonEscaping для читабельної кирилиці
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true, //відступ
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // Перетворює назви властивостей C# із PascalCase (наприклад, InventoryNumber) у camelCase (inventoryNumber) у JSON
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // Внутрішній кеш примірників у пам'яті (порівняння Id без урахування регістру)
    private readonly Dictionary<string, BookCopy> _cache = new(StringComparer.OrdinalIgnoreCase);

    // Повний нормалізований шлях до поточного файлу збереження
    private string _path;

    // Прапоць для запобігання повторному зчитуванню з диска
    private bool _loaded;

    // [Крок 5.3] Змінено ім'я файлу за замовчуванням на books.json відповідно до конфігурації
   // Конструктор файлового сховища; приймає шлях до JSON-файлу (якщо не передано — за замовчуванням береться "books.json")
public FileBookStore(string path = "books.json")
{
    // Перевіряємо, що шлях не є null, порожнім рядком або набором пробілів; інакше одразу кидаємо ArgumentException
    ArgumentException.ThrowIfNullOrWhiteSpace(path);

    // Перетворюємо переданий (можливо, відносний) шлях у повний абсолютний шлях на диску та зберігаємо в поле _path
    _path = Path.GetFullPath(path);
}

    // Властивість для перегляду або динамічної зміни шляху через прапорець
    public string FilePath
    {
        get => _path;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            var normalized = Path.GetFullPath(value);

            // Якщо шлях змінився — оновлюємо його і скидаємо стан кешу для нового файлу
            if (!string.Equals(_path, normalized, StringComparison.OrdinalIgnoreCase))
            {
                _path = normalized;
                Reload();
            }
        }
    }

    // Примусове перезавантаження кешу з диска
    public void Reload()
    {
        _cache.Clear();
        _loaded = false;
        EnsureLoaded();
    }

    // Завантажує дані з JSON-файлу у пам'ять лише під час першого звернення
    private void EnsureLoaded()
    {
        if (_loaded) return;

        // [Крок 5.4 - ЛР №9] Якщо файл даних ще не створений — просто позначаємо сховище як завантане без помилок
        if (!File.Exists(_path))
        {
            _loaded = true;
            return;
        }

        // [Крок 5.5 - ЛР №9] Додано обробку пошкодженого JSON (JsonException -> зрозумілий DomainException замість необробленого stack trace)
        try
        {
            var json = File.ReadAllText(_path);
            // [Крок 5.6 - ЛР №9] Передано Options у Deserialize для коректного зчитування camelCase-полів
            var dtos = JsonSerializer.Deserialize<List<BookDto>>(json, Options) ?? [];

            _cache.Clear();
            foreach (var dto in dtos)
            {
                var book = BookCopy.FromDto(dto);
                _cache[book.Id] = book;
            }
        }
        catch (JsonException ex)
        {
            throw new DomainException($"Помилка читання JSON у файлі '{_path}': некоректний формат даних ({ex.Message}).");
        }

        _loaded = true;
    }

    // Скидає поточний стан кешу у JSON-файл на диску
    private void Flush()
    {
        // Створення каталогу (якщо ще не існує) перед записом файлу
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory)) //Перевіряємо, що шлях до папки не порожній і не null (наприклад, якщо передано лише ім'я файлу без каталогу), перед тим як створювати директорію
        {
            Directory.CreateDirectory(directory); // Створює всі відсутні каталоги по вказаному шляху (якщо папка вже існує — нічого не робить і не викидає помилку)
        }

        var dtos = _cache.Values.Select(b => b.ToDto()).ToList(); // Конвертуємо всі доменні об'єкти книг із кешу у плоскі DTO-об'єкти для подальшої серіалізації в JSON

        // [Крок 5.7 - ЛР №9] Атомарний запис у JSON: спочатку у тимчасовий .tmp файл, потім заміна основного через File.Move
        //операція заміни файлу відбувається як одна неподільна 
        // миттєва дія без проміжного стану, коли файл відкритий і пустий або записаний лише наполовину.
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(dtos, Options)); //правила форматування
        File.Move(tmp, _path, overwrite: true); //дозвіл на перезапис
    }

    public IReadOnlyList<BookCopy> List()
    {
        EnsureLoaded();
        return _cache.Values.ToList();
    }

    public BookCopy? GetById(string id)
    {
        EnsureLoaded();
        return _cache.GetValueOrDefault(id);
    }

    public void Add(BookCopy item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureLoaded();

        if (_cache.ContainsKey(item.Id))
        {
            throw new InvalidOperationException($"Примірник з id={item.Id} уже існує.");
        }

        _cache.Add(item.Id, item);
        Flush();
    }

    public void Update(BookCopy item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureLoaded();

        _cache[item.Id] = item;
        Flush();
    }

    public bool Remove(string id)
    {
        EnsureLoaded();

        if (!_cache.Remove(id))
        {
            return false;
        }

        Flush();
        return true;
    }

    // Пошук за предикатом над кешем
    public IReadOnlyList<BookCopy> Find(Func<BookCopy, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        EnsureLoaded();
        return _cache.Values.Where(predicate).ToList();
    }

    // Пагінація та пошук для API
    public (IReadOnlyList<BookCopy> Items, int Total) GetPaged(int page, int pageSize, string? search, string? author = null)
    {
        EnsureLoaded();
        IEnumerable<BookCopy> query = _cache.Values;

        if (!string.IsNullOrWhiteSpace(author))
        {
            var authorTerm = author.Trim();
            query = query.Where(b => b.Author is not null &&
                                     b.Author.Contains(authorTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(b =>
                b.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (b.Author is not null && b.Author.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                b.Isbn.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = query.OrderBy(b => b.Id, StringComparer.OrdinalIgnoreCase).ToList();
        var total = ordered.Count;

        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, total);
    }
}