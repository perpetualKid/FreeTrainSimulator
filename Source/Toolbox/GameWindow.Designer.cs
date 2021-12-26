using System;
using System.Windows.Forms;

namespace Orts.Toolbox
{
    public partial class GameWindow
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
                foreach (var item in Components)
                {
                    if (item is IDisposable disposable)
                        disposable.Dispose();
                }

                ctsRouteLoading?.Dispose();
                loadRoutesSemaphore.Dispose();
                windowManager?.Dispose();
                graphicsDeviceManager?.Dispose();
                windowForm?.Dispose();
                spriteBatch?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            windowForm.SuspendLayout();
            this.mainmenu = new WinForms.Controls.MainMenuControl(this);
            this.mainmenu.SuspendLayout();
            windowForm.MainMenuStrip = mainmenu.Controls[0] as MenuStrip;
            this.mainmenu.Dock = DockStyle.Top;
            //windowForm.Controls.Add(mainmenu.Controls[0]);
            windowForm.Controls.Add(mainmenu);

            this.mainmenu.ResumeLayout();
            this.mainmenu.PerformLayout();

            this.statusbar = new WinForms.Controls.StatusbarControl(this);
            this.statusbar.SuspendLayout();
            this.statusbar.Dock = DockStyle.Bottom;
            windowForm.Controls.Add(statusbar);
            this.statusbar.ResumeLayout();
            this.statusbar.PerformLayout();

            windowForm.ResumeLayout(false);
            windowForm.PerformLayout();
        }

        private Toolbox.WinForms.Controls.MainMenuControl mainmenu;
        private Toolbox.WinForms.Controls.StatusbarControl statusbar;

    }
}