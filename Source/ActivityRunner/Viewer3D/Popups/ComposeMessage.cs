// COPYRIGHT 2012, 2013, 2014, 2015 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

// This file is the responsibility of the 3D & Environment Team. 

using System.Collections.Generic;
using System.Linq;
using System.Text;

using Orts.ActivityRunner.Viewer3D.Processes;
using Orts.Common.Input;
using Orts.MultiPlayer;

namespace Orts.ActivityRunner.Viewer3D.Popups
{
    public class ComposeMessage : Window
    {
        private Label messageLabel;
        private readonly StringBuilder messageText = new StringBuilder();

        private readonly KeyboardInputHandler<UserCommand> keyboardInput;
        private readonly Game game;

        public ComposeMessage(WindowManager owner, KeyboardInputHandler<UserCommand> keyboardInput, Game game)
            : base(owner, DecorationSize.X + owner.TextFontDefault.Height * 37, DecorationSize.Y + owner.TextFontDefault.Height * 2 + ControlLayout.SeparatorSize * 1, Viewer.Catalog.GetString("Compose Message (e.g.   receiver1, receiver2: message body)"))
        {
            this.keyboardInput = keyboardInput;
            this.game = game;
        }

        private void Window_TextInput(object sender, Microsoft.Xna.Framework.TextInputEventArgs e)
        {
            AppendMessage(e.Character);
        }

        public void InitMessage()
        {
            Visible = true;
            keyboardInput.SuspendForOverlayInput();
            game.Window.TextInput += Window_TextInput;

            if (!string.IsNullOrEmpty(MultiPlayerManager.Instance().lastSender))
                messageLabel.Text = MultiPlayerManager.Instance().lastSender + ":";
        }

        private void AppendMessage(char character)
        {
            switch (character)
            {
                case '\r':
                    SendMessage();
                    break;
                case '\b':
                    if (messageText.Length > 0)
                        messageText.Length--;
                    break;
                default:
                    if (!char.IsControl(character))
                        messageText.Append(character);
                    break;
            }
            messageLabel.Text = messageText.ToString();
        }

        private void SendMessage()
        {
            //we need to send message out
            string user = "";
            if (string.IsNullOrEmpty(MultiPlayerManager.Instance().lastSender)) //server will broadcast the message to everyone
                user = MultiPlayerManager.IsServer() ? string.Join("", MultiPlayerManager.OnlineTrains.Players.Keys.Select((string k) => $"{k}\r")) + "0END" : "0Server\r0END";

            string msg = messageText.ToString();
            int index = msg.IndexOf(':');
            if (index > 0)
            {
                msg = messageText.ToString(index + 1, messageText.Length - index - 1);

                IEnumerable<string> onlinePlayers = messageLabel.Text.Substring(0, index)
                    .Split(',')
                    .Select((string n) => n.Trim())
                    .Where((string nt) => MultiPlayerManager.OnlineTrains.Players.ContainsKey(nt))
                    .Select((string nt) => $"{nt}\r");

                string newUser = string.Join("", onlinePlayers);
                if (newUser.Length > 0)
                    user = newUser;
                user += "0END";
            }

            string msgText = new MSGText(MultiPlayerManager.GetUserName(), user, msg).ToString();
            try
            {
                MultiPlayerManager.Notify(msgText);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
#pragma warning restore CA1031 // Do not catch general exception types
            {
            }
            finally
            {
                Visible = false;
                messageText.Clear();
                game.Window.TextInput -= Window_TextInput;
                keyboardInput.ResumeFromOverlayInput();
            }
        }

        protected override ControlLayout Layout(ControlLayout layout)
        {
            ControlLayoutVertical vbox = base.Layout(layout).AddLayoutVertical();
            {
                ControlLayoutHorizontal hbox = vbox.AddLayoutHorizontalLineOfText();
                hbox.Add(messageLabel = new Label(hbox.RemainingWidth, hbox.RemainingHeight, string.Empty));
            }
            return vbox;
        }
    }
}
