using System;
using System.IO;
using System.Windows.Forms;

using GetText;

namespace FreeTrainSimulator.Menu
{
    public partial class TextInputControl : UserControl
    {
        private ToolTip toolTip;
        public event EventHandler OnAccept;
        public event EventHandler OnCancel;

        internal TextInputControl()
        {
            InitializeComponent();
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (Validate() || keyData == Keys.Escape)
            {
                toolTip?.Hide(this);
                switch (keyData)
                {
                    case Keys.Escape:
                        OnCancel?.Invoke(this, EventArgs.Empty);
                        return true;
                    case Keys.Return:
                        OnAccept?.Invoke(this, EventArgs.Empty);
                        return true;
                    default:
                        return base.ProcessDialogKey(keyData);
                }
            }
            return false;
        }

        public override string Text { get => textBox.Text; set => textBox.Text = value; }

        private void ButtonOK_Click(object sender, EventArgs e)
        {
            OnAccept?.Invoke(this, EventArgs.Empty);
        }

        private void ButtonCancel_Click(object sender, EventArgs e)
        {
            OnCancel?.Invoke(this, EventArgs.Empty);
        }

        private void TextBox_Validating(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (textBox.Text.IndexOfAny(Path.GetInvalidFileNameChars()) > -1)
            {
                e.Cancel = true;
                toolTip ??= new ToolTip()
                {
                    InitialDelay = 100,
                };
                toolTip.Show(CatalogManager.Catalog.GetString("Invalid characters detected, please check!"), this, textBox.Cursor.HotSpot.X, textBox.Bottom, 2500);
            }
        }
    }
}
