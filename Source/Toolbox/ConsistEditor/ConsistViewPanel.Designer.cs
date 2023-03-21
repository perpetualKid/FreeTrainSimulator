namespace Orts.Toolbox.ConsistEditor
{
    partial class ConsistViewPanel
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            hScrollBar1 = new System.Windows.Forms.HScrollBar();
            SuspendLayout();
            // 
            // hScrollBar1
            // 
            hScrollBar1.Location = new System.Drawing.Point(0, 125);
            hScrollBar1.Name = "hScrollBar1";
            hScrollBar1.Size = new System.Drawing.Size(900, 25);
            hScrollBar1.TabIndex = 0;
            // 
            // ConsistViewPanel
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.LightBlue;
            Controls.Add(hScrollBar1);
            Name = "ConsistViewPanel";
            Size = new System.Drawing.Size(900, 150);
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.HScrollBar hScrollBar1;
    }
}
