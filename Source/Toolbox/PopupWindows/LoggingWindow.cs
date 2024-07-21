using System.IO;

using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Window.Controls;
using FreeTrainSimulator.Graphics.Window.Controls.Layout;

using GetText;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Toolbox.PopupWindows
{
    internal sealed class LoggingWindow : WindowBase
    {
        private readonly string logText;

        public LoggingWindow(WindowManager owner, string logFile, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Logging"), relativeLocation, new Point(-50, 300), catalog)
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

            layout.Add(new TextBox(this, 0, 0, layout.RemainingWidth, layout.RemainingHeight, logText, HorizontalAlignment.Left, false, Owner.TextFontMonoDefault, Color.White));
            return layout;
        }
    }
}
