using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common.Input;
using Orts.Graphics.MapView;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;

namespace Orts.Toolbox.PopupWindows
{
    internal class TrackItemInfoWindow : WindowBase
    {
        private ContentArea contentArea;
        private readonly UserCommandController<UserCommand> userCommandController;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private NameValueTextGrid trackItemInfoGrid;
#pragma warning restore CA2213 // Disposable fields should be disposed

        public TrackItemInfoWindow(WindowManager owner, ContentArea contentArea, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Track Item Information"), relativeLocation, new Point(240, 202), catalog)
        {
            this.contentArea = contentArea;
            userCommandController = Owner.UserCommandController as UserCommandController<UserCommand>;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);

            layout = layout.AddLayoutVertical();
            trackItemInfoGrid = new NameValueTextGrid(this, 0, 0, layout.RemainingWidth, layout.RemainingHeight)
            {
                InformationProvider = (contentArea?.Content as ToolboxContent)?.TrackItemInfo,
                ColumnWidth = new int[] { layout.RemainingWidth / 3, layout.RemainingWidth / 3 * 2 },
            };
            layout.Add(trackItemInfoGrid);
            return layout;

        }
        internal void GameWindow_OnContentAreaChanged(object sender, ContentAreaChangedEventArgs e)
        {
            contentArea = e.ContentArea;
            trackItemInfoGrid.InformationProvider = (contentArea?.Content as ToolboxContent)?.TrackItemInfo;
        }

    }
}
