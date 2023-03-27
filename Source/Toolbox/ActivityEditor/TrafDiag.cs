using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Orts.Toolbox.ActivityEditor
{
    public partial class TrafDiag : Form
    {
        public TrafDiag()
        {
            InitializeComponent();
        }

        private void TrafDialogOKbutton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void TrafDialogCancelbutton_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
