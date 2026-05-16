using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using pylorak.TinyWall.Resources;
using pylorak.Windows;

namespace pylorak.TinyWall
{
    internal partial class SettingsForm : Form
    {
        private readonly List<ListViewItem> _exceptionItems = new();
        private readonly List<ListViewItem> _filteredExceptionItems = new();

        private Size _iconSize = new((int)Math.Round(16 * Utils.DpiScalingFactor),
            (int)Math.Round(16 * Utils.DpiScalingFactor));

        private bool _loadingSettings;

        internal ConfigContainer TmpConfig;

        internal SettingsForm(ServerConfiguration service, ControllerSettings controller)
        {
            InitializeComponent();
            Utils.SetRightToLeft(this);
            IconList.ImageSize = _iconSize;
            Icon = Icons.firewall;
            btnOK.Image = GlobalInstances.ApplyBtnIcon;
            btnCancel.Image = GlobalInstances.CancelBtnIcon;
            btnAppAutoDetect.Image = GlobalInstances.UninstallBtnIcon;
            btnAppAdd.Image = GlobalInstances.AddBtnIcon;
            btnAppModify.Image = GlobalInstances.ModifyBtnIcon;
            btnAppRemove.Image = GlobalInstances.RemoveBtnIcon;
            btnAppRemoveAll.Image = GlobalInstances.RemoveBtnIcon;
            btnSubmitAssoc.Image = GlobalInstances.SubmitBtnIcon;
            btnImport.Image = GlobalInstances.ImportBtnIcon;
            btnExport.Image = GlobalInstances.ExportBtnIcon;
            btnUpdate.Image = GlobalInstances.UpdateBtnIcon;

            listApplications.AllowDrop = true;
            listApplications.DragEnter += ListApplications_DragEnter;
            listApplications.DragDrop += ListApplications_DragDrop;

            TmpConfig = new ConfigContainer(service, controller);
            TmpConfig.Service.ActiveProfile.Normalise();
        }

        internal string? NewPassword { get; private set; }

        private async void ListApplications_DragDrop(object sender, DragEventArgs e)
        {
            List<FirewallExceptionV3> list = new();

            var files = (string[])e.Data.GetData(DataFormats.FileDrop, false);
            foreach (var file in files)
                try
                {
                    list.AddRange(
                        GlobalInstances.AppDatabase!.GetExceptionsForApp(new ExecutableSubject(file), true, out _));
                }
                catch
                {
                    // ignored
                }

            TmpConfig.Service.ActiveProfile.AddExceptions(list);
            await RebuildExceptionsList();
        }

        private void ListApplications_DragEnter(object sender, DragEventArgs e) => e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.All : DragDropEffects.None;

        private async Task InitSettingsUi()
        {
            _loadingSettings = true;
            try
            {
                // General page
                chkAutoUpdateCheck.Checked = TmpConfig.Service.AutoUpdateCheck;
                chkAskForExceptionDetails.Checked = TmpConfig.Controller.AskForExceptionDetails;
                chkEnableHotkeys.Checked = TmpConfig.Controller.EnableGlobalHotkeys;
                comboLanguages.SelectedIndex = 0;

                for (var i = 0; i < comboLanguages.Items.Count; ++i)
                {
                    var item = (IdWithName)comboLanguages.Items[i];

                    if (!item.Id.Equals(TmpConfig.Controller.Language, StringComparison.OrdinalIgnoreCase)) continue;

                    comboLanguages.SelectedIndex = i;
                    break;
                }

                // Fill Machine Settings tab
                chkDisplayOffBlock.Checked = TmpConfig.Service.ActiveProfile.DisplayOffBlock;
                chkLockHostsFile.Checked = TmpConfig.Service.LockHostsFile;
                chkHostsBlocklist.Checked = TmpConfig.Service.Blocklists.EnableHostsBlocklist;
                chkBlockMalwarePorts.Checked = TmpConfig.Service.Blocklists.EnablePortBlocklist;
                chkEnableBlocklists.Checked = TmpConfig.Service.Blocklists.EnableBlocklists;
                ChkEnableBlocklists_CheckedChanged(this, EventArgs.Empty);

                // Fill lists of special exceptions
                listRecommendedGlobalProfiles.BeginUpdate();
                listOptionalGlobalProfiles.BeginUpdate();
                listRecommendedGlobalProfiles.Items.Clear();
                listOptionalGlobalProfiles.Items.Clear();
                foreach (var app in GlobalInstances.AppDatabase!.KnownApplications)
                {
                    if (!app.HasFlag("TWUI:Special") || app.HasFlag("TWUI:Hidden")) continue;

                    // Get localised name
                    IdWithName item = new(app.Name, app.LocalisedName);

                    // Construct default name in case no localization exists
                    if (string.IsNullOrEmpty(item.Name))
                        item.Name = item.Id.Replace('_', ' ');

                    var listBox = app.HasFlag("TWUI:Recommended")
                        ? listRecommendedGlobalProfiles
                        : listOptionalGlobalProfiles;
                    var itemIdx = listBox.Items.Add(item);
                    listBox.SetItemChecked(itemIdx,
                        TmpConfig.Service.ActiveProfile.SpecialExceptions.Contains(item.Id));
                }

                listRecommendedGlobalProfiles.EndUpdate();
                listOptionalGlobalProfiles.EndUpdate();

                // Fill list of applications
                await RebuildExceptionsList();
            }
            finally
            {
                _loadingSettings = false;
            }
        }

        private async Task RebuildExceptionsList()
        {
            var packageList = await Task.Run(() => new UwpPackageList());
            _exceptionItems.Clear();

            foreach (var ex in TmpConfig.Service.ActiveProfile.AppExceptions)
                _exceptionItems.Add(ListItemFromAppException(ex, packageList));

            _exceptionItems.Sort(listApplications.ListViewItemSorter as ListViewItemComparer);

            ApplyExceptionFilter();
        }

        private void ApplyExceptionFilter()
        {
            var filter = txtExceptionListFilter.Text.Trim().ToUpperInvariant();
            _filteredExceptionItems.Clear();

            if (string.IsNullOrEmpty(filter))
                // No filter, add everything
                foreach (var t in _exceptionItems)
                    _filteredExceptionItems.Add(t);
            else
                // Apply filter
                foreach (var t in from t in _exceptionItems let sub0 = t.SubItems[0].Text.ToUpperInvariant() let sub1 = t.SubItems[1].Text.ToUpperInvariant() where sub0.Contains(filter) || sub1.Contains(filter) select t)
                {
                    _filteredExceptionItems.Add(t);
                }

            // Update visible list
            listApplications.VirtualListSize = _filteredExceptionItems.Count;
            listApplications.Refresh();

            // Update buttons
            ListApplications_SelectedIndexChanged(listApplications, EventArgs.Empty);
        }

        private ListViewItem ListItemFromAppException(FirewallExceptionV3 ex, UwpPackageList packageList)
        {
            var li = new ListViewItem { Tag = ex };

            var exeSubj = ex.Subject as ExecutableSubject;
            var srvSubj = ex.Subject as ServiceSubject;
            var uwpSubj = ex.Subject as AppContainerSubject;

            switch (ex.Subject.SubjectType)
            {
                case SubjectType.Executable:
                    li.Text = exeSubj!.ExecutableName;
                    li.SubItems.Add(Messages.SubjectTypeExecutable);
                    li.SubItems.Add(exeSubj.ExecutablePath);
                    break;
                case SubjectType.Service:
                    li.Text = srvSubj!.ServiceName;
                    li.SubItems.Add(Messages.SubjectTypeService);
                    li.SubItems.Add(srvSubj.ExecutablePath);
                    break;
                case SubjectType.Global:
                    li.Text = Messages.AllApplications;
                    li.SubItems.Add(Messages.SubjectTypeGlobal);
                    li.SubItems.Add(string.Empty);
                    li.ImageIndex = IconList.Images.IndexOfKey("window");
                    break;
                case SubjectType.AppContainer:
                    li.Text = uwpSubj!.DisplayName;
                    li.SubItems.Add(Messages.SubjectTypeUwpApp);
                    li.SubItems.Add(uwpSubj.PublisherId + ", " + uwpSubj.Publisher);
                    li.ImageIndex = IconList.Images.IndexOfKey("store");
                    break;
                case SubjectType.Invalid:
                default:
                    throw new NotImplementedException();
            }

            li.SubItems.Add(ex.CreationDate.ToString("yyyy/MM/dd HH:mm"));

            if (ex.Policy.PolicyType == PolicyType.HardBlock) li.BackColor = Color.LightPink;

            if (uwpSubj is not null)
                if (!packageList.FindPackage(uwpSubj.Sid).HasValue)
                {
                    li.ImageIndex = IconList.Images.IndexOfKey("deleted");
                    li.BackColor = Color.LightGray;
                }

            if (exeSubj is null) return li;

            if (NetworkPath.IsNetworkPath(exeSubj.ExecutablePath))
            {
                /* We do not load icons from network drives, to avoid 30s timeout if the drive is unavailable.
                     * If this is ever changed in the future, also remember that .Net's Icon.ExtractAssociatedIcon()
                     * does not work with UNC paths. For workaround see:
                     * http://stackoverflow.com/questions/1842226/how-to-get-the-associated-icon-from-a-network-share-file
                     */
                li.ImageIndex = IconList.Images.IndexOfKey("network-drive");
            }
            else if (File.Exists(exeSubj.ExecutablePath))
            {
                if (!IconList.Images.ContainsKey(exeSubj.ExecutablePath))
                    IconList.Images.Add(exeSubj.ExecutablePath,
                        Utils.GetIconContained(exeSubj.ExecutablePath, _iconSize.Width, _iconSize.Height));
                li.ImageIndex = IconList.Images.IndexOfKey(exeSubj.ExecutablePath);
            }
            else if (exeSubj.ExecutablePath == "System")
            {
                li.ImageIndex = IconList.Images.IndexOfKey("system");
            }
            else
            {
                li.ImageIndex = IconList.Images.IndexOfKey("deleted");
                li.BackColor = Color.LightGray;
            }

            return li;
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            // Check password input
            if (chkChangePassword.Checked)
                if (txtPassword.Text != txtPasswordAgain.Text)
                {
                    MessageBox.Show(this, Messages.PasswordFieldsDoNotMatch, Messages.TinyWall, MessageBoxButtons.OK,
                        MessageBoxIcon.Exclamation);
                    return;
                }

            // Set password
            NewPassword = chkChangePassword.Checked ? txtPassword.Text : null;

            // Save settings
            TmpConfig.Controller.AskForExceptionDetails = chkAskForExceptionDetails.Checked;
            TmpConfig.Controller.EnableGlobalHotkeys = chkEnableHotkeys.Checked;
            TmpConfig.Service.AutoUpdateCheck = chkAutoUpdateCheck.Checked;
            TmpConfig.Controller.SettingsTabIndex = tabControl1.SelectedIndex;
            TmpConfig.Service.LockHostsFile = chkLockHostsFile.Checked;
            TmpConfig.Service.Blocklists.EnablePortBlocklist = chkBlockMalwarePorts.Checked;
            TmpConfig.Service.Blocklists.EnableHostsBlocklist = chkHostsBlocklist.Checked;
            TmpConfig.Service.Blocklists.EnableBlocklists = chkEnableBlocklists.Checked;
            TmpConfig.Service.ActiveProfile.DisplayOffBlock = chkDisplayOffBlock.Checked;

            TmpConfig.Controller.Language = ((IdWithName)comboLanguages.SelectedItem).Id;

            DialogResult = DialogResult.OK;
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void ListRecommendedGlobalProfiles_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (_loadingSettings) return;

            var clb = (CheckedListBox)sender;
            var item = (IdWithName)clb.Items[e.Index];

            if (e.NewValue == CheckState.Checked)
                TmpConfig.Service.ActiveProfile.SpecialExceptions.Add(item.Id);
            else
                TmpConfig.Service.ActiveProfile.SpecialExceptions.Remove(item.Id);
        }

        private void ListOptionalGlobalProfiles_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            // The code is exactly the same as for listRecommendedGlobalProfiles
            ListRecommendedGlobalProfiles_ItemCheck(sender, e);
        }

        private async void BtnAppRemove_Click(object sender, EventArgs e)
        {
            for (var i = listApplications.SelectedIndices.Count - 1; i >= 0; --i)
            {
                var li = _filteredExceptionItems[listApplications.SelectedIndices[i]];
                TmpConfig.Service.ActiveProfile.AppExceptions.Remove((FirewallExceptionV3)li.Tag);
            }

            listApplications.SelectedIndices.Clear();
            await RebuildExceptionsList();
        }

        private async void BtnAppRemoveAll_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this, Messages.AreYouSureYouWantToRemoveAllExceptions, Messages.TinyWall,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.No)
                return;

            TmpConfig.Service.ActiveProfile.AppExceptions.Clear();
            await RebuildExceptionsList();
        }

        private async void BtnAppModify_Click(object sender, EventArgs e)
        {
            var li = _filteredExceptionItems[listApplications.SelectedIndices[0]];
            var oldEx = (FirewallExceptionV3)li.Tag;
            var newEx = Utils.DeepClone(oldEx);
            newEx.RegenerateId();

            using (var f = new ApplicationExceptionForm(newEx))
            {
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    // Remove old rule
                    TmpConfig.Service.ActiveProfile.AppExceptions.Remove(oldEx);
                    // Add new rule
                    TmpConfig.Service.ActiveProfile.AddExceptions(f.ExceptionSettings);
                    await RebuildExceptionsList();
                }
            }

            listApplications.Focus();
        }

        private async void BtnAppAdd_Click(object sender, EventArgs e)
        {
            //using var f = new ApplicationExceptionForm(FirewallExceptionV3.Default);
            using var f = new ApplicationExceptionForm();

            if (f.ShowDialog(this) != DialogResult.OK) return;

            TmpConfig.Service.ActiveProfile.AddExceptions(f.ExceptionSettings);
            await RebuildExceptionsList();
        }

        private void ChkEnablePassword_CheckedChanged(object sender, EventArgs e)
        {
            txtPassword.Enabled = txtPasswordAgain.Enabled = chkChangePassword.Checked;
        }

        private void BtnSubmitAssoc_Click(object sender, EventArgs e)
        {
            /* Not implemented */
        }

        private void SettingsForm_Shown(object sender, EventArgs e)
        {
            BringToFront();
            Activate();
        }

        private void ListApplications_DoubleClick(object sender, EventArgs e)
        {
            if (listApplications.SelectedIndices.Count == 0)
                return;

            BtnAppModify_Click(this, EventArgs.Empty);
        }

        private async void BtnAppAutoDetect_Click(object sender, EventArgs e)
        {
            try
            {
                using var aff = new AppFinderForm();

                if (aff.ShowDialog(this) != DialogResult.OK) return;

                TmpConfig.Service.ActiveProfile.AddExceptions(aff.SelectedExceptions);
                await RebuildExceptionsList();
            }
            catch
            {
                // ignored
            }
        }

        private void LblLinkLicense_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                var psi = new ProcessStartInfo(Path.Combine(
                    Path.GetDirectoryName(Utils.ExecutablePath) ?? throw new InvalidOperationException(),
                    "Licence.rtf"))
                {
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch
            {
                // ignored
            }
        }

        private async void BtnImport_Click(object sender, EventArgs e)
        {
            try
            {
                ofd.Filter = string.Format(CultureInfo.CurrentCulture, @"{0} (*.tws)|*.tws|{1} (*)|*",
                    Messages.TinyWallSettingsFileFilter, Messages.AllFilesFileFilter);

                if (ofd.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    TmpConfig = SerialisationHelper.DeserialiseFromFile(ofd.FileName, new ConfigContainer(), true);
                }
                catch
                {
                    // Fail import.
                    MessageBox.Show(this, Messages.ConfigurationImportError, Messages.TinyWall, MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                await InitSettingsUi();
                MessageBox.Show(this, Messages.ConfigurationHasBeenImported, Messages.TinyWall, MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch
            {
                // ignored
            }
        }

        private async void BtnExport_Click(object sender, EventArgs e)
        {
            try
            {
                ofd.Filter = string.Format(CultureInfo.CurrentCulture, @"{0} (*.tws)|*.tws|{1} (*)|*",
                    Messages.TinyWallSettingsFileFilter, Messages.AllFilesFileFilter);
                sfd.DefaultExt = "tws";

                if (sfd.ShowDialog(this) != DialogResult.OK) return;

                await Task.Run(() => SerialisationHelper.SerialiseToFile(TmpConfig, sfd.FileName));

                MessageBox.Show(this, Messages.ConfigurationHasBeenExported, Messages.TinyWallSettingsFileFilter,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch
            {
                // ignored
            }
        }

        private async void SettingsForm_Load(object sender, EventArgs e)
        {
            try
            {
                if (TmpConfig.Controller.SettingsFormWindowSize.Width != 0)
                    Size = TmpConfig.Controller.SettingsFormWindowSize;
                if (TmpConfig.Controller.SettingsFormWindowLoc.X != 0)
                {
                    Location = TmpConfig.Controller.SettingsFormWindowLoc;
                    Utils.FixupFormPosition(this);
                }

                foreach (ColumnHeader col in listApplications.Columns)
                    if (ActiveConfig.Controller.SettingsFormAppListColumnWidths.TryGetValue((string)col.Tag, out var width))
                        col.Width = width;

                Utils.SetDoubleBuffering(listApplications, true);
                listApplications.ListViewItemSorter = new ListViewItemComparer(0, IconList);
                tabControl1.SelectedIndex = TmpConfig.Controller.SettingsTabIndex;

                comboLanguages.Items.Add(new IdWithName("auto", "Automatic"));
                comboLanguages.Items.Add(new IdWithName("bg", "български"));
                comboLanguages.Items.Add(new IdWithName("cs", "Čeština"));
                comboLanguages.Items.Add(new IdWithName("de", "Deutsch"));
                comboLanguages.Items.Add(new IdWithName("en", "English"));
                comboLanguages.Items.Add(new IdWithName("es", "Español"));
                comboLanguages.Items.Add(new IdWithName("fr", "Français"));
                comboLanguages.Items.Add(new IdWithName("it", "Italiano"));
                comboLanguages.Items.Add(new IdWithName("hu", "Magyar"));
                comboLanguages.Items.Add(new IdWithName("nl", "Nederlands"));
                comboLanguages.Items.Add(new IdWithName("pl", "Polski"));
                comboLanguages.Items.Add(new IdWithName("pt-BR", "Português Brasileiro"));
                comboLanguages.Items.Add(new IdWithName("ru", "Русский"));
                comboLanguages.Items.Add(new IdWithName("tr", "Türkçe"));
                comboLanguages.Items.Add(new IdWithName("ja", "日本語"));
                comboLanguages.Items.Add(new IdWithName("ko", "한국어"));
                comboLanguages.Items.Add(new IdWithName("zh", "汉语"));

                IconList.Images.Add("deleted", Icons.delete);
                IconList.Images.Add("network-drive", Icons.network_drive_small);
                IconList.Images.Add("window", Icons.window);
                IconList.Images.Add("store", Icons.store);
                IconList.Images.Add("system", Icons.windows_small);

                lblVersion.Text = string.Format(CultureInfo.CurrentCulture, @"{0} {1}", lblVersion.Text,
                    Application.ProductVersion);

                await InitSettingsUi();

#if !DEBUG
                // TODO: Make submissions work
                btnSubmitAssoc.Visible = false;
#endif
            }
            catch
            {
                // ignored
            }
        }

        private void TxtExceptionListFilter_TextChanged(object sender, EventArgs e) => ApplyExceptionFilter();

        private async void ListApplications_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            try
            {
                var oldSorter = (ListViewItemComparer)listApplications.ListViewItemSorter;
                ListViewItemComparer newSorter = new(e.Column, IconList);
                if (oldSorter != null && oldSorter.Column == newSorter.Column)
                    newSorter.Ascending = !oldSorter.Ascending;

                listApplications.ListViewItemSorter = newSorter;
                await RebuildExceptionsList();
            }
            catch
            {
                // ignored
            }
        }

        private void ChkEnableBlocklists_CheckedChanged(object sender, EventArgs e)
        {
            chkHostsBlocklist.Enabled = chkEnableBlocklists.Checked;
            chkBlockMalwarePorts.Enabled = chkEnableBlocklists.Checked;
        }

        private void LblLinkAttributions_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                var psi = new ProcessStartInfo(Path.Combine(
                    Path.GetDirectoryName(Utils.ExecutablePath) ?? throw new InvalidOperationException(),
                    "Attributions.txt")) { UseShellExecute = true };
                Process.Start(psi);
            }
            catch
            {
                // ignored
            }
        }

        private void SettingsForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (!listApplications.Focused || e.KeyCode != Keys.Delete) return;

            BtnAppRemove_Click(btnAppRemove, EventArgs.Empty);
            e.Handled = true;
        }

        private void SettingsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            TmpConfig.Controller.SettingsFormWindowSize = Size;
            TmpConfig.Controller.SettingsFormWindowLoc = Location;
            ActiveConfig.Controller.SettingsFormWindowSize = Size;
            ActiveConfig.Controller.SettingsFormWindowLoc = Location;

            TmpConfig.Controller.SettingsFormAppListColumnWidths.Clear();
            ActiveConfig.Controller.SettingsFormAppListColumnWidths.Clear();
            foreach (ColumnHeader col in listApplications.Columns)
            {
                TmpConfig.Controller.SettingsFormAppListColumnWidths.Add((string)col.Tag, col.Width);
                ActiveConfig.Controller.SettingsFormAppListColumnWidths.Add((string)col.Tag, col.Width);
            }

            ActiveConfig.Controller.Save();
        }

        private void ListApplications_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e) => e.Item = _filteredExceptionItems[e.ItemIndex];

        private void ListApplications_VirtualItemsSelectionRangeChanged(object sender,
            ListViewVirtualItemsSelectionRangeChangedEventArgs e) =>
            ListApplications_SelectedIndexChanged(sender, EventArgs.Empty);

        private void ListApplications_SelectedIndexChanged(object sender, EventArgs e)
        {
            var anyItemSelected = listApplications.SelectedIndices.Count != 0;
            var singleItemSelected = listApplications.SelectedIndices.Count == 1;
            btnAppModify.Enabled = singleItemSelected;
            btnAppRemove.Enabled = anyItemSelected;
            btnSubmitAssoc.Enabled = anyItemSelected;
        }

        private void BtnGithub_Click(object sender, EventArgs e)
        {
            var psi = new ProcessStartInfo(@"https://github.com/ShirazAdam/tinywall") { UseShellExecute = true };
            Process.Start(psi);
        }

        private class IdWithName
        {
            internal readonly string Id;
            internal string Name;

            internal IdWithName(string id, string name)
            {
                Id = id;
                Name = name;
            }

            public override string ToString() => Name;
        }
    }
}