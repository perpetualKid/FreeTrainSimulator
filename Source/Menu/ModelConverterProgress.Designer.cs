namespace FreeTrainSimulator.Menu
{
    partial class ModelConverterProgress
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ModelConverterProgress));
            progressBarAnalyzer = new System.Windows.Forms.ProgressBar();
            textBoxDescription = new System.Windows.Forms.TextBox();
            SuspendLayout();
            // 
            // progressBarAnalyzer
            // 
            progressBarAnalyzer.Location = new System.Drawing.Point(16, 43);
            progressBarAnalyzer.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            progressBarAnalyzer.Name = "progressBarAnalyzer";
            progressBarAnalyzer.Size = new System.Drawing.Size(347, 35);
            progressBarAnalyzer.Step = 1;
            progressBarAnalyzer.TabIndex = 0;
            progressBarAnalyzer.UseWaitCursor = true;
            // 
            // textBoxDescription
            // 
            textBoxDescription.BorderStyle = System.Windows.Forms.BorderStyle.None;
            textBoxDescription.Enabled = false;
            textBoxDescription.Location = new System.Drawing.Point(16, 7);
            textBoxDescription.Multiline = true;
            textBoxDescription.Name = "textBoxDescription";
            textBoxDescription.ReadOnly = true;
            textBoxDescription.Size = new System.Drawing.Size(347, 39);
            textBoxDescription.TabIndex = 1;
            textBoxDescription.Text = "Please wait while content folders are being analyzed.\r\nThis may take a few minutes to complete.";
            textBoxDescription.UseWaitCursor = true;
            // 
            // ModelConverterProgress
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(379, 95);
            ControlBox = false;
            Controls.Add(textBoxDescription);
            Controls.Add(progressBarAnalyzer);
            Font = new System.Drawing.Font("Segoe UI", 9F);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "ModelConverterProgress";
            ShowInTaskbar = false;
            SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Content Analyzer Progress";
            UseWaitCursor = true;
            Shown += ModelConverterProgress_Shown;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.ProgressBar progressBarAnalyzer;
        private System.Windows.Forms.TextBox textBoxDescription;
    }
}

