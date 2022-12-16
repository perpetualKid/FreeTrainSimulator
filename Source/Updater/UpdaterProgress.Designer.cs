namespace Orts.Updater
{
    partial class UpdaterProgress
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
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UpdaterProgress));
            this.progressBarUpdater = new System.Windows.Forms.ProgressBar();
            this.SuspendLayout();
            // 
            // progressBarUpdater
            // 
            this.progressBarUpdater.Location = new System.Drawing.Point(16, 19);
            this.progressBarUpdater.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.progressBarUpdater.Name = "progressBarUpdater";
            this.progressBarUpdater.Size = new System.Drawing.Size(347, 35);
            this.progressBarUpdater.TabIndex = 0;
            this.progressBarUpdater.UseWaitCursor = true;
            // 
            // UpdaterProgress
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(120F, 120F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(379, 72);
            this.Controls.Add(this.progressBarUpdater);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "UpdaterProgress";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Open Rails Updater";
            this.UseWaitCursor = true;
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.UpdaterProgress_FormClosed);
            this.Load += new System.EventHandler(this.UpdaterProgress_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ProgressBar progressBarUpdater;
    }
}

