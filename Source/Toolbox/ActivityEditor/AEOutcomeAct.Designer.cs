namespace Orts.Toolbox.ActivityEditor
{
    partial class AEOutcomeAct
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
            comboBox1 = new System.Windows.Forms.ComboBox();
            AEOutcomeEvent = new System.Windows.Forms.GroupBox();
            comboBox2 = new System.Windows.Forms.ComboBox();
            AEOutcomeSound = new System.Windows.Forms.GroupBox();
            comboBox3 = new System.Windows.Forms.ComboBox();
            label2 = new System.Windows.Forms.Label();
            S = new System.Windows.Forms.TextBox();
            AEOutputWeather = new System.Windows.Forms.GroupBox();
            comboBox4 = new System.Windows.Forms.ComboBox();
            button1 = new System.Windows.Forms.Button();
            button2 = new System.Windows.Forms.Button();
            AEOutcomeMessage = new System.Windows.Forms.GroupBox();
            textBox1 = new System.Windows.Forms.TextBox();
            AEOutcomeEvent.SuspendLayout();
            AEOutcomeSound.SuspendLayout();
            AEOutputWeather.SuspendLayout();
            AEOutcomeMessage.SuspendLayout();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label1.Location = new System.Drawing.Point(12, 23);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(64, 19);
            label1.TabIndex = 0;
            label1.Text = "Action:";
            // 
            // comboBox1
            // 
            comboBox1.FormattingEnabled = true;
            comboBox1.Items.AddRange(new object[] { "Display A Message.", "Complete Activity Successfully.", "End Activity without success.", "Increase an event's activation level.", "Decrease an event's activation level.", "Restore an event's activation level.", "Activate an event.", "Start ignoring speed limits.", "Stop ignoring speed limits.", "Play Sound from file.", "Change Weather." });
            comboBox1.Location = new System.Drawing.Point(82, 23);
            comboBox1.Name = "comboBox1";
            comboBox1.Size = new System.Drawing.Size(337, 23);
            comboBox1.TabIndex = 1;
            comboBox1.SelectedIndexChanged += comboBox1_SelectedIndexChanged;
            // 
            // AEOutcomeEvent
            // 
            AEOutcomeEvent.Controls.Add(comboBox2);
            AEOutcomeEvent.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            AEOutcomeEvent.Location = new System.Drawing.Point(12, 52);
            AEOutcomeEvent.Name = "AEOutcomeEvent";
            AEOutcomeEvent.Size = new System.Drawing.Size(422, 51);
            AEOutcomeEvent.TabIndex = 4;
            AEOutcomeEvent.TabStop = false;
            AEOutcomeEvent.Text = "Event";
            // 
            // comboBox2
            // 
            comboBox2.FormattingEnabled = true;
            comboBox2.Location = new System.Drawing.Point(6, 18);
            comboBox2.Name = "comboBox2";
            comboBox2.Size = new System.Drawing.Size(401, 27);
            comboBox2.TabIndex = 5;
            // 
            // AEOutcomeSound
            // 
            AEOutcomeSound.Controls.Add(comboBox3);
            AEOutcomeSound.Controls.Add(label2);
            AEOutcomeSound.Controls.Add(S);
            AEOutcomeSound.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            AEOutcomeSound.Location = new System.Drawing.Point(12, 107);
            AEOutcomeSound.Name = "AEOutcomeSound";
            AEOutcomeSound.Size = new System.Drawing.Size(422, 100);
            AEOutcomeSound.TabIndex = 5;
            AEOutcomeSound.TabStop = false;
            AEOutcomeSound.Text = "Sound File";
            // 
            // comboBox3
            // 
            comboBox3.FormattingEnabled = true;
            comboBox3.Items.AddRange(new object[] { "Everywhere", "Cab", "Passenger", "Ground" });
            comboBox3.Location = new System.Drawing.Point(64, 61);
            comboBox3.Name = "comboBox3";
            comboBox3.Size = new System.Drawing.Size(346, 27);
            comboBox3.TabIndex = 2;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(6, 64);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(52, 19);
            label2.TabIndex = 1;
            label2.Text = "Type:";
            // 
            // S
            // 
            S.Location = new System.Drawing.Point(6, 22);
            S.Name = "S";
            S.Size = new System.Drawing.Size(404, 26);
            S.TabIndex = 0;
            // 
            // AEOutputWeather
            // 
            AEOutputWeather.Controls.Add(comboBox4);
            AEOutputWeather.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            AEOutputWeather.Location = new System.Drawing.Point(12, 213);
            AEOutputWeather.Name = "AEOutputWeather";
            AEOutputWeather.Size = new System.Drawing.Size(422, 58);
            AEOutputWeather.TabIndex = 6;
            AEOutputWeather.TabStop = false;
            AEOutputWeather.Text = "Weather Change";
            // 
            // comboBox4
            // 
            comboBox4.FormattingEnabled = true;
            comboBox4.Location = new System.Drawing.Point(6, 25);
            comboBox4.Name = "comboBox4";
            comboBox4.Size = new System.Drawing.Size(404, 27);
            comboBox4.TabIndex = 7;
            // 
            // button1
            // 
            button1.BackColor = System.Drawing.Color.Red;
            button1.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            button1.Location = new System.Drawing.Point(640, 450);
            button1.Name = "button1";
            button1.Size = new System.Drawing.Size(75, 32);
            button1.TabIndex = 7;
            button1.Text = "OK";
            button1.UseVisualStyleBackColor = false;
            button1.Click += button1_Click;
            // 
            // button2
            // 
            button2.BackColor = System.Drawing.Color.Red;
            button2.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            button2.Location = new System.Drawing.Point(721, 450);
            button2.Name = "button2";
            button2.Size = new System.Drawing.Size(75, 32);
            button2.TabIndex = 8;
            button2.Text = "Cancel";
            button2.UseVisualStyleBackColor = false;
            button2.Click += button2_Click;
            // 
            // AEOutcomeMessage
            // 
            AEOutcomeMessage.Controls.Add(textBox1);
            AEOutcomeMessage.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            AEOutcomeMessage.Location = new System.Drawing.Point(12, 277);
            AEOutcomeMessage.Name = "AEOutcomeMessage";
            AEOutcomeMessage.Size = new System.Drawing.Size(776, 167);
            AEOutcomeMessage.TabIndex = 9;
            AEOutcomeMessage.TabStop = false;
            AEOutcomeMessage.Text = "Display Message";
            // 
            // textBox1
            // 
            textBox1.Location = new System.Drawing.Point(6, 25);
            textBox1.Multiline = true;
            textBox1.Name = "textBox1";
            textBox1.Size = new System.Drawing.Size(764, 136);
            textBox1.TabIndex = 0;
            // 
            // AEOutcomeAct
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(800, 494);
            Controls.Add(AEOutcomeMessage);
            Controls.Add(button2);
            Controls.Add(button1);
            Controls.Add(AEOutputWeather);
            Controls.Add(AEOutcomeSound);
            Controls.Add(AEOutcomeEvent);
            Controls.Add(comboBox1);
            Controls.Add(label1);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "AEOutcomeAct";
            Text = "Selected Outcome";
            TopMost = true;
            AEOutcomeEvent.ResumeLayout(false);
            AEOutcomeSound.ResumeLayout(false);
            AEOutcomeSound.PerformLayout();
            AEOutputWeather.ResumeLayout(false);
            AEOutcomeMessage.ResumeLayout(false);
            AEOutcomeMessage.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.ComboBox comboBox2;
        private System.Windows.Forms.ComboBox comboBox3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox S;
        private System.Windows.Forms.ComboBox comboBox4;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        public System.Windows.Forms.GroupBox AEOutcomeEvent;
        public System.Windows.Forms.GroupBox AEOutcomeSound;
        public System.Windows.Forms.GroupBox AEOutputWeather;
        public System.Windows.Forms.GroupBox AEOutcomeMessage;
        private System.Windows.Forms.TextBox textBox1;
    }
}