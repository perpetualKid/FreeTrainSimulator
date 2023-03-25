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
    public partial class AEOutcomeAct : Form
    {
        public AEOutcomeAct()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int i = comboBox1.SelectedIndex;
            switch (i)
            {
                case 0:
                    AEOutcomeEvent.Visible = false;
                    AEOutcomeMessage.Visible = true;
                    AEOutcomeSound.Visible = false;
                    AEOutputWeather.Visible = false;
                    break;
                case 2:
                    AEOutcomeEvent.Visible = false;
                    AEOutcomeMessage.Visible = true;
                    AEOutcomeSound.Visible = false;
                    AEOutputWeather.Visible = false;
                    break;
                case 3:
                    AEOutcomeEvent.Visible = true;
                    AEOutcomeMessage.Visible = false;
                    AEOutcomeSound.Visible = false;
                    AEOutputWeather.Visible = false;
                    break;
                case 4:
                    AEOutcomeEvent.Visible = true;
                    AEOutcomeMessage.Visible = false;
                    AEOutcomeSound.Visible = false;
                    AEOutputWeather.Visible = false;
                    break;
                case 5:
                    AEOutcomeEvent.Visible = true;
                    AEOutcomeMessage.Visible = false;
                    AEOutcomeSound.Visible = false;
                    AEOutputWeather.Visible = false;
                    break;
                case 6:
                    AEOutcomeEvent.Visible = true;
                    AEOutcomeMessage.Visible = false;
                    AEOutcomeSound.Visible = false;
                    AEOutputWeather.Visible = false;
                    break;
                case 9:
                    AEOutcomeEvent.Visible = false;
                    AEOutcomeMessage.Visible = false;
                    AEOutcomeSound.Visible = true;
                    AEOutputWeather.Visible = false;
                    break;
                case 10:
                    AEOutcomeEvent.Visible = false;
                    AEOutcomeMessage.Visible = false;
                    AEOutcomeSound.Visible = false;
                    AEOutputWeather.Visible = true;
                    break;
                default:
                    AEOutcomeEvent.Visible = false;
                    AEOutcomeMessage.Visible = false;
                    AEOutcomeSound.Visible = false;
                    AEOutputWeather.Visible = false;
                    break;
            }
            this.Refresh();
        }
    }
}
