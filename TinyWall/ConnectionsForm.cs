using ModernTinyWall.Windows;
using ModernTinyWall.Windows.NetStat;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModernTinyWall.TinyWall
{
    internal partial class ConnectionsForm : Form
    {
        private readonly TinyWallController _controller;
        private readonly Size _iconSize = new((int)Math.Round(16 * Utils.DpiScalingFactor), (int)Math.Round(16 * Utils.DpiScalingFactor));
        private string _searchText = string.Empty;
        private List<ListViewItem> _itemColl = new();

        internal ConnectionsForm(TinyWallController ctrl)
        {
            InitializeComponent();
            Utils.SetRightToLeft(this);
            this.IconList.ImageSize = _iconSize;
            this.Icon = Resources.Icons.firewall;
            this._controller = ctrl;

            this.IconList.Images.Add("store", Resources.Icons.store);
            this.IconList.Images.Add("system", Resources.Icons.windows_small);
            this.IconList.Images.Add("network-drive", Resources.Icons.network_drive_small);
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private string GetPathFromPidCached(Dictionary<uint, string> cache, uint pid)
        {
            if (cache.TryGetValue(pid, out var cached))
                return cached;
            else
            {
                string ret = Utils.GetPathOfProcessUseTwService(pid, GlobalInstances.Controller);
                cache.Add(pid, ret);
                return ret;
            }
        }

        private async Task UpdateListAsync()
        {
            lblPleaseWait.Visible = true;
            Enabled = false;

            var showListen = chkShowListen.Checked;
            var showActive = chkShowActive.Checked;
            var showBlocked = chkShowBlocked.Checked;
            var searchText = _searchText;
            var now = DateTime.Now;
            var fwLogRequest = GlobalInstances.Controller.BeginReadFwLog();

            try
            {
                var entries = await Task.Run(() => BuildConnectionEntries(showListen, showActive, showBlocked, fwLogRequest, now));
                var items = new List<ListViewItem>(entries.Count);

                foreach (var entry in entries)
                {
                    ConstructListItem(items, entry);
                }

                if (!string.IsNullOrWhiteSpace(searchText))
                    items = [.. items.Where(item => item.SubItems[0].Text.Contains(searchText, StringComparison.CurrentCultureIgnoreCase))];

                _itemColl = items;

                list.BeginUpdate();
                try
                {
                    list.Items.Clear();
                    list.Items.AddRange([.. _itemColl]);
                }
                finally
                {
                    list.EndUpdate();
                }
            }
            finally
            {
                lblPleaseWait.Visible = false;
                Enabled = true;
            }
        }

        private List<ConnectionListEntry> BuildConnectionEntries(bool showListen, bool showActive, bool showBlocked, TwRequest fwLogRequest, DateTime now)
        {
            var entries = new List<ConnectionListEntry>();
            var packageList = new UwpPackageList();
            var procCache = new Dictionary<uint, string>();
            var servicePids = new ServicePidMap();

            // Retrieve IP tables while waiting for log entries.
            TcpTable tcpTable = NetStat.GetExtendedTcp4Table(false);

            foreach (TcpRow tcpRow in tcpTable)
            {
                if ((!showListen || (tcpRow.State != TcpState.Listen))
                    && (!showActive || (tcpRow.State == TcpState.Listen))) continue;

                var path = GetPathFromPidCached(procCache, tcpRow.ProcessId);
                var pi = ProcessInfo.Create(tcpRow.ProcessId, path, packageList, servicePids);
                entries.Add(CreateConnectionListEntry(pi, "TCP", tcpRow.LocalEndPoint, tcpRow.RemoteEndPoint, tcpRow.State.ToString(), now, RuleDirection.Invalid));
            }

            tcpTable = NetStat.GetExtendedTcp6Table(false);

            foreach (TcpRow tcpRow in tcpTable)
            {
                if ((!showListen || (tcpRow.State != TcpState.Listen))
                    && (!showActive || (tcpRow.State == TcpState.Listen))) continue;

                var path = GetPathFromPidCached(procCache, tcpRow.ProcessId);
                var pi = ProcessInfo.Create(tcpRow.ProcessId, path, packageList, servicePids);
                entries.Add(CreateConnectionListEntry(pi, "TCP", tcpRow.LocalEndPoint, tcpRow.RemoteEndPoint, tcpRow.State.ToString(), now, RuleDirection.Invalid));
            }

            if (showListen)
            {
                var dummyEp = new IPEndPoint(0, 0);
                var udpTable = NetStat.GetExtendedUdp4Table(false);

                foreach (UdpRow udpRow in udpTable)
                {
                    var path = GetPathFromPidCached(procCache, udpRow.ProcessId);
                    var pi = ProcessInfo.Create(udpRow.ProcessId, path, packageList, servicePids);
                    entries.Add(CreateConnectionListEntry(pi, "UDP", udpRow.LocalEndPoint, dummyEp, "Listen", now, RuleDirection.Invalid));
                }

                udpTable = NetStat.GetExtendedUdp6Table(false);

                foreach (UdpRow udpRow in udpTable)
                {
                    var path = GetPathFromPidCached(procCache, udpRow.ProcessId);
                    var pi = ProcessInfo.Create(udpRow.ProcessId, path, packageList, servicePids);
                    entries.Add(CreateConnectionListEntry(pi, "UDP", udpRow.LocalEndPoint, dummyEp, "Listen", now, RuleDirection.Invalid));
                }
            }

            // Finished reading tables, continue with log processing.
            var fwLog = Controller.EndReadFwLog(fwLogRequest.Response);

            if (showBlocked)
            {
                AddBlockedLogEntries(entries, fwLog, packageList, servicePids, now);
            }

            return entries;
        }

        private static void AddBlockedLogEntries(List<ConnectionListEntry> entries, FirewallLogEntry[] fwLog, UwpPackageList packageList, ServicePidMap servicePids, DateTime now)
        {
            var processPathInfoMap = new Dictionary<string, List<ProcessSnapshotEntry>>();
            foreach (var p in ProcessManager.CreateToolhelp32SnapshotExtended())
            {
                if (string.IsNullOrWhiteSpace(p.ImagePath))
                    continue;

                var key = p.ImagePath.ToLowerInvariant();
                if (!processPathInfoMap.ContainsKey(key))
                    processPathInfoMap.Add(key, new List<ProcessSnapshotEntry>());
                processPathInfoMap[key].Add(p);
            }

            foreach (var e in fwLog)
            {
                if (e.AppPath is null) continue;

                var key = e.AppPath.ToLowerInvariant();
                if (!processPathInfoMap.ContainsKey(key))
                    continue;

                var p = processPathInfoMap[key];
                if ((p.Count == 1) && (p[0].CreationTime < e.Timestamp.ToFileTime()))
                    e.ProcessId = p[0].ProcessId;
            }

            var filteredLog = new List<FirewallLogEntry>();
            var refSpan = TimeSpan.FromMinutes(5);
            foreach (var newEntry in fwLog)
            {
                // Ignore log entries older than refSpan
                TimeSpan span = now - newEntry.Timestamp;
                if (span > refSpan)
                    continue;

                switch (newEntry.Event)
                {
                    case EventLogEvent.ALLOWED_LISTEN:
                    case EventLogEvent.ALLOWED_CONNECTION:
                    case EventLogEvent.ALLOWED_LOCAL_BIND:
                    case EventLogEvent.ALLOWED:
                        newEntry.Event = EventLogEvent.ALLOWED;
                        break;
                    case EventLogEvent.BLOCKED_LISTEN:
                    case EventLogEvent.BLOCKED_CONNECTION:
                    case EventLogEvent.BLOCKED_LOCAL_BIND:
                    case EventLogEvent.BLOCKED_PACKET:
                    case EventLogEvent.BLOCKED:
                        var matchFound = false;
                        newEntry.Event = EventLogEvent.BLOCKED;

                        foreach (var oldEntry in filteredLog.Where(oldEntry => oldEntry.Equals(newEntry, false)))
                        {
                            matchFound = true;
                            oldEntry.Timestamp = newEntry.Timestamp;
                            break;
                        }

                        if (!matchFound)
                            filteredLog.Add(newEntry);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            foreach (var entry in filteredLog)
            {
                // Correct path capitalisation.
                // TODO: Do this in the service, and minimise overhead. Right now if GetExactPath() fails,
                // for example due to missing file system privileges, capitalisation will not be corrected.
                // The service has much more privileges, so doing this in the service would allow more paths
                // to be corrected.
                entry.AppPath = Utils.GetExactPath(entry.AppPath);

                var pi = ProcessInfo.Create(entry.ProcessId, entry.AppPath ?? string.Empty, entry.PackageId, packageList, servicePids);

                if (entry is { LocalIp: not null, RemoteIp: not null })
                {
                    entries.Add(CreateConnectionListEntry(
                        pi,
                        entry.Protocol.ToString(),
                        new IPEndPoint(IPAddress.Parse(entry.LocalIp), entry.LocalPort),
                        new IPEndPoint(IPAddress.Parse(entry.RemoteIp), entry.RemotePort),
                        "Blocked",
                        entry.Timestamp,
                        entry.Direction));
                }
            }
        }

        private static ConnectionListEntry CreateConnectionListEntry(ProcessInfo process, string protocol, IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string state, DateTime timestamp, RuleDirection direction)
        {
            var pathMetadata = PathMetadataCache.Get(process.Path, process.Package.HasValue, 16, 16);
            return new ConnectionListEntry(process, protocol, localEndPoint, remoteEndPoint, state, timestamp, direction, pathMetadata.ImageKey, pathMetadata.IconPng);
        }

        private sealed record ConnectionListEntry(ProcessInfo Process, string Protocol, IPEndPoint LocalEndPoint, IPEndPoint RemoteEndPoint, string State, DateTime Timestamp, RuleDirection Direction, string? ImageKey, byte[]? IconPng);

        private void ConstructListItem(List<ListViewItem> itemColl, ConnectionListEntry entry)
        {
            try
            {
                var e = entry.Process;

                // Construct list item
                string name = e.Package.HasValue ? e.Package.Value.Name : System.IO.Path.GetFileName(e.Path);
                string title = (e.Pid != 0) ? $"{name} ({e.Pid})" : $"{name}";
                ListViewItem li = new(title)
                {
                    Tag = e,
                    ToolTipText = e.Path
                };

                if (entry.ImageKey is not null)
                {
                    if ((entry.IconPng is not null) && !IconList.Images.ContainsKey(entry.ImageKey))
                    {
                        using var iconStream = new MemoryStream(entry.IconPng);
                        IconList.Images.Add(entry.ImageKey, Image.FromStream(iconStream));
                    }

                    li.ImageKey = entry.ImageKey;
                }

                li.SubItems.Add(e.Pid == 0 ? string.Empty : string.Join(", ", e.Services.ToArray()));
                li.SubItems.Add(entry.Protocol);
                li.SubItems.Add(entry.LocalEndPoint.Port.ToString(CultureInfo.InvariantCulture).PadLeft(5));
                li.SubItems.Add(entry.LocalEndPoint.Address.ToString());
                li.SubItems.Add(entry.RemoteEndPoint.Port.ToString(CultureInfo.InvariantCulture).PadLeft(5));
                li.SubItems.Add(entry.RemoteEndPoint.Address.ToString());
                li.SubItems.Add(entry.State);

                switch (entry.Direction)
                {
                    case RuleDirection.In:
                        li.SubItems.Add(Resources.Messages.TrafficIn);
                        break;
                    case RuleDirection.Out:
                        li.SubItems.Add(Resources.Messages.TrafficOut);
                        break;
                    case RuleDirection.InOut:
                    case RuleDirection.Invalid:
                    default:
                        li.SubItems.Add(string.Empty);
                        break;
                }
                li.SubItems.Add(entry.Timestamp.ToString("dd/MM/yyyy HH:mm:ss"));
                itemColl.Add(li);
            }
            catch
            {
                // Most probably process ID has become invalid,
                // but we also catch other errors too.
                // Simply do not add item to the list.
            }
        }

        private void list_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            var oldSorter = (ListViewItemComparer)list.ListViewItemSorter;
            var newSorter = new ListViewItemComparer(e.Column);

            if ((oldSorter != null) && (oldSorter.Column == newSorter.Column))
                newSorter.Ascending = !oldSorter.Ascending;

            list.ListViewItemSorter = newSorter;
        }

        private async void btnRefresh_Click(object sender, EventArgs e)
        {
            await UpdateListAsync();
        }

        private async void chkShowListen_CheckedChanged(object sender, EventArgs e)
        {
            await UpdateListAsync();
        }

        private async void chkShowBlocked_CheckedChanged(object sender, EventArgs e)
        {
            await UpdateListAsync();
        }

        private async void chkShowActive_CheckedChanged(object sender, EventArgs e)
        {
            await UpdateListAsync();
        }

        private void ConnectionsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            ActiveConfig.Controller.ConnFormWindowState = this.WindowState;
            if (this.WindowState == FormWindowState.Normal)
            {
                ActiveConfig.Controller.ConnFormWindowSize = this.Size;
                ActiveConfig.Controller.ConnFormWindowLoc = this.Location;
            }
            else
            {
                ActiveConfig.Controller.ConnFormWindowSize = this.RestoreBounds.Size;
                ActiveConfig.Controller.ConnFormWindowLoc = this.RestoreBounds.Location;
            }

            ActiveConfig.Controller.ConnFormShowConnections = this.chkShowActive.Checked;
            ActiveConfig.Controller.ConnFormShowOpenPorts = this.chkShowListen.Checked;
            ActiveConfig.Controller.ConnFormShowBlocked = this.chkShowBlocked.Checked;

            ActiveConfig.Controller.ConnFormColumnWidths.Clear();

            foreach (ColumnHeader col in list.Columns)
                ActiveConfig.Controller.ConnFormColumnWidths.Add((string)col.Tag, col.Width);

            ActiveConfig.Controller.Save();
        }

        private async void ConnectionsForm_Load(object sender, EventArgs e)
        {
            Utils.SetDoubleBuffering(list, true);
            list.ListViewItemSorter = new ListViewItemComparer(9, null, false);
            if (ActiveConfig.Controller.ConnFormWindowSize.Width != 0)
                this.Size = ActiveConfig.Controller.ConnFormWindowSize;
            if (ActiveConfig.Controller.ConnFormWindowLoc.X != 0)
            {
                this.Location = ActiveConfig.Controller.ConnFormWindowLoc;
                Utils.FixupFormPosition(this);
            }
            this.WindowState = ActiveConfig.Controller.ConnFormWindowState;
            this.chkShowActive.Checked = ActiveConfig.Controller.ConnFormShowConnections;
            this.chkShowListen.Checked = ActiveConfig.Controller.ConnFormShowOpenPorts;
            this.chkShowBlocked.Checked = ActiveConfig.Controller.ConnFormShowBlocked;

            foreach (ColumnHeader col in list.Columns)
            {
                if (ActiveConfig.Controller.ConnFormColumnWidths.TryGetValue((string)col.Tag, out int width))
                    col.Width = width;
            }

            await UpdateListAsync();
        }

        private void contextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (list.SelectedIndices.Count < 1)
                e.Cancel = true;

            // Don't allow Kill if we don't have a PID
            bool hasPid = list.SelectedItems.Cast<ListViewItem>().Aggregate(true, (current, li) => current & ((ProcessInfo)li.Tag).Pid != 0);

            mnuCloseProcess.Enabled = hasPid;
        }

        private async void mnuCloseProcess_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem li in list.SelectedItems)
            {
                var pi = (ProcessInfo)li.Tag;

                try
                {
                    using Process proc = Process.GetProcessById(unchecked((int)pi.Pid));
                    try
                    {
                        if (!proc.CloseMainWindow())
                            proc.Kill();

                        using var waitCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        try
                        {
                            await proc.WaitForExitAsync(waitCancellation.Token);
                            await UpdateListAsync();
                        }
                        catch (OperationCanceledException)
                        {
                            throw new ApplicationException();
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // The process has already exited. Fine, that's just what we want :)
                    }
                    catch
                    {
                        MessageBox.Show(this, string.Format(CultureInfo.CurrentCulture, Resources.Messages.CouldNotCloseProcess, proc.ProcessName, pi.Pid), Resources.Messages.TinyWall, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                }
                catch
                {
                    // The app has probably already quit. Leave it at that.
                }
            }
        }

        private void mnuUnblock_Click(object sender, EventArgs e)
        {
            if (!_controller.EnsureUnlockedServer())
                return;

            var selection = (from ListViewItem li in list.SelectedItems select (ProcessInfo)li.Tag).ToList();

            _controller.WhitelistProcesses(selection);
        }

        private void mnuCopyRemoteAddress_Click(object sender, EventArgs e)
        {
            ListViewItem li = list.SelectedItems[0];
            var clipboardData = li.SubItems[6].Text;

            IDataObject dataObject = new DataObject();
            dataObject.SetData(DataFormats.UnicodeText, false, clipboardData);

            try
            {
                Clipboard.SetDataObject(dataObject, true, 20, 100);
            }
            catch
            {
                // Fail silently :(
            }
        }

        private async void mnuVirusTotal_Click(object sender, EventArgs e)
        {
            try
            {
                ListViewItem li = list.SelectedItems[0];

                const string URL_TEMPLATE = @"https://www.virustotal.com/latest-scan/{0}";
                var hash = await Task.Run(() => Hasher.HashFile(((ProcessInfo)li.Tag).Path));
                var url = string.Format(CultureInfo.InvariantCulture, URL_TEMPLATE, hash);
                Utils.StartProcess(url, string.Empty, false);
            }
            catch
            {
                MessageBox.Show(this, Resources.Messages.CannotGetPathOfProcess, Resources.Messages.TinyWall, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

            }
        }

        private void mnuProcessLibrary_Click(object sender, EventArgs e)
        {
            try
            {
                ListViewItem li = list.SelectedItems[0];

                const string URL_TEMPLATE = @"http://www.processlibrary.com/search/?q={0}";
                var filename = System.IO.Path.GetFileName(((ProcessInfo)li.Tag).Path);
                var url = string.Format(CultureInfo.InvariantCulture, URL_TEMPLATE, filename);
                Utils.StartProcess(url, string.Empty, false);
            }
            catch
            {
                MessageBox.Show(this, Resources.Messages.CannotGetPathOfProcess, Resources.Messages.TinyWall, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private void mnuFileNameOnTheWeb_Click(object sender, EventArgs e)
        {
            try
            {
                ListViewItem li = list.SelectedItems[0];

                var urlTemplate = Resources.Messages.SearchEngine;
                var filename = System.IO.Path.GetFileName(((ProcessInfo)li.Tag).Path);
                var url = string.Format(CultureInfo.InvariantCulture, urlTemplate, filename);
                Utils.StartProcess(url, string.Empty, false);
            }
            catch
            {
                MessageBox.Show(this, Resources.Messages.CannotGetPathOfProcess, Resources.Messages.TinyWall, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private void mnuRemoteAddressOnTheWeb_Click(object sender, EventArgs e)
        {
            try
            {
                ListViewItem li = list.SelectedItems[0];

                var urlTemplate = Resources.Messages.SearchEngine;
                var address = li.SubItems[6].Text;
                var url = string.Format(CultureInfo.InvariantCulture, urlTemplate, address);
                Utils.StartProcess(url, string.Empty, false);
            }
            catch
            {
                MessageBox.Show(this, Resources.Messages.CannotGetPathOfProcess, Resources.Messages.TinyWall, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private void ConnectionsForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData != Keys.F5) return;

            btnRefresh_Click(btnRefresh, EventArgs.Empty);
            e.Handled = true;
        }

        private async void btnSearch_Click(object sender, EventArgs e)
        {
            try
            {
                _searchText = txtSearch.Text.ToLower();
                await UpdateListAsync();
            }
            catch
            {
                //throw; // TODO handle exception
            }
        }

        private async void BtnClear_Click(object sender, EventArgs e)
        {
            try
            {
                _searchText = string.Empty;
                await UpdateListAsync();
            }
            catch
            {
                //throw; // TODO handle exception
            }
        }

        private void txtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData is Keys.Enter or Keys.Return)
            {
                btnSearch.PerformClick();
            }
        }
    }
}
