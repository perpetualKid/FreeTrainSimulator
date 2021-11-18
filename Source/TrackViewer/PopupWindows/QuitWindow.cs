
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
        private Label cancelButton;
        private Label printScreenButton;

        public event EventHandler OnQuitGame;
        public event EventHandler OnQuitCancel;
        public event EventHandler OnPrintScreen;

        public QuitWindow(WindowManager owner, Point location) : 
            base(owner, CatalogManager.Catalog.GetString($"Exit {RuntimeInfo.ApplicationName}"), location, new Point(200, 100))
        {
        }

        public override bool Modal => true;

        protected override ControlLayout Layout(ControlLayout layout)
        {
            if (null == layout)
                throw new ArgumentNullException(nameof(layout));

            quitButton = new Label(layout.RemainingWidth/2, 24, "Quit", LabelAlignment.Center);
            quitButton.OnClick += QuitButton_OnClick;
            cancelButton = new Label(layout.RemainingWidth/2, 24, "Cancel", LabelAlignment.Center);
            cancelButton.OnClick += CancelButton_OnClick;
            layout = base.Layout(layout);
            layout.AddSpace(0, 12);
            ControlLayout buttonLine = layout.AddLayoutHorizontal(24);
            buttonLine.Add(quitButton);
            buttonLine.AddVerticalSeparator();
            buttonLine.Add(cancelButton);
            layout.AddHorizontalSeparator();
            printScreenButton = new Label(layout.RemainingWidth, 24, "Take Screenshot", LabelAlignment.Center);
            printScreenButton.OnClick += PrintScreenButton_OnClick;
            layout.Add(printScreenButton);
            return layout;
        }

        private void PrintScreenButton_OnClick(object sender, MouseClickEventArgs e)
        {
            Close();
            Owner.Game.RunOneFrame();// allow the Window to be closed before taking a screenshot
            OnPrintScreen?.Invoke(this, e);
        }

        private void CancelButton_OnClick(object sender, MouseClickEventArgs e)
        {
            Close();
            OnQuitCancel?.Invoke(this, e);
        }

        private void QuitButton_OnClick(object sender, MouseClickEventArgs e)
        {
            OnQuitGame.Invoke(this, e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                quitButton?.Dispose();
                cancelButton?.Dispose();
                printScreenButton?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
