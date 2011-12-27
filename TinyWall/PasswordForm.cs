﻿using System;
using System.Windows.Forms;

namespace PKSoft
{
    internal partial class PasswordForm : Form
    {
        private string m_PassHash;

        internal string PassHash
        {
            get { return m_PassHash; }
        }

        internal PasswordForm()
        {
            InitializeComponent();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            m_PassHash = Utils.GetHash(txtPassphrase.Text);
            this.DialogResult = System.Windows.Forms.DialogResult.OK;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = System.Windows.Forms.DialogResult.Cancel;
        }

        private void PasswordForm_Shown(object sender, EventArgs e)
        {
            txtPassphrase.Focus();
        }
    }
}
