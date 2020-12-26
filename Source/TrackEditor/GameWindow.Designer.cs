using System;
using System.Windows.Forms;

namespace Orts.TrackEditor
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
            if (disposing && (components != null))
            {
                components.Dispose();
                foreach (var item in Components)
                {
                    if (item is IDisposable disposable)
                        disposable.Dispose();
                }
            }
            graphicsDeviceManager?.Dispose();
            windowForm?.Dispose();
            spriteBatch?.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.statusbar = new WinForms.Controls.StatusbarControl();
            this.statusbar.SuspendLayout();
            this.mainmenu = new WinForms.Controls.MainMenuControl();
            this.mainmenu.SuspendLayout();
            windowForm.SuspendLayout();

            this.statusbar.Dock = DockStyle.Bottom;
            windowForm.Controls.Add(this.statusbar);
            this.statusbar.ResumeLayout(false);
            this.statusbar.PerformLayout();
            this.mainmenu.Dock = DockStyle.Top;
            windowForm.Controls.Add(this.mainmenu);
            this.mainmenu.ResumeLayout();
            this.mainmenu.PerformLayout();
            windowForm.ResumeLayout(false);
            windowForm.PerformLayout();
        }

        private TrackEditor.WinForms.Controls.StatusbarControl statusbar;
        private TrackEditor.WinForms.Controls.MainMenuControl mainmenu;
    }
}