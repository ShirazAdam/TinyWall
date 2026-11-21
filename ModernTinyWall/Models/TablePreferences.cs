using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

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

internal sealed class TableColumn(string key, string heading, double width, IComparer<object> comparer,
    Func<object, string>? display = null)
{
    public string Key { get; } = key;
    public string Heading { get; } = heading;
    public double Width { get; set; } = width;
    public IComparer<object> Comparer { get; } = comparer;
    public Func<object, string>? Display { get; } = display;

    internal static TableColumn Text<T>(string key, string heading, double width, Func<T, string> value) =>
        Value(key, heading, width, value, StringComparer.CurrentCultureIgnoreCase, value);

    internal static TableColumn Value<T, TValue>(string key, string heading, double width, Func<T, TValue> value,
        IComparer<TValue>? comparer = null, Func<T, string>? display = null) =>
        new(key, heading, width, Comparer<object>.Create((left, right) =>
        {
            if (left is not T first || right is not T second)
                throw new InvalidOperationException($"Column '{key}' received an unexpected row type.");
            return (comparer ?? Comparer<TValue>.Default).Compare(value(first), value(second));
        }), display is null ? null : item => item is T row
            ? display(row) ?? string.Empty
            : throw new InvalidOperationException($"Column '{key}' received an unexpected row type."));
}

internal sealed class TableGroup : INotifyPropertyChanged
{
    internal TableGroup(string key) => Key = key;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key { get; }
    public string Name { get; private set; } = string.Empty;
    public IReadOnlyList<object> Items { get; private set; } = [];
    public int Count => Items.Count;
    public string Label => $"{Name} ({Count})";
    public bool IsExpanded { get; private set; }
    public IReadOnlyList<string> Cells { get; private set; } = [];
    public string? IconPath { get; private set; }

    internal void Update(string name, IReadOnlyList<object> items, bool expanded, IReadOnlyList<string> cells, string? iconPath)
    {
        Name = name;
        Items = items;
        IsExpanded = expanded;
        Cells = cells;
        IconPath = iconPath;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    public override string ToString() => $"{Label}, {(IsExpanded ? "expanded" : "collapsed")}";
}

internal static partial class TableGrouping
{
    internal static string KeyOf(string name) => name.Trim().ToUpperInvariant();

    internal static string WithoutProcessId(string name) => ProcessIdSuffix().Replace(name, string.Empty);

    [GeneratedRegex(@"\s+\(\d+\)\s*$")]
    private static partial Regex ProcessIdSuffix();

    // Groups rows sharing the first column's name. Groups keep the position of their best-sorted row;
    // single rows stay as plain rows and expanded groups are followed by their rows.
    internal static IReadOnlyList<object> Build(IReadOnlyList<object> sortedItems, IReadOnlyList<TableColumn> columns,
        Func<object, string?>? iconPath, ISet<string> expandedKeys, IDictionary<string, TableGroup> groups,
        ISet<object> groupedItems, Func<string, string>? groupName = null)
    {
        ArgumentNullException.ThrowIfNull(sortedItems);
        if (columns.Count == 0 || columns[0].Display is not { } display)
            throw new InvalidOperationException("Grouping requires a text first column.");
        string name(object item) => groupName is null ? display(item) : groupName(display(item));
        groupedItems.Clear();
        var buckets = new Dictionary<string, List<object>>();
        var order = new List<(string? Key, object Item)>();
        foreach (var item in sortedItems)
        {
            var text = name(item);
            if (string.IsNullOrWhiteSpace(text))
            {
                order.Add((null, item));
                continue;
            }
            var key = KeyOf(text);
            if (!buckets.TryGetValue(key, out var bucket))
            {
                buckets[key] = bucket = [];
                order.Add((key, item));
            }
            bucket.Add(item);
        }

        foreach (var stale in groups.Keys.Where(key => !buckets.TryGetValue(key, out var bucket) || bucket.Count < 2).ToArray())
            groups.Remove(stale);
        expandedKeys.IntersectWith(buckets.Where(pair => pair.Value.Count > 1).Select(pair => pair.Key));

        var result = new List<object>(sortedItems.Count);
        foreach (var (key, first) in order)
        {
            if (key is null || buckets[key].Count == 1)
            {
                result.Add(first);
                continue;
            }
            var items = buckets[key];
            if (!groups.TryGetValue(key, out var group))
                groups[key] = group = new TableGroup(key);
            var expanded = expandedKeys.Contains(key);
            group.Update(name(items[0]).Trim(), items, expanded, CommonCells(items, columns),
                items.Select(item => iconPath?.Invoke(item)).FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)));
            result.Add(group);
            foreach (var item in items)
                groupedItems.Add(item);
            if (expanded)
                result.AddRange(items);
        }
        return result;
    }

    private static string[] CommonCells(IReadOnlyList<object> items, IReadOnlyList<TableColumn> columns)
    {
        var cells = new string[columns.Count];
        cells[0] = string.Empty;
        for (var index = 1; index < columns.Count; index++)
        {
            var display = columns[index].Display;
            var values = display is null ? [] : items.Select(display).Distinct(StringComparer.CurrentCultureIgnoreCase).Take(2).ToArray();
            cells[index] = values.Length == 1 ? values[0] : string.Empty;
        }
        return cells;
    }
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
