using pylorak.TinyWall.Resources;
using pylorak.Windows;
using pylorak.Windows.NetStat;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace pylorak.TinyWall
{
    internal partial class ConnectionsForm : Form
    {
        private readonly TinyWallController _controller;

        private readonly Size _iconSize = new((int)Math.Round(16 * Utils.DpiScalingFactor),
            (int)Math.Round(16 * Utils.DpiScalingFactor));

        private List<ListViewItem> _itemColl = new();
        private string _searchText = string.Empty;

        internal ConnectionsForm(TinyWallController ctrl)
        {
            InitializeComponent();
            Utils.SetRightToLeft(this);
            IconList.ImageSize = _iconSize;
            Icon = Icons.firewall;
            _controller = ctrl;

            IconList.Images.Add("store", Icons.store);
            IconList.Images.Add("system", Icons.windows_small);
            IconList.Images.Add("network-drive", Icons.network_drive_small);
        }

        private void BtnClose_Click(object sender, EventArgs e) => Close();

        private static string GetPathFromPidCached(Dictionary<uint, string> cache, uint pid)
        {
            if (cache.TryGetValue(pid, out var cached))
                return cached;
            var ret = Utils.GetPathOfProcessUseTwService(pid, GlobalInstances.Controller);
            cache.Add(pid, ret);
            return ret;
        }

        private Task UpdateListAsync()
        {
            lblPleaseWait.Visible = true;
            Enabled = false;

            var fwLogRequest = GlobalInstances.Controller.BeginReadFwLog();

            _itemColl = new List<ListViewItem>();
            var packageList = new UwpPackageList();
            var procCache = new Dictionary<uint, string>();
            var servicePids = new ServicePidMap();

            // Retrieve IP tables while waiting for log entries

            var now = DateTime.Now;
            var tcpTable = NetStat.GetExtendedTcp4Table(false);

            foreach (var tcpRow in tcpTable)
            {
                if ((!chkShowListen.Checked || tcpRow.State != TcpState.Listen)
                    && (!chkShowActive.Checked || tcpRow.State == TcpState.Listen)) continue;

                var path = GetPathFromPidCached(procCache, tcpRow.ProcessId);

                var pi = ProcessInfo.Create(tcpRow.ProcessId, path, packageList, servicePids);
                ConstructListItem(_itemColl, pi, "TCP", tcpRow.LocalEndPoint, tcpRow.RemoteEndPoint,
                    tcpRow.State.ToString(), now, RuleDirection.Invalid);
            }

            tcpTable = NetStat.GetExtendedTcp6Table(false);

            foreach (var tcpRow in tcpTable)
            {
                if ((!chkShowListen.Checked || tcpRow.State != TcpState.Listen)
                    && (!chkShowActive.Checked || tcpRow.State == TcpState.Listen)) continue;

                var path = GetPathFromPidCached(procCache, tcpRow.ProcessId);

                var pi = ProcessInfo.Create(tcpRow.ProcessId, path, packageList, servicePids);
                ConstructListItem(_itemColl, pi, "TCP", tcpRow.LocalEndPoint, tcpRow.RemoteEndPoint,
                    tcpRow.State.ToString(), now, RuleDirection.Invalid);
            }

            if (chkShowListen.Checked)
            {
                var dummyEp = new IPEndPoint(0, 0);

                var udpTable = NetStat.GetExtendedUdp4Table(false);

                CreateProcess(udpTable, procCache, packageList, servicePids, dummyEp, now);

                udpTable = NetStat.GetExtendedUdp6Table(false);

                CreateProcess(udpTable, procCache, packageList, servicePids, dummyEp, now);
            }

            // Finished reading tables, continues with log processing
            var fwLog = Controller.EndReadFwLog(fwLogRequest.Response);

            // Show log entries if requested by user
            if (chkShowBlocked.Checked)
            {
                // Try to resolve PIDs heuristically
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
                    if (p.Count == 1 && p[0].CreationTime < e.Timestamp.ToFileTime())
                        e.ProcessId = p[0].ProcessId;
                }

                var filteredLog = new List<FirewallLogEntry>();
                var refSpan = TimeSpan.FromMinutes(5);
                foreach (var newEntry in fwLog)
                {
                    // Ignore log entries older than refSpan
                    var span = now - newEntry.Timestamp;
                    if (span > refSpan)
                        continue;

                    switch (newEntry.Event)
                    {
                        case EventLogEvent.ALLOWED_LISTEN:
                        case EventLogEvent.ALLOWED_CONNECTION:
                        case EventLogEvent.ALLOWED_LOCAL_BIND:
                        case EventLogEvent.ALLOWED:
                            {
                                newEntry.Event = EventLogEvent.ALLOWED;
                                break;
                            }
                        case EventLogEvent.BLOCKED_LISTEN:
                        case EventLogEvent.BLOCKED_CONNECTION:
                        case EventLogEvent.BLOCKED_LOCAL_BIND:
                        case EventLogEvent.BLOCKED_PACKET:
                        case EventLogEvent.BLOCKED:
                            {
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
                            }
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                foreach (var entry in filteredLog)
                {
                    // Correct path capitalisation
                    // TODO: Do this in the service, and minimize overhead. Right now if GetExactPath() fails,
                    // for example due to missing file system privileges, capitalisation will not be corrected.
                    // The service has much more privileges, so doing this in the service would allow more paths
                    // to be corrected.
                    entry.AppPath = Utils.GetExactPath(entry.AppPath);

                    var pi = ProcessInfo.Create(entry.ProcessId, entry.AppPath ?? string.Empty, entry.PackageId,
                        packageList, servicePids);

                    if (entry is { LocalIp: not null, RemoteIp: not null })
                        ConstructListItem(_itemColl, pi, entry.Protocol.ToString(),
                            new IPEndPoint(IPAddress.Parse(entry.LocalIp), entry.LocalPort),
                            new IPEndPoint(IPAddress.Parse(entry.RemoteIp), entry.RemotePort), "Blocked",
                            entry.Timestamp, entry.Direction);
                }
            }

            // Add items to list
            list.BeginUpdate();
            list.Items.Clear();

            if (!string.IsNullOrWhiteSpace(_searchText))
                _itemColl = _itemColl.Where(item => item.SubItems[0].Text.ToLower().Contains(_searchText)).ToList();

            list.Items.AddRange(_itemColl.ToArray());
            list.EndUpdate();

            lblPleaseWait.Visible = false;
            Enabled = true;

            return Task.CompletedTask;
        }

        private void CreateProcess(UdpTable udpTable, Dictionary<uint, string> procCache, UwpPackageList packageList,
            ServicePidMap servicePids, IPEndPoint dummyEp, DateTime now)
        {
            foreach (var udpRow in udpTable)
            {
                var path = GetPathFromPidCached(procCache, udpRow.ProcessId);

                var pi = ProcessInfo.Create(udpRow.ProcessId, path, packageList, servicePids);
                ConstructListItem(_itemColl, pi, "UDP", udpRow.LocalEndPoint, dummyEp, "Listen", now,
                    RuleDirection.Invalid);
            }
        }

        private void ConstructListItem(List<ListViewItem> itemColl, ProcessInfo e, string protocol, IPEndPoint localEp,
            IPEndPoint remoteEp, string state, DateTime ts, RuleDirection dir)
        {
            try
            {
                // Construct list item
                var name = e.Package.HasValue ? e.Package.Value.Name : Path.GetFileName(e.Path);
                var title = e.Pid != 0 ? $"{name} ({e.Pid})" : $"{name}";
                ListViewItem li = new(title)
                {
                    Tag = e,
                    ToolTipText = e.Path
                };

                // Add icon
                if (e.Package.HasValue)
                {
                    li.ImageKey = @"store";
                }
                else if (e.Path == "System")
                {
                    li.ImageKey = @"system";
                }
                else if (NetworkPath.IsNetworkPath(e.Path))
                {
                    li.ImageKey = @"network-drive";
                }
                else if (Path.IsPathRooted(e.Path) && File.Exists(e.Path))
                {
                    if (!IconList.Images.ContainsKey(e.Path))
                        // Get icon
                        IconList.Images.Add(e.Path, Utils.GetIconContained(e.Path, _iconSize.Width, _iconSize.Height));
                    li.ImageKey = e.Path;
                }

                li.SubItems.Add(e.Pid == 0 ? string.Empty : string.Join(", ", e.Services.ToArray()));
                li.SubItems.Add(protocol);
                li.SubItems.Add(localEp.Port.ToString(CultureInfo.InvariantCulture).PadLeft(5));
                li.SubItems.Add(localEp.Address.ToString());
                li.SubItems.Add(remoteEp.Port.ToString(CultureInfo.InvariantCulture).PadLeft(5));
                li.SubItems.Add(remoteEp.Address.ToString());
                li.SubItems.Add(state);

                switch (dir)
                {
                    case RuleDirection.In:
                        li.SubItems.Add(Messages.TrafficIn);
                        break;
                    case RuleDirection.Out:
                        li.SubItems.Add(Messages.TrafficOut);
                        break;
                    case RuleDirection.InOut:
                    case RuleDirection.Invalid:
                    default:
                        li.SubItems.Add(string.Empty);
                        break;
                }

                li.SubItems.Add(ts.ToString("dd/MM/yyyy HH:mm:ss"));
                itemColl.Add(li);
            }
            catch
            {
                // Most probably process ID has become invalid,
                // but we also catch other errors too.
                // Simply do not add item to the list.
            }
        }

        private void List_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            var oldSorter = (ListViewItemComparer)list.ListViewItemSorter;
            var newSorter = new ListViewItemComparer(e.Column);

            if (oldSorter != null && oldSorter.Column == newSorter.Column)
                newSorter.Ascending = !oldSorter.Ascending;

            list.ListViewItemSorter = newSorter;
        }

        private async void BtnRefresh_Click(object sender, EventArgs e)
        {
            try
            {
                await UpdateListAsync();
            }
            catch
            {
                //throw; // TODO handle exception
            }
        }

        private async void ChkShowListen_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                await UpdateListAsync();
            }
            catch
            {
                //throw; // TODO handle exception
            }
        }

        private async void ChkShowBlocked_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                await UpdateListAsync();
            }
            catch
            {
                //throw; // TODO handle exception
            }
        }

        private async void ChkShowActive_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                await UpdateListAsync();
            }
            catch
            {
                //throw; // TODO handle exception
            }
        }

        private void ConnectionsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            ActiveConfig.Controller.ConnFormWindowState = WindowState;
            if (WindowState == FormWindowState.Normal)
            {
                ActiveConfig.Controller.ConnFormWindowSize = Size;
                ActiveConfig.Controller.ConnFormWindowLoc = Location;
            }
            else
            {
                ActiveConfig.Controller.ConnFormWindowSize = RestoreBounds.Size;
                ActiveConfig.Controller.ConnFormWindowLoc = RestoreBounds.Location;
            }

            ActiveConfig.Controller.ConnFormShowConnections = chkShowActive.Checked;
            ActiveConfig.Controller.ConnFormShowOpenPorts = chkShowListen.Checked;
            ActiveConfig.Controller.ConnFormShowBlocked = chkShowBlocked.Checked;

            ActiveConfig.Controller.ConnFormColumnWidths.Clear();

            foreach (ColumnHeader col in list.Columns)
                ActiveConfig.Controller.ConnFormColumnWidths.Add((string)col.Tag, col.Width);

            ActiveConfig.Controller.Save();
        }

        private async void ConnectionsForm_Load(object sender, EventArgs e)
        {
            try
            {
                Utils.SetDoubleBuffering(list, true);
                list.ListViewItemSorter = new ListViewItemComparer(9, null, false);
                if (ActiveConfig.Controller.ConnFormWindowSize.Width != 0)
                    Size = ActiveConfig.Controller.ConnFormWindowSize;
                if (ActiveConfig.Controller.ConnFormWindowLoc.X != 0)
                {
                    Location = ActiveConfig.Controller.ConnFormWindowLoc;
                    Utils.FixupFormPosition(this);
                }

                WindowState = ActiveConfig.Controller.ConnFormWindowState;
                chkShowActive.Checked = ActiveConfig.Controller.ConnFormShowConnections;
                chkShowListen.Checked = ActiveConfig.Controller.ConnFormShowOpenPorts;
                chkShowBlocked.Checked = ActiveConfig.Controller.ConnFormShowBlocked;

                foreach (ColumnHeader col in list.Columns)
                    if (ActiveConfig.Controller.ConnFormColumnWidths.TryGetValue((string)col.Tag, out var width))
                        col.Width = width;

                await UpdateListAsync();
            }
            catch
            {
                //throw; // TODO handle exception
            }
        }

        private void ContextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {
            if (list.SelectedIndices.Count < 1)
                e.Cancel = true;

            // Don't allow Kill if we don't have a PID
            var hasPid = list.SelectedItems.Cast<ListViewItem>()
                .Aggregate(true, (current, li) => current & (((ProcessInfo)li.Tag).Pid != 0));

            mnuCloseProcess.Enabled = hasPid;
        }

        private async void MnuCloseProcess_Click(object sender, EventArgs e)
        {
            try
            {
                foreach (ListViewItem li in list.SelectedItems)
                {
                    var pi = (ProcessInfo)li.Tag;

                    try
                    {
                        using var proc = Process.GetProcessById(unchecked((int)pi.Pid));
                        try
                        {
                            if (!proc.CloseMainWindow())
                                proc.Kill();

                            if (!proc.WaitForExit(5000))
                                throw new ApplicationException();
                            await UpdateListAsync();
                        }
                        catch (InvalidOperationException)
                        {
                            // The process has already exited. Fine, that's just what we want :)
                        }
                        catch
                        {
                            MessageBox.Show(this,
                                string.Format(CultureInfo.CurrentCulture, Messages.CouldNotCloseProcess, proc.ProcessName,
                                    pi.Pid), Messages.TinyWall, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        }
                    }
                    catch
                    {
                        // The app has probably already quit. Leave it at that.
                    }
                }
            }
            catch
            {
                //throw; // TODO handle exception
            }
        }

        private void MnuUnblock_Click(object sender, EventArgs e)
        {
            if (!_controller.EnsureUnlockedServer())
                return;

            var selection = (from ListViewItem li in list.SelectedItems select (ProcessInfo)li.Tag).ToList();

            _controller.WhitelistProcesses(selection);
        }

        private void MnuCopyRemoteAddress_Click(object sender, EventArgs e)
        {
            var li = list.SelectedItems[0];
            var clipboardData = li.SubItems[6].Text;

            var dataObject = new DataObject();
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

        private void MnuVirusTotal_Click(object sender, EventArgs e)
        {
            try
            {
                var li = list.SelectedItems[0];

                const string URL_TEMPLATE = @"https://www.virustotal.com/latest-scan/{0}";
                var hash = Hasher.HashFile(((ProcessInfo)li.Tag).Path);
                var url = string.Format(CultureInfo.InvariantCulture, URL_TEMPLATE, hash);
                Utils.StartProcess(url, string.Empty, false);
            }
            catch
            {
                MessageBox.Show(this, Messages.CannotGetPathOfProcess, Messages.TinyWall, MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
            }
        }

        private void MnuProcessLibrary_Click(object sender, EventArgs e)
        {
            try
            {
                var li = list.SelectedItems[0];

                const string URL_TEMPLATE = @"http://www.processlibrary.com/search/?q={0}";
                var filename = Path.GetFileName(((ProcessInfo)li.Tag).Path);
                var url = string.Format(CultureInfo.InvariantCulture, URL_TEMPLATE, filename);
                Utils.StartProcess(url, string.Empty, false);
            }
            catch
            {
                MessageBox.Show(this, Messages.CannotGetPathOfProcess, Messages.TinyWall, MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
            }
        }

        private void MnuFileNameOnTheWeb_Click(object sender, EventArgs e)
        {
            try
            {
                var li = list.SelectedItems[0];

                var urlTemplate = Messages.SearchEngine;
                var filename = Path.GetFileName(((ProcessInfo)li.Tag).Path);
                var url = string.Format(CultureInfo.InvariantCulture, urlTemplate, filename);
                Utils.StartProcess(url, string.Empty, false);
            }
            catch
            {
                MessageBox.Show(this, Messages.CannotGetPathOfProcess, Messages.TinyWall, MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
            }
        }

        private void MnuRemoteAddressOnTheWeb_Click(object sender, EventArgs e)
        {
            try
            {
                var li = list.SelectedItems[0];

                var urlTemplate = Messages.SearchEngine;
                var address = li.SubItems[6].Text;
                var url = string.Format(CultureInfo.InvariantCulture, urlTemplate, address);
                Utils.StartProcess(url, string.Empty, false);
            }
            catch
            {
                MessageBox.Show(this, Messages.CannotGetPathOfProcess, Messages.TinyWall, MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
            }
        }

        private void ConnectionsForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData != Keys.F5) return;

            BtnRefresh_Click(btnRefresh, EventArgs.Empty);
            e.Handled = true;
        }

        private async void BtnSearch_Click(object sender, EventArgs e)
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

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData is Keys.Return or Keys.Enter) btnSearch.PerformClick();
        }
    }
}