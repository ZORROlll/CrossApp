namespace Core.Abstractions;

/// <summary>
/// Абстракція приймача сповіщень про доменні події (альтернатива event через DI).
/// </summary>
public interface INotificationSink
{
    void Notify(string message);
}