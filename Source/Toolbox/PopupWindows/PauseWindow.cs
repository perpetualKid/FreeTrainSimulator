
using System;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common.Info;
using Orts.Graphics;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Toolbox.Control;

namespace Orts.Toolbox.PopupWindows
{
    public class PauseWindow : WindowBase
    {
        private Label headerLabel;
        private Label okButton;
        private Label printScreenButton;
        private Label pauseLabel;


        public event EventHandler OnQuitCancel;
        public event EventHandler OnPrintScreen;

      


        public string pauseText { get => pauseLabel?.Text; set => pauseLabel.Text = value; }

        public PauseWindow(WindowManager owner, Point relativeLocation) :
             base(owner, "Pause", relativeLocation, new Point(600, 110))
        {
            Modal = true;
            ZOrder = 100;
        }

        protected override ControlLayout Layout(ControlLayout layout)
        {
            if (null == layout)
                throw new ArgumentNullException(nameof(layout));

            okButton = new Label(this, layout.RemainingWidth, Owner.TextFontDefault.Height, CatalogManager.Catalog.GetString("OK"), LabelAlignment.Center);
            okButton.OnClick += CancelButton_OnClick;

            System.Drawing.Font headerFont = FontManager.Scaled(Owner.DefaultFont, System.Drawing.FontStyle.Bold)[(int)(Owner.DefaultFontSize * 2)];
            // Pad window by 4px, add caption and separator between to content area.
            layout = layout?.AddLayoutOffset((int)(4 * Owner.DpiScaling)).AddLayoutVertical() ?? throw new ArgumentNullException(nameof(layout));
            headerLabel = new Label(this, 0, 0, layout.RemainingWidth, headerFont.Height, Caption, LabelAlignment.Center, headerFont, Color.White);
            layout.Add(headerLabel);
            layout.AddHorizontalSeparator(false);
            pauseLabel = new Label(this, 0, 0, layout.RemainingWidth, Owner.TextFontDefault.Height, pauseText, LabelAlignment.Center, Owner.TextFontDefault, Color.Orange);
            layout.Add(pauseLabel);
            ControlLayout buttonLine = layout.AddLayoutHorizontal((int)(Owner.TextFontDefault.Height * 1.25));
            buttonLine.Add(okButton);
            layout.AddHorizontalSeparator(false);
            printScreenButton = new Label(this, layout.RemainingWidth, Owner.TextFontDefault.Height, CatalogManager.Catalog.GetString($"Take Screenshot ({InputSettings.UserCommands[UserCommand.PrintScreen]})"), LabelAlignment.Center);
            printScreenButton.OnClick += PrintScreenButton_OnClick;
            layout.Add(printScreenButton);
            return layout;
        }

        public override bool Open()
        {
            return base.Open();
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

    }
}
