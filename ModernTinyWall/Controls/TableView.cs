using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using ModernTinyWall.Models;
using ModernTinyWall.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Windows.System;

namespace ModernTinyWall.Controls;

public sealed class TableView : Grid
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(object), typeof(TableView), new PropertyMetadata(null, OnItemsSourceChanged));
    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(
        nameof(ItemTemplate), typeof(DataTemplate), typeof(TableView),
        new PropertyMetadata(null, (sender, args) => ((TableView)sender)._templates.Item = (DataTemplate?)args.NewValue));
    public static readonly DependencyProperty SelectionModeProperty = DependencyProperty.Register(
        nameof(SelectionMode), typeof(ListViewSelectionMode), typeof(TableView),
        new PropertyMetadata(ListViewSelectionMode.Single,
            (sender, args) => ((TableView)sender)._list.SelectionMode = (ListViewSelectionMode)args.NewValue));
    public static readonly DependencyProperty IsRowProperty = DependencyProperty.RegisterAttached(
        "IsRow", typeof(bool), typeof(TableView), new PropertyMetadata(false, OnIsRowChanged));
    private static readonly DependencyProperty OwnerProperty = DependencyProperty.RegisterAttached(
        "Owner", typeof(TableView), typeof(TableView), new PropertyMetadata(null));
    private static readonly DependencyProperty RowItemProperty = DependencyProperty.RegisterAttached(
        "RowItem", typeof(object), typeof(TableView), new PropertyMetadata(null));
    private static readonly DependencyProperty BasePaddingProperty = DependencyProperty.RegisterAttached(
        "BasePadding", typeof(object), typeof(TableView), new PropertyMetadata(null));
    private static readonly Dictionary<string, HashSet<string>> ExpandedGroups = new();

    private readonly IOptionsService _options = new OptionsService();
    private readonly ListView _list = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly Grid _header = new() { Padding = new Thickness(8, 0, 8, 0), ColumnSpacing = 12 };
    private readonly Grid _body = new() { HorizontalAlignment = HorizontalAlignment.Left };
    private readonly InfoBar _status = new() { IsOpen = false, IsClosable = true };
    private readonly HashSet<Grid> _rows = new();
    private readonly List<Button> _headings = new();
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private IReadOnlyList<TableColumn> _columns = [];
    private IReadOnlyList<object> _orderedItems = [];
    private ObservableCollection<object> _displayedItems = [];
    private readonly TableTemplateSelector _templates = new();
    private readonly Dictionary<string, TableGroup> _groups = new();
    private readonly HashSet<object> _groupedItems = new(ReferenceEqualityComparer.Instance);
    private HashSet<string> _expandedKeys = new();
    private bool _groupingEnabled;
    private Func<object, string?>? _groupIconPath;
    private Func<string, string>? _groupName;
    private INotifyCollectionChanged? _observedSource;
    private string _tableId = string.Empty;
    private string _sortKey = string.Empty;
    private bool _descending;
    private bool _refreshQueued;
    private int _interactionRevision;
    private ScrollViewer? _listScroller;

    public object? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public ListViewSelectionMode SelectionMode
    {
        get => (ListViewSelectionMode)GetValue(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    public object? SelectedItem => _list.SelectedItem;
    public IList<object> SelectedItems => _list.SelectedItems;
    public event SelectionChangedEventHandler? SelectionChanged;

    internal IReadOnlyList<object> OrderedItems
    {
        get
        {
            RefreshItems();
            return _orderedItems;
        }
    }

    public static bool GetIsRow(DependencyObject element) => (bool)element.GetValue(IsRowProperty);
    public static void SetIsRow(DependencyObject element, bool value) => element.SetValue(IsRowProperty, value);

    public TableView()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition());
        Children.Add(_status);
        _body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _body.RowDefinitions.Add(new RowDefinition());
        _body.Children.Add(_header);
        SetRow(_list, 1);
        _body.Children.Add(_list);
        var scroller = new ScrollViewer
        {
            Content = _body,
            HorizontalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalContentAlignment = HorizontalAlignment.Left
        };
        SetRow(scroller, 1);
        Children.Add(scroller);
        ScrollViewer.SetHorizontalScrollMode(_list, ScrollMode.Disabled);
        ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled);
        var style = new Style(typeof(ListViewItem));
        style.Setters.Add(new Setter(ListViewItem.PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(ListViewItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        _list.ItemContainerStyle = style;
        _list.ItemTemplateSelector = _templates;
        _list.ContainerContentChanging += List_ContainerContentChanging;
        _list.KeyDown += List_KeyDown;
        _list.SelectionChanged += (_, args) => SelectionChanged?.Invoke(this, args);
        _saveTimer.Tick += SaveTimer_Tick;
        Loaded += Table_Loaded;
        Unloaded += Table_Unloaded;
    }

    internal void Configure(string tableId, params TableColumn[] columns)
    {
        if (columns.Length == 0 || columns.Select(column => column.Key).Distinct().Count() != columns.Length)
            throw new ArgumentException("A table requires uniquely keyed columns.", nameof(columns));
        _tableId = tableId;
        _columns = columns;
        _sortKey = columns[0].Key;
        BuildHeader();
        RefreshItems();
    }

    // Groups rows with the same first-column name, like Task Manager's process groups.
    internal void EnableGrouping(Func<object, string?>? iconPath = null, Func<string, string>? groupName = null)
    {
        if (_columns.Count == 0 || _columns[0].Display is null)
            throw new InvalidOperationException("Configure the table with a text first column before enabling grouping.");
        _groupingEnabled = true;
        _groupIconPath = iconPath;
        _groupName = groupName;
        if (!ExpandedGroups.TryGetValue(_tableId, out var expanded))
            ExpandedGroups[_tableId] = expanded = new HashSet<string>();
        _expandedKeys = expanded;
        RefreshItems(force: true);
    }

    private void SetGroupExpanded(TableGroup group, bool expanded)
    {
        if (group.IsExpanded == expanded)
            return;
        TableGrouping.SetExpanded(_displayedItems, group, expanded);
        if (expanded)
            _expandedKeys.Add(group.Key);
        else
            _expandedKeys.Remove(group.Key);
        if ((_list.ContainerFromItem(group) as ListViewItem)?.ContentTemplateRoot is Grid row)
            UpdateGroupExpansionVisual(row, group);
        if (SelectionMode == ListViewSelectionMode.Single)
            _list.SelectedItem = group;
    }

    private TableGroup? GroupOf(object item) =>
        _groupedItems.Contains(item) ? _groups.Values.FirstOrDefault(group => group.Items.Contains(item)) : null;

    private void List_KeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (!_groupingEnabled || args.OriginalSource is not ListViewItem { Content: { } item })
            return;
        if (item is TableGroup group && args.Key is VirtualKey.Right or VirtualKey.Left or VirtualKey.Enter or VirtualKey.Space)
        {
            if (args.Key == VirtualKey.Right && group.IsExpanded && group.Items.Count > 0)
                _list.SelectedItem = group.Items[0];
            else
                SetGroupExpanded(group, args.Key switch
                {
                    VirtualKey.Right => true,
                    VirtualKey.Left => false,
                    _ => !group.IsExpanded
                });
            args.Handled = true;
        }
        else if (item is not TableGroup && args.Key == VirtualKey.Left && GroupOf(item) is { } parent)
        {
            SetGroupExpanded(parent, false);
            (_list.ContainerFromItem(parent) as Control)?.Focus(FocusState.Keyboard);
            args.Handled = true;
        }
    }

    private void List_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue || args.ItemContainer.ContentTemplateRoot is not Grid row)
            return;
        row.SetValue(RowItemProperty, args.Item);
        if (args.Item is TableGroup group)
            PopulateGroupRow(row, group);
        if (row.GetValue(OwnerProperty) is TableView)
            ApplyRowWidths(row);
    }

    private void PopulateGroupRow(Grid row, TableGroup group)
    {
        if (!GetIsRow(row))
        {
            row.DoubleTapped += (_, args) =>
            {
                if (row.GetValue(RowItemProperty) is TableGroup tapped)
                {
                    SetGroupExpanded(tapped, !tapped.IsExpanded);
                    args.Handled = true;
                }
            };
            SetIsRow(row, true);
            if (row.IsLoaded)
                Row_Loaded(row, new RoutedEventArgs());
        }
        row.Children.Clear();
        while (row.ColumnDefinitions.Count < _columns.Count)
            row.ColumnDefinitions.Add(new ColumnDefinition());
        while (row.ColumnDefinitions.Count > _columns.Count)
            row.ColumnDefinitions.RemoveAt(row.ColumnDefinitions.Count - 1);
        var name = new Grid { ColumnSpacing = TableRowLayout.GroupToggleSpacing };
        name.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TableRowLayout.GroupToggleWidth) });
        name.ColumnDefinitions.Add(new ColumnDefinition());
        var toggle = new Button
        {
            Width = TableRowLayout.GroupToggleWidth,
            Height = 20,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            IsTabStop = false,
            Content = new FontIcon { FontSize = 10 }
        };
        toggle.Click += (_, _) => SetGroupExpanded(group, !group.IsExpanded);
        name.Children.Add(toggle);

        var nameContent = new Grid { ColumnSpacing = 8 };
        if (_groupIconPath is not null)
        {
            nameContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            nameContent.ColumnDefinitions.Add(new ColumnDefinition());
            nameContent.Children.Add(new ApplicationIcon { SourcePath = group.IconPath ?? string.Empty });
        }
        var label = new TextBlock
        {
            Text = group.Label,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTipService.SetToolTip(label, group.Label);
        if (_groupIconPath is not null)
            SetColumn(label, 1);
        nameContent.Children.Add(label);
        SetColumn(nameContent, 1);
        name.Children.Add(nameContent);
        row.Children.Add(name);

        for (var index = 1; index < _columns.Count; index++)
        {
            var cell = new TextBlock
            {
                Text = index < group.Cells.Count ? group.Cells[index] : string.Empty,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };
            SetColumn(cell, index);
            row.Children.Add(cell);
        }
        UpdateGroupExpansionVisual(row, group);
    }

    private static void UpdateGroupExpansionVisual(Grid row, TableGroup group)
    {
        var toggle = (Button)((Grid)row.Children[0]).Children[0];
        ((FontIcon)toggle.Content).Glyph = group.IsExpanded ? "\uE70D" : "\uE76C";
        AutomationProperties.SetName(row, group.ToString());
        AutomationProperties.SetName(toggle, $"{(group.IsExpanded ? "Collapse" : "Expand")} {group.Name}");
        ToolTipService.SetToolTip(toggle, group.IsExpanded ? "Collapse group" : "Expand group");
    }

    private void BuildHeader()
    {
        _header.Children.Clear();
        _header.ColumnDefinitions.Clear();
        _headings.Clear();
        for (var index = 0; index < _columns.Count; index++)
        {
            var column = _columns[index];
            _header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(column.Width) });
            var cell = new Grid();
            var button = new Button
            {
                Padding = new Thickness(0, 8, 10, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                Content = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis }
            };
            button.Click += (_, _) =>
            {
                _descending = _sortKey == column.Key && !_descending;
                _sortKey = column.Key;
                UpdateHeadingLabels();
                RefreshItems(resetScroll: true);
                ScheduleSave();
            };
            ToolTipService.SetToolTip(button, $"Sort by {column.Heading}. Click again to reverse the order.");
            cell.Children.Add(button);
            _headings.Add(button);
            var grip = new ColumnResizeGrip
            {
                Width = 8,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            AutomationProperties.SetName(grip.Handle, $"Resize {column.Heading} column");
            ToolTipService.SetToolTip(grip, "Drag to resize. Left and right arrow keys resize by 10 pixels.");
            grip.Handle.DragStarted += (_, _) => _interactionRevision++;
            grip.Handle.DragDelta += (_, args) =>
            {
                column.Width = Math.Clamp(column.Width + args.HorizontalChange, 48, 1800);
                ApplyWidths();
            };
            grip.Handle.DragCompleted += (_, _) => ScheduleSave();
            grip.Handle.KeyDown += (_, args) =>
            {
                if (args.Key is VirtualKey.Left or VirtualKey.Right)
                {
                    column.Width = Math.Clamp(column.Width + (args.Key == VirtualKey.Left ? -10 : 10), 48, 1800);
                    ApplyWidths();
                    ScheduleSave();
                    args.Handled = true;
                }
            };
            cell.Children.Add(grip);
            SetColumn(cell, index);
            _header.Children.Add(cell);
        }
        UpdateHeadingLabels();
        ApplyWidths();
    }

    private void UpdateHeadingLabels()
    {
        for (var index = 0; index < _columns.Count; index++)
        {
            var column = _columns[index];
            var direction = column.Key == _sortKey ? (_descending ? "descending" : "ascending") : "not sorted";
            ((TextBlock)_headings[index].Content).Text = column.Heading
                + (column.Key == _sortKey ? (_descending ? " \u2193" : " \u2191") : string.Empty);
            AutomationProperties.SetName(_headings[index], $"{column.Heading}, {direction}");
        }
    }

    private void ApplyWidths()
    {
        _body.Width = _columns.Sum(column => column.Width) + Math.Max(0, _columns.Count - 1) * 12 + 16;
        for (var index = 0; index < _columns.Count; index++)
            _header.ColumnDefinitions[index].Width = new GridLength(_columns[index].Width);
        foreach (var row in _rows)
            ApplyRowWidths(row);
    }

    private void ApplyRowWidths(Grid row)
    {
        if (row.ColumnDefinitions.Count != _columns.Count)
            throw new InvalidOperationException($"The row template for table '{_tableId}' does not match its columns.");
        row.MinWidth = 0;
        var item = row.GetValue(RowItemProperty) ?? row.DataContext;
        if (row.GetValue(BasePaddingProperty) is not Thickness basePadding)
        {
            basePadding = row.Padding;
            row.SetValue(BasePaddingProperty, basePadding);
        }
        var indent = TableRowLayout.Indent(_groupingEnabled && item is not null,
            item is TableGroup, item is not null && _groupedItems.Contains(item));
        row.Padding = new Thickness(basePadding.Left + indent, basePadding.Top, basePadding.Right, basePadding.Bottom);
        for (var index = 0; index < _columns.Count; index++)
        {
            row.ColumnDefinitions[index].MinWidth = 0;
            row.ColumnDefinitions[index].Width = new GridLength(Math.Max(0, _columns[index].Width - (index == 0 ? indent : 0)));
        }
    }

    private static void OnIsRowChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not Grid row)
            throw new ArgumentException("Table rows must be grids.");
        if ((bool)args.NewValue)
        {
            row.Loaded += Row_Loaded;
            row.Unloaded += Row_Unloaded;
        }
        else
        {
            Row_Unloaded(row, new RoutedEventArgs());
            row.Loaded -= Row_Loaded;
            row.Unloaded -= Row_Unloaded;
        }
    }

    private static void Row_Loaded(object sender, RoutedEventArgs args)
    {
        var row = (Grid)sender;
        for (DependencyObject? parent = row; parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is TableView table)
            {
                row.SetValue(OwnerProperty, table);
                table._rows.Add(row);
                table.ApplyRowWidths(row);
                return;
            }
        }
    }

    private static void Row_Unloaded(object sender, RoutedEventArgs args)
    {
        var row = (Grid)sender;
        if (row.GetValue(OwnerProperty) is TableView owner)
            owner._rows.Remove(row);
        row.ClearValue(OwnerProperty);
    }

    private static void OnItemsSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var table = (TableView)sender;
        table.ObserveSource();
        table.QueueRefresh();
    }

    private void ObserveSource()
    {
        if (_observedSource is not null)
            _observedSource.CollectionChanged -= Source_CollectionChanged;
        _observedSource = IsLoaded ? ItemsSource as INotifyCollectionChanged : null;
        if (_observedSource is not null)
            _observedSource.CollectionChanged += Source_CollectionChanged;
    }

    private void Source_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs args) => QueueRefresh();

    private void QueueRefresh()
    {
        if (_refreshQueued || !IsLoaded)
            return;
        _refreshQueued = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _refreshQueued = false;
            if (IsLoaded)
                RefreshItems();
        });
    }

    private void RefreshItems(bool resetScroll = false, bool force = false)
    {
        if (_columns.Count == 0)
            return;
        var source = ItemsSource is IEnumerable enumerable ? enumerable.Cast<object>() : [];
        var sort = _columns.FirstOrDefault(column => column.Key == _sortKey) ?? _columns[0];
        var sorted = TableOrdering.Sort(source, sort, _columns[0], _descending);
        if (!resetScroll && !force && sorted.SequenceEqual(_orderedItems))
            return;
        var selected = _list.SelectedItems.ToArray();
        var offset = resetScroll ? 0 : _listScroller?.VerticalOffset ?? 0;
        _orderedItems = sorted;
        var displayed = _groupingEnabled
            ? TableGrouping.Build(sorted, _columns, _groupIconPath, _expandedKeys, _groups, _groupedItems, _groupName)
            : sorted;
        _displayedItems = new ObservableCollection<object>(displayed);
        _list.ItemsSource = _displayedItems;
        if (SelectionMode == ListViewSelectionMode.Single)
            _list.SelectedItem = selected.FirstOrDefault(item => displayed.Contains(item));
        else if (SelectionMode is ListViewSelectionMode.Multiple or ListViewSelectionMode.Extended)
            foreach (var item in selected)
                if (displayed.Contains(item) && !_list.SelectedItems.Contains(item))
                    _list.SelectedItems.Add(item);
        DispatcherQueue.TryEnqueue(() => _listScroller?.ChangeView(null, offset, null, true));
    }

    private async void Table_Loaded(object sender, RoutedEventArgs args)
    {
        ObserveSource();
        _listScroller = FindScroller(_list);
        RefreshItems();
        var revision = _interactionRevision;
        try
        {
            var options = await _options.LoadAsync();
            if (!IsLoaded || revision != _interactionRevision || !options.Tables.TryGetValue(_tableId, out var saved))
                return;
            var widths = _columns.Select(column => saved.GetWidth(column.Key, column.Width)).ToArray();
            for (var index = 0; index < _columns.Count; index++)
                _columns[index].Width = widths[index];
            _sortKey = _columns.Any(column => column.Key == saved.SortColumn) ? saved.SortColumn : _columns[0].Key;
            _descending = _sortKey == saved.SortColumn && saved.Descending;
            ApplyWidths();
            UpdateHeadingLabels();
            RefreshItems();
        }
        catch (Exception ex)
        {
            ShowError($"Could not load table preferences: {ex.Message}");
        }
    }

    private void Table_Unloaded(object sender, RoutedEventArgs args)
    {
        if (_observedSource is not null)
            _observedSource.CollectionChanged -= Source_CollectionChanged;
        _observedSource = null;
        _listScroller = null;
        if (_saveTimer.IsEnabled)
        {
            _saveTimer.Stop();
            SavePreferences();
        }
    }

    private void ScheduleSave()
    {
        _interactionRevision++;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveTimer_Tick(object? sender, object args)
    {
        _saveTimer.Stop();
        SavePreferences();
    }

    private async void SavePreferences()
    {
        var preferences = new TablePreferences
        {
            SortColumn = _sortKey,
            Descending = _descending,
            ColumnWidths = _columns.ToDictionary(column => column.Key, column => column.Width)
        };
        try
        {
            await _options.UpdateAsync(options =>
            {
                var tables = new Dictionary<string, TablePreferences>(options.Tables) { [_tableId] = preferences };
                return options with { Tables = tables };
            });
        }
        catch (Exception ex)
        {
            ShowError($"Could not save table preferences: {ex.Message}");
        }
    }

    private void ShowError(string message)
    {
        _status.Severity = InfoBarSeverity.Error;
        _status.Title = "Table preferences";
        _status.Message = message;
        _status.IsOpen = true;
    }

    private static ScrollViewer? FindScroller(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is ScrollViewer scroller)
                return scroller;
            if (FindScroller(child) is { } found)
                return found;
        }
        return null;
    }

    private sealed partial class TableTemplateSelector : DataTemplateSelector
    {
        private static readonly DataTemplate GroupTemplate = (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(
            "<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">"
            + "<Grid Padding=\"8\" ColumnSpacing=\"12\" Background=\"Transparent\" /></DataTemplate>");

        public DataTemplate? Item { get; set; }

        protected override DataTemplate? SelectTemplateCore(object item) => item is TableGroup ? GroupTemplate : Item;

        protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
    }

    private sealed class ColumnResizeGrip : Grid
    {
        public Thumb Handle { get; } = new() { IsTabStop = true };

        public ColumnResizeGrip()
        {
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
            Handle.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            Children.Add(Handle);
        }
    }
}
