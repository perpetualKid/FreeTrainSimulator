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

namespace Orts.Toolbox.PopupWindows
{
    internal class TrackItemInfoWindow : WindowBase
    {
        private ContentArea contentArea;
        private readonly UserCommandController<UserCommand> userCommandController;

        public TrackItemInfoWindow(WindowManager owner, ContentArea contentArea, Point relativeLocation, Catalog catalog = null) : 
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Track Item Information"), relativeLocation, new Point(240, 202), catalog)
        {
            this.contentArea = contentArea;
            userCommandController = Owner.UserCommandController as UserCommandController<UserCommand>;
        }

        internal void GameWindow_OnContentAreaChanged(object sender, ContentAreaChangedEventArgs e)
        {
            contentArea = e.ContentArea;
        }

    }
}
