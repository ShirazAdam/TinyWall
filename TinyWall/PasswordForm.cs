using System;
using System.Windows.Forms;

namespace ModernTinyWall.TinyWall
{
    internal partial class PasswordForm : Form
    {
        internal string PassHash { get; private set; } = string.Empty;

        internal PasswordForm()
        {
            InitializeComponent();
            Utils.SetRightToLeft(this);
            this.btnOK.Image = GlobalInstances.ApplyBtnIcon;
            this.btnCancel.Image = GlobalInstances.CancelBtnIcon;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            PassHash = Hasher.HashString(txtPassphrase.Text);
            this.DialogResult = DialogResult.OK;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }

        private void PasswordForm_Shown(object sender, EventArgs e)
        {
            txtPassphrase.Focus();
        }
    }
}
