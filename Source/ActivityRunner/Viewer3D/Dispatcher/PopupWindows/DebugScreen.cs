
using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Window.Controls;
using FreeTrainSimulator.Graphics.Window.Controls.Layout;
using FreeTrainSimulator.Graphics.Xna;

using GetText;

using Microsoft.Xna.Framework;

namespace Orts.ActivityRunner.Viewer3D.Dispatcher.PopupWindows
{
    public enum DebugScreenInformation
    {
        Common,
    }

    public class DebugScreen : OverlayBase
    {
        private readonly EnumArray<NameValueTextGrid, DebugScreenInformation> currentProvider = new EnumArray<NameValueTextGrid, DebugScreenInformation>();

        public DebugScreen(WindowManager owner, Color backgroundColor, Catalog catalog = null) :
            base(owner, catalog ?? CatalogManager.Catalog)
        {
            ZOrder = 0;
            currentProvider[DebugScreenInformation.Common] = new NameValueTextGrid(this, (int)(10 * Owner.DpiScaling), (int)(30 * Owner.DpiScaling));
            UpdateBackgroundColor(backgroundColor);
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling)
        {
            foreach (NameValueTextGrid item in currentProvider)
            {
                layout?.Add(item);
            }
            return base.Layout(layout, headerScaling);
        }

        public void SetInformationProvider(DebugScreenInformation informationType, INameValueInformationProvider provider)
        {
            currentProvider[informationType].InformationProvider = provider;
        }

        public void UpdateBackgroundColor(Color backgroundColor)
        {
            foreach(NameValueTextGrid item in currentProvider)
                item.TextColor = backgroundColor.ComplementColor();
        }
    }
}
