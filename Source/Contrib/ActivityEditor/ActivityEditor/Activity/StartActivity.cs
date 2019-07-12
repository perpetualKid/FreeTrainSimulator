using System;
using System.Windows.Forms;
using Orts.ActivityEditor.Engine;

namespace Orts.ActivityEditor.Activity
{
    public partial class StartActivity : Form
    {
        AEConfig config;

        public StartActivity(AEConfig aeConfig)
        {
            InitializeComponent();
            config = aeConfig;
            ActName.Text = aeConfig.GetActivityName();
            ActDescr.Text = aeConfig.GetActivityDescr();
        }

        private void SA_OK_Click(object sender, EventArgs e)
        {
            if (ActName.Text.Length <= 0)
                return;
            config.SetActivityName(ActName.Text);
            config.SetActivityDescr(ActDescr.Text);
            Close();
        }
    }
}
