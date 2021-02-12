using System.Windows.Forms;

namespace Orts.TrackViewer.WinForms.Controls
{
    public partial class StatusbarControl : UserControl
    {
        private readonly GameWindow parent;

        public StatusbarControl(GameWindow game)
        {
            parent = game;
            InitializeComponent();
        }

        public void UpdateStatusbarVisibility(bool show)
        {
            StatusStrip.SizingGrip = show;
        }

    }
}
