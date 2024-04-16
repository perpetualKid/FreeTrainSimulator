using GetText;

using Microsoft.Xna.Framework;

using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Simulation.Multiplayer;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class MultiPlayerMessaging : WindowBase
    {
#pragma warning disable CA2213 // Disposable fields should be disposed
        private Label sendButton;
        private TextInput textInput;
#pragma warning restore CA2213 // Disposable fields should be disposed

        public MultiPlayerMessaging(WindowManager owner, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Multiplayer Messaging Window"), relativeLocation, new Point(400, 80), catalog)
        {
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            layout.Add(new Label(this, layout.RemainingWidth, Owner.TextFontDefault.Height, Catalog.GetString("Compose Message (<<receiver1, receiver2: message text>>)")));
            layout.AddHorizontalSeparator();
            layout.Add(textInput = new TextInput(this, layout.RemainingWidth, (int)(Owner.TextFontDefault.Height * 1.2)));
            textInput.OnEnterKey += TextInput_OnEnterKey;
            layout.Add(sendButton = new Label(this, layout.RemainingWidth, Owner.TextFontDefault.Height, "Send", Graphics.HorizontalAlignment.Center));
            sendButton.OnClick += SendButton_OnClick;
            return layout;
        }

        private void TextInput_OnEnterKey(object sender, System.EventArgs e)
        {
            SendMessage();
        }

        private void SendButton_OnClick(object sender, MouseClickEventArgs e)
        {
            SendMessage();
        }

        public override bool Open()
        {
            bool result = base.Open();
            if (result)
                ActivateControl(textInput);
            return result;
        }

        private async void SendMessage()
        {
            await MultiPlayerManager.Instance().SendMessage(textInput.Text).ConfigureAwait(false);
            textInput.Text = null;
            Close();
        }
    }
}
