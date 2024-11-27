namespace FreeTrainSimulator.Menu
{
    partial class KeyInputEditControl
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
            if (keyboardHookId != System.IntPtr.Zero)
            {
                UnhookKeyboard();
                keyboardHookId = System.IntPtr.Zero;
            }

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
            buttonDefault = new System.Windows.Forms.Button();
            toolTip1 = new System.Windows.Forms.ToolTip(components);
            SuspendLayout();
            // 
            // buttonOK
            // 
            buttonOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            buttonOK.Dock = System.Windows.Forms.DockStyle.Right;
            buttonOK.Location = new System.Drawing.Point(174, 0);
            buttonOK.Margin = new System.Windows.Forms.Padding(4);
            buttonOK.Name = "buttonOK";
            buttonOK.Size = new System.Drawing.Size(31, 28);
            buttonOK.TabIndex = 3;
            buttonOK.Text = "✔";
            toolTip1.SetToolTip(buttonOK, "Accept changes");
            // 
            // buttonCancel
            // 
            buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            buttonCancel.Dock = System.Windows.Forms.DockStyle.Right;
            buttonCancel.Location = new System.Drawing.Point(205, 0);
            buttonCancel.Margin = new System.Windows.Forms.Padding(4);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new System.Drawing.Size(31, 28);
            buttonCancel.TabIndex = 2;
            buttonCancel.Text = "✘";
            toolTip1.SetToolTip(buttonCancel, "Cancel changes");
            // 
            // textBox
            // 
            textBox.Dock = System.Windows.Forms.DockStyle.Fill;
            textBox.Location = new System.Drawing.Point(0, 0);
            textBox.Margin = new System.Windows.Forms.Padding(4);
            textBox.Name = "textBox";
            textBox.ReadOnly = true;
            textBox.Size = new System.Drawing.Size(174, 23);
            textBox.TabIndex = 0;
            toolTip1.SetToolTip(textBox, "Press any key");
            // 
            // buttonDefault
            // 
            buttonDefault.BackColor = System.Drawing.SystemColors.ControlLight;
            buttonDefault.DialogResult = System.Windows.Forms.DialogResult.Ignore;
            buttonDefault.Dock = System.Windows.Forms.DockStyle.Right;
            buttonDefault.Location = new System.Drawing.Point(236, 0);
            buttonDefault.Margin = new System.Windows.Forms.Padding(4);
            buttonDefault.Name = "buttonDefault";
            buttonDefault.Size = new System.Drawing.Size(31, 28);
            buttonDefault.TabIndex = 1;
            buttonDefault.Text = "↺";
            toolTip1.SetToolTip(buttonDefault, "Reset to default");
            buttonDefault.UseVisualStyleBackColor = false;
            // 
            // KeyInputEditControl
            // 
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Inherit;
            ClientSize = new System.Drawing.Size(267, 28);
            ControlBox = false;
            Controls.Add(textBox);
            Controls.Add(buttonOK);
            Controls.Add(buttonCancel);
            Controls.Add(buttonDefault);
            Font = new System.Drawing.Font("Segoe UI", 9F);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            Margin = new System.Windows.Forms.Padding(4);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "KeyInputEditControl";
            ShowIcon = false;
            ShowInTaskbar = false;
            SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            Text = "EditKey";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Button buttonOK;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.TextBox textBox;
        private System.Windows.Forms.Button buttonDefault;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}
