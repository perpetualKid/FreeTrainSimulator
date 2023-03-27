namespace Orts.Toolbox.ActivityEditor
{
    partial class AEEditFilename
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
            FilenametextBox = new System.Windows.Forms.TextBox();
            AEEditFilenameOKbutton = new System.Windows.Forms.Button();
            AEEditFileNameCancelbutton = new System.Windows.Forms.Button();
            SuspendLayout();
            // 
            // FilenametextBox
            // 
            FilenametextBox.BackColor = System.Drawing.SystemColors.ActiveCaptionText;
            FilenametextBox.Location = new System.Drawing.Point(-2, 1);
            FilenametextBox.Name = "FilenametextBox";
            FilenametextBox.Size = new System.Drawing.Size(378, 23);
            FilenametextBox.TabIndex = 0;
            // 
            // AEEditFilenameOKbutton
            // 
            AEEditFilenameOKbutton.BackColor = System.Drawing.Color.IndianRed;
            AEEditFilenameOKbutton.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            AEEditFilenameOKbutton.Location = new System.Drawing.Point(192, 30);
            AEEditFilenameOKbutton.Name = "AEEditFilenameOKbutton";
            AEEditFilenameOKbutton.Size = new System.Drawing.Size(89, 31);
            AEEditFilenameOKbutton.TabIndex = 1;
            AEEditFilenameOKbutton.Text = "OK";
            AEEditFilenameOKbutton.UseVisualStyleBackColor = false;
            AEEditFilenameOKbutton.Click += AEEditFilenameOKbutton_Click;
            // 
            // AEEditFileNameCancelbutton
            // 
            AEEditFileNameCancelbutton.BackColor = System.Drawing.Color.IndianRed;
            AEEditFileNameCancelbutton.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            AEEditFileNameCancelbutton.Location = new System.Drawing.Point(287, 30);
            AEEditFileNameCancelbutton.Name = "AEEditFileNameCancelbutton";
            AEEditFileNameCancelbutton.Size = new System.Drawing.Size(89, 31);
            AEEditFileNameCancelbutton.TabIndex = 2;
            AEEditFileNameCancelbutton.Text = "Cancel";
            AEEditFileNameCancelbutton.UseVisualStyleBackColor = false;
            AEEditFileNameCancelbutton.Click += AEEditFileNameCancelbutton_Click;
            // 
            // AEEditFilename
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(379, 63);
            Controls.Add(AEEditFileNameCancelbutton);
            Controls.Add(AEEditFilenameOKbutton);
            Controls.Add(FilenametextBox);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "AEEditFilename";
            Text = "AEEditFilename";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TextBox FilenametextBox;
        private System.Windows.Forms.Button AEEditFilenameOKbutton;
        private System.Windows.Forms.Button AEEditFileNameCancelbutton;
    }
}