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
    public partial class AEForm : Form
    {
        public AEForm()
        {
            InitializeComponent();
        }

        private void button48_Click(object sender, EventArgs e)
        {
            AEOutcomeAct d = new AEOutcomeAct();
            d.AEOutcomeEvent.Visible = false;
            d.AEOutcomeMessage.Visible = false;
            d.AEOutcomeSound.Visible = false;
            d.AEOutputWeather.Visible = false;
            d.ShowDialog();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            MessageBox.Show("New button Clicked");

        }

        private void button22_Click(object sender, EventArgs e)
        {
            AEOutcomeAct d = new AEOutcomeAct();
            d.AEOutcomeEvent.Visible = false;
            d.AEOutcomeMessage.Visible = false;
            d.AEOutcomeSound.Visible = false;
            d.AEOutputWeather.Visible = false;
            d.ShowDialog();

        }

        private void button44_Click(object sender, EventArgs e)
        {
            AEOutcomeAct d = new AEOutcomeAct();
            d.AEOutcomeEvent.Visible = false;
            d.AEOutcomeMessage.Visible = false;
            d.AEOutcomeSound.Visible = false;
            d.AEOutputWeather.Visible = false;
            d.ShowDialog();
        }

        private void button9_Click(object sender, EventArgs e)
        {
            TrafDiag d = new TrafDiag();
            d.ShowDialog();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            AEEditFilename d = new AEEditFilename();
            d.Text = "New Traffic Filename";
            d.ShowDialog();
        }

        private void button24_Click(object sender, EventArgs e)
        {
            AEEditFilename d = new AEEditFilename();
            d.Text = "New Player Service Filename";
            d.ShowDialog();
        }
    }
}
