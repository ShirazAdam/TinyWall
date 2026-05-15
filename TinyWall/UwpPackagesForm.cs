using pylorak.Windows;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace pylorak.TinyWall
{
    public partial class UwpPackagesForm : Form
    {
        private readonly List<UwpPackageList.Package> _selectedPackages = new();
        private readonly Size _iconSize = new((int)Math.Round(16 * Utils.DpiScalingFactor), (int)Math.Round(16 * Utils.DpiScalingFactor));

        private string _searchItem = string.Empty;

        public UwpPackagesForm(bool multiSelect)
        {
            InitializeComponent();
            Utils.SetRightToLeft(this);
            listView.MultiSelect = multiSelect;
            Icon = Resources.Icons.firewall;
            btnOK.Image = GlobalInstances.ApplyBtnIcon;
            btnCancel.Image = GlobalInstances.CancelBtnIcon;

            IconList.ImageSize = _iconSize;
            IconList.Images.Add("store", Resources.Icons.store);
        }

        internal static List<UwpPackageList.Package> ChoosePackage(IWin32Window parent, bool multiSelect)
        {
            using var pf = new UwpPackagesForm(multiSelect);
            //var pathList = new List<UwpPackageList.Package>();

            return (pf.ShowDialog(parent) == DialogResult.Cancel) ? new List<UwpPackageList.Package>() : pf._selectedPackages;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            for (var i = 0; i < listView.SelectedItems.Count; ++i)
            {
                _selectedPackages.Add((UwpPackageList.Package)listView.SelectedItems[i].Tag);
            }
            DialogResult = DialogResult.OK;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void listView_DoubleClick(object sender, EventArgs e)
        {
            if (btnOK.Enabled)
            {
                btnOK_Click(btnOK, EventArgs.Empty);
            }
        }

        private async void UwpPackages_Load(object sender, EventArgs e)
        {
            Icon = Resources.Icons.firewall;
            if (ActiveConfig.Controller.UwpPackagesFormWindowSize.Width != 0)
                Size = ActiveConfig.Controller.UwpPackagesFormWindowSize;
            if (ActiveConfig.Controller.UwpPackagesFormWindowLoc.X != 0)
            {
                Location = ActiveConfig.Controller.UwpPackagesFormWindowLoc;
                Utils.FixupFormPosition(this);
            }
            WindowState = ActiveConfig.Controller.UwpPackagesFormWindowState;

            foreach (ColumnHeader col in listView.Columns)
            {
                if (ActiveConfig.Controller.UwpPackagesFormColumnWidths.TryGetValue((string)col.Tag, out var width))
                    col.Width = width;
            }

            await UpdateListAsync();
        }

        private Task UpdateListAsync()
        {
            lblPleaseWait.Visible = true;
            Enabled = false;

            var itemColl = new List<ListViewItem>();

            var packageList = new UwpPackageList();

            List<UwpPackageList.Package> packages;

            if (!string.IsNullOrWhiteSpace(_searchItem))
            {
                packages = [.. packageList.Where(p =>
                    p.Name.ToLower().Contains(_searchItem.ToLower())
                    || p.Publisher.ToLower().Contains(_searchItem.ToLower())
                )];
            }
            else
            {
                packages = [.. packageList];
            }

            foreach (var package in packages)
            {
                // Add list item
                var li = new ListViewItem(package.Name);
                li.SubItems.Add(package.PublisherId + ", " + package.Publisher);
                li.ImageKey = @"store";
                li.Tag = package;
                itemColl.Add(li);
            }

            Utils.SetDoubleBuffering(listView, true);
            listView.BeginUpdate();
            listView.Items.Clear();
            listView.ListViewItemSorter = new ListViewItemComparer(0);

            listView.Items.AddRange([.. itemColl]);
            listView.EndUpdate();

            lblPleaseWait.Visible = false;
            Enabled = true;

            return Task.CompletedTask;
        }

        private void listView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            var oldSorter = (ListViewItemComparer)listView.ListViewItemSorter;
            var newSorter = new ListViewItemComparer(e.Column);
            if (oldSorter != null && oldSorter.Column == newSorter.Column)
                newSorter.Ascending = !oldSorter.Ascending;

            listView.ListViewItemSorter = newSorter;
        }

        private void UwpPackages_FormClosing(object sender, FormClosingEventArgs e)
        {
            ActiveConfig.Controller.UwpPackagesFormWindowState = WindowState;
            if (WindowState == FormWindowState.Normal)
            {
                ActiveConfig.Controller.UwpPackagesFormWindowSize = Size;
                ActiveConfig.Controller.UwpPackagesFormWindowLoc = Location;
            }
            else
            {
                ActiveConfig.Controller.UwpPackagesFormWindowSize = RestoreBounds.Size;
                ActiveConfig.Controller.UwpPackagesFormWindowLoc = RestoreBounds.Location;
            }

            ActiveConfig.Controller.UwpPackagesFormColumnWidths.Clear();
            foreach (ColumnHeader col in listView.Columns)
                ActiveConfig.Controller.UwpPackagesFormColumnWidths.Add((string)col.Tag, col.Width);

            ActiveConfig.Controller.Save();
        }

        private void listView_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnOK.Enabled = listView.SelectedItems.Count > 0;
        }

        private async void btnSearch_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtBxSearch.Text))
                {
                    return;
                }

                _searchItem = txtBxSearch.Text.ToLower();

                await UpdateListAsync();
            }
            catch
            {
                //throw;
            }
        }

        private async void btnClear_Click(object sender, EventArgs e)
        {
            try
            {
                _searchItem = string.Empty;
                txtBxSearch.Text = string.Empty;

                await UpdateListAsync();
            }
            catch
            {
                //throw;
            }
        }

        private void txtBxSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode is Keys.Enter or Keys.Return)
            {
                btnSearch.PerformClick();
            }
        }
    }
}
