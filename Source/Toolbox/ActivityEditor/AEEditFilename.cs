﻿using System;
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
    public partial class AEEditFilename : Form
    {
        public AEEditFilename()
        {
            InitializeComponent();
        }

        private void AEEditFilenameOKbutton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void AEEditFileNameCancelbutton_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
