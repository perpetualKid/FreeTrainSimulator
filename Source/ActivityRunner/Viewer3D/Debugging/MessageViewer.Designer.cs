namespace Orts.ActivityRunner.Viewer3D.Debugging
{
    partial class MessageViewer
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
            this.clearAll = new System.Windows.Forms.Button();
            this.replySelected = new System.Windows.Forms.Button();
            this.messages = new System.Windows.Forms.ListBox();
            this.MSG = new System.Windows.Forms.TextBox();
            this.compose = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // clearAll
            // 
            this.clearAll.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.clearAll.Location = new System.Drawing.Point(16, 15);
            this.clearAll.Margin = new System.Windows.Forms.Padding(4);
            this.clearAll.Name = "clearAll";
            this.clearAll.Size = new System.Drawing.Size(147, 32);
            this.clearAll.TabIndex = 2;
            this.clearAll.Text = "Clear All";
            this.clearAll.UseVisualStyleBackColor = true;
            this.clearAll.Click += new System.EventHandler(this.ClearAllClick);
            // 
            // replySelected
            // 
            this.replySelected.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.replySelected.Location = new System.Drawing.Point(451, 15);
            this.replySelected.Margin = new System.Windows.Forms.Padding(4);
            this.replySelected.Name = "replySelected";
            this.replySelected.Size = new System.Drawing.Size(157, 32);
            this.replySelected.TabIndex = 4;
            this.replySelected.Text = "Reply Selected";
            this.replySelected.UseVisualStyleBackColor = true;
            this.replySelected.Click += new System.EventHandler(this.ReplySelectedClick);
            // 
            // messages
            // 
            this.messages.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.messages.FormattingEnabled = true;
            this.messages.ItemHeight = 25;
            this.messages.Location = new System.Drawing.Point(19, 96);
            this.messages.Margin = new System.Windows.Forms.Padding(4);
            this.messages.Name = "messages";
            this.messages.Size = new System.Drawing.Size(691, 204);
            this.messages.TabIndex = 3;
            // 
            // MSG
            // 
            this.MSG.Enabled = false;
            this.MSG.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MSG.Location = new System.Drawing.Point(19, 54);
            this.MSG.Margin = new System.Windows.Forms.Padding(4);
            this.MSG.Name = "MSG";
            this.MSG.Size = new System.Drawing.Size(691, 34);
            this.MSG.TabIndex = 19;
            this.MSG.WordWrap = false;
            // 
            // compose
            // 
            this.compose.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.compose.Location = new System.Drawing.Point(232, 15);
            this.compose.Margin = new System.Windows.Forms.Padding(4);
            this.compose.Name = "compose";
            this.compose.Size = new System.Drawing.Size(157, 32);
            this.compose.TabIndex = 20;
            this.compose.Text = "Compose MSG";
            this.compose.UseVisualStyleBackColor = true;
            this.compose.Click += new System.EventHandler(this.ComposeClick);
            // 
            // MessageViewer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(120F, 120F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(716, 327);
            this.Controls.Add(this.compose);
            this.Controls.Add(this.MSG);
            this.Controls.Add(this.replySelected);
            this.Controls.Add(this.messages);
            this.Controls.Add(this.clearAll);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "MessageViewer";
            this.Text = "MessageViewer";
            this.ResumeLayout(false);
            this.PerformLayout();

      }

      #endregion

	  private System.Windows.Forms.Button clearAll;
	  private System.Windows.Forms.Button replySelected;
	  private System.Windows.Forms.ListBox messages;
	  private System.Windows.Forms.TextBox MSG;
	  private System.Windows.Forms.Button compose;
   }
}
