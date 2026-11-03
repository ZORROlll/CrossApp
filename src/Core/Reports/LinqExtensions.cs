namespace Core.Reports;

public static class LinqExtensions
{
    /// <summary>
    /// Власний метод-розширення для взяття Топ-N елементів за заданим ключем сортування за спаданням.
    /// </summary>
    public static IEnumerable<TSource> TopBy<TSource, TKey>(
        this IEnumerable<TSource> source,
        Func<TSource, TKey> keySelector,
        int count)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);

        return source
            .OrderByDescending(keySelector)
            .Take(count);
    }
}