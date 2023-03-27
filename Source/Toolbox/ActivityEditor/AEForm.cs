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
            using (AEOutcomeAct d = new AEOutcomeAct())
            {
                d.AEOutcomeEvent.Visible = false;
                d.AEOutcomeMessage.Visible = false;
                d.AEOutcomeSound.Visible = false;
                d.AEOutputWeather.Visible = false;
                d.ShowDialog();
            }

        }

        private void AENewActbutton_Click(object sender, EventArgs e)
        {
            MessageBox.Show("New button Clicked");

        }

        private void button22_Click(object sender, EventArgs e)
        {
            using (AEOutcomeAct d = new AEOutcomeAct())
            {
                d.AEOutcomeEvent.Visible = false;
                d.AEOutcomeMessage.Visible = false;
                d.AEOutcomeSound.Visible = false;
                d.AEOutputWeather.Visible = false;
                d.ShowDialog();
            }
        }

        private void button44_Click(object sender, EventArgs e)
        {
            using (AEOutcomeAct d = new AEOutcomeAct())
            {
                d.AEOutcomeEvent.Visible = false;
                d.AEOutcomeMessage.Visible = false;
                d.AEOutcomeSound.Visible = false;
                d.AEOutputWeather.Visible = false;
                d.ShowDialog();
            }
        }

        private void AETrafAddNewbutton_Click(object sender, EventArgs e)
        {
            using (TrafDiag d = new TrafDiag())
            {
                d.ShowDialog();
            }
        }

        private void AENewTrafbutton_Click(object sender, EventArgs e)
        {
            using (AEEditFilename d = new AEEditFilename())
            {
                d.Text = "New Traffic Filename";
                d.ShowDialog();
            }

        }

        private void AENewPlayerbutton_Click(object sender, EventArgs e)
        {
            using (AEEditFilename d = new AEEditFilename())
            {
                d.Text = "New Player Service Filename";
                d.ShowDialog();
            }
        }

    }
}
