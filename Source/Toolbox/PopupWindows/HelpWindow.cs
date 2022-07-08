using System;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Toolbox.Settings;

namespace Orts.Toolbox.PopupWindows
{
    public class HelpWindow : WindowBase
    {
        public HelpWindow(WindowManager owner, Point relativeLocation) :
            base(owner ?? throw new ArgumentNullException(nameof(owner)), "Help", relativeLocation, new Point(360, 125))
        {
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            //just sample/test code for a horizontal scroll layout
            //layout = base.Layout(layout, headerScaling);
            //layout = layout.AddLayoutScrollboxHorizontal(50);//(layout.RemainingHeight);

            //foreach (UserCommand command in EnumExtension.GetValues<UserCommand>())
            //{
            //    ControlLayoutVertical column = layout.AddLayoutVertical(120);
            //    int width = 100;//column.RemainingWidth / 2;
            //    column.Add(new Label(this, width, 16, command.GetLocalizedDescription()));
            //    column.Add(new Label(this, width, 16, InputSettings.UserCommands[command]?.ToString()));
            //}
            //return layout;

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
