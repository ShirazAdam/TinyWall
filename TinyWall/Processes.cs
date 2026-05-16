using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace pylorak.TinyWall
{
    internal partial class ProcessesForm : Form
    {
        internal readonly List<ProcessInfo> Selection = [];

        private readonly Size _iconSize = new((int)Math.Round(16 * Utils.DpiScalingFactor),
            (int)Math.Round(16 * Utils.DpiScalingFactor));

        private string _searchItem = string.Empty;

        internal ProcessesForm(bool multiSelect)
        {
            InitializeComponent();
            Utils.SetRightToLeft(this);
            IconList.ImageSize = _iconSize;
            listView.MultiSelect = multiSelect;
            Icon = Resources.Icons.firewall;
            btnOK.Image = GlobalInstances.ApplyBtnIcon;
            btnCancel.Image = GlobalInstances.CancelBtnIcon;

            IconList.Images.Add("store", Resources.Icons.store);
            IconList.Images.Add("system", Resources.Icons.windows_small);
            IconList.Images.Add("network-drive", Resources.Icons.network_drive_small);
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < listView.SelectedItems.Count; ++i)
            {
                Selection.Add((ProcessInfo)listView.SelectedItems[i].Tag);
            }

            DialogResult = DialogResult.OK;
        }

        private void listView_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnOK.Enabled = listView.SelectedItems.Count > 0;
        }

        private void listView_DoubleClick(object sender, EventArgs e)
        {
            if (btnOK.Enabled)
            {
                btnOK_Click(btnOK, EventArgs.Empty);
            }
        }

        private async void ProcessesForm_Load(object sender, EventArgs ev)
        {
            try
            {
                Icon = Resources.Icons.firewall;
                if (ActiveConfig.Controller.ProcessesFormWindowSize.Width != 0)
                    Size = ActiveConfig.Controller.ProcessesFormWindowSize;
                if (ActiveConfig.Controller.ProcessesFormWindowLoc.X != 0)
                {
                    Location = ActiveConfig.Controller.ProcessesFormWindowLoc;
                    Utils.FixupFormPosition(this);
                }

                WindowState = ActiveConfig.Controller.ProcessesFormWindowState;

                await UpdateListAsync();
            }
            catch
            {
                // ignored
            }
        }

        private async Task UpdateListAsync()
        {
            lblPleaseWait.Visible = true;
            Enabled = false;

            foreach (ColumnHeader col in listView.Columns)
            {
                if (ActiveConfig.Controller.ProcessesFormColumnWidths.TryGetValue((string)col.Tag, out int width))
                    col.Width = width;
            }

            var searchItem = _searchItem;

            try
            {
                var processes = await Task.Run(() => BuildProcessEntries(searchItem));
                var itemColl = new List<ListViewItem>(processes.Count);

                foreach (var entry in processes)
                {
                    var li = new ListViewItem(entry.DisplayName);
                    li.SubItems.Add(string.Join(", ", entry.Process.Services.ToArray()));
                    li.SubItems.Add(entry.Process.Path);
                    li.Tag = entry.Process;
                    itemColl.Add(li);

                    if (entry.PathMetadata.ImageKey is not null)
                    {
                        if ((entry.PathMetadata.IconPng is not null) && !IconList.Images.ContainsKey(entry.PathMetadata.ImageKey))
                        {
                            using var iconStream = new MemoryStream(entry.PathMetadata.IconPng);
                            IconList.Images.Add(entry.PathMetadata.ImageKey, Image.FromStream(iconStream));
                        }

                        li.ImageKey = entry.PathMetadata.ImageKey;
                    }
                }

                Utils.SetDoubleBuffering(listView, true);
                listView.BeginUpdate();
                try
                {
                    listView.Items.Clear();
                    listView.ListViewItemSorter = new ListViewItemComparer(0);
                    listView.Items.AddRange([.. itemColl]);
                }
                finally
                {
                    listView.EndUpdate();
                }
            }
            finally
            {
                lblPleaseWait.Visible = false;
                Enabled = true;
            }
        }

        private static List<ProcessListEntry> BuildProcessEntries(string searchItem)
        {
            var entries = new List<ProcessListEntry>();
            var packageList = new UwpPackageList();
            var servicePids = new ServicePidMap();

            Process[] procs = Process.GetProcesses();

            if (!string.IsNullOrWhiteSpace(searchItem))
                procs = [.. procs.Where(p => p.ProcessName.Contains(searchItem, StringComparison.CurrentCultureIgnoreCase))];

            foreach (var t in procs)
            {
                using Process p = t;
                try
                {
                    var pid = unchecked((uint)p.Id);
                    var processInfo = ProcessInfo.Create(pid, packageList, servicePids);

                    if (string.IsNullOrEmpty(processInfo.Path))
                        continue;

                    // Scan list of already added items to prevent duplicates.
                    bool skip = entries.Select(t1 => t1.Process).Any(opi =>
                        (processInfo.Package == opi.Package) && (processInfo.Path == opi.Path) && (processInfo.Services.SetEquals(opi.Services)));

                    if (skip)
                        continue;

                    var displayName = processInfo.Package.HasValue ? processInfo.Package.Value.Name : p.ProcessName;
                    var pathMetadata = PathMetadataCache.Get(processInfo.Path, processInfo.Package.HasValue, 16, 16);
                    entries.Add(new ProcessListEntry(processInfo, displayName, pathMetadata));
                }
                catch
                {
                    // ignored
                }
            }

            return entries;
        }

        private sealed record ProcessListEntry(ProcessInfo Process, string DisplayName, PathMetadata PathMetadata);

        private void listView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            var oldSorter = (ListViewItemComparer)listView.ListViewItemSorter;
            var newSorter = new ListViewItemComparer(e.Column);
            if ((oldSorter != null) && (oldSorter.Column == newSorter.Column))
                newSorter.Ascending = !oldSorter.Ascending;

            listView.ListViewItemSorter = newSorter;
        }

        private void ProcessesForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            ActiveConfig.Controller.ProcessesFormWindowState = this.WindowState;
            if (this.WindowState == FormWindowState.Normal)
            {
                ActiveConfig.Controller.ProcessesFormWindowSize = this.Size;
                ActiveConfig.Controller.ProcessesFormWindowLoc = this.Location;
            }
            else
            {
                ActiveConfig.Controller.ProcessesFormWindowSize = this.RestoreBounds.Size;
                ActiveConfig.Controller.ProcessesFormWindowLoc = this.RestoreBounds.Location;
            }

            ActiveConfig.Controller.ProcessesFormColumnWidths.Clear();
            foreach (ColumnHeader col in listView.Columns)
                ActiveConfig.Controller.ProcessesFormColumnWidths.Add((string)col.Tag, col.Width);

            ActiveConfig.Controller.Save();
        }

        private void txtBxSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode is Keys.Enter or Keys.Return)
            {
                btnSearch.PerformClick();
            }
        }

        private async void btnClear_Click(object sender, EventArgs e)
        {
            _searchItem = string.Empty;
            txtBxSearch.Text = string.Empty;

            await UpdateListAsync();
        }

        private async void btnSearch_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtBxSearch.Text))
            {
                return;
            }

            _searchItem = txtBxSearch.Text.ToLower();

            await UpdateListAsync();
        }
    }
}