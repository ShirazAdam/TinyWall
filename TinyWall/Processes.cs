﻿using pylorak.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace pylorak.TinyWall
{
    internal partial class ProcessesForm : Form
    {
        internal readonly List<ProcessInfo> Selection = new List<ProcessInfo>();
        private readonly Size _iconSize = new Size((int)Math.Round(16 * Utils.DpiScalingFactor), (int)Math.Round(16 * Utils.DpiScalingFactor));

        internal ProcessesForm(bool multiSelect)
        {
            InitializeComponent();
            Utils.SetRightToLeft(this);
            this.IconList.ImageSize = _iconSize;
            this.listView.MultiSelect = multiSelect;
            this.Icon = Resources.Icons.firewall;
            this.btnOK.Image = GlobalInstances.ApplyBtnIcon;
            this.btnCancel.Image = GlobalInstances.CancelBtnIcon;

            this.IconList.Images.Add("store", Resources.Icons.store);
            this.IconList.Images.Add("system", Resources.Icons.windows_small);
            this.IconList.Images.Add("network-drive", Resources.Icons.network_drive_small);
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < listView.SelectedItems.Count; ++i)
            {
                this.Selection.Add((ProcessInfo)listView.SelectedItems[i].Tag);
            }
            this.DialogResult = DialogResult.OK;
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

        private void ProcessesForm_Load(object sender, EventArgs ev)
        {
            this.Icon = Resources.Icons.firewall;
            if (ActiveConfig.Controller.ProcessesFormWindowSize.Width != 0)
                this.Size = ActiveConfig.Controller.ProcessesFormWindowSize;
            if (ActiveConfig.Controller.ProcessesFormWindowLoc.X != 0)
            {
                this.Location = ActiveConfig.Controller.ProcessesFormWindowLoc;
                Utils.FixupFormPosition(this);
            }
            this.WindowState = ActiveConfig.Controller.ProcessesFormWindowState;

            foreach (ColumnHeader col in listView.Columns)
            {
                if (ActiveConfig.Controller.ProcessesFormColumnWidths.TryGetValue((string)col.Tag, out int width))
                    col.Width = width;
            }

            List<ListViewItem> itemColl = new List<ListViewItem>();
            UwpPackage packages = new UwpPackage();
            ServicePidMap servicePids = new ServicePidMap();

            Process[] procs = Process.GetProcesses();
            foreach (var t in procs)
            {
                using Process p = t;
                try
                {
                    var pid = unchecked((uint)p.Id);
                    var e = ProcessInfo.Create(pid, packages, servicePids);

                    if (string.IsNullOrEmpty(e.Path))
                        continue;

                    // Scan list of already added items to prevent duplicates
                    bool skip = itemColl.Select(t1 => (ProcessInfo)t1.Tag).Any(opi => (e.Package == opi.Package) && (e.Path == opi.Path) && (e.Services.SetEquals(opi.Services)));

                    if (skip)
                        continue;

                    // Add list item
                    ListViewItem li = new ListViewItem(e.Package.HasValue ? e.Package.Value.Name : p.ProcessName);
                    li.SubItems.Add(string.Join(", ", e.Services.ToArray()));
                    li.SubItems.Add(e.Path);
                    li.Tag = e;
                    itemColl.Add(li);

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
                    else if (System.IO.Path.IsPathRooted(e.Path) && System.IO.File.Exists(e.Path))
                    {
                        if (!IconList.Images.ContainsKey(e.Path))
                            IconList.Images.Add(e.Path, Utils.GetIconContained(e.Path, _iconSize.Width, _iconSize.Height));
                        li.ImageKey = e.Path;
                    }
                }
                catch
                {
                    // ignored
                }
            }

            Utils.SetDoubleBuffering(listView, true);
            listView.BeginUpdate();
            listView.ListViewItemSorter = new ListViewItemComparer(0);
            listView.Items.AddRange(itemColl.ToArray());
            listView.EndUpdate();
        }

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
    }
}
