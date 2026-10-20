using System;
using System.Windows.Forms;

namespace FreeTrainSimulator.Toolbox.Hosting
{
    /// <summary>Owner-thread dispatch HWND whose lifetime is independent of the render form and its message loop.</summary>
    internal sealed class GameThreadControl : Control
    {
        internal GameThreadControl()
        {
            _ = Handle;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                // HWND_MESSAGE avoids WinForms' parking window, which is destroyed by Application.Run teardown.
                parameters.Parent = new IntPtr(-3);
                return parameters;
            }
        }
    }
}
