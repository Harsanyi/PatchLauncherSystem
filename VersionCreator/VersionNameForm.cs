using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Patcher
{
    public partial class VersionNameForm : Form
    {
        public string version => versionTextBox.Text;

        public VersionNameForm()
        {
            InitializeComponent();
            okButton.Enabled = false;
            StartPosition = FormStartPosition.Manual;
            Location = MousePosition;
        }

        private void OnTextEdited(object sender, EventArgs e)
        {
            okButton.Enabled = !(string.IsNullOrEmpty(versionTextBox.Text)||string.IsNullOrWhiteSpace(versionTextBox.Text));
        }

        private void OnOkClicked(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
