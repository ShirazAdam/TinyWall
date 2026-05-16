namespace pylorak.TinyWall
{
    partial class SettingsForm
    {
        /// <summary>
        /// Erforderliche Designervariable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Verwendete Ressourcen bereinigen.
        /// </summary>
        /// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Vom Windows Form-Designer generierter Code

        /// <summary>
        /// Erforderliche Methode für die Designerunterstützung.
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SettingsForm));
            btnCancel = new System.Windows.Forms.Button();
            btnOK = new System.Windows.Forms.Button();
            tabPage3 = new System.Windows.Forms.TabPage();
            txtExceptionListFilter = new System.Windows.Forms.TextBox();
            btnAppRemoveAll = new System.Windows.Forms.Button();
            btnAppAutoDetect = new System.Windows.Forms.Button();
            btnSubmitAssoc = new System.Windows.Forms.Button();
            label3 = new System.Windows.Forms.Label();
            btnAppRemove = new System.Windows.Forms.Button();
            btnAppModify = new System.Windows.Forms.Button();
            btnAppAdd = new System.Windows.Forms.Button();
            listApplications = new System.Windows.Forms.ListView();
            columnApp = new System.Windows.Forms.ColumnHeader();
            columnType = new System.Windows.Forms.ColumnHeader();
            columnDetails = new System.Windows.Forms.ColumnHeader();
            columnLastModified = new System.Windows.Forms.ColumnHeader();
            IconList = new System.Windows.Forms.ImageList(components);
            label4 = new System.Windows.Forms.Label();
            tabPage2 = new System.Windows.Forms.TabPage();
            label5 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            label1 = new System.Windows.Forms.Label();
            listOptionalGlobalProfiles = new System.Windows.Forms.CheckedListBox();
            listRecommendedGlobalProfiles = new System.Windows.Forms.CheckedListBox();
            tabPage1 = new System.Windows.Forms.TabPage();
            tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            label11 = new System.Windows.Forms.Label();
            chkHostsBlocklist = new System.Windows.Forms.CheckBox();
            chkEnableBlocklists = new System.Windows.Forms.CheckBox();
            chkBlockMalwarePorts = new System.Windows.Forms.CheckBox();
            chkDisplayOffBlock = new System.Windows.Forms.CheckBox();
            chkLockHostsFile = new System.Windows.Forms.CheckBox();
            comboLanguages = new System.Windows.Forms.ComboBox();
            chkEnableHotkeys = new System.Windows.Forms.CheckBox();
            chkAutoUpdateCheck = new System.Windows.Forms.CheckBox();
            chkAskForExceptionDetails = new System.Windows.Forms.CheckBox();
            groupBox1 = new System.Windows.Forms.GroupBox();
            label9 = new System.Windows.Forms.Label();
            label8 = new System.Windows.Forms.Label();
            label7 = new System.Windows.Forms.Label();
            chkChangePassword = new System.Windows.Forms.CheckBox();
            txtPasswordAgain = new System.Windows.Forms.TextBox();
            txtPassword = new System.Windows.Forms.TextBox();
            tabControl1 = new System.Windows.Forms.TabControl();
            tabPage4 = new System.Windows.Forms.TabPage();
            btnGithub = new System.Windows.Forms.Button();
            btnImport = new System.Windows.Forms.Button();
            btnExport = new System.Windows.Forms.Button();
            groupBox2 = new System.Windows.Forms.GroupBox();
            lblLinkAttributions = new System.Windows.Forms.LinkLabel();
            lblLinkLicense = new System.Windows.Forms.LinkLabel();
            lblVersion = new System.Windows.Forms.Label();
            btnUpdate = new System.Windows.Forms.Button();
            sfd = new System.Windows.Forms.SaveFileDialog();
            ofd = new System.Windows.Forms.OpenFileDialog();
            tabPage3.SuspendLayout();
            tabPage2.SuspendLayout();
            tabPage1.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            groupBox1.SuspendLayout();
            tabControl1.SuspendLayout();
            tabPage4.SuspendLayout();
            groupBox2.SuspendLayout();
            SuspendLayout();
            // 
            // btnCancel
            // 
            resources.ApplyResources(btnCancel, "btnCancel");
            btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            btnCancel.Name = "btnCancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;
            // 
            // btnOK
            // 
            resources.ApplyResources(btnOK, "btnOK");
            btnOK.Name = "btnOK";
            btnOK.UseVisualStyleBackColor = true;
            btnOK.Click += btnOK_Click;
            // 
            // tabPage3
            // 
            tabPage3.Controls.Add(txtExceptionListFilter);
            tabPage3.Controls.Add(btnAppRemoveAll);
            tabPage3.Controls.Add(btnAppAutoDetect);
            tabPage3.Controls.Add(btnSubmitAssoc);
            tabPage3.Controls.Add(label3);
            tabPage3.Controls.Add(btnAppRemove);
            tabPage3.Controls.Add(btnAppModify);
            tabPage3.Controls.Add(btnAppAdd);
            tabPage3.Controls.Add(listApplications);
            tabPage3.Controls.Add(label4);
            resources.ApplyResources(tabPage3, "tabPage3");
            tabPage3.Name = "tabPage3";
            tabPage3.UseVisualStyleBackColor = true;
            // 
            // txtExceptionListFilter
            // 
            resources.ApplyResources(txtExceptionListFilter, "txtExceptionListFilter");
            txtExceptionListFilter.Name = "txtExceptionListFilter";
            txtExceptionListFilter.TextChanged += txtExceptionListFilter_TextChanged;
            // 
            // btnAppRemoveAll
            // 
            resources.ApplyResources(btnAppRemoveAll, "btnAppRemoveAll");
            btnAppRemoveAll.Name = "btnAppRemoveAll";
            btnAppRemoveAll.UseVisualStyleBackColor = true;
            btnAppRemoveAll.Click += btnAppRemoveAll_Click;
            // 
            // btnAppAutoDetect
            // 
            resources.ApplyResources(btnAppAutoDetect, "btnAppAutoDetect");
            btnAppAutoDetect.Name = "btnAppAutoDetect";
            btnAppAutoDetect.UseVisualStyleBackColor = true;
            btnAppAutoDetect.Click += btnAppAutoDetect_Click;
            // 
            // btnSubmitAssoc
            // 
            resources.ApplyResources(btnSubmitAssoc, "btnSubmitAssoc");
            btnSubmitAssoc.Name = "btnSubmitAssoc";
            btnSubmitAssoc.UseVisualStyleBackColor = true;
            btnSubmitAssoc.Click += btnSubmitAssoc_Click;
            // 
            // label3
            // 
            resources.ApplyResources(label3, "label3");
            label3.Name = "label3";
            // 
            // btnAppRemove
            // 
            resources.ApplyResources(btnAppRemove, "btnAppRemove");
            btnAppRemove.Name = "btnAppRemove";
            btnAppRemove.UseVisualStyleBackColor = true;
            btnAppRemove.Click += btnAppRemove_Click;
            // 
            // btnAppModify
            // 
            resources.ApplyResources(btnAppModify, "btnAppModify");
            btnAppModify.Name = "btnAppModify";
            btnAppModify.UseVisualStyleBackColor = true;
            btnAppModify.Click += btnAppModify_Click;
            // 
            // btnAppAdd
            // 
            resources.ApplyResources(btnAppAdd, "btnAppAdd");
            btnAppAdd.Name = "btnAppAdd";
            btnAppAdd.UseVisualStyleBackColor = true;
            btnAppAdd.Click += btnAppAdd_Click;
            // 
            // listApplications
            // 
            resources.ApplyResources(listApplications, "listApplications");
            listApplications.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { columnApp, columnType, columnDetails, columnLastModified });
            listApplications.FullRowSelect = true;
            listApplications.GridLines = true;
            listApplications.Name = "listApplications";
            listApplications.SmallImageList = IconList;
            listApplications.UseCompatibleStateImageBehavior = false;
            listApplications.View = System.Windows.Forms.View.Details;
            listApplications.VirtualMode = true;
            listApplications.ColumnClick += listApplications_ColumnClick;
            listApplications.RetrieveVirtualItem += listApplications_RetrieveVirtualItem;
            listApplications.SelectedIndexChanged += listApplications_SelectedIndexChanged;
            listApplications.VirtualItemsSelectionRangeChanged += listApplications_VirtualItemsSelectionRangeChanged;
            listApplications.DoubleClick += listApplications_DoubleClick;
            // 
            // columnApp
            // 
            columnApp.Tag = "colApplication";
            resources.ApplyResources(columnApp, "columnApp");
            // 
            // columnType
            // 
            columnType.Tag = "colType";
            resources.ApplyResources(columnType, "columnType");
            // 
            // columnDetails
            // 
            columnDetails.Tag = "colDetails";
            resources.ApplyResources(columnDetails, "columnDetails");
            // 
            // columnLastModified
            // 
            columnLastModified.Tag = "colLastModified";
            // 
            // IconList
            // 
            IconList.ColorDepth = System.Windows.Forms.ColorDepth.Depth8Bit;
            resources.ApplyResources(IconList, "IconList");
            IconList.TransparentColor = System.Drawing.Color.Transparent;
            // 
            // label4
            // 
            resources.ApplyResources(label4, "label4");
            label4.Name = "label4";
            // 
            // tabPage2
            // 
            tabPage2.Controls.Add(label5);
            tabPage2.Controls.Add(label2);
            tabPage2.Controls.Add(label1);
            tabPage2.Controls.Add(listOptionalGlobalProfiles);
            tabPage2.Controls.Add(listRecommendedGlobalProfiles);
            resources.ApplyResources(tabPage2, "tabPage2");
            tabPage2.Name = "tabPage2";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // label5
            // 
            resources.ApplyResources(label5, "label5");
            label5.Name = "label5";
            // 
            // label2
            // 
            resources.ApplyResources(label2, "label2");
            label2.Name = "label2";
            // 
            // label1
            // 
            resources.ApplyResources(label1, "label1");
            label1.Name = "label1";
            // 
            // listOptionalGlobalProfiles
            // 
            resources.ApplyResources(listOptionalGlobalProfiles, "listOptionalGlobalProfiles");
            listOptionalGlobalProfiles.CheckOnClick = true;
            listOptionalGlobalProfiles.FormattingEnabled = true;
            listOptionalGlobalProfiles.Name = "listOptionalGlobalProfiles";
            listOptionalGlobalProfiles.Sorted = true;
            listOptionalGlobalProfiles.ItemCheck += listOptionalGlobalProfiles_ItemCheck;
            // 
            // listRecommendedGlobalProfiles
            // 
            resources.ApplyResources(listRecommendedGlobalProfiles, "listRecommendedGlobalProfiles");
            listRecommendedGlobalProfiles.CheckOnClick = true;
            listRecommendedGlobalProfiles.FormattingEnabled = true;
            listRecommendedGlobalProfiles.Name = "listRecommendedGlobalProfiles";
            listRecommendedGlobalProfiles.Sorted = true;
            listRecommendedGlobalProfiles.ItemCheck += listRecommendedGlobalProfiles_ItemCheck;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(tableLayoutPanel1);
            tabPage1.Controls.Add(groupBox1);
            resources.ApplyResources(tabPage1, "tabPage1");
            tabPage1.Name = "tabPage1";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(tableLayoutPanel1, "tableLayoutPanel1");
            tableLayoutPanel1.Controls.Add(label11, 0, 0);
            tableLayoutPanel1.Controls.Add(chkHostsBlocklist, 4, 3);
            tableLayoutPanel1.Controls.Add(chkEnableBlocklists, 3, 1);
            tableLayoutPanel1.Controls.Add(chkBlockMalwarePorts, 4, 2);
            tableLayoutPanel1.Controls.Add(chkDisplayOffBlock, 0, 4);
            tableLayoutPanel1.Controls.Add(chkLockHostsFile, 3, 0);
            tableLayoutPanel1.Controls.Add(comboLanguages, 1, 0);
            tableLayoutPanel1.Controls.Add(chkEnableHotkeys, 0, 3);
            tableLayoutPanel1.Controls.Add(chkAutoUpdateCheck, 0, 1);
            tableLayoutPanel1.Controls.Add(chkAskForExceptionDetails, 0, 2);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // label11
            // 
            resources.ApplyResources(label11, "label11");
            label11.Name = "label11";
            // 
            // chkHostsBlocklist
            // 
            resources.ApplyResources(chkHostsBlocklist, "chkHostsBlocklist");
            chkHostsBlocklist.Name = "chkHostsBlocklist";
            chkHostsBlocklist.UseVisualStyleBackColor = true;
            // 
            // chkEnableBlocklists
            // 
            resources.ApplyResources(chkEnableBlocklists, "chkEnableBlocklists");
            tableLayoutPanel1.SetColumnSpan(chkEnableBlocklists, 2);
            chkEnableBlocklists.Name = "chkEnableBlocklists";
            chkEnableBlocklists.UseVisualStyleBackColor = true;
            chkEnableBlocklists.CheckedChanged += chkEnableBlocklists_CheckedChanged;
            // 
            // chkBlockMalwarePorts
            // 
            resources.ApplyResources(chkBlockMalwarePorts, "chkBlockMalwarePorts");
            chkBlockMalwarePorts.Name = "chkBlockMalwarePorts";
            chkBlockMalwarePorts.UseVisualStyleBackColor = true;
            // 
            // chkDisplayOffBlock
            // 
            resources.ApplyResources(chkDisplayOffBlock, "chkDisplayOffBlock");
            tableLayoutPanel1.SetColumnSpan(chkDisplayOffBlock, 2);
            chkDisplayOffBlock.Name = "chkDisplayOffBlock";
            chkDisplayOffBlock.UseVisualStyleBackColor = true;
            // 
            // chkLockHostsFile
            // 
            resources.ApplyResources(chkLockHostsFile, "chkLockHostsFile");
            tableLayoutPanel1.SetColumnSpan(chkLockHostsFile, 2);
            chkLockHostsFile.Name = "chkLockHostsFile";
            chkLockHostsFile.UseVisualStyleBackColor = true;
            // 
            // comboLanguages
            // 
            resources.ApplyResources(comboLanguages, "comboLanguages");
            comboLanguages.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboLanguages.FormattingEnabled = true;
            comboLanguages.Name = "comboLanguages";
            // 
            // chkEnableHotkeys
            // 
            resources.ApplyResources(chkEnableHotkeys, "chkEnableHotkeys");
            tableLayoutPanel1.SetColumnSpan(chkEnableHotkeys, 2);
            chkEnableHotkeys.Name = "chkEnableHotkeys";
            chkEnableHotkeys.UseVisualStyleBackColor = true;
            // 
            // chkAutoUpdateCheck
            // 
            resources.ApplyResources(chkAutoUpdateCheck, "chkAutoUpdateCheck");
            tableLayoutPanel1.SetColumnSpan(chkAutoUpdateCheck, 2);
            chkAutoUpdateCheck.Name = "chkAutoUpdateCheck";
            chkAutoUpdateCheck.UseVisualStyleBackColor = true;
            // 
            // chkAskForExceptionDetails
            // 
            resources.ApplyResources(chkAskForExceptionDetails, "chkAskForExceptionDetails");
            tableLayoutPanel1.SetColumnSpan(chkAskForExceptionDetails, 2);
            chkAskForExceptionDetails.Name = "chkAskForExceptionDetails";
            chkAskForExceptionDetails.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(label9);
            groupBox1.Controls.Add(label8);
            groupBox1.Controls.Add(label7);
            groupBox1.Controls.Add(chkChangePassword);
            groupBox1.Controls.Add(txtPasswordAgain);
            groupBox1.Controls.Add(txtPassword);
            resources.ApplyResources(groupBox1, "groupBox1");
            groupBox1.Name = "groupBox1";
            groupBox1.TabStop = false;
            // 
            // label9
            // 
            resources.ApplyResources(label9, "label9");
            label9.Name = "label9";
            // 
            // label8
            // 
            resources.ApplyResources(label8, "label8");
            label8.Name = "label8";
            // 
            // label7
            // 
            resources.ApplyResources(label7, "label7");
            label7.Name = "label7";
            // 
            // chkChangePassword
            // 
            resources.ApplyResources(chkChangePassword, "chkChangePassword");
            chkChangePassword.Name = "chkChangePassword";
            chkChangePassword.UseVisualStyleBackColor = true;
            chkChangePassword.CheckedChanged += chkEnablePassword_CheckedChanged;
            // 
            // txtPasswordAgain
            // 
            resources.ApplyResources(txtPasswordAgain, "txtPasswordAgain");
            txtPasswordAgain.Name = "txtPasswordAgain";
            txtPasswordAgain.UseSystemPasswordChar = true;
            // 
            // txtPassword
            // 
            resources.ApplyResources(txtPassword, "txtPassword");
            txtPassword.Name = "txtPassword";
            txtPassword.UseSystemPasswordChar = true;
            // 
            // tabControl1
            // 
            resources.ApplyResources(tabControl1, "tabControl1");
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabPage3);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Controls.Add(tabPage4);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            // 
            // tabPage4
            // 
            tabPage4.Controls.Add(btnGithub);
            tabPage4.Controls.Add(btnImport);
            tabPage4.Controls.Add(btnExport);
            tabPage4.Controls.Add(groupBox2);
            tabPage4.Controls.Add(btnUpdate);
            resources.ApplyResources(tabPage4, "tabPage4");
            tabPage4.Name = "tabPage4";
            tabPage4.UseVisualStyleBackColor = true;
            // 
            // btnGithub
            // 
            resources.ApplyResources(btnGithub, "btnGithub");
            btnGithub.Name = "btnGithub";
            btnGithub.UseVisualStyleBackColor = true;
            btnGithub.Click += btnGithub_Click;
            // 
            // btnImport
            // 
            resources.ApplyResources(btnImport, "btnImport");
            btnImport.Name = "btnImport";
            btnImport.UseVisualStyleBackColor = true;
            btnImport.Click += btnImport_Click;
            // 
            // btnExport
            // 
            resources.ApplyResources(btnExport, "btnExport");
            btnExport.Name = "btnExport";
            btnExport.UseVisualStyleBackColor = true;
            btnExport.Click += btnExport_Click;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(lblLinkAttributions);
            groupBox2.Controls.Add(lblLinkLicense);
            groupBox2.Controls.Add(lblVersion);
            resources.ApplyResources(groupBox2, "groupBox2");
            groupBox2.Name = "groupBox2";
            groupBox2.TabStop = false;
            // 
            // lblLinkAttributions
            // 
            resources.ApplyResources(lblLinkAttributions, "lblLinkAttributions");
            lblLinkAttributions.Name = "lblLinkAttributions";
            lblLinkAttributions.TabStop = true;
            lblLinkAttributions.LinkClicked += lblLinkAttributions_LinkClicked;
            // 
            // lblLinkLicense
            // 
            resources.ApplyResources(lblLinkLicense, "lblLinkLicense");
            lblLinkLicense.Name = "lblLinkLicense";
            lblLinkLicense.TabStop = true;
            lblLinkLicense.LinkClicked += lblLinkLicense_LinkClicked;
            // 
            // lblVersion
            // 
            resources.ApplyResources(lblVersion, "lblVersion");
            lblVersion.Name = "lblVersion";
            // 
            // btnUpdate
            // 
            resources.ApplyResources(btnUpdate, "btnUpdate");
            btnUpdate.Name = "btnUpdate";
            btnUpdate.UseVisualStyleBackColor = true;
            btnUpdate.Click += btnUpdate_Click;
            // 
            // sfd
            // 
            sfd.DefaultExt = "xml";
            // 
            // SettingsForm
            // 
            resources.ApplyResources(this, "$this");
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            Controls.Add(btnCancel);
            Controls.Add(btnOK);
            Controls.Add(tabControl1);
            KeyPreview = true;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "SettingsForm";
            FormClosing += SettingsForm_FormClosing;
            Load += SettingsForm_Load;
            Shown += SettingsForm_Shown;
            KeyDown += SettingsForm_KeyDown;
            tabPage3.ResumeLayout(false);
            tabPage3.PerformLayout();
            tabPage2.ResumeLayout(false);
            tabPage2.PerformLayout();
            tabPage1.ResumeLayout(false);
            tabPage1.PerformLayout();
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            tabControl1.ResumeLayout(false);
            tabPage4.ResumeLayout(false);
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnAppRemove;
        private System.Windows.Forms.Button btnAppModify;
        private System.Windows.Forms.Button btnAppAdd;
        private System.Windows.Forms.ListView listApplications;
        private System.Windows.Forms.ColumnHeader columnApp;
        private System.Windows.Forms.ColumnHeader columnDetails;
        private System.Windows.Forms.ColumnHeader columnLastModified;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckedListBox listOptionalGlobalProfiles;
        private System.Windows.Forms.CheckedListBox listRecommendedGlobalProfiles;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.CheckBox chkAskForExceptionDetails;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.CheckBox chkChangePassword;
        private System.Windows.Forms.TextBox txtPasswordAgain;
        private System.Windows.Forms.TextBox txtPassword;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Button btnSubmitAssoc;
        private System.Windows.Forms.SaveFileDialog sfd;
        private System.Windows.Forms.TabPage tabPage4;
        private System.Windows.Forms.Label lblVersion;
        private System.Windows.Forms.Button btnUpdate;
        private System.Windows.Forms.CheckBox chkBlockMalwarePorts;
        private System.Windows.Forms.Button btnAppAutoDetect;
        private System.Windows.Forms.ImageList IconList;
        private System.Windows.Forms.CheckBox chkAutoUpdateCheck;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.LinkLabel lblLinkLicense;
        private System.Windows.Forms.Button btnImport;
        private System.Windows.Forms.Button btnExport;
        private System.Windows.Forms.OpenFileDialog ofd;
        private System.Windows.Forms.CheckBox chkLockHostsFile;
        private System.Windows.Forms.CheckBox chkHostsBlocklist;
        private System.Windows.Forms.Button btnAppRemoveAll;
        private System.Windows.Forms.TextBox txtExceptionListFilter;
        private System.Windows.Forms.CheckBox chkEnableBlocklists;
        private System.Windows.Forms.ComboBox comboLanguages;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.CheckBox chkEnableHotkeys;
        private System.Windows.Forms.LinkLabel lblLinkAttributions;
        private System.Windows.Forms.ColumnHeader columnType;
        private System.Windows.Forms.CheckBox chkDisplayOffBlock;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Button btnGithub;
    }
}