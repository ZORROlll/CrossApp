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
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // Внутрішній кеш примірників у пам'яті (порівняння Id без урахування регістру)
    private readonly Dictionary<string, BookCopy> _cache = new(StringComparer.OrdinalIgnoreCase);

    // Повний нормалізований шлях до поточного файлу збереження
    private string _path;

    // Прапорець для запобігання повторному зчитуванню з диска
    private bool _loaded;

    // [Крок 5.3 - ЛР №9] Змінено ім'я файлу за замовчуванням на books.json відповідно до конфігурації
    public FileBookStore(string path = "books.json")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
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

        // [Крок 5.4 - ЛР №9] Якщо файл даних ще не створений — просто позначаємо сховище як завантажене без помилок
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
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var dtos = _cache.Values.Select(b => b.ToDto()).ToList();

        // [Крок 5.7 - ЛР №9] Атомарний запис у JSON: спочатку у тимчасовий .tmp файл, потім заміна основного через File.Move
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(dtos, Options));
        File.Move(tmp, _path, overwrite: true);
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
}