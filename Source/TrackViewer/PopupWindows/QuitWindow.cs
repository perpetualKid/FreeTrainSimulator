
using System;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common.Info;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;

namespace Orts.TrackViewer.PopupWindows
{
    public class QuitWindow : WindowBase
    {
        private Label quitButton;

        public event EventHandler OnQuitGame;

        public QuitWindow(WindowManager owner) : 
            base(owner, CatalogManager.Catalog.GetString($"Quit {RuntimeInfo.ApplicationName}"), new Point(200, 200), new Point(200, 100))
        {
        }

        public override bool Modal => true;

        protected override ControlLayout Layout(ControlLayout layout)
        {
            if (null == layout)
                throw new ArgumentNullException(nameof(layout));

            quitButton = new Label(layout.RemainingWidth, 24, "Quit", LabelAlignment.Center);
            quitButton.OnClick += QuitButton_OnClick;
            layout = base.Layout(layout);
            layout.AddSpace(0, 12);
            layout.Add(quitButton);
            return layout;
        }

        private void QuitButton_OnClick(object sender, MouseClickEventArgs e)
        {
            OnQuitGame.Invoke(this, EventArgs.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                quitButton?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
