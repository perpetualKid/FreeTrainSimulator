using System;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;

namespace Orts.ActivityRunner.Viewer3D.Dispatcher.PopupWindows
{
    internal class HelpWindow: WindowBase
    {
        public HelpWindow(WindowManager owner, Point relativeLocation, Catalog catalog = null) :
            base(owner ?? throw new ArgumentNullException(nameof(owner)), (catalog ??= CatalogManager.Catalog).GetString("Help"), relativeLocation, new Point(360, 125), catalog)
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
