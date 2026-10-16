using Core.Abstractions;

namespace Cli;

/// <summary>
/// Приймач сповіщень, що виводить повідомлення в консоль.
/// </summary>
public sealed class ConsoleSink : INotificationSink
{
    public void Notify(string message) => Console.WriteLine($"[ConsoleSink] {message}");
}

/// <summary>
/// Приймач сповіщень, що записує повідомлення у файл.
/// </summary>
public sealed class FileSink(string filePath) : INotificationSink
{
    private readonly string _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));

    public void Notify(string message)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.AppendAllText(_filePath, $"[FileSink] {message}{Environment.NewLine}");
    }
}

/// <summary>
/// Композитний приймач для надсилання одного сповіщення одразу в кілька приймачів.
/// </summary>
public sealed class CompositeNotificationSink(params INotificationSink[] sinks) : INotificationSink
{
    private readonly IReadOnlyList<INotificationSink> _sinks = sinks ?? throw new ArgumentNullException(nameof(sinks));

    public void Notify(string message)
    {
        foreach (var sink in _sinks)
        {
            sink.Notify(message);
        }
    }
}