namespace Orts.Toolbox.ActivityEditor
{
    partial class TrafDiag
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
            label1 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            TrafDialogServiceName = new System.Windows.Forms.ComboBox();
            TrafDialogSerStartTime = new System.Windows.Forms.NumericUpDown();
            TrafDialogOKbutton = new System.Windows.Forms.Button();
            TrafDialogCancelbutton = new System.Windows.Forms.Button();
            TrafDialog24HourPlus = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)TrafDialogSerStartTime).BeginInit();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label1.Location = new System.Drawing.Point(49, 25);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(72, 19);
            label1.TabIndex = 0;
            label1.Text = "Service:";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label2.Location = new System.Drawing.Point(29, 57);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(92, 19);
            label2.TabIndex = 1;
            label2.Text = "Start Time:";
            // 
            // TrafDialogServiceName
            // 
            TrafDialogServiceName.FormattingEnabled = true;
            TrafDialogServiceName.Location = new System.Drawing.Point(127, 25);
            TrafDialogServiceName.Name = "TrafDialogServiceName";
            TrafDialogServiceName.Size = new System.Drawing.Size(321, 23);
            TrafDialogServiceName.TabIndex = 2;
            // 
            // TrafDialogSerStartTime
            // 
            TrafDialogSerStartTime.Location = new System.Drawing.Point(127, 58);
            TrafDialogSerStartTime.Name = "TrafDialogSerStartTime";
            TrafDialogSerStartTime.Size = new System.Drawing.Size(321, 23);
            TrafDialogSerStartTime.TabIndex = 3;
            // 
            // TrafDialogOKbutton
            // 
            TrafDialogOKbutton.BackColor = System.Drawing.Color.IndianRed;
            TrafDialogOKbutton.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            TrafDialogOKbutton.Location = new System.Drawing.Point(282, 99);
            TrafDialogOKbutton.Name = "TrafDialogOKbutton";
            TrafDialogOKbutton.Size = new System.Drawing.Size(80, 27);
            TrafDialogOKbutton.TabIndex = 4;
            TrafDialogOKbutton.Text = "OK";
            TrafDialogOKbutton.UseVisualStyleBackColor = false;
            TrafDialogOKbutton.Click += TrafDialogOKbutton_Click;
            // 
            // TrafDialogCancelbutton
            // 
            TrafDialogCancelbutton.BackColor = System.Drawing.Color.IndianRed;
            TrafDialogCancelbutton.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            TrafDialogCancelbutton.Location = new System.Drawing.Point(368, 99);
            TrafDialogCancelbutton.Name = "TrafDialogCancelbutton";
            TrafDialogCancelbutton.Size = new System.Drawing.Size(80, 27);
            TrafDialogCancelbutton.TabIndex = 5;
            TrafDialogCancelbutton.Text = "Cancel";
            TrafDialogCancelbutton.UseVisualStyleBackColor = false;
            TrafDialogCancelbutton.Click += TrafDialogCancelbutton_Click;
            // 
            // TrafDialog24HourPlus
            // 
            TrafDialog24HourPlus.AutoSize = true;
            TrafDialog24HourPlus.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            TrafDialog24HourPlus.Location = new System.Drawing.Point(127, 99);
            TrafDialog24HourPlus.Name = "TrafDialog24HourPlus";
            TrafDialog24HourPlus.Size = new System.Drawing.Size(98, 23);
            TrafDialog24HourPlus.TabIndex = 6;
            TrafDialog24HourPlus.Text = "+ 24 HRS";
            TrafDialog24HourPlus.UseVisualStyleBackColor = true;
            // 
            // TrafDiag
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(474, 140);
            Controls.Add(TrafDialog24HourPlus);
            Controls.Add(TrafDialogCancelbutton);
            Controls.Add(TrafDialogOKbutton);
            Controls.Add(TrafDialogSerStartTime);
            Controls.Add(TrafDialogServiceName);
            Controls.Add(label2);
            Controls.Add(label1);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "TrafDiag";
            Text = "Traffic Start Time";
            ((System.ComponentModel.ISupportInitialize)TrafDialogSerStartTime).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox TrafDialogServiceName;
        private System.Windows.Forms.NumericUpDown TrafDialogSerStartTime;
        private System.Windows.Forms.Button TrafDialogOKbutton;
        private System.Windows.Forms.Button TrafDialogCancelbutton;
        private System.Windows.Forms.CheckBox TrafDialog24HourPlus;
    }
}