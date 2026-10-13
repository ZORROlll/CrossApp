namespace Core.Storage;

using Core.Abstractions;

/// <summary>
/// Фабрика для інкапсуляції логіки вибору та конфігурації сховища книг.
/// </summary>
public static class StoreFactory
{
    public static IBookStore Create(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        bool useFile = args.Contains("--file");
        bool useCache = !args.Contains("--no-cache"); // За замовчуванням кешуємо, або додай за потребою

        string catalogDataPath = Path.Combine(AppContext.BaseDirectory, "data", "catalog.json");

        // 1. Створення базового сховища
        IBookStore store = useFile
            ? new FileBookStore(catalogDataPath)
            : new InMemoryBookStore(SampleData.Books());

        // 2. Огортання декоратором за необхідності
        if (useCache)
        {
            store = new CachingBookStore(store);
        }

        return store;
    }
}