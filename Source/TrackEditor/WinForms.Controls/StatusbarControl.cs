using System.Windows.Forms;

namespace Orts.TrackEditor.WinForms.Controls
{
    public partial class StatusbarControl : UserControl
    {
        public StatusbarControl()
        {
            InitializeComponent();
        }

        public void UpdateStatusbarVisibility(bool show)
        {
            StatusStrip.SizingGrip = show;
        }

    }
}
