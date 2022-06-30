using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Graphics;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;

namespace Orts.Toolbox.PopupWindows
{
    internal class LoggingWindow : WindowBase
    {
        private readonly string logText;

        public LoggingWindow(WindowManager owner, string logFile, Point relativeLocation, Catalog catalog = null) : 
            base(owner, "Logging", relativeLocation, new Point(400, 300), catalog)
        {
            if (File.Exists(logFile))
            {
                using (FileStream stream = File.Open(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (StreamReader reader = new StreamReader(stream))
                        logText = reader.ReadToEnd();
                }
            }
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);
//            layout = layout.AddLayoutScrollboxVertical(layout.RemainingWidth);

            layout.Add(new Label(this, 0, 0, layout.RemainingWidth, layout.RemainingHeight, logText, Graphics.HorizontalAlignment.Left, FontManager.Scaled("Courier New", System.Drawing.FontStyle.Regular)[12], Color.AliceBlue));
            return layout;
        }
    }
}
