namespace Core.Storage;

using System.Text.Json;
using Core.Abstractions;
using Core.Domain;
using Core.Dto;

public sealed class FileBookStore : IBookStore
{
    // Статичні налаштування JSON: однаковий формат для всіх екземплярів, читабельний відступ
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    // Внутрішній кеш примірників у пам'яті (порівняння Id без урахування регістру)
    private readonly Dictionary<string, BookCopy> _cache = new(StringComparer.OrdinalIgnoreCase);

    // Повний нормалізований шлях до поточного файлу збереження
    private string _path;

    // Прапорець для запобігання повторному зчитуванню з диска
    private bool _loaded;

    public FileBookStore(string path = "catalog.json")
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

        if (File.Exists(_path))
        {
            var json = File.ReadAllText(_path);
            var dtos = JsonSerializer.Deserialize<List<BookDto>>(json) ?? [];

            foreach (var dto in dtos)
            {
                var book = BookCopy.FromDto(dto);
                _cache[book.Id] = book;
            }
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
        File.WriteAllText(_path, JsonSerializer.Serialize(dtos, Options));
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