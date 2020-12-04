using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            }
            graphicsDeviceManager?.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.statusbar = new WinForms.Controls.StatusbarControl();
            this.statusbar.SuspendLayout();
            windowForm.SuspendLayout();

            this.statusbar.Dock = DockStyle.Bottom;
            windowForm.Controls.Add(this.statusbar);
            this.statusbar.ResumeLayout(false);
            this.statusbar.PerformLayout();
            windowForm.ResumeLayout(false);
            windowForm.PerformLayout();
        }

        private TrackEditor.WinForms.Controls.StatusbarControl statusbar;
    }
}