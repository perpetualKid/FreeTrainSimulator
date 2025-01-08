namespace FreeTrainSimulator.Menu
{
    partial class TextInputControl
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
            components = new System.ComponentModel.Container();
            buttonOK = new System.Windows.Forms.Button();
            buttonCancel = new System.Windows.Forms.Button();
            textBox = new System.Windows.Forms.TextBox();
            toolTip1 = new System.Windows.Forms.ToolTip(components);
            SuspendLayout();
            // 
            // buttonOK
            // 
            buttonOK.Dock = System.Windows.Forms.DockStyle.Right;
            buttonOK.Location = new System.Drawing.Point(205, 0);
            buttonOK.Margin = new System.Windows.Forms.Padding(4);
            buttonOK.Name = "buttonOK";
            buttonOK.Size = new System.Drawing.Size(31, 28);
            buttonOK.TabIndex = 3;
            buttonOK.Text = "✔";
            toolTip1.SetToolTip(buttonOK, "Accept changes");
            buttonOK.Click += ButtonOK_Click;
            // 
            // buttonCancel
            // 
            buttonCancel.Dock = System.Windows.Forms.DockStyle.Right;
            buttonCancel.Location = new System.Drawing.Point(236, 0);
            buttonCancel.Margin = new System.Windows.Forms.Padding(4);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new System.Drawing.Size(31, 28);
            buttonCancel.TabIndex = 2;
            buttonCancel.Text = "✘";
            toolTip1.SetToolTip(buttonCancel, "Cancel changes");
            buttonCancel.Click += ButtonCancel_Click;
            // 
            // textBox
            // 
            textBox.Dock = System.Windows.Forms.DockStyle.Fill;
            textBox.HideSelection = false;
            textBox.Location = new System.Drawing.Point(0, 0);
            textBox.Margin = new System.Windows.Forms.Padding(4);
            textBox.Name = "textBox";
            textBox.PlaceholderText = "<Profile Name>";
            textBox.Size = new System.Drawing.Size(205, 23);
            textBox.TabIndex = 0;
            toolTip1.SetToolTip(textBox, "Profile Name");
            // 
            // TextInputControl
            // 
            Controls.Add(textBox);
            Controls.Add(buttonOK);
            Controls.Add(buttonCancel);
            Font = new System.Drawing.Font("Segoe UI", 9F);
            Margin = new System.Windows.Forms.Padding(4);
            Name = "TextInputControl";
            Size = new System.Drawing.Size(267, 28);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Button buttonOK;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.TextBox textBox;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}
