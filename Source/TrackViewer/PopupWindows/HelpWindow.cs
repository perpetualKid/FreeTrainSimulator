using System;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.TrackViewer.Settings;

namespace Orts.TrackViewer.PopupWindows
{
    public class HelpWindow : WindowBase
    {
        public HelpWindow(WindowManager owner, Point relativeLocation) :
            base(owner ?? throw new ArgumentNullException(nameof(owner)), CatalogManager.Catalog.GetString("Help"),
                relativeLocation, new Point(owner.DefaultFontSize * 30, (int)(owner.DefaultFontSize * 8.5f + 20)))
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
#pragma warning disable CA2000 // Dispose objects before losing scope
                line.Add(new Label(this, width, line.RemainingHeight, command.GetLocalizedDescription()));
                line.Add(new Label(this, width, line.RemainingHeight, InputSettings.UserCommands[command]?.ToString()));
#pragma warning restore CA2000 // Dispose objects before losing scope
            }
            return layout;
        }
    }
}
