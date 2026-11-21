using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernTinyWall.Models;
using ModernTinyWall.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ModernTinyWall.Exceptions.Tests;

[TestClass]
public sealed class TableTests
{
    private sealed record Row(string Name, long Bytes, string Port, string Created);

    [TestMethod]
    public void HeaderSortUsesNumbersInsteadOfFormattedTextAndReversesDirection()
    {
        object[] rows =
        [
            new Row("Zulu", 10, "   10", "2026/01/03 10:00"),
            new Row("alpha", 2, "    2", "2026/01/02 10:00"),
            new Row("Beta", 10, "   10", "2026/01/01 10:00")
        ];
        var name = TableColumn.Text<Row>("name", "Name", 200, row => row.Name);
        var bytes = TableColumn.Value<Row, long>("bytes", "Bytes", 100, row => row.Bytes);
        var port = TableColumn.Value<Row, int?>("port", "Port", 100, row => TableSortValues.Port(row.Port));
        var created = TableColumn.Value<Row, DateTime?>("created", "Created", 150, row => TableSortValues.Created(row.Created));

        CollectionAssert.AreEqual(new[] { "alpha", "Beta", "Zulu" }, Names(TableOrdering.Sort(rows, name, name, false)));
        CollectionAssert.AreEqual(new[] { "Zulu", "Beta", "alpha" }, Names(TableOrdering.Sort(rows, name, name, true)));
        CollectionAssert.AreEqual(new[] { "alpha", "Beta", "Zulu" }, Names(TableOrdering.Sort(rows, bytes, name, false)));
        CollectionAssert.AreEqual(new[] { "Beta", "Zulu", "alpha" }, Names(TableOrdering.Sort(rows, bytes, name, true)));
        CollectionAssert.AreEqual(new[] { "alpha", "Beta", "Zulu" }, Names(TableOrdering.Sort(rows, port, name, false)));
        CollectionAssert.AreEqual(new[] { "Beta", "alpha", "Zulu" }, Names(TableOrdering.Sort(rows, created, name, false)));
    }

    [TestMethod]
    public async Task TablePreferencesSurviveReloadWithoutOverwritingAnotherTable()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ModernTinyWallTables-" + Guid.NewGuid());
        var path = Path.Combine(directory, "options.json");
        try
        {
            var options = new OptionsService(path);
            await options.UpdateAsync(state => state with
            {
                Tables = new Dictionary<string, TablePreferences>
                {
                    ["processes"] = new()
                    {
                        SortColumn = "path",
                        Descending = true,
                        ColumnWidths = new() { ["name"] = 420, ["path"] = 710 }
                    }
                }
            });
            await options.UpdateAsync(state => state with { CloseToTray = false });
            await options.UpdateAsync(state =>
            {
                var tables = new Dictionary<string, TablePreferences>(state.Tables)
                {
                    ["services"] = new() { SortColumn = "name", ColumnWidths = new() { ["name"] = 360 } }
                };
                return state with { Tables = tables };
            });

            var restored = await new OptionsService(path).LoadAsync();

            Assert.AreEqual("path", restored.Tables["processes"].SortColumn);
            Assert.IsTrue(restored.Tables["processes"].Descending);
            Assert.AreEqual(420, restored.Tables["processes"].GetWidth("name", 100));
            Assert.AreEqual(360, restored.Tables["services"].GetWidth("name", 100));
            Assert.IsFalse(restored.CloseToTray);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    [TestMethod]
    public void InvalidSavedColumnWidthIsReported()
    {
        var saved = new TablePreferences { ColumnWidths = new() { ["name"] = -20 } };
        Assert.ThrowsExactly<InvalidDataException>(() => saved.GetWidth("name", 240));
        Assert.AreEqual(240, saved.GetWidth("newColumn", 240));
    }

    [TestMethod]
    public void RowsWithTheSameNameAreGroupedWithCountsAndExpandInSortedPosition()
    {
        var chrome1 = new Row("chrome.exe", 30, "443", "a");
        var chrome2 = new Row("Chrome.exe", 10, "443", "b");
        var solo = new Row("edge.exe", 20, "80", "c");
        object[] sorted = [chrome1, solo, chrome2];
        TableColumn[] columns =
        [
            TableColumn.Text<Row>("name", "Name", 200, row => row.Name),
            TableColumn.Value<Row, int?>("port", "Port", 100, row => TableSortValues.Port(row.Port), display: row => row.Port),
            TableColumn.Text<Row>("created", "Created", 150, row => row.Created),
            TableColumn.Value<Row, long>("bytes", "Bytes", 100, row => row.Bytes)
        ];
        var expanded = new HashSet<string>();
        var groups = new Dictionary<string, TableGroup>();
        var grouped = new HashSet<object>();

        var collapsed = TableGrouping.Build(sorted, columns, row => "C:\\" + ((Row)row).Name, expanded, groups, grouped);

        Assert.HasCount(2, collapsed);
        var group = (TableGroup)collapsed[0];
        Assert.AreSame(solo, collapsed[1]);
        Assert.AreEqual("chrome.exe (2)", group.Label);
        Assert.IsFalse(group.IsExpanded);
        CollectionAssert.AreEqual(new object[] { chrome1, chrome2 }, group.Items.ToArray());
        CollectionAssert.AreEqual(new[] { "", "443", "", "" }, group.Cells.ToArray());
        Assert.AreEqual("C:\\chrome.exe", group.IconPath);
        Assert.IsTrue(grouped.SetEquals([chrome1, chrome2]));

        expanded.Add(group.Key);
        var open = TableGrouping.Build(sorted, columns, null, expanded, groups, grouped);

        CollectionAssert.AreEqual(new object[] { group, chrome1, chrome2, solo }, open.ToArray());
        Assert.IsTrue(group.IsExpanded);

        var remaining = TableGrouping.Build([chrome1, solo], columns, null, expanded, groups, grouped);

        CollectionAssert.AreEqual(new object[] { chrome1, solo }, remaining.ToArray());
        Assert.IsEmpty(groups);
        Assert.IsEmpty(expanded);
        Assert.IsEmpty(grouped);

        object[] processes = [new Row("azmcp (12748)", 1, "", ""), new Row("azmcp (57704)", 2, "", "")];
        var byProcess = TableGrouping.Build(processes, columns, null, expanded, groups, grouped, TableGrouping.WithoutProcessId);

        Assert.AreEqual("azmcp (2)", ((TableGroup)byProcess.Single()).Label);
        Assert.AreEqual("Setup (x86)", TableGrouping.WithoutProcessId("Setup (x86)"));
    }

    [TestMethod]
    public void ExpandingAndCollapsingOnlyChangesTheGroupsChildren()
    {
        var first = new Row("alpha", 1, "", "");
        var second = new Row("alpha", 2, "", "");
        var otherFirst = new Row("beta", 3, "", "");
        var otherSecond = new Row("beta", 4, "", "");
        var columns = new[] { TableColumn.Text<Row>("name", "Name", 200, row => row.Name) };
        var groups = new Dictionary<string, TableGroup>();
        var grouped = new HashSet<object>();
        var items = new ObservableCollection<object>(TableGrouping.Build(
            [first, second, otherFirst, otherSecond], columns, null, new HashSet<string>(), groups, grouped));
        var alpha = (TableGroup)items[0];
        var beta = (TableGroup)items[1];
        var actions = new List<NotifyCollectionChangedAction>();
        items.CollectionChanged += (_, args) => actions.Add(args.Action);

        TableGrouping.SetExpanded(items, alpha, true);

        CollectionAssert.AreEqual(new object[] { alpha, first, second, beta }, items.ToArray());
        CollectionAssert.AreEqual(new[] { NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Add }, actions);
        Assert.IsTrue(alpha.IsExpanded);

        actions.Clear();
        TableGrouping.SetExpanded(items, beta, true);
        TableGrouping.SetExpanded(items, alpha, false);

        CollectionAssert.AreEqual(new object[] { alpha, beta, otherFirst, otherSecond }, items.ToArray());
        CollectionAssert.AreEqual(new[]
        {
            NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Add,
            NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Remove
        }, actions);
        Assert.IsFalse(alpha.IsExpanded);
        Assert.IsTrue(beta.IsExpanded);

        actions.Clear();
        TableGrouping.SetExpanded(items, alpha, false);
        Assert.IsEmpty(actions);
    }

    private static string[] Names(IReadOnlyList<object> rows) => rows.OfType<Row>().Select(row => row.Name).ToArray();
}
