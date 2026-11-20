using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;

namespace ModernTinyWall.Models;

internal sealed record TablePreferences
{
    public string SortColumn { get; init; } = string.Empty;
    public bool Descending { get; init; }
    public Dictionary<string, double> ColumnWidths { get; init; } = new();

    internal double GetWidth(string key, double defaultWidth)
    {
        if (!ColumnWidths.TryGetValue(key, out var width))
            return defaultWidth;
        if (!double.IsFinite(width) || width is < 48 or > 1800)
            throw new InvalidDataException($"The saved width for column '{key}' is invalid.");
        return width;
    }
}

internal sealed class TableColumn(string key, string heading, double width, IComparer<object> comparer)
{
    public string Key { get; } = key;
    public string Heading { get; } = heading;
    public double Width { get; set; } = width;
    public IComparer<object> Comparer { get; } = comparer;

    internal static TableColumn Text<T>(string key, string heading, double width, Func<T, string> value) =>
        Value(key, heading, width, value, StringComparer.CurrentCultureIgnoreCase);

    internal static TableColumn Value<T, TValue>(string key, string heading, double width, Func<T, TValue> value,
        IComparer<TValue>? comparer = null) =>
        new(key, heading, width, Comparer<object>.Create((left, right) =>
        {
            if (left is not T first || right is not T second)
                throw new InvalidOperationException($"Column '{key}' received an unexpected row type.");
            return (comparer ?? Comparer<TValue>.Default).Compare(value(first), value(second));
        }));
}

internal static class TableOrdering
{
    internal static IReadOnlyList<object> Sort(IEnumerable<object> items, TableColumn column, TableColumn nameColumn, bool descending)
    {
        var sorted = descending
            ? items.OrderByDescending(item => item, column.Comparer)
            : items.OrderBy(item => item, column.Comparer);
        return sorted.ThenBy(item => item, nameColumn.Comparer).ToArray();
    }
}

internal static class TableSortValues
{
    internal static int? Port(string text) => int.TryParse(text, out var value) ? value : null;
    internal static DateTime? Created(string text) =>
        DateTime.TryParseExact(text, "yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var value) ? value : null;
}
