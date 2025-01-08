using System;
using System.Windows.Forms;

namespace FreeTrainSimulator.Menu
{
    public partial class TextInputControl : UserControl
    {
        public event EventHandler OnAccept;
        public event EventHandler OnCancel;

        internal TextInputControl()
        {
            InitializeComponent();
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
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

        public override string Text { get => textBox.Text; set => textBox.Text = value; }

        private void ButtonOK_Click(object sender, EventArgs e)
        {
            OnAccept?.Invoke(this, EventArgs.Empty);
        }

        private void ButtonCancel_Click(object sender, EventArgs e)
        {
            OnCancel?.Invoke(this, EventArgs.Empty);
        }
    }
}
