namespace Core.Configuration;

public sealed class AppOptions
{
    public StorageOptions Storage { get; init; } = new();
    public LoggingOptions Logging { get; init; } = new();
    public LibraryOptions Library { get; init; } = new();
    public string EnvironmentName { get; init; } = "Development";
}

public sealed class StorageOptions
{
    public string DataPath { get; init; } = "data";
    public string FileName { get; init; } = "books.json";

    public string ResolveFullPath(string baseDir)
    {
        var root = Path.IsPathRooted(DataPath) ? DataPath : Path.Combine(baseDir, DataPath);
        Directory.CreateDirectory(root);
        return Path.GetFullPath(Path.Combine(root, FileName));
    }
}

public sealed class LoggingOptions
{
    public string MinimumLevel { get; init; } = "Information";
}

public sealed class LibraryOptions
{
    public int LoanDays { get; init; } = 14;
}