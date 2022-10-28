using System;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Toolbox.Settings;

namespace Orts.Toolbox.PopupWindows
{
    public class HelpWindow : FramedWindowBase
    {
        public HelpWindow(WindowManager owner, Point relativeLocation) :
            base(owner ?? throw new ArgumentNullException(nameof(owner)), "Help", relativeLocation, new Point(360, 134))
        {
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
            layout = layout.AddLayoutScrollboxVertical(layout.RemainingWidth);

            foreach (UserCommand command in EnumExtension.GetValues<UserCommand>())
            {
                ControlLayoutHorizontal line = layout.AddLayoutHorizontalLineOfText();
                int width = line.RemainingWidth / 2;
                line.Add(new Label(this, width, line.RemainingHeight, command.GetLocalizedDescription()));
                line.Add(new Label(this, width, line.RemainingHeight, InputSettings.UserCommands[command]?.ToString()));
            }
            return layout;
        }
    }
}
