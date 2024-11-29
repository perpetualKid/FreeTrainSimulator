using System;
using System.Windows.Forms;

using GetText;
using GetText.WindowsForms;

namespace FreeTrainSimulator.Menu
{
    public partial class ModelConverterProgress : Form, IProgress<int>
    {
        public ModelConverterProgress()
        {
            InitializeComponent();
            Localizer.Localize(this, CatalogManager.Catalog);
        }

        public void Report(int value)
        {
            if (InvokeRequired)
            {
                Invoke(Report, value);
                return;
            }
            progressBarAnalyzer.Value = value;
        }

        private void ModelConverterProgress_Shown(object sender, EventArgs e)
        {
            CenterToParent();
            BringToFront();
        }
    }
}
